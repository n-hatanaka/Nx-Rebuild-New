using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using NxRebuild.Client.Pages.Auth;
using NxRebuild.shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public class SyncNutPropertyObjMgr : SyncBaseDataObjMgr<NutritionProperty, SyncNutProperty, int> {
        public override string ApiRoute => "NutProperty"; // 実際のAPIルート
        public SyncNutPropertyObjMgr(IDbConnection db, HttpClient http, CustomAuthStateProvider auth, Guid tenantCode, Guid currentUserId)
            : base(db, http, auth, tenantCode, currentUserId)
        {
            _http = http;
            _auth = auth; 
            
            _baseDataObjMgr = new NutritionPropertiesMgr(db, tenantCode, currentUserId);

        }

        //Initializeメソッドから呼び出される。栄養素プロパティのデータをDBから取得する。
        public override async Task<IEnumerable<dynamic>> LoadRecordsAsync() {
            // データベースから栄養素プロパティを取得する
            string sql = $"SELECT * FROM \"{TblName}\" WHERE tenant_code = @TenantCode ORDER BY SortNo;";
            
            return await DBcon.QueryAsync<dynamic>(sql, new { TenantCode = TenantCode });
        }


        public override async Task<NxOperationResult<int>> SyncData() {
            // ① API に世界線同期点を渡す
            var url = $"{ApiRoute}/sync";

            HttpResponseMessage response;

            try {
                response = await _http.GetAsync(url);
            } catch (Exception ex) {
                return new NxOperationResult<int> {
                    OperationErrorCode = NxOperationErrorCode.NetworkError,
                    Message = $"通信エラー: {ex.Message}"
                };
            }

            if (!response.IsSuccessStatusCode) {
                return new NxOperationResult<int> {
                    OperationErrorCode = NxOperationErrorCode.HttpError,
                    Message = $"HTTPエラー: {response.StatusCode}"
                };
            }

            // ② NxOperationResult として受け取る（API の返却形と同じ）
            NxOperationResult<int>? syncResult;

            try {
                syncResult = await response.Content.ReadFromJsonAsync<NxOperationResult<int>>();
            } catch (Exception ex) {
                return new NxOperationResult<int> {
                    OperationErrorCode = NxOperationErrorCode.ResponseParseError,
                    Message = $"レスポンス解析エラー: {ex.Message}"
                };
            }

            if (syncResult == null) {
                return new NxOperationResult<int> {
                    OperationErrorCode = NxOperationErrorCode.Unknown,
                    Message = "同期結果が取得できませんでした"
                };
            }

            // サーバーがエラーコード付きで返してきた場合はそのまま返す
            if (!syncResult.Success)
                return syncResult;

            // ③ 反映（削除判定なし）。更新ゼロ件は SyncItems が空なだけで正常
            foreach (var item in syncResult.SyncItems) {

                var dataId = item.DataId;
                var dataJson = item.Data;

                var tran = DBcon.BeginTransaction();

                try {
                    var target = _baseDataObjMgr.Get(dataId);

                    if (target != null) {
                        var ok = await target.JsonToTblAsync(dataJson, tran);
                        if (!ok) {
                            tran.Rollback();
                            return new NxOperationResult<int> {
                                OperationErrorCode = NxOperationErrorCode.DbError,
                                Message = $"JsonToTblAsync 失敗: ID={dataId}"
                            };
                        }
                    } else {
                        var newSyncObj = _baseDataObjMgr.CreateNewDataObj(default(int));
                        newSyncObj.DataID = dataId;

                        var ok = await newSyncObj.JsonToTblAsync(dataJson, tran);
                        if (!ok) {
                            tran.Rollback();
                            return new NxOperationResult<int> {
                                OperationErrorCode = NxOperationErrorCode.DbError,
                                Message = $"JsonToTblAsync 新規作成失敗: ID={dataId}"
                            };
                        }

                        _baseDataObjMgr._dataList.Add(newSyncObj);
                    }

                    tran.Commit();
                } catch (Exception ex) {
                    tran.Rollback();
                    return new NxOperationResult<int> {
                        OperationErrorCode = NxOperationErrorCode.ServerException,
                        Message = $"同期処理中に例外が発生しました: {ex.Message}"
                    };
                }
            }

            return new NxOperationResult<int> {
                Message = "Sync completed",
                RefreshedAt = syncResult.RefreshedAt
            };
        }


        public override async Task<NxOperationResult<int>> DeleteDataAsync(IEnumerable<int> dataIDs, bool softDelete = false) {
            //削除は行わないので無効化
            throw new NotImplementedException();
        }
        protected override async Task<bool> _DeleteDataObjFromListAsync(int dataID, bool softDelete = false) {
            //削除は行わないので無効化
            throw new NotImplementedException();    
        }
    }
}
