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

        IBaseDataObj<TKey>? Get(TKey id);

        IBaseDataObj<TKey> CreateNewDataObj(TKey? parentID);
        void InsertNewDataItem(IBaseDataObj<TKey> obj);

        Task<List<TKey>> DeleteData(IEnumerable<TKey> dataIDs, bool softDelete = false);
        void SetParent(IBaseDataObj<TKey> obj);
        Task DistributeJsonData(string json);
        Task Initialize(RecordQuery? query);
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

        Task<List<TKey>> DeleteData(IEnumerable<TKey> dataIDs, bool softDelete = false);

        IBaseDataObj<TKey>? Get(TKey id);


        Task DistributeJsonData(string json);
        Task Initialize(RecordQuery? query);
        string LoadMultipleDataAsJson(List<TKey> idList);
        void RemoveFromList(BaseDataObj<TKey> obj);
        IBaseDataObj<TKey> CreateNewDataObj(TKey? parentID);
    }


  public class Condition
{
    public string Column { get; set; }
    public object Value { get; set; }
    public string Operator { get; set; } = "="; // =, <, >, LIKE, etc.
}
    /*
        ================================
        RecordQuery の使い方（簡易 AND / OR 版）
        ================================

        ■ RecordQuery 
          Initialize() / LoadRecordsAsync() に渡す検索条件の。
          AND / OR / Keyword / Paging / Order をひとまとめにする。

        ■ 目的
          ・検索条件を統一する
          ・ページングと検索を混線させない
          ・Base を汚さず派生先で SQL を自由に書き換えられる
          ・UI / API / Sync が同じ世界線を共有できる

        ================================
        1. AND 条件の書き方
        ================================

        var q = new RecordQuery();
        q.And.Add(new Condition {
            Column = "Name",
            Operator = "=",
            Value = "Akino"
        });

        q.And.Add(new Condition {
            Column = "Age",
            Operator = ">=",
            Value = 4x
        });

        → SQL 例：
          AND Name = @Name
          AND Age >= @Age

        ================================
        2. OR 条件の書き方
        ================================

        q.Or.Add(new Condition {
            Column = "Status",
            Operator = "=",
            Value = "Active"
        });

        q.Or.Add(new Condition {
            Column = "Status",
           Operator = "=",
            Value = "Pending"
        });

        → SQL 例：
          AND (Status = @Status OR Status = @Status)

        ================================
        3. キーワード検索（任意）
        ================================

        q.Keyword = "apple";

        ※ Keyword の実装は派生先で行う。
           例：全列 LIKE、特定列 LIKE など。

        ================================
        4. ページング
        ================================

        q.PageIndex = 0;   // 0-based
        q.PageSize  = 50;  // 0ならページングなし

        → SQL 例：
          LIMIT 50 OFFSET 0

        ※ TotalCount は返却用（API側でセット）

        ================================
        5. ソート
        ================================

        q.OrderBy = "CreatedAt";
        q.OrderDesc = true;

        → SQL 例：
          ORDER BY CreatedAt DESC

        ================================
        6. 実際の呼び出し
        ================================

        await mgr.Initialize(q);

        または

        var records = await mgr.LoadRecordsAsync(q);

        ================================
        7. BuildSql の動作（簡易 AND / OR 版）
        ================================

        ・AND 条件 → そのまま AND で連結
        ・OR 条件 → AND (...) の中に OR で連結
        ・Keyword → 派生先で実装
        ・Order → ORDER BY
        ・Paging → LIMIT / OFFSET

        ================================
        8. 注意点
        ================================

        ・RecordQuery の項目はすべて optional
        ・世界線は RecordQuery ひとつに統一する
        ・Base は汚さず、派生先で SQL を自由に書き換える
        ・必要になったら条件ツリー（完全版）に進化可能

    */
    /*
      ============================================
      Keyword（キーワード検索）の派生先実装例
      ============================================

      ■ 前提
        Nx のテーブルは「colname テーブル」で
        ・カラム名（ColumnName）
        ・データ名（DataName）
        ・印刷名
        ・単位名
        ・フォーマット
        ・Digit
        などを持つ。

        このうち「DataName（＝UI影のラベル）」を
        キーワード検索の対象にする設計。

        つまり Keyword は namecolname を LIKE する。

      ■ なぜ Base に書かない？
        ・検索対象列はテーブルごとに違う
        ・Base が列名を知ると抽象核が汚れる
        ・Nx の世界線モデルでは Keyword は「テーブル固有世界線」

        よって Keyword は派生先で実装する。

      ============================================
      BuildSql の派生先実装（Keyword 対応版）
      ============================================

      protected override string BuildSql(RecordQuery q)
      {
          var sb = new StringBuilder();
          sb.Append($"SELECT * FROM \"{_tblName}\" WHERE 1=1 ");

          // AND 条件（Base の簡易版をそのまま使う）
          foreach (var c in q.And)
          {
              sb.Append($" AND {c.Column} {c.Operator} @{c.Column} ");
          }

          // OR 条件（簡易版）
          if (q.Or.Count > 0)
          {
              sb.Append(" AND (");
              sb.Append(string.Join(" OR ",
                  q.Or.Select(c => $"{c.Column} {c.Operator} @{c.Column}")));
              sb.Append(") ");
          }

          // ============================================
          // Keyword 条件（namecolname を LIKE）
          // ============================================
          if (!string.IsNullOrEmpty(q.Keyword))
          {
              sb.Append(" AND (");

              // ここで「検索対象列」を決める
              // namecolname（UI影のラベル）を対象にする
              // ※ 実際には colname テーブルから対象列を取得しても良い
              var keywordCols = new[] {
                  "NameColName",   // UI影のラベル
                  "PrintName1",    // 印刷名1
                  "PrintName2",    // 印刷名2
                  "PrintName3"     // 印刷名3
              };

              sb.Append(string.Join(" OR ",
                  keywordCols.Select(col => $"{col} LIKE @Keyword")));

              sb.Append(") ");
          }

          // ソート
          if (!string.IsNullOrEmpty(q.OrderBy))
          {
              sb.Append($" ORDER BY {q.OrderBy} {(q.OrderDesc ? "DESC" : "ASC")} ");
          }

          // ページング
          if (q.PageSize > 0)
          {
              sb.Append($" LIMIT {q.PageSize} OFFSET {q.PageIndex * q.PageSize} ");
          }

          return sb.ToString();
      }

      ============================================
      使い方
      ============================================

      var q = new RecordQuery();
      q.Keyword = "りんご";  // namecolname に LIKE '%りんご%' をかける

      await mgr.Initialize(q);

      → 具象の BuildSql が呼ばれ、Keyword 条件が適用される。

  */
    public class RecordQuery {
        // ================================
        // ① AND 条件
        // ================================
        public List<Condition> And { get; set; } = new();

        // ================================
        // ② OR 条件
        // ================================
        public List<Condition> Or { get; set; } = new();

        // ================================
        // ③ キーワード検索（任意）
        // ================================
        public string Keyword { get; set; } = "";

        // ================================
        // ④ ページング
        // ================================
        public int PageIndex { get; set; } = 0;
        public int PageSize { get; set; } = 0;
        public int TotalCount { get; set; } = 0;

        // ================================
        // ⑤ ソート
        // ================================
        public string OrderBy { get; set; } = "";
        public bool OrderDesc { get; set; } = false;

        // ================================
        // ⑥ ★ TargetIds（IDリスト検索）
        // Delete / Bulk Update / Bulk Lock 用
        // ================================
        public List<object> TargetIds { get; set; } = new();
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

        protected string RootName { get; set; }//rootNodeの表示名。空の場合はInitializeメソッドでRootNodeオブジェクトは作成されない

        string IdColName { get; }
        string NameColName { get; }
        public NxDataType DataType { get; set; }

        public Guid TenantCode { get; set; }

        public Guid CurrentUserID { get; set; }


        public IDbConnection DBcon { get; set; }


        //DataObjのList：派生したDataObjも保持できる様Objectにダウンキャストする。
        public List<IBaseDataObj<TKey>> _dataList = new List<IBaseDataObj<TKey>>();

        /// <summary>
        /// DataList を取得します。
        /// </summary>
        public IEnumerable<IBaseDataObj<TKey>> DataList
                                => _dataList.Cast<IBaseDataObj<TKey>>();


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

        // --------------------------------------------------
        // idで指定されたDataObjを返す
        // --------------------------------------------------

        public virtual IBaseDataObj<TKey>? Get(TKey id) {
            return _dataList
                .FirstOrDefault(x => EqualityComparer<TKey>.Default.Equals(x.DataID, id));
        }


        public BaseDataObjMgr(IDbConnection db , Guid tenantCode , Guid currUserID) {
            DBcon = db;
            TenantCode = tenantCode;
            CurrentUserID = currUserID;

            //テーブル名などの基本情報は派生先のコンストラクタでハードコードする事。
            //Initialize()はインスタンス生成元が呼び出す事(この中で呼んではいけない)
        }
        protected abstract TKey GenerateDataID();


        //RootノードにあたるDataObjを生成
        protected virtual T CreateRoot() {
            var root = new T();

            root.DBcon = DBcon;
            root.SelfObjMgr = this;

            // ルートは物理レコードを持たないので空スキーマでよい
            root.Setproperties(GetEmptySchema());

            root.TenantCode = TenantCode;
            root.CurrUsrID = CurrentUserID;

            // ★ GenerateDataID を使わず、型に応じて 0 / Guid.Empty をセット
            if (typeof(TKey) == typeof(int))
                root.DataID = (TKey)(object)0;
            else if (typeof(TKey) == typeof(Guid))
                root.DataID = (TKey)(object)Guid.Empty;
            else
                throw new NotSupportedException("Unsupported TKey type for CreateRoot.");

            //rootとしての属性をセットする
            root.SetAsRoot(RootName);

            // ルートは親を持たない
            root.ParentDataObj = null;

            _dataList.Add(root);
            return root;
        }

        // 新規レコードを生成する。
        // UI側で新規作成ボタンを押したときに呼び出す。
        public virtual IBaseDataObj<TKey> CreateNewDataObj(TKey? parentID) {
            var dataObj = new T();
            dataObj.DBcon = DBcon;
            dataObj.SelfObjMgr = this;
            dataObj.Setproperties(GetEmptySchema());

            // UI から渡された親IDだけセット
            dataObj.ParentID = parentID;
            dataObj.TenantCode = TenantCode;
            dataObj.CurrUsrID = CurrentUserID;
            dataObj.DataID = GenerateDataID();
            dataObj.DataType = this.DataType;
            // ここではまだ _dataList に入れない
            // DataID は仮で OK（保存時に確定でもいい）
            return dataObj;
        }


        //createnewDataObj()で生成したオブジェクトを_dataListに追加
        //子リストに追加する
        public void InsertNewDataItem(IBaseDataObj<TKey> obj) {
            // _dataList に追加
            _dataList.Add(obj);

            // 親をセット
            SetParent(obj);

        }


        // 新規レコードの場合に必要になる空のレコードを生成する。
        protected Dictionary<string, object?> GetEmptySchema()
        {
            // NxTypeMap が未設定なら例外（世界線の正本が無い）
            if (NxTypeMapper.Current == null)
                throw new Exception("NxTypeMap が初期化されていません。");
        
            // 型マップに基づいて初期値辞書を生成
            return NxTypeMapper.Current.CreateEmptyRow(_tblName);
        }


        //データベースからデータを取得する。(クライアント、サーバー共用）
        //ラッパークラスからも使用されるのでコンストラクタで呼び出してはいけない。
        public virtual async Task<IEnumerable<dynamic>> _LoadRecordsAsync(RecordQuery? query) {
            string sql = _BuildSql(query);

            // パラメータをまとめる
            var param = new Dictionary<string, object>();
            param["TenantCode"] = TenantCode;

            if (query != null) {
                foreach (var c in query.And)
                    param[c.Column] = c.Value;

                foreach (var c in query.Or)
                    param[c.Column] = c.Value;

                if (!string.IsNullOrEmpty(query.Keyword))
                    param["Keyword"] = "%" + query.Keyword + "%";
            }

            return await DBcon.QueryAsync<dynamic>(sql, param);
        }




        //RecordQueryをもとにSQLを組み立てる
        public virtual string _BuildSql(RecordQuery? q) {
            var sql = new List<string>();
            sql.Add($"SELECT * FROM \"{_tblName}\"");
            sql.Add("WHERE 1=1");

            // 世界線境界（必ず入れる）
            sql.Add("AND tenant_code = @TenantCode");

            // null → 全件取得（tenant_code だけで絞る）
            if (q == null)
                return string.Join(" ", sql);

            if (q.TargetIds.Count > 0) {
                var idParams = q.TargetIds
                    .Select((id, idx) => $"@id{idx}")
                    .ToList();

                where.Add($"{_idColName} IN ({string.Join(", ", idParams)})");
            }



            var where = new List<string>();

            // AND 条件
            foreach (var c in q.And)
                where.Add($"{c.Column} {c.Operator} @{c.Column}");

            // OR 条件
            if (q.Or.Count > 0) {
                var orList = q.Or.Select(c => $"{c.Column} {c.Operator} @{c.Column}");
                where.Add("(" + string.Join(" OR ", orList) + ")");
            }

            // AND 条件を追加
            foreach (var w in where)
                sql.Add("AND " + w);

            // ORDER
            if (!string.IsNullOrEmpty(q.OrderBy))
                sql.Add($"ORDER BY {q.OrderBy} {(q.OrderDesc ? "DESC" : "ASC")}");

            // ページング
            if (q.PageSize > 0)
                sql.Add($"LIMIT {q.PageSize} OFFSET {q.PageIndex * q.PageSize}");

            return string.Join(" ", sql);
        }





        //データベースからデータを取得する。(クライアント、サーバー共用）
        //コンストラクタで呼び出してはいけない。
        public virtual async Task Initialize(RecordQuery? query) {
            if (!string.IsNullOrEmpty(RootName))
                CreateRoot();

            var records = await _LoadRecordsAsync(query);

            foreach (var record in records) {
                T obj = new T();
                obj.DBcon = DBcon;
                obj.SelfObjMgr = this;
                obj.Setproperties((IDictionary<string, object>)record);
                obj.TenantCode = TenantCode;
                obj.CurrUsrID = CurrentUserID;

                _dataList.Add(obj);
            }

            foreach (var obj in _dataList)
                SetParent((T)obj);
        }


        // 親子関係を設定する（新規作成の場合はUIがここに新しいDataObjを渡す。
        public virtual void SetParent(IBaseDataObj<TKey> obj) {
            // 親ID列が存在しない世界線
            if (string.IsNullOrEmpty(obj.ParentIDColName)) {
                obj.ParentDataObj = null;
                return;
            }

            var pid = obj.ParentID;

            // 親IDが未設定（null）または属性がroot → 親オブジェクトをNULLに
            if (pid == null || obj.DataType == NxDataType.root) {
                obj.ParentDataObj = null;
                return;
            }

            // 親を検索
            var parent = _dataList.FirstOrDefault(x =>
                EqualityComparer<TKey>.Default.Equals(x.DataID, pid));

            if (parent != null) {
                obj.ParentDataObj = parent;
            } else {
                // ★ 親が存在しない世界線 → 最小値をセット
                obj.ParentDataObj = null; // 親オブジェクトは null
                obj.ParentID = GetMinValue<TKey>();
            }
        }

        //TKeyの最小値を返すメソッド。int.MinValueやGuid.Emptyなど、TKeyの型に応じて適切な最小値を返す。
        public static TKey GetMinValue<TKey>() {
            if (typeof(TKey) == typeof(int))
                return (TKey)(object)int.MinValue;

            if (typeof(TKey) == typeof(Guid))
                return (TKey)(object)Guid.Empty; // UUIDv7 の最小値

            throw new NotSupportedException("Unsupported TKey type.");
        }



        // 指定したID群を順次削除し、削除に失敗したIDを返す。
        // 返り値のリストが空なら全件成功。
        // UIはこの返り値を観測して成功／部分失敗を判断する。
        public virtual async Task<List<TKey>> DeleteData(IEnumerable<TKey> dataIDs, bool softDelete = false) {
            var failedLst = new List<TKey>();

            foreach (var id in dataIDs) {
                try {
                    if (!await _DeleteDataObj(id, softDelete))
                        failedLst.Add(id);   // 正常系の失敗
                } catch {
                    failedLst.Add(id);       // 異常系の破綻も「失敗」として扱う
                }
            }

            return failedLst;
        }



        //指定したデータを削除し、_dataListからオブジェクトを削除
        public virtual async Task<bool> _DeleteDataObj(TKey dataID, bool softDelete = false) {
            var target = (BaseDataObj<TKey>)_dataList
                .FirstOrDefault(x => ((BaseDataObj<TKey>)x).DataID.Equals(dataID));

            if (target == null)
                return false;
            

            var transaction = DBcon.BeginTransaction();

            try {
                bool result = softDelete
                    ? await target.SoftDeleteQueryExec(transaction)
                    : await target.DeleteQueryExec(transaction);

                if (!result) {
                    transaction.Rollback();
                    return false;   // 正常系の失敗
                }

                transaction.Commit();

                if (!softDelete)
                    _dataList.Remove(target);

                return true;
            } catch {
                transaction.Rollback();
                throw;  // 異常系の破綻
            }
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

            var tran = DBcon.BeginTransaction();
            try {

                foreach (var group in groupedRecords) {
                    var id = (TKey)Convert.ChangeType(group.Key, typeof(TKey));

                    // 3. IDに対応するオブジェクトを探す
                    var obj = DataList.FirstOrDefault(d => d.DataID.Equals(id));

                    if (obj == null) {
                        // 存在しなければ新規作成
                        obj = new T();
                        obj.DataID = id; // IDをセット
                        _dataList.Add(obj);
                    }

                    // 4. そのオブジェクト専用のJSONを作成して渡す
                    // グループ化したレコードを再度JSON文字列にして、個別のJsonToTblへ流し込む
                    string individualJson = JsonSerializer.Serialize(group.ToList());
                    await obj.JsonToTbl(individualJson, tran);
                }

                tran.Commit();
            } catch (Exception ex) {
                // エラーが発生した場合はトランザクションをロールバック
                tran.Rollback();
                throw new Exception("Failed to distribute JSON data.", ex);
            }
        }
    }
}
