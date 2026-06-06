using NxRebuild.Client.Pages.NxPrograms.DB;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View {

    // ドラッグドロップの状態を管理する静的クラス
    public static class DraggingState {
        public static MyDataObj? DraggingGridItem { get; set; } = null;
        public static MyTreeData? DraggingTreeNode { get; set; } = null;
    }

    // ツリーノードのデータモデル
    public class MyTreeData {
        public string Text { get; set; } = "";
        public string EditingText { get; set; } = "";
        public bool IsEditing { get; set; }
        public bool IsSelected { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsHighlighted { get; set; }

        public DataObj ItemData {  get; set; }
        public List<MyTreeData> Children { get; set; } = new();
    }

    // グリッド列の定義
    public class GridColumn {
        public string Caption { get; set; } = "";
        public string DataKey { get; set; } = "";
        public string AlignClass { get; set; } = "";
        public string Format { get; set; } = "";
        public int Width { get; set; }
    }

    // グリッドデータのオブジェクト
    public class MyDataObj {
        
        public string Name { get; set; } = "";
        public bool IsEditing { get; set; }

        public bool IsLocked { get; set; }
        public DataObj ItemData { get; set; }
        public Dictionary<string, object> ExtraData { get; set; } = new();
        public bool IsSelected { get; set; }
    }

}
