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
        : SyncBaseDataObjMgr<ZmstEntity, SyncZmstEntity, int> {
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

        // ---------------------------------------------------------
        // 新規作成（SyncDataObj）
        // ---------------------------------------------------------
        public SyncZmstEntity CreateNewSyncObj() {
            // BaseDataObjで新規作成
            var baseObj = _baseDataObjMgr.CreateNewDataObj();

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

            // BaseDataObjの DataList を SyncDataObjに変換
            foreach (var baseObj in _baseDataObjMgr.DataList) {
                var syncObj = new SyncZmstEntity();
                syncObj.Http = _http;
                syncObj.Auth = _auth;

                syncObj.DBcon = DBcon;
                syncObj.TenantCode = TenantCode;
                syncObj.CurrUsrID = CurrentUserID;

                // BaseDataObjの rawData をコピー
                syncObj.Setproperties(((ZmstEntity)baseObj)._rawData);

                // tan_m をロード
                await syncObj.LoadTanMAsync();

                // DataList に追加
                _baseDataObjMgr._dataList.Add(syncObj);
            }
        }

        // ---------------------------------------------------------
        // Save（SyncDataObj → BaseDataObj → APIDataObj）
        // ---------------------------------------------------------
        public async Task<bool> SaveAsync(SyncZmstEntity syncObj) {
            // BaseDataObjで保存
            bool ok = await syncObj.SaveAsync();
            if (!ok)
                return false;

            // SyncDataObj → APIへ送信
            var json = syncObj.TblToJson();
            var url = $"{ApiRoute}/Save/{syncObj.DataID}";

            HttpResponseMessage response;
            try {
                response = await _http.PostAsJsonAsync(url, json);
            } catch {
                return false;
            }

            if (!response.IsSuccessStatusCode)
                return false;

            // APIが返す正本DataObjの JSON をローカルDBに反映
            var updatedJson = await response.Content.ReadAsStringAsync();
            await syncObj.JsonToTbl(updatedJson);

            return true;
        }

        // ---------------------------------------------------------
        // Delete（SyncDataObj → BaseDataObj → APIDataObj）
        // ---------------------------------------------------------
        public async Task<bool> DeleteAsync(int dataID) {
            return await DeleteDataObj(dataID);
        }
    }
}
