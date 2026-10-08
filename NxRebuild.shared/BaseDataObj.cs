using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Transactions;
using System.Xml.Linq;

namespace NxRebuild.shared {
  enum NxLocationKind
{
    Server,
    Client,
    Both
    }
    //　サーバー同期用のJSON構造体
    public enum OperationType {
        Save,
        Delete,
        Rename,
        Sync,
        Import,
        LocalEdit
    }

    public class TableJson {
        public string Table { get; set; }
        public List<Dictionary<string, object>> Rows { get; set; }
    }


    [Flags]
    public enum NxDataType {
        root = 0,
        Folder = 1,

        //ここから下は使われるシステムに合わせて作成する。必要なければ削除してOK
        Zairyou = 2,
        Ryouri = 4,
        Meal = 8,
        Kondate = 16,
        Calendar = 32,
        Person = 64,
        InstMeals = 128, //給食'Institutional meals'
        RyouriRow = 256//料理内材料

    }


    public interface IBaseDataObj<TKey> {
        Guid CurrUsrID { get; set; }
        TKey DataID { get; set; }
        string DataName { get; }
        NxDataType DataType { get; set; }
        IDbConnection DBcon { get; set; }
        string ParentIDColName { get; }
        TKey ParentID {  get; set; }
        IBaseDataObj<TKey>? ParentDataObj { get; set; }
        string IdColName { get; }
        string InfoTbl { get; }
        DateTime LockedAt { get;  }
        Guid LockerID { get; }
        string NameColName { get; }
        string S_TblName { get; }
        object SelfObjMgr { get; set; }
        string TblName { get; }
        Guid TenantCode { get; set; }
        DateTime Update_at { get;  }
        string W_TblName { get;  }
        string Ws_TblName { get;  }
        void CreateWorkingMemory(Dictionary<string, object?> workingRaw);
        void CreateWorkingSubTables(List<List<Dictionary<string, object?>>> workingSubList);
        Task<LockStatus> DataOpen();
        Task<LockStatus> DataClose();
        string TblToJson();
        string TblToJson(TKey id, IDbTransaction tran);
        Task<bool> JsonToTbl(string json, IDbTransaction tran);
        Task<bool> ReName(string newName);
        Task<bool> SaveAsync(
                        Dictionary<string, object?> workingRaw,
                        List<List<Dictionary<string, object?>>>? subTables = null);

        void Validate(OperationType op);
        Task<LockStatus> SetLockAsync(); 
        Task<LockStatus> SetLockAsync(LockStatus lockStatus,
                                        IDbTransaction dbTransaction = null);
    }


    public abstract class BaseDataObj<TKey> : IBaseDataObj<TKey> {

        // 【変更】レコード内容をJSON（辞書）として保持するメンバ
        public Dictionary<string, object> _rawData = new();

        protected string _nameColName; //テーブルのデータ名カラムのカラム名
        protected string _idColName;//テーブルのIDカラムのカラム名
        protected string _parentIDColName;//テーブルの親IDカラムのカラム名
        protected string _tblName; //データ名等基本データが格納されるテーブル名
        protected string _s_tblName;//材料など詳細データが格納されるテーブル名
        protected string _infoTbl;//_tblNameに加え栄養素などの集計結果が入っているテーブル

        //ワークテーブル名、ワークテーブルを持たない場合は
        //_tblName,_s_tblName
        //と同じ値を設定すること
        protected string _w_tblName;
        protected string _ws_tblName;

        public IBaseDataObj<TKey>? ParentDataObj { get; set; }
        public string NameColName => _nameColName;
        public string IdColName => _idColName;
        public string ParentIDColName => _parentIDColName;
        public string TblName => _tblName;
        public string S_TblName => _s_tblName;
        public string InfoTbl => _infoTbl;
        public string W_TblName => _w_tblName;
        public string Ws_TblName => _ws_tblName;

        protected NxDataType _datatype;
        protected DateTime _update_at;
        protected Guid _locker_ID;
        protected DateTime _locked_at;

        public object SelfObjMgr { get; set; }
        public IDbConnection DBcon { get; set; }

      //
public NxLocationKind LocationKind
{
    get
    {
        // DB 接続文字列を取得（Nx の基盤ならここで取れる）
        var conn = NxDbConnectionProvider.CurrentConnectionString;

        if (string.IsNullOrEmpty(conn))
            return NxLocationKind.Client; // WASM / MAUI の in-memory はここに来る

        // memory が含まれていればクライアント世界線
        if (conn.Contains("memory", StringComparison.OrdinalIgnoreCase))
            return NxLocationKind.Client;

        // それ以外はサーバー世界線
        return NxLocationKind.Server;
    }
}

        public Guid TenantCode {
            get => _rawData.TryGetValue("tenant_code", out var v)
                ? (v is Guid g ? g : Guid.Parse(v.ToString()!))
                : Guid.Empty;

            set => _rawData["tenant_code"] = value;   
        }

        

        public TKey DataID {
            get => (TKey)_rawData[_idColName];
            set => _rawData[_idColName] = value;
        }

        public string DataName {
            get => (string)_rawData[_nameColName];
        }

        public TKey ParentID {
            get {
                // 親ID列が存在しない
                if (string.IsNullOrEmpty(_parentIDColName))
                    return default(TKey); // intなら0、stringならnull

                // 親ID列がある
                if (_rawData.TryGetValue(_parentIDColName, out var v) && v != null)
                    return (TKey)v;

                return default(TKey);
            }

            set {
                // 親ID列が存在しない線 → 無効化
                if (string.IsNullOrEmpty(_parentIDColName))
                    return;

                // 親ID列がある → 通常処理
                _rawData[_parentIDColName] = value;
            }
        }


        public NxDataType DataType {
            get => _datatype;
            set => _datatype = value;
        }

        public DateTime Update_at {
            get {
                try {
                    if (_rawData.TryGetValue("update_at", out var v) && v != null && v is not DBNull)
                        return Convert.ToDateTime(v);
                } catch {
                    // 苦肉の策：
                    // DapperRow / ExpandoObject / CreateEmptyRow の型揺れで
                    // 変換不能な値が来ることがあるため、例外は正常系として扱う。
                    // 変換できなければ MinValue にフォールバックする。
                    // 以下同じ
                }

                return DateTime.MinValue;
            }
        }

        public DateTime LockedAt {
            get {
                try {
                    if (_rawData.TryGetValue("locked_at", out var v) && v != null && v is not DBNull)
                        return Convert.ToDateTime(v);
                } catch { }

                return DateTime.MinValue;
            }
        }

        public Guid LockerID {
            get {
                try {
                    if (_rawData.TryGetValue("locked_by", out var v) && v != null && v is not DBNull)
                        return Guid.Parse(v.ToString());
                } catch { }

                return Guid.Empty;
            }
        }

        public bool Opened {  get; set; }

        public bool Edited {  get; set; }

        //参照しているユーザーのID
        //インスタンス作成後に必ずセットする事
        public Guid CurrUsrID { get; set; }




        // この中は派生先で実装する事。
        //ここで固定のテーブル名やNameカラム名などのプロパティを設定する
        public BaseDataObj() {

        }

/// <summary>
/// 【役割】
///   - このエンティティに関する業務ロジック（削除禁止・名前重複禁止など）を
///     “エンティティ自身” に閉じ込めるための唯一の入口。
///   - Nx 基盤側は、Save / Delete / Rename / Sync / Import / LocalEdit など
///     すべての操作の直前で必ずこのメソッドを呼び出し、
///     「この操作（OperationType）を実行してよいか？」を判定する。
///   - 具象クラスは必要に応じて override し、
///     自身固有の規則を NxValidator に追加することで、
///     正本・ローカル・同期の各世界線で同じ規則が発火する。
///
/// 【設計思想】
///   - 規則違反時は例外を投げず、NxValidationResult を返すことで
///     世界線の外へ例外を漏らさずに因果を閉じる。
///   - NxValidationResult は ErrorCode と ErrorMessage を持ち、
///     UI・API・Sync のどの世界線でも同じ形式で扱える。
///   - BaseDataObj は抽象核であり、規則の中身は NxValidator に委譲する。
///     これにより循環依存を避け、抽象構造を汚さない。
///
/// 【具象側の書き方例】
///     var result = Validate(OperationType.Delete);
///     if (!result.IsValid)
///         return result;   // 規則違反を呼び出し元へ返す
///
/// 【発火ポイント】
///   - Save / Delete / Rename / Import / Sync など、
///     正本世界線に影響する操作の直前で必ず発火する。
///   - WASM ローカル側の CRUD（working テーブル操作）は「作業用世界線」であり、
///     正本ではないため Validate は発火しない。
///   - クライアント側で唯一 Validate が発火するのは DataOpen（編集開始）時で、
///     これはロック状態と整合性を取るための軽量チェックに限定される。
///
/// 【結果】
///   - 正本の整合性は NxValidator によって一元的に保証され、
///     クライアント側は高速なローカル編集に専念できる。
///   - すべての世界線（Base / Sync / API / UI）が同じ NxValidationResult を共有し、
///     規則が漏れず、二重発火せず、整合性が保たれる。
/// </summary>
public virtual NxValidationResult Validate(OperationType op, object? arg = null)
{
    // ★ BaseDataObj の値をコピーした Validator を生成（循環依存なし）
    var validator = new NxValidator<TKey>(this);

    try
    {
        return op switch
        {
            OperationType.Save =>
                validator.ValidateSave(),

            OperationType.Delete =>
                validator.ValidateDelete(),

            OperationType.Rename =>
                arg is string newName
                    ? validator.ValidateRename(newName)
                    : NxValidationResult.Fail(
                        NxValidationErrorCode.Unknown,
                        "Rename の引数が不正です"),

            OperationType.Sync =>
                validator.ValidateSync(),

            OperationType.Import =>
                validator.ValidateImport(),

            OperationType.LocalEdit =>
                validator.ValidateLocalEdit(),

            _ =>
                NxValidationResult.Fail(
                    NxValidationErrorCode.Unknown,
                    $"未定義の OperationType: {op}")
        };
    }
    catch (Exception ex)
    {
        // ★ BaseDataObj 側では例外を外に漏らさず NxValidationResult に変換
        return NxValidationResult.Fail(
            NxValidationErrorCode.Unknown,
            $"Validate 内部例外: {ex.Message}");
    }
}
        //public async Task<bool> ApplySync() {
        //    
        //    // SyncQueryExec は具象側
        //    return await SyncQueryExec();
        //}

        public virtual void SetAsRoot(string RootName, NxDataType DataType = NxDataType.root) {
            _rawData[_nameColName] = RootName;
            _datatype = DataType;
        }

        public virtual void Setproperties(IDictionary<string, object> record)
        {
            // ---------------------------------------------------------
            // ★ NxTypeMapper による「型の正本化」
            //   - JSON の Number(double/long) → 正しい型へ
            //   - SQLite の INTEGER(long) → int/long に矯正
            //   - datetime（マイクロ秒対応）もここで正しく変換
            //   - bool / string も型マップに従って正本化
            // ---------------------------------------------------------
            var normalized = NxTypeMapper.ConvertRow(TblName, record.ToDictionary(k => k.Key, v => v.Value));

            // 正本化された辞書をそのまま保持
            _rawData = normalized;
        }


        public void CreateWorkingMemory(
            Dictionary<string, object?> workingRaw) {
            // Raw の Deep Copy
            workingRaw.Clear();
            foreach (var kv in _rawData)
                workingRaw[kv.Key] = kv.Value;

            return;
        }

        public virtual void CreateWorkingSubTables(
                List<List<Dictionary<string, object?>>> workingSubList) {
            // 抽象側は何もしない
            // 具象側が必要なときだけ override する(zmstEntity参照）
            return;
        }


        // テーブルからデータを取得してJSON文字列にする(ロード用
        public string TblToJson() {
            return TblToJson(this.DataID,null);
        }


        // テーブルからデータを取得してJSON文字列にする（保存処理中に使用する、トランザクション付き）
        public string TblToJson(TKey id, IDbTransaction tran) {
            var sqlList = CreateJSONsql();
            var results = new List<object>();

            foreach (var (tableName, sql) in sqlList) {
                var r = DBcon.Query<dynamic>(
                    sql,
                    new { dataID = id, tenantCode = TenantCode },
                    tran
                ).ToList();

                results.Add(new { Table = tableName, Rows = r });
            }

            return JsonSerializer.Serialize(results);
        }




        protected abstract IEnumerable<(string tableName, string sql)> CreateJSONsql();
        //{ JSON生成用のビュー。以下実装例        
        //    yield return (_tblName,
        //        $@"SELECT * FROM ""{_tblName}"" 
        //            WHERE ""{_idColName}"" = @dataID AND tenant_code = @tenantCode");

        //    yield return (_s_tblName,
        //        $@"SELECT * FROM ""{_s_tblName}"" 
        //            WHERE ""LocalCode"" = @dataID AND tenant_code = @tenantCode");

        //    続けてサブテーブルがある場合は同様に yield return で追加する

        //}

        public async Task<bool> JsonToTbl(string json, IDbTransaction tran) {

            this.Validate(OperationType.Import);

            var tables = JsonSerializer.Deserialize<List<TableJson>>(json);

            var result = await DeleteQueryExec(tran);
            if (!result) return false;

            try {
                foreach (var table in tables) {
                    foreach (var row in table.Rows) {
                        var normalized = NxTypeMapper.ConvertRow(table.Table, row);


                        var columns = string.Join(", ", normalized.Keys.Select(k => $@"""{k}"""));
                        var values = string.Join(", ", normalized.Keys.Select(k => $@"@{k}"));

                        DBcon.Execute(
                            $@"INSERT INTO ""{table.Table}"" ({columns}) VALUES ({values})",
                            normalized,
                            tran
                        );


                    }
                }


                return true;
            } catch {
                return false;
            }
        }


        // ---------------------------------------------------------
        // DataOpen（編集開始前処理）
        // ---------------------------------------------------------
        public virtual Task<LockStatus> DataOpen() {


            Opened = true;//編集中フラグをON

            // --- ローカル編集開始なのでロックは常に false ---
            return Task.FromResult(new LockStatus {
                Exists = true,
                IsLocked = true
            });
        }

        // ---------------------------------------------------------
        // DataClose(編集終了）
        // フラグのセットのみ。UI側でSaveまたはRestoreを呼んだうえで
        // DataCloseを呼ぶこと
        // ---------------------------------------------------------
        public virtual Task<LockStatus> DataClose() {
            Opened = false; //編集中フラグをOFF
            return Task.FromResult(new LockStatus {
                Exists = true,
                IsLocked = false
            });
        }



        public abstract Task<bool> DeleteQueryExec(IDbTransaction transaction);
        // データベースからエンティティを物理削除する。
        // 派生先では TblName テーブルおよび関連するサブテーブル（s_tblName など）を完全削除すること。

        public virtual async Task<bool> SoftDeleteQueryExec(IDbTransaction transaction){
            // ※ API 層からのみ呼び出す。
            // 論理削除を実装する：
            //   - TblName テーブルのレコードを「削除済み」と扱える状態にする
            //     （例：Name をクリア、Parent カラムを NULL にする、Updated_at を更新する）。
            //   - サブテーブル以下の関連レコードは物理削除する。
            // UI では、この論理削除状態を参照して「削除済みデータ」を判定する。
            // 実装時は挙動に注意すること。
            return await Task.FromResult(true);
        }



        // 名前変更の検証メソッド
        // 必要に応じて派生クラスでオーバーライドできるように virtual にしておく
        public virtual async Task<bool> ReName(string newName) {

            // 1. バリデーション
            if (string.IsNullOrWhiteSpace(newName) || newName.Length > 20) {
                return false;
            }

            IDbTransaction transaction = DBcon.BeginTransaction();
            if (await ReNameQueryExec(newName, transaction)) {
                transaction.Commit();
                await Updateproperties();
                return true;
            }
            transaction.Rollback();
            return false;
        }

        //DBへのデータ名変更を試みる。成功した場合プロパティの値も書き換える
        public virtual async Task<bool> ReNameQueryExec(string newName, IDbTransaction dbTransaction) {
            try {
                // サーバー側でマイクロ秒精度の UTC を生成
                var updateAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");

                string sql = $@"
                                UPDATE ""{_tblName}""
                                SET ""{_nameColName}"" = @name,
                                    ""update_at"" = @update_at
                                WHERE ""{_idColName}"" = @id
                                  AND ""tenant_code"" = @tenantCode;
                            ";

                return await DBcon.ExecuteAsync(sql,
                    new {
                        name = newName,
                        id = DataID,
                        update_at = updateAt,
                        tenantCode = TenantCode
                    },
                    dbTransaction) > 0;

            } catch (Exception ex) {
                Console.WriteLine($"RenameQueryExec Error: {ex.Message}");
                return false;
            }
        }


        public virtual async Task Updateproperties() {
            try {
                string sql = $@"
                                SELECT *
                                FROM ""{_tblName}""
                                WHERE ""{_idColName}"" = @DataID
                                  AND ""tenant_code"" = @TenantCode;
                            ";

                var record = await DBcon.QueryFirstOrDefaultAsync<dynamic>(sql, new {
                    DataID = this.DataID,
                    TenantCode = this.TenantCode
                });


                if (record != null) {
                    var dict = ((IDictionary<string, object>)record)
                        .ToDictionary(k => k.Key, v => v.Value);

                    var normalized = NxTypeMapper.ConvertRow(_tblName, dict);

                    Setproperties(normalized);
                } else {
                    Console.WriteLine($"[Nx] Updateproperties: レコードなし {_tblName} DataID={DataID}");
                }
            } catch (Exception ex) {
                Console.WriteLine($"[Nx] Updateproperties Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        // =======================================================
        // 保存の時にDataIDを確定させる場合はここにその処理を記述(ZmstEntity参照)
        // =======================================================

        public virtual TKey? EnsureIDForSave(IDbTransaction tran){
            return this.DataID;
        }
        // =======================================================
        // 保存の標準実
        // =======================================================

        public virtual async Task<bool> SaveAsync(
            Dictionary<string, object?> workingRaw,
            List<List<Dictionary<string, object?>>>? subTables = null)
        {

            using var tran = DBcon.BeginTransaction();

            try
            {
                if (!await DeleteQueryExec(tran))
                {
                    tran.Rollback();
                    return false;
                }


                workingRaw[IdColName] = EnsureIDForSave(tran);
        
      
                // ★ Working 全体保存（メイン＋サブ）
                if (!await SaveWorkingAsync(workingRaw, subTables, tran))
                {
                    tran.Rollback();
                    return false;
                }

                // ★ 継承先で追加の確定処理
                if (!await SaveQueryExec(tran))
                {
                    tran.Rollback();
                    return false;
                }

                // ★ コミット
                tran.Commit();

                // ★ 正本 Raw に反映
                ApplyWorkingToRaw(workingRaw);

                return true;
            } 
            catch (Exception ex) 
            {
                        Console.WriteLine("BaseDataObj.SaveAsync ERROR:");
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                        tran.Rollback();
                return false;
            }
        }

        public virtual async Task<bool> SaveWorkingAsync(
            Dictionary<string, object?> workingRaw,
            List<List<Dictionary<string, object?>>>? subTables,
            IDbTransaction tran)
        {
            // ★ メインテーブル保存
            if (!await SaveWorkingMainAsync(workingRaw, tran))
                return false;

            // ★ サブテーブル保存（具象側で追加）
            if (!await SaveWorkingSubAsync(subTables, workingRaw, tran))
                return false;

            return true;
        }
        // =======================================================
        // メインテーブル保存（WorkingRaw → _w_tblName）
        // =======================================================

        public virtual async Task<bool> SaveWorkingMainAsync(
            Dictionary<string, object?> workingRaw,
            IDbTransaction tran)
        {

            //ローカル書き込み用なのでとりあえずPC時間で
            workingRaw["Update_at"] = DateTime.UtcNow;

            // ★ 次に INSERT（WorkingRaw をそのまま書き込む）
                    var cols = new List<string>();
            var vals = new List<string>();

            foreach (var kv in workingRaw)
            {
                cols.Add($@"""{kv.Key}""");
                vals.Add($@"@{kv.Key}");
            }

            string insSql = $@"
                INSERT INTO ""{_w_tblName}""
                ({string.Join(", ", cols)})
                VALUES ({string.Join(", ", vals)});
            ";

            var rows = await DBcon.ExecuteAsync(insSql, workingRaw, tran);
            return rows == 1;
        }


        // =======================================================
        // サブテーブル保存（DELETE → INSERT 再構築）
        // =======================================================
        public virtual Task<bool> SaveWorkingSubAsync(
            List<List<Dictionary<string, object?>>>? subTables,
            Dictionary<string, object?> MainWorkingRaw,
            IDbTransaction tran)
        {
            // ★ スモールエンティティはサブ無しが基本
            return Task.FromResult(true);
        }



        // =======================================================
        // 正本 Raw に反映
        // =======================================================

        public void ApplyWorkingToRaw(Dictionary<string, object?> workingRaw)
        {
            _rawData.Clear();
            foreach (var kv in workingRaw)
                _rawData[kv.Key] = kv.Value;
        }


        // =======================================================
        // 継承先で必ず実装する確定処理
        /// <summary>
        /// 扱うエンティティが単一レコードが主になると思われる為、ピュアバーチャルではなく
        /// virtual とし、デフォルト実装は true を返すだけとする。
        /// 具象クラスが複数テーブル・複数レコードを扱う場合は、
        /// ワークテーブルを同じスキーマで作成し、
        /// そちらから本テーブルへ書き込む処理をここに記述する。
        /// </summary>
        /// 
        // =======================================================
        public virtual Task<bool> SaveQueryExec(IDbTransaction transaction)
        {
            return Task.FromResult(true);
        }

        public virtual async Task<LockStatus> SetLockAsync() {
            var lockStatus = new LockStatus {
                IsLocked = true,
                LockedByUserId = CurrUsrID,
                Locked_at = DateTime.UtcNow
            };
            return await SetLockAsync(lockStatus);
        }

        // =======================================================
        //データロックメソッド。
        //ロックされてるか確認したくなってもロックが目的なので意味
        //が無いのでこれを呼び出せ。
        // ======================================================
        public virtual async Task<LockStatus> SetLockAsync(LockStatus request, IDbTransaction dbTransaction = null)
{
    if (dbTransaction == null)
        dbTransaction = DBcon.BeginTransaction();

    // ---- 現在のロック状態を取得 ----
    var current = await LockedChkfromTbl(dbTransaction);
    current.CurrUserId = request.CurrUserId;

    // ---- 他人ロック中（編集不可） ----
    if (current.IsLockedForEdit)
    {
        current.HasError = true;
        current.ErrorMessage = "他のユーザーがロック中です。";

        _rawData["locked_at"] = current.Locked_at ?? DateTime.MinValue;
        _rawData["locked_by"] = current.LockedByUserId;

        dbTransaction.Commit();
        return current;
    }

    // ---- RecordNone（新規作成） → ロック不要、編集保存可 ----
    if (!current.Exists)
    {
        // 新規作成なのでロック不要
        current.HasError = false;
        current.ErrorMessage = "";

        _rawData["locked_at"] = null;
        _rawData["locked_by"] = null;

        dbTransaction.Commit();
        return current;
    }

    // ---- ロック情報書き込み（自分ロックを確保） ----
    var writeResult = await WriteLockInfoAsync(request, dbTransaction);

    // WriteLockInfoAsync は LockStatus を返す
    writeResult.CurrUserId = request.CurrUserId;

    // ---- DBエラー ----
    if (writeResult.HasError)
    {
        dbTransaction.Rollback();
        return writeResult;
    }

    // ---- 他人ロック（書き込み不可） ----
    if (writeResult.Result == LockResult.LockedByOther)
    {
        writeResult.HasError = true;
        writeResult.ErrorMessage = "他のユーザーがロック中です。";

        dbTransaction.Commit();
        return writeResult;
    }

    // ---- RecordNone（新規作成） ----
    if (writeResult.Result == LockResult.RecordNone)
    {
        // 新規作成なのでロック不要
        dbTransaction.Rollback();
        return writeResult;
    }

    // ---- Success（自分ロック成立） ----
    var latest = await LockedChkfromTbl(dbTransaction);
    latest.CurrUserId = request.CurrUserId;

    _rawData["locked_at"] = latest.Locked_at ?? DateTime.MinValue;
    _rawData["locked_by"] = latest.LockedByUserId;

    dbTransaction.Commit();
    return latest;
        }
      
protected virtual async Task<LockStatus> WriteLockInfoAsync(LockStatus lockStatus, IDbTransaction transaction)
{
    var expiryTime = DateTime.UtcNow.AddMinutes(-10);

    string sql = $@"
        UPDATE ""{TblName}""
        SET ""locked_by"" = @userId,
            ""locked_at"" = @lockedAt
        WHERE ""{IdColName}"" = @dataID
          AND ""tenant_code"" = @tenantCode
          AND (""locked_at"" IS NULL OR ""locked_at"" < @expiryTime);
    ";

    try
    {
        int affectedRows = await DBcon.ExecuteAsync(
            sql,
            new {
                userId = lockStatus.LockedByUserId,
                lockedAt = DateTime.UtcNow,
                dataID = DataID,
                tenantCode = TenantCode,
                expiryTime = expiryTime
            },
            transaction
        );

        // ---- ロック成功（自分ロック成立） ----
        if (affectedRows > 0)
        {
            lockStatus.HasError = false;
            lockStatus.ErrorMessage = "";
            return lockStatus;   // Result は Success になる
        }

        // ---- ロックできなかったので、現在の状態を確認 ----
        var currentStatus = await LockedChkfromTbl(transaction);
        currentStatus.CurrUserId = lockStatus.CurrUserId;

        // ---- レコード無し（RecordNone） ----
        if (!currentStatus.Exists)
        {
            currentStatus.HasError = false;
            currentStatus.ErrorMessage = "";
            return currentStatus; // Result = RecordNone
        }

        // ---- 他人ロック中（編集不可） ----
        if (currentStatus.IsLockedForEdit)
        {
            currentStatus.HasError = true;
            currentStatus.ErrorMessage = "他のユーザーがロック中です。";
            return currentStatus; // Result = LockedByOther
        }

        // ---- ここまで来たらロックできない理由は DBエラー扱い ----
        currentStatus.HasError = true;
        currentStatus.ErrorMessage = "ロック更新に失敗しました。";
        return currentStatus; // Result = DbError
    }
    catch (Exception ex)
    {
        // ---- catch 時は必ず HasError をセット ----
        lockStatus.HasError = true;
        lockStatus.ErrorMessage = ex.Message;

        // Exists が false の場合は RecordNone として扱われる
        return lockStatus; // Result = DbError
    }
}


        //テーブルからロック情報を読み取って返す。ユーザー名はクライアントで取得して
protected virtual async Task<LockStatus> LockedChkfromTbl(IDbTransaction transaction)
{
    var sql = $@"
        SELECT
            ""locked_at"",
            ""locked_by"" AS ""UserId"",
            ""Update_at""
        FROM ""{TblName}""
        WHERE ""tenant_code"" = @tenantCode
          AND ""{IdColName}"" = @dataID;
    ";

    try
    {
        var result = await DBcon.QueryFirstOrDefaultAsync<dynamic>(
            sql,
            new { tenantCode = TenantCode, dataID = DataID },
            transaction
        );

        // ---- レコード無し（RecordNone） ----
        if (result == null)
        {
            return new LockStatus {
                Exists = false,
                HasError = false,
                ErrorMessage = ""
            };
        }

        // ---- locked_by の GUID パース ----
        Guid lockedByRaw = Guid.Empty;
        if (result.UserId != null)
            Guid.TryParse(result.UserId.ToString(), out lockedByRaw);

        // ---- LockStatus（生データ）を構築 ----
        var lockSt = new LockStatus {
            Exists = true,
            LockedByUserId = lockedByRaw,
            Locked_at = (DateTime?)result.locked_at,
            Update_at = (DateTime?)result.Update_at,
            HasError = false,
            ErrorMessage = ""
        };

        // ---- UI世界線へ反映（rawData） ----
        _rawData["locked_at"] = lockSt.Locked_at ?? DateTime.MinValue;
        _rawData["locked_by"] = lockSt.LockedByUserId;

        return lockSt;
    }
    catch (Exception ex)
    {
        // ---- catch 時は必ず HasError をセット ----
        return new LockStatus {
            Exists = false,
            HasError = true,
            ErrorMessage = ex.Message
        };
    }
}
    }
}
