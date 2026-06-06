using Dapper;
using Microsoft.Data.Sqlite;
using NxRebuild.shared;
using System.Data;
using System.Data.Common;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Text.Json;
using static MudBlazor.CategoryTypes;

namespace NxRebuild.Client.Pages.NxPrograms.DB{
    public class ColNameRecord {
        public Guid Group_ID { get; set; }
        public int No { get; set; }
        public int SortNo { get; set; }
        public string Col { get; set; }
        public string Name { get; set; }
        public string Format { get; set; }
        public int Digit { get; set; }
        public bool Visible { get; set; }
        public bool Nutrition { get; set; }
        public string PName1 { get; set; }
        public string PName2 { get; set; }
        public string PName3 { get; set; }
        public string PTanni { get; set; }
    }

    public class InMemoryDatabaseState : IDisposable{
        private IDbConnection? _connection;

        // 他のコンポーネントがこのプロパティを介してDBにアクセスします
        public IDbConnection? Connection => _connection;

        // すでにDBが初期化されているかどうかのフラグ
        public bool IsInitialized => _connection != null;

        public List<ColNameRecord> NutritionPropertys { get; set; }

        /// <summary>
        /// JSONスキーマを基にインメモリDBを初期化します。すでに初期化済みの場合は何もしません。
        /// </summary>
        public void Initialize(string jsonSchema)
        {
            if (IsInitialized) return;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // DTOの変更に合わせて TableSchemaDto の中に ForeignKeys プロパティを追加してください
            var schemas = JsonSerializer.Deserialize<List<ConvertedTableSchema>>(jsonSchema, options);

            if (schemas == null) throw new ArgumentException("JSONの解析に失敗しました。");

            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            try
            {
                // ★SQLiteで外部キー制約（リレーション）を有効化するためのコマンドを実行
                conn.Execute("PRAGMA foreign_keys = ON;");

                foreach (var table in schemas)
                {
                    var columnDefs = new List<string>();
                    foreach (var col in table.Columns)
                    {
                        columnDefs.Add($"\"{col.ColumnName}\" {col.SqliteType}");                        
                    }


                    var pkeyDefs = new List<string>();
                    foreach (var col in table.Columns)
                    {
                        if (col.IsPrimaryKey) 
                            pkeyDefs.Add($"\"{col.ColumnName}");
                    }

                    if (pkeyDefs.Count > 0) {
                        var pkSql = string.Join(", ", pkeyDefs);
                        columnDefs.Add($"PRIMARY KEY (\"{pkSql}\")");
                    }
                    
                    // 外部キー（リレーション）定義をSQLに組み込む
                    foreach (var fk in table.ForeignKeys)
                    {
                        // 例: FOREIGN KEY("user_id") REFERENCES "users"("id")
                        columnDefs.Add($"FOREIGN KEY(\"{fk.FromColumn}\") REFERENCES \"{fk.ToTable}\"(\"{fk.ToColumn}\")");
                    }

                    var columnsSql = string.Join(", ", columnDefs);
                    var createTableSql = $"CREATE TABLE \"{table.TableName}\" ({columnsSql});";

                    conn.Execute(createTableSql);
                }

                _connection = conn;
            }
            catch
            {
                conn.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 指定されたマスタテーブルのデータをサーバーから取得し、ローカルのインメモリDBに丸ごとコピー。
        /// </summary>
        public async Task SyncMasterDataAsync(HttpClient http, List<string> masterTableNames, string groupCode)
        {
            if (_connection == null) throw new InvalidOperationException("DBが初期化されていません。");

            foreach (var tableName in masterTableNames){
                // 1. サーバーから、指定テーブルの全データを「辞書のリスト」として取得する
                // (カラム名 -> 値 のマップのリストになります)
                var rows = await http.GetFromJsonAsync<List<Dictionary<string, object>>>($"api/GetScm/data/{tableName}/{groupCode}");
                if (rows == null || rows.Count == 0) continue;

                InsertMaster(tableName, rows);

            }
        }

        private void InsertMaster(string tableName, List<Dictionary<string, object>> rows) {

            _connection.Execute("PRAGMA foreign_keys = OFF;");
            try {
                // 2. 1行目のデータから、動的に INSERT SQL を組み立てる
                // 例: INSERT INTO "users" ("id", "name") VALUES (@id, @name);
                var firstRow = rows[0];
                var columnNames = string.Join(", ", firstRow.Keys.Select(k => $"\"{k}\""));
                var paramNames = string.Join(", ", firstRow.Keys.Select(k => $"@{k}"));

                var insertSql = $"INSERT INTO \"{tableName}\" ({columnNames}) VALUES ({paramNames});";

                // 3. トランザクションをかけて、Dapperで高速に一括挿入する
                using (var transaction = _connection.BeginTransaction()) {
                    foreach (var row in rows) {

                        // ★【ここを追加】：辞書の中の JsonElement を生のデータ型に変換（アンパック）する
                        var unpackedRow = row.ToDictionary(
                            kvp => kvp.Key,
                            kvp => UnpackJsonValue(kvp.Value)
                        );
                        // ★Dapperの超強力な仕様：
                        // パラメータとして Dictionary<string, object> を直接渡すと、
                        // @key の部分を 辞書のキーと自動でマッピングして実行してくれます！
                        _connection.Execute(insertSql, unpackedRow, transaction);
                    }
                    transaction.Commit();
                }
                Console.WriteLine($"[Sync] {tableName} テーブル: {rows.Count} 件の同期に成功しました。");
                return;

            } catch (Exception ex) {
                Console.WriteLine($"[Sync Error] {tableName} の同期に失敗: {ex.Message}");
            } finally {
                _connection.Execute("PRAGMA foreign_keys = ON;");
            }
            return;
        }


        public void  SyncNutritionPropertys() {
            const string sql = "SELECT * FROM ColName";
            NutritionPropertys = _connection.Query<ColNameRecord>(sql).ToList();
            return;
        }

        // -------------------------------------------------------------
        // JsonElement を C# の「生データ型」に変換するヘルパー関数
        // -------------------------------------------------------------
        private object? UnpackJsonValue(object? value)
        {
            if (value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        return element.GetString();

                    case JsonValueKind.Number:
                        // 整数なら long、小数なら double として取り出す
                        if (element.TryGetInt64(out long l)) return l;
                        return element.GetDouble();

                    case JsonValueKind.True:
                        return true;

                    case JsonValueKind.False:
                        return false;

                    case JsonValueKind.Null:
                        return null;

                    default:
                        return element.GetRawText();
                }
            }
            return value;
        }
        // アプリ終了時に接続を破棄する処理
        public void Dispose()
            {
                _connection?.Dispose();
                _connection = null;
            }
        }

    }