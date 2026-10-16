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
        Task<LockStatus> PingLockAsync();
        HttpClient Http { get; set; }
        CustomAuthStateProvider Auth { get; set; }
        
    }


    //DataObjのラッパークラス。
    //サーバーとの同期機能を付与する。
    //UIからはインターフェース経由でaccessさせる。
    //オブジェクトの作成と削除はSyncマネージャークラスから行う。
    public abstract class SyncBaseDataObj<TKey> : ISyncBaseDataObj<TKey>, ISharedDataObj<TKey> {
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


        protected void _Validate(OperationType operationType) => _dataObj._Validate(operationType);

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
        public void CreateWorkingMemory(Dictionary<string, object?> wirkingRaw)
                        => _dataObj.CreateWorkingMemory(wirkingRaw);
        public void CreateWorkingDetailTables(List<List<Dictionary<string, object?>>>? subTables) 
                        => _dataObj.CreateWorkingDetailTables(subTables);
        public string TblToJson() => _dataObj.TblToJson();

        public string TblToJson(TKey dataId, IDbTransaction transaction) => _dataObj.TblToJson(dataId, transaction);

        public Task<bool> JsonToTblAsync(string json, IDbTransaction transaction) => _dataObj.JsonToTblAsync(json, transaction);



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

        protected void ApplyWorkingToRaw(Dictionary<string, object?> workingRaw) => _dataObj.ApplyWorkingToRaw(workingRaw);


        // --- SaveAsync（同期保存） ---
        // ===============================================================
        // SyncBase の Save は「ローカル先行 → サーバー反映」が正しい理由
        // ===============================================================
        //
        // 【1. SyncBase は単一ユーザーであり、編集中はローカルDBが正史である】
        //   - SyncBase は他ユーザーとデータを共有しない。
        //   - よってローカルDBが唯一の正史（canonical timeline）となる。
        //   - サーバー側は正史のバックアップ／同期先に過ぎない。
        //   - そのため、正史は必ずローカル側で先に確定させる必要がある。
        //
        // 【2. サーバーへ送る JSON は「ローカル正史の写像」である】
        //   - サーバーへ送信する JSON はローカルDBに書き込まれた内容を
        //     一度読み出して生成する。
        //   - つまりローカルDBが先に確定していないと正しい JSON が作れない。
        //   - この構造上、ローカル先行でなければ保存処理がが破綻する。
        //
        // 【3. ローカルトランザクションが長くても問題ない（単一ユーザー）】
        //   - SyncBase は単一ユーザーのため、ローカルDBのロック競合が発生しない。
        //   - ネットワーク待ちでトランザクションを開いたままでも UI が詰まらない。
        //   - 一般的な多ユーザーDBの制約を受けないため、ローカル先行が成立する。
        //
        // 【4. サーバー側の保存は「正史の投影更新」に過ぎない】
        //   - サーバー側はローカル正史の写像（projection timeline）。
        //   - サーバー保存が失敗した場合はローカル正史をロールバックすればよい。
        //   - 正史はローカルにあるため、サーバー失敗は致命傷にならない。
        //   - 次の同期でサーバー側を再構築できるため、データの整合性が保たれる。
        //
        // 【5. 結論：SyncBase の Save はローカル先行が唯一の正解】
        //   - ローカル正史を確定 → JSON 化 → サーバー反映。
        //   - サーバー失敗時はローカル正史をロールバック。
        //   - CRUDの因果が常に一貫し、保存の信頼性が最大化される。
        // ===============================================================

        public virtual async Task<bool> SaveAsync(
                        Dictionary<string, object?> workingRaw,
                        List<List<Dictionary<string, object?>>>? subTables = null) {
            using var tran = DBcon.BeginTransaction();

            try {
                // ★ 既存行の HardDelete（ローカル側のデータ初期化）
                if (!await _dataObj._HardDeleteQueryExecAsync(tran)) {
                    tran.Rollback();
                    return false;
                }

                // ★ 新規ならローカル側で仮ID採番（サーバー側で上書きされる可能性あり）
                workingRaw[IdColName] = _dataObj.EnsureIDForSave(tran);

                // ★ Working 全体保存（メイン＋サブ）
                if (!await _dataObj.SaveWorkingAsync(workingRaw, subTables, tran)) {
                    tran.Rollback();
                    return false;
                }

                // ★ サーバーへ送る JSON を生成
                var postJson = _dataObj.TblToJson(this.DataID, tran);
                var url = $"{ApiRoute}/Save/{DataID}";

                HttpResponseMessage response;

                try {
                    // ★ サーバーへ送信
                    response = await Http.PostAsJsonAsync(url, postJson);
                } catch (Exception ex) {
                    // ★ 通信レベルのエラー（ネットワーク断・タイムアウト等）
                    Console.WriteLine("SyncBaseDataObj.SaveAsync NETWORK ERROR:");
                    Console.WriteLine(ex.Message);
                    tran.Rollback();
                    return false;
                }

                // ★ HTTP ステータス異常（4xx / 5xx）
                if (!response.IsSuccessStatusCode) {
                    Console.WriteLine("SyncBaseDataObj.SaveAsync HTTP ERROR:");
                    Console.WriteLine($"StatusCode: {response.StatusCode}");
                    tran.Rollback();
                    return false;
                }

                SaveResponseDto<TKey>? respObj = null;

                try {
                    // ★ 応答を読む（JSON パース）
                    respObj = await response.Content.ReadFromJsonAsync<SaveResponseDto<TKey>>();
                } catch (Exception ex) {
                    // ★ JSON パース失敗（サーバー側の異常応答）
                    Console.WriteLine("SyncBaseDataObj.SaveAsync RESPONSE PARSE ERROR:");
                    Console.WriteLine(ex.Message);
                    tran.Rollback();
                    return false;
                }

                if (respObj == null) {
                    // ★ サーバーが空応答を返した場合
                    Console.WriteLine("SyncBaseDataObj.SaveAsync EMPTY RESPONSE ERROR:");
                    tran.Rollback();
                    return false;
                }

                // ★ コミット（ローカル世界線の確定）
                tran.Commit();

                // ★ Working → Raw 反映（ローカル正本）
                ApplyWorkingToRaw(workingRaw);

                // ★ サーバー正史の プロパティ をオブジェクトに反映
                await _dataObj._UpdatePropertiesFromTblAsync();

                return true;
            } catch (Exception ex) {
                Console.WriteLine("SyncBaseDataObj.SaveAsync ERROR:");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                tran.Rollback();
                return false;
            }
        }




        //ロック延命
        public async Task<LockStatus> PingLockAsync()
        {
            // ① サーバーにロック延命要求（locked_at を更新）
            var lockStatus = await SetLockAsync();

            // ② UI ローカル世界線に反映
            UpdateRawDataToTbl(new Dictionary<string, object> {
                { "locked_at", lockStatus.Locked_at },
                { "locked_by", lockStatus.LockedByUserId }
            }, null);

            // ③ サーバーから返ってきた LockStatus をそのまま返す
            return lockStatus;
        }
      
        public async Task<LockStatus> SetLockAsync()
        {
            // ① Auth から UserID と UserName を取得
            var authState = await Auth.GetAuthenticationStateAsync();
            var userName = authState.User.Identity?.Name;

            // ② ロック要求
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
                UpdateRawDataToTbl(new Dictionary<string, object> {
                                    { "locked_at", serverStatus.Locked_at }
                                },null);
            else
                UpdateRawDataToTbl(new Dictionary<string, object> {
                                    { "locked_at", null }
                                }, null);

            UpdateRawDataToTbl(new Dictionary<string, object> {
                                    { "locked_by", serverStatus.LockedByUserId }
                                }, null);   

            // ④ サーバーが返した LockStatus をそのまま返す
            return serverStatus;
        }

        //保存時にロック情報を更新する。サーバー側の正本のロック情報を取得して、UI側のローカルに反映する。
        protected async Task<LockStatus> _ReWriteLockInfoAsync(IDbTransaction tran,
                                                        Guid? Locked_by,
                                                        DateTime? Locked_at) 
                                        => await _dataObj._ReWriteLockInfoAsync(tran, Locked_by, Locked_at);

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
                _SetProperties(new Dictionary<string, object> {
                                    { "locked_at", serverStatus.Locked_at }
                                });
            else 
                _SetProperties(new Dictionary<string, object> {
                                    { "locked_at", null }
                                });
            _SetProperties(new Dictionary<string, object> {
                                    { "locked_by", serverStatus.LockedByUserId }
                                });

            // ④ サーバーが返した LockStatus をそのまま返す
            return serverStatus;
        }

        public async Task<LockStatus> DataOpenAsync() {
            // ① ローカルバリデーション（世界線の入口）
            _dataObj._Validate(OperationType.LocalEdit);

            HttpResponseMessage result;

            try {
                // ② サーバーへロック要求＋最新正本の取得
                var apiUrl = $"{ApiRoute}/DataOpenLatest";
                result = await Http.PostAsJsonAsync(apiUrl, DataID);
            } catch (Exception ex) {
                // ★ 通信レベルのエラー（ネットワーク断・タイムアウト等）
                Console.WriteLine("DataOpenAsync NETWORK ERROR:");
                Console.WriteLine(ex.Message);
                return new LockStatus { HasError = true, ErrorMessage = "通信エラーが発生しました" };
            }

            // ★ HTTP ステータス異常（4xx / 5xx）
            if (!result.IsSuccessStatusCode) {
                Console.WriteLine("DataOpenAsync HTTP ERROR:");
                Console.WriteLine($"StatusCode: {result.StatusCode}");
                return new LockStatus { HasError = true, ErrorMessage = "サーバー応答が異常です" };
            }

            DataOpenResult? openResult = null;

            try {
                // ★ 応答を読む（JSON パース）
                openResult = await result.Content.ReadFromJsonAsync<DataOpenResult>();
            } catch (Exception ex) {
                // ★ JSON パース失敗（サーバー側の異常応答）
                Console.WriteLine("DataOpenAsync RESPONSE PARSE ERROR:");
                Console.WriteLine(ex.Message);
                return new LockStatus { HasError = true, ErrorMessage = "応答の解析に失敗しました" };
            }

            // ③ LockStatus の確定（null やエラー時のフォールバック）
            var lockStatus = openResult?.LockStatus
                ?? new LockStatus { HasError = true, ErrorMessage = "ロック情報が取得できませんでした" };

            // ④ API 側でエラーならロック情報だけ返す（世界線を開かない）
            if (openResult == null || openResult.HasError)
                return lockStatus;

            // ⑤ 正本 JSON → DB 世界線へ反映（JsonToTbl）
            if (!string.IsNullOrEmpty(openResult.Json))
                await _dataObj.JsonToTblAsync(openResult.Json, null);

            // ⑥ 編集モードへ（DataOpen）
            await _dataObj.DataOpenAsync();

            // ⑦ UI ローカル世界線へロック情報を反映
            await _ReWriteLockInfoAsync(null, lockStatus.LockedByUserId, lockStatus.Locked_at);

            // ⑧ RawData を DB 世界線の正本で上書き（世界線の閉じ）
            await _dataObj._UpdatePropertiesFromTblAsync();

            // ⑨ ロック成功した LockStatus を返す
            return lockStatus;
        }



        public async Task<LockStatus> DataCloseAsync()
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

            // ③ ロック解除失敗なら DataCloseAsync しない
            if (lockStatus.IsLocked || lockStatus.HasError)
                return lockStatus;

            // ④ ロック解除成功後に DataCloseAsync 実行
            await _dataObj.DataCloseAsync();//ここでローカルのロック情報も削除される

            // ⑤ UIとDBのローカル状態を反映（MinValue は使わない）
            await _UpdatePropertiesFromTblAsync();
            return lockStatus;
        }


        public virtual async Task<bool> ReNameAsync(string newName)
        {
            // 1. バリデーション
            if (string.IsNullOrWhiteSpace(newName) || newName.Length > 20)
                return false;

            // Sync世界線 → API呼び出し
            var url = $"{ApiRoute}/ReName/{DataID}/{newName}";
            HttpResponseMessage response;

            try
            {
                response = await Http.PostAsync(url, null);
            }
            catch
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            // 5. API（正本）を取得
            var json = await response.Content.ReadAsStringAsync();

            Dictionary<string, object>? updatedRaw;

            try {
                var tmp = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (tmp == null)
                    return false;

                // NxTypeMapper で正本に変換
                updatedRaw = NxTypeMapper.ConvertRow(TblName, tmp);
            } catch {
                return false;
            }

            // 6. 正本を Baseに反映
            if (!UpdateRawDataToTbl(updatedRaw, null)) {
                return false;
            }

            return true;
        }

        //何かしらの更新をした時にサーバーから返ってきたメタデータ（TblName）を更新する際に使用する。
        //例えばRenameとか
        protected bool UpdateRawDataToTbl(Dictionary<string, object> raw, IDbTransaction transaction) {
            string sql = "";
            try {
                // 1. SET に使うカラムを抽出（主キーと tenant_code は除外）
                var columns = raw.Keys.Where(k => k != IdColName && k != "tenant_code");
                var setClause = string.Join(", ", columns.Select(c => $"{c} = @{c}"));

                // 2. SQL を生成
                sql =
                    $"UPDATE {TblName} SET {setClause} " +
                    $"WHERE {IdColName} = @DataID AND tenant_code = @TenantCode";

                // 3. DynamicParameters に変換（Dapper が確実に扱える形）
                // DynamicParameters は Dapper が最も確実に扱えるパラメータ形式。
                // 内部的には：
                // パラメータ名をそのまま保持
                // 型をそのまま保持
                // SQLite の大文字小文字問題を回避
                // JsonElement を自動でプリミティブに変換
                // Dictionary の曖昧さを完全排除
                // 次のようにすることで確実にSQLを実行できるようになる。
                var param = new DynamicParameters();

                // raw の全キーをパラメータとして追加
                foreach (var kv in raw) {
                    param.Add(kv.Key, kv.Value);
                }

                // WHERE 用のパラメータを追加
                param.Add("DataID", raw[IdColName]);
                param.Add("TenantCode", raw["tenant_code"]);

                // 4. UPDATE 実行
                DBcon.Execute(sql, param, transaction);

                // 5. UI の世界線を更新
                _SetProperties(raw);

                return true;
            } catch (Exception ex) {
                Console.WriteLine("UpdateRawDataToTbl ERROR:");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"SQL: {sql}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return false;
            }
        }



        protected virtual async Task _UpdatePropertiesFromTblAsync()
                                    => await _dataObj._UpdatePropertiesFromTblAsync();

        public void _SetProperties(IDictionary<string, object> record) 
                                    => _dataObj._SetProperties(record);       

        public virtual NxValidationResult _Validate(OperationType op, object? arg = null)
                                                => _dataObj._Validate(op, arg);

        public virtual async Task<bool> _DeleteQueryExecAsync(IDbTransaction transaction, bool softDelete) 
                                                => await _dataObj._DeleteQueryExecAsync(transaction, softDelete);


    }
}
