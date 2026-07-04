using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NxRebuild.Api.Models;
using NxRebuild.shared;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using static Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal.PgTableValuedFunctionExpression;

namespace NxRebuild.Api.Controllers {
    [ApiController]
    [Route("Z_mst/[controller]")]
    public class ZmstController : AppDataController {
        public ZmstController(IDbConnection dbConnection, UserManager<ApplicationUser> userManager)
            : base(dbConnection, userManager) {
            this._tableName = "Zmst";
            this._nameColName = "Z_name";
            this._idColName = "LocalCode";

        }

        [HttpGet("DataOpen/{dataId}/{updateAt}")]
        public override async Task<IActionResult> DataOpen(string dataId, DateTime updateAt) {
            // SQL: グループとIDが一致し、かつ指定日時より新しいレコードを検索
            // 1件もヒットしなければ「更新なし」とみなす
            string sql = @"
            SELECT * FROM Z_mst 
            WHERE Group_Id = @GroupId 
            AND LocalCode = @DataId 
            AND Update_at > @UpdateAt";

            // Dapperでクエリ実行
            var result = await _db.QueryFirstOrDefaultAsync<dynamic>(sql, new {
                GroupId = _userGroup,
                DataId = dataId,
                UpdateAt = updateAt
            });


            //ロック情報書き込み
            //
            //データが存在しなくてもこの処理は置いとく
            LockStatus lockSt = await this.LockData(dataId);

            if ((lockSt.IsLocked) && (lockSt.LockedByUserId == _userID)) {
                //ロック成功
                if (result == null)
                    //更新データなし
                    return Ok();
                else
                    //更新データあり
                    return Ok(result);
            } else {
                return Conflict("このデータは現在他のユーザーによってロックされています。");
            }



        }

    }
}