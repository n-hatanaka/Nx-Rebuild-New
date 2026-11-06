using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NxRebuild.Client.Pages.NxPrograms.DB;
using NxRebuild.Client.Pages.NxPrograms.MDI;
using NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View;
using NxRebuild.Client.Services;
using NxRebuild.shared;
using static MudBlazor.CategoryTypes;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.RyLib {

    public class FrmRyLibViewBase : AknTreeListViewBase<RyLibEntity, Guid> {

        protected AknTreeView<Guid>? TreeView {
            get => _treeview;
            set => _treeview = value;
        }

        protected AknListView<Guid>? ListView {
            get => _listview;
            set => _listview = value;
        }

        protected IRyLibEntityMgr? RyLibMgr { get; set; }

        public override async Task DeleteSelectedItemAsync() {
            if (DataMgr == null)
                return;

            var item = GridDataItems.FirstOrDefault(x => x.IsSelected);
            if (item == null) return;

            if (item.ItemData is RyLibEntity obj) {
                // ソフトデリートで削除を実行
                var failed = await DataMgr.DeleteDataAsync(new List<Guid> { obj.DataID }, true);

                if (failed.FailedIds.Count > 0)
                    Console.WriteLine($"削除失敗: {failed.FailedIds[0]}");

                BuildGridFromFolder(SelectedFolder);
            }
        }

        public override async Task CreateNewItemAsync() {
            if (DataMgr == null || SelectedFolder == null)
                return;

            var parentID = SelectedFolder.DataID;
            var newObj = DataMgr.CreateNewDataObj(parentID);

            // 料理編集画面（例: RyLibEdit）をウィンドウで開く
            //Manager.Open<RyLibEdit>(
            //    $"新規作成: {newObj.DataName}",
            //    new Dictionary<string, object>
            //    {
            //        { "LocalCode", newObj.DataID },
            //        { "IsNew", true },
            //        { "OnSaved", EventCallback.Factory.Create(this, OnChildSaved) },
            //        { "OnClose", EventCallback.Factory.Create(this, OnChildClosed) }
            //    }
            //);
        }

        public override async Task BeginEditSelectedItem() {
            var item = GridDataItems.FirstOrDefault(x => x.IsSelected);
            if (item == null) return;

            if (item.ItemData.DataType != NxDataType.Ryouri)
                return;

            var baseObj = (RyLibEntity)item.ItemData;
            var result = await baseObj.DataOpenAsync();

            if (!result.Success) {
                Console.WriteLine(result.Message);
                await JS.InvokeVoidAsync("aknAlert", $"編集開始失敗: {result.Message}");
                return;
            }

            //Manager.Open<RyLibEdit>(
            //    $"料理編集: {baseObj.DataName}",
            //    new Dictionary<string, object>
            //    {
            //        { "LocalCode", baseObj.DataID },
            //        { "IsNew", false },
            //        { "OnSaved", EventCallback.Factory.Create(this, OnChildSaved) },
            //        { "OnClose", EventCallback.Factory.Create(this, OnChildClosed) }
            //    }
            //);
        }

        private async Task OnChildClosed() {
            Console.WriteLine("料理編集画面が閉じられました");
        }

        private async Task OnChildSaved() {
            if (SelectedFolder != null)
                BuildGridFromFolder(SelectedFolder);

            StateHasChanged();
        }

        public override void HandleGridDoubleClick(MyDataObj<Guid> item) {
            Console.WriteLine($"料理編集画面へ遷移: {item.Name}");

            foreach (var row in GridDataItems)
                row.IsSelected = false;

            item.IsSelected = true;

            BeginEditSelectedItem();
        }

        // フォルダから選択されたとき、DataType が NxDataType.Ryouri のものだけをグリッドへ展開する
        public override void BuildGridFromFolder(IBaseDataObj<Guid> folder) {
            GridDataItems.Clear();
            if (DataMgr == null || folder == null)
                return;

            var parentId = folder.DataID;

            var children = DataMgr.DataList
                .Where(x => x.DataType == NxDataType.Ryouri &&
                            EqualityComparer<Guid>.Default.Equals(x.ParentID, parentId))
                .ToList();

            foreach (var obj in children) {
                var row = new MyDataObj<Guid>(obj);

                if (obj is BaseDataObj<Guid> baseObj) {
                    foreach (var kv in baseObj._rawData) {
                        string key = kv.Key;

                        if (key == obj.IdColName ||
                            key == obj.ParentIDColName ||
                            key == "locked_at" ||
                            key == "update_at")
                            continue;

                        row.ExtraData[key] = kv.Value ?? "";
                    }
                }

                GridDataItems.Add(row);
            }

            StateHasChanged();
        }
    }
}
