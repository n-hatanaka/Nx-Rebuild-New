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
    [Route("Zmst")]
    public class ZmstController : NxDataController<ZmstEntity, int> {
        protected override IsrvBaseDataObjMgr<ZmstEntity, int> CreateMgrInstance(Guid userId) {
            return new ZmstEntityMgr(_db, _tenantCode, userId);
        }
        public ZmstController(
            IConfiguration config,
            UserManager<ApplicationUser> userManager,
            IDatabaseSchemaProvider schemaProvider)
            : base(config, userManager, schemaProvider) 
        {

        }
        // =======================================================
        // 保存の時にDataIDを確定させる
        // =======================================================
        protected override int EnsureIDForSave(IBaseDataObj<int> obj, IDbTransaction tran) {
            return ((ZmstEntity)obj).GenerateDataID(tran);
        }

        //カタログに材料コードを追加する
        protected override void AddCatalogColumns(IBaseDataObj<int> obj, Dictionary<string, object?> row) {
            var zmst = (ZmstEntity)obj;
            row["Z_code"] = zmst.ZCode;
        }

    }
}
