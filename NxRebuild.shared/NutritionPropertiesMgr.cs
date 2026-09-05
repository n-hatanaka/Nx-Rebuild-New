using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace NxRebuild.shared {
    public class NutritionPropertiesMgr : BaseDataObjMgr<NutritionProperty, int> {

        public NutritionPropertiesMgr(IDbConnection db, Guid tenantCode, Guid currentUserID)
            : base(db, tenantCode, currentUserID) {
            _tblName = "ColName";
        }

        // DBから栄養素プロパティを取得
        public override async Task<IEnumerable<dynamic>> LoadRecordsAsync() {
            string sql = $@"
                                SELECT *
                                FROM ""{_tblName}""
                                WHERE tenant_code = @TenantCode
                                ORDER BY ""SortNo"";
                            ";

            try {
                Console.WriteLine($"[NxAPI] LoadRecordsAsync TenantCode = {TenantCode}");
                Console.WriteLine($"[NxAPI] SQL = {sql}");

                var records = await DBcon.QueryAsync<dynamic>(
                    sql,
                    new { TenantCode = TenantCode } // Guid のまま渡す
                );

                Console.WriteLine($"[NxAPI] Records Count = {records.Count()}");

                return records;
            } catch (Exception ex) {
                Console.WriteLine("=== LoadRecordsAsync Exception ===");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Type: {ex.GetType().FullName}");

                if (ex.InnerException != null) {
                    Console.WriteLine("--- InnerException ---");
                    Console.WriteLine($"Inner Message: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner Type: {ex.InnerException.GetType().FullName}");
                }

                Console.WriteLine("==============================");

                throw; // そのまま再スローして上位で拾えるようにする
            }
        }



        // 削除は行わない → 空のリストを返す
        public override Task<List<int>> DeleteData(IEnumerable<int> dataIDs) {
            return Task.FromResult(new List<int>());
        }

        // 削除は行わない → false を返す
        public override Task<bool> DeleteDataObj(int dataID) {
            return Task.FromResult(false);
        }

        // 削除は行わない → 何もしない
        public override void RemoveFromList(BaseDataObj<int> obj) {
            // no-op
        }

        // ID生成は行わない → とりあえず 0 を返す（Base側が使わないならこれでOK）
        protected override int GenerateDataID() {
            return 0;
        }
    }
}
