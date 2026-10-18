using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NxRebuild.Api.Models;
using NxRebuild.Api.Schema;
using NxRebuild.shared;
using System.Data;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using static Dapper.SqlMapper;


namespace NxRebuild.Api.Controllers {

    [ApiController]
    [Route("NutProperty")]
    public class NutritionPropertiesController : NxDataController<NutritionProperty, int> {
        public NutritionPropertiesController(
            IConfiguration config,
            UserManager<ApplicationUser> userManager,
            IDatabaseSchemaProvider schemaProvider)
            : base(config, userManager, schemaProvider) {
            //_tblName = "ColName";
            //_nameColName = "Name";


        }
        protected override async Task CreateObjMgrAsync(RecordQuery query)
        {
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
            _dataObjMgr = new NutritionPropertiesMgr(_db, _tenantCode, uid);
        
            // DataObjMgr の初期化
            await _dataObjMgr.InitializeAsync(query);
        
            await Task.CompletedTask;
        }

        [HttpPost("Visible/{dataId}/{newVal}")]
        public async Task<IActionResult> VisibleChg(int dataId, bool newVal) {
            // ObjMgr 初期化
            await CreateObjMgrAsync(new RecordQuery { TargetIds = { dataId } });

            var mgr = _dataObjMgr as NutritionPropertiesMgr;
            var target = mgr?.Get(dataId) as NutritionProperty;

            if (target == null) {
                return Ok(new NxOperationResult<int> {
                    ValidationErrorCode = NxValidationErrorCode.Unknown,
                    Message = "Data not found"
                });
            }

            // WorkingRaw 作成
            var raw = new Dictionary<string, object?>();
            target.CreateWorkingMemory(raw);

            // Visible を変更
            raw["Visible"] = newVal;

            // SaveAsync（NxOperationResult を返す）
            var result = await target.SaveAsync(raw);

            // API 世界線の出口は IActionResult → JSON にするだけ
            return Ok(result);
        }


        [HttpGet("sync")]
        public async Task<IActionResult> SyncAll() {
            // DataObjMgr を生成
            await CreateObjMgrAsync(null);
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
        public override async Task<IActionResult> Delete(
            [FromBody] List<int> dataLst,
            [FromQuery] bool softDelete = false)   // ★ デフォルト false)
        {
            //無効化
            return Ok();
        }


        [HttpPost("ReName/{dataId}/{newName}")]
        public override async Task<IActionResult> ReName(int dataId, string newName) {
            //無効化
            return Ok();
        }
    }
}