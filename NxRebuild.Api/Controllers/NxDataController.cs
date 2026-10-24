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

            var meta = NxObjsMetaReader.Read(typeof(T));
            if (meta == null)
                throw new Exception($"NxObjsMetaDataAttribute が {typeof(T).Name} に設定されていません。");

            _tblName = meta.PrimaryTable;
            _nameColName = meta.NameCol;
            _idColName = meta.IdCol;
            

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
        protected abstract Task CreateObjMgrAsync(RecordQuery query);
        // { 派生先での実装例（_dataObjMgr の型は派生先に合わせて変更すること）
        //   ※ ほぼコピペで使えるが、各コントローラ固有の ObjMgr を new する点だけ注意
        //     // ★ユーザー情報を取得（JWT の tenant_code を含む）
        //    await SetUserInfo();
        //    EnsureConnection(); // ★ここで _db を開く
        //    
        //     // ★ObjMgr を生成（Initialize は呼ばない）
        //    _dataObjMgr = new BaseDataObjMgr<T, TKey>(_db, _tenantCode, _userID);

        //     ★DataList やテーブル情報を読み込む
        //    _dataObjMgr.Initialize();
        //}

        [HttpGet("sync/{refreshedAt}")]
        public virtual async Task<IActionResult> SyncAll(DateTime refreshedAt) {
            try {
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

                // ★ DataObjMgr を生成
                await CreateObjMgrAsync(q);
                var mgr = _dataObjMgr;

                var resultList = new List<object>();

                foreach (var obj in mgr.DataList) {
                    if ((obj.Update_at <= refreshedAt) && (obj.LockedAt <= refreshedAt))
                        continue;

                    // ★ クライアントの更新及びロック時刻よりあたらしいBaseDataObj<TKey>
                    string json;

                    try {
                        json = obj.TblToJson();   // ← ここで例外が起こる可能性あり
                    } catch (Exception ex) {
                        // ★ TblToJson の例外を NxOperationResult に包んで返す
                        return BadRequest(new NxOperationResult<TKey> {
                            OperationErrorCode = NxOperationErrorCode.ServerException,
                            Message = $"TblToJson failed: {ex.Message}"
                        });
                    }

                    resultList.Add(new {
                        Key = obj.DataID,
                        Data = json
                    });
                }

                // ★ NxOperationResult に SyncItems を含めて返す
                return Ok(new NxOperationResult<TKey> {
                    SyncItems = resultList,
                    Message = "Sync completed"
                    // Success は計算判定で true
                });
            } catch (Exception ex) {
                // ★ CreateObjMgrAsync やその他の例外を NxOperationResult に包んで返す
                return BadRequest(new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"SyncAll failed: {ex.Message}"
                });
            }
        }



        [HttpPost("SetLockStatus")]
        public virtual async Task<IActionResult> SetLockStatus([FromBody] LockCommand<TKey> req) {

            try {
                var q = new RecordQuery();
                q.TargetIds = new List<object> { req.DataId };

                await CreateObjMgrAsync(q);

                // ① 対象データ取得
                var dataObj = (BaseDataObj<TKey>)_dataObjMgr.Get(req.DataId);

                if (dataObj == null)
                    return BadRequest(new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.ServerException,
                        Message = $"Data not found: {req.DataId}"
                    });

                // ② ロック要求（セット）
                LockStatus lockStatus;

                try {
                    if (req.doLock)
                        lockStatus = await dataObj._SetLockAsync();     // ← ここで例外が起こる可能性あり
                    else
                        lockStatus = await dataObj._SetUnLockAsync();   // ← ここで例外が起こる可能性あり
                } catch (Exception ex) {
                    // ★ ロック処理中の例外を NxOperationResult に包んで返す
                    return BadRequest(new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.ServerException,
                        Message = $"Lock operation failed: {ex.Message}"
                    });
                }

                // ③ 結果返却（LockStatus を NxOperationResult に包む）
                return Ok(new NxOperationResult<TKey> {
                    LockStatus = lockStatus,
                    Message = lockStatus.ErrorMessage,
                    // ValidationErrorCode は使わない（ロックは業務ロジックではない）
                    OperationErrorCode = lockStatus.HasError
                        ? NxOperationErrorCode.ServerException
                        : NxOperationErrorCode.None
                });
            } catch (Exception ex) {
                // ★ CreateObjMgrAsync やその他の例外を NxOperationResult に包んで返す
                return BadRequest(new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"SetLockStatus failed: {ex.Message}"
                });
            }
        }



        [HttpPost("Delete")]
        public virtual async Task<IActionResult> Delete(
                                        [FromBody] List<TKey> dataLst,
                                        [FromQuery] bool softDelete = false) {
            try {
                var q = new RecordQuery();
                q.TargetIds = dataLst.Cast<object>().ToList(); ;   // ★ IDリストを世界線にセット

                await CreateObjMgrAsync(q);

                var failedIds = new List<TKey>();

                foreach (var dataId in dataLst) {
                    try {
                        //  DataObj を取得
                        var dataObj = (BaseDataObj<TKey>)_dataObjMgr.Get(dataId);

                        if (dataObj == null) {
                            failedIds.Add(dataId);
                            continue;
                        }

                        //  _Validate
                        var v = dataObj._Validate(OperationType.Delete);
                        if (!v.Success) {
                            failedIds.Add(dataId);
                            continue;
                        }

                        //  ロック要求（冪等性保証）
                        var lockst = await dataObj._SetLockAsync();

                        //　ロック確認
                        if (!lockst.CanEdit || lockst.Result != LockResult.Success) {
                            failedIds.Add(dataId);
                            continue;
                        }

                        var DeleteResult = await _dataObjMgr.DeleteDataAsync(
                            new List<TKey> { dataObj.DataID },
                            softDelete
                        );

                        // 削除成功かどうか
                        bool result = DeleteResult.FailedIds.Count == 0;

                        if (!result) {
                            // ロック解除（失敗時は必ず）
                            await dataObj._SetUnLockAsync();

                            failedIds.Add(dataObj.DataID);
                            continue;
                        }

                        // 削除成功したらレコードは存在しないかソフトデリートでもロック解除は必要ない
                    } catch (Exception ex) {
                        // ★ 個別データの削除処理で例外が起きた場合
                        failedIds.Add(dataId);

                        // ロックが残っている可能性があるので安全に解除を試みる
                        try {
                            var dataObj = (BaseDataObj<TKey>)_dataObjMgr.Get(dataId);
                            if (dataObj != null)
                                await dataObj._SetUnLockAsync();
                        } catch { /* ロック解除失敗は無視 */ }

                        // 続行（他の ID の削除は続ける）
                        continue;
                    }
                }

                // ★ NxOperationResult に変換して返す
                return Ok(new NxOperationResult<TKey> {
                    FailedIds = failedIds,
                    ValidationErrorCode = failedIds.Count == 0
                        ? NxValidationErrorCode.None
                        : NxValidationErrorCode.CannotDeleteRoot,
                    Message = failedIds.Count == 0
                        ? "Deleted"
                        : "Some items failed"
                });
            } catch (Exception ex) {
                // ★ Delete 全体の例外（CreateObjMgrAsync や内部例外）
                return BadRequest(new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"Delete failed: {ex.Message}"
                });
            }
        }



        [HttpPost("DataOpenLatest")]
        public async Task<IActionResult> DataOpenLatest([FromBody] TKey dataId) {
            try {
                var q = new RecordQuery();
                q.TargetIds = new List<object> { dataId ?? default };

                await CreateObjMgrAsync(q);

                // ① 正本世界線のロック状態を取得
                var DataObj = (BaseDataObj<TKey>)_dataObjMgr.Get(dataId);

                if (DataObj == null) {
                    return BadRequest(new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.ServerException,
                        Message = $"Data not found: {dataId}"
                    });
                }

                LockStatus lockst;

                try {
                    lockst = await DataObj._SetLockAsync();   // ← ここで例外が起こる可能性あり
                } catch (Exception ex) {
                    return BadRequest(new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.ServerException,
                        Message = $"Lock failed: {ex.Message}"
                    });
                }

                // ② レコード無し（RecordNone）なら新規扱い
                if (!lockst.Exists) {
                    return Ok(new NxOperationResult<TKey> {
                        LockStatus = lockst,
                        Message = "Record does not exist (new data)",
                        RawData = null
                    });
                }

                // ③ 編集不可（他人ロック）なら JSON は返さない
                if ((!lockst.CanEdit || lockst.Result != LockResult.Success)) {
                    return BadRequest(new NxOperationResult<TKey> {
                        ValidationErrorCode = NxValidationErrorCode.LockedByOther,
                        Message = lockst.ErrorMessage,
                        LockStatus = lockst,
                        RawData = null
                    });
                }

                // ④ 編集可能 → 最新状態の JSON を返す
                string json;

                try {
                    // Base世界線の正本を TblToJson で取得
                    var dataObj = _dataObjMgr.Get(dataId);   // DataObj 管理構造に合わせて
                    json = dataObj.TblToJson(dataId, null);  // ← ここで例外が起こる可能性あり
                } catch (Exception ex) {
                    return BadRequest(new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.ServerException,
                        Message = $"TblToJson failed: {ex.Message}",
                        LockStatus = lockst
                    });
                }

                // ★ 正本 JSON を RawData に入れるかどうかは設計次第
                // 今回は JSON をそのまま返すため RawData は null のまま
                return Ok(new NxOperationResult<TKey> {
                    LockStatus = lockst,
                    Message = "Data opened successfully",
                    RawData = null,   // JSON は別フィールドで返すならここは null
                                      // ★ SyncItems を使うか、専用フィールドを追加するかは後で決める
                });
            } catch (Exception ex) {
                // ★ CreateObjMgrAsync やその他の例外を NxOperationResult に包んで返す
                return BadRequest(new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"DataOpenLatest failed: {ex.Message}"
                });
            }
        }

        // =======================================================
        // サーバー側保存処理を行う
        // Nxでは複数ユーザーによる単一データに対する同時編集を許可しないため、
        // 他ユーザーによる同時書き込み・ロック競合が発生しません。
        // そのため保存処理中にトランザクションが詰まる要因が存在しません。
        // よって保存後の処理は一本のトランザクションで安全に完結させることができます。
        //
        // protected virtual Task<bool> BeforeSaveProcess
        // protected virtual Task<bool> AfterSaveProcess
        // の二つのメソッドをオーバーライドすることで、保存前後の追加処理を実装できます。
        // この中でトランザクションを使用する場合は、引数の tran を使用してください。
        // =======================================================
        [HttpPost("Save/{dataId}")]
        public virtual async Task<IActionResult> Save(TKey? dataId, [FromBody] string ReceiveJson) {
            var q = new RecordQuery();
            q.TargetIds = new List<object> { dataId ?? default };

            await CreateObjMgrAsync(q);

            // 既存 or 新規オブジェクト取得
            var obj = (BaseDataObj<TKey>)(_dataObjMgr.Get(dataId)
                      ?? _dataObjMgr.CreateNewDataObj(default));

            // ★ _Validate を最初に必ず通す(！！！！！！！！！工事中！！！！！！
            //var v = obj._Validate(OperationType.Save);
            //if (!v.Success)
            //    return BadRequest(v);

            var lockst = await obj._SetLockAsync();
            if (!lockst.CanSave) {
                return BadRequest(new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = "Lock failed"
                });
            }

            using var tran = _db.BeginTransaction();

            if (!await obj._DeleteDetailQueryExecAsync(tran)) {
                tran.Rollback();
                return BadRequest(new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.DbError,
                    Message = "既存データの削除に失敗しました"
                });
            }

            var SaveJson = ReceiveJson;

            // ★ JSON を Dictionary に変換
            var workingRaw = JsonSerializer.Deserialize<List<TableJson>>(SaveJson);

            var Table = workingRaw.First(t => t.Table == this._tblName);

            try {
                var Row = Table.Rows.First();
                // ★ 新規なら ID 採番
                if (EqualityComparer<TKey>.Default.Equals(obj.DataID, default)) {

                    var realID = EnsureIDForSave(obj, tran);

                    // ★ rawData の PK を更新（必須）
                    obj.DataID = realID;

                    // ★ 親（Zmst）
                    Row[obj.IdColName] = realID;

                    // ★ 子（tan_m）
                    foreach (var tbl in workingRaw.Where(t => t.Table != this._tblName)) {
                        foreach (var row in tbl.Rows) {
                            row[obj.IdColName] = realID;
                            row["tenant_code"] = obj.TenantCode;
                        }
                    }
                }

                // ★ テーブル書き込み前フック
                var beforeSaveResult = await BeforeSaveProcess(obj, workingRaw, tran);
                if (!beforeSaveResult.Success) {
                    tran.Rollback();
                    return BadRequest(beforeSaveResult);
                }

                var UpdateAtsrv = DateTime.UtcNow;//.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                Row["Update_at"] = UpdateAtsrv;
                SaveJson = JsonSerializer.Serialize(workingRaw);

                // ★ 保存処理へ
                if (!await obj.JsonToTblAsync(SaveJson, tran)) {
                    tran.Rollback();
                    return BadRequest(new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.DbError,
                        Message = "JsonToTbl failed"
                    });
                }

                // ★ ロック情報が JSON で更新されてしまうので再設定
                await obj._ReWriteLockInfoAsync(tran, obj.CurrUsrID, DateTime.UtcNow);

                // ★ テーブル書き込み後フック
                var afterSaveResult = await AfterSaveProcess(obj, workingRaw, tran);
                if (!afterSaveResult.Success) {
                    tran.Rollback();
                    return BadRequest(afterSaveResult);
                }

                tran.Commit();

                // ★ DB → オブジェクトへ最新値を反映
                await obj._UpdatePropertiesFromTblAsync();

                // ★ NxOperationResult を返す（正史の世界線）
                return Ok(new NxOperationResult<TKey> {
                    RawData = obj._rawData,
                    NewID = obj.DataID,
                    UpdateAt = obj.Update_at.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                });
            } catch (Exception ex) {
                tran.Rollback();
                return BadRequest(new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = ex.Message
                });
            }
        }


        // =======================================================
        // 保存の前後に追加処理を行いたい場合は継承先で
        // BeforeSaveProcess / AfterSaveProcess をオーバーライドする
        // =======================================================
        protected virtual Task<NxOperationResult<TKey>> BeforeSaveProcess(
                                                IBaseDataObj<TKey> obj,
                                                List<TableJson> workingRaw,
                                                IDbTransaction tran) {


            return Task.FromResult(new NxOperationResult<TKey> {
                OperationErrorCode = NxOperationErrorCode.None,
                ValidationErrorCode = NxValidationErrorCode.None
            });
        }

        protected virtual Task<NxOperationResult<TKey>> AfterSaveProcess(
                                                IBaseDataObj<TKey> obj,
                                                List<TableJson> workingRaw,
                                                IDbTransaction tran) {


            return Task.FromResult(new NxOperationResult<TKey> {
                OperationErrorCode = NxOperationErrorCode.None,
                ValidationErrorCode = NxValidationErrorCode.None
            });
        }


        // =======================================================
        // 保存の時にDataIDを確定させる場合は継承先で
        // オーバーライドして処理を記述(ZmstController参照)
        // =======================================================
        protected virtual TKey EnsureIDForSave(IBaseDataObj<TKey> obj, IDbTransaction tran) {
            return obj.DataID;
        }


        [HttpPost("ReName/{dataId}/{newName}")]
        public virtual async Task<IActionResult> ReName(TKey dataId, string newName) {
            var q = new RecordQuery();
            q.TargetIds = new List<object> { dataId };
            await CreateObjMgrAsync(q);

            // ① DataObj を取得
            var dataObj = (BaseDataObj<TKey>)_dataObjMgr.Get(dataId);

            if (dataObj == null)
                return BadRequest(new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = "Data not found"
                });

            // ② ローカルバリデーション（UI側と同型）
            var v = dataObj._Validate(OperationType.Rename, newName);
            if (!v.Success)
                return BadRequest(v);

            // ③ ロック要求（冪等性保証）    
            var lockst = await dataObj._SetLockAsync();

            // ④ ロック確認
            if (lockst.HasError)
                return BadRequest(new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = lockst.ErrorMessage
                });

            if (!lockst.IsTimeValid)
                return BadRequest(new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = "Lock expired"
                });

            if (!lockst.IsMine)
                return BadRequest(new NxOperationResult<TKey> {
                    ValidationErrorCode = NxValidationErrorCode.LockedByOther,
                    Message = "Locked by another user"
                });

            // ⑤ 名前変更（内部で DB 更新まで完結）
            var renameResult = await dataObj.ReNameAsync(newName);

            // ⑥ ロック解除（成功/失敗に関わらず必ず）
            await dataObj._SetUnLockAsync();

            // ⑦ 結果返却
            if (!renameResult.Success)
                return BadRequest(renameResult);


            //await dataObj._UpdatePropertiesFromTblAsync(); // DB から最新状態を反映

            return Ok(new NxOperationResult<TKey> {
                RawData = dataObj._rawData,
                UpdateAt = dataObj.Update_at.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            });
        }
    }
}
