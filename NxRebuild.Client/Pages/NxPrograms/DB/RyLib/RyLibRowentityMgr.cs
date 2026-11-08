using NxRebuild.shared;
using System.Data;

namespace NxRebuild.Client.Pages.NxPrograms.DB.RyLib {
    public interface IRyLibRowEntityMgr
        : IBaseDataObjMgr<RyLibRowEntity, int> {
    }

    public class RyLibRowEntityMgr
        : BaseDataObjMgr<RyLibRowEntity, int>,
          IRyLibRowEntityMgr {
        private int _nextId = -1;

        public RyLibRowEntityMgr(
            IDbConnection db,
            Guid tenantCode,
            Guid currentUserId)
            : base(db, tenantCode, currentUserId) {
        }

        public override int _GenerateDataID() {
            return _nextId--;
        }
    }
}
