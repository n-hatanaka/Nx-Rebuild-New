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
using System.Text;
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public class SyncNutPropertyObjMgr : SyncBaseDataObjMgr<NutritionProperty, SyncNutPropertyObj, int> {
        public override string ApiRoute => "NutProperty"; // 実際のAPIルート
        public SyncNutPropertyObjMgr(DbConnection db, HttpClient http, CustomAuthStateProvider auth, Guid tenantCode, Guid currentUserId)
            : base(db, http, auth, tenantCode, currentUserId)
        {
            _http = http;
            _auth = auth; 
            
            _baseDataObjMgr = new NutritionPropertysMgr(db, tenantCode, currentUserId);

        }

        //Initializeメソッドから呼び出される。栄養素プロパティのデータをDBから取得する。
        public override async Task<IEnumerable<Dictionary<string, object>>> LoadRecordsAsync() {
            // データベースから栄養素プロパティを取得する
            string sql = $"SELECT * FROM \"{TblName}\" WHERE tenant_code = @TenantCode ORDER BY SortNo;";

            return await DBcon.QueryAsync<Dictionary<string, object>>(sql, new { TenantCode = TenantCode });
        }


        public virtual async Task<bool> SyncData() {
            // ① API に世界線同期点を渡す
            var url = $"{ApiRoute}/sync";
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return false;

            // ② JSON を受け取る
            var json = await response.Content.ReadAsStringAsync();

            // ③ パース（API の返却構造に合わせた型を使用）
            var syncResult = JsonSerializer.Deserialize<SyncAllResult>(json);

            if (syncResult?.Items == null)
                return false;

            // ④ ループして DataObjMgr の世界線を更新
            foreach (var item in syncResult.Items) {
                var dataId = item.Key;
                var dataJson = item.Data;

                // ⑤ 既存 DataObj を探す
                var target = _baseDataObjMgr.DataList
                    .FirstOrDefault(x => x.DataID.Equals(dataId));

                if (target != null) {
                    // ⑥ 既存オブジェクトに世界線を流し込む
                    await target.JsonToTbl(dataJson);
                } else {
                    // ⑦ 新規作成
                    var newObj = _baseDataObjMgr.CreateNewDataObj();

                    // DataID をセット（必要なら）
                    newObj.DataID = dataId;

                    // ⑧ 世界線を流し込む
                    await newObj.JsonToTbl(dataJson);

                    var newSyncObj = CreateNewSyncDataObj(newObj);

                    // ⑨ DataList に追加
                    _baseDataObjMgr._dataList.Add(newSyncObj);

                }
            }

            return true;
        }

        public override async Task<List<int>> DeleteData(IEnumerable<int> dataIDs) {
            //削除は行わないので無効化
            throw new NotImplementedException();
        }
        public override async Task<bool> DeleteDataObj(int dataID) {
            //削除は行わないので無効化
            throw new NotImplementedException();    
        }
    }
}
