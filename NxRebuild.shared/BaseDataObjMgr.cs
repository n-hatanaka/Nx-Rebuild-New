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
    // マネージャの機能だけを外出しするインターフェース（これに型は不要）
    public interface IBaseDataObjMgr { }

    //DataObjを管理するクラス
    //派生先では次のように定義する事
    //public class HaseiObjMgr<T, Guid> : DataObjMgr<T, TKey> where T : HaseiObj<Guid>

    public class BaseDataObjMgr<T, TKey> : IBaseDataObjMgr where T : BaseDataObj<TKey>, new() {
        protected string _tblName;　//データ名等基本データが格納されるテーブル名
        protected string _s_tblName;//材料など詳細データが格納されるテーブル名
        protected string _infoTbl;//_tblNameに加え栄養素などの集計結果が入っているテーブル

        protected string _w_tblName;
        protected string _ws_tblName;

        //DataObjのList：派生したDataObjも保持できる様Objectにダウンキャストする。
        protected List<Object> _dataList = new List<Object>();
        protected NxDataType _datatype;
        protected DateTime _refreshed_at;//最後にDBと整合を取った時間

        protected HttpClient _http;

        public string TenatCode { get; set; }


        protected IDbConnection DBcon { get; set; }
     

        protected IEnumerable<T> DataList => _dataList.Cast<T>();

        //public DataObjMgr(HttpClient Http, InMemoryDatabaseState db, CustomAuthStateProvider auth) {
        //    _dbstate = db;
        //    _authProv = auth;

        //    Initialize();
        //}


        //BaseDataObjのインスタンスを作成する
        protected T CreateDataObj() {
            var obj = new T();
            return obj;
        }

        //データベースからデータを取得する。(クライアント、サーバー共用）
        //コンストラクタで呼び出す事。コンストラクタはサーバー、クライアントそれぞれの派生先で内容変える。
        public virtual async Task Initialize(string strWhere = "") {

            //管理するDataObjを生成してリストに登録
            string sql = $"SELECT * FROM \"{_tblName}\"";
            if (!string.IsNullOrWhiteSpace(strWhere)) {
                sql += $" WHERE {strWhere}";
            }
            sql += ";";

            var records = await DBcon.QueryAsync<KeyedList<string, object>>(sql);
            
            foreach (var record in records) {
                T readData = CreateDataObj();
                readData.DBcon = DBcon;
                readData.TenantCode = TenatCode; 
                readData.SetPropertys(record);
                _dataList.Add(readData);
            }
        }
    }
}
}
