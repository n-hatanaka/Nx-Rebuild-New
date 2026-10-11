using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.Client.Pages.Auth;
using NxRebuild.shared;
using System.Data;
using System.Net.Http.Json;
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB {

    public interface ISyncBaseDataObjMgr<TBase, TSync, TKey> : IBaseDataObjMgr<TBase, TKey>
        where TBase : BaseDataObj<TKey>
        where TSync : SyncBaseDataObj<TKey> {
    }

    public abstract class SyncBaseDataObjMgr<TBase, TSync, TKey> : ISyncBaseDataObjMgr<TBase, TSync, TKey>
        where TBase : BaseDataObj<TKey>, new()
        where TSync : SyncBaseDataObj<TKey>, new() {

        protected BaseDataObjMgr<TBase, TKey> _baseDataObjMgr;
        protected HttpClient _http;
        protected CustomAuthStateProvider _auth;

        public abstract string ApiRoute { get; }

        public string TblName => _baseDataObjMgr.TblName;
        public string S_TblName => _baseDataObjMgr.S_TblName;
        public string InfoTbl => _baseDataObjMgr.InfoTbl;
        public string W_TblName => _baseDataObjMgr.W_TblName;
        public string Ws_TblName => _baseDataObjMgr.Ws_TblName;

        public NxDataType DataType { get => _baseDataObjMgr.DataType; set => _baseDataObjMgr.DataType = value; }
        public DateTime Refreshed_at => _baseDataObjMgr.Refreshed_at;

        public Guid TenantCode { get => _baseDataObjMgr.TenantCode; set => _baseDataObjMgr.TenantCode = value; }
        public Guid CurrentUserID { get => _baseDataObjMgr.CurrentUserID; set => _baseDataObjMgr.CurrentUserID = value; }

        public IDbConnection DBcon { get => _baseDataObjMgr.DBcon; set => _baseDataObjMgr.DBcon = value; }

        public IEnumerable<IBaseDataObj<TKey>> DataList {
            get => _baseDataObjMgr._dataList.Cast<IBaseDataObj<TKey>>();            
        }

        public SyncBaseDataObjMgr(IDbConnection db, HttpClient http, CustomAuthStateProvider auth,
                                  Guid tenantCode, Guid currentUserId) {
            // 派生先で BaseDataObjMgr を生成すること
        }

        public virtual IBaseDataObj<TKey>? Get(TKey id) => _baseDataObjMgr.Get(id);
        public virtual IBaseDataObj<TKey> CreateNewDataObj(TKey? parentID) {
            var baseObj = _baseDataObjMgr.CreateNewDataObj(parentID);
            var syncObj = new TSync();
            syncObj.Http = _http;
            syncObj.Auth = _auth;
            syncObj._dataObj = (TBase)baseObj;
            syncObj.ParentID = parentID;
            syncObj.SelfObjMgr = this;
            syncObj.DBcon = DBcon;
            syncObj.TenantCode = TenantCode;
            syncObj.CurrUsrID = CurrentUserID;
            _baseDataObjMgr._dataList.Add(syncObj);//親の登録（Setparent）はまだしない
            return syncObj;
        }


        public virtual void SetParent(IBaseDataObj<TKey> obj) {
            _baseDataObjMgr.SetParent(obj);
        }



        public virtual void InsertNewDataItem(IBaseDataObj<TKey> obj) {
            _baseDataObjMgr.InsertNewDataItem(obj);
        }


        // データベースからデータを取得する（クライアント・サーバー共用）
        public virtual async Task Initialize(RecordQuery q) {

            // BaseDataObjでプロパティをロード
            await _baseDataObjMgr.Initialize(q);

            // ★ Sync 用の新しいリストを作る
            var syncList = new List<IBaseDataObj<TKey>>();

            foreach (var baseObj in _baseDataObjMgr._dataList) {//ここではDataListで呼び出してはいけない

                var syncObj = new TSync();
                syncObj.Http = _http;
                syncObj.Auth = _auth;
                syncObj._dataObj = (TBase)baseObj;
                syncObj.SelfObjMgr = this;
                syncObj.DBcon = DBcon;
                syncObj.TenantCode = TenantCode;
                syncObj.CurrUsrID = CurrentUserID;

                syncList.Add(syncObj);
            }

            // ★ Base の DataList を Sync のリストで丸ごと挿げ替え
            if (syncList != null) {
                _baseDataObjMgr._dataList = syncList;

                foreach (var obj in _baseDataObjMgr._dataList) {
                    SetParent(obj);
                }
            }
        }

        public virtual async Task<IEnumerable<dynamic>> LoadRecordsAsync() {
            string sql = $"SELECT * FROM \"{TblName}\" WHERE tenant_code = @TenantCode;";
            return await DBcon.QueryAsync<dynamic>(sql, new { TenantCode = TenantCode });
        }

        public virtual async Task<bool> SyncData() {
            DateTime refreshed_at = _baseDataObjMgr.Refreshed_at;

            var url = $"{ApiRoute}/sync/{refreshed_at:O}";
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return false;

            var tran = DBcon.BeginTransaction();
            var json = await response.Content.ReadAsStringAsync();
            var syncResult = JsonSerializer.Deserialize<SyncAllResult>(json);

            if (syncResult?.Items == null)
                return false;

            foreach (var item in syncResult.Items) {

                var dataId = item.Key;
                var dataJson = item.Data;

                var target = _baseDataObjMgr.Get(dataId);

                if (target != null) {
                    await target.JsonToTblAsync(dataJson,tran);
                }
                else {
                    var newSyncObj = CreateNewDataObj(default(TKey));
                    newSyncObj.DataID = dataId;
                    await newSyncObj.JsonToTblAsync(dataJson,tran);
                    _baseDataObjMgr._dataList.Add(newSyncObj);
                }
            }
            
            tran.Commit();

            // SetParent を再度呼び出して、全てのオブジェクトに対して親子関係を更新
            // Tree順にデータが届く事はほぼない
            foreach (var obj in DataList) {
                SetParent(obj);
            }


            return true;
        }

        public virtual async Task<List<TKey>> DeleteData(IEnumerable<TKey> dataIDs, bool softDelete = false) {

            var url = $"{ApiRoute}/Delete";

            HttpResponseMessage response;

            try {
                response = await _http.PostAsJsonAsync(url, dataIDs);
            } catch (Exception ex) {
                Console.WriteLine("DeleteData ERROR: HTTP通信失敗");
                Console.WriteLine(ex.Message);
                return dataIDs.ToList(); // 全件失敗扱い
            }

            if (!response.IsSuccessStatusCode) {
                Console.WriteLine($"DeleteData ERROR: APIステータス異常 ({response.StatusCode})");
                return dataIDs.ToList();
            }

            List<string>? failedStrLst = null;

            try {
                // API の JSON をそのまま取り出す
                failedStrLst = await response.Content.ReadFromJsonAsync<List<string>>();
            } catch (Exception ex) {
                Console.WriteLine("DeleteData ERROR: JSONパース失敗");
                Console.WriteLine(ex.Message);
                return dataIDs.ToList(); // 全件失敗扱い
            }

            // 失敗IDが返ってきた場合
            var failedLst = failedStrLst
                .Select(x => (TKey)Convert.ChangeType(x, typeof(TKey)))
                .ToList();

            //  成功したIDを抽出する
            var successIds = dataIDs
                .Where(id => !failedLst.Contains(id))
                .ToList();

            // 成功したIDだけローカルオブジェクト削除 ★
            foreach (var id in successIds) {
                try {
                    await _DeleteDataObjFromList(id, softDelete);
                } catch (Exception ex) {
                    Console.WriteLine($"DeleteDataObjFromList ERROR: ID={id}");
                    Console.WriteLine(ex.Message);

                    // ローカル削除失敗も「失敗ID」に追加して返す
                    failedLst.Add(id);
                }
            }

            // 最終的に「失敗したIDだけ」返す（API仕様と一致）
            return failedLst;
        }



        protected virtual async Task<bool> _DeleteDataObjFromList(TKey dataID, bool softDelete = false) {
            return await _baseDataObjMgr._DeleteDataObjFromList(dataID, softDelete);
        }

        protected class SyncAllResult {
            public DateTime Refreshed_at { get; set; }
            public List<SyncItem> Items { get; set; }
        }

        protected class SyncItem {
            public TKey Key { get; set; }
            public string Data { get; set; }
        }

        public virtual string LoadMultipleDataAsJson(List<TKey> idList)
            => _baseDataObjMgr.LoadMultipleDataAsJson(idList);

        public virtual Task DistributeJsonData(string json)
            => _baseDataObjMgr.DistributeJsonData(json);

        public virtual void RemoveFromList(BaseDataObj<TKey> obj)
            => _baseDataObjMgr.RemoveFromList(obj);

        // ★ 新規レコード用の空辞書（NxTypeMapper の型正本に基づく）
        protected Dictionary<string, object?> GetEmptySchema() {
            if (NxTypeMapper.Current == null)
                throw new Exception("NxTypeMap が初期化されていません。");

            return NxTypeMapper.Current.CreateEmptyRow(TblName);
        }
    }
}