using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    public class ZmstController : NxDataController<ZmstEntity, int> {
        protected override async Task CreateObjMgr() {
            await SetUserInfo();

            EnsureConnection(); // _db を初期化（NxDataController側で定義）

            Guid uid;

            if (_userID == "anonymous")
                uid = Guid.Empty; // anonymous の世界線では GUID を使わない
            else
                uid = Guid.Parse(_userID);

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

        [HttpPost("Save/{dataId}")]
        public async Task<IActionResult> Save(int dataId, [FromBody] string ZmstJson) {
            await CreateObjMgr();

            // 既存 or 新規オブジェクト取得
            var obj = _dataObjMgr.DataList.FirstOrDefault(d => d.DataID == dataId) as ZmstEntity
                      ?? _dataObjMgr.CreateNewDataObj(0);

            // ロック確認
            var lockst = new LockStatus { IsLocked = true, LockedByUserId = _userID };
            await obj.SetLockAsync(lockst);
            if (!lockst.IsLocked || lockst.LockedByUserId != _userID)
                return BadRequest("Lock failed");

            using var tran = _db.BeginTransaction();

            var SaveJson = ZmstJson;


            // ★ JSON を Dictionary に変換
            var workingRaw = JsonSerializer.Deserialize<Dictionary<string, object?>>(SaveJson);


            try {
                int realID = dataId;

                // ★ 新規なら ID 採番
                if (dataId == 0) {
                    realID = obj.GenerateDataID(tran);

                    // ★ JSON 内の ID を新IDに書き換え
                    workingRaw[obj.IdColName] = realID;

                    // ★ サブテーブルも書き換え（tan_m など）
                    if (workingRaw.ContainsKey("SubTables")) {
                        var subTables = workingRaw["SubTables"] as List<List<Dictionary<string, object?>>>;

                        if (subTables != null) {
                            foreach (var tbl in subTables) {
                                foreach (var row in tbl) {
                                    row["LocalCode"] = realID;
                                    row["tenant_code"] = obj.TenantCode;
                                }
                            }
                        }
                    }

                }
                var NowUpdate_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");

                workingRaw["update_at"] = NowUpdate_at;

                SaveJson = JsonSerializer.Serialize(workingRaw);

                // ★ 保存処理へ
                if (!await obj.JsonToTbl(SaveJson, tran)) {
                    tran.Rollback();
                    return BadRequest("JsonToTbl failed");
                }

                tran.Commit();

                return Ok(new { NewID = realID, UpdateAt = NowUpdate_at });
            } catch (Exception ex) {
                tran.Rollback();
                return BadRequest(ex.Message);
            }
        }

    }
}
