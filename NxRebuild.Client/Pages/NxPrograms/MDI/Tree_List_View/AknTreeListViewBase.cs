using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NxRebuild.Client.Pages.NxPrograms.DB;
using NxRebuild.Client.Services;
using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View {

    // ★ TObj を追加した正しい抽象構造
    public class AknTreeListViewBase<TObj, TKey> : ComponentBase
        where TObj : BaseDataObj<TKey>
        where TKey : notnull {

        [Parameter] public EventCallback<MyDataObj<TKey>> OnRenameRequested { get; set; }
        [Parameter] public EventCallback<(MyDataObj<TKey> Item, MouseEventArgs Args)> OnRowClicked { get; set; }
        [Parameter] public EventCallback<MyDataObj<TKey>> OnRowDoubleClicked { get; set; }
        [Parameter] public EventCallback<(int TargetIndex, MyDataObj<TKey>? DraggedItem)> OnRowDropped { get; set; }
        [Parameter] public EventCallback<(string Key, bool IsAscending)> OnSortRequested { get; set; }

        protected AknTreeView<TKey>? _treeview;
        public AknTreeView<TKey>? TreeView => _treeview;

        protected AknListView<TKey>? _listview;
        public AknListView<TKey>? ListView => _listview;
        protected IBaseDataObj<TKey>? SelectedFolder { get; set; }

        // ツリーデータ
        [Parameter] public List<MyTreeData<TKey>> TreeData { get; set; } = new();

        // グリッド列定義
        [Parameter] public List<GridColumn> Columns { get; set; } = new();

        // グリッドデータリスト
        [Parameter] public List<MyDataObj<TKey>> GridDataItems { get; set; } = new();

        // ---------------------------------------------------------
        /// <summary>
        /// データマネージャー
        /// </summary>
        protected IBaseDataObjMgr<TObj, TKey>? DataMgr { get; set; }
        // ---------------------------------------------------------

        // ノード選択イベントハンドラ
        public virtual void HandleNodeSelection(MyTreeData<TKey> selectedNode) {
            if (selectedNode.ItemData is IBaseDataObj<TKey> folder) {
                SelectedFolder = folder;
                BuildGridFromFolder(folder);
            }
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

            var idx = GridDataItems.IndexOf(payload.DraggedItem);
            if (idx != -1) {
                GridDataItems.RemoveAt(idx);
                GridDataItems.Insert(payload.TargetIndex, payload.DraggedItem);
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

        //リネームリクエストイベントハンドラ
        protected async Task HandleRename(MyDataObj<TKey> item) {

            if (item == null)
                return; // キャンセル

            await ConfirmRename(item);
        }

        //リネーム処理
        public virtual async Task ConfirmRename(MyDataObj<TKey> item) {
            // UI 側の編集名を反映（仮）
            item.Name = item.EditingName;

            // ★ DataMgr に反映（DB更新）
            if (item.ItemData is BaseDataObj<TKey> baseObj) {
                var ok = await baseObj.ReName(item.EditingName);

                if (!ok) {
                    // 失敗したら元に戻す
                    item.Name = baseObj.DataName;
                    item.IsEditing = false;
                    return;
                }
            }

            item.IsEditing = false;

            // グリッド再構築
            if (SelectedFolder != null)
                BuildGridFromFolder(SelectedFolder);
        }


        public void AddDefaultColumns() {
            Columns.Clear();

            // ファイル名
            Columns.Add(new GridColumn {
                Caption = "ファイル名",
                DataKey = "Name",
                AlignClass = "text-left",
                Width = 200
            });

            // locked_at
            Columns.Add(new GridColumn {
                Caption = "ロック日時",
                DataKey = "locked_at",
                AlignClass = "text-center",
                Format = "yyyy/MM/dd HH:mm",
                Width = 150
            });

            // update_at
            Columns.Add(new GridColumn {
                Caption = "更新日時",
                DataKey = "update_at",
                AlignClass = "text-center",
                Format = "yyyy/MM/dd HH:mm",
                Width = 150
            });
        }

        // ------------------------- DataObj関連 ---------------------------------------
        public virtual void SetManager(IBaseDataObjMgr<TObj, TKey> mgr) {
            DataMgr = mgr;
            BuildTreeFromMgr();
        }

        // ---------------------------------------------------------
        // ★ Mgr → TreeView へ流し込む（IBaseDataObj を MyTreeData に投影）
        // ---------------------------------------------------------
        public virtual void BuildTreeFromMgr() {
            if (DataMgr == null) return;

            TreeData.Clear();

            // 1. ルートノードだけ追加（親が null のもの）
            var roots = DataMgr.DataList
                        .Where(o =>
                            o.DataType == NxDataType.Folder ||
                            (o.DataType == NxDataType.root || o.ParentDataObj == null));


            foreach (var root in roots) {
                var node = new MyTreeData<TKey>(root) { IsExpanded = true };
                BuildChildren(node, DataMgr.DataList);
                TreeData.Add(node);
            }

            StateHasChanged();
        }

        private void BuildChildren(MyTreeData<TKey> parentNode, IEnumerable<IBaseDataObj<TKey>> all) {
            var children = all
                .Where(o => o.DataType == NxDataType.Folder && o.ParentDataObj == parentNode.ItemData);

            foreach (var child in children) {
                var childNode = new MyTreeData<TKey>(child) { IsExpanded = true };
                BuildChildren(childNode, all);
                parentNode.Children.Add(childNode);
            }
        }

        // ---------------------------------------------------------
        // ★ DataObj → GridView へ流し込む（IBaseDataObj を MyDataObj に投影）
        // ---------------------------------------------------------
        public virtual void BuildGridFromFolder(IBaseDataObj<TKey> folder) {
            GridDataItems.Clear();
            if (DataMgr == null || folder == null)
                return;

            // 親フォルダの ID
            var parentId = folder.DataID;

            // 子アイテムを抽出（正本から）
            var children = DataMgr.DataList
                .Where(x => x.DataType == NxDataType.Zairyou &&
                            EqualityComparer<TKey>.Default.Equals(x.ParentID, parentId))
                .ToList();

            foreach (var obj in children) {
                // ★ 新構造：MyDataObj(obj) を使う
                var row = new MyDataObj<TKey>(obj);

                // BaseDataObj<TKey> にキャストして RawData を読む
                if (obj is BaseDataObj<TKey> baseObj) {
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
                }

                GridDataItems.Add(row);
            }

            StateHasChanged();
        }
    }
}
