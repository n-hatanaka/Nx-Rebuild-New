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
using static Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal.PgTableValuedFunctionExpression;

namespace NxRebuild.Api.Controllers {

    [Authorize]//継承先のすべてのコントローラーを自動的に「ログイン必須」にする（継承先では書かなくていい）
    public class NxDataController<T, TKey> : ControllerBase where T : BaseDataObj<TKey>  {
        protected readonly IDbConnection _db;
        protected readonly UserManager<ApplicationUser> _userMgr;
        protected string _tableName;
        protected string _nameColName; //テーブルのデータ名カラムのカラム名
        protected string _idColName;//テーブルのIDカラムのカラム名
        protected ApplicationUser _user;
        protected string _userID;
        protected string _userGroup;
        protected IBaseDataObjMgr<BaseDataObj<TKey>,TKey> _dataObjMgr;

        public NxDataController(IDbConnection dbConnection, UserManager<ApplicationUser> userManager) {
            _db = dbConnection;
            SetUserInfo(userManager);

            //派生先はここで_tableName等の基本情報を設定する
            //_dataObjMgrもここで生成する。

        }

        protected async Task SetUserInfo(UserManager<ApplicationUser> userManager) {
            _user = await userManager.GetUserAsync(User);
            _userID = _user.Id;
            _userGroup = _userGroup;

        }


        [HttpPost("ReName/{dataId}/{newName}")]
        public async Task<IActionResult> Rename(TKey dataId, string newName) {
            // ① DataObj を取得
            var dataObj = _dataObjMgr.DataList
                .FirstOrDefault(d => d.DataID.Equals(dataId)) as BaseDataObj<TKey>;

            if (dataObj == null)
                return BadRequest("Data not found");

            // ② ロック要求
            var lockst = new LockStatus { IsLocked = true, LockedByUserId = _userID };
            await dataObj.SetLockAsync(lockst);

            // ③ ロック確認
            if (!lockst.IsLocked || lockst.LockedByUserId != _userID)
                return BadRequest("Lock failed");

            // ④ 名前変更
            var renamed = await dataObj.ReName(newName);

            // ⑤ ロック解除
            lockst = new LockStatus {
                IsLocked = false,
                LockedByUserId = null
            };
            await dataObj.SetLockAsync(lockst);

            // ⑥ 結果返却
            if (renamed)
                return Ok(true);

            return BadRequest("Rename failed");
        }


    }
}
