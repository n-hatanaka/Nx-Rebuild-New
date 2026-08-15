using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NxRebuild.Api.Models;
using NxRebuild.shared;
using System.Data;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace NxRebuild.Api.Controllers {

    [ApiController]
    [Route("NutProperty")]
    public class NutritionPropertysController : NxDataController<NutritionProperty, int> {
        public NutritionPropertysController(IDbConnection dbConnection, UserManager<ApplicationUser> userManager)
            : base(dbConnection, userManager) {
            // 初期化ロジックをここに実装
            _tblName = "ColName";
            _nameColName = "Name";
            _idColName = "No";

        }

        protected override async Task CreateObjMgr() {

            _dataObjMgr = new NutritionPropertysMgr(_db, _tenantCode, Guid.Parse(_userID));
            await Task.CompletedTask;
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