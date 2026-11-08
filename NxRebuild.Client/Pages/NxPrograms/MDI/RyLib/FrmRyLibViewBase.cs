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

            if (item.ItemData is IRyLibEntity obj) {
                // ソフトデリートの扱いはサーバー側（コントローラー）で行うため、フラグは渡さない
                var failed = await DataMgr.DeleteDataAsync(new List<Guid> { obj.DataID });

                if (failed.FailedIds.Count > 0)
                    Console.WriteLine($"削除失敗: {failed.FailedIds[0]}");

                BuildGridFromFolder(SelectedFolder);
            }
        }

        public override async Task CreateNewItemAsync() {
            if (DataMgr == null)
                return;
            if (SelectedFolder == null)
                SelectedFolder = DataMgr.DataList.First();//必ずrootアイテムがあるはずなので、最初のアイテムを選択する

            // Sync でも Base でも IRyLibEntity として扱う
            var newObj = (IRyLibEntity)DataMgr.CreateNewDataObj(SelectedFolder.DataID);

            // 仮保存中は同期処理を止める（Sync の保存系と同じ作法）
            using var _pause = await NxSyncControl.For(newObj.DBcon).PauseAsync();

            var working = new Dictionary<string, object?>();
            newObj.CreateWorkingMemory(working);
            working[newObj.NameColName] = "NewRyouri";
            working["Ninzu"] = 1;

            // 仮保存: ワークテーブル(w_Ry_Lib)だけに書く。正本(Ry_lib)へはヘッダ保存時に反映される
            using (var tran = newObj.DBcon.BeginTransaction()) {
                try {
                    await newObj.SaveWorkingAsync(working, null, tran);
                    tran.Commit();
                } catch (Exception ex) {
                    tran.Rollback();
                    await JS.InvokeVoidAsync("aknAlert", $"新規作成失敗: {ex.Message}");
                    return;
                }
            }

            // 編集画面がフォームの初期値に使えるよう、正本にも仮の内容を反映しておく
            newObj._SetProperties(working);

            Manager.Open<RyouriEdit>(
                $"新規作成: {newObj.DataName}",
                new Dictionary<string, object>
                {
                    { "Ry_Code", newObj.DataID },
                    { "IsNew", true },
                    { "OnSaved", EventCallback.Factory.Create(this, OnChildSaved) },
                    { "OnClose", EventCallback.Factory.Create(this, OnChildClosed) }
                }
            );
        }

        public override async Task BeginEditSelectedItem() {
            var item = GridDataItems.FirstOrDefault(x => x.IsSelected);
            if (item == null) return;

            if (item.ItemData.DataType != NxDataType.Ryouri)
                return;

            var baseObj = (IRyLibEntity)item.ItemData;

            // ★ Zmst と同じ排他・オープン処理
            var result = await baseObj.DataOpenAsync();
            if (!result.Success) {
                Console.WriteLine(result.Message);
                await JS.InvokeVoidAsync("aknAlert", $"編集開始失敗: {result.Message}");
                return;
            }

            Manager.Open<RyouriEdit>(
                $"料理編集: {baseObj.DataName}",
                new Dictionary<string, object>
                {
                    { "Ry_Code", baseObj.DataID },
                    { "IsNew", false },
                    { "OnSaved", EventCallback.Factory.Create(this, OnChildSaved) },
                    { "OnClose", EventCallback.Factory.Create(this, OnChildClosed) }
                }
            );
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

                // Sync ラッパー経由でも生データを取れるようにする
                var raw = obj switch {
                    BaseDataObj<Guid> b => b._rawData,
                    SyncBaseDataObj<Guid> s => s._rawData,
                    _ => null
                };

                if (raw != null) {
                    foreach (var kv in raw) {
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