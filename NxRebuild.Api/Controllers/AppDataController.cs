using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NxRebuild.Api.Models;
using NxRebuild.shared;
using System.Data;
using System.Diagnostics.Contracts;
using static Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal.PgTableValuedFunctionExpression;


namespace NxRebuild.Api.Controllers {

    [Authorize]//継承先のすべてのコントローラーを自動的に「ログイン必須」にする（継承先では書かなくていい）
    public abstract class AppDataController : ControllerBase {
        protected readonly IDbConnection _db;
        protected readonly UserManager<ApplicationUser> _userMgr;
        protected string _tableName;
        protected string _nameColName; //テーブルのデータ名カラムのカラム名
        protected string _idColName;//テーブルのIDカラムのカラム名
        protected ApplicationUser _user;
        protected string _userID;
        protected string _userGroup;

        public AppDataController(IDbConnection dbConnection, UserManager<ApplicationUser> userManager) {
            _db = dbConnection;
            SetUserInfo(userManager);

        }

        protected async Task SetUserInfo(UserManager<ApplicationUser> userManager) {
            _user = await userManager.GetUserAsync(User);
            _userID = _user.Id;
            _userGroup = _userGroup;

        }

        //指定されたデータが編集中か確認し。同時に編集中のユーザーと、更新日時を返す。

        [HttpGet("IsLocked/{dataID}")]
        public async Task<LockStatus> GetLockStatusAsync(string dataID) {
            // SQLでlocked_atとlocked_byの両方を取得
            var sql = $@"SELECT locked_at, locked_by as UserId, Update_at  
                 FROM {_tableName} 
                 WHERE group_code = @groupCode AND {_idColName} = @dataID";

            var result = await _db.QueryFirstOrDefaultAsync<dynamic>(sql, new { _userGroup, dataID });

            //ロックされていない場合
            if (result == null || result.locked_at == null)
                return new LockStatus { 
                        IsLocked = false, 
                        Update_at = (DateTime)result.Update_at 
                };

            bool isLocked = (DateTime.UtcNow - (DateTime)result.locked_at).TotalMinutes < 10;

            if (!isLocked) return new LockStatus { IsLocked = false };

            // ロックされていたらユーザー名を取得
            string userName = await _userMgr.GetUserNameAsync(result.UserId);

            return new LockStatus {
                IsLocked = true,
                LockedByUserId = result.UserId,
                LockedByUserName = userName,
                Update_at = result.Update_at
            };
        }

        public abstract Task<IActionResult> DataOpen(string dataId, DateTime updateAt);

        [HttpPost("LockData/{dataId}")]
        public async Task<LockStatus> LockData(string dataId) {
            // 更新用SQL: 該当DataIDのレコードをロックする
            LockStatus Lockst = await GetLockStatusAsync(dataId);
            if (Lockst.IsLocked)
                return Lockst;

            string sql = $@"
                        UPDATE {this._tableName}  
                        SET locked_by = @UserId, 
                            locked_at = @LockedAt 
                        WHERE Data_ID = @DataId";

            // 3. Dapperで実行
            // Dapperは戻り値として「影響を受けた行数」を返すので、
            // 0なら「データが見つからなかった」と判断できます
            DateTime LockTime = DateTime.UtcNow; // サーバー基準の現在時刻
            int rowsAffected = await _db.ExecuteAsync(sql, new {
                UserId = _userID,
                LockedAt = LockTime,
                DataId = dataId
            });

            return Lockst;          

        }
    }
}
