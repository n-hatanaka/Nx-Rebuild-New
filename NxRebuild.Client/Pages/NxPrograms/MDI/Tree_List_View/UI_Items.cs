using NxRebuild.Client.Pages.NxPrograms.DB;
using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View {

    // ドラッグドロップの状態を管理する静的クラス
    // ★ ジェネリック化（新世界線）
    public static class DraggingState<TKey> {
        public static MyDataObj<TKey>? DraggingGridItem { get; set; } = null;
        public static MyTreeData<TKey>? DraggingTreeNode { get; set; } = null;
    }

    // ツリーノードのデータモデル
    public class MyTreeData<TKey> {
        public string Text { get; set; } = "";
        public string EditingText { get; set; } = "";
        public bool IsEditing { get; set; }
        public bool IsSelected { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsHighlighted { get; set; }

        // ★ 追加：正本データ
        public IBaseDataObj<TKey>? ItemData { get; set; }

        // ★ ExtraData（UI 表示用）
        public Dictionary<string, object> ExtraData { get; set; } = new();

        // ★ ジェネリック化（元コメント保持）
        public List<MyTreeData<TKey>> Children { get; set; } = new();

        // ★ 追加：IsLocked（MyDataObj と揃える）
        public bool IsLocked { get; set; }

        // ---------------------------------------------------------
        // ★ コンストラクタ：IBaseDataObj と UI プロパティを結び付ける
        // ---------------------------------------------------------
        public MyTreeData(IBaseDataObj<TKey> obj) {
            ItemData = obj;

            // 表示名
            Text = obj.DataName;
            EditingText = obj.DataName;

            // ロック状態
            IsLocked = obj.LockedAt != DateTime.MinValue;

            // ExtraData に必要な情報を投影
            ExtraData["locked_at"] = obj.LockedAt;
            ExtraData["update_at"] = obj.Update_at;
            ExtraData["id"] = obj.DataID;
            ExtraData["parent_id"] = obj.ParentID;
        }

        // パラメータなしコンストラクタ（既存互換）
        public MyTreeData() { }
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
    public class MyDataObj<TKey> {
        public string Name { get; set; } = "";
        public bool IsEditing { get; set; }
        public string EditingName { get; set; } = "";

        public bool IsLocked { get; set; }
        public bool IsSelected { get; set; }

        // ★ 追加：正本データ
        public IBaseDataObj<TKey>? ItemData { get; set; }

        // ★ ExtraData は UI 表示用の辞書
        public Dictionary<string, object> ExtraData { get; set; } = new();

        // ---------------------------------------------------------
        // ★ コンストラクタ：IBaseDataObj と UI プロパティを結び付ける
        // ---------------------------------------------------------
        public MyDataObj(IBaseDataObj<TKey> obj) {
            ItemData = obj;

            // Name
            Name = obj.DataName;

            // Locked 状態
            IsLocked = obj.LockedAt != DateTime.MinValue;

            // ExtraData に必要な情報を投影
            ExtraData["locked_at"] = obj.LockedAt;
            ExtraData["update_at"] = obj.Update_at;
            ExtraData["id"] = obj.DataID;
            ExtraData["parent_id"] = obj.ParentID;
        }

        // パラメータなしコンストラクタも残しておく（既存コード互換）
        public MyDataObj() { }
    }

}
