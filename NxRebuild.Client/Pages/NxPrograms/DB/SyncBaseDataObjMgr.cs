using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using NxRebuild.Client.Pages.Auth;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.shared;
using System.Data;
using System.Data.Common;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public class SyncBaseDataObjMgr<T, TKey> where T : BaseDataObj<TKey> , IBaseDataObjMgr<T, TKey>, new() {
        protected BaseDataObjMgr<T, TKey> _baseDataObjMgr;

        protected HttpClient _http;

        protected CustomAuthStateProvider _auth;

        public string TableName { get; set; }
        public string DataTableName { get; set; }
        public string InfoTableName { get; set; }
        public string WarehouseTableName { get; set; }
        public string WarehouseSupplyTableName { get; set; }

        public NxDataType DataType { get => _baseDataObjMgr.DataType; set => _baseDataObjMgr.DataType = value; }
        public DateTime RefreshedAt { get => _baseDataObjMgr.Refreshed_at; set => _baseDataObjMgr.Refreshed_at = value; }


        public Guid TenantCode { get => _baseDataObjMgr.TenantCode; set => _baseDataObjMgr.TenantCode = value; }

        public Guid CurrentUserID { get => _baseDataObjMgr.CurrentUserID; set => _baseDataObjMgr.CurrentUserID = value; }

        public IDbConnection DBcon { get => _baseDataObjMgr.DBcon; set => _baseDataObjMgr.DBcon = value; }

        public IEnumerable<ISyncBaseDataObj<TKey>> DataList {
            get {
                foreach (var obj in _baseDataObjMgr._dataList) {
                    if (obj is SyncBaseDataObj<TKey> syncObj) {
                        // 必要な世界線情報を付与
                        if (syncObj.Http == null) syncObj.Http = _http;
                        if (syncObj.Auth == null) syncObj.Auth = _auth;
                    }
                }

                return _baseDataObjMgr._dataList
                    .Cast<ISyncBaseDataObj<TKey>>();
            }
        }

        public DateTime Refreshed_at {
            get {
                DateTime latestUpdate = DateTime.MinValue;
                DateTime latestLocked = DateTime.MinValue;
        
                foreach (var obj in DataList) {
                    // DataList は ISyncBaseDataObj<TKey> を返すのでそのまま使える
                    if (obj.Update_at > latestUpdate)
                        latestUpdate = obj.Update_at;
        
                    if (obj.Locked_at > latestLocked)
                        latestLocked = obj.Locked_at;
                }
        
                // 古い方を返す
                return latestUpdate < latestLocked
                    ? latestUpdate
                    : latestLocked;
            }
        }


        public SyncBaseDataObjMgr( DbConnection db, HttpClient http, CustomAuthStateProvider auth, Guid tenantCode, Guid currentUserId) {
            //以下派生先での実装例
            //_baseDataObjMgr = new BaseDataObjMgr<T, TKey>(db, tenantCode, currentUserId);

            //_http = http;
            //_auth = auth;

            //次のメソッドの中でテーブル名などの基本的な情報をハードコード
            //DataObjはSyncDataObjとして次のメソッドの中で生成するように派生したDataObjMgrで実装する事。
            //なお生成したSyncDataObj内の
            //・Http
            //・Auth
            //　の二つのメンバはこのラッパークラス内でインスタンスを付与するので、生成時にその二つはNullのままでいい。
            //_baseDataObjMgr.Initialize();

            // SyncDataObjMgr は BaseDataObjMgr を内包し、同型性を保つために
            // Base のテーブル情報を Sync 側にコピーする必要があるため以下を記述

            //TableName = _baseDataObjMgr.TblName;
            //DataTableName = _baseDataObjMgr.S_TblName;
            //InfoTableName = _baseDataObjMgr.InfoTbl;

        }
        public async Task<List<TKey>> DeleteData(IEnumerable<TKey> dataIDs)
       {
           var url = $"{ApiRoute}/Delete";
       
           HttpResponseMessage response;
       
           try {
               response = await Http.PostAsJsonAsync(url, dataIDs);
           }
           catch {
               return dataIDs.ToList(); // 通信失敗 → 全件失敗
           }
       
           if (!response.IsSuccessStatusCode) {
               return dataIDs.ToList(); // API側失敗 → 全件失敗
           }
       
           // ★ API は List<string> を返す
           var failedStrLst = await response.Content.ReadFromJsonAsync<List<string>>();
       
           if (failedStrLst == null)
               return dataIDs.ToList(); // 想定外レスポンス
       
           // ★ TKey に変換（UUIDv7 でも int でもここで吸収）
           var failedLst = failedStrLst
               .Select(x => (TKey)Convert.ChangeType(x, typeof(TKey)))
               .ToList();
       
           // ★ 成功した ID をローカル側でも削除
           foreach (var id in dataIDs) {
               if (!failedLst.Contains(id)) {
                   await DeleteDataObj(id); // ローカルリストから削除
               }
           }
       
           return failedLst;
       }
       
       public async Task<bool> DeleteDataObj(TKey dataID) {
           return _baseDataObjMgr.DeleteDataObj(dataID);
       }


        public string LoadMultipleDataAsJson(List<TKey> idList) => _baseDataObjMgr.LoadMultipleDataAsJson(idList);

        public void DistributeJsonData(string json) => _baseDataObjMgr.DistributeJsonData(json);

        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion INotifyPropertyChanged Members
    }

}
