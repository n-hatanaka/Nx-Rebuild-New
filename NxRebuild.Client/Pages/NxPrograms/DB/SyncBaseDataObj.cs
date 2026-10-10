using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using NxRebuild.Client.Pages.Auth;
using NxRebuild.shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB {

    public interface ISyncBaseDataObj<TKey>: IBaseDataObj<TKey> {
        HttpClient Http { get; set; }
        CustomAuthStateProvider Auth { get; set; }
        
    }


    //DataObjのラッパークラス。<br/>サーバーとの同期機能を付与する。
    //UIからはインターフェース経由でaccessさせる。
    //オブジェクトの作成と削除はマネージャークラスから行う。
    public abstract class SyncBaseDataObj<TKey> : ISyncBaseDataObj<TKey> {
        public BaseDataObj<TKey> _dataObj;

        protected BaseDataObj<TKey> DataObj { get => _dataObj;}

        public HttpClient Http { get; set; }
        public CustomAuthStateProvider Auth {  get; set; }
        public abstract string ApiRoute { get; } //←派生先で設定する事。
        public Guid CurrUsrID{ get => _dataObj.CurrUsrID; 
                                set => _dataObj.CurrUsrID = value; }

        public string ParentIDColName { get => _dataObj.ParentIDColName; }
        public TKey ParentID { get => _dataObj.ParentID; 
                        set => _dataObj.ParentID = value; }


        public IBaseDataObj<TKey>? ParentDataObj { get => _dataObj.ParentDataObj; 
                                            set => _dataObj.ParentDataObj = value; }


        public void Validate(OperationType operationType) => _dataObj.Validate(operationType);

        public SyncBaseDataObj() {
        }


        public IDbConnection DBcon {
            get => _dataObj.DBcon;
            set => _dataObj.DBcon = value;
        }

        public object SelfObjMgr { 
            get => _dataObj.SelfObjMgr;
            set => _dataObj.SelfObjMgr = value;
        }

        public virtual NxLocationKind LocationKind => _dataObj.LocationKind;

        // DataObjのメソッドへのアクセスラッパー
        public void CreateWorkingMemory(Dictionary<string, object?> wirkingRaw) => _dataObj.CreateWorkingMemory(wirkingRaw);
        public void CreateWorkingSubTables(List<List<Dictionary<string, object?>>>? subTables) => _dataObj.CreateWorkingSubTables(subTables);
        public string TblToJson() => _dataObj.TblToJson();

        public string TblToJson(TKey dataId, IDbTransaction transaction) => _dataObj.TblToJson(dataId, transaction);

        public Task<bool> JsonToTbl(string json, IDbTransaction transaction) => _dataObj.JsonToTbl(json, transaction);

        public abstract Task<bool> SaveAsync(
                Dictionary<string, object?> workingRaw,
                List<List<Dictionary<string, object?>>>? subTables = null);

        public TKey DataID {
            get => _dataObj.DataID;
            set => _dataObj.DataID = value;
        }

        public string DataName {

            get => _dataObj.DataName;
        }
        public NxDataType DataType {
            get => _dataObj.DataType;
            set => _dataObj.DataType = value;
        }

        public DateTime Update_at {
            get => _dataObj.Update_at;
        }
        public Guid LockerID {
            get => _dataObj.LockerID;
        }
        public DateTime LockedAt {
            get => _dataObj.LockedAt;
        }


        public Guid TenantCode {
            get => _dataObj.TenantCode;
            set => _dataObj.TenantCode = value;
        }

        public Dictionary<string, object> _rawData {
            get => _dataObj._rawData;
        }

        public string NameColName => _dataObj.NameColName;
        public string IdColName => _dataObj.IdColName;
        public string TblName => _dataObj.TblName;
        public string S_TblName => _dataObj.S_TblName;
        public string InfoTbl => _dataObj.InfoTbl;
        public string W_TblName => _dataObj.W_TblName;
        public string Ws_TblName => _dataObj.Ws_TblName;

        public void ApplyWorkingToRaw(Dictionary<string, object?> workingRaw) => _dataObj.ApplyWorkingToRaw(workingRaw);

        public virtual async Task Updateproperties() => await _dataObj.UpdatePropertiesFromTbl();

        //public virtual void SetBaseDataObj(BaseDataObj<TKey> baseObj) {
        //    _dataObj = baseObj ?? throw new ArgumentNullException(nameof(baseObj));
        //}

      
        //ロック延命
        public async Task<LockStatus> PingLockAsync()
        {
            // ① サーバーにロック延命要求（locked_at を更新）
            var lockStatus = await SetLockAsync();

            // ② UI ローカル世界線に反映
            SetProperties(new Dictionary<string, object> {
                { "locked_at", lockStatus.Locked_at },
                { "locked_by", lockStatus.LockedByUserId }
            });

            // ③ サーバーから返ってきた LockStatus をそのまま返す
            return lockStatus;
        }
      
        public async Task<LockStatus> SetLockAsync()
        {
            // ① Auth から UserID と UserName を取得
            var authState = await Auth.GetAuthenticationStateAsync();
            var userName = authState.User.Identity?.Name;

            // ② ロック要求（Exists は UI が決めない）
            var req = new LockCommand<TKey> {
                DataId = this.DataID,
                doLock = true
            };

            var url = $"{ApiRoute}/SetLockStatus";

            HttpResponseMessage response;

            try {
                response = await Http.PostAsJsonAsync(url, req);
            }
            catch (Exception ex) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = $"通信エラー: {ex.Message}",                    
                };
            }

            if (!response.IsSuccessStatusCode) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = $"HTTPエラー: {response.StatusCode}",
                };
            }

            LockStatus? serverStatus;

            try {
                serverStatus = await response.Content.ReadFromJsonAsync<LockStatus>();
            }
            catch (Exception ex) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = $"レスポンス解析エラー: {ex.Message}",
                };
            }

            if (serverStatus == null) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = "ロック情報が返されませんでした",
                };
            }

            // ③ UI 側のローカル状態を更新（世界線整合）
            if (serverStatus.Locked_at != null)
                SetProperties(new Dictionary<string, object> {
                                    { "locked_at", serverStatus.Locked_at }
                                });
            else
                SetProperties(new Dictionary<string, object> {
                                    { "locked_at", null }
                                });
            SetProperties(new Dictionary<string, object> {
                                    { "locked_by", serverStatus.LockedByUserId }
                                });

            // ④ サーバーが返した LockStatus をそのまま返す
            return serverStatus;
        }

        //保存時にロック情報を更新する。サーバー側の正本のロック情報を取得して、UI側のローカルに反映する。
        public async Task<LockStatus> ReWriteLockInfoAsync(IDbTransaction tran,
                                                        Guid? Locked_by,
                                                        DateTime? Locked_at) 
                                        => await _dataObj.ReWriteLockInfoAsync(tran, Locked_by, Locked_at);

        public async Task<LockStatus> SetUnLockAsync() {
            // ① Auth から UserID と UserName を取得
            var authState = await Auth.GetAuthenticationStateAsync();
            var userName = authState.User.Identity?.Name;

            // ② ロック要求（Exists は UI が決めない）
            var req = new LockCommand<TKey> {
                DataId = this.DataID,
                doLock = false
            };

            var url = $"{ApiRoute}/SetLockStatus";

            HttpResponseMessage response;

            try {
                response = await Http.PostAsJsonAsync(url, req);
            } catch (Exception ex) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = $"通信エラー: {ex.Message}",
                };
            }

            if (!response.IsSuccessStatusCode) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = $"HTTPエラー: {response.StatusCode}",
                };
            }

            LockStatus? serverStatus;

            try {
                serverStatus = await response.Content.ReadFromJsonAsync<LockStatus>();
            } catch (Exception ex) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = $"レスポンス解析エラー: {ex.Message}",
                };
            }

            if (serverStatus == null) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = "ロック情報が返されませんでした",
                };
            }

            // ③ UI 側のローカル状態を更新（世界線整合）
            if (serverStatus.Locked_at != null)
                SetProperties(new Dictionary<string, object> {
                                    { "locked_at", serverStatus.Locked_at }
                                });
            else 
                SetProperties(new Dictionary<string, object> {
                                    { "locked_at", null }
                                });
            SetProperties(new Dictionary<string, object> {
                                    { "locked_by", serverStatus.LockedByUserId }
                                });

            // ④ サーバーが返した LockStatus をそのまま返す
            return serverStatus;
        }

        public async Task<LockStatus> DataOpen()
        {
            // ① ローカルバリデーション（世界線の入口）
            _dataObj.Validate(OperationType.LocalEdit);

            // ② ロック要求（サーバーが最新の LockStatus を返す）
            var lockStatus = await SetLockAsync();

            // ③ ロック失敗（他人ロック or エラー）
            if (!lockStatus.CanEdit || lockStatus.HasError)
            {
                _rawData["locked_at"] = lockStatus.Locked_at;
                _rawData["locked_by"] = lockStatus.LockedByUserId;
                return lockStatus;
            }

            // ④ ロック成功 → ローカル編集世界線を開く
            await _dataObj.DataOpen();

            // ⑤ 最新データ取得（正本世界線 → ローカル世界線）
            // ★ API: DataOpenLatest を呼び出して正本の JSON を取得
            var apiUrl = $"{ApiRoute}/DataOpenLatest";
            var result = await Http.PostAsJsonAsync(apiUrl, DataID);
            var openResult = await result.Content.ReadFromJsonAsync<DataOpenResult>();

            if (openResult == null || openResult.HasError)
            {
                // API 側でエラーが起きた場合はロックだけ返す
                return lockStatus;
            }

            if (!string.IsNullOrEmpty(openResult.Json))
            {
                // ★ 正本 JSON → DB に反映
                await _dataObj.JsonToTbl(openResult.Json, null);


            }

            // ⑥ UI 側のローカル状態を反映
            ReWriteLockInfoAsync(null, lockStatus.LockedByUserId, lockStatus.Locked_at);
            // ★ RawData を正本で上書き
            await _dataObj.UpdatePropertiesFromTbl();

            // ⑦ ロック成功した LockStatus を返す
            return lockStatus;
        }



        public async Task<LockStatus> DataClose()
        {
            // ① Auth から UserID と UserName を取得
            var authState = await Auth.GetAuthenticationStateAsync();
            var userId = authState.User.FindFirst("sub")?.Value;
            var userName = authState.User.Identity?.Name;

            // ② ロック解除要求（Exists は UI が決めない）
            var req = new LockCommand<TKey> {
                DataId = this.DataID,
                doLock = false
            };

            var url = $"{ApiRoute}/SetLockStatus";

            HttpResponseMessage response;

            try {
                response = await Http.PostAsJsonAsync(url, req);
            }
            catch (Exception ex) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = $"通信エラー: {ex.Message}",
                };
            }

            if (!response.IsSuccessStatusCode) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = $"HTTPエラー: {response.StatusCode}",
                };
            }

            LockStatus? lockStatus;

            try {
                lockStatus = await response.Content.ReadFromJsonAsync<LockStatus>();
            }
            catch (Exception ex) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = $"レスポンス解析エラー: {ex.Message}",
                };
            }

            if (lockStatus == null) {
                return new LockStatus {
                    HasError = true,
                    ErrorMessage = "ロック解除情報が返されませんでした",
                };
            }

            // ③ ロック解除失敗なら DataClose しない
            if (lockStatus.IsLocked || lockStatus.HasError)
                return lockStatus;

            // ④ ロック解除成功後に DataClose 実行
            await _dataObj.DataClose();

            // ⑤ UIとDBのローカル状態を反映（MinValue は使わない）
            _dataObj.SetProperties(new Dictionary<string, object> {
                { "locked_at", null },
                { "locked_by", null }
            });
            _dataObj.ReWriteLockInfoAsync(null, null, null);
            return lockStatus;
        }


        public virtual async Task<bool> ReName(string newName)
        {
            // 1. バリデーション
            if (string.IsNullOrWhiteSpace(newName) || newName.Length > 20)
                return false;

            // 2. トランザクション開始（Base世界線）
            using IDbTransaction transaction = DBcon.BeginTransaction();

            // 3. Base世界線で仮更新
            var localUpdated = await _dataObj.ReNameQueryExec(newName, transaction);
            if (!localUpdated)
            {
                transaction.Rollback();
                return false;
            }

            // 4. Sync世界線 → API呼び出し
            var url = $"{ApiRoute}/ReName/{DataID}/{newName}";
            HttpResponseMessage response;

            try
            {
                response = await Http.PostAsync(url, null);
            }
            catch
            {
                transaction.Rollback();
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                transaction.Rollback();
                return false;
            }

            // 5. API世界線（正本）を取得
            var json = await response.Content.ReadAsStringAsync();

            Dictionary<string, object>? updatedRaw;

            try
            {
                updatedRaw = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (updatedRaw == null)
                {
                    transaction.Rollback();
                    return false;
                }
            }
            catch
            {
                transaction.Rollback();
                return false;
            }

            // 6. 正本世界線を Base世界線に反映
            if (!UpdateRawData(updatedRaw, transaction))
            {
                transaction.Rollback();
                return false;
            }

            // 7. プロパティ更新（正本世界線の反映）
            SetProperties(updatedRaw);

            // 8. 世界線を閉じる
            transaction.Commit();

            return true;
        }

        //何かしらの更新をした時にサーバーから返ってきたメタデータ（TblName）を更新する際に使用する。
        //例えばRenameとか
        public bool UpdateRawData(Dictionary<string, object> raw, IDbTransaction transaction) {
            try {
                // 1. カラム名と値を準備（IDColNameとtenant_code は SET に含めない）
                //raw（Dictionary）に入っている全カラムのうち、
                //主キー（IdColName）とテナントキー（tenant_code）を除外して
                //UPDATE の SET に使うカラムだけを抽出する。
                //要はこれは「SET に入れていいカラムだけを抽出している」
                var columns = raw.Keys.Where(k => k != IdColName && k != "tenant_code");
                var setClause = string.Join(", ", columns.Select(c => $"{c} = @{c}"));

                // 2. UPDATE 実行（世界線の一意性を保証）
                var sql =
                        $"UPDATE {TblName} SET {setClause} " +
                        $"WHERE {IdColName} = @DataID AND tenant_code = @TenantCode";


                // 3. インメモリの RawData を更新（正本世界線の吸収）
                DBcon.Execute(sql, raw, transaction);

                //4.　このオブジェクトのプロパティも更新する。これをやらないと、UI側の表示が変わらない。
                SetProperties(raw);

                return true;
            } catch {
                return false;
            }
        }

        public virtual async Task UpdatePropertiesFromTbl()
                                    => await _dataObj.UpdatePropertiesFromTbl();

        public void SetProperties(IDictionary<string, object> record) 
                                    => _dataObj.SetProperties(record);
        

        public virtual NxValidationResult Validate(OperationType op, object? arg = null)
                                                => _dataObj.Validate(op, arg);
    }
}
