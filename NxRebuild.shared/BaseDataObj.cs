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
using System.Xml.Linq;

namespace NxRebuild.shared {
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

    public enum LockResult {
        Success,       // ロック確保成功
        LockedByOther, // 他人がロック中
        RecordNone,
        DbError        // システムエラー
    }

    public interface IBaseDataObj<TKey> {
        Guid CurrUsrID { get; set; }
        TKey DataID { get; set; }
        string DataName { get; }
        NxDataType DataType { get; }
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

        Task<LockStatus> DataOpen();
        Task<LockStatus> DataClose();
        string TblToJson();
        Task<bool> JsonToTbl(string json);
        Task<bool> ReName(string newName);
        Task<bool> SaveAsync(
                        Dictionary<string, object?> workingRaw,
                        List<List<Dictionary<string, object?>>>? subTables = null);
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
        public Guid TenantCode { get; set; }
        public IDbConnection DBcon { get; set; }

        // --- 【変更】プロパティ実装：変数からJSON（_rawData）への参照へ切り替え ---

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


        public NxDataType DataType => _datatype; // ※_datatypeはメタデータ側管理ならそのままでOK

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
            Dictionary<string, object?> workingRaw,
            List<List<Dictionary<string, object?>>>? workingSubList = null) {
            // Raw の Deep Copy
            workingRaw.Clear();
            foreach (var kv in _rawData)
                workingRaw[kv.Key] = kv.Value;

            // SubRecColList の Deep Copy（ネスト対応）
            if (workingSubList != null) {
                workingSubList.Clear();

                foreach (var subRecCol in workingSubList) 
                {
                    var newSubCol = new List<Dictionary<string, object?>>();

                    foreach (var rec in subRecCol) // Dictionary<string, object?>
                    {
                        var newDict = new Dictionary<string, object?>();
                        foreach (var kv in rec)
                            newDict[kv.Key] = kv.Value;

                        newSubCol.Add(newDict);
                    }

                    workingSubList.Add(newSubCol);
                }
            }
        }





        // テーブルからデータを取得してJSON文字列にする
        public string TblToJson() {
            string sql = CreateJSONsql();
            var result = DBcon.Query<dynamic>(sql, new { dataID = DataID, tenantCode = TenantCode });
            return JsonSerializer.Serialize(result);
        }
        protected abstract string CreateJSONsql();
        //{ JSON生成用のビュー。以下実装例
        // 各レコードに自動的に "_table_type" というキーが追加される
        //return $@"SELECT t.*, '{_tblName}' as _table_type FROM {_tblName} t
        //        WHERE t.id = @dataID AND t.tenant_code = @tenantCode
        //        UNION ALL
        //        SELECT s.*, '{_s_tblName}' as _table_type FROM {_s_tblName} s
        //        WHERE s.parent_id = @dataID";
        //}

        public async Task<bool> JsonToTbl(string json) {
            var records = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
            // JSONを受け取ってテーブルに保存（Delete & Insert）
            var transaction = DBcon.BeginTransaction();
            // 1. Delete: 対象テーブル削除
            var result = await DeleteQueryExec(transaction);
            if (!result) {
                transaction.Rollback();
                return false;
            }

            try {
                foreach (var record in records) {
                   string targetTable = record.ContainsKey("_table_type")
                       ? record["_table_type"].ToString()
                       : _tblName;
               
                   // ★ 型マップ正本化（必須）
                   var normalized = NxTypeMapper.ConvertRow(targetTable, record);
               
                   var columns = string.Join(", ", normalized.Keys);
                   var values = string.Join(", ", normalized.Keys.Select(k => "@" + k));
               
                   DBcon.Execute(
                       $"INSERT INTO {targetTable} ({columns}) VALUES ({values})",
                       normalized,
                       transaction
                   );
                }
                transaction.Commit();
                return true;

            } catch (Exception ex) {
                transaction.Rollback();
                return false;
            }
        }

        public abstract Task<LockStatus> DataOpen();

        public abstract Task<LockStatus> DataClose();



        public abstract Task<bool> DeleteQueryExec(IDbTransaction transaction);
        // データベースからエンティティを物理削除する。
        // 派生先では TblName テーブルおよび関連するサブテーブル（s_tblName など）を完全削除すること。

        public abstract Task<bool> SoftDeleteQueryExec(IDbTransaction transaction);
        // ※ API 層からのみ呼び出す。
        // 論理削除を実装する：
        //   - TblName テーブルのレコードを「削除済み」と扱える状態にする
        //     （例：Name をクリア、Parent カラムを NULL にする、Updated_at を更新する）。
        //   - サブテーブル以下の関連レコードは物理削除する。
        // UI では、この論理削除状態を参照して「削除済みデータ」を判定する。
        // 実装時は挙動に注意すること。



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
            // ここでSQLを構築して実行
            string sql = $@"
                                UPDATE ""{_tblName}""
                                SET ""{_nameColName}"" = @name,
                                    ""update_at"" = @update_at
                                WHERE ""{_idColName}"" = @id
                                  AND ""tenant_code"" = @tenantCode;
                            ";

            // 成功したら true が返る
            return await DBcon.ExecuteAsync(sql, new { name = newName, id = DataID, update_at = DateTime.UtcNow ,tenantCode = TenantCode }, dbTransaction) > 0;

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

protected virtual TKey? EnsureIDForSave(IDbTransaction tran){
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

        
        this.DataID = EnsureIDForSave(tran);
        
      
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
    catch
    {
        tran.Rollback();
        return false;
    }
}

protected virtual async Task<bool> SaveWorkingAsync(
    Dictionary<string, object?> workingRaw,
    List<List<Dictionary<string, object?>>>? subTables,
    IDbTransaction tran)
{
    // ★ メインテーブル保存
    if (!await SaveWorkingMainAsync(workingRaw, tran))
        return false;

    // ★ サブテーブル保存（具象側で追加）
    if (!await SaveWorkingSubAsync(subTables, tran))
        return false;

    return true;
}
// =======================================================
// メインテーブル保存（WorkingRaw → _w_tblName）
// =======================================================

protected virtual async Task<bool> SaveWorkingMainAsync(
    Dictionary<string, object?> workingRaw,
    IDbTransaction tran)
{
    

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
protected virtual Task<bool> SaveWorkingSubAsync(
    List<List<Dictionary<string, object?>>>? subTables,
    IDbTransaction tran)
{
    // ★ スモールエンティティはサブ無しが基本
    return Task.FromResult(true);
}



// =======================================================
// 正本 Raw に反映
// =======================================================

protected void ApplyWorkingToRaw(Dictionary<string, object?> workingRaw)
{
    _rawData.Clear();
    foreach (var kv in workingRaw)
        _rawData[kv.Key] = kv.Value;
}


// =======================================================
// 継承先で必ず実装する確定処理
// =======================================================

/// <summary>
/// 扱うエンティティが単一レコードが主になると思われる為、ピュアバーチャルではなく
/// virtual とし、デフォルト実装は true を返すだけとする。
/// 具象クラスが複数テーブル・複数レコードを扱う場合は、
/// ワークテーブルを同じスキーマで作成し、
/// そちらから本テーブルへ書き込む処理をここに記述する。
/// </summary>
public virtual Task<bool> SaveQueryExec(IDbTransaction transaction)
{
    return Task.FromResult(true);
}

        //データロックメソッド。
        //ロックされてるか確認したくなってもロックが目的なので意味
        //が無いのでこれを呼び出せ。
        public virtual async Task<LockStatus>
            SetLockAsync(LockStatus lockStatus) {
            IDbTransaction dbTransaction = DBcon.BeginTransaction();
            LockStatus Lockst = await LockedChkfromTbl(dbTransaction);
            Guid parsedGuid;
            if (Lockst.IsLocked) {
                //すでにロック済みの場合その情報を返す。
                //自分のプロパティも更新
                _rawData["locked_at"] = (DateTime)Lockst.Locked_at;

                // 文字列をGuidに変換する
                if (Guid.TryParse(Lockst.LockedByUserId, out parsedGuid)) {
                    _rawData["locked_by"] = parsedGuid;
                } else {
                    // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                    _rawData["locked_by"] = Guid.Empty;
                }
                dbTransaction.Commit();
                return Lockst;
            } else {
                //ロック情報書き込み
                LockResult result = await WriteLockInfoAsync(lockStatus,dbTransaction);
                switch (result) {
                    case LockResult.Success:
                        // 書き込み成功後、改めて最新の情報をDBから取得して返す
                        //自分のプロパティも更新
                        _rawData["locked_at"] = (DateTime)Lockst.Locked_at;

                        // 文字列をGuidに変換する
                        if (Guid.TryParse(Lockst.LockedByUserId, out parsedGuid)) {
                            _rawData["locked_by"] = parsedGuid;
                        } else {
                            // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                            _rawData["locked_by"] = Guid.Empty;
                        }
                        dbTransaction.Commit();
                        return await LockedChkfromTbl(dbTransaction);

                    case LockResult.RecordNone:
                        // ロックすべきレコードが無い（新規データ）の場合、
                        // ロックしたものとしてリクエスト内容を返す
                        //すでにロック済みの場合その情報を返す。
                        _rawData["locked_at"] = (DateTime)Lockst.Locked_at;

                        // 文字列をGuidに変換する
                        if (Guid.TryParse(Lockst.LockedByUserId, out parsedGuid)) {
                            _rawData["locked_by"] = parsedGuid;
                        } else {
                            // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                            _rawData["locked_by"] = Guid.Empty;
                        }
                        dbTransaction.Rollback();
                        return lockStatus;

                    case LockResult.DbError:
                    default:
                        // エラー（DbError）および想定外のケース（default）の処理
                        // ロック無し、かつHasErrorを立てて通知する
                        dbTransaction.Rollback();
                        return new LockStatus {
                            Exists = false,
                            IsLocked = false,
                            LockedByUserId = null,
                            Locked_at = null,
                            HasError = true,
                            ErrorMessage = "DBエラーが発生しました"
                        };
                }
            }
        }
        //
        protected virtual async Task<LockResult> WriteLockInfoAsync(LockStatus lockStatus, IDbTransaction transaction) {
            // 10分経過したものは期限切れとみなす
            var expiryTime = DateTime.UtcNow.AddMinutes(-10);

            // 1. まず更新を試みる
            string sql = $@"
                        UPDATE ""{TblName}""
                        SET ""locked_by"" = @userId,
                            ""locked_at"" = @lockedAt
                        WHERE ""{IdColName}"" = @dataID
                          AND ""tenant_code"" = @tenantCode
                          AND (""locked_at"" IS NULL OR ""locked_at"" < @expiryTime);
                    ";


            try {
                int affectedRows = await DBcon.ExecuteAsync(sql, new {
                    userId = lockStatus.LockedByUserId,
                    lockedAt = DateTime.UtcNow,
                    dataID = DataID,
                    tenantCode = TenantCode,
                    expiryTime = expiryTime,
                    transaction
                });

                if (affectedRows > 0) return LockResult.Success;

                // 2. 更新できなかった場合、理由を調べるために再確認
                // ここでレコードが存在するか確認する
                var currentStatus = await LockedChkfromTbl(transaction);

                // currentStatus.Update_at が MinValue ならレコード無しと判定
                if (currentStatus.Locked_at == DateTime.MinValue) {
                    return LockResult.RecordNone;
                }

                // レコードはあるがIsLockedがtrue＝他人がロック中
                return LockResult.LockedByOther;

            } catch (Exception ex) {
                return LockResult.DbError;
            }
        }


        //テーブルからロック情報を読み取って返す。ユーザー名はクライアントで取得して
        protected virtual async Task<LockStatus> LockedChkfromTbl(IDbTransaction transaction) {
            // SQLでlocked_atとlocked_byの両方を取得
            var sql = $@"
                        SELECT
                            ""locked_at"",
                            ""locked_by"" AS ""UserId"",
                            ""Update_at""
                        FROM ""{TblName}""
                        WHERE ""tenant_code"" = @tenantCode
                          AND ""{IdColName}"" = @dataID;
                    ";


            var result = await DBcon.QueryFirstOrDefaultAsync<dynamic>(sql, new { TenantCode, DataID },transaction);

            // デフォルト値を設定
            bool isLocked = false;
            string? userId = null;
            DateTime updateAt = result?.Update_at ?? DateTime.MinValue;

            // レコードが取れなかった場合
            if (result == null) {
                return new LockStatus { Exists = false };
            }

            // レコードがある場合
            DateTime? lockedAt = result.locked_at as DateTime?;
            bool locked = lockedAt != null && (DateTime.UtcNow - lockedAt.Value).TotalMinutes < 10;

            LockStatus lockSt = new LockStatus {
                Exists = true, // レコードあり！
                IsLocked = locked,
                LockedByUserId = locked ? (string)result.UserId : null,
                Locked_at = (DateTime?)result.Update_at
            };
            //自分のプロパティも更新
            Guid parsedGuid;
            _rawData["locked_at"] = (DateTime)lockSt.Locked_at;

            // 文字列をGuidに変換する
            if (Guid.TryParse(lockSt.LockedByUserId, out parsedGuid)) {
                _rawData["locked_by"] = parsedGuid;
            } else {
                // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                _rawData["locked_by"] = Guid.Empty;
            }
            return lockSt;

        }
    }
}
