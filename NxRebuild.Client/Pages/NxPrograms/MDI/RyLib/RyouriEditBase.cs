using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NxRebuild.Client.Pages.NxPrograms.DB.RyLib;
using NxRebuild.Client.Pages.NxPrograms.MDI.Tree_List_View;
using NxRebuild.Client.Services;
using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.RyLib {
    public class RyouriEditBase : ComponentBase {
        [Parameter] public Guid Ry_Code { get; set; }
        [Parameter] public bool IsNew { get; set; }

        private bool _committed;
        [Parameter] public EventCallback OnSaved { get; set; }
        [Parameter] public EventCallback OnClose { get; set; }
        [Inject] protected IJSRuntime JS { get; set; } = default!;

        protected const string TanNameKey = "tan_Name";
        public IRyLibEntity? RyData { get; set; }

        // ヘッダのワーキングメモリ
        protected Dictionary<string, object?> WorkingRaw = new();
        protected RyLibRowEntityMgr? RowMgr { get; set; }

        // 標準の MyDataObj<Guid> を使用
        protected List<MyDataObj<Guid>> Rows { get; } = new();

        protected record ZairyouOption(int LocalCode, string Code, string Label);
        protected List<ZairyouOption> ZairyouOptions { get; private set; } = new();

        protected bool _saving;

        protected string RyName {
            get => WorkingRaw.TryGetValue("Name", out var v) ? v?.ToString() ?? "" : "";
            set => WorkingRaw["Name"] = value;
        }

        protected int Ninzu {
            get => WorkingRaw.TryGetValue("Ninzu", out var v) && int.TryParse(v?.ToString(), out var n) ? n : 1;
            set => WorkingRaw["Ninzu"] = value;
        }

        protected string RyNote {
            get => WorkingRaw.TryGetValue("Rymemo", out var v) ? v?.ToString() ?? "" : "";
            set => WorkingRaw["Rymemo"] = value;
        }

        protected override async Task OnInitializedAsync() {
            var rm = GlobalState.RyLibEntityMgr ?? throw new Exception("RyLibEntityMgr が初期化されていません。");
            RyData = (IRyLibEntity?)rm.Get(Ry_Code) ?? throw new Exception($"Ry_Code={Ry_Code} の料理データがロードされていません。");

            RyData.CreateWorkingMemory(WorkingRaw);
            BuildZairyouOptions();

            RowMgr = new RyLibRowEntityMgr(RyData.DBcon, RyData.TenantCode, RyData.CurrUsrID);

            if (!IsNew) {
                var q = new RecordQuery();
                q.And.Add(new Condition { Column = "Ry_Code", Operator = "=", Value = Ry_Code });
                await RowMgr.InitializeAsync(q);

                foreach (var e in RowMgr.DataList.OfType<RyLibRowEntity>().OrderBy(r => r.SortNo)) {
                    var obj = CreateDataObjFromEntity(e);
                    Rows.Add(obj);
                }
            }
        }

        private MyDataObj<Guid> CreateDataObjFromEntity(RyLibRowEntity e) {
            var obj = new MyDataObj<Guid>(e);
            var rawWorking = new Dictionary<string, object?>();
            e.CreateWorkingMemory(rawWorking);

            foreach (var kv in rawWorking) {
                if (kv.Value != null) obj.ExtraData[kv.Key] = kv.Value;
            }
            obj.ExtraData["Dirty"] = false;
            obj.ExtraData["Saving"] = false;
            return obj;
        }

        private void BuildZairyouOptions() {
            var zm = GlobalState.ZmstEntityMgr ?? throw new Exception("ZmstEntityMgr が初期化されていません。");
            ZairyouOptions = zm.DataList
                .OfType<IZmstEntity>()
                .Where(z => z is not CategoryEntity && z.DataType == NxDataType.Zairyou)
                .Select(z => new ZairyouOption(z.DataID, z.ZCode, $"{z.ZCode} {z.DataName}"))
                .OrderBy(o => o.Code, StringComparer.Ordinal)
                .ToList();
        }

        protected float CalcNetWeight(Dictionary<string, object> row) {
            var localCode = IntOf(row, "LocalCode");
            var zmst = GlobalState.ZmstEntityMgr?.Get(localCode) as IZmstEntity;
            if (zmst == null) return 0f;

            var tanCd = IntOf(row, "tan_cd");
            var tan = zmst.TanList.FirstOrDefault(t => IntOf(t, "tan_cd") == tanCd);
            if (tan == null) return 0f;

            float junbi = FloatOf(row, "Junbi");
            float unitWeight = FloatOf(tan, "jyuuryou");
            float haiki = Convert.ToSingle(zmst.lss);
            float henka = Convert.ToSingle(zmst.Wca);

            return junbi * unitWeight * (1 - haiki / 100) * (henka / 100);
        }

        protected void RecalcSyoumi(MyDataObj<Guid> obj) {
            obj.ExtraData["Syoumi"] = CalcNetWeight(obj.ExtraData);
        }

        protected static int IntOf(Dictionary<string, object> row, string key) =>
            row.TryGetValue(key, out var v) && int.TryParse(v?.ToString(), out var n) ? n : 0;

        protected static float FloatOf(Dictionary<string, object> row, string key) {
            var kv = row.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            return kv.Value != null && float.TryParse(kv.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var f) ? f : 0f;
        }

        protected static bool BoolOf(Dictionary<string, object> row, string key) =>
            row.TryGetValue(key, out var v) && v is bool b && b;

        protected string SyoumiText(Dictionary<string, object> row) =>
            FloatOf(row, "Syoumi").ToString("0.##", CultureInfo.InvariantCulture);

        protected List<(int Code, string Label)> GetTanOptions(Dictionary<string, object> row) {
            var localCode = IntOf(row, "LocalCode");
            if (localCode == 0) return new();

            var z = GlobalState.ZmstEntityMgr?.Get(localCode) as IZmstEntity;
            if (z == null) return new();

            return z.TanList
                .Select(t => {
                    var code = IntOf(t, "tan_cd");
                    var name = t.FirstOrDefault(kv => string.Equals(kv.Key, TanNameKey, StringComparison.OrdinalIgnoreCase)).Value?.ToString();
                    return (code, string.IsNullOrEmpty(name) ? code.ToString() : name);
                })
                .ToList();
        }

        protected void OnZairyouChanged(MyDataObj<Guid> obj, object? value) {
            int.TryParse(value?.ToString(), out var localCode);
            var opt = ZairyouOptions.FirstOrDefault(o => o.LocalCode == localCode);

            obj.ExtraData["LocalCode"] = localCode;
            obj.ExtraData["Z_code"] = opt?.Code ?? "";
            if (opt != null) obj.Name = opt.Label;

            var tans = GetTanOptions(obj.ExtraData);
            obj.ExtraData["tan_cd"] = tans.Count > 0 ? tans[0].Code : 0;
            RecalcSyoumi(obj);
            obj.ExtraData["Dirty"] = true;
        }

        protected void OnJunbiChanged(MyDataObj<Guid> obj, object? value) {
            float.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var f);
            obj.ExtraData["Junbi"] = f;
            RecalcSyoumi(obj);
            obj.ExtraData["Dirty"] = true;
        }

        protected void OnTanChanged(MyDataObj<Guid> obj, object? value) {
            int.TryParse(value?.ToString(), out var code);
            obj.ExtraData["tan_cd"] = code;
            RecalcSyoumi(obj);
            obj.ExtraData["Dirty"] = true;
        }

        // ---------------------------------------------------------
        // ドラッグ＆ドロップ（標準の DraggingState<Guid> を活用）
        // ---------------------------------------------------------
        protected void OnDragStart(MyDataObj<Guid> item) {
            DraggingState<Guid>.DraggingGridItem = item;
        }

        protected void DropRow(int targetIndex) {
            var dragged = DraggingState<Guid>.DraggingGridItem;
            if (dragged == null) return;

            var sourceIndex = Rows.IndexOf(dragged);

            // 別エディタからのドラッグ（あるいは新規複製挿入）の場合
            if (sourceIndex < 0) {
                // 必要に応じて複製・新規 Entity への変換処理を行う
                Rows.Insert(targetIndex, dragged);
            } else {
                // 同一画面内での並び替え
                if (sourceIndex == targetIndex) return;
                Rows.RemoveAt(sourceIndex);
                if (targetIndex > sourceIndex) targetIndex--;
                Rows.Insert(targetIndex, dragged);
            }

            // sort_no を振り直して Dirty フラグを立てる
            for (int i = 0; i < Rows.Count; i++) {
                Rows[i].ExtraData["sort_no"] = i + 1;
                Rows[i].ExtraData["Dirty"] = true;
            }

            // ドラッグ状態を解除
            DraggingState<Guid>.DraggingGridItem = null;
            StateHasChanged();
        }

        protected void AddRow() {
            if (RowMgr == null || RyData == null) return;

            var e = new RyLibRowEntity {
                DBcon = RowMgr.DBcon,
                SelfObjMgr = RowMgr,
                TenantCode = RyData.TenantCode,
                CurrUsrID = RyData.CurrUsrID
            };

            e._SetProperties(NxTypeMapper.Current.CreateEmptyRow(RowMgr.TblName));
            e._SetProperties(new Dictionary<string, object?> {
                ["tenant_code"] = RyData.TenantCode,
                ["Ry_Code"] = Ry_Code,
                ["sort_no"] = Rows.Count + 1,
                ["Z_code"] = "",
                ["LocalCode"] = 0,
                ["Syoumi"] = 0f,
                ["Junbi"] = 0f,
                ["tan_cd"] = 0
            });

            RowMgr.InsertNewDataItem(e);

            var obj = CreateDataObjFromEntity(e);
            Rows.Add(obj);
        }

        protected async Task RemoveRowAsync(MyDataObj<Guid> obj) {
            if (RowMgr == null) return;

            if (obj.ItemData is RyLibRowEntity entity && entity.DataID > 0) {
                var r = await RowMgr.DeleteDataAsync(new List<int> { entity.DataID }, false);
                if (r.FailedIds.Count > 0) {
                    await JS.InvokeVoidAsync("aknAlert", $"削除失敗: {r.Message}");
                    return;
                }
            } else if (obj.ItemData is RyLibRowEntity e) {
                RowMgr._RemoveFromList(e);
            }

            Rows.Remove(obj);
        }

        private async Task<bool> SaveRowAsync(MyDataObj<Guid> obj) {
            if (BoolOf(obj.ExtraData, "Saving") || RyData == null) return true;
            if (obj.ItemData is not RyLibRowEntity entity) return false;

            obj.ExtraData["Saving"] = true;
            try {
                obj.ExtraData["Ry_Code"] = Ry_Code;
                obj.ExtraData["tenant_code"] = RyData.TenantCode;
                obj.ExtraData["sort_no"] = Rows.IndexOf(obj) + 1;

                var workingDict = obj.ExtraData.ToDictionary(k => k.Key, k => (object?)k.Value);
                var result = await entity.SaveAsync(workingDict);
                if (!result.Success) {
                    await JS.InvokeVoidAsync("aknAlert", $"行の保存失敗: {result.Message}");
                    return false;
                }

                obj.ExtraData["Dirty"] = false;
                return true;
            } finally {
                obj.ExtraData["Saving"] = false;
            }
        }

        protected async Task OnRowFocusOut(MyDataObj<Guid> obj) {
            if (!BoolOf(obj.ExtraData, "Dirty")) return;
            if (IntOf(obj.ExtraData, "LocalCode") == 0) return;
            await SaveRowAsync(obj);
        }

        private async Task<bool> FlushRowsAsync() {
            for (int i = 0; i < Rows.Count; i++) {
                var obj = Rows[i];
                if (IntOf(obj.ExtraData, "sort_no") != i + 1) obj.ExtraData["Dirty"] = true;
                if (!BoolOf(obj.ExtraData, "Dirty")) continue;
                if (!await SaveRowAsync(obj)) return false;
            }
            return true;
        }

        public async Task SaveAsync() {
            if (_saving || RyData == null) return;

            if (Rows.Any(r => IntOf(r.ExtraData, "LocalCode") == 0)) {
                await JS.InvokeVoidAsync("aknAlert", "材料が未選択の行があります。");
                return;
            }

            _saving = true;
            try {
                var result = await RyData.SaveAsync(WorkingRaw);
                if (!result.Success) {
                    await JS.InvokeVoidAsync("aknAlert", $"保存失敗: {result.Message}");
                    return;
                }

                if (IsNew && !_committed) {
                    GlobalState.RyLibEntityMgr!.SetParent(RyData);
                    _committed = true;
                }

                if (!await FlushRowsAsync()) return;

                await OnSaved.InvokeAsync();
                await JS.InvokeVoidAsync("aknAlert", "保存しました。");
            } catch (Exception ex) {
                await JS.InvokeVoidAsync("aknAlert", $"例外発生: {ex.Message}");
            } finally {
                _saving = false;
            }
        }

        public async Task CloseAsync() {
            if (RyData != null && (!IsNew || _committed)) await RyData.DataCloseAsync();
            await OnClose.InvokeAsync();
        }
    }
}