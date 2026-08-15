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
    public interface IBaseDataObjMgr<T, TKey>  where T : BaseDataObj<TKey> {
        Guid CurrentUserID { get; set; }
        IEnumerable<IBaseDataObj<TKey>> DataList { get; }
        NxDataType DataType { get; set; }
        IDbConnection DBcon { get; set; }
        DateTime Refreshed_at { get; }
        Guid TenantCode { get; set; }

        string TblName { get; }
        string S_TblName { get; }
        string InfoTbl { get;  }

        string W_TblName { get;  }
        string Ws_TblName { get; }

        Task<List<TKey>> DeleteData(IEnumerable<TKey> dataIDs);

        Task DistributeJsonData(string json);
        Task Initialize();
        string LoadMultipleDataAsJson(List<TKey> idList);
        
    }

    // サーバー向けマネージャの機能だけを外出しするインターフェース
    public interface IsrvBaseDataObjMgr<T, TKey> where T : BaseDataObj<TKey> {
        Guid CurrentUserID { get; set; }
        IEnumerable<IBaseDataObj<TKey>> DataList { get; }
        NxDataType DataType { get; set; }
        IDbConnection DBcon { get; set; }
        DateTime Refreshed_at { get; }
        Guid TenantCode { get; set; }

        string TblName { get; }
        string S_TblName { get; }
        string InfoTbl { get; }

        string W_TblName { get; }
        string Ws_TblName { get; }

        Task<List<TKey>> DeleteData(IEnumerable<TKey> dataIDs);

        Task DistributeJsonData(string json);
        Task Initialize();
        string LoadMultipleDataAsJson(List<TKey> idList);
        void RemoveFromList(BaseDataObj<TKey> obj);
    }
        //DataObjを管理するクラス
        //派生先では次のように定義する事
        //public class HaseiObjMgr<T, Guid> : DataObjMgr<T, TKey> where T : HaseiObj<Guid>

    public abstract class BaseDataObjMgr<T, TKey> : IBaseDataObjMgr<T, TKey> , IsrvBaseDataObjMgr<T, TKey> where T : BaseDataObj<TKey>, new() {
        protected string _tblName;　
        protected string _s_tblName;
        protected string _infoTbl;

        protected string _w_tblName;
        protected string _ws_tblName;
        public string TblName { get => _tblName; }//データ名等基本データが格納されるテーブル名
        public string S_TblName { get => _s_tblName; }//明細データが格納されるサブテーブル名
        public string InfoTbl { get => _infoTbl; }//TblNameに加え栄養素などの集計結果が入っているテーブル(プロパティをビューから取得したいときはこれを使う

        public string W_TblName { get => _w_tblName; }
        public string Ws_TblName { get => _ws_tblName; }

        public NxDataType DataType { get; set; }

        public Guid TenantCode { get; set; }

        public Guid CurrentUserID { get; set; }


        public IDbConnection DBcon { get; set; }


        //DataObjのList：派生したDataObjも保持できる様Objectにダウンキャストする。
        public List<Object> _dataList = new List<Object>();

        public IEnumerable<IBaseDataObj<TKey>> DataList => _dataList.Cast<IBaseDataObj<TKey>>();
        public DateTime Refreshed_at {
            get {
                DateTime latestUpdate = DateTime.MinValue;
                DateTime latestLocked = DateTime.MinValue;

                foreach (var obj in DataList) {
                    // DataList は ISyncBaseDataObj<TKey> を返すのでそのまま使える
                    if (obj.Update_at > latestUpdate)
                        latestUpdate = obj.Update_at;

                    if (obj.LockedAt > latestLocked)
                        latestLocked = obj.LockedAt;
                }

                // 古い方を返す
                return latestUpdate < latestLocked
                    ? latestUpdate
                    : latestLocked;
            }
        }

        public BaseDataObjMgr(IDbConnection db , Guid tenantCode , Guid currUserID) {
            DBcon = db;
            TenantCode = tenantCode;
            CurrentUserID = currUserID;
            //テーブル名などの基本情報は派生先のコンストラクタでハードコードする事。
            //Initialize()はインスタンス生成元が呼び出す事(この中で呼んではいけない)
        }
        protected abstract TKey GenerateDataID();

        //BaseDataObjのインスタンスを作成する
        protected T CreateDataObj() {
            var obj = new T();
            return obj;
        }

        public T CreateNewDataObj() {
            var dataObj = new T();
            dataObj.DBcon = DBcon;
            dataObj.TenantCode = TenantCode;
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
        //コンストラクタで呼び出してはいけない。
        public virtual async Task<IEnumerable<Dictionary<string, object>>> LoadRecordsAsync() {
            // Base は “世界線の物理層” なので意味を持たない
            // 派生先で SQL を完全に書き換える前提なら、ここは空実装でいい
            string sql = $"SELECT * FROM \"{_tblName}\";";

            return await DBcon.QueryAsync<Dictionary<string, object>>(sql);
        }

        //データベースからデータを取得する。(クライアント、サーバー共用）
        //コンストラクタで呼び出してはいけない。
        public virtual async Task Initialize() {
            var records = await LoadRecordsAsync();

            foreach (var record in records) {
                T obj = CreateDataObj();
                obj.DBcon = DBcon;
                obj.TenantCode = TenantCode;
                obj.SetPropertys(record);

                _dataList.Add(obj);
            }
        }

        // 指定したID群を順次削除し、削除に失敗したIDを返す。
        // 返り値のリストが空なら全件成功。
        // UIはこの返り値を観測して成功／部分失敗を判断する。
        public virtual async Task<List<TKey>> DeleteData(IEnumerable<TKey> dataIDs) {
            var failedLst = new List<TKey>();

            foreach (var id in dataIDs) {
                if (!await DeleteDataObj(id))
                    failedLst.Add(id);
            }

            return failedLst;
        }


        //指定したデータを削除し、_dataListからオブジェクトを削除
        public virtual async Task<bool> DeleteDataObj(TKey dataID) {
            var target = (BaseDataObj<TKey>)_dataList.FirstOrDefault(x => ((BaseDataObj<TKey>)x).DataID.Equals(dataID));

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

        public virtual void RemoveFromList(BaseDataObj<TKey> obj) {
            _dataList.Remove(obj);
        }

        //idListに含まれるIDのDataObjを順次Json化して返す。
        //API,Cliant双方での使用を想定している。
        public string LoadMultipleDataAsJson(List<TKey> idList) {
            var jsonResults = new List<string>();

            foreach (var id in idList) {
                // 1. DataList から該当するオブジェクトを特定
                // 既存の DataList（Object型）を T にキャストして検索
                var dataObj = DataList.FirstOrDefault(d => d.DataID.Equals(id));

                if (dataObj != null) {
                    // 2. 各オブジェクトの LoadDataAsJson() を呼ぶ
                    jsonResults.Add(dataObj.TblToJson());
                }
            }

            // 3. 個別のJSON文字列を結合して一つのJSON配列にする
            // 各jsonResultsの要素は既に文字列化されているため、
            // 単純にカンマで繋いで [] で囲みます。
            return "[" + string.Join(",", jsonResults) + "]";
        }

        //クライアントから送られたJSONをDataObjに振り分ける
            //リアルセーブ（＝サーバー側の永続化）ができない限り、
            //インメモリ世界線は“正本”になれない。
            //だから巨大 JSON を分配して BaseObj に流し込むメソッドが必要になる。
        //APIからのみ使用する。
        public async Task DistributeJsonData(string json) {
            // 1. JSON全体をレコードのリストにパース
            var allRecords = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

            // 2. IDごとにレコードをグループ化する
            // 親データだけでなく、子データも混ざっているため、親ID（parent_id または id）でまとめる
            var groupedRecords = allRecords.GroupBy(r =>
                r.ContainsKey("parent_id") ? r["parent_id"] : r["id"]
            );

            foreach (var group in groupedRecords) {
                var id = (TKey)Convert.ChangeType(group.Key, typeof(TKey));

                // 3. IDに対応するオブジェクトを探す
                var obj = DataList.FirstOrDefault(d => d.DataID.Equals(id));

                if (obj == null) {
                    // 存在しなければ新規作成
                    obj = CreateDataObj();
                    obj.DataID = id; // IDをセット
                    _dataList.Add(obj);
                }

                // 4. そのオブジェクト専用のJSONを作成して渡す
                // グループ化したレコードを再度JSON文字列にして、個別のJsonToTblへ流し込む
                string individualJson = JsonSerializer.Serialize(group.ToList());
                await obj.JsonToTbl(individualJson);
            }
        }
    }
}
