using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.shared;
using System.Data;
using System.Data.Common;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public class DataobjMgr {
        protected string _tblName;　//データ名等基本データが格納されるテーブル名
        protected string _s_tblName;//材料など詳細データが格納されるテーブル名
        protected string _infoTbl;//_tblNameに加え栄養素などの集計結果が入っているテーブル

        protected string _w_tblName;
        protected string _ws_tblName;
        protected List<DataObj<Guid>> _dataList;
        protected NxDataType _datatype;
        protected DateTime _refreshed_at;//最後にDBと整合を取った時間

        private InMemoryDatabaseState _dbstate;
        private AuthenticationStateProvider _authProv;
        protected IDbConnection DBcon { get => _dbstate.Connection; }
        protected AuthenticationStateProvider authProv { get => _authProv; }

        protected List<DataObj<Guid>> DataList { get => _dataList; }

        public DataobjMgr(InMemoryDatabaseState db, AuthenticationStateProvider auth) {
            _dbstate = db;
            _authProv = auth;
        }

        protected virtual void Initialize() {
            if (DBcon == null) {
                return;
            }
            //このオブジェクトが管理するテーブルのレコードごとのオブジェクトを作成し、リストに登録
            var records = DBcon.Query<IDictionary<string, object>>("SELECT * FROM \"{_tblName}\";");
            foreach (var record in records) {
                DataObj<Guid> readData = new DataObj<Guid>(_dbstate, _authProv);
                readData.SetPropertys(record);
                _dataList.Add(readData);
            }
        }
    }
}
