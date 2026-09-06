using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace NxRebuild.shared {
    public class TanMEntity {
        public Dictionary<string, object?> Raw { get; private set; }

        public TanMEntity(Dictionary<string, object> row) {
            Raw = NxTypeMapper.ConvertRow("tan_m", row);
        }
    }

    public class ZmstEntity : BaseDataObj<int>, IBaseDataObj<int> {
        // --- サブテーブル tan_m を保持する ---
        public List<TanMEntity> TanList { get; private set; } = new();


        public ZmstEntity() {
            _tblName = "Zmst";
            _nameColName = "Z_name";
            _idColName = "LocalCode";
            _parentIDColName = "gun_cd";
            _s_tblName = "tan_m";  // サブテーブル

            _infoTbl = "";
            _w_tblName = "";
            _ws_tblName = "";

            _datatype = NxDataType.Zairyou;
        }

        // ---------------------------------------------------------
        // JSON生成 SQL（Zmst + tan_m）
        // ---------------------------------------------------------
        protected override string CreateJSONsql() {
            return $@"
                SELECT t.*, '{_tblName}' AS _table_type
                FROM ""{_tblName}"" t
                WHERE t.""{_idColName}"" = @dataID
                  AND t.""tenant_code"" = @tenantCode

                UNION ALL

                SELECT s.*, '{_s_tblName}' AS _table_type
                FROM ""{_s_tblName}"" s
                WHERE s.""LocalCode"" = @dataID
                  AND s.""tenant_code"" = @tenantCode;
            ";
        }

        // ---------------------------------------------------------
        // DataOpen（排他制御）
        // ---------------------------------------------------------
        public override async Task<LockStatus> DataOpen() {
            var lockReq = new LockStatus {
                Exists = true,
                IsLocked = true,
                LockedByUserId = CurrUsrID.ToString(),
                Locked_at = DateTime.UtcNow
            };

            var lockStatus = await SetLockAsync(lockReq);


            return lockStatus;
        }

        // ---------------------------------------------------------
        // tan_m 読み込み
        // ---------------------------------------------------------
        public async Task LoadTanMAsync() {
            string sql = $@"
                SELECT *
                FROM ""tan_m""
                WHERE ""LocalCode"" = @DataID
                  AND ""tenant_code"" = @TenantCode;
            ";

            var rows = await DBcon.QueryAsync<Dictionary<string, object>>(sql,
                new { DataID = this.DataID, TenantCode = this.TenantCode });

            TanList.Clear();

            foreach (var row in rows) {
                TanList.Add(new TanMEntity(row));
            }
        }

        // ---------------------------------------------------------
        // 物理削除（tan_m）
        // 更新処理用Zmstは削除してはいけない
        // ---------------------------------------------------------
        public override async Task<bool> DeleteQueryExec(IDbTransaction transaction) {
            try {
                // tan_m 削除
                string sqlSub = $@"
                    DELETE FROM ""{_s_tblName}""
                    WHERE ""LocalCode"" = @DataID
                      AND ""tenant_code"" = @TenantCode;
                ";

                await DBcon.ExecuteAsync(sqlSub, new {
                    DataID = this.DataID,
                    TenantCode = this.TenantCode
                }, transaction);

                return true;
            } catch {
                return false;
            }
        }

        // ---------------------------------------------------------
        // ソフトデリート（Zmst のみ）単位マスタは消してはいけないので放置
        // ---------------------------------------------------------
        public override async Task<bool> SoftDeleteQueryExec(IDbTransaction transaction) {
            try {
                string sql = $@"
                    UPDATE ""{_tblName}""
                    SET ""deleted"" = 1,
                        ""Update_at"" = @UpdateAt
                    WHERE ""{_idColName}"" = @DataID
                      AND ""tenant_code"" = @TenantCode;
                ";

                await DBcon.ExecuteAsync(sql, new {
                    DataID = this.DataID,
                    TenantCode = this.TenantCode,
                    UpdateAt = DateTime.UtcNow
                }, transaction);

                return true;
            } catch {
                return false;
            }
        }

        // ---------------------------------------------------------
        // ID生成（LocalCode の新規採番）
        // 
        // ---------------------------------------------------------
        public int GenerateDataID(IDbTransaction tran) {
            string sql = $@"
                        SELECT COALESCE(MAX(""LocalCode""), 0) + 1
                        FROM ""{_tblName}""
                        WHERE ""tenant_code"" = @TenantCode
                        FOR UPDATE;
                    ";

            return DBcon.ExecuteScalar<int>(sql, new { TenantCode }, tran);
        }

        // ---------------------------------------------------------
        // SaveAsync（Zmst + tan_m）
        // ---------------------------------------------------------
        public override async Task<bool> SaveAsync() {
            using var tran = DBcon.BeginTransaction();
            try {
                // ★ ID未採番ならここで採番する（Zmst特例世界線）
                if (this.DataID == 0) {
                    this.DataID = GenerateDataID(tran);
                }

                bool ok = await DeleteQueryExec(tran);
                if (!ok) {
                    tran.Rollback();
                    return false;
                }

                ok = await SaveQueryExec(tran);
                if (!ok) {
                    tran.Rollback();
                    return false;
                }

                tran.Commit();
                return true;
            } catch {
                tran.Rollback();
                return false;
            }
        }


        // ---------------------------------------------------------
        // SaveQueryExec（Zmst + tan_m）
        // ---------------------------------------------------------
        public override async Task<bool> SaveQueryExec(IDbTransaction transaction) {
            try {
                // --- Zmst 更新 ---
                var cols = new List<string>();
                foreach (var kv in _rawData) {
                    if (kv.Key == _idColName || kv.Key == "tenant_code")
                        continue;

                    cols.Add($@"""{kv.Key}"" = @{kv.Key}");
                }

                string setClause = string.Join(", ", cols);

                string sqlMain = $@"
                                UPDATE ""{_tblName}""
                                SET {setClause},
                                    ""Update_at"" = @UpdateAt
                                WHERE ""{_idColName}"" = @DataID
                                  AND ""tenant_code"" = @TenantCode;
                            ";

                var param = new DynamicParameters(_rawData);
                param.Add("DataID", this.DataID);
                param.Add("TenantCode", this.TenantCode);
                param.Add("UpdateAt", DateTime.UtcNow);

                await DBcon.ExecuteAsync(sqlMain, param, transaction);

                // --- tan_m INSERT（DELETE は DeleteQueryExec 側） ---
                foreach (var tan in TanList) {
                    var row = tan.Raw;

                    row["LocalCode"] = this.DataID;
                    row["tenant_code"] = this.TenantCode;

                    var normalized = NxTypeMapper.ConvertRow(_s_tblName, row);

                    string columns = string.Join(", ", normalized.Keys);
                    string values = string.Join(", ", normalized.Keys.Select(k => "@" + k));

                    string sqlIns = $@"
                                    INSERT INTO ""{_s_tblName}"" ({columns})
                                    VALUES ({values});
                                ";

                    await DBcon.ExecuteAsync(sqlIns, normalized, transaction);
                }

                return true;
            } catch {
                return false;
            }
        }

    }
}
