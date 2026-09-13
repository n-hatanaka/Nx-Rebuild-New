using Microsoft.AspNetCore.Components;
using NxRebuild.Client.Pages.NxPrograms.DB;
using NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View;
using NxRebuild.Client.Services;
using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.Zmst {
    public class FrmZmstViewBase : AknTreeListViewBase<int> {
        //次の二つのフィールドは継承されるが、明記しないとrazorが認識できないので明記する
        protected AknTreeView<int>? _treeview;
        protected AknListView<int>? _listview;

        protected IZmstEntityMgr? ZmstMgr { get; set; }
        protected IBaseDataObj<int>? SelectedFolder { get; set; }

        // ---------------------------------------------------------
        // 初期化（Explorer）
        // ---------------------------------------------------------

        protected override async Task OnInitializedAsync() {
            ZmstMgr = GlobalState.ZmstEntityMgr;

            if (ZmstMgr == null)
                return;

            SetManager((IBaseDataObjMgr<BaseDataObj<int>, int>)ZmstMgr);

        }


        // ---------------------------------------------------------
        // フォルダだけツリーに入れる（新構造）
        // ---------------------------------------------------------
        public override void BuildTreeFromMgr() {
            if (ZmstMgr == null) return;

            TreeData.Clear();

            foreach (var obj in ZmstMgr.DataList) {
                if (obj.DataType == NxDataType.Folder) {
                    TreeData.Add(new MyTreeData<int>(obj) {
                        IsExpanded = true
                    });
                }
            }

            StateHasChanged();
        }

        // ---------------------------------------------------------
        // ノード選択 → フォルダ内の材料をグリッドへ流し込む
        // ---------------------------------------------------------
        public override void HandleNodeSelection(MyTreeData<int> selectedNode) {

            if (selectedNode.ItemData is IBaseDataObj<int> folder) {
                SelectedFolder = folder;
                BuildGridFromFolder(folder);
            }
        }

        // ---------------------------------------------------------
        // フォルダ内の ZmstEntity をグリッドに流し込む（Explorer）
        // ---------------------------------------------------------
        public void BuildGridFromFolder(IBaseDataObj<int> folder) {
            GridDataItems.Clear();
            if (ZmstMgr == null) return;

            foreach (var obj in ZmstMgr.DataList) {
                if (obj.DataType == NxDataType.Zairyou &&
                    obj.ParentID.Equals(folder.DataID)) {

                    if (obj is IZmstEntity zmst) {

                        // ★ 新構造：MyDataObj(obj) を使う
                        var row = new MyDataObj<int>(obj);

                        // ---------------------------------------------------------
                        // ★ rawData の全カラムを ExtraData に追加する
                        //   ただし MyDataObj が自動で入れる基本カラムは除外
                        // ---------------------------------------------------------
                        // BaseDataObj<int> にキャストして RawData を読む
                        var baseObj = (BaseDataObj<int>)obj;

                        foreach (var kv in baseObj._rawData) {
                            string key = kv.Key;

                            // MyDataObj が自動で入れる基本カラムはスキップ
                            if (key == obj.IdColName ||
                                key == obj.ParentIDColName ||
                                key == "locked_at" ||
                                key == "update_at")
                                continue;

                            row.ExtraData[key] = kv.Value ?? "";
                        }

                        GridDataItems.Add(row);
                    }
                }
            }

            StateHasChanged();
        }

        // ---------------------------------------------------------
        // ダブルクリック → 編集画面へ遷移（後で作る）
        // ---------------------------------------------------------
        public override void HandleGridDoubleClick(MyDataObj<int> item) {

            Console.WriteLine($"材料編集画面へ遷移: {item.Name}");

            // NavigationManager.NavigateTo($"/nx/zmst/edit/{item.ExtraData["LocalCode"]}");
        }
    }
}
