using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace NxRebuild.shared {
    public class CategoryEntity : BaseDataObj<int> {
        public CategoryEntity() {
            _tblName = "gun_m";
            _idColName = "syou_cd";
            _nameColName = "syou_name";
            _parentIDColName = "dai_cd";
            _datatype = NxDataType.Folder;   // UIでフォルダ扱い
        }

        // ---------------------------------------------------------
        // ★ 編集禁止：常に「ロックなし」を返す
        // ---------------------------------------------------------
        public override Task<LockStatus> DataOpen() {
            return Task.FromResult(new LockStatus {
                Exists = true,
                IsLocked = false
            });
        }

        public override Task<LockStatus> DataClose() {
            return Task.FromResult(new LockStatus {
                Exists = true,
                IsLocked = false
            });
        }


        // ---------------------------------------------------------
        // ★ 名前変更禁止：常に false
        // ---------------------------------------------------------
        public override Task<bool> ReName(string newName) {
            return Task.FromResult(false);
        }

        // ---------------------------------------------------------
        // ★ 保存禁止：常に false
        // ---------------------------------------------------------
        public override Task<bool> SaveAsync() {
            return Task.FromResult(false);
        }

        // ---------------------------------------------------------
        // ★ 保存SQL禁止：常に false
        // ---------------------------------------------------------
        public override Task<bool> SaveQueryExec(IDbTransaction tran) {
            return Task.FromResult(false);
        }

        // ---------------------------------------------------------
        // ★ 削除禁止：常に false
        // ---------------------------------------------------------
        public override Task<bool> DeleteQueryExec(IDbTransaction tran) {
            return Task.FromResult(false);
        }

        // ---------------------------------------------------------
        // ★ 論理削除禁止：常に false
        // ---------------------------------------------------------
        public override Task<bool> SoftDeleteQueryExec(IDbTransaction tran) {
            return Task.FromResult(false);
        }

        // ---------------------------------------------------------
        // JSON生成SQLは使わないので空でOK
        // ---------------------------------------------------------
        protected override string CreateJSONsql() {
            return "";
        }
    }


    public class TanMEntity {
        public Dictionary<string, object?> Raw { get; private set; }

        public TanMEntity(Dictionary<string, object> row) {
            Raw = NxTypeMapper.ConvertRow("tan_m", row);
        }
    }
    //IZmstEntity インターフェース
    public interface IZmstEntity : IBaseDataObj<int> {
        List<TanMEntity> TanList { get; }
    }

    public class ZmstEntity : BaseDataObj<int>, IZmstEntity {
        // --- サブテーブル tan_m を保持する ---
        public List<TanMEntity> TanList { get; private set; } = new();

        // --- バックアップ ---
        public Dictionary<string, object?> RawBackup { get; private set; }
        public List<TanMEntity> TanBackup { get; private set; }

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
        // DataOpen（編集開始前処理）
        // ---------------------------------------------------------
        public override Task<LockStatus> DataOpen() {
            // --- ★ バックアップ作成 ---
            RawBackup = CloneRaw(_rawData);
            TanBackup = CloneTanList(TanList);

            Opened = true;//編集中フラグをON

            // --- ローカル編集開始なのでロックは常に false ---
            return Task.FromResult(new LockStatus {
                Exists = true,
                IsLocked = false
            });
        }

        // ---------------------------------------------------------
        // DataClose(編集終了）
        // フラグのセットのみ。UI側でSaveまたはRestoreを呼んだうえで
        // DataCloseを呼ぶこと
        // ---------------------------------------------------------
        public override Task<LockStatus> DataClose() {
            Opened = false; //編集中フラグをOFF
            return Task.FromResult(new LockStatus {
                Exists = true,
                IsLocked = false
            });
        }

        // ---------------------------------------------------------
        // バックアップから復元する
        // ---------------------------------------------------------
        public void RestoreBackup() {
            // --- RawData の復元 ---
            if (RawBackup != null) {
                _rawData = CloneRaw(RawBackup);
            }

            // --- TanList の復元 ---
            if (TanBackup != null) {
                TanList = CloneTanList(TanBackup);
            }
        }


        // ---------------------------------------------------------
        // TanListのDeepCopy
        // ---------------------------------------------------------
        private List<TanMEntity> CloneTanList(List<TanMEntity> src) {
            var list = new List<TanMEntity>();

            foreach (var tan in src) {
                var rawCopy = new Dictionary<string, object?>();

                foreach (var kv in tan.Raw)
                    rawCopy[kv.Key] = kv.Value;

                list.Add(new TanMEntity(rawCopy));
            }

            return list;
        }

        // ---------------------------------------------------------
        // CloneRaw（Zmst + tan_m の Raw データを複製する）
        // ---------------------------------------------------------
        private Dictionary<string, object?> CloneRaw(Dictionary<string, object?> src) {
            var dst = new Dictionary<string, object?>();
            foreach (var kv in src) {
                // object は参照型だが NxTypeMapper の値は基本プリミティブなのでそのままでOK
                dst[kv.Key] = kv.Value;
            }
            return dst;
        }



        // ---------------------------------------------------------
        // 物理削除（tan_m）
        // Zmstもtam_mも削除してはいけない
        // ---------------------------------------------------------
        public override Task<bool> DeleteQueryExec(IDbTransaction transaction) {
            // ★ 削除禁止：常に false を返す
            return Task.FromResult(false);
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
