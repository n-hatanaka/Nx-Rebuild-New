using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using System.Xml.Linq;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View {

    public abstract class AknTreeViewBase : ComponentBase {

        [Parameter] public List<MyTreeData> TreeData { get; set; } = new();

        //ドラッグドロップそれぞれの有効化、無効化
        [Parameter] public bool DisableNodeDrag { get; set; } = false;
        [Parameter] public bool DisableNodeDrop { get; set; } = false;

        // 外部からのイベント処理を受け取るパラメータ        
        [Parameter] public EventCallback<MyTreeData> OnNodeSelected { get; set; }
        [Parameter] public EventCallback<(MyTreeData Target, MyDataObj? DroppedItem, MyTreeData? DroppedNode)> OnNodeDropped { get; set; }

        private MyTreeData? _editingNode;
        public MyTreeData? editingNode => _editingNode; 

        private ElementReference _editInput;

        public ElementReference editInput { get { return _editInput; } set { _editInput = value; } }

        protected async Task SelectTreeNode(MyTreeData node) {
            if (node == null) return;

            ClearTreeSelection(TreeData);
            node.IsSelected = true;

            if (OnNodeSelected.HasDelegate) {
                await OnNodeSelected.InvokeAsync(node);
            }
            base.StateHasChanged();
        }

        private void ClearTreeSelection(List<MyTreeData> nodes) {
            if (nodes == null) return;
            foreach (var n in nodes) {
                n.IsSelected = false;
                if (n.Children != null) ClearTreeSelection(n.Children);
            }
        }

        protected async Task StartEditingTreeNode(MyTreeData node) {
            if (_editingNode != null) _editingNode.IsEditing = false;
            node.EditingText = node.Text;
            node.IsEditing = true;
            _editingNode = node;
            base.StateHasChanged();

            await Task.Delay(20);
            await _editInput.FocusAsync();
        }

        protected void StopEditingTreeNode(MyTreeData node, bool isCommit) {
            if (isCommit && !string.IsNullOrWhiteSpace(node.EditingText)) node.Text = node.EditingText;
            node.IsEditing = false;
            base.StateHasChanged();
        }

        protected void HandleTreeKeyUp(KeyboardEventArgs e, MyTreeData node) {
            if (e.Key == "Enter") StopEditingTreeNode(node, true);
            if (e.Key == "Escape") StopEditingTreeNode(node, false);
        }


        protected async Task DropOnNode(MyTreeData targetNode) {
            // _editingNodeがnullでない場合、編集中ノードの編集を停止します。
            if (_editingNode != null) StopEditingTreeNode(_editingNode, true);

            // targetNodeがnullまたはDisableNodeDropパラメータがtrueの場合、ドロップ処理を終了します。
            if (targetNode == null || DisableNodeDrop) return;


            // OnNodeDroppedのイベントハンドラにターゲットノードを渡して、ドロップ処理を行います。
            // 親コンポーネント側にドラッグ元情報を渡して委譲
            if (OnNodeDropped.HasDelegate) {
                await OnNodeDropped.InvokeAsync((targetNode, DraggingState.DraggingGridItem, DraggingState.DraggingTreeNode));
            }

            targetNode.IsHighlighted = false;
            targetNode.IsExpanded = true;

            // ターゲットノードのハイライト状態と展開状態を更新し、UIを再描画します。
            base.StateHasChanged();
        }
    }
}
