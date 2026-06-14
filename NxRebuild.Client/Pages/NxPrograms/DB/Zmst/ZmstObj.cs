using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Npgsql;
using NxRebuild.shared;
using System.Data;
using System.Data.Common;
using System.Net.Http.Json; // GetFromJsonAsync用
using System.Text.Json;

namespace NxRebuild.Client.Pages.NxPrograms.DB.Zmst {
    public class ZmstObj : DataObj<int> {       
        public string Z_code { get; set; }
        public KeyedList<string, object> ZmstExtData  { get; set; }

        public ZmstObj(InMemoryDatabaseState db, AuthenticationStateProvider auth) : base(db, auth) {
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
    }
}
