using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NxRebuild.Api.Models;
using NxRebuild.shared;
using NxRebuild.Api.Schema;
using System.Data;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;


namespace NxRebuild.Api.Controllers {

    [ApiController]
    [Route("NutProperty")]
    public class NutritionPropertiesController : NxDataController<NutritionProperty, int> {
        public NutritionPropertiesController(
            IConfiguration config,
            UserManager<ApplicationUser> userManager,
            IDatabaseSchemaProvider schemaProvider)
            : base(config, userManager, schemaProvider) {
            _tblName = "ColName";
            _nameColName = "Name";
            _idColName = "No";


        }
        protected override async Task CreateObjMgr()
        {
            await SetUserInfo();

            EnsureConnection(); // _db を初期化（NxDataController側で定義）

            Guid uid;
        
            if (_userID == "anonymous")
                uid = Guid.Empty; // anonymous の世界線では GUID を使わない
            else
                uid = Guid.Parse(_userID);

            // API初期化（共通化済み）
            await InitializeNxApi();            
        
            // DataObjMgr を生成
            _dataObjMgr = new NutritionPropertiesMgr(_db, _tenantCode, uid);
        
            // DataObjMgr の初期化
            await _dataObjMgr.Initialize();
        
            await Task.CompletedTask;
        }
        
        [HttpPost("Visible/{dataId}/{newVal}")]
        public async Task<IActionResult> VisibleChg(int dataId, bool newVal)
        {
            await CreateObjMgr();
            var mgr = _dataObjMgr as NutritionPropertiesMgr;
        
            var target = (mgr.DataList.FirstOrDefault(x => x.DataID == dataId) as NutritionProperty);
            if (target == null)
                return BadRequest("Data not found");
        
            // ① オブジェクトの状態を変える
            target.Visible = newVal;
        
            // ② SaveAsync（内部で SaveQueryExec + トランザクション）
            var ok = await target.SaveAsync();
            if (!ok)
                return StatusCode(500, "Update failed");
        
            // ③ 正本世界線の JSON を返す
            var json = target.TblToJson();
            return Ok(json);
        }

        [HttpGet("sync")]
        public async Task<IActionResult> SyncAll() {
            // DataObjMgr を生成
            await CreateObjMgr();
            var mgr = _dataObjMgr;

            var resultList = new List<object>();

            foreach (var obj in mgr.DataList) {
                // 全てのデータをJSonに変換
                var json = obj.TblToJson();

                resultList.Add(new {
                    Key = obj.DataID,
                    Data = json
                });
            }

            // ひとまとめにして返す
            return Ok(new {
                Refreshed_at = DateTime.UtcNow,
                Items = resultList
            });
        }


        [HttpPost("Delete")]
        public async Task<IActionResult> Delete([FromBody] List<int> dataLst) {
            //無効化
            return Ok();
        }


        [HttpPost("ReName/{dataId}/{tenantCode}/{newName}")]
        public async Task<IActionResult> Rename(int dataId, string newName) {
            //無効化
            return Ok();
        }
    }
}