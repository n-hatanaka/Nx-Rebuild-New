using System.Data;
using System.Text.Json;
using System.Transactions;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public class SyncNutPropertyObj : SyncBaseDataObj<int> {
        // 抽象メンバーを実装
        public override string ApiRoute => "NutProperty"; // 実際のルート

        public bool Visible
        {
            get => _dataObj.Visible;          // Base世界線の状態を読む
            set
            {
                _dataObj.Visible = value; 
            }
        }
         // ★追加：Nutrition（ラップドから取るだけ）
         public bool Nutrition
         {
             get => _dataObj.Nutrition;
         }
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
                        SetPropertys(updatedRaw);
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
