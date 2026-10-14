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
    public enum NxLocationKind
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

    //BaseDataObjのインターフェース
    //Syncラッパーとの同型性とUI側へ不要な実装の隠蔽のためにインターフェースを作成する
    //やむおえずインターフェースメンバーとしたメソッドはアンダーバーを付けてあるのでaccessしない事
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
        NxLocationKind LocationKind { get; }
        void CreateWorkingMemory(Dictionary<string, object?> workingRaw);
        void CreateWorkingSubTables(List<List<Dictionary<string, object?>>> workingSubList);
        Task<LockStatus> DataOpenAsync();
        Task<LockStatus> DataCloseAsync();
        string TblToJson();
        string TblToJson(TKey id, IDbTransaction tran);
        Task<bool> JsonToTblAsync(string json, IDbTransaction tran);
        Task<bool> ReNameAsync(string newName);
        Task<bool> SaveAsync(
                        Dictionary<string, object?> workingRaw,
                        List<List<Dictionary<string, object?>>>? subTables = null);

        NxValidationResult _Validate(OperationType op, object? arg = null);


    }

    public interface ISharedDataObj<TKey> {
        Task<bool> _DeleteQueryExecAsync(IDbTransaction transaction);
        Task<bool> _SoftDeleteQueryExecAsync(IDbTransaction transaction);
    }
        // -----------------------------------------------------------------
        // BaseDataObj　クライアントとサーバーで直接DBにaccessするオブジェクト
        // 同期機能の拡張用にSyncラッパークラスを使用するため本来protectedと
        // すべきメソッドもパブリックになっているが、その様なメソッドはアンダーバー
        // が名前の先頭にあるためラッパークラス以外からはアクセスしない事
        // -----------------------------------------------------------------
        public abstract class BaseDataObj<TKey> : IBaseDataObj<TKey>, ISharedDataObj<TKey> {

        // レコード内容をJSON（辞書）として保持するメンバ
        //　直接アクセスせずSetProperties()を通してセットすること
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
                var conn = DBcon.ConnectionString;

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

            set => _SetProperties(new Dictionary<string, object> { ["tenant_code"] = value });   
        }

        

        public TKey DataID {
            get => (TKey)_rawData[_idColName];
            set => _SetProperties(new Dictionary<string, object> { [_idColName] = value });
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
                _SetProperties(new Dictionary<string, object> { [_parentIDColName] = value });
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
        ///     var result = _Validate(OperationType.Delete);
        ///     if (!result.IsValid)
        ///         return result;   // 規則違反を呼び出し元へ返す
        ///
        /// 【発火ポイント】
        ///   - Save / Delete / Rename / Import / Sync など、
        ///     正本世界線に影響する操作の直前で必ず発火する。
        ///   - WASM ローカル側の CRUD（working テーブル操作）は「作業用世界線」であり、
        ///     正本ではないため _Validate は発火しない。
        ///   - クライアント側で唯一 _Validate が発火するのは DataOpenAsync（編集開始）時で、
        ///     これはロック状態と整合性を取るための軽量チェックに限定される。
        ///
        /// 【結果】
        ///   - 正本の整合性は NxValidator によって一元的に保証され、
        ///     クライアント側は高速なローカル編集に専念できる。
        ///   - すべての世界線（Base / Sync / API / UI）が同じ NxValidationResult を共有し、
        ///     規則が漏れず、二重発火せず、整合性が保たれる。
        /// </summary>
        public virtual NxValidationResult _Validate(OperationType op, object? arg = null)
        {
            // ★ BaseDataObj の値をコピーした Validator を生成（循環依存なし）
            var validator = new NxValidator<TKey>(this, _rawData);

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
            _SetProperties(new Dictionary<string, object> { [_nameColName] = RootName });
            _datatype = DataType;
        }

        // ---------------------------------------------------------
        // _rawDataが保持するレコードの内容を正本化してセットする
        // セットするカラムは一部だけでもよい。正本化は NxTypeMapper に委譲する。
        // ---------------------------------------------------------
        public virtual void _SetProperties(IDictionary<string, object> record)
        {
            // ---------------------------------------------------------
            // ★ NxTypeMapper による「型の正本化」
            //   - JSON の Number(double/long) → 正しい型へ
            //   - SQLite の INTEGER(long) → int/long に矯正
            //   - datetime（マイクロ秒対応）もここで正しく変換
            //   - bool / string も型マップに従って正本化
            // ---------------------------------------------------------


            // 正本化された辞書をそのまま保持
            // rawData が null の場合は「空の正本」を作る
            if (_rawData == null)
                _rawData = NxTypeMapper.Current.CreateEmptyRow(_tblName);

            foreach (var kvp in record) {
                // 型マップに存在しないカラムは無視（セーブ時にエラーになるため）
                if (!NxTypeMapper.HasColumn(_tblName, kvp.Key))
                    continue;

                // 単一カラムだけ正規化して上書き
                _rawData[kvp.Key] = NxTypeMapper.Convert(_tblName, kvp.Key, kvp.Value);
            }
        }


        public void CreateWorkingMemory(
            Dictionary<string, object?> workingRaw) {
            // Raw の Deep Copy
            workingRaw.Clear();
            foreach (var kv in _rawData)
                workingRaw[kv.Key] = kv.Value;

            return;
        }

        public virtual void CreateWorkingDetailTables(
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

        public async Task<bool> JsonToTblAsync(string json, IDbTransaction tran) {

            this._Validate(OperationType.Import);

            var tables = JsonSerializer.Deserialize<List<TableJson>>(json);

            var result = await _HardDeleteQueryExecAsync(tran);
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
        // DataOpenAsync（編集開始前処理）
        //ローカル専用の派生ではロック機能を使わない。
        //だから DataOpenAsync / DataCloseAsync は null を返せばいい。
        //Sync や API の世界線では LockStatus をちゃんと返すようにしてある。
        // ---------------------------------------------------------
        public virtual Task<LockStatus> DataOpenAsync() {


            Opened = true;//編集中フラグをON

            return Task.FromResult<LockStatus?>(null);
        }

        // ---------------------------------------------------------
        // DataCloseAsync(編集終了）
        // フラグのセットのみ。UI側でSaveまたはRestoreを呼んだうえで
        // DataCloseを呼ぶこと
        // ローカル専用の派生ではロック機能を使わない。
        // DataOpenAsync / DataCloseAsync は null を返せばいい。
        // Sync や API の世界線では LockStatus をちゃんと返す様にしてある。
        // ---------------------------------------------------------
        public virtual async Task<LockStatus> DataCloseAsync() {
            Opened = false; //編集中フラグをOFF
            return await _ReWriteLockInfoAsync(null, null, null); //ロック情報をクリアする
           
        }


public override async Task<bool> -_DeleteQueryExecAsync(IDbTransaction transaction, bool softDelete)
{
    if (soft)
    {
        // ソフトデリート：Primary のみ論理削除
        return await SoftDeletePrimaryQueryExecAsync(transaction);
    }
    else
    {
        // 物理削除：Primary と Detail を完全削除ただしサーバー側の場合はクライアントに削除を伝播させるためプライマリテーブルのみ論理削除とする
        if (LocationKind == NxLocationKind.Server) {
          bool primaryDeleted = await SoftDeletePrimaryQueryExecAsync(transaction);
        }
        else
        {
          bool primaryDeleted = await DeletePrimaryQueryExecAsync(transaction);
        }
        bool detailDeleted  = await DeleteDetailQueryExecAsync(transaction);

        return primaryDeleted && detailDeleted;
    }
}

public virtual async Task<bool> _HardDeleteQueryExecAsync(IDbTransaction transaction)
{
    bool primaryDeleted = await DeletePrimaryQueryExecAsync(transaction);
    bool detailDeleted  = await DeleteDetailQueryExecAsync(transaction);

    return primaryDeleted && detailDeleted;
}
          
protected virtual async Task<bool> -DeletePrimaryQueryExecAsync(IDbTransaction transaction)
{
    try
    {
        string sql = $@"
            DELETE FROM ""{_tblName}""
            WHERE ""{_idColName}"" = @DataID
              AND ""tenant_code"" = @TenantCode;
        ";

        await DBcon.ExecuteAsync(sql, new {
            DataID = this.DataID,
            TenantCode = this.TenantCode
        }, transaction);

        return true;
    }
    catch
    {
        return false;
    }
}

protected virtual async Task<bool> -SoftDeletePrimaryQueryExecAsync(IDbTransaction transaction)
{
    try
    {
        string sql = $@"
            UPDATE ""{_tblName}""
            SET ""deleted"" = 1,
                ""Update_at"" = @UpdateAt,
                ""Locked_at"" = @LockedAt
            WHERE ""{_idColName}"" = @DataID
              AND ""tenant_code"" = @TenantCode;
        ";

        await DBcon.ExecuteAsync(sql, new {
            DataID = this.DataID,
            TenantCode = this.TenantCode,
            UpdateAt = DateTime.MaxValue,
            LockedAt = DateTime.MinValue
        }, transaction);

        return true;
    }
    catch
    {
        return false;
    }
}        

protected virtual async Task<bool> -DeleteDetailQueryExecAsync(IDbTransaction transaction)
{
    try
    {
        return true;
    }
    catch
    {
        return false;
    }
}

        public abstract Task<bool> _DeleteQueryExecAsync(IDbTransaction transaction);
        // データベースからエンティティを物理削除する。
        // 派生先では TblName テーブルおよび関連するサブテーブル（s_tblName など）を完全削除すること。

        public virtual async Task<bool> _SoftDeleteQueryExecAsync(IDbTransaction transaction){
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
        public virtual async Task<bool> ReNameAsync(string newName) {

            // 1. バリデーション
            if (string.IsNullOrWhiteSpace(newName) || newName.Length > 20) {
                return false;
            }

            IDbTransaction transaction = DBcon.BeginTransaction();
            if (await ReNameQueryExecAsync(newName, transaction)) {
                transaction.Commit();
                await _UpdatePropertiesFromTblAsync();
                return true;
            }
            transaction.Rollback();
            return false;
        }

        //DBへのデータ名変更を試みる。成功した場合プロパティの値も書き換える
        public virtual async Task<bool> ReNameQueryExecAsync(string newName, IDbTransaction dbTransaction) {
            try {
                // サーバー側でマイクロ秒精度の UTC を生成
                var updateAt = DateTime.UtcNow;

                string sql = $@"
                                UPDATE ""{_tblName}""
                                SET ""{_nameColName}"" = @name,
                                    ""Update_at"" = @update_at
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

        // データベースから最新のプロパティを取得して _rawData に反映する
        public virtual async Task _UpdatePropertiesFromTblAsync() {
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

                    _SetProperties(normalized);
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
                if (!await _HardDeleteQueryExecAsync(tran))
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

        public virtual async Task<bool> SaveWorkingPrimaryAsync(
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
        public virtual Task<bool> SaveWorkingDetailAsync(
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

        public virtual async Task<LockStatus> _SetLockAsync() {

            return await WriteLockInfoAsync(true);
        }
        public virtual async Task<LockStatus> _SetUnLockAsync() {

            return await WriteLockInfoAsync(false);
        }
        // =======================================================
        //データロック状態設定メソッド。
        // ======================================================
        protected virtual async Task<LockStatus> SetLockStatusAsync(bool doLock) {
            var result = await WriteLockInfoAsync(doLock);

            var nowupdate_at = result.Locked_at ?? DateTime.MinValue;
            // UIへの反映
            _SetProperties(new Dictionary<string, object> {
                ["locked_at"] = DateTime.UtcNow,
                ["locked_by"] = result.LockedByUserId
            });

            return result;
        }

        // ロック状態をDBに反映する
        // 保存時以外に呼んではいけない。
        public virtual async Task<LockStatus> _ReWriteLockInfoAsync(
                                                        IDbTransaction tran,
                                                        Guid? Locked_by,
                                                        DateTime? Locked_at) {
            LockStatus result;

            // ---- ロック情報が無い場合（ロック延命） ----
            if (Locked_by == null || Locked_at == null) {
                result = await WriteLockInfoAsync(true, tran);
            } else {
                // ---- ロック情報が既にある場合（ロック延命はしない） ----
                result = new LockStatus {
                    Exists = true,
                    lockReqUsr = CurrUsrID,
                    LockedByUserId = Locked_by,
                    Locked_at = Locked_at,
                    Update_at = Update_at,
                    HasError = false,
                    ErrorMessage = ""
                };

                // DBのロック情報を反映するためにプロパティ更新
                _SetProperties(new Dictionary<string, object> {
                    ["locked_at"] = result.Locked_at,
                    ["locked_by"] = result.LockedByUserId
                });
            }

            // ---- UIへの反映（整合） ----
            _SetProperties(new Dictionary<string, object> {
                ["locked_at"] = result.Locked_at,
                ["locked_by"] = result.LockedByUserId
            });

            return result;
        }



        //保存中にロック状態を更新する必要があるのでトランザクションを渡すが、基本は渡さない
        //また通常は呼び出してはいけない
        protected virtual async Task<LockStatus> WriteLockInfoAsync(bool doLock, IDbTransaction? tran = null) {
            // 時刻は必ず1回だけ取得
            var now = DateTime.UtcNow;
            var expiryTime = now.AddMinutes(-10);

            try {
                // ============================================================
                // ① doLock == true → ロック要求
                // ============================================================
                if (doLock) {
                    string sqlLock = $@"
                            UPDATE ""{TblName}""
                            SET ""locked_by"" = @userId,
                                ""locked_at"" = @lockedAt
                            WHERE ""{IdColName}"" = @dataID
                              AND ""tenant_code"" = @tenantCode
                              AND (
                                    ""locked_by"" = @userId
                                 OR ""locked_at"" IS NULL
                                 OR ""locked_at"" < @expiryTime
                              );
                        ";


                    int affected = await DBcon.ExecuteAsync(sqlLock, new {
                                                                userId = CurrUsrID,
                                                                lockedAt = now,
                                                                dataID = DataID,
                                                                tenantCode = TenantCode,
                                                                expiryTime = expiryTime
                                                            }, transaction: tran);

                    // ---- ロック成功 ----
                    if (affected > 0) {
                        return new LockStatus {
                            Exists = true,
                            lockReqUsr = CurrUsrID,
                            LockedByUserId = CurrUsrID,
                            Locked_at = now,
                            Update_at = Update_at,
                            HasError = false,
                            ErrorMessage = ""
                        };
                    }

                    // ---- ロック失敗 → 現在の状態を返す ----
                    var current = await LockedChkfromTbl();
                    current.lockReqUsr = CurrUsrID;

                    if (!current.Exists) {
                        current.HasError = false;
                        current.ErrorMessage = "";
                        return current; // RecordNone
                    }

                    if (current.IsLockedForEdit) {
                        current.HasError = true;
                        current.ErrorMessage = "他のユーザーがロック中です。";
                        return current; // 他人ロック
                    }

                    current.HasError = true;
                    current.ErrorMessage = "ロック更新に失敗しました。";
                    return current;
                }

                // ============================================================
                // ② doLock == false → ロック解除要求
                // ============================================================
                string sqlUnlock = $@"
                                    UPDATE ""{{TblName}}""
                                    SET ""locked_by"" = NULL,
                                        ""locked_at"" = NULL
                                    WHERE ""{{IdColName}}"" = @dataID
                                      AND ""tenant_code"" = @tenantCode
                                      AND ""locked_by"" = @userId;
                                    ";//他ユーザーがロックしているのに誤って解除する事故防止のためユーザーIDでも確認する

                await DBcon.ExecuteAsync(sqlUnlock, new {
                    dataID = DataID,
                    tenantCode = TenantCode,
                    userId = CurrUsrID
                }, transaction: tran);

                return new LockStatus {
                    Exists = true,
                    LockedByUserId = null,
                    Locked_at = null,
                    Update_at = Update_at,
                    lockReqUsr = CurrUsrID,
                    HasError = false,
                    ErrorMessage = ""
                };
            } catch (Exception ex) {
                return new LockStatus {
                    Exists = true,
                    LockedByUserId = null,
                    Locked_at = null,
                    Update_at = Update_at,
                    lockReqUsr = CurrUsrID,
                    HasError = true,
                    ErrorMessage = ex.Message
                };
            }
        }


        //テーブルからロック情報を読み取って返す。ユーザー名はクライアントで取得して
        protected virtual async Task<LockStatus> LockedChkfromTbl()
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
                    new { tenantCode = TenantCode, dataID = DataID });

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
                    lockReqUsr = CurrUsrID,
                    HasError = false,
                    ErrorMessage = ""
                };

                // ---- UI世界線へ反映（rawData） ----
                _SetProperties(new Dictionary<string, object> {
                    ["locked_at"] = lockSt.Locked_at ?? DateTime.MinValue,
                    ["locked_by"] = lockSt.LockedByUserId
                });

                return lockSt;
            }
            catch (Exception ex)
            {
                // ---- catch 時は必ず HasError をセット ----
                return new LockStatus {
                    lockReqUsr = CurrUsrID,
                    Exists = false,
                    HasError = true,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
