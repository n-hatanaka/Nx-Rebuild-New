using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace NxRebuild.shared {
    public class SyncBaseDataObj<TKey> {
        private readonly BaseDataObj<TKey> _dataObj;

        public SyncBaseDataObj(BaseDataObj<TKey> dataObj) {
            _dataObj = dataObj ?? throw new ArgumentNullException(nameof(dataObj));
        }

        protected IDbConnection DBCon => _dataObj.DBcon;

        // DataObjのメソッドへのアクセスラッパー
        public string LoadDataAsJson() => _dataObj.LoadDataAsJson();
        public Task<bool> SaveAsync() => _dataObj.SaveAsync();
        public TKey DataID => _dataObj.DataID;
        public string DataName {

            get => _dataObj.DataName;
            set => _dataObj.DataName = value;
        }
        public NxDataType DataType => _dataObj.DataType;
        public DateTime Update_at {
            get => _dataObj.Update_at;
            set => _dataObj.Update_at = value;
        }
        public Guid LockerID {
            get => _dataObj.LockerID;
            set => _dataObj.LockerID = value;
        }
        public DateTime LockedAt {
            get => _dataObj.LockedAt;
            set => _dataObj.LockedAt = value;
        }


        public string TenantCode {
            get => _dataObj.TenantCode;
            set => _dataObj.TenantCode = value;
        }

        public string NameColName => _dataObj.NameColName;
        public string IdColName => _dataObj.IdColName;
        public string TblName => _dataObj.TblName;
        public string S_TblName => _dataObj.S_TblName;
        public string InfoTbl => _dataObj.InfoTbl;
        public string W_TblName => _dataObj.W_TblName;
        public string Ws_TblName => _dataObj.Ws_TblName;

        public async Task<LockStatus> DataOpen() => await _dataObj.DataOpen();

   
        public virtual async Task<bool> ReName(string newName) {
            // 1. バリデーション
            if (string.IsNullOrWhiteSpace(newName) || newName.Length > 20) {
                return false;
            }

            // 2. ロック状態を確認
            LockStatus currentLock = await LockedChkfromTbl();

            // 3. エクスプローラー仕様のガード
            // 誰かがロックしていたら（IsLocked == true）、例えそれが自分でもリネーム不可
            if (currentLock.IsLocked) {
                return false;
            }

            // 4. ロックされていなければ実行
            if (await ReNameQueryExec(newName)) {
                this.DataName = newName;
                return true;
            }

            return false;
        }

        //DataObjに実装されているReNameQueryExecメソッドにアクセス
        public static async Task<bool> ReNameQueryExec(string newName) {
            // TODO: 実際のSQL実行ロジックをここに実装
            // DataObjのReNameQueryExecメソッドへの委譲
            return await _dataObj.ReNameQueryExec(newName);
        }
        public virtual async Task<LockStatus> SetLockAsync(LockStatus lockStatus) {
            LockStatus Lockst = await LockedChkfromTbl();
            Guid parsedGuid;
            if (Lockst.IsLocked) {
                //すでにロック済みの場合その情報を返す。
                //自分のプロパティも更新
                LockedAt = (DateTime)Lockst.Locked_at;

                // 文字列をGuidに変換する
                if (Guid.TryParse(Lockst.LockedByUserId, out parsedGuid)) {
                    LockerID = parsedGuid;
                } else {
                    // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                    LockerID = Guid.Empty;
                }
                return Lockst;
            } else {
                //ロック情報書き込み
                LockResult result = await WriteLockInfoAsync(lockStatus);
                switch (result) {
                    case LockResult.Success:
                        // 書き込み成功後、改めて最新の情報をDBから取得して返す
                        //自分のプロパティも更新
                        LockedAt = (DateTime)Lockst.Locked_at;

                        // 文字列をGuidに変換する
                        if (Guid.TryParse(Lockst.LockedByUserId, out parsedGuid)) {
                            LockerID = parsedGuid;
                        } else {
                            // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                            LockerID = Guid.Empty;
                        }
                        return await LockedChkfromTbl();

                    case LockResult.RecordNone:
                        // ロックすべきレコードが無い（新規データ）の場合、
                        // ロックしたものとしてリクエスト内容を返す
                        //すでにロック済みの場合その情報を返す。
                        LockedAt = (DateTime)Lockst.Locked_at;

                        // 文字列をGuidに変換する
                        if (Guid.TryParse(Lockst.LockedByUserId, out parsedGuid)) {
                            LockerID = parsedGuid;
                        } else {
                            // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                            LockerID = Guid.Empty;
                        }
                        return lockStatus;

                    case LockResult.DbError:
                    default:
                        // エラー（DbError）および想定外のケース（default）の処理
                        // ロック無し、かつHasErrorを立てて通知する
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

        protected virtual async Task<LockResult> WriteLockInfoAsync(LockStatus lockStatus) {
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
                    expiryTime = expiryTime
                });

                if (affectedRows > 0) return LockResult.Success;

                // 2. 更新できなかった場合、理由を調べるために再確認
                // ここでレコードが存在するか確認する
                var currentStatus = await LockedChkfromTbl();

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

        //テーブルからロック情報を読み取って返す。ユーザー名はクライアントで
        protected virtual async Task<LockStatus> LockedChkfromTbl() {
            // SQLでlocked_atとlocked_byの両方を取得
            var sql = $@"SELECT locked_at, locked_by as UserId, Update_at  
                 FROM {TblName} 
                 WHERE group_code = @groupCode AND {IdColName} = @dataID";

            var result = await DBcon.QueryFirstOrDefaultAsync<dynamic>(sql, new { TenantCode, DataID });

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
            LockedAt = (DateTime)lockSt.Locked_at;

            // 文字列をGuidに変換する
            if (Guid.TryParse(lockSt.LockedByUserId, out parsedGuid)) {
                LockerID = parsedGuid;
            } else {
                // 万が一、DBにIDではない不正な文字列が入っていた場合の保険
                LockerID = Guid.Empty;
            }
            return lockSt;

        }
    }
}
