using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.shared;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Text.Json;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NxRebuild.Client.Pages.NxPrograms.DB.Zmst {
    public class ZmstObj : DataObj<int> {

        public string Z_code { get; set; }

        public KeyedList<string, object> ZmstExtData  { get; set; }

        public ZmstObj(HttpClient Http, InMemoryDatabaseState db, AuthenticationStateProvider auth) : base(Http, db, auth) {
        }
        protected override void Initialize() {
            _idColName = "LocalCode";
            _nameColName = "Z_name";
            _tblName = "Zmst";
            _datatype = NxDataType.Zairyou;
        }

        public override void SetPropertys(KeyedList<string, object> record) {
            base.SetPropertys(record);
            if ((bool)record["deleted"]) return;//論理削除済のデータはロードしない

            ZmstExtData["Z_code"] = (string)record["Z_code"];
            ZmstExtData["gun_cd"] = (int)record["gun_cd"];
            ZmstExtData["Tou_cd"] = (int)record["Tou_cd"];
            ZmstExtData["Zin_cd"] = (int)record["Zin_cd"];
            ZmstExtData["D_S"] = (int)record["D_S"];
            ZmstExtData["iss"] = (float)record["iss"];
            ZmstExtData["Wca"] = (float)record["Wca"];
            ZmstExtData["Txt"] = (string)record["Txt"];
            foreach(ColNameRecord NutProp in _dbstate.NutritionPropertys) {
                if (NutProp.Nutrition)
                    ZmstExtData[NutProp.Col] = (float)record[NutProp.Col];
            }

        }

        public override async Task<LockStatus> DataOpen() {
            try {
                var response = await _http.GetAsync($"Z_mst/DataOpen/{this.DataID}/{this.Update_at}");

                // 1. ステータスコードが成功以外の場合
                if (!response.IsSuccessStatusCode) {
                    if (response.StatusCode == HttpStatusCode.Conflict) // 409 Conflict
                    {
                        Console.WriteLine("今は誰かが編集中のようです");
                        return null;
                    }
                    throw new HttpRequestException($"通信エラー: {response.StatusCode}");
                }

                // 2. 204 No Content のチェック
                if (response.StatusCode == HttpStatusCode.NoContent) {
                    Console.WriteLine("データなし（更新なし等）");
                    return null;
                }

                // 3. JSON の読み込み
                
                var jsonString = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(jsonString);
                JsonElement root = doc.RootElement;

                // カラム名がそのままプロパティキーになる
                string groupName = root.GetProperty("Group_Id").GetString();
                int dataId = root.GetProperty("Data_ID").GetInt32();

            } catch (Exception ex) {
                Console.WriteLine($"エラーが発生しました: {ex.Message}");
                throw;
            }
        }
}
