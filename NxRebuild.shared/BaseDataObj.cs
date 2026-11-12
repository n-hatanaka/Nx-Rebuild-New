using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Transactions;
using System.Xml.Linq;

namespace NxRebuild.shared {
    public enum NxLocationKind
    {
        Server,
        Client,
        Both
    }
    //　サーバー同期用のJSON構造体
    public enum OperationType {
        Save,
        Delete,
        Rename,
        Sync,
        Import,
        LocalEdit
    }



    public class TableJson {
        public string Table { get; set; }
        public List<Dictionary<string, object>> Rows { get; set; }
    }


    [Flags]
    public enum NxDataType {
        root = 0,
        Folder = 1,

        //ここから下は使われるシステムに合わせて作成する。必要なければ削除してOK
        Zairyou = 2,
        Ryouri = 4,
        Meal = 8,
        Kondate = 16,
        Calendar = 32,
        Person = 64,
        InstMeals = 128, //給食'Institutional meals'
        RyouriRow = 256//料理内材料

    }

    //BaseDataObjのインターフェース
    //Syncラッパーとの同型性とUI側へ不要な実装の隠蔽のためにインターフェースを作成する
    //やむおえずインターフェースメンバーとしたメソッドはアンダーバーを付けてあるのでaccessしない事
    public interface IBaseDataObj<TKey> {
        Guid CurrUsrID { get; set; }
        TKey DataID { get; set; }

        //複合PKが設定されている場合はこのプロパティでセットもしくは取得する
        Dictionary<string, object?> PrimaryPKDict { get; set; }

        string DataName { get; }
        NxDataType DataType { get; set; }
        IDbConnection DBcon { get; set; }
        string ParentIDColName { get; }
        TKey ParentID { get; set; }
        IBaseDataObj<TKey>? ParentDataObj { get; set; }
        string IdColName { get; }
        string InfoTbl { get; }
        DateTime LockedAt { get; }
        Guid LockerID { get; }
        string NameColName { get; }
        string[] DetailTableList { get; }
        object SelfObjMgr { get; set; }
        string TblName { get; }
        Guid TenantCode { get; set; }
        DateTime Update_at { get; }
        string W_TblName { get; }
        string[] W_DetailTblList { get; }
        NxLocationKind LocationKind { get; }
        List<(string TableName, List<Dictionary<string, object?>> Rows)> SubTables { get; set; }
        void CreateWorkingMemory(Dictionary<string, object?> workingRaw);
        void CreateWorkingDetailTables(List<(string , List<Dictionary<string, object?>>)> workingSubList);

        string TblToJson();

        string TblToJson(TKey id, IDbTransaction tran);

        Task<bool> JsonToTblAsync(string json, IDbTransaction tran);

        // ★ ここから NxOperationResult に統一
        Task<NxOperationResult<TKey>> DataOpenAsync();
        Task<NxOperationResult<TKey>> DataCloseAsync();
        Task<NxOperationResult<TKey>> ReNameAsync(string newName);
        Task<NxOperationResult<TKey>> SaveAsync(
                Dictionary<string, object?> workingRaw,
                List<(string, List<Dictionary<string, object?>>)>? subTables = null);

        Task<bool> SaveWorkingAsync(
                Dictionary<string, object?> workingRaw,
                List<(string TableName, List<Dictionary<string, object?>> Rows)>? subTables,
                IDbTransaction tran);

        void _SetProperties(IDictionary<string, object?> record);

        NxOperationResult<TKey> _Validate(OperationType op, string? arg = null);
    }


    public interface ISharedDataObj<TKey> {
        Task<bool> _DeleteQueryExecAsync(IDbTransaction transaction, bool softDelete);
    }


    // -----------------------------------------------------------------
    // BaseDataObj　クライアントとサーバーで直接DBにaccessするオブジェクト
    // 同期機能の拡張用にSyncラッパークラスを使用するため本来protectedと
    // すべきメソッドもパブリックになっているが、その様なメソッドはアンダーバー
    // が名前の先頭にあるためラッパークラス以外からはアクセスしない事
    // -----------------------------------------------------------------
    public abstract class BaseDataObj<TKey> : IBaseDataObj<TKey>, ISharedDataObj<TKey> {
        // -------------------------------------------------------------
        // コンストラクタ
        // ここで固定のテーブル名やNameカラム名などのプロパティを設定する
        // -------------------------------------------------------------
        public BaseDataObj() {
            var meta = NxObjsMetaReader.Read(this.GetType());

            _primaryTableName = meta.PrimaryTable;

            _primaryTblPKList = meta.PriTblPKColList;

            _detailTableList = meta.DetailTableList;
            //DetailテーブルのPKカラム名リストをDictionaryに変換
            _detailTblPKList = new Dictionary<string, string[]>();

            // ★ JSON → Dictionary<string, string[]>
            _detailTblPKList = JsonSerializer.Deserialize<Dictionary<string, string[]>>(meta.DetailTblPKJson)
                              ?? new Dictionary<string, string[]>();

            _nameColName = meta.NameCol;
            _DataTypeColName = meta.DataTypeCol;
            _updateAtCol = meta.UpdateAtCol;
            _lockedAtCol = meta.LockedAtCol;
            _lockedByCol = meta.LockedByCol;
            _lockTimeOutLimit = TimeSpan.FromSeconds(meta.LockTimeOutSec);
            _tenantCodeCol = meta.TenantCodeCol;
            _idColName = meta.IdCol;
            _parentIDColName = meta.ParentIDCol;
            _infoTbl = meta.InfoTable;
            _w_tblName = meta.WorkPriTable;
            _w_detailTblList = meta.WorkDtlTblList;
            _datatype = meta.DataType;
        }


        // レコード内容をJSON（辞書）として保持するメンバ
        //　直接アクセスせずSetProperties()を通してセットすること
        public Dictionary<string, object> _rawData = new();

        protected string _nameColName; //テーブルのデータ名カラムのカラム名
        protected string _updateAtCol;
        protected string _lockedByCol;
        protected string _lockedAtCol;
        protected TimeSpan _lockTimeOutLimit;// ロックタイムアウト時間 
        protected string _tenantCodeCol;
        protected string _idColName;//テーブルのIDカラムのカラム名
        protected string _DataTypeColName;//テーブルのデータ種別カラムのカラム名
        public string _parentIDColName;//テーブルの親IDカラムのカラム名
        public string _primaryTableName; //データ名等基本データが格納されるテーブル名

        public string[] _primaryTblPKList;
        public string[] _detailTableList;//材料など詳細データが格納されるテーブル名
        public Dictionary<string, string[]> _detailTblPKList;//詳細テーブルのPKカラム名リスト>
        protected string _infoTbl;//_tblNameに加え栄養素などの集計結果が入っているテーブル

        //ワークテーブル名、ワークテーブルを持たない場合は
        //_primaryTableName,_detailTableList
        //と同じ値を設定すること
        protected string _w_tblName;
        protected string[] _w_detailTblList;

        public IBaseDataObj<TKey>? ParentDataObj { get; set; }
        public string NameColName => _nameColName;
        public string IdColName => _idColName;
        public string UpdateAtCol => _updateAtCol;
        public string LockedAtCol => _lockedAtCol;
        public string LockedByCol => _lockedByCol;
        public string TenantCodeCol => _tenantCodeCol;
        public string ParentIDColName => _parentIDColName;
        public string TblName => _primaryTableName;
        public string[] DetailTableList => _detailTableList;
        public string InfoTbl => _infoTbl;
        public string W_TblName => _w_tblName;
        public string[] W_DetailTblList => _w_detailTblList;

        protected NxDataType _datatype;
        protected DateTime _update_at;
        protected Guid _locker_ID;
        protected DateTime _locked_at;

        public object SelfObjMgr { get; set; }
        public IDbConnection DBcon { get; set; }

      //
        public NxLocationKind LocationKind
        {
            get
            {
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

        public virtual Guid TenantCode {
            get => _rawData.TryGetValue(TenantCodeCol, out var v)
                ? (v is Guid g ? g : Guid.Parse(v.ToString()!))
                : Guid.Empty;

            set => _SetProperties(new Dictionary<string, object> { [TenantCodeCol] = value });   
        }

        

        public TKey DataID {
            get => (TKey)_rawData[IdColName];
            set => _SetProperties(new Dictionary<string, object> { [IdColName] = value });
        }

        public string DataName {
            get => (string)_rawData[NameColName];
        }

        public TKey ParentID {
            get {
                // 親ID列が存在しない
                if (string.IsNullOrEmpty(ParentIDColName))
                    return default(TKey); // intなら0、stringならnull

                // 親ID列がある
                if (_rawData.TryGetValue(ParentIDColName, out var v) && v != null)
                    return (TKey)v;

                return default(TKey);
            }

            set {
                // 親ID列が存在しない線 → 無効化
                if (string.IsNullOrEmpty(ParentIDColName))
                    return;

                // 親ID列がある → 通常処理
                _SetProperties(new Dictionary<string, object> { [ParentIDColName] = value });
            }
        }


        // ★ DataType は NxDataType 列がある場合は _rawData から読み書きする
        public virtual NxDataType DataType {
            get {
                // _DataTypeColName が空の時はフィールドから読み込む
                if (string.IsNullOrEmpty(_DataTypeColName)) {
                    return _datatype;
                }

                // _DataTypeColName があるときは _rawData から読み込む
                if (_rawData.TryGetValue(_DataTypeColName, out var v) && v != null && v is not DBNull) {
                    return v switch {
                        NxDataType dt => dt,
                        int i => (NxDataType)i,
                        long l => (NxDataType)l,
                        _ => Enum.TryParse(v.ToString(), out NxDataType parsed) ? parsed : _datatype
                    };
                }

                return _datatype;
            }
            set {
                // _DataTypeColName が空の時はフィールドへ書き込む
                if (string.IsNullOrEmpty(_DataTypeColName)) {
                    _datatype = value;
                } else {
                    // あるときは _rawData（正本）に書き込む
                    _SetProperties(new Dictionary<string, object?> { [_DataTypeColName] = value });
                }
            }
        }

        public DateTime Update_at {
            get {
                try {
                    if (_rawData.TryGetValue(UpdateAtCol, out var v) && v != null && v is not DBNull)
                        return Convert.ToDateTime(v);
                } catch {
                    // 苦肉の策：
                    // DapperRow / ExpandoObject / CreateEmptyRow の型揺れで
                    // 変換不能な値が来ることがあるため、例外は正常系として扱う。
                    // 変換できなければ MinValue にフォールバックする。
                    // 以下同じ
                }

                return DateTime.MinValue;
            }
        }

        public DateTime LockedAt {
            get {
                try {
                    if (_rawData.TryGetValue(LockedAtCol, out var v) && v != null && v is not DBNull)
                        return Convert.ToDateTime(v);
                } catch { }

                return DateTime.MinValue;
            }
        }

        public Guid LockerID {
            get {
                try {
                    if (_rawData.TryGetValue(LockedByCol, out var v) && v != null && v is not DBNull)
                        return Guid.Parse(v.ToString());
                } catch { }

                return Guid.Empty;
            }
        }

        // ★ Primary PK を辞書で扱うプロパティ
        public Dictionary<string, object?> PrimaryPKDict {
            get {
                var dict = new Dictionary<string, object?>();

                foreach (var pk in _primaryTblPKList) {
                    if (_rawData.TryGetValue(pk, out var v))
                        dict[pk] = v;
                    else
                        dict[pk] = null; // PK がまだセットされていない新規レコード
                }

                return dict;
            }

            set {
                if (value == null)
                    return;

                // ★ PK のみを _rawData に正本化してセットする
                var record = new Dictionary<string, object>();

                foreach (var pk in _primaryTblPKList) {
                    if (!value.ContainsKey(pk))
                        throw new Exception($"PrimaryPKDict SET ERROR: key '{pk}' is missing.");

                    record[pk] = value[pk]!;
                }

                _SetProperties(record);
            }
        }


        // --- サブテーブル を保持する ---
        // 小さいサブテーブルを常時ロードしておきたいときだけ使う（通常は使用しない
        public List<(string TableName, List<Dictionary<string, object?>> Rows)> SubTables { get; set; }
                                                        = new List<(string , List<Dictionary<string, object?>>)>();

        public bool Opened {  get; set; }

        public bool Edited {  get; set; }

        //参照しているユーザーのID
        //インスタンス作成後に必ずセットする事
        public Guid CurrUsrID { get; set; }






        /// <summary>
        /// 【役割】
        ///   - このエンティティに関する業務ロジック（削除禁止・名前重複禁止など）を
        ///     “エンティティ自身” に閉じ込めるための唯一の入口。
        ///   - Nx 基盤側は、Save / Delete / Rename / Sync / Import / LocalEdit など
        ///     すべての操作の直前で必ずこのメソッドを呼び出し、
        ///     「この操作（OperationType）を実行してよいか？」を判定する。
        ///   - 具象クラスは必要に応じて override し、
        ///     自身固有の規則を NxValidator に追加することで、
        ///     正本・ローカル・同期の各世界線で同じ規則が発火する。
        ///
        /// 【設計思想】
        ///   - 規則違反時は例外を投げず、NxOperationResult を返すことで
        ///     世界線の外へ例外を漏らさずに因果を閉じる。
        ///   - NxOperationResult は ValidationErrorCode と OperationErrorCode を持ち、
        ///     UI・API・Sync のどの世界線でも同じ形式で扱える。
        ///   - BaseDataObj は抽象核であり、規則の中身は NxValidator に委譲する。
        ///     これにより循環依存を避け、抽象構造を汚さない。
        ///
        /// 【発火ポイント】
        ///   - Save / Delete / Rename / Import / Sync など、
        ///     正本世界線に影響する操作の直前で必ず発火する。
        ///   - WASM ローカル側の CRUD（working テーブル操作）は「作業用世界線」であり、
        ///     正本ではないため _Validate は発火しない。
        ///   - クライアント側で唯一 _Validate が発火するのは DataOpenAsync（編集開始）時で、
        ///     これはロック状態と整合性を取るための軽量チェックに限定される。
        ///
        /// 【結果】
        ///   - 正本の整合性は NxValidator によって一元的に保証され、
        ///     クライアント側は高速なローカル編集に専念できる。
        ///   - すべての世界線（Base / Sync / API / UI）が同じ NxOperationResult を共有し、
        ///     規則が漏れず、二重発火せず、整合性が保たれる。
        /// </summary>
        protected virtual NxValidator<TKey> CreateValidator(IBaseDataObj<TKey> IBaseDataObj, string? validData = null) {
            return new NxValidator<TKey>(this, validData);
        }
        public virtual NxOperationResult<TKey> _Validate(OperationType op, string? arg = null) {
            // ★ BaseDataObj の値をコピーした Validator を生成（循環依存なし）
            var validator = CreateValidator(this, arg);

            try {
                NxOperationResult<TKey> v = op switch {
                    OperationType.Save =>
                        validator.ValidateSave(),

                    OperationType.Delete =>
                        validator.ValidateDelete(),

                    OperationType.Rename =>
                        arg is string newName
                            ? validator.ValidateRename(newName)
                            : new NxOperationResult<TKey> {
                                ValidationErrorCode = NxValidationErrorCode.Unknown,
                                Message = "Rename の引数が不正です"
                            },

                    OperationType.Sync =>
                        validator.ValidateSync(),

                    OperationType.Import =>
                        validator.ValidateImport(),

                    OperationType.LocalEdit =>
                        validator.ValidateLocalEdit(),

                    _ =>
                        new NxOperationResult<TKey> {
                            ValidationErrorCode = NxValidationErrorCode.Unknown,
                            Message = $"未定義の OperationType: {op}"
                        }
                };

                return v;
            } catch (Exception ex) {
                // ★ BaseDataObj 側では例外を外に漏らさず NxOperationResult に変換
                return new NxOperationResult<TKey> {
                    ValidationErrorCode = NxValidationErrorCode.Unknown,
                    Message = $"Validate 内部例外: {ex.Message}"
                };
            }
        }



        public virtual void SetAsRoot(string RootName, NxDataType DataType = NxDataType.root) {
            _SetProperties(new Dictionary<string, object?> { [NameColName] = RootName });
            _datatype = DataType;
        }

        // ---------------------------------------------------------
        // _rawDataが保持するレコードの内容を正本化してセットする
        // セットするカラムは一部だけでもよい。正本化は NxTypeMapper に委譲する。
        // ---------------------------------------------------------
        public virtual void _SetProperties(IDictionary<string, object?> record)
        {
            // ---------------------------------------------------------
            // ★ NxTypeMapper による「型の正本化」
            //   - JSON の Number(double/long) → 正しい型へ
            //   - SQLite の INTEGER(long) → int/long に矯正
            //   - datetime（マイクロ秒対応）もここで正しく変換
            //   - bool / string も型マップに従って正本化
            // ---------------------------------------------------------


            // 正本化された辞書をそのまま保持
            // rawData が null の場合は「空の正本」を作る
            if (_rawData == null)
                _rawData = NxTypeMapper.Current.CreateEmptyRow(_primaryTableName);

            foreach (var kvp in record) {
                // 型マップに存在しないカラムは無視（セーブ時にエラーになるため）
                if (!NxTypeMapper.HasColumn(_primaryTableName, kvp.Key))
                    continue;

                // 単一カラムだけ正規化して上書き
                _rawData[kvp.Key] = NxTypeMapper.Convert(_primaryTableName, kvp.Key, kvp.Value);
            }
        }


        public void CreateWorkingMemory(
            Dictionary<string, object?> workingRaw) {
            // Raw の Deep Copy
            workingRaw.Clear();
            foreach (var kv in _rawData)
                workingRaw[kv.Key] = kv.Value;

            return;
        }

        // ---------------------------------------------------------
        // サブテーブル編集用ワーキングメモリ　
        // ---------------------------------------------------------
        public virtual void CreateWorkingDetailTables(
            List<(string , List<Dictionary<string, object?>>)> workingSubList) {
            workingSubList.Clear();

            foreach (var (tableName, rows) in SubTables) {
                var newRows = rows.Select(r => new Dictionary<string, object?>(r)).ToList();
                workingSubList.Add((tableName, newRows));
            }
        }

        // ---------------------------------------------------------
        // サブテーブルの読み込み
        // （メモリ上にサブテーブルを保持したいときだけ使う。通常は使わない）
        // ---------------------------------------------------------
        public virtual async Task _LoadDetailTablesFromRawAsync() {
            SubTables.Clear(); // 全サブテーブルをクリア

            foreach (var dtl in _detailTableList) {

                // ★ PK リストを取得
                if (!_detailTblPKList.TryGetValue(dtl, out var pkCols))
                    throw new Exception($"_LoadDetailTablesFromRawAsync ERROR: PK not defined for detail table '{dtl}'");

                // ★ 親由来 PK のみ WHERE に使う
                var parentPKs = pkCols.Where(pk =>
                    PrimaryPKDict.ContainsKey(pk)   // ← これが最適解
                ).ToArray();

                var whereClause = string.Join(" AND ",
                    parentPKs.Select(pk => $@"""{pk}"" = @{pk}")
                );

                string sql = $@"
                                SELECT *
                                FROM ""{dtl}""
                                WHERE {whereClause};
                            ";

                // ★ PrimaryPKDict をそのまま使う（最適解）
                var param = new Dictionary<string, object>();
                foreach (var pk in parentPKs)
                    param[pk] = PrimaryPKDict[pk]!;

                // ★ 実行
                var rawRows = await DBcon.QueryAsync<dynamic>(sql, param);

                // ★ 正本化
                var normalizedRows = rawRows
                    .Select(r => NxTypeMapper.ConvertRow(dtl, (IDictionary<string, object>)r))
                    .ToList();

                // ★ Dictionary にコピー
                var subList = normalizedRows
                    .Select(row => new Dictionary<string, object?>(row))
                    .ToList();

                // ★ SubTables に追加
                SubTables.Add((dtl, subList));
            }
        }



        // テーブルからデータを取得してJSON文字列にする(ロード用
        public string TblToJson() {
            return TblToJson(this.DataID,null);
        }


        // テーブルからデータを取得してJSON文字列にする（保存処理中に使用する、トランザクション付き）
        public string TblToJson(TKey id, IDbTransaction tran) {
            var sqlList = CreateJSONsql();
            var results = new List<object>();

            foreach (var (tableName, sql) in sqlList) {

                if (string.IsNullOrWhiteSpace(sql))
                    return null;

                // ★ PK を辞書からそのまま渡す
                var param = PrimaryPKDict;

                var r = DBcon.Query<dynamic>(
                    sql,
                    param,
                    tran
                ).ToList();

                if (r.Count > 0) {
                    results.Add(new {
                        Table = tableName,
                        Rows = r
                    });
                }
            }
            if (results.Count == 0)
                return null;

            return JsonSerializer.Serialize(results);
        }



        protected virtual IEnumerable<(string tableName, string sql)> CreateJSONsql() {

            // ★ Primary の WHERE（PrimaryPKDict のキーを使う）
            var priWhere = string.Join(" AND ",
                PrimaryPKDict.Keys.Select(pk => $@"""{pk}"" = @{pk}")
            );

            yield return (
                _primaryTableName,
                $@"SELECT * FROM ""{_primaryTableName}""
                    WHERE {priWhere}"
                    );

            if (_detailTableList.Length == 0)
                yield break;

            foreach (var dtl in _detailTableList) {

                if (!_detailTblPKList.TryGetValue(dtl, out var pkCols))
                    throw new Exception($"CreateJSONsql ERROR: PK not defined for detail table '{dtl}'");

                // ★ 親由来 PK のみ
                var parentPKs = pkCols.Where(pk =>
                    PrimaryPKDict.ContainsKey(pk)   
                ).ToArray();

                var dtlWhere = string.Join(" AND ",
                    parentPKs.Select(pk => $@"""{pk}"" = @{pk}")
                );

                yield return (
                    dtl,
                    $@"SELECT * FROM ""{dtl}""
                WHERE {dtlWhere}"
                );
            }
        }



        public virtual async Task<bool> JsonToTblAsync(string json, IDbTransaction tran) {
            if (!await _DeleteDetailQueryExecAsync(tran))
                throw new Exception("JsonToTblAsync ERROR: _DeleteDetailQueryExecAsync failed");

            var tables = JsonSerializer.Deserialize<List<TableJson>>(json)
                         ?? throw new Exception("JsonToTblAsync ERROR: json is empty");

            bool isServer = LocationKind == NxLocationKind.Server;

            foreach (var table in tables) {
                bool isDetail = _detailTblPKList.TryGetValue(table.Table, out var pkCols);
                var pkSet = isDetail ? pkCols! : _primaryTblPKList;

                foreach (var row in table.Rows) {
                    var normalized = NxTypeMapper.ConvertRow(table.Table, row);

                    if (isDetail) {
                        // 親 PK の補完は、列リストを組み立てる前に行う
                        var parentPKs = pkCols!.Where(pk => pk == TenantCodeCol || pk == IdColName).ToArray();
                        foreach (var pk in parentPKs)
                            normalized[pk] = _rawData[pk];

                        foreach (var pk in pkCols!.Except(parentPKs))
                            if (!normalized.ContainsKey(pk))
                                throw new Exception($"Detail PK '{pk}' missing in JSON row.");
                    }

                    var columns = string.Join(", ", normalized.Keys.Select(k => $@"""{k}"""));
                    var values = string.Join(", ", normalized.Keys.Select(k => $@"@{k}"));
                    var updates = string.Join(", ",
                        normalized.Keys
                            .Where(k => !pkSet.Contains(k))
                            .Where(k => !(isServer && (k == LockedByCol || k == LockedAtCol)))  // 5番のa'
                            .Select(k => $@"""{k}"" = EXCLUDED.""{k}"""));

                    var sql = isDetail
                        ? BuildDetailUpsertSql(table.Table, columns, values, updates)
                        : BuildPrimaryUpsertSql(table.Table, columns, values, updates);

                    var rowsAffected = await DBcon.ExecuteAsync(sql, normalized, tran);
                    if (rowsAffected != 1)
                        throw new Exception($"JsonToTblAsync ERROR: Upsert failed or affected {rowsAffected} rows");
                }
            }

            // 全行を反映してから、メモリ上のプロパティを1回だけ更新する(同じトランザクション内で読む)
            await _UpdatePropertiesFromTblAsync(tran);
            return true;
        }



        protected Dictionary<string, string[]> DetailPrimaryKeys { get; }
                                                 = new Dictionary<string, string[]>();



        protected string BuildPrimaryUpsertSql(string tableName, string columns, string values, string updates) {
            var pkClause = string.Join(", ", _primaryTblPKList.Select(c => $@"""{c}"""));

            return $@"
                        INSERT INTO ""{tableName}""
                        ({columns})
                        VALUES ({values})
                        ON CONFLICT ({pkClause})
                        DO UPDATE SET
                            {updates};
                    ";
        }



        protected string BuildDetailUpsertSql(string tableName, string columns, string values, string updates) {
            var pkCols = _detailTblPKList[tableName];
            var pkClause = string.Join(", ", pkCols.Select(c => $@"""{c}"""));

            return $@"
                        INSERT INTO ""{tableName}""
                        ({columns})
                        VALUES ({values})
                        ON CONFLICT ({pkClause})
                        DO UPDATE SET
                            {updates};
                    ";
        }




        // ---------------------------------------------------------
        // DataOpenAsync（編集開始前処理）
        //ローカル専用の派生ではロック機能を使わない。
        //だから DataOpenAsync / DataCloseAsync は null を返せばいい。
        //Sync や API の世界線では LockStatus をちゃんと返すようにしてある。
        // ---------------------------------------------------------
        public virtual async Task<NxOperationResult<TKey>> DataOpenAsync() {

            try {
                // ローカル専用ではロック機能を使わない

                // ワークテーブルが存在するエンティティは、ワークテーブルを削除して再作成する
                if (_primaryTableName != _w_tblName) {
                    await _DeleteWorksAsync();
                    await _InsertWorksAsync();
                }
                Opened = true;

                return new NxOperationResult<TKey> {
                    Message = "Local DataOpen",
                    LockStatus = null
                };
            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"DataOpenAsync failed: {ex.Message}"
                };
            }
        }


        // ---------------------------------------------------------
        // DataCloseAsync(編集終了）
        // フラグのセットのみ。UI側でSaveまたはRestoreを呼んだうえで
        // DataCloseを呼ぶこと
        // ローカル専用の派生ではロック機能を使わない。
        // DataOpenAsync / DataCloseAsync は null を返せばいい。
        // Sync や API の世界線では LockStatus をちゃんと返す様にしてある。
        // ---------------------------------------------------------
        public virtual async Task<NxOperationResult<TKey>> DataCloseAsync() {

            try {
                Opened = false; //編集中フラグをOFF

                var lockst = await _SetUnLockAsync();   // ローカル行のロック情報を解除する

                return new NxOperationResult<TKey> {
                    LockStatus = lockst,
                    Message = "Local DataClose"
                };
            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"DataCloseAsync failed: {ex.Message}"
                };
            }
        }


        // ---------------------------------------------------------
        // 削除クエリを実行する。
        // BaseDataObjMgrからはこれを呼び出す事
        // ---------------------------------------------------------
        public virtual async Task<bool> _DeleteQueryExecAsync(IDbTransaction transaction, bool softDelete) {
            if (softDelete) {
                // ソフトデリート：Primary のみ論理削除
                return await _SoftDeletePrimaryQueryExecAsync(transaction);
            } else {
                // 物理削除：Detail → Primary の順に削除
                bool detailDeleted = await _DeleteDetailQueryExecAsync(transaction);

                bool primaryDeleted;
                if (LocationKind == NxLocationKind.Server) {
                    // サーバー側：削除伝播のため Primary は論理削除
                    primaryDeleted = await _SoftDeletePrimaryQueryExecAsync(transaction);
                } else {
                    // クライアント側：Primary を物理削除
                    primaryDeleted = await _DeletePrimaryQueryExecAsync(transaction);
                }

                return detailDeleted && primaryDeleted;
            }
        }


        protected virtual async Task<bool> _DeletePrimaryQueryExecAsync(IDbTransaction transaction) {
            try {
                // ★ PK の WHERE 句を動的生成
                var whereClause = string.Join(" AND ",
                    _primaryTblPKList.Select(pk => $@"""{pk}"" = @{pk}")
                );

                string sql = $@"
                                DELETE FROM ""{_primaryTableName}""
                                WHERE {whereClause};
                            ";

                // ★ PrimaryPKDict をそのまま使う（最適解）
                var param = new Dictionary<string, object>();
                foreach (var pk in _primaryTblPKList)
                    param[pk] = PrimaryPKDict[pk]!;

                var rows = await DBcon.ExecuteAsync(sql, param, transaction);

                // ★ 世界線整合性チェック
                if (rows == 0)
                    throw new Exception("_DeletePrimaryQueryExecAsync ERROR: Primary delete failed (no row deleted)");

                if (rows > 1)
                    throw new Exception($"_DeletePrimaryQueryExecAsync ERROR: {rows} rows deleted (BROKEN)");

                return true;
            } catch (Exception ex) {
                throw new Exception($"_DeletePrimaryQueryExecAsync ERROR: {ex.Message}", ex);
            }
        }



        // softデリートメソッド ※ API 層からの削除についてプライマリテーブルに対しては必ずこれを実行する。
        // 論理削除を実装する：
        //   - TblName テーブルのレコードを「削除済み」と扱える状態にする
        //     （Nxの標準は：Updated_atを最大値に、Locked_atを最小値にすることで削除済と判定するが、
        //     　アプリケーションロジックとしてソフトデリートを実装する場合は明示的に無効化フラグを
        //     　持たせそのフラグで判定するロジックに修正することを推奨）。
        //   -
        // クライアント側 では、この論理削除状態を参照して「削除済みデータ」を判定する。
        // 実装時は挙動に注意すること。
        protected virtual async Task<bool> _SoftDeletePrimaryQueryExecAsync(IDbTransaction transaction) {
            // ★ PK の WHERE 句を動的生成
            var whereClause = string.Join(" AND ",
                _primaryTblPKList.Select(pk => $@"""{pk}"" = @{pk}")
            );

            string sql = $@"
                            UPDATE ""{_primaryTableName}""
                            SET ""deleted"" = 1,
                                ""{UpdateAtCol}"" = @UpdateAt,
                                ""{LockedAtCol}"" = @LockedAt
                            WHERE {whereClause};
                        ";

            // ★ PrimaryPKDict をそのまま使う（最適解）
            var param = new Dictionary<string, object>();

            foreach (var pk in _primaryTblPKList)
                param[pk] = PrimaryPKDict[pk]!;   // rawData から正本化済み PK を取得

            // SoftDelete 固有の値
            param[UpdateAtCol] = DateTime.MaxValue;
            param[LockedAtCol] = DateTime.MinValue;

            var rows = await DBcon.ExecuteAsync(sql, param, transaction);

            // ★ 整合性チェック
            if (rows == 0)
                throw new Exception("ERROR: SoftDelete failed (no row updated)");

            if (rows > 1)
                throw new Exception($"BROKEN: {rows} rows soft-deleted");

            return true;
        }




        //ディティールテーブル(サブテーブル)の物理削除。
        //保存ロジックは物理削除→インサートの順で実行されるため、
        //ディテールテーブルの削除を行わないっ設計であっても派生先で必ず実装すること
        public virtual async Task<bool> _DeleteDetailQueryExecAsync(IDbTransaction transaction) {
            // ★ Detail が存在しないなら即終了
            if (_detailTableList == null || _detailTableList.Length == 0)
                return true;

            try {
                foreach (var dtl in _detailTableList) {

                    // ★ PK が定義されていない場合はエラー
                    if (!_detailTblPKList.TryGetValue(dtl, out var pkCols))
                        throw new Exception($"_DeleteDetailQueryExecAsync ERROR: PK not defined for detail table '{dtl}'");

                    // ★ 親由来 PK のみ抽出
                    var parentPKs = pkCols.Where(pk =>
                        pk == TenantCodeCol || pk == IdColName
                    ).ToArray();

                    // ★ WHERE は親 PK のみ
                    var whereClause = string.Join(" AND ",
                        parentPKs.Select(pk => $@"""{pk}"" = @{pk}")
                    );

                    string sql = $@"
                                        DELETE FROM ""{dtl}""
                                        WHERE {whereClause};
                                    ";

                    // ★ PrimaryPKDict をそのまま使う（最適解）
                    var param = new Dictionary<string, object>();
                    foreach (var pk in parentPKs)
                        param[pk] = PrimaryPKDict[pk]!;

                    await DBcon.ExecuteAsync(sql, param, transaction);
                }

                return true;
            } catch (Exception ex) {
                throw new Exception($"_DeleteDetailQueryExecAsync ERROR: {ex.Message}", ex);
            }
        }

        // =======================================================
        // ワークテーブル（_w_tblName および _w_detailTblList）の削除
        // =======================================================
        public virtual async Task<bool> _DeleteWorksAsync(IDbTransaction? transaction = null) {
            try {
                // 1. サブテーブル（_w_detailTblList）の物理削除
                if (_w_detailTblList != null && _w_detailTblList.Length > 0) {
                    foreach (var dtl in _w_detailTblList) {
                        if (!_detailTblPKList.TryGetValue(dtl, out var pkCols))
                            throw new Exception($"_DeleteWorksAsync ERROR: PK not defined for detail table '{dtl}'");

                        // 親由来 PK のみ抽出（TenantCode または IdColName）
                        var parentPKs = pkCols.Where(pk =>
                            pk == TenantCodeCol || pk == IdColName
                        ).ToArray();

                        var whereClause = string.Join(" AND ",
                            parentPKs.Select(pk => $@"""{pk}"" = @{pk}")
                        );

                        string dtlSql = $@"
                            DELETE FROM ""{dtl}""
                            WHERE {whereClause}
                        ";

                        var dtlParam = new Dictionary<string, object>();
                        foreach (var pk in parentPKs) {
                            if (PrimaryPKDict.TryGetValue(pk, out var val) && val != null) {
                                dtlParam[pk] = val;
                            } else if (_rawData.TryGetValue(pk, out var rawVal) && rawVal != null) {
                                dtlParam[pk] = rawVal;
                            } else {
                                throw new Exception($"_DeleteWorksAsync ERROR: Missing primary key value for '{pk}'");
                            }
                        }

                        await DBcon.ExecuteAsync(dtlSql, dtlParam, transaction);
                    }
                }

                // 2. メインワークテーブル（_w_tblName）の物理削除
                if (!string.IsNullOrEmpty(_w_tblName)) {
                    var priWhereClause = string.Join(" AND ",
                        _primaryTblPKList.Select(pk => $@"""{pk}"" = @{pk}")
                    );

                    string priSql = $@"
                        DELETE FROM ""{ _w_tblName}""
                        WHERE {priWhereClause};
                    ";

                    var priParam = new Dictionary<string, object>();
                    foreach (var pk in _primaryTblPKList) {
                        if (PrimaryPKDict.TryGetValue(pk, out var val) && val != null) {
                            priParam[pk] = val;
                        } else if (_rawData.TryGetValue(pk, out var rawVal) && rawVal != null) {
                            priParam[pk] = rawVal;
                        } else {
                            throw new Exception($"_DeleteWorksAsync ERROR: Missing primary key value for '{pk}'");
                        }
                    }

                    await DBcon.ExecuteAsync(priSql, priParam, transaction);
                }

                return true;
            } catch (Exception ex) {
                // 例外を握りつぶさず、上位へ正確に伝えるためにカスタムメッセージ付きで再スロー
                throw new Exception($"_DeleteWorksAsync failed: {ex.Message}", ex);
            }
        }

        // =======================================================
        // 正本テーブルの内容をワークテーブル（_w_tblName / _w_detailTblList）へ複製・挿入する
        // _detailTableList / _w_detailTblList の順序は必ず対応していること
        // =======================================================
        public virtual async Task<bool> _InsertWorksAsync(IDbTransaction? transaction = null) {
            try {
                // 1. メインテーブルの複製（_primaryTableName → _w_tblName）
                if (!string.IsNullOrEmpty(_primaryTableName) && !string.IsNullOrEmpty(_w_tblName)) {
                    // PK の WHERE 句を動的生成
                    var priWhereClause = string.Join(" AND ",
                        _primaryTblPKList.Select(pk => $@"""{pk}"" = @{pk}")
                    );

                    // 正本から該当レコードを全カラム取得するSQL
                    string selectPriSql = $@"
                        SELECT* FROM ""{ _primaryTableName}""
                        WHERE { priWhereClause};
                    ";

                    var priParam = new Dictionary<string, object>();
                    foreach (var pk in _primaryTblPKList) {
                        if (PrimaryPKDict.TryGetValue(pk, out var val) && val != null) {
                            priParam[pk] = val;
                        } else if (_rawData.TryGetValue(pk, out var rawVal) && rawVal != null) {
                            priParam[pk] = rawVal;
                        } else {
                            throw new Exception($"_InsertWorksAsync ERROR: Missing primary key value for '{pk}'");
                        }
                    }

                    // 正本からレコードを取得（DapperRow / 辞書 or 動的オブジェクト）
                    var recordObj = await DBcon.QueryFirstOrDefaultAsync<dynamic>(
                                                selectPriSql,
                                                priParam,
                                                transaction);

                    if (recordObj != null) {
                        var record = (IDictionary<string, object>)recordObj;
                        // 型マッピング等を通して一応正規化しておく
                        var normalized = NxTypeMapper.ConvertRow(_w_tblName, record);

                        var cols = string.Join(", ", normalized.Keys.Select(k => $@"""{k}"""));
                        var vals = string.Join(", ", normalized.Keys.Select(k => $@"""{k}""")); // プレースホルダーは @key

                        // 動的に INSERT 文を構築（プレースホルダー用）
                        var colNames = new List<string>();
                        var valParams = new List<string>();
                        foreach (var kv in normalized) {
                            colNames.Add($@"""{kv.Key}""");
                            valParams.Add($@"@{kv.Key}");
                        }

                        string insertPriSql = $@"
                            INSERT INTO ""{ _w_tblName}""
                            ({ string.Join(", ", colNames)})
                            VALUES({ string.Join(", ", valParams)})
                            ON CONFLICT DO NOTHING;
                        ";

                        await DBcon.ExecuteAsync(insertPriSql, normalized, transaction);
                    }
                }

                // 2. 詳細テーブルの複製（_detailTableList から _w_detailTblList へ順次コピー）
                if (_detailTableList != null && _detailTableList.Length > 0 &&
                    _w_detailTblList != null && _w_detailTblList.Length > 0) {

                    for (int i = 0; i < _detailTableList.Length; i++) {
                        var srcTable = _detailTableList[i];
                        var dstTable = _w_detailTblList[i];

                        if (!_detailTblPKList.TryGetValue(srcTable, out var pkCols))
                            throw new Exception($"_InsertWorksAsync ERROR: PK not defined for detail table '{srcTable}'");

                        // 親由来 PK のみ抽出（TenantCode または IdColName）
                        var parentPKs = pkCols.Where(pk =>
                            pk == TenantCodeCol || pk == IdColName
                        ).ToArray();

                        var dtlWhereClause = string.Join(" AND ",
                            parentPKs.Select(pk => $@"""{pk}"" = @{pk}")
                        );

                        string selectDtlSql = $@"
                            SELECT* FROM ""{ srcTable}""
                            WHERE { dtlWhereClause};
                        ";

                        var dtlParam = new Dictionary<string, object>();
                        foreach (var pk in parentPKs) {
                            if (PrimaryPKDict.TryGetValue(pk, out var val) && val != null) {
                                dtlParam[pk] = val;
                            } else if (_rawData.TryGetValue(pk, out var rawVal) && rawVal != null) {
                                dtlParam[pk] = rawVal;
                            } else {
                                throw new Exception($"_InsertWorksAsync ERROR: Missing primary key value for detail '{pk}'");
                            }
                        }

                        var rows = await DBcon.QueryAsync<dynamic>(
                                                                    selectDtlSql,
                                                                    dtlParam,
                                                                    transaction);

                        foreach (var rowObj in rows) {
                            var row = (IDictionary<string, object>)rowObj;

                            var normalizedDtl = NxTypeMapper.ConvertRow(
                                                                    dstTable,
                                                                    row);

                            var dtlColNames = new List<string>();
                            var dtlValParams = new List<string>();
                            foreach (var kv in normalizedDtl) {
                                dtlColNames.Add($@"""{kv.Key}""");
                                dtlValParams.Add($@"@{kv.Key}");
                            }

                            string insertDtlSql = $@"
                                INSERT INTO ""{ dstTable}""
                                ({ string.Join(", ", dtlColNames)})
                                VALUES({ string.Join(", ", dtlValParams)})
                                ON CONFLICT DO NOTHING;
                            ";

                            await DBcon.ExecuteAsync(insertDtlSql, normalizedDtl, transaction);
                        }
                    }
                }

                return true;
            } catch (Exception ex) {
                // 例外を握りつぶさずに再スローして上位トランザクションのロールバックへ繋げる
                throw new Exception($"_InsertWorksAsync failed: {ex.Message}", ex);
            }
        }

        // 名前変更の検証メソッド
        // 必要に応じて派生クラスでオーバーライドできるように virtual にしておく
        public virtual async Task<NxOperationResult<TKey>> ReNameAsync(string newName) {

            // 1. バリデーション
            var v = _Validate(OperationType.Rename, newName);
            if (!v.Success)
                return v;

            IDbTransaction transaction = DBcon.BeginTransaction();

            try {
                if (await _ReNameQueryExecAsync(newName, transaction)) {
                    transaction.Commit();
                    await _UpdatePropertiesFromTblAsync();

                    return new NxOperationResult<TKey> {
                        Message = "Rename completed",
                        RawData = _rawData
                    };
                }


                return new NxOperationResult<TKey> {
                    ValidationErrorCode = NxValidationErrorCode.CannotRename,
                    Message = "Rename failed"
                };
            } catch (Exception ex) {
                transaction.Rollback();
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"Rename exception: {ex.Message}"
                };
            }
        }


        //DBへのデータ名変更を試みる。成功した場合プロパティの値も書き換える
        public virtual async Task<bool> _ReNameQueryExecAsync(string newName, IDbTransaction dbTransaction) {
            try {
                var updateAt = DateTime.UtcNow;

                // ★ PK の WHERE 句を動的生成
                var whereClause = string.Join(" AND ",
                    _primaryTblPKList.Select(pk => $@"""{pk}"" = @{pk}")
                );

                string sql = $@"
                                UPDATE ""{_primaryTableName}""
                                SET ""{NameColName}"" = @name,
                                    ""{UpdateAtCol}"" = @update_at
                                WHERE {whereClause};
                            ";

                // ★ PK パラメータ辞書を作成（PrimaryPKDict を使う）
                var param = new Dictionary<string, object> {
                    [NameColName] = newName,
                    [UpdateAtCol] = updateAt
                };

                foreach (var pk in _primaryTblPKList)
                    param[pk] = PrimaryPKDict[pk]!;

                var rows = await DBcon.ExecuteAsync(sql, param, dbTransaction);

                // ★ 整合性チェック
                if (rows == 0)
                    throw new Exception("_ReNameQueryExecAsync ERROR: Rename failed (no row updated)");

                if (rows > 1)
                    throw new Exception($"_ReNameQueryExecAsync ERROR: {rows} rows updated (BROKEN)");

                return true;
            } catch (Exception ex) {
                throw new Exception($"_ReNameQueryExecAsync ERROR: {ex.Message}", ex);
            }
        }




        // データベースから最新のプロパティを取得して _rawData に反映する
        public virtual async Task _UpdatePropertiesFromTblAsync(IDbTransaction tran = null) {
            try {
                // ★ PK の WHERE 句を動的生成
                var whereClause = string.Join(" AND ",
                    _primaryTblPKList.Select(pk => $@"""{pk}"" = @{pk}")
                );

                string sql = $@"
                                SELECT *
                                FROM ""{_primaryTableName}""
                                WHERE {whereClause};
                            ";

                // ★ PrimaryPKDict をそのまま使う（最適解）
                var param = new Dictionary<string, object>();
                foreach (var pk in _primaryTblPKList)
                    param[pk] = PrimaryPKDict[pk]!;

                var record = await DBcon.QueryFirstOrDefaultAsync<dynamic>(sql, param, tran);

                if (record == null)
                    throw new Exception(
                        $"_UpdatePropertiesFromTblAsync ERROR: No record found in {_primaryTableName} (PK={string.Join(",", _primaryTblPKList)})"
                    );

                var dict = ((IDictionary<string, object>)record)
                    .ToDictionary(k => k.Key, v => v.Value);

                var normalized = NxTypeMapper.ConvertRow(_primaryTableName, dict);

                _SetProperties(normalized);
            } catch (Exception ex) {
                throw new Exception($"_UpdatePropertiesFromTblAsync ERROR: {ex.Message}", ex);
            }
        }




        // =======================================================
        // 保存の時にDataIDを確定させる場合はここにその処理を記述(ZmstEntity参照)
        // =======================================================

        public virtual TKey? EnsureIDForSave(IDbTransaction tran){
            return this.DataID;
        }
        // =======================================================
        // 保存のローカル実装（ワーク系テーブルへの保存）
        // 【処理順】
        //   1. 新規レコードの場合はローカル側で仮IDを採番する
        //   2. ワーク側ディテール（_w_detailTblList）の事前整理・削除
        //      - subTables が渡されている場合：ワーク側の既存データを一度クリアして差し替え
        //      - subTables が null の場合：_w_detailTblList と _detailTableList を比較し、不要なものをワーク側から削除
        //   3. workingRaw を _w_tblName（メインワークテーブル）へ Upsert 保存
        //   4. subTables を _w_detailTblList（サブワークテーブル群）へ Upsert 保存
        // =======================================================
        public virtual async Task<NxOperationResult<TKey>> SaveAsync(
                                            Dictionary<string, object?> workingRaw,
                                            List<(string, List<Dictionary<string, object?>>)>? subTables = null) {
            using var tran = DBcon.BeginTransaction();

            try {
                // 1. 【ID採番】新規レコードならローカル側で仮ID採番
                var newID = EnsureIDForSave(tran);
                DataID = newID;
                workingRaw[IdColName] = newID;

                // 2. 【ワーク側ディテールの差分整理・削除】
                if (!await _SyncAndDeleteWorkingDetailsAsync(subTables, tran)) {
                    tran.Rollback();
                    return new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.DbError,
                        Message = "ワーク側ディテールデータの整理に失敗しました"
                    };
                }

                // 3. 【ワークテーブルへの一括保存】
                if (!await SaveWorkingAsync(workingRaw, subTables, tran)) {
                    tran.Rollback();
                    return new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.DbError,
                        Message = "ワークテーブルへの保存に失敗しました"
                    };
                }

                // 4. 【本テーブル（正本）への保存・反映】
                // ワークテーブルに確定した内容を、本番の本テーブル（_primaryTableName / _detailTableList）へ書き込む
                // ただし、ワークテーブルが存在しない設計の場合（本テーブルと同じテーブル名）の場合は保存済なので実行しない
                if (_w_tblName != _primaryTableName) {

                    if (!await SaveQueryExec(tran)) {
                        tran.Rollback();
                        return new NxOperationResult<TKey> {
                            OperationErrorCode = NxOperationErrorCode.DbError,
                            Message = "本テーブルへの保存に失敗しました"
                        };
                    }
                }

                // すべての処理が正常に完了したらトランザクションをコミット
                tran.Commit();

                // メモリ上の正本データを更新し、DBから最新状態を再読み込み
                ApplyWorkingToRaw(workingRaw);
                await _UpdatePropertiesFromTblAsync();

                return new NxOperationResult<TKey> {
                    RawData = _rawData,
                    Message = "Save completed"
                };
            } catch (Exception ex) {
                tran.Rollback();
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"SaveAsync failed: {ex.Message}"
                };
            }
        }


        // =======================================================
        // ワーク側で削除され、本テーブルに残っているレコードを削除する
        // detailの各行にカスケードが張られていた場合、必要なレコードのみ
        // 残してアップサート出来る様配慮
        // =======================================================
        public virtual async Task<bool> _SyncAndDeleteWorkingDetailsAsync(
            List<(string TableName, List<Dictionary<string, object?>> Rows)>? subTables,
            IDbTransaction tran) {

            // ワーク側のディテールテーブル定義がない場合は処理をスキップ
            if (_w_detailTblList == null || _w_detailTblList.Length == 0)
                return true;

            // ワーク側の各ディテールテーブルを順番に走査する
            for (int i = 0; i < _w_detailTblList.Length; i++) {
                var targetWorkTable = _w_detailTblList[i];

                // 対象ワークテーブルの主キー構成を取得（定義がない場合は正本側からフォールバック取得）
                if (!_detailTblPKList.TryGetValue(targetWorkTable, out var pkCols)) {
                    if (_detailTableList != null && i < _detailTableList.Length) {
                        _detailTblPKList.TryGetValue(_detailTableList[i], out pkCols);
                    }
                }

                if (pkCols == null)
                    throw new Exception($"SyncAndDeleteWorkingDetailsAsync ERROR: PK not defined for work detail table '{targetWorkTable}'");

                // 主キーを「親結合用PK」と「明細の固有PK」に分離
                var parentPKs = pkCols.Where(pk => pk == TenantCodeCol || pk == IdColName).ToArray();
                var uniquePKs = pkCols.Except(parentPKs).ToArray();

                // 親ID等で絞り込むための WHERE 条件を構築
                var parentWhere = string.Join(" AND ", parentPKs.Select(pk => $@"""{pk}"" = @{pk}"));
                var param = new Dictionary<string, object>();
                foreach (var pk in parentPKs) {
                    param[pk] = PrimaryPKDict.TryGetValue(pk, out var val) && val != null ? val : _rawData[pk];
                }

                // -----------------------------------------------------------------
                // パターンA: subTables が null の場合
                //   → ワーク側の既存行と、本テーブル（_detailTableList）側を比較し、
                //      本テーブル側に存在しない（画面等で削除された）行をワーク側から削除する
                // -----------------------------------------------------------------
                if (subTables == null) {
                    string targetSrcTable = (_detailTableList != null && i < _detailTableList.Length) ? _detailTableList[i] : targetWorkTable;

                    // 1. 本テーブル側から現在のレコードを全取得
                    // 本テーブル取得
                    var srcRows = await DBcon.QueryAsync<dynamic>(
                        $@"SELECT * FROM ""{targetSrcTable}"" WHERE {parentWhere};",
                        param,
                        tran);

                    // ワークテーブル取得
                    var workRows = await DBcon.QueryAsync<dynamic>(
                        $@"SELECT * FROM ""{targetWorkTable}"" WHERE {parentWhere};",
                        param,
                        tran);

                    // ワーク側PK一覧作成
                    var workPkValuesList = new List<Dictionary<string, object?>>();

                    foreach (var w in workRows) {
                        var row = (IDictionary<string, object>)w;

                        var pkDict = new Dictionary<string, object?>();

                        foreach (var upk in uniquePKs) {
                            pkDict[upk] = row[upk];
                        }

                        workPkValuesList.Add(pkDict);
                    }

                    // 本テーブルに存在するがワークに存在しない行を削除
                    foreach (var srcObj in srcRows) {
                        var srcRow = (IDictionary<string, object>)srcObj;

                        bool existsInWork = false;

                        foreach (var workPk in workPkValuesList) {
                            bool match = true;

                            foreach (var upk in uniquePKs) {
                                if (!srcRow.TryGetValue(upk, out var srcVal)
                                    || !Equals(srcVal, workPk[upk])) {
                                    match = false;
                                    break;
                                }
                            }

                            if (match) {
                                existsInWork = true;
                                break;
                            }
                        }

                        if (!existsInWork) {
                            var delWhereParts = new List<string>();
                            var delParam = new Dictionary<string, object>();

                            foreach (var pk in pkCols) {
                                delWhereParts.Add($@"""{pk}"" = @del_{pk}");
                                delParam[$"del_{pk}"] = srcRow[pk];
                            }

                            string delSql =
                                $@"DELETE FROM ""{targetSrcTable}""
                                WHERE {string.Join(" AND ", delWhereParts)};";

                            await DBcon.ExecuteAsync(delSql, delParam, tran);
                        }
                    }                    
                } else {
                    // -----------------------------------------------------------------
                    // パターンB: subTables が渡されている場合
                    //   → 新しいサブテーブル群で丸ごと上書きするため、
                    //      ワーク側の該当親に紐づく既存ディテール行を一度すべてクリアする
                    // -----------------------------------------------------------------
                    string deleteSql = $@"DELETE FROM ""{targetWorkTable}"" WHERE {parentWhere};";
                    await DBcon.ExecuteAsync(deleteSql, param, tran);
                }
            }

            return true;
        }


        // =======================================================
        // ワークテーブルへの保存処理（メイン ＋ サブ一括実行）
        // =======================================================
        public virtual async Task<bool> SaveWorkingAsync(
            Dictionary<string, object?> workingRaw,
            List<(string TableName, List<Dictionary<string, object?>> Rows)>? subTables,
            IDbTransaction tran) {

            // 1. メインワークテーブル（_w_tblName）への保存
            if (!await SaveWorkingPrimaryAsync(workingRaw, tran))
                return false;

            // 2. サブワークテーブル群（_w_detailTblList）への保存
            if (!await SaveWorkingDetailsAsync(subTables, workingRaw, tran))
                return false;

            return true;
        }


        // =======================================================
        // メインワークテーブル保存（workingRaw → _w_tblName）
        // （競合時は UPDATE、新規時は INSERT の Upsert 処理）
        // =======================================================
        public virtual async Task<bool> SaveWorkingPrimaryAsync(
            Dictionary<string, object?> workingRaw,
            IDbTransaction tran) {
            try {
                // ワーク保存時の更新日時をセット
                workingRaw[_updateAtCol] = DateTime.UtcNow;

                var cols = new List<string>();
                var vals = new List<string>();
                var updates = new List<string>();

                foreach (var kv in workingRaw) {
                    cols.Add($@"""{kv.Key}""");
                    vals.Add($@"@{kv.Key}");

                    // PK カラム以外を UPDATE 対象として構築
                    if (!_primaryTblPKList.Contains(kv.Key)) {
                        updates.Add($@"""{kv.Key}"" = EXCLUDED.""{kv.Key}""");
                    }
                }

                var pkClause = string.Join(", ", _primaryTblPKList.Select(pk => $@"""{pk}"""));

                string sql = $@"
                    INSERT INTO ""{_w_tblName}""
                    ({string.Join(", ", cols)})
                    VALUES ({string.Join(", ", vals)})
                    ON CONFLICT ({pkClause})
                    DO UPDATE SET
                        {string.Join(", ", updates)};
                ";

                var rows = await DBcon.ExecuteAsync(sql, workingRaw, tran);
                if (rows != 1)
                    throw new Exception($"SaveWorkingPrimaryAsync ERROR: UPSERT affected {rows} rows");

                return true;
            } catch (Exception ex) {
                throw new Exception($"SaveWorkingPrimaryAsync ERROR: {ex.Message}", ex);
            }
        }


        // =======================================================
        // サブワークテーブル保存（subTables → _w_detailTblList）
        // =======================================================
        public virtual async Task<bool> SaveWorkingDetailsAsync(
            List<(string TableName, List<Dictionary<string, object?>> Rows)>? w_DetailTblList,
            Dictionary<string, object?> MainWorkingRaw,
            IDbTransaction tran) {
            if (w_DetailTblList == null || w_DetailTblList.Count == 0)
                return true;

            for (int i = 0; i < w_DetailTblList.Count; i++) {
                if (i >= _w_detailTblList.Length) break; // 定義数を超えた場合は安全のため打ち切り

                var tableName = _w_detailTblList[i]; // 保存先のワーク用ディテールテーブル名
                var detailRows = w_DetailTblList[i].Rows;

                // PK定義の取得（ワーク側にない場合は正本側から補完）
                if (!_detailTblPKList.TryGetValue(tableName, out var pkCols)) {
                    if (_detailTableList != null && i < _detailTableList.Length)
                        _detailTblPKList.TryGetValue(_detailTableList[i], out pkCols);
                }

                if (pkCols == null)
                    throw new Exception($"SaveWorkingDetailsAsync ERROR: PK not defined for detail table '{tableName}'");

                var parentPKs = pkCols.Where(pk => pk == TenantCodeCol || pk == IdColName).ToArray();

                foreach (var row in detailRows) {
                    // 親の PK 情報を明細行に補完
                    foreach (var pk in parentPKs)
                        row[pk] = MainWorkingRaw[pk];

                    // 派生クラス用のフック（必要に応じた行データの最終調整）
                    CompleteDetailRow(tableName, row, MainWorkingRaw);

                    // 固有PKが確実に含まれているか検証
                    foreach (var pk in pkCols.Except(parentPKs)) {
                        if (!row.ContainsKey(pk))
                            throw new Exception($"Detail PK '{pk}' missing in row.");
                    }

                    var cols = new List<string>();
                    var vals = new List<string>();
                    var updates = new List<string>();

                    foreach (var kv in row) {
                        cols.Add($@"""{kv.Key}""");
                        vals.Add($@"@{kv.Key}");

                        // PK および更新日時以外を UPDATE 対象にする
                        if (!pkCols.Contains(kv.Key) && kv.Key != _updateAtCol)
                            updates.Add($@"""{kv.Key}"" = EXCLUDED.""{kv.Key}""");
                    }

                    var pkClause = string.Join(", ", pkCols.Select(pk => $@"""{pk}"""));

                    string sql = $@"
                        INSERT INTO ""{tableName}""
                        ({string.Join(", ", cols)})
                        VALUES ({string.Join(", ", vals)})
                        ON CONFLICT ({pkClause})
                        DO UPDATE SET
                            {string.Join(", ", updates)};
                    ";

                    var affected = await DBcon.ExecuteAsync(sql, row, tran);
                    if (affected != 1)
                        throw new Exception($"SaveWorkingDetailsAsync ERROR: UPSERT affected {affected} rows");
                }
            }

            return true;
        }
        protected virtual void CompleteDetailRow(
                                string tableName, Dictionary<string, object?> row, Dictionary<string, object?> mainWorkingRaw) { }

        // =======================================================
        // ワークテーブルの内容を本テーブル（正本）へ反映・保存する
        // （_InsertWorksAsync の逆処理：ワーク → 本テーブルへの Upsert 反映）
        // =======================================================
        public virtual async Task<bool> SaveQueryExec(IDbTransaction transaction) {
            try {
                // 1. メインテーブルの反映（_w_tblName → _primaryTableName）
                if (!string.IsNullOrEmpty(_w_tblName) && !string.IsNullOrEmpty(_primaryTableName)) {
                    // PK の WHERE 句を動的生成
                    var priWhereClause = string.Join(" AND ",
                        _primaryTblPKList.Select(pk => $@"""{pk}"" = @{pk}")
                    );

                    string selectWorkPriSql = $@"
                        SELECT * FROM ""{_w_tblName}""
                        WHERE {priWhereClause};
                    ";

                    var priParam = new Dictionary<string, object>();
                    foreach (var pk in _primaryTblPKList) {
                        if (PrimaryPKDict.TryGetValue(pk, out var val) && val != null) {
                            priParam[pk] = val;
                        } else if (_rawData.TryGetValue(pk, out var rawVal) && rawVal != null) {
                            priParam[pk] = rawVal;
                        } else {
                            throw new Exception($"SaveQueryExec ERROR: Missing primary key value for '{pk}'");
                        }
                    }

                    // ワーク側から該当レコードを取得
                    var recordObj = await DBcon.QueryFirstOrDefaultAsync<dynamic>(
                                                    selectWorkPriSql,
                                                    priParam,
                                                    transaction);

                    if (recordObj != null) {
                        var record = (IDictionary<string, object>)recordObj;
                        // 本テーブルのスキーマに合わせて型を正規化
                        var normalized = NxTypeMapper.ConvertRow(_primaryTableName, record);

                        var cols = new List<string>();
                        var vals = new List<string>();
                        var updates = new List<string>();

                        foreach (var kv in normalized) {
                            cols.Add($@"""{kv.Key}""");
                            vals.Add($@"@{kv.Key}");

                            // PK 以外のカラムを UPDATE 対象にする
                            if (!_primaryTblPKList.Contains(kv.Key)) {
                                updates.Add($@"""{kv.Key}"" = EXCLUDED.""{kv.Key}""");
                            }
                        }

                        var pkClause = string.Join(", ", _primaryTblPKList.Select(pk => $@"""{pk}"""));

                        // 本テーブルへの Upsert 実行
                        string upsertPriSql = $@"
                            INSERT INTO ""{_primaryTableName}""
                            ({string.Join(", ", cols)})
                            VALUES ({string.Join(", ", vals)})
                            ON CONFLICT ({pkClause})
                            DO UPDATE SET
                                {string.Join(", ", updates)};
                        ";

                        var affected = await DBcon.ExecuteAsync(upsertPriSql, normalized, transaction);
                        if (affected != 1)
                            throw new Exception($"SaveQueryExec ERROR: Primary UPSERT affected {affected} rows");
                    }
                }

                // 2. 詳細テーブルの反映（_w_detailTblList から _detailTableList へ順次コピー＆Upsert）
                if (_w_detailTblList != null && _w_detailTblList.Length > 0 &&
                    _detailTableList != null && _detailTableList.Length > 0) {

                    for (int i = 0; i < _w_detailTblList.Length; i++) {
                        if (i >= _detailTableList.Length) break;

                        var srcWorkTable = _w_detailTblList[i];
                        var dstTable = _detailTableList[i];

                        if (!_detailTblPKList.TryGetValue(dstTable, out var pkCols)) {
                            _detailTblPKList.TryGetValue(srcWorkTable, out pkCols);
                        }

                        if (pkCols == null)
                            throw new Exception($"SaveQueryExec ERROR: PK not defined for detail table '{dstTable}'");

                        // 親由来の PK のみ抽出
                        var parentPKs = pkCols.Where(pk => pk == TenantCodeCol || pk == IdColName).ToArray();

                        var dtlWhereClause = string.Join(" AND ",
                            parentPKs.Select(pk => $@"""{pk}"" = @{pk}")
                        );

                        string selectWorkDtlSql = $@"
                            SELECT * FROM ""{srcWorkTable}""
                            WHERE {dtlWhereClause};
                        ";

                        var dtlParam = new Dictionary<string, object>();
                        foreach (var pk in parentPKs) {
                            if (PrimaryPKDict.TryGetValue(pk, out var val) && val != null) {
                                dtlParam[pk] = val;
                            } else if (_rawData.TryGetValue(pk, out var rawVal) && rawVal != null) {
                                dtlParam[pk] = rawVal;
                            } else {
                                throw new Exception($"SaveQueryExec ERROR: Missing primary key value for detail '{pk}'");
                            }
                        }

                        // ワーク側の明細行を全取得
                        var rows = await DBcon.QueryAsync<dynamic>(
                            selectWorkDtlSql,
                            dtlParam,
                            transaction);

                        foreach (var rowObj in rows) {
                            var row = (IDictionary<string, object>)rowObj;

                            var normalizedDtl =
                                NxTypeMapper.ConvertRow(dstTable, row);

                            var dtlCols = new List<string>();
                            var dtlVals = new List<string>();
                            var dtlUpdates = new List<string>();

                            foreach (var kv in normalizedDtl) {
                                dtlCols.Add($@"""{kv.Key}""");
                                dtlVals.Add($@"@{kv.Key}");

                                // PK および update_at 以外のカラムを UPDATE 対象にする
                                if (!pkCols.Contains(kv.Key) && kv.Key != _updateAtCol) {
                                    dtlUpdates.Add($@"""{kv.Key}"" = EXCLUDED.""{kv.Key}""");
                                }
                            }

                            var pkClause = string.Join(", ", pkCols.Select(pk => $@"""{pk}"""));

                            // 本詳細テーブルへの Upsert 実行
                            string upsertDtlSql = $@"
                                INSERT INTO ""{dstTable}""
                                ({string.Join(", ", dtlCols)})
                                VALUES ({string.Join(", ", dtlVals)})
                                ON CONFLICT ({pkClause})
                                DO UPDATE SET
                                    {string.Join(", ", dtlUpdates)};
                            ";

                            var affected = await DBcon.ExecuteAsync(upsertDtlSql, normalizedDtl, transaction);
                            if (affected != 1)
                                throw new Exception($"SaveQueryExec ERROR: Detail UPSERT affected {affected} rows");
                        }
                    }
                }

                return true;
            } catch (Exception ex) {
                // 例外を握りつぶさずに上位へスローし、SaveAsync 側のトランザクションロールバックを確実にする
                throw new Exception($"SaveQueryExec failed: {ex.Message}", ex);
            }
        }

        // =======================================================
        // 正本 Raw に反映
        // =======================================================

        public void ApplyWorkingToRaw(Dictionary<string, object?> workingRaw)
        {
            _rawData.Clear();
            foreach (var kv in workingRaw)
                _rawData[kv.Key] = kv.Value;
        }



        public virtual async Task<LockStatus> _SetLockAsync() {

            return await WriteLockInfoAsync(true);
        }
        public virtual async Task<LockStatus> _SetUnLockAsync() {

            return await WriteLockInfoAsync(false);
        }
        // =======================================================
        //データロック状態設定メソッド。
        // ======================================================
        protected virtual async Task<LockStatus> SetLockStatusAsync(bool doLock) {
            var result = await WriteLockInfoAsync(doLock);

            var nowupdate_at = result.Locked_at ?? DateTime.MinValue;
            // UIへの反映
            _SetProperties(new Dictionary<string, object> {
                [LockedAtCol] = DateTime.UtcNow,
                [LockedByCol] = result.LockedByUserId
            });

            return result;
        }

        // ロック状態をDBに反映する
        // 保存時以外に呼んではいけない。
        public virtual async Task<LockStatus> _ReWriteLockInfoAsync(
                                                        IDbTransaction tran,
                                                        Guid? Locked_by,
                                                        DateTime? Locked_at) {
            LockStatus result;

            // ---- ロック情報が無い場合（ロック延命） ----
            if (Locked_by == null || Locked_at == null) {
                result = await WriteLockInfoAsync(true, tran);
            } else {
                // ---- ロック情報が既にある場合（ロック延命はしない） ----
                result = new LockStatus {
                    Exists = true,
                    lockReqUsr = CurrUsrID,
                    LockedByUserId = Locked_by,
                    Locked_at = Locked_at,
                    Update_at = Update_at,
                    LockTimeOutLimit = _lockTimeOutLimit,
                    HasError = false,
                    ErrorMessage = ""
                };

                // DBのロック情報を反映するためにプロパティ更新
                _SetProperties(new Dictionary<string, object> {
                    [LockedAtCol] = result.Locked_at,
                    [LockedByCol] = result.LockedByUserId
                });
            }

            // ---- UIへの反映（整合） ----
            _SetProperties(new Dictionary<string, object> {
                [LockedAtCol] = result.Locked_at,
                [LockedByCol] = result.LockedByUserId
            });

            return result;
        }



        //保存中にロック状態を更新する必要があるのでトランザクションを渡すが、基本は渡さない
        //また通常は呼び出してはいけない
        protected virtual async Task<LockStatus> WriteLockInfoAsync(bool doLock, IDbTransaction? tran = null) {
            var now = DateTime.UtcNow;
            var expiryTime = now - _lockTimeOutLimit;

            try {
                // ★ PK WHERE 句を動的生成
                var whereClause = string.Join(" AND ",
                    _primaryTblPKList.Select(pk => $@"""{pk}"" = @{pk}")
                );

                // ★ PrimaryPKDict をそのまま使う（最適解）
                var pkParams = new Dictionary<string, object>();
                foreach (var pk in _primaryTblPKList)
                    pkParams[pk] = PrimaryPKDict[pk]!;

                // ============================================================
                // ① doLock == true → ロック要求
                // ============================================================
                if (doLock) {
                    string sqlLock = $@"
                UPDATE ""{TblName}""
                SET ""{LockedByCol}"" = @userId,
                    ""{LockedAtCol}"" = @lockedAt
                WHERE {whereClause}
                  AND (
                        ""{LockedByCol}"" = @userId
                     OR ""{LockedAtCol}"" IS NULL
                     OR ""{LockedAtCol}"" < @expiryTime
                  );
            ";

                    var param = new Dictionary<string, object>(pkParams) {
                        ["userId"] = CurrUsrID,
                        ["lockedAt"] = now,
                        ["expiryTime"] = expiryTime
                    };

                    int affected = await DBcon.ExecuteAsync(sqlLock, param, transaction: tran);

                    if (affected > 0) {
                        return new LockStatus {
                            Exists = true,
                            lockReqUsr = CurrUsrID,
                            LockedByUserId = CurrUsrID,
                            Locked_at = now,
                            LockTimeOutLimit = _lockTimeOutLimit,
                            Update_at = Update_at,
                            HasError = false,
                            ErrorMessage = ""
                        };
                    }

                    // ---- ロック失敗 → 現在の状態を返す ----
                    var current = await LockedChkfromTblAsync();
                    current.lockReqUsr = CurrUsrID;

                    if (!current.Exists) {
                        current.HasError = false;
                        current.ErrorMessage = "";
                        return current;
                    }

                    if (current.IsLockedForEdit) {
                        current.HasError = true;
                        current.ErrorMessage = "他のユーザーがロック中です。";
                        return current;
                    }

                    current.HasError = true;
                    current.ErrorMessage = "ロック更新に失敗しました。";
                    return current;
                }

                // ============================================================
                // ② doLock == false → ロック解除要求
                // ============================================================
                string sqlUnlock = $@"
                                    UPDATE ""{TblName}""
                                    SET ""{LockedByCol}"" = NULL,
                                        ""{LockedAtCol}"" = NULL
                                    WHERE {whereClause}
                                      AND ""{LockedByCol}"" = @userId;
                                ";

                var unlockParam = new Dictionary<string, object>(pkParams) {
                    ["userId"] = CurrUsrID
                };

                await DBcon.ExecuteAsync(sqlUnlock, unlockParam, transaction: tran);

                return new LockStatus {
                    Exists = true,
                    LockedByUserId = null,
                    Locked_at = null,
                    Update_at = Update_at,
                    LockTimeOutLimit = _lockTimeOutLimit,
                    lockReqUsr = CurrUsrID,
                    HasError = false,
                    ErrorMessage = ""
                };
            } catch (Exception ex) {
                throw new Exception($"WriteLockInfoAsync ERROR: {ex.Message}", ex);
            }
        }



        //テーブルからロック情報を読み取って返す。ユーザー名はクライアントで取得して
        protected virtual async Task<LockStatus> LockedChkfromTblAsync() {
            try {
                // ★ PK WHERE 句を動的生成
                var whereClause = string.Join(" AND ",
                    _primaryTblPKList.Select(pk => $@"""{pk}"" = @{pk}")
                );

                string sql = $@"
                                SELECT
                                    ""{LockedAtCol}"",
                                    ""{LockedByCol}"" AS ""UserId"",
                                    ""{UpdateAtCol}""
                                FROM ""{TblName}""
                                WHERE {whereClause};
                            ";

                // ★ PrimaryPKDict をそのまま使う（最適解）
                var param = new Dictionary<string, object>();
                foreach (var pk in _primaryTblPKList)
                    param[pk] = PrimaryPKDict[pk]!;

                var result = await DBcon.QueryFirstOrDefaultAsync<dynamic>(sql, param);

                // ---- レコード無し（RecordNone） ----
                if (result == null) {
                    return new LockStatus {
                        Exists = false,
                        HasError = false,
                        ErrorMessage = "",
                        lockReqUsr = CurrUsrID,
                        LockTimeOutLimit = _lockTimeOutLimit
                    };
                }

                // ---- locked_by の GUID パース ----
                Guid lockedByRaw = Guid.Empty;
                if (result.UserId != null)
                    Guid.TryParse(result.UserId.ToString(), out lockedByRaw);

                var lockedAt = NxTypeMapper.Convert(TblName, LockedAtCol, (object?)result[LockedAtCol]) as DateTime?;
                var updateAt = NxTypeMapper.Convert(TblName, UpdateAtCol, (object?)result[UpdateAtCol]) as DateTime?;

                var lockSt = new LockStatus {
                    Exists = true,
                    LockedByUserId = lockedByRaw == Guid.Empty ? null : lockedByRaw,
                    Locked_at = lockedAt,
                    Update_at = updateAt,
                    lockReqUsr = CurrUsrID,
                    HasError = false,
                    ErrorMessage = "",
                    LockTimeOutLimit = _lockTimeOutLimit
                };

                _SetProperties(new Dictionary<string, object?> {      // 未ロックは NULL のまま
                    [LockedAtCol] = lockSt.Locked_at,
                    [LockedByCol] = lockSt.LockedByUserId
                });

                return lockSt;
            } catch (Exception ex) {
                throw new Exception($"LockedChkfromTblAsync ERROR: {ex.Message}", ex);
            }
        }
public virtual async Task<bool> ReceiveSqlExecAsync(
    string primarySql,
    List<(string TableName, string Sql)> detailSqls,
    TKey newId,
    IDbTransaction tran)
{
    try
    {
        // =====================================================
        // Primary
        // =====================================================

        var primaryObj =
            await DBcon.QueryFirstOrDefaultAsync<dynamic>(
                primarySql,
                transaction: tran);

        if (primaryObj == null)
            throw new Exception("Primary record not found.");

        var primary =
            new Dictionary<string, object?>(
                (IDictionary<string, object>)primaryObj);

        // PK差し替え
        primary[IdColName] = newId;
        primary[TenantCodeCol] = TenantCode;

        await InsertRowAsync(
            _primaryTableName,
            primary,
            tran);

        // =====================================================
        // Detail
        // =====================================================

        foreach (var (tableName, sql) in detailSqls)
        {
            var rows = await DBcon.QueryAsync<dynamic>(
                sql,
                transaction: tran);

            foreach (var rowObj in rows)
            {
                var row =
                    new Dictionary<string, object?>(
                        (IDictionary<string, object>)rowObj);

                // 親FK差し替え
                row[IdColName] = newId;
                row[TenantCodeCol] = TenantCode;

                // 必要なら派生クラスでDetailPK再採番
                CompleteReceivedDetailRow(
                    tableName,
                    row,
                    newId);

                await InsertRowAsync(
                    tableName,
                    row,
                    tran);
            }
        }

        // =====================================================
        // 自分自身を新PKへ切り替え
        // =====================================================

        DataID = newId;

        // DBから最新状態を読み込み
        await _UpdatePropertiesFromTblAsync(tran);

        return true;
    }
    catch (Exception ex)
    {
        throw new Exception(
            $"ReceiveSqlExecAsync ERROR: {ex.Message}",
            ex);
    }
}


public virtual (string PrimarySql, List<string> DetailSqls) SendData()
{
    // Primary PK WHERE
    var whereClause = string.Join(" AND ",
        _primaryTblPKList.Select(pk => $"\"{pk}\" = @{pk}")
    );

    // Primary のカラム一覧取得
    var priCols = NxTypeMapper.Current
        .GetColumns(_primaryTableName)
        .Where(c =>
            !c.Equals(LockedAtCol, StringComparison.OrdinalIgnoreCase) &&
            !c.Equals(LockedByCol, StringComparison.OrdinalIgnoreCase))
        .Select(c => $"\"{c}\"");

    string primarySql = $@"
        SELECT {string.Join(", ", priCols)}
        FROM ""{_primaryTableName}""
        WHERE {whereClause};
    ";

    var detailSqls = new List<string>();

    foreach (var dtl in _detailTableList)
    {
        if (!_detailTblPKList.TryGetValue(dtl, out var pkCols))
            continue;

        var parentPKs = pkCols
            .Where(pk => PrimaryPKDict.ContainsKey(pk))
            .ToArray();

        var detailWhere = string.Join(" AND ",
            parentPKs.Select(pk => $"\"{pk}\" = @{pk}")
        );

        detailSqls.Add($@"
            SELECT *
            FROM ""{dtl}""
            WHERE {detailWhere};
        ");
    }

    return (primarySql, detailSqls);
}

    }
}
