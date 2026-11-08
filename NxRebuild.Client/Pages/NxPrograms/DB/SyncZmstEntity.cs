using Dapper;
using NxRebuild.shared;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public class SyncZmstEntity : SyncBaseDataObj<int> , IBaseDataObj<int>, IZmstEntity {
        
        public override string ApiRoute => "Zmst";

        public string ZCode {
            get => ((IZmstEntity)_dataObj).ZCode;
        }


        public int LocalCode {
            get => ((IZmstEntity)_dataObj).LocalCode;
        }


        public float lss {
            get => ((IZmstEntity)_dataObj).lss;
        }


        public float Wca {
            get => ((IZmstEntity)_dataObj).Wca;
        }


        // --- ZmstEntity へのアクセスを簡略化するためのプロパティ ---
        private ZmstEntity Zmst => (ZmstEntity)_dataObj;

        public List<Dictionary<string, object?>> TanList => Zmst.TanList;

        public decimal? GetNutritionValue(string col) {
            return Zmst.GetNutritionValue(col);
        }

        public void SetNutritionValue(string col, decimal? value) {
            Zmst.SetNutritionValue(col, value);
        }

        public async Task _LoadDetailTablesFromRawAsync() => await Zmst._LoadDetailTablesFromRawAsync();




    }
}
