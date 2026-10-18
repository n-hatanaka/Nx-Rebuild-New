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

        public virtual async Task<NxOperationResult<TKey>> SaveAsync(
                            Dictionary<string, object?> workingRaw,
                            List<List<Dictionary<string, object?>>>? subTables = null) {
            using var tran = DBcon.BeginTransaction();

            try {
                // ★ 既存行の HardDelete（ローカル側のデータ初期化）
                if (!await _dataObj._HardDeleteQueryExecAsync(tran)) {
                    tran.Rollback();
                    return new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.DbError,
                        Message = "ローカル初期化に失敗しました"
                    };
                }

                // ★ 新規ならローカル側で仮ID採番（サーバー側で上書きされる可能性あり）
                workingRaw[IdColName] = _dataObj.EnsureIDForSave(tran);

                // ★ Working 全体保存（メイン＋サブ）
                if (!await _dataObj.SaveWorkingAsync(workingRaw, subTables, tran)) {
                    tran.Rollback();
                    return new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.DbError,
                        Message = "ローカル保存に失敗しました"
                    };
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
                    return new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.NetworkError,
                        Message = $"通信エラー: {ex.Message}"
                    };
                }

                // ★ HTTP ステータス異常（4xx / 5xx）
                if (!response.IsSuccessStatusCode) {
                    Console.WriteLine("SyncBaseDataObj.SaveAsync HTTP ERROR:");
                    Console.WriteLine($"StatusCode: {response.StatusCode}");
                    tran.Rollback();
                    return new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.HttpError,
                        Message = $"HTTPエラー: {response.StatusCode}"
                    };
                }

                NxOperationResult<TKey>? op = null;

                try {
                    // ★ 応答を読む（JSON パース）
                    op = await response.Content.ReadFromJsonAsync<NxOperationResult<TKey>>();
                } catch (Exception ex) {
                    // ★ JSON パース失敗（サーバー側の異常応答）
                    Console.WriteLine("SyncBaseDataObj.SaveAsync RESPONSE PARSE ERROR:");
                    Console.WriteLine(ex.Message);
                    tran.Rollback();
                    return new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.ResponseParseError,
                        Message = $"レスポンス解析エラー: {ex.Message}"
                    };
                }

                if (op == null) {
                    tran.Rollback();
                    return new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.Unknown,
                        Message = "サーバー応答が空です"
                    };
                }

                // ★ API 側でエラーならローカル正史をロールバックして返す
                if (!op.Success) {
                    tran.Rollback();
                    return op;
                }

                // ★ コミット（ローカル世界線の確定）
                tran.Commit();

                // ★ Working → Raw 反映（ローカル正史）
                ApplyWorkingToRaw(workingRaw);

                // ★ サーバー正史の RawData をローカルへ反映
                if (op.RawData != null)
                    UpdateRawDataToTbl(op.RawData, null);

                // ★ サーバー正史のプロパティをオブジェクトに反映
                await _dataObj._UpdatePropertiesFromTblAsync();

                // ★ 正常終了
                return new NxOperationResult<TKey> {
                    RawData = op.RawData,
                    NewID = op.NewID,
                    UpdateAt = op.UpdateAt,
                    Message = "Save completed"
                };
            } catch (Exception ex) {
                Console.WriteLine("SyncBaseDataObj.SaveAsync ERROR:");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                tran.Rollback();
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"SaveAsync failed: {ex.Message}"
                };
            }
        }




        public async Task<NxOperationResult<TKey>> SetLockAsync() {
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
                // ★ サーバーへ送信
                response = await Http.PostAsJsonAsync(url, req);
            } catch (Exception ex) {
                // ★ 通信レベルのエラー（ネットワーク断・タイムアウト等）
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.NetworkError,
                    Message = $"通信エラー: {ex.Message}"
                };
            }

            // ★ HTTP ステータス異常（4xx / 5xx）
            if (!response.IsSuccessStatusCode) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.HttpError,
                    Message = $"HTTPエラー: {response.StatusCode}"
                };
            }

            NxOperationResult<TKey>? op;

            try {
                // ★ 応答を読む（JSON パース）
                op = await response.Content.ReadFromJsonAsync<NxOperationResult<TKey>>();
            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ResponseParseError,
                    Message = $"レスポンス解析エラー: {ex.Message}"
                };
            }

            if (op == null) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.Unknown,
                    Message = "ロック情報が返されませんでした"
                };
            }

            // ③ UI 側のローカル状態を更新
            if (op.LockStatus != null) {
                try {
                    if (!UpdateRawDataToTbl(new Dictionary<string, object> {
                                    { "locked_at", op.LockStatus.Locked_at },
                                    { "locked_by", op.LockStatus.LockedByUserId }
                                    }, null)) {
                        return new NxOperationResult<TKey> {
                            OperationErrorCode = NxOperationErrorCode.DbError,
                            Message = "SetLockAsync:UpdateRawDataToTbl ERROR:ローカルDBの更新に失敗しました"
                        };
                    }
                } catch (Exception ex) {
                    return new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.DbError,
                        Message = $"SetLockAsync:UpdateRawDataToTbl ERROR:ローカルDB更新中に例外: {ex.Message}"
                    };
                }
            }

            // ④ サーバーが返した NxOperationResult をそのまま返す
            return op;
        }


        //保存時にロック情報を更新する。サーバー側の正本のロック情報を取得して、UI側のローカルに反映する。
        protected async Task<LockStatus> _ReWriteLockInfoAsync(IDbTransaction tran,
                                                        Guid? Locked_by,
                                                        DateTime? Locked_at) 
                                        => await _dataObj._ReWriteLockInfoAsync(tran, Locked_by, Locked_at);

        public async Task<NxOperationResult<TKey>> SetUnLockAsync() {
            // ① Auth から UserID と UserName を取得
            var authState = await Auth.GetAuthenticationStateAsync();
            var userName = authState.User.Identity?.Name;

            // ② ロック解除要求
            var req = new LockCommand<TKey> {
                DataId = this.DataID,
                doLock = false
            };

            var url = $"{ApiRoute}/SetLockStatus";

            HttpResponseMessage response;

            try {
                // ★ サーバーへ送信
                response = await Http.PostAsJsonAsync(url, req);
            } catch (Exception ex) {
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

            NxOperationResult<TKey>? op;

            try {
                op = await response.Content.ReadFromJsonAsync<NxOperationResult<TKey>>();
            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ResponseParseError,
                    Message = $"レスポンス解析エラー: {ex.Message}"
                };
            }

            if (op == null) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.Unknown,
                    Message = "ロック解除情報が返されませんでした"
                };
            }

            // ③ UI 側のローカル状態を更新
            if (op.LockStatus != null) {
                try {
                    if (!UpdateRawDataToTbl(new Dictionary<string, object> {
                                    { "locked_at", op.LockStatus.Locked_at },
                                    { "locked_by", op.LockStatus.LockedByUserId }
                                    }, null)) {
                        return new NxOperationResult<TKey> {
                            OperationErrorCode = NxOperationErrorCode.DbError,
                            Message = "SetUnLockAsync:UpdateRawDataToTbl ERROR:ローカルDBの更新に失敗しました"
                        };
                    }
                } catch (Exception ex) {
                    return new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.DbError,
                        Message = $"SetUnLockAsync:UpdateRawDataToTbl ERROR:ローカルDB更新中に例外: {ex.Message}"
                    };
                }
            }

            return op;
        }


        public async Task<NxOperationResult<TKey>> DataOpenAsync() {
            // ① ローカルバリデーション
            var v = _dataObj._Validate(OperationType.LocalEdit);
            if (!v.Success) {
                return new NxOperationResult<TKey> {
                    ValidationErrorCode = v.ValidationErrorCode,
                    Message = v.Message
                };
            }

            HttpResponseMessage result;

            try {
                // ② サーバーへロック要求＋最新正本の取得
                var apiUrl = $"{ApiRoute}/DataOpenLatest";
                result = await Http.PostAsJsonAsync(apiUrl, DataID);
            } catch (Exception ex) {
                // ★ 通信レベルのエラー（ネットワーク断・タイムアウト等）
                Console.WriteLine("DataOpenAsync NETWORK ERROR:");
                Console.WriteLine(ex.Message);
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.NetworkError,
                    Message = "通信エラーが発生しました"
                };
            }

            // ★ HTTP ステータス異常（4xx / 5xx）
            if (!result.IsSuccessStatusCode) {
                Console.WriteLine("DataOpenAsync HTTP ERROR:");
                Console.WriteLine($"StatusCode: {result.StatusCode}");
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.HttpError,
                    Message = "サーバー応答が異常です"
                };
            }

            NxOperationResult<TKey>? openResult = null;

            try {
                // ★ 応答を読む（JSON パース）
                openResult = await result.Content.ReadFromJsonAsync<NxOperationResult<TKey>>();
            } catch (Exception ex) {
                // ★ JSON パース失敗（サーバー側の異常応答）
                Console.WriteLine("DataOpenAsync RESPONSE PARSE ERROR:");
                Console.WriteLine(ex.Message);
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ResponseParseError,
                    Message = "応答の解析に失敗しました"
                };
            }

            if (openResult == null) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.Unknown,
                    Message = "ロック情報が取得できませんでした"
                };
            }

            var lockStatus = openResult.LockStatus
                ?? new LockStatus { HasError = true, ErrorMessage = "ロック情報が取得できませんでした" };

            // ③ API 側でエラーならロック情報だけ返す
            if (!openResult.Success)
                return new NxOperationResult<TKey> {
                    ValidationErrorCode = openResult.ValidationErrorCode,
                    OperationErrorCode = openResult.OperationErrorCode,
                    Message = openResult.Message,
                    LockStatus = lockStatus
                };

            // ⑤ 正本 JSON → DB へ反映（JsonToTbl）
            if (openResult.RawData != null && openResult.RawData.TryGetValue("json", out var jsonObj)
                && jsonObj is string jsonStr && !string.IsNullOrEmpty(jsonStr)) {
                await _dataObj.JsonToTblAsync(jsonStr, null);
            }

            // ⑥ 編集モードへ（DataOpen）
            await _dataObj.DataOpenAsync();

            // ⑦ UI ローカルへロック情報を反映
            await _ReWriteLockInfoAsync(null, lockStatus.LockedByUserId, lockStatus.Locked_at);

            // ⑧ RawData を DB 正本で上書き
            await _dataObj._UpdatePropertiesFromTblAsync();

            // ⑨ NxOperationResult として返す（LockStatus を含む）
            return new NxOperationResult<TKey> {
                LockStatus = lockStatus,
                Message = "Data opened successfully"
            };
        }




        public async Task<NxOperationResult<TKey>> DataCloseAsync() {
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
                // ★ サーバーへ送信
                response = await Http.PostAsJsonAsync(url, req);
            } catch (Exception ex) {
                // ★ 通信レベルのエラー（ネットワーク断・タイムアウト等）
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.NetworkError,
                    Message = $"通信エラー: {ex.Message}"
                };
            }

            // ★ HTTP ステータス異常（4xx / 5xx）
            if (!response.IsSuccessStatusCode) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.HttpError,
                    Message = $"HTTPエラー: {response.StatusCode}"
                };
            }

            NxOperationResult<TKey>? op;

            try {
                // ★ 応答を読む（JSON パース）
                op = await response.Content.ReadFromJsonAsync<NxOperationResult<TKey>>();
            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ResponseParseError,
                    Message = $"レスポンス解析エラー: {ex.Message}"
                };
            }

            if (op == null) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.Unknown,
                    Message = "ロック解除情報が返されませんでした"
                };
            }

            var lockStatus = op.LockStatus;

            // ③ ロック解除失敗なら DataCloseAsync しない
            if (!op.Success || lockStatus == null || lockStatus.IsLocked || lockStatus.HasError) {
                return new NxOperationResult<TKey> {
                    ValidationErrorCode = op.ValidationErrorCode,
                    OperationErrorCode = op.OperationErrorCode,
                    Message = op.Message,
                    LockStatus = lockStatus
                };
            }

            try {
                // ④ ロック解除成功後に DataCloseAsync 実行
                await _dataObj.DataCloseAsync(); //ここでローカルのロック情報も削除される

                // ⑤ UIとDBのローカル状態を反映（MinValue は使わない）
                await _UpdatePropertiesFromTblAsync();
            } catch (Exception ex) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = $"DataCloseAsync failed: {ex.Message}",
                    LockStatus = lockStatus
                };
            }

            // ⑥ 正常終了
            return new NxOperationResult<TKey> {
                LockStatus = lockStatus,
                Message = "Data closed successfully"
            };
        }



        public virtual async Task<NxOperationResult<TKey>> ReNameAsync(string newName) {
            // 1. バリデーション
            if (string.IsNullOrWhiteSpace(newName) || newName.Length > 20) {
                return new NxOperationResult<TKey> {
                    ValidationErrorCode = NxValidationErrorCode.RenameEmpty,
                    Message = "名前が空、または長すぎます"
                };
            }

            // Sync世界線 → API呼び出し
            var url = $"{ApiRoute}/ReName/{DataID}/{newName}";
            HttpResponseMessage response;

            try {
                // ★ サーバーへ送信
                response = await Http.PostAsync(url, null);
            } catch (Exception ex) {
                // ★ 通信レベルのエラー（ネットワーク断・タイムアウト等）
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.NetworkError,
                    Message = $"通信エラー: {ex.Message}"
                };
            }

            // ★ HTTP ステータス異常（4xx / 5xx）
            if (!response.IsSuccessStatusCode) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.HttpError,
                    Message = $"HTTPエラー: {response.StatusCode}"
                };
            }

            NxOperationResult<TKey>? op;

            try {
                // ★ API（正本）を取得
                op = await response.Content.ReadFromJsonAsync<NxOperationResult<TKey>>();
            } catch (Exception ex) {
                // ★ JSON パース失敗（サーバー側の異常応答）
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.ResponseParseError,
                    Message = $"レスポンス解析エラー: {ex.Message}"
                };
            }

            if (op == null) {
                return new NxOperationResult<TKey> {
                    OperationErrorCode = NxOperationErrorCode.Unknown,
                    Message = "Rename結果が返されませんでした"
                };
            }

            // ★ API 側でエラーならそのまま返す
            if (!op.Success)
                return op;

            // 6. 正本を Baseに反映
            if (op.RawData != null) {
                try {
                    if (!UpdateRawDataToTbl(op.RawData, null)) {
                        return new NxOperationResult<TKey> {
                            OperationErrorCode = NxOperationErrorCode.DbError,
                            Message = "ReNameAsync:UpdateRawDataToTbl ERROR:ローカルDBの更新に失敗しました"
                        };
                    }
                } catch (Exception ex) {
                    return new NxOperationResult<TKey> {
                        OperationErrorCode = NxOperationErrorCode.DbError,
                        Message = $"ReNameAsync:UpdateRawDataToTbl ERROR:ローカルDB更新中に例外: {ex.Message}"
                    };
                }
            }

            // ★ 正常終了
            return new NxOperationResult<TKey> {
                RawData = op.RawData,
                Message = "Rename completed"
            };
        }


        //何かしらの更新をした時にサーバーから返ってきたメタデータ（TblName）を更新する際に使用する。
        //例えばRenameとか
        protected bool UpdateRawDataToTbl(Dictionary<string, object> raw, IDbTransaction transaction) {
            string sql = "";
     
            raw = NxTypeMapper.ConvertRow(TblName, raw);

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
            var rows = DBcon.Execute(sql, param, transaction);

            if (rows == 0) {
                Console.WriteLine($"UpdateRawDataToTbl: NO ROW UPDATED (DataID={raw[IdColName]})");
                return false;
            }

            if (rows > 1) {
                Console.WriteLine($"WORLDLINE BROKEN: {rows} rows updated!");
                return false; // or throw
            }

            // 5. UI の世界線を更新
            _SetProperties(raw);

            return true;
        }



        protected virtual async Task _UpdatePropertiesFromTblAsync()
                                    => await _dataObj._UpdatePropertiesFromTblAsync();

        public void _SetProperties(IDictionary<string, object> record) 
                                    => _dataObj._SetProperties(record);       

        public virtual NxOperationResult<TKey> _Validate(OperationType op, object? arg = null)
                                                => _dataObj._Validate(op, arg);

        public virtual async Task<bool> _DeleteQueryExecAsync(IDbTransaction transaction, bool softDelete) 
                                                => await _dataObj._DeleteQueryExecAsync(transaction, softDelete);


    }
}
