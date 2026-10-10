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
using System.Text.Json;
using static Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal.PgTableValuedFunctionExpression;

namespace NxRebuild.Api.Controllers {

    //-------------------------------------------------------------------------
    [Authorize]//継承先のすべてのコントローラーを自動的に「ログイン必須」にする（継承先では書かなくていい）
    public abstract class NxDataController<T, TKey> : ControllerBase where T : BaseDataObj<TKey>, new() {
        private readonly string _connectionString;

        // このリクエスト専用の接続
        protected NpgsqlConnection _db;
        protected readonly UserManager<ApplicationUser> _userMgr;
        //次の四つのメンバは初期化時にハードコード
        protected string _tblName;//メタデータのテーブル名
        protected string _nameColName; //テーブルのデータ名カラムのカラム名
        protected string _idColName;//テーブルのIDカラムのカラム名
        protected Guid _tenantCode;//テナントコード
        protected ApplicationUser _user;
        protected Guid? _userID;
        protected string _usertenant_code;
        protected IsrvBaseDataObjMgr<T,TKey> _dataObjMgr;     
        protected readonly IDatabaseSchemaProvider _schemaProvider;
        public NxDataController(IConfiguration config,
                                UserManager<ApplicationUser> userManager,
                                IDatabaseSchemaProvider schemaProvider) {
            _connectionString = config.GetConnectionString("DefaultConnection");
            _userMgr = userManager;
            _schemaProvider = schemaProvider;
        }

        protected NpgsqlConnection CreateConnection() {
            return new NpgsqlConnection(_connectionString);
        }

        protected async Task InitializeNxApi() {
            var schemas = await _schemaProvider.GetSchemasAsync();
            var typeMap = NxTypeMapBuilder.FromSchemas(schemas);
            NxTypeMapper.Set(typeMap);
        }

        protected async Task SetUserInfo() {
            _user = await _userMgr.GetUserAsync(User);

            if (_user == null) {
                _userID = Guid.Empty;
                _tenantCode = Guid.Empty;
                return;
            }

            // IdentityUser.Id は string なので Guid に変換する
            _userID = Guid.TryParse(_user.Id, out var uid) ? uid : Guid.Empty;
            _tenantCode = Guid.TryParse(_user.TenantCode, out var tc) ? tc : Guid.Empty;
        }



        // ★ このリクエスト用の接続を用意して _db にセット
        protected void EnsureConnection() {
            if (_db == null) {
                _db = CreateConnection();
                _db.Open();
            }
        }

        //Httpエンドポイントで必ず呼び出す事。
        protected abstract Task CreateObjMgr(RecordQuery query);
        // { 派生先での実装例（_dataObjMgr の型は派生先に合わせて変更すること）
        //   ※ ほぼコピペで使えるが、各コントローラ固有の ObjMgr を new する点だけ注意
        //     // ★ユーザー情報を取得（JWT の tenant_code を含む）
        //    await SetUserInfo();
        //    EnsureConnection(); // ★ここで _db を開く
        //    
        //     // ★ObjMgr を生成（Initialize は呼ばない）
        //    _dataObjMgr = new BaseDataObjMgr<T, TKey>(_db, _tenantCode, _userID);

        //    _dataObjMgr.TableName   = _tblName;
        //    _dataObjMgr.IdColName   = _idColName;
        //    _dataObjMgr.NameColName = _nameColName;
        //     ★DataList やテーブル情報を読み込む
        //    _dataObjMgr.Initialize();
        //}

        [HttpGet("sync/{refreshedAt}")]
        public virtual async Task<IActionResult> SyncAll(DateTime refreshedAt)
        {
            var q = new RecordQuery();
            q.And.Add(new Condition {
                Column = "update_at",
                Operator = ">",
                Value = refreshedAt
            });

            q.Or.Add(new Condition {
                Column = "locked_at",
                Operator = ">",
                Value = refreshedAt
            });

            // DataObjMgr を生成
            await CreateObjMgr(q); 
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


        [HttpPost("SetLockStatus")]
        public virtual async Task<IActionResult> SetLockStatus([FromBody] LockCommand<TKey> req) {
  
            var q = new RecordQuery();
            q.TargetIds = new List<object> { req.DataId };

            await CreateObjMgr(q);

            // ① 対象データ取得
            var dataObj = (IBaseDataObj<TKey>)_dataObjMgr.Get(req.DataId);

            if (dataObj == null)
                return BadRequest($"Data not found: {req.DataId}");

            // ② ロック要求（セット／解除兼用）
            var lockStatus = await dataObj.SetLockAsync();

            // ③ 結果返却（SetLockAsync の戻り値そのまま）
            return Ok(lockStatus);
        }

        [HttpPost("Delete")]
        public virtual async Task<IActionResult> Delete(
            [FromBody] List<TKey> dataLst,
            [FromQuery] bool softDelete = false
        ) {
            // ================================
            // RecordQuery を組み立てる（対象ID世界線）
            // ================================
            var q = new RecordQuery();
            q.TargetIds = dataLst.Cast<object>().ToList(); ;   // ★ IDリストを世界線にセット

            // ================================
            // ObjMgr を RecordQuery 付きで生成
            // ================================
            await CreateObjMgr(q);

            var failedIds = new List<TKey>();

            foreach (var dataId in dataLst) {

                // ① DataObj を取得
                var dataObj = _dataObjMgr.Get(dataId);

                if (dataObj == null) {
                    failedIds.Add(dataId);
                    continue;
                }

                // ② Validate（世界線の入口）
                dataObj.Validate(OperationType.Delete);

                // ③ ロック要求（冪等性保証）
                var lockst = await dataObj.SetLockAsync();

                // ④ ロック確認
                if (!lockst.IsLocked || lockst.LockedByUserId != _userID) {
                    failedIds.Add(dataId);
                    continue;
                }

                // ⑤ 削除処理（ソフト／物理）
                var transaction = _db.BeginTransaction();

                bool result =
                    softDelete
                        ? await ((BaseDataObj<TKey>)dataObj).SoftDeleteQueryExec(transaction)
                        : await ((BaseDataObj<TKey>)dataObj).DeleteQueryExec(transaction);

                if (!result) {
                    transaction.Rollback();

                    // ⑥ ロック解除（失敗時も必ず）
                    await dataObj.SetUnLockAsync();

                    failedIds.Add(dataId);
                    continue;
                }

                // ⑦ 削除成功
                transaction.Commit();

                // DataList から削除
                _dataObjMgr.RemoveFromList((BaseDataObj<TKey>)dataObj);

                // ⑧ ロック解除（正常系でも必ず）
                await dataObj.SetUnLockAsync();
            }

            // ⑨ 結果返却
            return Ok(failedIds);
        }

        [HttpPost("DataOpenLatest")]
        public async Task<DataOpenResult> DataOpenLatest([FromBody] TKey dataId)
        {
            var q = new RecordQuery();
            q.TargetIds = new List<object> { dataId ?? default };

            await CreateObjMgr(q);
            // ① 正本世界線のロック状態を取得
            var DataObj = _dataObjMgr.Get(dataId);
            var lockSt = await DataObj.SetLockAsync();
    
             // ② レコード無し（RecordNone）なら新規扱い
            if (!lockSt.Exists)
            {
                return new DataOpenResult {
                    HasError = false,
                    LockStatus = lockSt,
                    Json = null
                };
            }

            // ③ 編集不可（他人ロック）なら JSON は返さない
            if (!lockSt.CanEdit || lockSt.HasError)
            {
                return new DataOpenResult {
                    HasError = lockSt.HasError,
                    ErrorMessage = lockSt.ErrorMessage,
                    LockStatus = lockSt,
                    Json = null
                };
            }

            // ④ 編集可能 → 最新状態の JSON を返す
            // Base世界線の正本を TblToJson で取得
            var dataObj = _dataObjMgr.Get(dataId);   // あなたの DataObj 管理構造に合わせて
            var json = dataObj.TblToJson(dataId, null);

            return new DataOpenResult {
                HasError = false,
                LockStatus = lockSt,
                Json = json
            };
        }

      [HttpPost("Save/{dataId}")]
        public virtual async Task<IActionResult> Save(TKey? dataId, [FromBody] string ReceiveJson)
        {
            var q = new RecordQuery();
            q.TargetIds = new List<object> { dataId ?? default };

            await _dataObjMgr.Initialize(q);

            // 既存 or 新規オブジェクト取得
            var obj = _dataObjMgr.Get(dataId)
                      ?? _dataObjMgr.CreateNewDataObj(default);

            // ★ Validate を最初に必ず通す
            obj.Validate(OperationType.Save);

            var lockinfo = await obj.SetLockAsync();
            if (!lockinfo.IsLocked || lockinfo.LockedByUserId != _userID)
                return BadRequest("Lock failed");

            using var tran = _db.BeginTransaction();

            var SaveJson = ReceiveJson;

            // ★ JSON を Dictionary に変換
            var workingRaw = JsonSerializer.Deserialize<List<TableJson>>(SaveJson);

            var Table = workingRaw.First(t => t.Table == this._tblName);
            var Row = Table.Rows.First();

            try {
                // ★ 新規なら ID 採番
                if (EqualityComparer<TKey>.Default.Equals(dataId, default)) {
                    var realID = EnsureIDForSave(obj, tran);
                    Row[obj.IdColName] = realID;

                    // サブテーブルも書き換え
                    foreach (var tbl in workingRaw.Where(t => t.Table != this._tblName)) {
                        foreach (var row in tbl.Rows) {
                            row[obj.IdColName] = realID;
                            row["tenant_code"] = obj.TenantCode;
                        }
                    }
                }

                // ★ テーブル書き込み前フック
                if (!await BeforeSaveProcess(obj, workingRaw, tran)) {
                    tran.Rollback();
                    return BadRequest("BeforeSaveProcess failed");
                }

                SaveJson = JsonSerializer.Serialize(workingRaw);

                // ★ 保存処理へ
                if (!await obj.JsonToTbl(SaveJson, tran)) {
                    tran.Rollback();
                    return BadRequest("JsonToTbl failed");
                }

                // ★ ロック情報が JSON で更新されてしまうので再設定
                 await obj.ReWriteLockInfoAsync(tran, obj.CurrUsrID, DateTime.UtcNow);

                // ★ テーブル書き込み後フック
                if (!await AfterSaveProcess(obj, workingRaw, tran)) {
                    tran.Rollback();
                    return BadRequest("AfterSaveProcess failed");
                }

                tran.Commit();
                await obj.UpdatePropertiesFromTbl();
                return Ok(new { NewID = obj.DataID, UpdateAt = obj.Update_at });
            }
            catch (Exception ex) {
                tran.Rollback();
                return BadRequest(ex.Message);
            }
        }
        // =======================================================
        // 保存の前後に追加処理を行いたい場合は継承先で
        // BeforeSaveProcess / AfterSaveProcess をオーバーライドする
        // =======================================================
        protected virtual Task<bool> BeforeSaveProcess(
                                                IBaseDataObj<TKey> obj,
                                                List<TableJson> workingRaw,
                                                IDbTransaction tran) {
            return Task.FromResult(true);
        }

        protected virtual Task<bool> AfterSaveProcess(
                                                IBaseDataObj<TKey> obj,
                                                List<TableJson> workingRaw,
                                                IDbTransaction tran) {
            return Task.FromResult(true);
        }


        // =======================================================
        // 保存の時にDataIDを確定させる場合は継承先で
        // オーバーライドして処理を記述(ZmstController参照)
        // =======================================================
        protected virtual TKey EnsureIDForSave(IBaseDataObj<TKey> obj, IDbTransaction tran) {
            return obj.DataID;
        }


        [HttpPost("ReName/{dataId}/{newName}")]
        public virtual async Task<IActionResult> Rename(TKey dataId, string newName)
        {
            var q = new RecordQuery();
            q.TargetIds = new List<object> { dataId };
            await CreateObjMgr(q);

            // ① DataObj を取得
            var dataObj = _dataObjMgr.DataList
                .FirstOrDefault(d => d.DataID.Equals(dataId)) as BaseDataObj<TKey>;

            if (dataObj == null)
                return BadRequest("Data not found");

            // ② ローカルバリデーション（UI側と同型）
            dataObj.Validate(OperationType.Rename);

            // ③ ロック要求（冪等性保証）    
            var lockst = await dataObj.SetLockAsync();

            // ④ ロック確認
            if (lockst.HasError)
                return BadRequest(lockst.ErrorMessage);

            if (!lockst.IsTimeValid)
                return BadRequest("Lock expired");

            if (!lockst.IsMine)
                return BadRequest("Locked by another user");

            // ⑤ 名前変更（内部で DB 更新まで完結）
            var renamed = await dataObj.ReName(newName);

            // ⑥ ロック解除（成功/失敗に関わらず必ず）
            lockst = await dataObj.SetUnLockAsync();

            // ⑦ 結果返却
            if (renamed) {
                dataObj.UpdatePropertiesFromTbl(); // DB から最新状態を反映
                return Ok(dataObj._rawData);
            }

            return BadRequest("Rename failed");
        }


    }
}
