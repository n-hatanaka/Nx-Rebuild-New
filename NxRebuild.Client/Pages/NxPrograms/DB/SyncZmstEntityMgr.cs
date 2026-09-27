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

        // ---------------------------------------------------------
        // 新規作成（SyncDataObj）
        // ---------------------------------------------------------
        public SyncZmstEntity CreateNewSyncObj() {
            // BaseDataObjで新規作成
            var baseObj = _baseDataObjMgr.CreateNewDataObj(0);

            // SyncDataObjのラッパーを作成
            var syncObj = new SyncZmstEntity();
            syncObj.Http = _http;
            syncObj.Auth = _auth;

            syncObj.DBcon = DBcon;
            syncObj.TenantCode = TenantCode;
            syncObj.CurrUsrID = CurrentUserID;

            // BaseDataObjの rawData をそのままコピー
            syncObj.Setproperties(baseObj._rawData);

            // DataID は BaseDataObjの値を使う（Zmstは0で始まる）
            syncObj.DataID = baseObj.DataID;

            // DataList に追加
            _baseDataObjMgr._dataList.Add(syncObj);

            return syncObj;
        }

        // ---------------------------------------------------------
        // Initialize（Zmst + tan_m を SyncDataObjでロード）
        // ---------------------------------------------------------
        public override async Task Initialize() {
            // BaseDataObjで Zmst + tan_m をロード
            await _baseDataObjMgr.Initialize();

            // ★ Sync 用の新しいリストを作る
            var syncList = new List<IBaseDataObj<int>>();

            foreach (var baseObj in _baseDataObjMgr.DataList) {

                var syncObj = new SyncZmstEntity();
                syncObj.Http = _http;
                syncObj.Auth = _auth;
                syncObj._dataObj= (ZmstEntity)baseObj;
                syncObj.SelfObjMgr = this;
                syncObj.DBcon = DBcon;
                syncObj.TenantCode = TenantCode;
                syncObj.CurrUsrID = CurrentUserID;

                // BaseDataObjの rawData をコピー
                syncObj.Setproperties(((ZmstEntity)baseObj)._rawData);

                syncList.Add(syncObj);
            }

            // ★ Base の DataList を Sync のリストで丸ごと挿げ替え
            if (syncList != null) {
                _baseDataObjMgr._dataList = syncList;

                foreach(var obj in _baseDataObjMgr._dataList) {
                    SetParent(obj);
                }
            }
        }


        // ---------------------------------------------------------
        // Delete（SyncDataObj → BaseDataObj → APIDataObj）
        // ---------------------------------------------------------
        public async Task<bool> DeleteAsync(int dataID) {
            return await DeleteDataObj(dataID);
        }
    }
}
