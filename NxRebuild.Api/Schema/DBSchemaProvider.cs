using Dapper;
using System.Data;
using NxRebuild.shared;

namespace NxRebuild.Api.Schema {
    public interface IDatabaseSchemaProvider {
        Task<List<ConvertedTableSchema>> GetSchemasAsync();
    }

    /// <summary>
    /// PostgreSQL からテーブル名・カラム情報・外部キー情報を取得し、
    /// NxRebuild.shared の DTO に詰めて返す純粋なスキーマプロバイダー。
    /// </summary>
    public class DatabaseSchemaProvider : IDatabaseSchemaProvider {
        private readonly IDbConnection _db;

        public DatabaseSchemaProvider(IDbConnection db) {
            _db = db;
        }

        public async Task<List<ConvertedTableSchema>> GetSchemasAsync() {
            var tableNames = await GetTableNamesAsync();
            var foreignKeys = await GetForeignKeysAsync();

            var result = new List<ConvertedTableSchema>();

            foreach (var tableName in tableNames) {
                var schema = await GetTableSchemaAsync(tableName);

                var converted = new ConvertedTableSchema {
                    TableName = tableName
                };

                // カラム情報
                foreach (var col in schema.Columns) {
                    converted.Columns.Add(new ConvertedColumnInfo {
                        ColumnName = col.ColumnName,
                        PostgresType = col.DataType,
                        SqliteType = "",          // SQLite 変換はクライアント側で行う
                        IsPrimaryKey = col.IsPrimaryKey
                    });
                }

                // 外部キー情報
                foreach (var fk in foreignKeys.Where(f => f.FromTable == tableName)) {
                    converted.ForeignKeys.Add(new ForeignKeyInfo {
                        FromColumn = fk.FromColumn,
                        ToTable = fk.ToTable,
                        ToColumn = fk.ToColumn
                    });
                }

                result.Add(converted);
            }

            return result;
        }

        // -------------------------------------------------------------
        // テーブル一覧取得
        // -------------------------------------------------------------
        private async Task<IEnumerable<string>> GetTableNamesAsync() {
            var sql = @"
                SELECT table_name
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE'
                ORDER BY table_name;
            ";

            return await _db.QueryAsync<string>(sql);
        }

        // -------------------------------------------------------------
        // カラム情報取得
        // -------------------------------------------------------------
        private async Task<TableSchemaRaw> GetTableSchemaAsync(string tableName) {
            var sql = @"
                SELECT 
                    c.column_name AS ColumnName,
                    c.data_type AS DataType,
                    (pk.column_name IS NOT NULL) AS IsPrimaryKey
                FROM information_schema.columns c
                LEFT JOIN (
                    SELECT kcu.column_name
                    FROM information_schema.table_constraints AS tc
                    JOIN information_schema.key_column_usage AS kcu
                      ON tc.constraint_name = kcu.constraint_name
                      AND tc.table_schema = kcu.table_schema
                    WHERE tc.constraint_type = 'PRIMARY KEY'
                      AND tc.table_name = @TableName
                      AND tc.table_schema = 'public'
                ) pk ON c.column_name = pk.column_name
                WHERE c.table_name = @TableName
                  AND c.table_schema = 'public'
                ORDER BY c.ordinal_position;
            ";

            var columns = await _db.QueryAsync<ColumnInfoRaw>(sql, new { TableName = tableName });

            return new TableSchemaRaw {
                TableName = tableName,
                Columns = columns.ToList()
            };
        }

        // -------------------------------------------------------------
        // 外部キー取得
        // -------------------------------------------------------------
        private async Task<IEnumerable<ForeignKeyRaw>> GetForeignKeysAsync() {
            var sql = @"
                SELECT
                    kcu.table_name AS FromTable,
                    kcu.column_name AS FromColumn,
                    ccu.table_name AS ToTable,
                    ccu.column_name AS ToColumn
                FROM information_schema.table_constraints AS tc
                JOIN information_schema.key_column_usage AS kcu
                  ON tc.constraint_name = kcu.constraint_name
                  AND tc.table_schema = kcu.table_schema
                JOIN information_schema.constraint_column_usage AS ccu
                  ON ccu.constraint_name = tc.constraint_name
                  AND ccu.table_schema = tc.table_schema
                WHERE tc.constraint_type = 'FOREIGN KEY'
                  AND tc.table_schema = 'public';
            ";

            return await _db.QueryAsync<ForeignKeyRaw>(sql);
        }
    }

    // -------------------------------------------------------------
    // 内部用 Raw DTO（Shared に入れない）
    // -------------------------------------------------------------
    public class TableSchemaRaw {
        public string TableName { get; set; } = string.Empty;
        public List<ColumnInfoRaw> Columns { get; set; } = new();
    }

    public class ColumnInfoRaw {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsPrimaryKey { get; set; }
    }

    public class ForeignKeyRaw {
        public string FromTable { get; set; } = string.Empty;
        public string FromColumn { get; set; } = string.Empty;
        public string ToTable { get; set; } = string.Empty;
        public string ToColumn { get; set; } = string.Empty;
    }
}
