using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.Client.Pages.Auth;
using NxRebuild.shared;
using SQLitePCL;
using System.Data;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    //public class SyncBaseDataObjMgr<T, TKey> where T : BaseDataObj<TKey> , IBaseDataObjMgr<T, TKey>, new()
    public interface ISyncBaseDataObjMgr<TBase, TSync, TKey> : IBaseDataObjMgr<TBase, TKey>
                                        where TBase : BaseDataObj<TKey>
                                        where TSync : SyncBaseDataObj<TKey> {
     
    }
    public abstract class SyncBaseDataObjMgr<TBase, TSync, TKey> : ISyncBaseDataObjMgr<TBase, TSync, TKey>
                                        where TBase : BaseDataObj<TKey>, new()
                                        where TSync : SyncBaseDataObj<TKey>, new() {
        protected BaseDataObjMgr<TBase, TKey> _baseDataObjMgr;

        protected HttpClient _http;

        public abstract string ApiRoute { get ; } // 派生先で設定する事。

        protected CustomAuthStateProvider _auth;

        public string TblName { get => _baseDataObjMgr.TblName; }
        public string S_TblName { get => _baseDataObjMgr.S_TblName;}
        public string InfoTbl { get => _baseDataObjMgr.InfoTbl;}
        public string W_TblName { get => _baseDataObjMgr.W_TblName;}
        public string Ws_TblName { get => _baseDataObjMgr.Ws_TblName;}

        public NxDataType DataType { get => _baseDataObjMgr.DataType; set => _baseDataObjMgr.DataType = value; }
        public DateTime Refreshed_at { get => _baseDataObjMgr.Refreshed_at; }


        public Guid TenantCode { get => _baseDataObjMgr.TenantCode; set => _baseDataObjMgr.TenantCode = value; }

        public Guid CurrentUserID { get => _baseDataObjMgr.CurrentUserID; set => _baseDataObjMgr.CurrentUserID = value; }

        public IDbConnection DBcon { get => _baseDataObjMgr.DBcon; set => _baseDataObjMgr.DBcon = value; }

        public IEnumerable<IBaseDataObj<TKey>> DataList {
            get {
                foreach (var obj in _baseDataObjMgr._dataList) {
                    if (obj is SyncBaseDataObj<TKey> syncObj) {
                        // 必要な世界線情報を付与
                        if (syncObj.Http == null) syncObj.Http = _http;
                        if (syncObj.Auth == null) syncObj.Auth = _auth;
                    }
                }

                return _baseDataObjMgr._dataList
                    .Cast<IBaseDataObj<TKey>>();
            }
        }




        public SyncBaseDataObjMgr( IDbConnection db, HttpClient http, CustomAuthStateProvider auth, Guid tenantCode, Guid currentUserId) {
            //以下派生先での実装例
            //_baseDataObjMgr = new BaseDataObjMgr<T, TKey>(db, tenantCode, currentUserId);

            //_http = http;
            //_auth = auth;


            //次のメソッドの中でテーブル名などの基本的な情報をハードコード
            //DataObjはSyncDataObjとして次のメソッドの中で生成するように派生したDataObjMgrで実装する事。
           
           



        }
        
        protected virtual TSync CreateNewSyncDataObj() {           
            var newSyncObj = new TSync();
            newSyncObj.Http = _http;
            newSyncObj.Auth = _auth;
            return newSyncObj;
        }

        //データベースからデータを取得する。(クライアント、サーバー共用）
        //コンストラクタで呼び出してもいいかも
        public virtual async Task Initialize() {

            // ① dynamic で確実にレコードを取る
            var records = await LoadRecordsAsync();

            foreach (var record in records) {
                // ② dynamic(DapperRow) → Dictionary<string, object> に変換
                var dict = new Dictionary<string, object>();
                foreach (var kv in (IDictionary<string, object>)record) {
                    dict[kv.Key] = kv.Value;
                }

                TSync readData = CreateNewSyncDataObj();
                readData.DBcon = DBcon;
                readData.TenantCode = TenantCode;
                readData.Setproperties(dict);

                _baseDataObjMgr._dataList.Add(readData);
            }
        }

        //データベースからデータを取得する。(クライアント、サーバー共用）
        //コンストラクタで呼び出してはいけない。
        public virtual async Task<IEnumerable<dynamic>> LoadRecordsAsync() {
            string sql = $"SELECT * FROM \"{TblName}\" WHERE tenant_code = @TenantCode;";
           

            return await DBcon.QueryAsync<dynamic>(sql, new { TenantCode = TenantCode });
        }


        public virtual async Task<bool> SyncData()
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
                    // ⑥ 既存オブジェクトに更新データを流し込む
                    await target.JsonToTbl(dataJson);
                }
                else
                {
                    // ⑦ 新しい SyncDataObj を作成
                    var newSyncObj = CreateNewSyncDataObj();

                    newSyncObj.DataID = dataId;

                    // ⑧ 新しいデータを流し込む
                    await newSyncObj.JsonToTbl(dataJson);

                    // ⑨ DataList に追加
                    _baseDataObjMgr._dataList.Add(newSyncObj);
                    
                }
            }
        
            return true;
        }
        
        public virtual async Task<List<TKey>> DeleteData(IEnumerable<TKey> dataIDs)
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
       
       public virtual async Task<bool> DeleteDataObj(TKey dataID) {
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

        public virtual string LoadMultipleDataAsJson(List<TKey> idList) => _baseDataObjMgr.LoadMultipleDataAsJson(idList);

        public virtual Task DistributeJsonData(string json) => _baseDataObjMgr.DistributeJsonData(json);
        public virtual void RemoveFromList(BaseDataObj<TKey> obj) => _baseDataObjMgr.RemoveFromList(obj);
           

        }

}
