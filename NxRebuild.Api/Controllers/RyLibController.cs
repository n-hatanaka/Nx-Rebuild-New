using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Npgsql;
using NxRebuild.Api.Models;
using NxRebuild.Api.Schema;
using NxRebuild.shared;
using System.Data;
using System.Diagnostics.Contracts;
using System.Net.Sockets;
using System.Text.Json;
using static Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal.PgTableValuedFunctionExpression;

namespace NxRebuild.Api.Controllers {

    [ApiController]
    [Route("RyLib")]
    public class RyLibController : NxDataController<RyLibEntity, Guid> {

        protected override async Task CreateObjMgrAsync(RecordQuery q) {

            await SetUserInfo();

            EnsureConnection(); // _db を初期化（NxDataController側で定義）

            Guid uid;

            if (_userID == null || _userID == Guid.Empty) {
                uid = Guid.Empty;   // anonymous 
            } else {
                uid = _userID.Value; // Guid? → Guid
            }

            // API初期化（共通化済み）
            await InitializeNxApi();


            // DataObjMgr を生成
            _dataObjMgr = new RyLibEntityMgr(_db, _tenantCode, uid);

            // DataObjMgr の初期化
            await _dataObjMgr.InitializeAsync(q);

            await Task.CompletedTask;
        }
        protected override async Task<NxOperationResult<Guid>> BeforeDeleteProcess(
        IBaseDataObj<Guid> obj, IDbTransaction tran) {
            if (obj.DataType == NxDataType.Folder) {
                var cnt = await _db.ExecuteScalarAsync<long>(
                    @"SELECT COUNT(*) FROM ""Ry_lib"" WHERE ""tenant_code"" = @t AND ""Parent"" = @id /* + 削除済みの除外条件 */",
                    new { t = obj.TenantCode, id = obj.DataID }, tran);
                if (cnt > 0)
                    return new NxOperationResult<Guid> {
                        ValidationErrorCode = NxValidationErrorCode.Unknown,
                        Message = "フォルダが空ではありません"
                    };
            }
            return await base.BeforeDeleteProcess(obj, tran);
        }

        public RyLibController(
            IConfiguration config,
            UserManager<ApplicationUser> userManager,
            IDatabaseSchemaProvider schemaProvider)
            : base(config, userManager, schemaProvider) {

        }
    }
}
