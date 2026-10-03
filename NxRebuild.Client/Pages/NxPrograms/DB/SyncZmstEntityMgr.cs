using NxRebuild.Client.Pages.Auth;
using NxRebuild.shared;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public class SyncZmstEntityMgr
        : SyncBaseDataObjMgr<ZmstEntity, SyncZmstEntity, int>, IZmstEntityMgr {
        public override string ApiRoute => "/api/Zmst";

        // --- BaseDataObjの ZmstEntityMgr を保持 ---
        public SyncZmstEntityMgr(
            IDbConnection db,
            HttpClient http,
            CustomAuthStateProvider auth,
            Guid tenantCode,
            Guid currentUserId)
            : base(db, http, auth, tenantCode, currentUserId) {
            _http = http;
            _auth = auth;

            _baseDataObjMgr = new ZmstEntityMgr(db, tenantCode, currentUserId);
        }

        public List<CategoryEntity> GunList {
            get => ((IZmstEntityMgr)_baseDataObjMgr).GunList;
        }

        public override IZmstEntity? Get(int id) {
            return (IZmstEntity)_baseDataObjMgr.Get(id);
        }

        // ---------------------------------------------------------
        // Delete（SyncDataObj → BaseDataObj → APIDataObj）
        // ---------------------------------------------------------
        public async Task<bool> DeleteAsync(int dataID) {
            return await DeleteDataObj(dataID);
        }
    }
}
