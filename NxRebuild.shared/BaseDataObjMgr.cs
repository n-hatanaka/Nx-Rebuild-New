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

    public abstract class BaseDataObjMgr<T, TKey> : IBaseDataObjMgr where T : BaseDataObj<TKey>, new() {
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

        public Guid CurrentUserID {  get; set; }


        protected IDbConnection DBcon { get; set; }
     

        protected IEnumerable<IBaseDataObj<TKey>> DataList => _dataList.Cast<IBaseDataObj<TKey>>();

        //public DataObjMgr(HttpClient Http, InMemoryDatabaseState db, CustomAuthStateProvider auth) {
        //    _dbstate = db;
        //    _authProv = auth;

        //    Initialize();
        //}
        protected abstract TKey GenerateDataID();

        //BaseDataObjのインスタンスを作成する
        protected T CreateDataObj() {
            var obj = new T();
            return obj;
        }

        public T CreateNewDataObj() {
            var dataObj = new T();
            dataObj.DBcon = DBcon; 
            dataObj.TenantCode = TenatCode;
            dataObj.CurrUsrID = CurrentUserID;
            dataObj.DataID = GenerateDataID();
            dataObj.SetPropertys(GetEmptySchema());
            _dataList.Add(dataObj);
            return dataObj;
        }

        //新規レコードの場合に必要になる空のレコードを生成する。
        protected Dictionary<string, object> GetEmptySchema() {
            // 1=0 で空の結果を要求し、先頭（というか実質これだけ）を取得
            // dynamicで受けることでメタデータを保持できる
            var result = DBcon.QueryFirstOrDefault<dynamic>($"SELECT * FROM {_tblName} WHERE 1 = 0");

            // DapperRowは IDictionary<string, object> にキャスト可能
            var dictionary = (IDictionary<string, object>)result;

            // もしテーブル名が間違っていたりして null が返る場合に備えてハンドリング
            if (dictionary == null) {
                throw new Exception($"テーブル {_tblName} が見つからないか、スキーマを取得できませんでした。");
            }

            // キーのみ抽出して値をnullで初期化して返す
            return dictionary.Keys.ToDictionary(key => key, key => (object)null);
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

            var records = await DBcon.QueryAsync<Dictionary<string, object>>(sql);
            
            foreach (var record in records) {
                T readData = CreateDataObj();
                readData.DBcon = DBcon;
                readData.TenantCode = TenatCode; 
                readData.SetPropertys(record);
                _dataList.Add(readData);
            }
        }

        //指定したデータを削除し、_dataListからオブジェクトを削除
        public async Task<bool> DeleteDataObj(TKey dataID) {
            var target = DataList.FirstOrDefault(x => x.DataID.Equals(dataID));

            if (target != null) {
                // 見つかった場合の処理
                var transaction = DBcon.BeginTransaction();
                if (!(await target.DeleteQueryExec(transaction)))
                    transaction.Rollback();
                else {
                    transaction.Commit();
                    _dataList.Remove(dataID);
                    
                    return true;
                }

            }
            return false;
        }
        
        public string LoadMultipleDataAsJson(List<TKey> idList)
        {
            var jsonResults = new List<string>();
        
            foreach (var id in idList)
            {
                // 1. DataList から該当するオブジェクトを特定
                // 既存の DataList（Object型）を T にキャストして検索
                var dataObj = DataList.FirstOrDefault(d => d.DataID.Equals(id));
        
                if (dataObj != null)
                {
                    // 2. 各オブジェクトの LoadDataAsJson() を呼ぶ
                    // （BaseDataObj側でID等のプロパティがセットされている前提）
                    jsonResults.Add(dataObj.LoadDataAsJson());
                }
            }
        
            // 3. 個別のJSON文字列を結合して一つのJSON配列にする
            // 各jsonResultsの要素は既に文字列化されているため、
            // 単純にカンマで繋いで [] で囲みます。
            return "[" + string.Join(",", jsonResults) + "]";
        }
        
        
        public void DistributeJsonData(string json)
        {
            // 1. JSON全体をレコードのリストにパース
            var allRecords = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
            
            // 2. IDごとにレコードをグループ化する
            // 親データだけでなく、子データも混ざっているため、親ID（parent_id または id）でまとめる
            var groupedRecords = allRecords.GroupBy(r => 
                r.ContainsKey("parent_id") ? r["parent_id"] : r["id"]
            );
        
            foreach (var group in groupedRecords)
            {
                var id = (TKey)Convert.ChangeType(group.Key, typeof(TKey));
                
                // 3. IDに対応するオブジェクトを探す
                var obj = DataList.FirstOrDefault(d => d.DataID.Equals(id));
                
                if (obj == null)
                {
                    // 存在しなければ新規作成
                    obj = CreateDataObj();
                    obj.DataID = id; // IDをセット
                    _dataList.Add(obj);
                }
        
                // 4. そのオブジェクト専用のJSONを作成して渡す
                // グループ化したレコードを再度JSON文字列にして、個別のSaveJsonDataへ流し込む
                string individualJson = JsonSerializer.Serialize(group.ToList());
                obj.SaveJsonData(individualJson);
            }
            
            _refreshed_at = DateTime.Now;
        }
    }
}
}
