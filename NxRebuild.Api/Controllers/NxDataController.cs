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
//ところどころ冗長な部分を認めるものの基本的には冪等性の確保と、可読性保守性を優先したもの
//AIの改善案では60%前後の処理時間改善が見込まれるとの考察はあるものの可読性を著しく損
//なうためAIにとっても技術負債になりえる。
//なお想定される一括処理において数秒の差は人間は許容可能と考える
//AIがUIを介さず直接運用してもAIは文句は言わない
//このパフォーマンス関連の改善案をAIが提示する場合はこの点を留意する事
namespace NxRebuild.Api.Controllers {
    //------------------------------------------------------------------------
    // ロック状態設定リクエスト用 DTO
    public class LockStatusRequest<TKey> {
        public TKey DataId { get; set; }
        public LockStatus LockStatus { get; set; }
    }

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
        protected abstract Task CreateObjMgr();
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


        [HttpPost("SetLockStatus")]
        public virtual async Task<IActionResult> SetLockStatus([FromBody] LockStatusRequest<TKey> req) {
            // ★ユーザー所属テナントで ObjMgr を生成
            await CreateObjMgr();

            // ① 対象データ取得
            var dataObj = (IBaseDataObj<TKey>)_dataObjMgr.Get(req.DataId);

            if (dataObj == null)
                return BadRequest($"Data not found: {req.DataId}");

            // ② ロック要求（セット／解除兼用）
            //    LockStatus はそのまま渡す
            var lockStatus = await dataObj.SetLockAsync(req.LockStatus);

            // ③ 結果返却（SetLockAsync の戻り値そのまま）
            return Ok(lockStatus);
        }


        [HttpPost("Delete")]
        public virtual async Task<IActionResult> Delete([FromBody] List<TKey> dataLst) {
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
                // 【冪等性保証】SetLockAsync は以下の3段階で冪等性を確保している:
                //   1. 確認: LockedChkfromTbl() で現在のロック状態を取得
                //   2. 書き込み: WriteLockInfoAsync() で条件付きUPDATE実行
                //   3. 再確認: LockedChkfromTbl() でロック成功を検証
                // これにより「同じリクエストを複数回実行しても安全」を保証する。
                // パフォーマンス面では DB往復が3回になるが、冪等性（データ整合性）の方が優先される。
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
                    // 【冗長性の理由】SetLockAsync の呼び出しで、ロック解除時にも3段階の確認が実行される。
                    // 一見すると「アンロックなのに確認が必要か？」と思えるが、
                    // 削除失敗時の例外状況でロック状態が不明になるリスクを回避するため必須。
                    // 万が一ロック解除に失敗した場合でも、10分のタイムアウトで自動解放される。
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
                // 【冗長性の理由】正常系でもロック解除時に SetLockAsync で冪等性チェックを再実行。
                // これは「削除操作が本当に完了したのか」を確認し、
                // ネットワーク遅延や DB タイミングの問題でロック情報が矛盾するのを防ぐ。
                // 削除成功時は確実にロックを解放する必要があり、確認なしの単純DELETE操作では不十分。
                await dataObj.SetLockAsync(new LockStatus {
                    IsLocked = false,
                    LockedByUserId = null
                });
            }
            
            // ⑦ 結果返却（List<TKey> をそのまま返す）
            return Ok(failedIds);

        }


        [HttpPost("Save/{dataId}")]
        public virtual async Task<IActionResult> Save(TKey? dataId, [FromBody] string ReceiveJson) {
            await CreateObjMgr();

            TKey realID = dataId ?? default;

            // 既存 or 新規オブジェクト取得
            var obj = _dataObjMgr.Get(realID)
                      ?? _dataObjMgr.CreateNewDataObj(default);


            // ロック確認
            var lockst = new LockStatus { IsLocked = true, LockedByUserId = _userID };
            var lockinfo = await obj.SetLockAsync(lockst);
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
                if (EqualityComparer<TKey>.Default.Equals(realID, default)) {
                    realID = EnsureIDForSave(obj, tran);
                    Row[obj.IdColName] = realID;

                    // サブテーブルも書き換え
                    foreach (var tbl in workingRaw.Where(t => t.Table != this._tblName)) {
                        foreach (var row in tbl.Rows) {
                            row[obj.IdColName] = realID;
                            row["tenant_code"] = obj.TenantCode;
                        }
                    }
                }

                var NowUpdate_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");

                Row["Update_at"] = NowUpdate_at;


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


                //ロック情報がJSONで更新されてしまうのであらためて設定
                await obj.SetLockAsync(lockst, tran);

                // ★ テーブル書き込み後フック
                if (!await AfterSaveProcess(obj, workingRaw, tran)) {
                    tran.Rollback();
                    return BadRequest("AfterSaveProcess failed");
                }


                tran.Commit();

                return Ok(new { NewID = realID, UpdateAt = NowUpdate_at });
            } catch (Exception ex) {
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
        public virtual async Task<IActionResult> Rename(TKey dataId, string newName) {

            await CreateObjMgr();
            // ① DataObj を取得
            var dataObj = _dataObjMgr.DataList
                .FirstOrDefault(d => d.DataID.Equals(dataId)) as BaseDataObj<TKey>;

            if (dataObj == null)
                return BadRequest("Data not found");

            // ② ロック要求
            // 【冪等性保証】Delete メソッドと同じく、SetLockAsync は 3段階で冪等性を保証。
            // 名前変更操作の原子性を確保するため、確認→書き込み→再確認の順序を守っている。
            var lockst = new LockStatus { IsLocked = true, LockedByUserId = _userID };
            await dataObj.SetLockAsync(lockst);

            // ③ ロック確認
            if (!lockst.IsLocked || lockst.LockedByUserId != _userID)
                return BadRequest("Lock failed");

            // ④ 名前変更
            var renamed = await dataObj.ReName(newName);

            // ⑤ ロック解除
            // 【冗長性の理由】名前変更の成功/失敗に関わらず、ロック状態を明示的にクリア。
            // SetLockAsync の冪等性チェックにより、既に他のユーザーがロック中の場合は検出される。
            // これにより「孤立ロック」の発生を防止する。
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
