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


        public List<List<Dictionary<string, object?>>> SubTables => Zmst.SubTables;

        // --- ZmstEntity へのアクセスを簡略化するためのプロパティ ---
        private ZmstEntity Zmst => (ZmstEntity)_dataObj;

        public List<Dictionary<string, object?>> TanList => Zmst.TanList;

        public decimal? GetNutritionValue(string col) {
            return Zmst.GetNutritionValue(col);
        }

        public void SetNutritionValue(string col, decimal? value) {
            Zmst.SetNutritionValue(col, value);
        }

        public async Task LoadSubTablesFromRaw() => Zmst.LoadSubTablesFromRaw();

        // --- SaveAsync（同期保存） ---
        public override async Task<bool> SaveAsync(
                Dictionary<string, object?> workingRaw,
                List<List<Dictionary<string, object?>>>? subTables = null) 
        {
            using var tran = DBcon.BeginTransaction();

            try {
                if (!await Zmst.DeleteQueryExec(tran)) {
                    tran.Rollback();
                    return false;
                }


                workingRaw[IdColName] = Zmst.EnsureIDForSave(tran);


                // ★ Working 全体保存（メイン＋サブ）
                if (!await Zmst.SaveWorkingAsync(workingRaw, subTables, tran)) {
                    tran.Rollback();
                    return false;
                }

                var PostJSON = Zmst.TblToJson(this.DataID, tran);

                var url = $"{ApiRoute}/Save/{DataID}";

                HttpResponseMessage response;

                response = await Http.PostAsJsonAsync(url, PostJSON);

                // ★ コミット
                tran.Commit();

                // ★ 正本 Raw に反映
                ApplyWorkingToRaw(workingRaw);

                return true;
            } catch (Exception ex) {
                Console.WriteLine("SyncZmstEntity.SaveAsync ERROR:");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                tran.Rollback();
                return false;
            }

        }

        // --- ReName（同期世界線） ---
        public async Task<bool> ReName(string newName) {
            return await base.ReName(newName);
        }




    }
}
