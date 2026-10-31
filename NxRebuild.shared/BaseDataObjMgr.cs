using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.shared;
using System.ComponentModel.DataAnnotations;
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
        Dictionary<string, object?> PrimaryPKDict { get; set; }

        string[] DetailTableList { get; }
        string InfoTbl { get;  }

        string W_TblName { get;  }
        string[] W_DetailTblList { get; }

        IBaseDataObj<TKey>? Get(TKey id);

        IBaseDataObj<TKey> CreateNewDataObj(TKey? parentID);
        void InsertNewDataItem(IBaseDataObj<TKey> obj);

        Task<NxOperationResult<TKey>> DeleteDataAsync(IEnumerable<TKey> dataIDs, bool softDelete = false, IDbTransaction tran = null);
        void SetParent(IBaseDataObj<TKey> obj);
        Task DistributeJsonDataAsync(string json);
        Task InitializeAsync(RecordQuery? query);
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
        string[] DetailTableList { get; }
        string InfoTbl { get; }

        string W_TblName { get; }
        string[] W_DetailTblList { get; }

        Task<NxOperationResult<TKey>> DeleteDataAsync(IEnumerable<TKey> dataIDs, bool softDelete = false, IDbTransaction tran = null);

        IBaseDataObj<TKey>? Get(TKey id);


        Task DistributeJsonDataAsync(string json);
        Task InitializeAsync(RecordQuery? query);
        string LoadMultipleDataAsJson(List<TKey> idList);
        void _RemoveFromList(IBaseDataObj<TKey> obj);
        IBaseDataObj<TKey> CreateNewDataObj(TKey? parentID);
    }

    //setter → 入力許容getter → 利用時に安全性保証
    //Column → null / 空 / スペース禁止
    //Operator → Allow-list
    //Value → パラメータ化で安全
    //Condition DTOは最低限のバリデは自分でやる
    //DTO が最低限の自己防衛を持つことで
    //責務の境界が明確になり、後続レイヤーの負荷が減ります。
    //“責務分離” を成立させるための最低ラインです。
    public class Condition
    {
        private static readonly HashSet<string> AllowedOperators = new()
        {
            "=",
            "<>",
            ">",
            "<",
            ">=",
            "<=",
            "LIKE"
        };

        private string _column;
        public string Column {
            get {
                if (string.IsNullOrWhiteSpace(_column))
                    throw new ArgumentException("Column cannot be null or empty.");

                if (_column.Contains(' '))
                    throw new ArgumentException("Column cannot contain spaces.");

                return _column;
            }
            set {
                _column = value;
            }
        }


        public object Value { get; set; }

        private string _operator = "=";
        public string Operator
        {
            get
            {
                if (!AllowedOperators.Contains(_operator))
                    throw new ArgumentException($"Invalid operator: {_operator}");

                return _operator;
            }
            set
            {
                _operator = value;
            }
        }
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
          sb.Append($"SELECT * FROM \"{_primaryTableName}\" WHERE 1=1 ");

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
    // インスタンス生成後、Mgr が保持している PK（IdCol 以外の親 PK/FK）がある場合は、
    // 生成元）が PrimaryPKDict を使って DataObj に注入する。
    public abstract class BaseDataObjMgr<T, TKey> : IBaseDataObjMgr<T, TKey> , IsrvBaseDataObjMgr<T, TKey> where T : BaseDataObj<TKey>, new() {

        protected string _piymaryTableName;
        protected string _idColName;
        protected string[] _primaryTblPKList;
        protected string[] _detailTableList;
        protected string _infoTbl;
        protected string _w_tblName;
        protected string[] _w_detailTblList;
        public string TblName { get => _piymaryTableName; }//データ名等基本データが格納されるテーブル名
        public string[] DetailTableList { get => _detailTableList; }//明細データが格納されるサブテーブル名
        public string InfoTbl { get => _infoTbl; }//TblNameに加え栄養素などの集計結果が入っているテーブル(プロパティをビューから取得したいときはこれを使う

        public string W_TblName { get => _w_tblName; }
        public string[] W_DetailTblList { get => _w_detailTblList; }

        protected string RootName { get; set; }//rootNodeの表示名。空の場合はInitializeメソッドでRootNodeオブジェクトは作成されない

        string IdColName { get; }
        string NameColName { get; }
        public NxDataType DataType { get; set; }

        public Guid TenantCode { get; set; }

        public Guid CurrentUserID { get; set; }


        public IDbConnection DBcon { get; set; }


        //DataObjのList：派生したDataObjも保持できる様Objectにダウンキャストする。
        public List<IBaseDataObj<TKey>> _dataList = new List<IBaseDataObj<TKey>>();

        // ObjMgr が保持する PK 固定値
        public Dictionary<string, object?> _mgrPrimaryPKDict = new();

        /// <summary>
        /// ObjMgr 用の PK 辞書（IdCol を除外）
        /// Getter：現在の PK 辞書を返す
        /// Setter：PK 辞書を丸ごと設定する（rawData は覗かない）
        /// </summary>

        public Dictionary<string, object?> PrimaryPKDict {
            get {
                var dict = new Dictionary<string, object?>();

                foreach (var pk in _primaryTblPKList) {
                    // ★ IdColName は除外
                    if (pk == _idColName)
                        continue;

                    // ★ ObjMgr が保持する PK 値を使う
                    if (_mgrPrimaryPKDict.TryGetValue(pk, out var v))
                        dict[pk] = v;
                    else
                        dict[pk] = null;
                }

                return dict;
            }

            set {
                if (value == null)
                    return;

                foreach (var pk in _primaryTblPKList) {
                    if (pk == _idColName)
                        continue;

                    if (!value.ContainsKey(pk))
                        throw new Exception($"PrimaryPKDictMgrWithoutId SET ERROR: key '{pk}' is missing.");

                    _mgrPrimaryPKDict[pk] = value[pk];
                }
            }
        }


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
                if (latestUpdate == DateTime.MinValue &&
                    latestLocked == DateTime.MinValue) {
                    return DateTime.MinValue;
                }

                if (latestUpdate == DateTime.MinValue) {
                    return latestLocked;
                }

                if (latestLocked == DateTime.MinValue) {
                    return latestUpdate;
                }

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
            
            var meta = NxObjsMetaReader.Read(typeof(T));
            if (meta == null)
                throw new Exception($"NxObjsMetaDataAttribute が {typeof(T).Name} に設定されていません。");

            _piymaryTableName = meta.PrimaryTable;
            //_nameColName = meta.NameCol;
            _idColName = meta.IdCol;
            _primaryTblPKList = meta.PriTblPKColList;
            //_parentIDColName = meta.ParentIDCol;
            _detailTableList = meta.DetailTableList;
            _infoTbl = meta.InfoTable;
            _w_tblName = meta.WorkPriTable;
            _w_detailTblList = meta.WorkDtlTblList;
            //_datatype = meta.DataType;

            //Initialize()はインスタンス生成元が呼び出す事(ラッパークラス生成に関連するためこの中で呼んではいけない)
        }
        protected abstract TKey GenerateDataID();

        public NxLocationKind LocationKind {
            get {
                var conn = DBcon.ConnectionString;
                // WASM / MAUI / WPF の in-memory をすべて拾う
                if (string.IsNullOrWhiteSpace(conn))
                    return NxLocationKind.Client;

                // SQLite の in-memory 全パターン
                if (conn.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
                    return NxLocationKind.Client;

                if (conn.Contains("mode=memory", StringComparison.OrdinalIgnoreCase))
                    return NxLocationKind.Client;

                if (conn.Contains("cache=shared", StringComparison.OrdinalIgnoreCase))
                    return NxLocationKind.Client;

                // それ以外はサーバー
                return NxLocationKind.Server;
            }
        }


        //RootノードにあたるDataObjを生成
        protected virtual T CreateRoot() {
            var root = new T();

            root.DBcon = DBcon;
            root.SelfObjMgr = this;

            // ルートは物理レコードを持たないので空スキーマでよい
            root._SetProperties(GetEmptySchema());

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
            dataObj._SetProperties(GetEmptySchema());

            // ★ Mgr が保持している PK を注入（IdCol は除外）
            foreach (var kv in PrimaryPKDict) {
                // null の場合はまだ設定されていないのでスキップ
                if (kv.Value != null)
                    dataObj.PrimaryPKDict[kv.Key] = kv.Value;
            }

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
            return NxTypeMapper.Current.CreateEmptyRow(_piymaryTableName);
        }


        //データベースからデータを取得する。(クライアント、サーバー共用）
        //ラッパークラスからも使用されるのでコンストラクタで呼び出してはいけない。
        public virtual async Task<IEnumerable<dynamic>> _LoadRecordsAsync(RecordQuery? query) {
            string sql = _BuildSql(query);

            // パラメータをまとめる
            var param = new Dictionary<string, object>();
            param["TenantCode"] = TenantCode;

            foreach (var kv in PrimaryPKDict) {
                if (kv.Value != null)
                    param[kv.Key] = kv.Value;
            }

            if (query != null) {
                foreach (var c in query.And)
                    param[c.Column] = c.Value;

                foreach (var c in query.Or)
                    param[c.Column] = c.Value;

                if (!string.IsNullOrEmpty(query.Keyword))
                    param["Keyword"] = "%" + query.Keyword + "%";
                
                // TargetIds（id0, id1, id2, ...）
                if (query.TargetIds.Count > 0) {
                    for (int i = 0; i < query.TargetIds.Count; i++)
                        param[$"id{i}"] = query.TargetIds[i];
                }
            }

            return await DBcon.QueryAsync<dynamic>(sql, param);
        }


        //RecordQueryをもとにSQLを組み立てる
        // RecordQueryをもとにSQLを組み立てる
        // RecordQueryをもとにSQLを組み立てる
        public virtual string _BuildSql(RecordQuery? q) {
            var sql = new List<string>();

            // SELECT
            sql.Add($"SELECT * FROM \"{_piymaryTableName}\"");
            sql.Add("WHERE 1=1");

            // 世界線境界（必ず入れる）
            sql.Add("AND \"tenant_code\" = @TenantCode");

            // null → 全件取得
            if (q == null)
                return string.Join(" ", sql);

            // TargetIds
            if (q.TargetIds.Count > 0) {
                var idParams = q.TargetIds
                    .Select((id, idx) => $"@id{idx}")
                    .ToList();

                sql.Add($"AND \"{_idColName}\" IN ({string.Join(", ", idParams)})");
            }

            // 条件（Condition）を安全にSQL文字列に変換するヘルパー関数
            string FormatCondition(Condition c) {
                var op = c.Operator?.Trim().ToUpper() ?? "=";

                // "IS NULL" や "IS NOT NULL" が指定された場合、または Value が null の場合
                if (op == "IS NULL" || op == "IS NOT NULL") {
                    return $"\"{c.Column}\" {op}";
                }
                if ((op == "IS" || op == "=") && c.Value == null) {
                    return $"\"{c.Column}\" IS NULL";
                }
                if ((op == "IS NOT" || op == "!=" || op == "<>") && c.Value == null) {
                    return $"\"{c.Column}\" IS NOT NULL";
                }

                // 通常の比較演算子（=, >, >=, < など）
                return $"\"{c.Column}\" {c.Operator} @{c.Column}";
            }

            // AND 条件の追加
            foreach (var c in q.And) {
                sql.Add("AND " + FormatCondition(c));
            }

            // OR 条件の追加（複数のOR条件を `OR` で結合し、括弧で括る）
            if (q.Or.Count > 0) {
                var orList = q.Or.Select(c => FormatCondition(c)).ToList();
                sql.Add($"AND ({string.Join(" OR ", orList)})");
            }

            // ORDER
            if (!string.IsNullOrEmpty(q.OrderBy)) {
                sql.Add($"ORDER BY \"{q.OrderBy}\" {(q.OrderDesc ? "DESC" : "ASC")}");
            }

            // ページング
            if (q.PageSize > 0) {
                sql.Add($"LIMIT {q.PageSize} OFFSET {q.PageIndex * q.PageSize}");
            }

            return string.Join(" ", sql);
        }


        //データベースからデータを取得する。(クライアント、サーバー共用）
        //コンストラクタで呼び出してはいけない。
        public virtual async Task InitializeAsync(RecordQuery? query) {

            if (LocationKind == NxLocationKind.Client) {
                if (!string.IsNullOrEmpty(RootName))
                    CreateRoot();
            }

            var records = await _LoadRecordsAsync(query);

            foreach (var record in records) {
                T obj = new T();
                obj.DBcon = DBcon;
                obj.SelfObjMgr = this;
                obj._SetProperties((IDictionary<string, object>)record);
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
        public virtual async Task<NxOperationResult<TKey>> DeleteDataAsync(
            IEnumerable<TKey> dataIDs,
            bool softDelete = false,
            IDbTransaction tran = null) {
            var opResult = new NxOperationResult<TKey>();

            foreach (var id in dataIDs) {
                try {
                    var obj = Get(id);

                    // Validation
                    var validResult = obj._Validate(OperationType.Delete);

                    if (!validResult.Success) {
                        opResult.FailedIds.Add(id);

                        opResult.Message +=
                            $"[{obj.DataName}] {validResult.Message}{Environment.NewLine}";

                        continue;
                    }

                    // Delete
                    if (!await _DeleteDataObjFromListAsync(id, softDelete, tran)) {
                        opResult.FailedIds.Add(id);

                        opResult.Message +=
                            $"[{obj.DataName}] 削除に失敗しました{Environment.NewLine}";
                    }
                } catch (Exception ex) {
                    opResult.FailedIds.Add(id);

                    opResult.Message +=
                        $"[ID:{id}] 例外: {ex.Message}{Environment.NewLine}";
                }
            }

            if (opResult.FailedIds.Count == 0) 
                opResult.Message = "Delete completed";
             

            return opResult;
        }



        //指定したデータを削除し、_dataListからオブジェクトを削除
        public virtual async Task<bool> _DeleteDataObjFromListAsync(TKey dataID, bool softDelete = false ,IDbTransaction tran = null) {
            var target = (ISharedDataObj<TKey>)Get(dataID);

            if (target == null)
                return false;

            var transaction = tran;

            if (tran == null) 
                transaction = DBcon.BeginTransaction();

            try {


                bool result;
                var baseObj = target;
                result = await baseObj._DeleteQueryExecAsync(transaction, softDelete);
                
                if (!result) {
                    transaction.Rollback();
                    return false;   // 正常系の失敗
                }

                transaction.Commit();

                _RemoveFromList((IBaseDataObj<TKey>)target);

                return true;
            } catch {
                transaction.Rollback();
                throw;  // 異常系の破綻
            }
        }


        public virtual void _RemoveFromList(IBaseDataObj<TKey> obj) {
            _dataList.Remove(obj);
        }

        //idListに含まれるIDのDataObjを順次Json化して返す。
        //API,Cliant双方での使用を想定している。
        public string LoadMultipleDataAsJson(List<TKey> idList) {
            var jsonResults = new List<string>();

            foreach (var id in idList) {
                // 1. DataList から該当するオブジェクトを特定
                // 既存の DataList（Object型）を T にキャストして検索
                var dataObj = Get(id);

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
        public async Task DistributeJsonDataAsync(string json) {
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
                    var obj = Get(id);

                    if (obj == null) {
                        // 存在しなければ新規作成
                        obj = new T();
                        obj.DataID = id; // IDをセット
                        _dataList.Add(obj);
                    }

                    // 4. そのオブジェクト専用のJSONを作成して渡す
                    // グループ化したレコードを再度JSON文字列にして、個別のJsonToTblへ流し込む
                    string individualJson = JsonSerializer.Serialize(group.ToList());
                    await obj.JsonToTblAsync(individualJson, tran);
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
