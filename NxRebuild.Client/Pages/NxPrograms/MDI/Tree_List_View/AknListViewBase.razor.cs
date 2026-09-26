using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NxRebuild.shared;


namespace NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View {
    public abstract class AknListViewBase<TKey> : ComponentBase where TKey : notnull {

        [Parameter] public List<MyDataObj<TKey>> ListDataItems { get; set; } = new(); // 表示するデータ一覧
        [Parameter] public List<GridColumn> Columns { get; set; } = new(); // カラム定義リスト
        [Parameter] public bool AllowSorting { get; set; } = true; // データのソートを許可するかどうか
        [Parameter] public bool DisableRowDragDrop { get; set; } = false; // 行のドラッグ・ドロップ機能を無効化するかどうか

        [Parameter] public EventCallback<(MyDataObj<TKey> Item, MouseEventArgs Args)> OnRowClicked { get; set; } // 行クリックイベントコールバック
        [Parameter] public EventCallback<MyDataObj<TKey>> OnRowDoubleClicked { get; set; } // 行ダブルクリックイベントコールバック
        [Parameter] public EventCallback<(int TargetIndex, MyDataObj<TKey>? DraggedItem)> OnRowDropped { get; set; } // 行ドロップイベントコールバック
        [Parameter] public EventCallback<(string Key, bool IsAscending)> OnSortRequested { get; set; } // ソートリクエストイベントコールバック
        [Parameter] public EventCallback<MyDataObj<TKey>> OnRenameRequested { get; set; }

        protected bool _isDoubleClicking = false; // ダブルクリック処理中かどうかのフラグ
        private string _currentSortKey = ""; // 現在ソート対象のキー
        private bool _isAscending = true; // 昇順か降順かのフラグ
        private DateTime _lastClickTime = DateTime.MinValue; // 最終クリック時間
        private MyDataObj<TKey>? _lastClickedItem; // 最後にクリックされたアイテム
        protected ElementReference _listInputRef; // 入力フィールドへの参照


        // 「項目名」の列配置を解決するメソッド
        // もし親で "Name" という DataKey のカラム定義が存在すればその配置クラスを使い、
        // なければデフォルトで "List-cell-left" を返す
        public string GetNameAlignClass() {
            var nameCol = Columns.FirstOrDefault(c => c.DataKey == "Name");
            return nameCol != null && !string.IsNullOrEmpty(nameCol.AlignClass)
                   ? nameCol.AlignClass
                   : "List-cell-left";
        }

        public void Sort(string key) {
            if (!AllowSorting || ListDataItems == null || !ListDataItems.Any()) return; // ソートを許可されていない、またはデータが空なら何もしない

            if (_currentSortKey == key) {
                _isAscending = !_isAscending; // 既にソート対象のキーなら、順序を反転
            } else {
                _currentSortKey = key; // 新しいソート対象のキーを設定し、順序を初期化
                _isAscending = true;
            }

            List<MyDataObj<TKey>> sortedList;
            if (key == "Name") { // 「項目名」でソートする場合
                sortedList = _isAscending
                    ? ListDataItems.OrderBy(i => i.Name).ToList() // 昇順に並べ替え
                    : ListDataItems.OrderByDescending(i => i.Name).ToList(); // 降順に並べ替え
            } else { // 数値でソートする場合
                Func<MyDataObj<TKey>, object> keySelector = i => {
                    if (!i.ExtraData.TryGetValue(key, out var val) || val == null) {
                        return string.Empty; // データが空なら空文字を返す
                    }
                    if (decimal.TryParse(val.ToString(), out decimal num)) {
                        return num; // 数値ならその数値を返す
                    }
                    return val.ToString() ?? string.Empty; // 文字ならその文字を返す
                };

                sortedList = _isAscending
                    ? ListDataItems.OrderBy(keySelector).ToList() // 昇順に並べ替え
                    : ListDataItems.OrderByDescending(keySelector).ToList(); // 降順に並べ替え
            }

            ListDataItems.Clear();
            ListDataItems.AddRange(sortedList); // ソート後のリストを反映

            // ★ 再描画は 1 回だけ、親と競合しないよう InvokeAsync 経由で行う
            _ = InvokeAsync(StateHasChanged);
        }


        public virtual void BuildGridFromObj(IBaseDataObj<TKey> obj) {
            ListDataItems.Clear();
            ListDataItems.Add(new MyDataObj<TKey>(obj));
            // ★ 明示的に再描画（ここも 1 回だけ）
            _ = InvokeAsync(StateHasChanged);
        }


        public string GetSortIcon(string key) {
            if (!AllowSorting || _currentSortKey != key) return ""; // ソートを許可されていない、またはソート対象のキーと異なるなら空文字を返す
            return _isAscending ? " ▲" : " ▼"; // 昇順なら↑、降順なら↓のアイコンを返す
        }

        public async Task HandleClick(MyDataObj<TKey> item, MouseEventArgs e) {
            var now = DateTime.Now;

            // ★ ダブルクリック判定：300ms 以内に同じアイテムが再クリックされたらダブルクリック扱い
            if (item == _lastClickedItem && (now - _lastClickTime).TotalMilliseconds < 300) {
                _lastClickTime = DateTime.MinValue; // 最終クリック時間をリセット
                _lastClickedItem = null;

                if (OnRowDoubleClicked.HasDelegate) {
                    await OnRowDoubleClicked.InvokeAsync(item); // ダブルクリックイベントを呼び出す
                }
                return;
            }

            _lastClickTime = now; // 最終クリック時間を更新
            _lastClickedItem = item; // 最後にクリックされたアイテムを保存

            // ★ UI スレッドをブロックしないよう、遅延処理は InvokeAsync 経由で投げる
            _ = InvokeAsync(async () => {
                await Task.Delay(300); // 0.3秒の遅延（UI スレッドとは分離）

                if (_lastClickedItem != item) return; // クリックがキャンセルされたなら何もしない

                if (item.IsSelected) {
                    await StartEditingListItem(item); // 選択されているアイテムを編集モードに遷移
                } else {
                    if (OnRowClicked.HasDelegate) {
                        await OnRowClicked.InvokeAsync((item, e)); // 行クリックイベントを呼び出す
                    }
                }
            });
        }

        public async Task HandleDoubleClick(MyDataObj<TKey> item) {
            _isDoubleClicking = true; // ダブルクリック処理中に設定

            if (OnRowDoubleClicked.HasDelegate) {
                await OnRowDoubleClicked.InvokeAsync(item); // ダブルクリックイベントを呼び出す
            }

            _isDoubleClicking = false;
        }

        public async Task HandleListEditKey(KeyboardEventArgs e, MyDataObj<TKey> item) {
            if (e.Key == "Enter")
                await OnRenameRequested.InvokeAsync(item);
            else if (e.Key == "Escape")
                CancelRename(item);  // Cancel
        }

        public async Task DropRow(int targetIndex) {
            if (!DisableRowDragDrop && OnRowDropped.HasDelegate) { // 行ドラッグ・ドロップが有効で、ドロップイベントコールバックがあるなら
                await OnRowDropped.InvokeAsync((targetIndex, DraggingState<TKey>.DraggingGridItem)); // ドロップイベントを呼び出す
            }
        }

        public async Task StartEditingListItem(MyDataObj<TKey> item) {
            foreach (var i in ListDataItems) i.IsEditing = false; // すべてのアイテムの編集モードをリセット
            item.IsEditing = true; // クリックされたアイテムの編集モードに設定

            item.EditingName = item.Name;   // 編集前の名前を保持

            // ★ 再描画は InvokeAsync 経由で 1 回だけ
            await InvokeAsync(StateHasChanged);

            // ★ フォーカス設定も UI スレッドをブロックしないように行う
            await InvokeAsync(async () => {
                await Task.Delay(50); // 0.05秒の遅延
                if (_listInputRef.Context != null) {
                    await _listInputRef.FocusAsync(); // フォーカスを設定
                }
            });
        }

        public void CancelRename(MyDataObj<TKey> item) {
            item.IsEditing = false;
            item.EditingName = item.Name;
            // ★ 再描画は 1 回だけ
            _ = InvokeAsync(StateHasChanged);
        }

        public string FormatValue(object val, string format) { // 値をフォーマットするメソッド
            if (val == null) return "-"; // 値が空なら"-"
            if (string.IsNullOrEmpty(format)) return val.ToString(); // フォーマットが空なら元の値を返す

            if (decimal.TryParse(val.ToString(), out decimal num)) { // 数値ならフォーマットして返す
                return num.ToString(format);
            }

            return val.ToString(); // それ以外は元の値を返す
        }

    }
}
