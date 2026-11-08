using NxRebuild.Client.Pages.Auth;
using NxRebuild.shared;
using System.Data;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public interface ISyncRyLibEntityMgr
        : ISyncBaseDataObjMgr<RyLibEntity, SyncRyLibEntity, Guid> {
    }

    public class SyncRyLibEntityMgr
        : SyncBaseDataObjMgr<RyLibEntity, SyncRyLibEntity, Guid>, IRyLibEntityMgr, ISyncRyLibEntityMgr {

        public override string ApiRoute => "RyLib";   // サーバー側コントローラのルートに合わせること

        // --- BaseDataObj の RyLibEntityMgr を保持 ---
        public SyncRyLibEntityMgr(
            IDbConnection db,
            HttpClient http,
            CustomAuthStateProvider auth,
            Guid tenantCode,
            Guid currentUserId)
            : base(db, http, auth, tenantCode, currentUserId) {

            _baseDataObjMgr = new RyLibEntityMgr(db, tenantCode, currentUserId);
        }
    }
}