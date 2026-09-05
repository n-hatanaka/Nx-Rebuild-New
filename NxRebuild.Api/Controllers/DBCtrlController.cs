using Dapper;
using Microsoft.AspNetCore.Mvc;
using NxRebuild.shared;
using NxRebuild.Api.Schema;
using Npgsql;
using Microsoft.AspNetCore.Authorization;

namespace NxRebuild.Api.Controllers {
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class GetScmController : ControllerBase {
        private readonly string _connectionString;
        private readonly IDatabaseSchemaProvider _schemaProvider;

        public GetScmController(IConfiguration config, IDatabaseSchemaProvider schemaProvider) {
            _connectionString = config.GetConnectionString("DefaultConnection");
            _schemaProvider = schemaProvider;
        }

        private NpgsqlConnection CreateConnection() {
            return new NpgsqlConnection(_connectionString);
        }

        // -------------------------------------------------------------
        // 指定されたテーブル名のデータを丸ごと返す（識別子クォート統一版）
        // -------------------------------------------------------------
        [HttpGet("data/{tableName}/{tenant_code}")]
        public async Task<IActionResult> GetTableData(string tableName, string tenant_code) {
            if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[a-zA-Z0-9_]+$"))
                return BadRequest("不正なテーブル名です。");

            if (string.IsNullOrEmpty(tenant_code))
                return BadRequest("不正なtenant_codeです。");

            try {
                using var con = CreateConnection();
                await con.OpenAsync();

                // information_schema は PostgreSQL のシステムビューなのでクォート不要
                var sql = @"
                    SELECT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = @TableName
                          AND column_name = 'tenant_code'
                          AND table_schema = 'public'
                    );
                ";

                var hasTenantCode = await con.ExecuteScalarAsync<bool>(sql, new { TableName = tableName });

                IEnumerable<dynamic> data;

                if (hasTenantCode) {
                    var guidValue = Guid.Parse(tenant_code);

                    sql = $@"
                        SELECT *
                        FROM ""{tableName}""
                        WHERE ""tenant_code"" = @tenant_code;
                    ";

                    data = await con.QueryAsync(sql, new { tenant_code = guidValue });
                } else {
                    sql = $@"
                        SELECT *
                        FROM ""{tableName}"";
                    ";

                    data = await con.QueryAsync(sql);
                }

                return Ok(data);
            } catch (Exception ex) {
                return StatusCode(500, $"データ取得に失敗しました ({tableName}): {ex.Message}");
            }
        }

        // -------------------------------------------------------------
        // PostgreSQL の生スキーマを Nx 用 DTO に変換して返す
        // -------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Get() {
            try {
                var schemas = await _schemaProvider.GetSchemasAsync();
                return Ok(schemas);
            } catch (Exception ex) {
                return StatusCode(500, $"スキーマ取得に失敗しました: {ex.Message}");
            }
        }
    }
}
