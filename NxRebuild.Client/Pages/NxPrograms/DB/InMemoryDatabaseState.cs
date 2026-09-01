using Dapper;
using Microsoft.Data.Sqlite;
using NxRebuild.shared;
using System.Data;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public class InMemoryDatabaseState : IDisposable {
        private IDbConnection? _connection;

        // 他のコンポーネントがこのプロパティを介してDBにアクセスします
        public IDbConnection? Connection => _connection;

        // すでにDBが初期化されているかどうかのフラグ
        public bool IsInitialized => _connection != null;


        /// <summary>
        /// JSONスキーマを基にインメモリDBを初期化します。すでに初期化済みの場合は何もしません。
        /// </summary>
        public void Initialize(string jsonSchema) {
            if (IsInitialized) return;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // ① JSON → スキーマ DTO に変換
            var schemas = JsonSerializer.Deserialize<List<ConvertedTableSchema>>(jsonSchema, options);
            if (schemas == null) throw new ArgumentException("JSONの解析に失敗しました。");


            // ---------------------------------------------------------
            // ② スキーマ → 型マップ正本を生成
            //    → 全テーブル・全カラムの CsType がここで決まる
            // ---------------------------------------------------------
            var typeMap = NxTypeMapBuilder.FromSchemas(schemas);

            // ---------------------------------------------------------
            // ③ 世界線に型の正本をロードする
            //    → BaseDataObj / InsertMaster / API が全部この型を使う
            // ---------------------------------------------------------
            NxTypeMapper.Set(typeMap);

            // ---------------------------------------------------------
            // ④ SQLite インメモリ DB を作成
            // ---------------------------------------------------------
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            try {
                // ★SQLiteで外部キー制約（リレーション）を有効化するためのコマンドを実行
                conn.Execute("PRAGMA foreign_keys = ON;");

                foreach (var table in schemas) {
                    var columnDefs = new List<string>();

                    // カラム定義
                    foreach (var col in table.Columns) {
                        columnDefs.Add($"\"{col.ColumnName}\" {col.SqliteType}");
                    }

                    // 主キー定義
                    var pkeyDefs = new List<string>();
                    foreach (var col in table.Columns) {
                        if (col.IsPrimaryKey)
                            pkeyDefs.Add($"\"{col.ColumnName}\"");
                    }

                    if (pkeyDefs.Count > 0) {
                        var pkSql = string.Join(", ", pkeyDefs);
                        columnDefs.Add($"PRIMARY KEY ({pkSql})");
                    }

                    // 外部キー定義
                    foreach (var fk in table.ForeignKeys) {
                        // 例: FOREIGN KEY("user_id") REFERENCES "users"("id")
                        columnDefs.Add(
                            $"FOREIGN KEY(\"{fk.FromColumn}\") REFERENCES \"{fk.ToTable}\"(\"{fk.ToColumn}\")"
                        );
                    }

                    var columnsSql = string.Join(", ", columnDefs);
                    var createTableSql = $"CREATE TABLE \"{table.TableName}\" ({columnsSql});";

                    conn.Execute(createTableSql);
                }

                _connection = conn;
            } catch {
                conn.Dispose();
                throw;
            }
        }


        /// <summary>
        /// 指定されたマスタテーブルのデータをサーバーから取得し、ローカルのインメモリDBに丸ごとコピー。
        /// </summary>
        public async Task SyncMasterDataAsync(HttpClient http, List<string> masterTableNames, string tenant_code) {
            if (_connection == null) throw new InvalidOperationException("DBが初期化されていません.");

            foreach (var tableName in masterTableNames) {
                // 1. サーバーから、指定テーブルの全データを「辞書のリスト」として取得する
                var rows = await http.GetFromJsonAsync<List<Dictionary<string, object>>>(
                    $"api/GetScm/data/{tableName}/{tenant_code}"
                );

                if (rows == null || rows.Count == 0) continue;

                InsertMaster(tableName, rows);
            }
        }


        private void InsertMaster(string tableName, List<Dictionary<string, object>> rows) {
            _connection.Execute("PRAGMA foreign_keys = OFF;");

            try {
                // 2. 1行目のデータから、動的に INSERT SQL を組み立てる
                var firstRow = rows[0];
                var columnNames = string.Join(", ", firstRow.Keys.Select(k => $"\"{k}\""));
                var paramNames = string.Join(", ", firstRow.Keys.Select(k => $"@{k}"));

                var insertSql = $"INSERT INTO \"{tableName}\" ({columnNames}) VALUES ({paramNames});";

                // 3. トランザクションをかけて、Dapperで高速に一括挿入する
                using (var transaction = _connection.BeginTransaction()) {
                    foreach (var row in rows) {
                        // ---------------------------------------------------------
                        // ★ NxTypeMapper による「正本型への矯正」
                        //   - JSON の Number(double/long) → 正しい型へ
                        //   - SQLite の INTEGER(long) → int/long に矯正
                        //   - datetime（マイクロ秒対応）もここで正しく変換
                        // ---------------------------------------------------------
                        var converted = NxTypeMapper.ConvertRow(tableName, row);

                        // ★Dapperの仕様：Dictionary をそのまま渡せば @key に自動マッピング
                        _connection.Execute(insertSql, converted, transaction);
                    }

                    transaction.Commit();
                }

                Console.WriteLine($"[Sync] {tableName} テーブル: {rows.Count} 件の同期に成功しました。");
            } catch (Exception ex) {
                Console.WriteLine($"[Sync Error] {tableName} の同期に失敗: {ex.Message}");
            } finally {
                _connection.Execute("PRAGMA foreign_keys = ON;");
            }
        }


        public void Dispose() {
            _connection?.Dispose();
            _connection = null;
        }

    }
}
