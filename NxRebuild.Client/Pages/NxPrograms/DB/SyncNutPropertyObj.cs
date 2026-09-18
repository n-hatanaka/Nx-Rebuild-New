using System.Data;
using System.Text.Json;
using System.Transactions;

using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public class SyncNutProperty : SyncBaseDataObj<int> , INutritionProperty {
        // 抽象メンバーを実装
        public override string ApiRoute => "NutProperty"; // 実際のルート

        protected override BaseDataObj<int> CreateBaseDataObj() {
            return new NutritionProperty();
        }

        private NutritionProperty NP => (NutritionProperty)_dataObj;

        // --- 読み取り専用プロパティ（Base世界線の正本をラップ） ---

        public int SortNo => NP.SortNo;
        public string Col => NP.Col;
        public string Format => NP.Format;
        public int Digit => NP.Digit;

        public string PName1 => NP.PName1;
        public string PName2 => NP.PName2;
        public string PName3 => NP.PName3;
        public string PTanni => NP.PTanni;

        // --- 編集可能プロパティ（Visibleのみ） ---
        public bool Visible {
            get => NP.Visible;
            set => NP.Visible = value;
        }

        // --- 読み取り専用（栄養素として扱うか） ---
        public bool Nutrition => NP.Nutrition;
        public override async Task<bool> ReName(string newName) {
            //リネームは行わないので無効化
            throw new NotImplementedException();
        }

        public async Task<bool> NutVisibleChg(bool NewVal) {
            var url = $"{ApiRoute}/Visible/{DataID}/{NewVal}";
            HttpResponseMessage response;
            try {
                response = await Http.PostAsync(url, null);
            } catch {
                return false; // 通信失敗
            }

            if (!response.IsSuccessStatusCode) {
                // API側で失敗 
                return false;
            }

            // 5. APIが返す JSONを取得
            var json = await response.Content.ReadAsStringAsync();

            // デシリアライズ（トランザクション開始前に）
            Dictionary<string, object>? updatedRaw;
            try {
                updatedRaw = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (updatedRaw == null) return false;
            } catch {
                return false;
            }

            // トランザクションを using で開始して処理
            using (var transaction = DBcon.BeginTransaction()) {
                try {
                    // サーバーからメタデータを受け取りクライアントのテーブルを更新
                    if (UpdateRawData(updatedRaw, transaction)) {
                        // 保持しているプロパティも更新
                        Setproperties(updatedRaw);
                    } else {
                        transaction.Rollback();
                        return false;
                    }

                    // コミットして更新を完了する
                    transaction.Commit();
                    return true;
                } catch {
                    transaction.Rollback();
                    return false;
                }
            }
        }
    }
}
