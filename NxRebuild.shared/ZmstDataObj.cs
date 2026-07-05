using System;
using System.Collections.Generic;
using System.Text;

namespace NxRebuild.shared {

    public class ColNameRecord {
        public Guid Group_ID { get; set; }
        public int No { get; set; }
        public int SortNo { get; set; }
        public string Col { get; set; }
        public string Name { get; set; }
        public string Format { get; set; }
        public int Digit { get; set; }
        public bool Visible { get; set; }
        public bool Nutrition { get; set; }
        public string PName1 { get; set; }
        public string PName2 { get; set; }
        public string PName3 { get; set; }
        public string PTanni { get; set; }
    }

    public class ZmstDataObj:BaseDataObj<int> {
        public string Z_code { get; set; }
        public KeyedList<string, object> ZmstExtData { get; set; }


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
            foreach (ColNameRecord NutProp in dbcon.NutritionPropertys) {
                if (NutProp.Nutrition)
                    ZmstExtData[NutProp.Col] = (float)record[NutProp.Col];
            }

        }

        public override async Task<LockStatus> DataOpen() {
            throw new NotImplementedException();
        }

        public override async Task<bool> DeleteQueryExec() {

            throw new NotImplementedException();

        }

        public override async Task<bool> SaveQueryExec() {
            throw new NotImplementedException();
        }

        public override async Task<string> TbltoJson() {
            throw new NotImplementedException();
        }

        public override async Task<bool> JsonToTable(string Json) {
            throw new NotImplementedException();
        }
    }

}
