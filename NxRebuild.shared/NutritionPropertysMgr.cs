using Dapper;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace NxRebuild.shared {
    public class NutritionPropertysMgr : BaseDataObjMgr<NutritionProperty, int> {


        // BaseDataObjMgr(DbConnection, Guid, Guid) を呼べるコンストラクタを提供
        public NutritionPropertysMgr(DbConnection db, Guid tenantCode, Guid currentUserID)
            : base(db, tenantCode, currentUserID) {
      　     _tblName ="ColName";
        }

        // データベースから栄養素プロパティを取得する(Initializeメソッドからのみ呼び出す)
        public override async Task<IEnumerable<Dictionary<string, object>>> LoadRecordsAsync() {
            // データベースから栄養素プロパティを取得する
            string sql = $"SELECT * FROM \"{_tblName}\" WHERE tenant_code = @TenantCode ORDER BY SortNo;";

            return await DBcon.QueryAsync<Dictionary<string, object>>(sql);
        }
        public override async Task<List<int>> DeleteData(IEnumerable<int> dataIDs){
            //削除は行わないので無効化
            throw new NotImplementedException();
        }

        public override async Task<bool> DeleteDataObj(int dataID) {
            //削除は行わないので無効化
            throw new NotImplementedException();
        }
        public override void RemoveFromList(BaseDataObj<int> obj) {
            //削除は行わないので無効化
            throw new NotImplementedException();
        }

        protected override int GenerateDataID() {
            //ID生成は行わないので無効化
            throw new NotImplementedException();
        }
    }
}
