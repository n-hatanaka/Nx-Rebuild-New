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
    //DataObjを管理するクラス
    //派生先では次のように定義する事
    //public class HaseiObjMgr<T> : DataObjMgr<T> where T : HaseiObj<Guid>
    public  class DataObjMgr<T> where T : DataObj<Guid> {
        protected string _tblName;　//データ名等基本データが格納されるテーブル名
        protected string _s_tblName;//材料など詳細データが格納されるテーブル名
        protected string _infoTbl;//_tblNameに加え栄養素などの集計結果が入っているテーブル

        protected string _w_tblName;
        protected string _ws_tblName;

        //DataObjのList：派生したDataObjも保持できる様Objectにダウンキャストする。
        protected List<Object> _dataList = new List<Object>();
        protected NxDataType _datatype;
        protected DateTime _refreshed_at;//最後にDBと整合を取った時間

        //DataObjのインスタンスを作成する
        //派生先では次のように変更できる。
        //protected override HaseiObj<Guid> CreateInstance(DbState db, AuthProv auth){
        // 派生先専用のコンストラクタを呼ぶ
        // 派生先のコンストラクタの引数が変わってもその様に変更できる。
        // return new HaseiObj<Guid>(db, auth);}
        protected virtual T CreateInstans(InMemoryDatabaseState db, AuthenticationStateProvider auth) {
            return (T)Activator.CreateInstance(typeof(T), db, auth);
        }

        private InMemoryDatabaseState _dbstate;
        private AuthenticationStateProvider _authProv;
        protected IDbConnection DBcon { get => _dbstate.Connection; }
        protected AuthenticationStateProvider authProv { get => _authProv; }

        protected IEnumerable<T> DataList => _dataList.Cast<T>();

        public DataObjMgr(InMemoryDatabaseState db, AuthenticationStateProvider auth) {
            _dbstate = db;
            _authProv = auth;
            Initialize();
        }

        public virtual void Initialize(string strWhere = "") {
            //管理するDataObjを生成してリストに登録
            string sql = $"SELECT * FROM \"{_tblName}\"";
            if (!string.IsNullOrWhiteSpace(strWhere)) {
                sql += $" WHERE {strWhere}";
            }
            sql += ";";

            var records = DBcon.Query<IDictionary<string, object>>(sql);
            foreach (var record in records) {
                T readData = CreateInstans(_dbstate, _authProv);
                readData.SetPropertys(record);
                _dataList.Add(readData);
            }
        }
    }
}
