using Dapper;
using NxRebuild.shared;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public class SyncZmstEntity : SyncBaseDataObj<int> {
        public override string ApiRoute => "/api/Zmst";

        // --- ZmstEntity の具象インスタンスを生成 ---
        protected override BaseDataObj<int> CreateBaseDataObj() {
            return new ZmstEntity();
        }

        // --- ZmstEntity へのアクセスを簡略化するためのプロパティ ---
        private ZmstEntity Zmst => (ZmstEntity)_dataObj;

        // --- tan_m のリストを直接参照できるようにする ---
        public List<TanMEntity> TanList => Zmst.TanList;

        // --- tan_m のロード（サーバーではなくローカルDBから） ---
        public async Task LoadTanMAsync() {
            await Zmst.LoadTanMAsync();
        }

        // --- SaveAsync（同期世界線） ---
        public async Task<bool> SaveAsync() {
            // 1. Base世界線で保存（ローカルDB）
            bool ok = await _dataObj.SaveAsync();
            if (!ok)
                return false;

            // 2. Sync世界線 → APIへ送信
            var json = _dataObj.TblToJson();

            var url = $"{ApiRoute}/Save/{DataID}";
            HttpResponseMessage response;

            try {
                response = await Http.PostAsJsonAsync(url, json);
            } catch {
                return false;
            }

            if (!response.IsSuccessStatusCode)
                return false;

            // 3. APIが返す正本世界線の JSON をローカルDBに反映
            var updatedJson = await response.Content.ReadAsStringAsync();
            var updatedRecords = System.Text.Json.JsonSerializer
                .Deserialize<List<Dictionary<string, object>>>(updatedJson);

            if (updatedRecords == null)
                return false;

            using IDbTransaction tran = DBcon.BeginTransaction();

            try {
                // JSON → テーブル反映（Base世界線）
                foreach (var record in updatedRecords) {
                    string tbl = record.ContainsKey("_table_type")
                        ? record["_table_type"].ToString()
                        : Zmst.TblName;

                    var normalized = NxTypeMapper.ConvertRow(tbl, record);

                    string columns = string.Join(", ", normalized.Keys);
                    string values = string.Join(", ", normalized.Keys.Select(k => "@" + k));

                    DBcon.Execute(
                        $"INSERT OR REPLACE INTO {tbl} ({columns}) VALUES ({values})",
                        normalized,
                        tran
                    );
                }

                tran.Commit();
            } catch {
                tran.Rollback();
                return false;
            }

            // 4. メモリ上のプロパティを更新
            await Updateproperties();

            return true;
        }

        // --- ReName（同期世界線） ---
        public async Task<bool> ReName(string newName) {
            return await base.ReName(newName);
        }

        // --- DataOpen（排他制御） ---
        public async Task<LockStatus> DataOpen() {
            return await _dataObj.DataOpen();
        }
    }
}
