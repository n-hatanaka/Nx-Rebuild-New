using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.shared;
using System.Data;
using System.Data.Common;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Text.Json;

namespace NxRebuild.shared {
    public class SyncBaseDataObjMgr<T, TKey> where T : BaseDataObj<TKey> , IBaseDataObjMgr<T, TKey>, new() {
        private BaseDataObjMgr<T, TKey> _baseDataObjMgr;

        public string TableName { get; set; }
        public string DataTableName { get; set; }
        public string InfoTableName { get; set; }

        public string WarehouseTableName { get; set; }
        public string WarehouseSupplyTableName { get; set; }

        public NxDataType DataType { get => _baseDataObjMgr.DataType; set => _baseDataObjMgr.DataType = value; }
        public DateTime RefreshedAt { get => _baseDataObjMgr.Refreshed_at; set => _baseDataObjMgr.Refreshed_at = value; }

        public HttpClient Http { get => _baseDataObjMgr._http; set => _baseDataObjMgr._http = value; }

        public string TenantCode { get => _baseDataObjMgr.TenantCode; set => _baseDataObjMgr.TenantCode = value; }

        public Guid CurrentUserID { get => _baseDataObjMgr.CurrentUserID; set => _baseDataObjMgr.CurrentUserID = value; }

        public IDbConnection DBcon { get => _baseDataObjMgr.DBcon; set => _baseDataObjMgr.DBcon = value; }

        public List<T> DataList {
            get { return _baseDataObjMgr.DataList.ToList(); }
            // set { _baseDataObjMgr._dataList = value.Cast<Object>().ToList(); }
        }

        public SyncBaseDataObjMgr(DbContext db, HttpClient http, string tenantCode, Guid currentUserId) {
            _baseDataObjMgr = new BaseDataObjMgr<T, TKey>() {
                DbConnection = db.Database.GetDbConnection(),
                Http = http,
                TenantCode = tenantCode,
                CurrentUserID = currentUserId
            };

            TableName = _baseDataObjMgr._tblName;
            DataTableName = _baseDataObjMgr._s_tblName;
            InfoTableName = _baseDataObjMgr._infoTbl;

            WarehouseTableName = _baseDataObjMgr._w_tblName;
            WarehouseSupplyTableName = _baseDataObjMgr._ws_tblName;
        }

        public async Task Initialize(string whereClause = "") => await _baseDataObjMgr.Initialize(whereClause);

        public async Task<bool> DeleteDataObj(TKey dataId) => await _baseDataObjMgr.DeleteDataObj(dataId);

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
