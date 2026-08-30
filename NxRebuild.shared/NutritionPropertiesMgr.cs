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
            string sql = $"SELECT * FROM \"{_tblName}\" WHERE tenant_code = @TenantCode ORDER BY SortNo;";
            return await DBcon.QueryAsync<dynamic>(sql);
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
