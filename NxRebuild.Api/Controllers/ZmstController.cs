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
        protected override async Task CreateObjMgr() {
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

            await SetUserInfo(); // ユーザー情報を設定（_userID, _tenantCode, _userName）

            // DataObjMgr を生成
            _dataObjMgr = new ZmstEntityMgr(_db, _tenantCode, uid);
            
            // DataObjMgr の初期化
            await _dataObjMgr.Initialize();

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


        //[HttpPost("Save/{dataId}")]
        //public async Task<IActionResult> Save(int dataId, [FromBody] string ZmstJson) {
        //    await CreateObjMgr();

        //    // 既存 or 新規オブジェクト取得
        //    var obj = (ZmstEntity)_dataObjMgr.Get(dataId);

        //    if (obj == null) {
        //        obj = (ZmstEntity)_dataObjMgr.CreateNewDataObj(0);
        //    }


        //    // ロック確認
        //    var lockst = new LockStatus { IsLocked = true, LockedByUserId = _userID };
        //    var lockinfo = await obj.SetLockAsync(lockst);
        //    if (!lockinfo.IsLocked || lockinfo.LockedByUserId != _userID)
        //        return BadRequest("Lock failed");

        //    using var tran = _db.BeginTransaction();

        //    var SaveJson = ZmstJson;


        //    // ★ JSON を Dictionary に変換
        //    var workingRaw = JsonSerializer.Deserialize<List<TableJson>>(SaveJson);


        //    var zmstTable = workingRaw.First(t => t.Table == this._tblName);
        //    var zmstRow = zmstTable.Rows.First();

        //    try {
        //        int realID = dataId;

        //        // ★ 新規なら ID 採番
        //        if (dataId == 0) {
        //            realID = ((ZmstEntity)obj).GenerateDataID(tran);

        //            zmstRow[obj.IdColName] = realID;

        //            // サブテーブルも書き換え
        //            foreach (var tbl in workingRaw.Where(t => t.Table != this._tblName)) {
        //                foreach (var row in tbl.Rows) {
        //                    row[obj.IdColName] = realID;
        //                    row["tenant_code"] = obj.TenantCode;
        //                }
        //            }
        //        }

        //        var NowUpdate_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");

        //        zmstRow["Update_at"] = NowUpdate_at;

        //        SaveJson = JsonSerializer.Serialize(workingRaw);

        //        // ★ 保存処理へ
        //        if (!await obj.JsonToTbl(SaveJson, tran)) {
        //            tran.Rollback();
        //            return BadRequest("JsonToTbl failed");
        //        }

        //        //ロック情報がJSONで更新されてしまうのであらためて設定
        //        await obj.SetLockAsync(lockst, tran);

        //        tran.Commit();

        //        return Ok(new { NewID = realID, UpdateAt = NowUpdate_at });
        //    } catch (Exception ex) {
        //        tran.Rollback();
        //        return BadRequest(ex.Message);
        //    }
        //}

    }
}
