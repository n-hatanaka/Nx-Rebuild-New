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
using static Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal.PgTableValuedFunctionExpression;

namespace NxRebuild.Api.Controllers {

    [Authorize]//継承先のすべてのコントローラーを自動的に「ログイン必須」にする（継承先では書かなくていい）
    public abstract class NxDataController<T, TKey> : ControllerBase where T : BaseDataObj<TKey>, new() {
        protected readonly IDbConnection _db;
        protected readonly UserManager<ApplicationUser> _userMgr;
        //次の四つのメンバは初期化時にハードコード
        protected string _tblName;//メタデータのテーブル名
        protected string _nameColName; //テーブルのデータ名カラムのカラム名
        protected string _idColName;//テーブルのIDカラムのカラム名
        protected Guid _tenantCode;//テナントコード
        protected ApplicationUser _user;
        protected string _userID;
        protected string _usertenant_code;
        protected IsrvBaseDataObjMgr<T,TKey> _dataObjMgr;

     
        protected readonly IDatabaseSchemaProvider _schemaProvider;
    
    
        public NxDataController(
            IDbConnection dbConnection,
            UserManager<ApplicationUser> userManager,
            IDatabaseSchemaProvider schemaProvider)
        {
            _db = dbConnection;
            _userManager = userManager;
            _schemaProvider = schemaProvider;
        }
    
        protected async Task InitializeNxApi()
        {
            var schemas = await _schemaProvider.GetSchemasAsync();
            NxTypeMapper.Initialize(schemas);
        }
     
        public NxDataController(IDbConnection dbConnection, UserManager<ApplicationUser> userManager) {
            _db = dbConnection;
            _userMgr = userManager;
            //派生先はここで_tableName等の基本情報を設定する


        }

        protected async Task SetUserInfo() {
            _user = await _userMgr.GetUserAsync(User);

            if (_user == null) {
                // 匿名ユーザー扱い
                _userID = "anonymous";
                _usertenant_code = "00000000-0001-7000-8000-0000000000000";
                return;
            }

            _userID = _user.Id;
            _usertenant_code = _user.TenantCode;

        }

        //Httpエンドポイントで必ず呼び出す事。
        protected abstract Task CreateObjMgr();
        // { 派生先での実装例（_dataObjMgr の型は派生先に合わせて変更すること）
        //   ※ ほぼコピペで使えるが、各コントローラ固有の ObjMgr を new する点だけ注意
        //     // ★ユーザー情報を取得（JWT の tenant_code を含む）
        //     await SetUserInfo(_userMgr);　の呼び出し
        //     // ★ObjMgr を生成（Initialize は呼ばない）
        //     _dataObjMgr = new BaseDataObjMgr<T, TKey>(_db, Guid.Parse(tenantCode), userId);
        //
        //     // ★派生先で設定される基本情報をここで反映
        //     _dataObjMgr.TableName   = _tableName;
        //     _dataObjMgr.IdColName   = _idColName;
        //     _dataObjMgr.NameColName = _nameColName;
        //
        //     // ★DataList やテーブル情報を読み込む
        //     _dataObjMgr.Initialize();
        // }

        [HttpGet("sync/{refreshedAt}")]
        public async Task<IActionResult> SyncAll(DateTime refreshedAt)
        {
            // DataObjMgr を生成
            await CreateObjMgr(); 
            var mgr = _dataObjMgr;
        
            var resultList = new List<object>();
        
            foreach (var obj in mgr.DataList)
            {
                if ((obj.Update_at <= refreshedAt) && (obj.LockedAt <= refreshedAt))
                    continue;

                // クライアントの更新及びロック時刻よりあたらしいBaseDataObj<TKey>
                var json = obj.TblToJson();   
        
                resultList.Add(new {
                    Key = obj.DataID,
                    Data = json
                });
            }
        
            // ひとまとめにして返す
            return Ok(new {
                Refreshed_at = refreshedAt,
                Items = resultList
            });
        }

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete([FromBody] List<TKey> dataLst) {
            // ★ユーザー所属テナントで ObjMgr を生成
            await CreateObjMgr();

            var failedIds = new List<TKey>();

            foreach (var dataId in dataLst) {
                // ① DataObj を取得
                var dataObj = _dataObjMgr.DataList
                    .FirstOrDefault(d => d.DataID.Equals(dataId)) as BaseDataObj<TKey>;

                if (dataObj == null) {
                    failedIds.Add(dataId);
                    continue;
                }

                // ② ロック要求
                var lockst = new LockStatus { IsLocked = true, LockedByUserId = _userID };
                await dataObj.SetLockAsync(lockst);

                // ③ ロック確認
                if (!lockst.IsLocked || lockst.LockedByUserId != _userID) {
                    failedIds.Add(dataId);
                    continue;
                }

                // ④ 削除処理
                var transaction = _db.BeginTransaction();

                if (!(await dataObj.SoftDeleteQueryExec(transaction))) {
                    transaction.Rollback();

                    // ロック解除（失敗時も必ず）
                    await dataObj.SetLockAsync(new LockStatus {
                        IsLocked = false,
                        LockedByUserId = null
                    });

                    failedIds.Add(dataId);
                    continue;
                }

                // ⑤ 削除成功
                transaction.Commit();

                // DataList から削除
                _dataObjMgr.RemoveFromList(dataObj);

                // ⑥ ロック解除
                await dataObj.SetLockAsync(new LockStatus {
                    IsLocked = false,
                    LockedByUserId = null
                });
            }
            
            // ⑦ 結果返却（List<TKey> をそのまま返す）
            return Ok(failedIds);

        }




        [HttpPost("ReName/{dataId}/{tenantCode}/{newName}")]
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
            if (renamed) {
                return Ok(dataObj._rawData);
            }

            return BadRequest("Rename failed");
        }


    }
}
