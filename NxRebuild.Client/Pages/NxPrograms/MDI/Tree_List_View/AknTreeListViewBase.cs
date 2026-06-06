using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View {
    public class AknTreeListViewBase : ComponentBase {

        [Parameter] public EventCallback<(MyDataObj Item, MouseEventArgs Args)> OnRowClicked { get; set; } // 行クリックイベントコールバック
        [Parameter] public EventCallback<MyDataObj> OnRowDoubleClicked { get; set; } // 行ダブルクリックイベントコールバック
        [Parameter] public EventCallback<(int TargetIndex, MyDataObj? DraggedItem)> OnRowDropped { get; set; } // 行ドロップイベントコールバック
        [Parameter] public EventCallback<(string Key, bool IsAscending)> OnSortRequested { get; set; } // ソートリクエストイベントコールバック

        protected AknTreeView? _treeview;
        public AknTreeView? TreeView => _treeview; 

        protected AknListView? _listview;
        public AknListView? ListView => _listview;

        // ツリーデータ
        [Parameter] public List<MyTreeData> TreeData { get; set; } = new();

        // グリッド列定義
        [Parameter] public List<GridColumn> Columns { get; set; } = new();

        // グリッドデータリスト
        [Parameter] public List<MyDataObj> GridDataItems { get; set; } = new();

        // ノード選択イベントハンドラ
        protected void HandleNodeSelection(MyTreeData selectedNode) {
            Console.WriteLine($"フォルダ選択: {selectedNode.Text}");
        }

        // ノードドラッグドロップイベントハンドラ
        protected void HandleNodeDrop((MyTreeData Target, MyDataObj? DroppedItem, MyTreeData? DroppedNode) payload) {
            if (payload.DroppedItem != null) {
                payload.Target.Children.Add(new MyTreeData { Text = payload.DroppedItem.Name });
            } else if (payload.DroppedNode != null) {
                if (payload.DroppedNode == payload.Target || IsDescendantNode(payload.DroppedNode, payload.Target)) return;
                RemoveNodeFromAll(TreeData, payload.DroppedNode);
                payload.Target.Children.Add(payload.DroppedNode);
            }

            DraggingState.DraggingGridItem = null;
            DraggingState.DraggingTreeNode = null;
            base.StateHasChanged();
        }

        // グリッド行クリックイベントハンドラ
        protected void HandleGridClick((MyDataObj Item, MouseEventArgs Args) payload) {
            if (!payload.Args.CtrlKey) {
                foreach (var i in GridDataItems) i.IsSelected = false;
            }
            payload.Item.IsSelected = true;
        }

        // グリッド行ダブルクリックイベントハンドラ
        protected void HandleGridDoubleClick(MyDataObj item) {
            Console.WriteLine($"グリッド行がダブルクリックされました: {item.Name}");
        }

        // グリッド行再配置イベントハンドラ
        protected void HandleGridReorder((int TargetIndex, MyDataObj? DraggedItem) payload) {
            if (payload.DraggedItem == null) return;

            var idx = GridDataItems.IndexOf(payload.DraggedItem);
            if (idx != -1) {
                GridDataItems.RemoveAt(idx);
                GridDataItems.Insert(payload.TargetIndex, payload.DraggedItem);
            }

            DraggingState.DraggingGridItem = null;
        }

        //ソートリクエストイベントハンドラ
        protected async Task HandleSortRequest((string Key, bool IsAscending) payload) {
            //ソートを外部結合
            await OnSortRequested.InvokeAsync(payload);
        }

        // 親ノードが子孫ノードであるかどうかチェック
        protected bool IsDescendantNode(MyTreeData parent, MyTreeData potentialDescendant) {
            if (parent == potentialDescendant) return true;

            foreach (var child in parent.Children) {
                if (IsDescendantNode(child, potentialDescendant)) return true;
            }

            return false;
        }

        // 指定されたノードとその子孫からリストから削除
        protected void RemoveNodeFromAll(List<MyTreeData> list, MyTreeData node) {
            if (list.Remove(node)) return;

            foreach (var n in list) {
                RemoveNodeFromAll(n.Children, node);
            }
        }
    }
}
