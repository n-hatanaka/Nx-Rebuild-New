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
        protected override async Task CreateObjMgr(RecordQuery q) {

            await SetUserInfo();

            EnsureConnection(); // _db を初期化（NxDataController側で定義）

            Guid uid;

            if (_userID == null || _userID == Guid.Empty) {
                uid = Guid.Empty;   // anonymous 世界線
            } else {
                uid = _userID.Value; // Guid? → Guid
            }

            // API初期化（共通化済み）
            await InitializeNxApi();


            // DataObjMgr を生成
            _dataObjMgr = new ZmstEntityMgr(_db, _tenantCode, uid);
            
            // DataObjMgr の初期化
            await _dataObjMgr.Initialize(q);

            await Task.CompletedTask;
        }

        public ZmstController(
            IConfiguration config,
            UserManager<ApplicationUser> userManager,
            IDatabaseSchemaProvider schemaProvider)
            : base(config, userManager, schemaProvider) 
        {
            _tblName = "Zmst";
            _nameColName = "name";
            _idColName = "id";
        }
        // =======================================================
        // 保存の時にDataIDを確定させる
        // =======================================================
        protected override int EnsureIDForSave(IBaseDataObj<int> obj, IDbTransaction tran) {
            return ((ZmstEntity)obj).GenerateDataID(tran);
        }



    }
}
