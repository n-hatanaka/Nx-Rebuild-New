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
    //public class SyncBaseDataObjMgr<T, TKey> where T : BaseDataObj<TKey> , IBaseDataObjMgr<T, TKey>, new()
    public interface ISyncBaseDataObjMgr<TSync, TKey>
                                        where TSync : SyncBaseDataObj<TKey> {
     
    }
    public class SyncBaseDataObjMgr<TBase, TSync, TKey>
                                        where TBase : BaseDataObj<TKey>, new()
                                        where TSync : SyncBaseDataObj<TKey>, new() {
        protected BaseDataObjMgr<TBase, TKey> _baseDataObjMgr;

        protected HttpClient _http;

        protected string ApiRoute { get; set; } // 派生先で設定する事。

        protected CustomAuthStateProvider _auth;

        public string TableName { get; set; }
        public string DataTableName { get; set; }
        public string InfoTableName { get; set; }
        public string WarehouseTableName { get; set; }
        public string WarehouseSupplyTableName { get; set; }

        public NxDataType DataType { get => _baseDataObjMgr.DataType; set => _baseDataObjMgr.DataType = value; }
        public DateTime RefreshedAt { get => _baseDataObjMgr.Refreshed_at; }


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




        public SyncBaseDataObjMgr( DbConnection db, HttpClient http, CustomAuthStateProvider auth, Guid tenantCode, Guid currentUserId) {
            //以下派生先での実装例
            //_baseDataObjMgr = new BaseDataObjMgr<T, TKey>(db, tenantCode, currentUserId);

            //_http = http;
            //_auth = auth;

            //_apiRoute = "ハードコードする";

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
        
        protected virtual TSync CreateNewSyncDataObj(TBase baseDataOjb) {
            //本来はabustractにすべきだが実装例として書いておく。具象クラスで適宜修正する事。
            var newSyncObj = new TSync();
            newSyncObj.SetBaseDataObj(baseDataOjb);
            newSyncObj.Http = _http;
            newSyncObj.Auth = _auth;
            return newSyncObj;
        }

        //データベースからデータを取得する。(クライアント、サーバー共用）
        //コンストラクタで呼び出す事。コンストラクタはサーバー、クライアントそれぞれの派生先で内容変える。
        public virtual async Task Initialize(string strWhere = "") {

            string sql = $"SELECT * FROM \"{TableName}\"";
            if (!string.IsNullOrWhiteSpace(strWhere)) {
                sql += $" WHERE {strWhere}";
            }
            sql += ";";

            var records = await DBcon.QueryAsync<Dictionary<string, object>>(sql);

            foreach (var record in records) {
                T readData = _baseDataObjMgr.CreateNewDataObj();
                readData.DBcon = DBcon;
                readData.TenantCode = TenantCode;
                readData.SetPropertys(record);
                var readSyncData = CreateNewSyncDataObj(readData);
                _baseDataObjMgr._dataList.Add(readSyncData);
            }
        }

        public async Task<bool> SyncData()
        {
            DateTime refreshed_at = _baseDataObjMgr.Refreshed_at;
            // ① API に世界線同期点を渡す
            var url = $"{ApiRoute}/sync/{refreshed_at:O}";
            var response = await _http.GetAsync(url);
        
            if (!response.IsSuccessStatusCode)
                return false;
        
            // ② JSON を受け取る
            var json = await response.Content.ReadAsStringAsync();
        
            // ③ パース（API の返却構造に合わせた型を使用）
            var syncResult = JsonSerializer.Deserialize<SyncAllResult>(json);
        
            if (syncResult?.Items == null)
                return false;
        
            // ④ ループして DataObjMgr の世界線を更新
            foreach (var item in syncResult.Items)
            {
                var dataId = item.Key;
                var dataJson = item.Data;
        
                // ⑤ 既存 DataObj を探す
                var target = _baseDataObjMgr.DataList
                    .FirstOrDefault(x => x.DataID.Equals(dataId));
        
                if (target != null)
                {
                    // ⑥ 既存オブジェクトに世界線を流し込む
                    await target.JsonToTbl(dataJson);
                }
                else
                {
                    // ⑦ 新規作成
                    var newObj = _baseDataObjMgr.CreateNewDataObj();
        
                    // DataID をセット（必要なら）
                    newObj.DataID = dataId;
        
                    // ⑧ 世界線を流し込む
                    await newObj.JsonToTbl(dataJson);

                    var newSyncObj = CreateNewSyncDataObj(newObj);

                    // ⑨ DataList に追加
                    _baseDataObjMgr._dataList.Add((object)newSyncObj);
                    
                }
            }
        
            return true;
        }
        
        public async Task<List<TKey>> DeleteData(IEnumerable<TKey> dataIDs)
       {
           var url = $"{ApiRoute}/Delete";
       
           HttpResponseMessage response;
       
           try {
               response = await _http.PostAsJsonAsync(url, dataIDs);
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
           return await _baseDataObjMgr.DeleteDataObj(dataID);
       }


        // API の返却 JSON に合わせた内部型
        protected class SyncAllResult {
            public DateTime Refreshed_at { get; set; }
            public List<SyncItem> Items { get; set; }
        }

        protected class SyncItem {
            public TKey Key { get; set; }
            public string Data { get; set; }
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
