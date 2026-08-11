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
        Zairyou = 2,
        Ryouri = 4,
        Meal = 8,
        Kondate = 16,
        Calendar = 32,
        Person = 64,
        InstMeals = 128 //給食'Institutional meals'

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
        string IdColName { get; }
        string InfoTbl { get; }
        DateTime LockedAt { get;  }
        Guid LockerID { get; }
        string NameColName { get; }
        string S_TblName { get; }
        IBaseDataObjMgr<BaseDataObj<TKey>, TKey> SelfObjMgr { get; set; }
        string TblName { get; }
        Guid TenantCode { get; set; }
        DateTime Update_at { get;  }
        string W_TblName { get; }
        string Ws_TblName { get; }

        Task<LockStatus> DataOpen();
        string TblToJson();
        Task<bool> JsonToTbl(string json);
        Task<bool> ReName(string newName);
        Task<bool> SaveAsync();
    }

    public abstract class BaseDataObj<TKey> : IBaseDataObj<TKey> {

        // 【変更】レコード内容をJSON（辞書）として保持するメンバ
        public Dictionary<string, object> _rawData = new();

        protected string _nameColName; //テーブルのデータ名カラムのカラム名
        protected string _idColName;//テーブルのIDカラムのカラム名

        protected string _tblName; //データ名等基本データが格納されるテーブル名
        protected string _s_tblName;//材料など詳細データが格納されるテーブル名
        protected string _infoTbl;//_tblNameに加え栄養素などの集計結果が入っているテーブル

        protected string _w_tblName;
        protected string _ws_tblName;


        public string NameColName => _nameColName;
        public string IdColName => _idColName;
        public string TblName => _tblName;
        public string S_TblName => _s_tblName;
        public string InfoTbl => _infoTbl;
        public string W_TblName => _w_tblName;
        public string Ws_TblName => _ws_tblName;

        protected NxDataType _datatype;
        protected DateTime _update_at;
        protected Guid _locker_ID;
        protected DateTime _locked_at;


        public IBaseDataObjMgr<BaseDataObj<TKey>, TKey> SelfObjMgr { get; set; }
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

        public NxDataType DataType => _datatype; // ※_datatypeはメタデータ側管理ならそのままでOK

        public DateTime Update_at {
            get => (DateTime)_rawData["update_at"];
        }

        public Guid LockerID {
            get => (Guid)_rawData["locked_by"];
        }

        public DateTime LockedAt {
            get => (DateTime)_rawData["locked_at"];
        }

        public bool Opened {  get; set; }

        public bool Edited {  get; set; }

        //参照しているユーザーのID
        //インスタンス作成後に必ずセットする事
        public Guid CurrUsrID { get; set; }




        // この中は派生先で実装する事。
        //ここで固定のテーブル名やNameカラム名などのプロパティを設定する
        protected abstract void Initialize();

        public virtual void SetPropertys(Dictionary<string, object> record) {
            // 【変更】recordをそのまま _rawData として保持する設計に移行
            // ※KeyedListとDictionaryの互換性がある前提ですが、
            // 必要に応じてここでコピーまたは変換を行ってください。
            _rawData = record;

            // 既存のフィールド個別セットは不要になるため、実質上記の一行で完結します。
            // 個別にプロパティへセットしていた古い実装はここで終了します。

            // --- コメントアウトした元実装の意図 ---
            // _dataID = (TKey?)record[_idColName];
            // _dataName = (string)record[_nameColName];
            // _update_at = (DateTime)record["update_at"];
            // _locker_ID = (Guid)record["locked_by"];
            // _locked_at = (DateTime)record["locked_at"];
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
                    // どのテーブルに属するかを判定するプロパティ(例: "_table_type")があると仮定
                    string targetTable = record.ContainsKey("_table_type") ? record["_table_type"].ToString() : _tblName;

                    // 2. Insert: targetTable に対して動的Insert
                    var columns = string.Join(", ", record.Keys);
                    var values = string.Join(", ", record.Keys.Select(k => "@" + k));

                    DBcon.Execute($"INSERT INTO {targetTable} ({columns}) VALUES ({values})", record, transaction);

                }
                transaction.Commit();
                return true;

            } catch (Exception ex) {
                transaction.Rollback();
                return false;
            }
        }

        public abstract Task<LockStatus> DataOpen();




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
                await UpdatePropertys();
                return true;
            }
            transaction.Rollback();
            return false;
        }

        //DBへのデータ名変更を試みる。成功した場合プロパティの値も書き換える
        public virtual async Task<bool> ReNameQueryExec(string newName, IDbTransaction dbTransaction) {
            // ここでSQLを構築して実行
            string sql = $"UPDATE {_tblName} SET {_nameColName} = @name, update_at = @update_at WHERE {_idColName} = @id AND group_code = @tenantCode";

            // 成功したら true が返る
            return await DBcon.ExecuteAsync(sql, new { name = newName, id = DataID, update_at = DateTime.UtcNow ,tenantCode = TenantCode }, dbTransaction) > 0;

        }

        public virtual async Task UpdatePropertys() {
            string sql =
                $"SELECT * FROM {_tblName} WHERE {_idColName} = '{DataID}' AND tenant_code = '{TenantCode}'";

            var record = await DBcon.QueryFirstOrDefaultAsync<Dictionary<string, object>>(sql);

            if (record != null) {
                SetPropertys(record);
            }
        }

        public abstract Task<bool> SaveAsync();

        //DBへの処理でのみ使用
        public abstract Task<bool> SaveQueryExec(IDbTransaction transaction);

        //データロックメソッド。
        //ロックされてるか確認したくなってもどーせロックが目的なので意味
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
                        UPDATE {TblName} 
                        SET locked_by = @userId, locked_at = @lockedAt 
                        WHERE {IdColName} = @dataID 
                          AND group_code = @tenantCode
                          AND (locked_at IS NULL OR locked_at < @expiryTime)";

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
            var sql = $@"SELECT locked_at, locked_by as UserId, Update_at  
                 FROM {TblName} 
                 WHERE group_code = @groupCode AND {IdColName} = @dataID";

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
