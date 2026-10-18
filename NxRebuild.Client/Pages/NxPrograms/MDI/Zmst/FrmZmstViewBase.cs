using Microsoft.AspNetCore.Components;
using NxRebuild.Client.Pages.NxPrograms.DB;
using NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View;
using NxRebuild.Client.Services;
using Microsoft.JSInterop;

using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.Zmst {

    // ★ AknTreeListViewBase<TObj, TKey> に合わせて修正
    public class FrmZmstViewBase : AknTreeListViewBase<ZmstEntity, int> {

        //次の二つのフィールドは継承されるが、明記しないとrazorが認識できないので
        //プロパティとして明記する
        protected AknTreeView<int>? TreeView {
            get => _treeview;
            set => _treeview = value;
        }

        protected AknListView<int>? ListView {
            get => _listview;
            set => _listview = value;
        }

        protected IZmstEntityMgr? ZmstMgr { get; set; }


        // ---------------------------------------------------------
        // フォルダ内の ZmstEntity をグリッドに流し込む（Explorer）
        // ---------------------------------------------------------
        //public void BuildGridFromFolder(IBaseDataObj<int> folder) {
        //    GridDataItems.Clear();
        //    if (ZmstMgr == null) return;

        //    foreach (var obj in ZmstMgr.DataList) {
        //        if (obj.DataType == NxDataType.Zairyou &&
        //            obj.ParentID.Equals(folder.DataID)) {

        //            if (obj is IZmstEntity zmst) {

        //                // ★ 新構造：MyDataObj(obj) を使う
        //                var row = new MyDataObj<int>(obj);

        //                // ---------------------------------------------------------
        //                // ★ rawData の全カラムを ExtraData に追加する
        //                //   ただし MyDataObj が自動で入れる基本カラムは除外
        //                // ---------------------------------------------------------
        //                // BaseDataObj<int> にキャストして RawData を読む
        //                var baseObj = (BaseDataObj<int>)obj;

        //                foreach (var kv in baseObj._rawData) {
        //                    string key = kv.Key;

        //                    // MyDataObj が自動で入れる基本カラムはスキップ
        //                    if (key == obj.IdColName ||
        //                        key == obj.ParentIDColName ||
        //                        key == "locked_at" ||
        //                        key == "update_at")
        //                        continue;

        //                    row.ExtraData[key] = kv.Value ?? "";
        //                }

        //                GridDataItems.Add(row);
        //            }
        //        }
        //    }

        //    StateHasChanged();
        //}
        public override async Task DeleteSelectedItemAsync() {
            if (DataMgr == null)
                return;

            // 選択行を取得
            //あとで複数選択できる仕様にするけどとりあえず1件だけで
            var item = GridDataItems.FirstOrDefault(x => x.IsSelected);
            if (item == null) return;

            if (item.ItemData is IZmstEntity obj) {

                // 1件削除 → リストにして渡す
                //ソフトデリート
                var failed = await DataMgr.DeleteDataAsync(new List<int> { obj.DataID }, true);


                // 失敗した ID があればログ
                if (failed.FailedIds.Count > 0)
                    Console.WriteLine($"削除失敗: {failed.FailedIds[0]}");

                // グリッド再構築
                BuildGridFromFolder(SelectedFolder);
            }
        }

        public override async Task CreateNewItemAsync() {
            if (DataMgr == null || SelectedFolder == null)
                return;

            var parentID = SelectedFolder.DataID;

            //  1. 孤立オブジェクト生成
            var newObj = DataMgr.CreateNewDataObj(parentID);

            //  2. 編集画面を開く（閉じたら保存済み）
            Manager.Open<Z_Edit>(
                $"新規作成: {newObj.DataName}",
                new Dictionary<string, object>
                {
                    { "LocalCode", newObj.DataID },
                    { "IsNew", true },
                    { "OnSaved", EventCallback.Factory.Create(this, OnChildSaved) },
                    { "OnClose", EventCallback.Factory.Create(this, OnChildClosed) }
                }
            );
        }


        // ---------------------------------------------------------
        // ダブルクリック → 編集画面へ遷移（後で作る）
        // ---------------------------------------------------------
        /// <summary>
        /// 編集画面へ遷移（Zmst の編集画面）
        /// </summary>
        public override async Task BeginEditSelectedItem() {
            var item = GridDataItems.FirstOrDefault(x => x.IsSelected);
            if (item == null) return;

            if (item.ItemData.DataType != NxDataType.Zairyou)
                return;

            var baseObj = (IZmstEntity)item.ItemData;

            // ★ DataOpenAsync は NxOperationResult を返す
            var result = await baseObj.DataOpenAsync();

            if (!result.Success) {
                Console.WriteLine(result.Message);
                await JS.InvokeVoidAsync("aknAlert", $"編集開始失敗: {result.Message}");
                return;
            }

            var winId = Guid.NewGuid();

            Manager.Open<Z_Edit>(
                $"材料編集: {baseObj.DataName}",
                new Dictionary<string, object>
                {
                    { "LocalCode", baseObj.DataID },
                    { "IsNew", false },
                    { "OnSaved", EventCallback.Factory.Create(this, OnChildSaved) },
                    { "OnClose", EventCallback.Factory.Create(this, OnChildClosed) }
                }
            );
        }


        private async Task OnChildClosed() {
            // 子が閉じられたときにやりたい処理を書く
            Console.WriteLine("材料編集画面が閉じられました");
        }

        private async Task OnChildSaved() {
            // ツリーは通常変わらないので再構築不要
            if (SelectedFolder != null)
                BuildGridFromFolder(SelectedFolder);

            StateHasChanged();
        }

        public override void HandleGridDoubleClick(MyDataObj<int> item) {
            Console.WriteLine($"材料編集画面へ遷移: {item.Name}");

            // ★ ダブルクリックされた行を選択状態にする
            foreach (var row in GridDataItems)
                row.IsSelected = false;

            item.IsSelected = true;

            BeginEditSelectedItem();
        }

    }
}
