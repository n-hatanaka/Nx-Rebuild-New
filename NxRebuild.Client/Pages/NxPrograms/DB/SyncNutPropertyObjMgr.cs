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
            string sql = $"SELECT * FROM \"{TableName}\" WHERE tenant_code = @TenantCode ORDER BY SortNo;";

            return await DBcon.QueryAsync<Dictionary<string, object>>(sql);
        }

    }
}
