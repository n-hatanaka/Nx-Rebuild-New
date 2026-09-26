using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace NxRebuild.shared {
    // ---------------------------------------------------------
    // CategoryEntity
    // 食品群マスタ（gun_m）を表すエンティティ
    // ZmstEntityMgr のデータリストに混在して格納されるため、ZmstEntity を継承する
    // ---------------------------------------------------------
    public class CategoryEntity : ZmstEntity {
        public CategoryEntity() {
            _tblName = "gun_m";
            _idColName = "syou_cd";
            _nameColName = "syou_name";
            _parentIDColName = "dai_cd";
            _datatype = NxDataType.Folder;   // UIでフォルダ扱い
        }

        public int jun {
            get {
                if (_rawData.TryGetValue("jun", out var v) && v != null)
                    return Convert.ToInt32(v);
                return 0;
            }
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
        public override async Task<bool> SaveAsync(
            Dictionary<string, object?> workingRaw,
            List<List<Dictionary<string, object?>>>? subTables = null) {

            return false;
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



    //IZmstEntity インターフェース
    public interface IZmstEntity : IBaseDataObj<int> {
        List<Dictionary<string, object?>> TanList { get; }
        decimal? GetNutritionValue(string col);
        void SetNutritionValue(string col, decimal? value);

        Task LoadSubTablesFromRaw();

    }

    public class ZmstEntity : BaseDataObj<int>, IZmstEntity {
        // --- サブテーブル tan_m を保持する ---
        public List<List<Dictionary<string, object?>>> SubTables { get; set; }
                                                        = new List<List<Dictionary<string, object?>>>();
        public List<Dictionary<string, object?>> TanList {
            get {
                // サブテーブルがまだ無い場合は作る
                if (SubTables.Count == 0)
                    SubTables.Add(new List<Dictionary<string, object?>>());

                return SubTables[0];
            }
        }


        public decimal? GetNutritionValue(string col) {
            if (_rawData.TryGetValue(col, out var v))
                return v == null ? null : Convert.ToDecimal(v);
            return null;
        }

        public void SetNutritionValue(string col, decimal? value) {
            _rawData[col] = value;
        }


        public ZmstEntity() {
            _tblName = "Zmst";
            _nameColName = "Z_name";
            _idColName = "LocalCode";
            _parentIDColName = "gun_cd";
            _s_tblName = "tan_m";  // サブテーブル

            _infoTbl = "";

            //Zmst,tan_mはワークテーブルを持たないので同じテーブル名を設定する。
            _w_tblName = "Zmst";
            _ws_tblName = "tan_m";

            _datatype = NxDataType.Zairyou;
        }

        // ---------------------------------------------------------
        // サブテーブル編集用ワーキングメモリ　
        // ---------------------------------------------------------
        public override void CreateWorkingSubTables(
                            List<List<Dictionary<string, object?>>> workingSubList) {
            workingSubList.Clear();

            foreach (var subRecCol in SubTables) {
                var newSubCol = new List<Dictionary<string, object?>>();

                foreach (var rec in subRecCol) {
                    var newDict = new Dictionary<string, object?>();
                    foreach (var kv in rec)
                        newDict[kv.Key] = kv.Value;

                    newSubCol.Add(newDict);
                }

                workingSubList.Add(newSubCol);
            }
        }

        // ---------------------------------------------------------
        // サブテーブルの読み込み（tan_m）
        // ---------------------------------------------------------
        public async Task LoadSubTablesFromRaw() {
            SubTables.Clear(); // 一旦クリア

            string sqlSub = $@"
                                SELECT *
                                FROM ""{_s_tblName}""
                                WHERE ""tenant_code"" = @tc
                                  AND ""LocalCode"" = @lc;
                            ";

            var tc = TenantCode.ToString();
            var lc = this.DataID;

            var rawTanRows = await DBcon.QueryAsync<dynamic>(
                sqlSub,
                new { tc, lc }
            );

            var tanRows = rawTanRows
                .Select(r => NxTypeMapper.ConvertRow(_s_tblName, (IDictionary<string, object>)r))
                .ToList();

            var subList = new List<Dictionary<string, object?>>();

            foreach (var row in tanRows)
                subList.Add(new Dictionary<string, object?>(row));

            SubTables.Add(subList);
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
        // 物理削除（保存前に既存レコードの削除の為のみに使う）
        // Zmstもtam_mも削除してはいけない
        // ---------------------------------------------------------
        public override async Task<bool> DeleteQueryExec(IDbTransaction transaction)
        {
            try
            {
                // ★ サブテーブル tan_m を削除（Zmst 固定世界線）
                string delSubSql = $@"
                    DELETE FROM ""tan_m""
                    WHERE ""{_idColName}"" = @dataID
                      AND ""tenant_code"" = @tenantCode;
                ";

                await DBcon.ExecuteAsync(delSubSql, new {
                    dataID = this.DataID,
                    tenantCode = this.TenantCode
                }, transaction);

                // ★ メインテーブル zmst を削除
                string delMainSql = $@"
                    DELETE FROM ""zmst""
                    WHERE ""{_idColName}"" = @dataID
                      AND ""tenant_code"" = @tenantCode;
                ";

                await DBcon.ExecuteAsync(delMainSql, new {
                    dataID = this.DataID,
                    tenantCode = this.TenantCode
                }, transaction);

                return true;
            } 
            catch (Exception ex) 
            {
                Console.WriteLine("DeleteQueryExec ERROR:");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);

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
      
        protected override int EnsureIDForSave(IDbTransaction tran){
            if (this.DataID == 0)
            {
                // ★ Zmst は保存時採番（新規レコード）
                int newId = GenerateDataID(tran);
                return newId;
            }
            else
            {
                // ★ 既存レコード（採番不要）
                return this.DataID;
            }
        }


        public override async Task<bool> ReNameQueryExec(string newName, IDbTransaction dbTransaction) {
            try {
                // LocalCode と tenant_code を正本から取得
                var localCode = this.DataID;
                var tenantCode = this.TenantCode;

                // SQL：Z_name と Update_at を更新
                // SQLite と PostgreSQL で現在時刻の取得方法が異なるため、
                // DB種別（SQLiteかどうかだけ）に応じて式を切り替える
                // (LocalCrudのみ行う非同期オブジェクトのためのため）
                var updateAtExpr = 
                        DBcon.ConnectionString.Contains("mode=memory", StringComparison.OrdinalIgnoreCase)
                                                        ? "strftime('%Y-%m-%d %H:%M:%f', 'now')"   // SQLite（ミリ秒）
                                                        : "NOW()";                                 // PostgreSQL（マイクロ秒）


                var sql = $@"
                                UPDATE Zmst
                                SET 
                                    Z_name = @NewName,
                                    Update_at = {updateAtExpr}
                                WHERE tenant_code = @TenantCode
                                  AND LocalCode = @LocalCode;
                            ";

                var rows = await DBcon.ExecuteAsync(sql, new {
                    NewName = newName,
                    TenantCode = tenantCode,
                    LocalCode = DataID
                }, dbTransaction);

                // 成功判定（1行更新されればOK）
                return rows == 1;
            } catch (Exception ex) {
                Console.WriteLine($"RenameQueryExec Error: {ex.Message}");
                return false;
            }
        }

        protected override async Task<bool> SaveWorkingSubAsync(
            List<List<Dictionary<string, object?>>>? w_SubTblList,
            IDbTransaction tran) {
            if (w_SubTblList == null)
                return true;

            // w_SubTblList = サブテーブルごとの行リスト
            foreach (var subTableRows in w_SubTblList) {
                // subTableRows = 1つのサブテーブルの行一覧
                foreach (var row in subTableRows) {
                    var cols = new List<string>();
                    var vals = new List<string>();

                    foreach (var kv in row) {
                        cols.Add($@"""{kv.Key}""");
                        vals.Add($@"@{kv.Key}");
                    }

                    string insSql = $@"
                                        INSERT INTO ""tan_m""
                                        ({string.Join(", ", cols)})
                                        VALUES ({string.Join(", ", vals)});
                                    ";

                    var affected = await DBcon.ExecuteAsync(insSql, row, tran);
                    if (affected != 1)
                        return false;
                }
            }

            return true;
        }


    }
}
