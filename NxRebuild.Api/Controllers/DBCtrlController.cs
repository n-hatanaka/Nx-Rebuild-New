using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NxRebuild.shared;
using System.Data;
using static Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal.PgTableValuedFunctionExpression;

namespace NxRebuild.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GetScmController : ControllerBase
    {
        private readonly IDbConnection _dbConnection;

        public GetScmController(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        // -------------------------------------------------------------
        //指定されたテーブル名のデータを丸ごとJSONで返す
        // -------------------------------------------------------------
        [HttpGet("data/{tableName}/{group_id}")]
        public async Task<IActionResult> GetTableData(string tableName, string group_id)
        {
            // セキュリティ対策：怪しいテーブル名は弾く（英数字とアンダースコアのみ許可）
            if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[a-zA-Z0-9_]+$"))
            {
                return BadRequest("不正なテーブル名です。");
            }

            if (string.IsNullOrEmpty(group_id)) {
                return BadRequest("不正なIDです。");
            }

            try
            {
                var sql = @"SELECT EXISTS (
                                SELECT 1
                                FROM information_schema.columns
                                WHERE table_name = @TableName
                                  AND column_name = 'Group_ID' 
                                  AND table_schema = 'public'
                            );";

                var IsGrouping = _dbConnection.ExecuteScalar<bool>(sql, new { TableName = tableName});
        

                // 指定されたテーブルから「SELECT *」で全データを取得
                // Dapperの「dynamic（動的）」型で読み出します
                if (IsGrouping)                    
                    sql = $"SELECT * FROM \"{tableName}\" WHERE Group_ID =\"{group_id}\"";
                else
                    sql = $"SELECT * FROM \"{tableName}\"";
                IEnumerable<dynamic> data = await _dbConnection.QueryAsync(sql);

                // そのままJSONとしてクライアントへ返却
                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"データ取得に失敗しました ({tableName}): {ex.Message}");
            }
        }

        // -------------------------------------------------------------
        // APIエンドポイント: 型変換したスキーマ情報をJSONで返す
        // -------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                // 1. スキーマ情報と外部キー情報を両方取得する
                List<TableSchema> schemas = await GetAllTableSchemasAsync();
                var allForeignKeys = await GetForeignKeysInternalAsync();

                var convertedResult = new List<ConvertedTableSchema>();

                // 2. テーブルごとにループ
                foreach (var schema in schemas)
                {
                    var table = new ConvertedTableSchema
                    {
                        TableName = schema.TableName
                    };

                    // 3. カラムの型変換
                    foreach (var col in schema.Columns)
                    {
                        var convertedCol = new ConvertedColumnInfo
                        {
                            ColumnName = col.ColumnName,
                            PostgresType = col.DataType,
                            IsPrimaryKey = col.IsPrivaryKey,
                            SqliteType = MapPostgresToSqlite(col.DataType)
                        };
                        table.Columns.Add(convertedCol);
                    }

                    // 4. ★追加：このテーブル（TableName）に紐づく外部キーだけを抽出してセットする
                    foreach (var fk in allForeignKeys)
                    {
                        if (fk.FromTable == schema.TableName)
                        {
                            table.ForeignKeys.Add(new ForeignKeyInfo
                            {
                                FromColumn = fk.FromColumn,
                                ToTable = fk.ToTable,
                                ToColumn = fk.ToColumn
                            });
                        }
                    }

                    convertedResult.Add(table);
                }

                return Ok(convertedResult);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"スキーマ・リレーションの取得に失敗しました: {ex.Message}");
            }
        }
        // PostgreSQLから全ての外部キー制約をぶっこ抜く関数
        private async Task<IEnumerable<dynamic>> GetForeignKeysInternalAsync()
        {
            // PostgreSQLのシステムテーブルから外部キー情報を取得する複雑なSQL
            var sql = @"
                        SELECT
                            kcu.table_name AS FromTable,
                            kcu.column_name AS FromColumn,
                            ccu.table_name AS ToTable,
                            ccu.column_name AS ToColumn
                        FROM
                            information_schema.table_constraints AS tc
                            JOIN information_schema.key_column_usage AS kcu
                              ON tc.constraint_name = kcu.constraint_name
                              AND tc.table_schema = kcu.table_schema
                            JOIN information_schema.constraint_column_usage AS ccu
                              ON ccu.constraint_name = tc.constraint_name
                              AND ccu.table_schema = tc.table_schema
                        WHERE tc.constraint_type = 'FOREIGN KEY'
                          AND tc.table_schema = 'public';";

            return await _dbConnection.QueryAsync(sql);
        }
        // -------------------------------------------------------------
        // PostgreSQL のデータ型を SQLite のデータ型に変換する静的関数
        // -------------------------------------------------------------
        public static string MapPostgresToSqlite(string pgDataType)
        {
            // PostgreSQLの型名を小文字にして判定
            return pgDataType.ToLower() switch
            {
                // ① 整数・連番・真偽値はすべて INTEGER に丸める
                "integer" or "bigint" or "smallint" or "serial" or "bigserial" => "INTEGER",
                "boolean" => "INTEGER", // SQLiteにbool型はないので 0 か 1 で管理

                // ② 小数点・通貨・精密な数値は REAL（または NUMERIC 属性）
                "numeric" or "decimal" or "double precision" or "real" => "REAL",

                // ③ 文字列・コード類はすべて TEXT
                "character varying" or "varchar" or "text" or "char" or "character" => "TEXT",

                // ④ ★鬼門の日付型も、SQLiteでは TEXT（または INTEGER）として扱うのが鉄板
                "timestamp" or "timestamp without time zone" or "date" or "time" => "TEXT",

                // ⑤ バイナリは BLOB
                "bytea" => "BLOB",

                // どれにも当てはまらない場合は TEXT にしておけば安全
                _ => "TEXT"
            };
        }

        private async Task<IEnumerable<string>> GetTableNames()
        {
            // PostgreSQLからテーブル一覧（物理テーブルのみ）を取得するSQL文
            var sql = @"
                    SELECT table_name 
                    FROM information_schema.tables 
                    WHERE table_schema = 'public' 
                      AND table_type = 'BASE TABLE'
                    ORDER BY table_name;";

            try
            {
                // Dapperでクエリを実行し、文字列のリストとして受け取り
                return await _dbConnection.QueryAsync<string>(sql);
            }
            catch (Exception ex)
            {
                // 呼び出し元の関数へそのまま投げます（throw）。
                throw new Exception($"データベースからの取得に失敗しました: {ex.Message}", ex);
            }
        }

        // -------------------------------------------------------------
        // 1つのテーブル名からスキーマ（列情報など）を取得する関数
        // -------------------------------------------------------------
        private async Task<TableSchema> GetTableSchemaAsync(string tableName)
        {
            // 指定されたテーブルのカラム名とデータ型を取得するSQL
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
                        ORDER BY c.ordinal_position;";

            try
            {
                // SQLのプレースホルダー（@TableName）に、引数の tableName を安全に渡します
                var columns = await _dbConnection.QueryAsync<ColumnInfo>(sql, new { TableName = tableName });

                // 取得したカラム情報を含めた TableSchema オブジェクトを作成して返す
                return new TableSchema
                {
                    TableName = tableName,
                    Columns = columns
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"テーブル {tableName} のスキーマ取得に失敗しました: {ex.Message}", ex);
            }

        }
        private async Task<List<TableSchema>> GetAllTableSchemasAsync()
        {
            // 1. まずテーブル名の一覧を非同期で取得
            IEnumerable<string> tableNames = await GetTableNames();

            var allSchemas = new List<TableSchema>();

            // 2. ループで1つずつテーブル名を取り出し、スキーマ取得関数に渡す
            foreach (var tableName in tableNames)
            {
                // ★ ループ内で await することで、1つの処理が終わるのを待ってから次のループに進みます
                TableSchema schema = await GetTableSchemaAsync(tableName);

                allSchemas.Add(schema);
            }

            // すべてのスキーマ情報をまとめたリストを返す
            return allSchemas;
        }



    }


    public class TableSchema
    {
        public string TableName { get; set; } = string.Empty;
        // ここにカラム情報など、スキーマのデータを定義します
        public IEnumerable<ColumnInfo> Columns { get; set; } = Enumerable.Empty<ColumnInfo>();
    }
    public class ColumnInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;

        public bool IsPrivaryKey { get; set; } =false;
    }

    //// 変換後のテーブル情報クラス
    //public class ConvertedTableSchema
    //{
    //    public string TableName { get; set; } = string.Empty;
    //    public List<ConvertedColumnInfo> Columns { get; set; } = new();
    //}

    //// 変換後のカラム情報クラス
    //public class ConvertedColumnInfo
    //{
    //    public string ColumnName { get; set; } = string.Empty;
    //    public string PostgresType { get; set; } = string.Empty;
    //    public string SqliteType { get; set; } = string.Empty;
    //}

}
