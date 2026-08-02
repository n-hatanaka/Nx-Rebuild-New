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


        public async Task<bool> DeleteDataObj(TKey dataID) {
            // ★対象オブジェクト取得
            var target = (BaseDataObj<TKey>)DataList
                .FirstOrDefault(x => ((BaseDataObj<TKey>)x).DataID.Equals(dataID));

            if (target == null)
                return false;

            // ★まずロックを取る（SetLockAsync は内部でトランザクション管理）
            var lockStatus = await target.SetLockAsync(new LockStatus {
                IsLocked = true,
                LockedByUserId = CurrentUserID.ToString()  // ← NxDataController でセット済み
            });

            // ★ロックが取れなかった場合（他ユーザーがロック中）
            if (!lockStatus.IsLocked || lockStatus.LockedByUserId != CurrentUserID.ToString()) {
                return false;
            }

            // ★ロックが自分のものなので削除処理へ
            var transaction = DBcon.BeginTransaction();

            if (!(await target.DeleteQueryExec(transaction))) {
                transaction.Rollback();

                // ★ロック解除（失敗時も必ず）
                await target.SetLockAsync(new LockStatus {
                    IsLocked = false,
                    LockedByUserId = null
                });

                return false;
            }

            // ★削除成功
            transaction.Commit();

            // ★DataList から削除
            _baseDataObjMgr._dataList.Remove(target);

            // ★ロック解除（成功時も必ず）
            await target.SetLockAsync(new LockStatus {
                IsLocked = false,
                LockedByUserId = null
            });

            return true;
        }
        // 指定したID群を順次削除し、削除に失敗したIDを返す。
        // 返り値のリストが空なら全件成功。
        // UIはこの返り値を観測して成功／部分失敗を判断する。
        public async Task<List<TKey>> DeleteData(IEnumerable<TKey> dataIDs) {
            var failedLst = new List<TKey>();

            foreach (var id in dataIDs) {
                if (!await DeleteDataObj(id))
                    failedLst.Add(id);
            }

            return failedLst;
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
