using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.Client.Pages.Auth;
using NxRebuild.shared;
using System.Data;
using System.Net.Http.Json;
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB {

    public interface ISyncBaseDataObjMgr<TBase, TSync, TKey> : IBaseDataObjMgr<TBase, TKey>
        where TBase : BaseDataObj<TKey>
        where TSync : SyncBaseDataObj<TKey> {
        event Action? DataChanged;
        Task<NxOperationResult<TKey>> SyncData();
    }

    // インスタンス生成後、Mgr が保持している PK（IdCol 以外の親 PK/FK）がある場合は、
    // 生成元）が PrimaryPKDict を使って DataObj に注入する。
    public abstract class SyncBaseDataObjMgr<TBase, TSync, TKey> : ISyncBaseDataObjMgr<TBase, TSync, TKey>
        where TBase : BaseDataObj<TKey>, new()
        where TSync : SyncBaseDataObj<TKey>, new() {

        protected BaseDataObjMgr<TBase, TKey> _baseDataObjMgr;
        protected HttpClient _http;
        protected CustomAuthStateProvider _auth;

        public abstract string ApiRoute { get; }

        public event Action? DataChanged;　//更新イベント

        public string TblName => _baseDataObjMgr.TblName;
        public string[] DetailTableList => _baseDataObjMgr.DetailTableList;
        public string InfoTbl => _baseDataObjMgr.InfoTbl;
        public string W_TblName => _baseDataObjMgr.W_TblName;
        public string[] W_DetailTblList => _baseDataObjMgr.W_DetailTblList;

        public NxDataType DataType { get => _baseDataObjMgr.DataType; set => _baseDataObjMgr.DataType = value; }
        public DateTime Refreshed_at => _baseDataObjMgr.Refreshed_at;

        public Guid TenantCode { get => _baseDataObjMgr.TenantCode; set => _baseDataObjMgr.TenantCode = value; }
        public Guid CurrentUserID { get => _baseDataObjMgr.CurrentUserID; set => _baseDataObjMgr.CurrentUserID = value; }

        public IDbConnection DBcon { get => _baseDataObjMgr.DBcon; set => _baseDataObjMgr.DBcon = value; }

        public IEnumerable<IBaseDataObj<TKey>> DataList {
            get => _baseDataObjMgr._dataList.Cast<IBaseDataObj<TKey>>();
        }
        public Dictionary<string, object?> PrimaryPKDict {
            get => _baseDataObjMgr.PrimaryPKDict;
            set => _baseDataObjMgr.PrimaryPKDict = value;
        }

        public SyncBaseDataObjMgr(IDbConnection db, HttpClient http, CustomAuthStateProvider auth,
                                  Guid tenantCode, Guid currentUserId) {
            // 派生先で BaseDataObjMgr を生成すること
        }

        public virtual IBaseDataObj<TKey>? Get(TKey id) => _baseDataObjMgr.Get(id);

        //新しいデータオブジェクトを作成する。
        //PKが複数ある場合は、オーバーライドしてPKDictで設定する。
        public virtual IBaseDataObj<TKey> CreateNewDataObj(TKey? parentID) {
            var baseObj = _baseDataObjMgr.CreateNewDataObj(parentID);
            var syncObj = new TSync();
            syncObj.Http = _http;
            syncObj.Auth = _auth;
            syncObj._dataObj = (TBase)baseObj;
            syncObj.SelfObjMgr = this;
            syncObj.DBcon = DBcon;
            syncObj.TenantCode = TenantCode;
            syncObj.CurrUsrID = CurrentUserID;
            _baseDataObjMgr._dataList.Add(syncObj);//親の登録（Setparent）はまだしない
            return syncObj;
        }


        public virtual void SetParent(IBaseDataObj<TKey> obj) {
            _baseDataObjMgr.SetParent(obj);
        }



        public virtual void InsertNewDataItem(IBaseDataObj<TKey> obj) {
            _baseDataObjMgr.InsertNewDataItem(obj);
        }


        // データベースからデータを取得する（クライアント・サーバー共用）
        public virtual async Task InitializeAsync(RecordQuery q) {

            // BaseDataObjでプロパティをロード
            await _baseDataObjMgr.InitializeAsync(q);

            // ★ Sync 用の新しいリストを作る
            var syncList = new List<IBaseDataObj<TKey>>();

            foreach (var baseObj in _baseDataObjMgr._dataList) {//ここではDataListで呼び出してはいけない

                var syncObj = new TSync();
                syncObj.Http = _http;
                syncObj.Auth = _auth;
                syncObj._dataObj = (TBase)baseObj;
                syncObj.SelfObjMgr = this;
                syncObj.DBcon = DBcon;
                syncObj.TenantCode = TenantCode;
                syncObj.CurrUsrID = CurrentUserID;

                syncList.Add(syncObj);
            }

            // ★ Base の DataList を Sync のリストで丸ごと挿げ替え
            if (syncList != null) {
                _baseDataObjMgr._dataList = syncList;

                foreach (var obj in _baseDataObjMgr._dataList) {
                    SetParent(obj);
                }
            }
        }

        public virtual async Task<IEnumerable<dynamic>> LoadRecordsAsync() {
            string sql = $"SELECT * FROM \"{TblName}\" WHERE tenant_code = @TenantCode;";
            return await DBcon.QueryAsync<dynamic>(sql, new { TenantCode = TenantCode });
        }

        // =======================================================
        // サーバー側との同期処理を行う
        // Nxではローカル正史を保持し、サーバーは正史の写像を返すだけの世界線のため、
        // 差分検出や競合解決は必要ありません。
        // 通信は「正史スナップショット(JSON)の受信」が唯一の役割となります。
        //
        // 通信失敗時はスナップショット更新が進まないだけでローカル正史は壊れません。
        // そのためネットワーク例外・タイムアウト・APIエラーが発生しても、
        // ローカル側の整合性が崩れることはありません。次回の同期で解決されます。
        // =======================================================

        protected bool _syncing;
        public virtual async Task<NxOperationResult<TKey>> SyncData() {
            if (_syncing)
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.Unknown,
                    Message = "同期処理中"
                };

            _syncing = true;

            DateTime refreshed_at = _baseDataObjMgr.Refreshed_at;
            var url = $"{ApiRoute}/sync/{refreshed_at:O}";

            HttpResponseMessage response;

            try {
                // --- 通信ハンドリング（データは通信失敗で壊れない） ---
                response = await _http.GetAsync(url);
            } catch (Exception ex) {
                // ネットワーク例外時はデータ更新が進まないだけなので false を返す
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.NetworkError,
                    Message = $"通信エラー: {ex.Message}"
                };
            }

            if (!response.IsSuccessStatusCode) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.HttpError,
                    Message = $"HTTPエラー: {response.StatusCode}"
                };
            }

            NxOperationResult<TKey>? syncResult;

            try {
                syncResult = await response.Content.ReadFromJsonAsync<NxOperationResult<TKey>>();
            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ResponseParseError,
                    Message = $"レスポンス解析エラー: {ex.Message}"
                };
            }

            if (syncResult == null) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.Unknown,
                    Message = "同期結果が取得できませんでした"
                };
            }

            // サーバーが正常系の形でエラーを返してきた場合はそのまま返す
            if (!syncResult.Success)
                return syncResult;

            // ★ 検証が終わってからトランザクションを開始する（早期 return での開きっぱなしを防ぐ）
            var tran = DBcon.BeginTransaction();

            try {
                int index = 0;
                foreach (var item in syncResult.SyncItems) {

                    var dataId = item.DataId;
                    var dataJson = item.Data;

                    var target = _baseDataObjMgr.Get(dataId);

                    index++;

                    if (target != null) {
                        await target.JsonToTblAsync(dataJson, tran);
                    } else {
                        if (dataJson == null) {
                            continue;
                        }

                        target = CreateNewDataObj(default(TKey));
                        target.DataID = dataId;
                        await target.JsonToTblAsync(dataJson, tran);

                        
                    }

                    // =======================================================
                    // 削除済みデータの扱い
                    // Nxでは削除は「正史のスナップショットに存在しない」ことで表現されます。
                    // Update_at = MaxValue かつ LockedAt = MinValue の組み合わせは
                    // 「サーバーで削除済み」を意味します。
                    // =======================================================
                    if (target.Update_at == DateTime.MaxValue
                        && target.LockedAt == DateTime.MinValue) {

                        await _baseDataObjMgr._DeleteDataObjFromListAsync(target.DataID);
                    }
                }

                tran.Commit();
            } catch (Exception ex) {
                tran.Rollback();
                _syncing = false;
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"同期処理中に例外が発生しました: {ex.Message}"
                };
            }

            // =======================================================
            // 親子関係の再構築
            // =======================================================

            foreach (var obj in DataList) {

                SetParent(obj);
            }

            
            DataChanged?.Invoke();
            _syncing = false;
            // ★ 正常終了
            return new NxOperationResult<TKey> {
                Message = "Sync completed",
                RefreshedAt = syncResult.RefreshedAt
            };
        }

        public virtual async Task<NxOperationResult<TKey>> DeleteDataAsync(IEnumerable<TKey> dataIDs, bool softDelete = false, IDbTransaction tran = null) {
            var url = $"{ApiRoute}/Delete";

            HttpResponseMessage response;

            try {
                // ★ サーバーへ送信
                response = await _http.PostAsJsonAsync(url, dataIDs);
            } catch (Exception ex) {
                Console.WriteLine("DeleteData ERROR: HTTP通信失敗");
                Console.WriteLine(ex.Message);

                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.NetworkError,
                    Message = $"通信エラー: {ex.Message}",
                    FailedIds = dataIDs.ToList() // 全件失敗扱い
                };
            }

            if (!response.IsSuccessStatusCode) {
                Console.WriteLine($"DeleteData ERROR: APIステータス異常 ({response.StatusCode})");

                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.HttpError,
                    Message = $"HTTPエラー: {response.StatusCode}",
                    FailedIds = dataIDs.ToList()
                };
            }

            NxOperationResult<TKey>? op;

            try {
                // ★ API の NxOperationResult をそのまま取り出す
                op = await response.Content.ReadFromJsonAsync<NxOperationResult<TKey>>();
            } catch (Exception ex) {
                Console.WriteLine("DeleteData ERROR: JSONパース失敗");
                Console.WriteLine(ex.Message);

                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ResponseParseError,
                    Message = $"レスポンス解析エラー: {ex.Message}",
                    FailedIds = dataIDs.ToList()
                };
            }

            if (op == null) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.Unknown,
                    Message = "Delete結果が返されませんでした",
                    FailedIds = dataIDs.ToList()
                };
            }

            // ★ API 側で失敗している場合はローカル削除は行わない
            if (!op.Success) {
                return op;
            }

            // ★ 成功したIDだけローカルオブジェクト削除
            var failedLst = op.FailedIds ?? new List<TKey>();

            var successIds = dataIDs
                .Where(id => !failedLst.Contains(id))
                .ToList();

            foreach (var id in successIds) {
                try {
                    await _DeleteDataObjFromListAsync(id, softDelete);
                } catch (Exception ex) {
                    Console.WriteLine($"DeleteDataObjFromList ERROR: ID={id}");
                    Console.WriteLine(ex.Message);

                    // ローカル削除失敗も「失敗ID」に追加して返す
                    failedLst.Add(id);
                }
            }

            // ★ NxOperationResult を返す（APIの結果＋ローカル削除結果）
            return new NxOperationResult<TKey> {
                FailedIds = failedLst,
                Message = failedLst.Count == 0
                    ? "Delete completed"
                    : "Some items failed"
            };
        }



        protected virtual async Task<bool> _DeleteDataObjFromListAsync(TKey dataID, bool softDelete = false,IDbTransaction tran = null) {
            return await _baseDataObjMgr._DeleteDataObjFromListAsync(dataID, softDelete, tran);
        }

        public virtual string LoadMultipleDataAsJson(List<TKey> idList)
            => _baseDataObjMgr.LoadMultipleDataAsJson(idList);

        public virtual Task DistributeJsonDataAsync(string json)
            => _baseDataObjMgr.DistributeJsonDataAsync(json);

        public virtual void _RemoveFromList(BaseDataObj<TKey> obj)
            => _baseDataObjMgr._RemoveFromList(obj);

        // ★ 新規レコード用の空辞書（NxTypeMapper の型正本に基づく）
        protected Dictionary<string, object?> GetEmptySchema() {
            if (NxTypeMapper.Current == null)
                throw new Exception("NxTypeMap が初期化されていません。");

            return NxTypeMapper.Current.CreateEmptyRow(TblName);
        }
    }
}