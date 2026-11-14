using NxRebuild.shared;
using System.Data;

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

        public override int _GenerateDataID() {
            for (int id = -1; ; id--) {
                if (!_dataList.Any(x => x.DataID == id))
                    return id;
            }
        }
    }
}
