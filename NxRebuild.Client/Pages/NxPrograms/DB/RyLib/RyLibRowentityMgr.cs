using NxRebuild.shared;
using System.Data;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace NxRebuild.Client.Pages.NxPrograms.DB.RyLib {
    public interface IRyLibRowEntityMgr
        : IBaseDataObjMgr<RyLibRowEntity, int> {
    }

    public class RyLibRowEntityMgr
        : BaseDataObjMgr<RyLibRowEntity, int>,
          IRyLibRowEntityMgr {

        public RyLibRowEntityMgr(
            IDbConnection db,
            Guid tenantCode,
            Guid currentUserId)
            : base(db, tenantCode, currentUserId) {
        }

        //想定100件未満なのでこの実装で充分
        public override int _GenerateDataID() {
            var existingIds = new HashSet<int>(_dataList.Select(x => x.DataID));

            int id = 1;
            while (existingIds.Contains(id)) {
                id++;
            }
            return id;
        }

        protected override bool CanReceive(IBaseDataObj<int> sourceObj) {
            if (sourceObj.DataType == NxDataType.RyouriRow
                || sourceObj.DataType == NxDataType.Zairyou)
                return true;
            else
                return false;
        }
    }
}
