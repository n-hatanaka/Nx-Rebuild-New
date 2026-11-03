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
        public SyncNutPropertyObjMgr(IDbConnection db, HttpClient? http, CustomAuthStateProvider auth, Guid tenantCode, Guid currentUserId)
            : base(db, http, auth, tenantCode, currentUserId)
        {
            
            _baseDataObjMgr = new NutritionPropertiesMgr(db, tenantCode, currentUserId);
        }

        //Initializeメソッドから呼び出される。栄養素プロパティのデータをDBから取得する。
        public override async Task<IEnumerable<dynamic>> LoadRecordsAsync() {
            // データベースから栄養素プロパティを取得する
            string sql = $"SELECT * FROM \"{TblName}\" WHERE tenant_code = @TenantCode ORDER BY SortNo;";
            
            return await DBcon.QueryAsync<dynamic>(sql, new { TenantCode = TenantCode });
        }

        public override async Task<NxOperationResult<int>> SyncData() {
            using var slot = NxSyncControl.For(DBcon).TryBeginSync();

            if (slot == null) {
                return new NxOperationResult<int> {
                    OperationErrorCode = NxOperationErrorCode.TransactionFailed,
                    Message = "操作中のため同期をスキップしました"
                };
            }

            try {
                // サーバーから List<Dictionary<string, object>> を直接受け取る
                var rows = await _http.GetFromJsonAsync<List<Dictionary<string, object>>>($"{ApiRoute}/sync");

                if (rows == null) {
                    return new NxOperationResult<int> {
                        OperationErrorCode = NxOperationErrorCode.ResponseParseError,
                        Message = "同期データを取得できませんでした"
                    };
                }

                using var tran = DBcon.BeginTransaction();

                try {
                    foreach (var row in rows) {
                        var dataId = Convert.ToInt32(row["No"]);

                        var target = _baseDataObjMgr.Get(dataId);

                        if (target == null) {
                            target = _baseDataObjMgr.CreateNewDataObj(default);
                            target.DataID = dataId;
                            _baseDataObjMgr._dataList.Add(target);
                        }

                        // テーブル名やカラムマッピングに合わせて調整
                        ((SyncNutProperty)target)._SetProperties(
                            NxTypeMapper.ConvertRow("ColName", row)
                        );

                        await ((SyncNutProperty)target)._dataObj.SaveWorkingPrimaryAsync(
                            new Dictionary<string, object?>(((SyncNutProperty)target)._rawData),
                            tran
                        );
                    }

                    tran.Commit();

                    return new NxOperationResult<int> {
                        Message = "Sync completed"
                    };
                } catch (Exception ex) {
                    tran.Rollback();

                    return new NxOperationResult<int> {
                        OperationErrorCode = NxOperationErrorCode.DbError,
                        Message = ex.Message
                    };
                }
            } catch (HttpRequestException ex) {
                return new NxOperationResult<int> {
                    OperationErrorCode = NxOperationErrorCode.NetworkError,
                    Message = ex.Message
                };
            } catch (Exception ex) {
                return new NxOperationResult<int> {
                    OperationErrorCode = NxOperationErrorCode.ServerException,
                    Message = ex.Message
                };
            }
        }


        public override async Task<NxOperationResult<int>> DeleteDataAsync(IEnumerable<int> dataIDs, bool softDelete = false, IDbTransaction tran = null) {
            //削除は行わないので無効化
            throw new NotImplementedException();
        }
        protected override async Task<bool> _DeleteDataObjFromListAsync(int dataID, bool softDelete = false, IDbTransaction tran = null) {
            //削除は行わないので無効化
            throw new NotImplementedException();    
        }
    }
}
