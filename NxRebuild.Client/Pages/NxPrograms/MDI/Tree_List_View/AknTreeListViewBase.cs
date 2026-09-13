using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NxRebuild.Client.Pages.NxPrograms.DB;
using NxRebuild.Client.Services;
using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View {

    public class AknTreeListViewBase<TKey> : ComponentBase
        where TKey : notnull {
        [Parameter] public EventCallback<(MyDataObj<TKey> Item, MouseEventArgs Args)> OnRowClicked { get; set; }
        [Parameter] public EventCallback<MyDataObj<TKey>> OnRowDoubleClicked { get; set; }
        [Parameter] public EventCallback<(int TargetIndex, MyDataObj<TKey>? DraggedItem)> OnRowDropped { get; set; }
        [Parameter] public EventCallback<(string Key, bool IsAscending)> OnSortRequested { get; set; }

        protected AknTreeView<TKey>? _treeview;
        public AknTreeView<TKey>? TreeView => _treeview;

        protected AknListView<TKey>? _listview;
        public AknListView<TKey>? ListView => _listview;

        // ツリーデータ
        [Parameter] public List<MyTreeData<TKey>> TreeData { get; set; } = new();

        // グリッド列定義
        [Parameter] public List<GridColumn> Columns { get; set; } = new();

        // グリッドデータリスト
        [Parameter] public List<MyDataObj<TKey>> GridDataItems { get; set; } = new();

        // ---------------------------------------------------------
        protected IBaseDataObjMgr<BaseDataObj<TKey>, TKey>? DataMgr { get; set; }
        // ---------------------------------------------------------


        // ノード選択イベントハンドラ
        public virtual void HandleNodeSelection(MyTreeData<TKey> selectedNode) {
            Console.WriteLine($"フォルダ選択: {selectedNode.Text}");
        }

        // ノードドラッグドロップイベントハンドラ
        public virtual void HandleNodeDrop((MyTreeData<TKey> Target, MyDataObj<TKey>? DroppedItem, MyTreeData<TKey>? DroppedNode) payload) {
            if (payload.DroppedItem != null) {
                payload.Target.Children.Add(new MyTreeData<TKey>(payload.DroppedItem.ItemData!));
            } else if (payload.DroppedNode != null) {
                if (payload.DroppedNode == payload.Target ||
                    IsDescendantNode(payload.DroppedNode, payload.Target))
                    return;

                RemoveNodeFromAll(TreeData, payload.DroppedNode);
                payload.Target.Children.Add(payload.DroppedNode);
            }

            DraggingState<TKey>.DraggingGridItem = null;
            DraggingState<TKey>.DraggingTreeNode = null;
            StateHasChanged();
        }

        // グリッド行クリックイベントハンドラ
        public virtual void HandleGridClick((MyDataObj<TKey> Item, MouseEventArgs Args) payload) {
            if (!payload.Args.CtrlKey) {
                foreach (var i in GridDataItems) i.IsSelected = false;
            }
            payload.Item.IsSelected = true;
        }

        // グリッド行ダブルクリックイベントハンドラ
        public virtual void HandleGridDoubleClick(MyDataObj<TKey> item) {
            Console.WriteLine($"グリッド行ダブルクリック: {item.Name}");
        }

        // グリッド行再配置イベントハンドラ
        public virtual void HandleGridReorder((int TargetIndex, MyDataObj<TKey>? DraggedItem) payload) {
            if (payload.DraggedItem == null) return;

            var idx = ListDataItems.IndexOf(payload.DraggedItem);
            if (idx != -1) {
                ListDataItems.RemoveAt(idx);
                ListDataItems.Insert(payload.TargetIndex, payload.DraggedItem);
            }

            DraggingState<TKey>.DraggingGridItem = null;
        }


        // ソートリクエストイベントハンドラ
        public virtual async Task HandleSortRequest((string Key, bool IsAscending) payload) {
            await OnSortRequested.InvokeAsync(payload);
        }

        // 親ノードが子孫ノードであるかどうかチェック
        public virtual bool IsDescendantNode(MyTreeData<TKey> parent, MyTreeData<TKey> potentialDescendant) {
            if (parent == potentialDescendant) return true;

            foreach (var child in parent.Children) {
                if (IsDescendantNode(child, potentialDescendant)) return true;
            }
            return false;
        }

        // 指定されたノードとその子孫からリストから削除
        public virtual void RemoveNodeFromAll(List<MyTreeData<TKey>> list, MyTreeData<TKey> node) {
            if (list.Remove(node)) return;

            foreach (var n in list) {
                RemoveNodeFromAll(n.Children, node);
            }
        }

        // ------------------------- DataObj関連 ---------------------------------------

        public virtual void SetManager(IBaseDataObjMgr<BaseDataObj<TKey>, TKey> mgr) {
            DataMgr = mgr;
            BuildTreeFromMgr();
        }

        // ---------------------------------------------------------
        // ★ Mgr → TreeView へ流し込む（IBaseDataObj を MyTreeData に投影）
        // ---------------------------------------------------------
        public virtual void BuildTreeFromMgr() {
            if (DataMgr == null) return;

            TreeData.Clear();

            foreach (var obj in DataMgr.DataList) {
                if (obj.DataType == NxDataType.Folder) {
                    TreeData.Add(new MyTreeData<TKey>(obj) {
                        IsExpanded = true
                    });
                }
            }

            StateHasChanged();
        }

        // ---------------------------------------------------------
        // ★ DataObj → GridView へ流し込む（IBaseDataObj を MyDataObj に投影）
        // ---------------------------------------------------------
        public virtual void BuildGridFromObj(IBaseDataObj<TKey> obj) {
            ListDataItems.Clear();
            ListDataItems.Add(new MyDataObj<TKey>(obj));
            StateHasChanged();
        }
    }
}
