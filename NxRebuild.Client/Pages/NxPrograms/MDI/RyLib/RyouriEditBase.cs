using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NxRebuild.Client.Pages.NxPrograms.DB.RyLib;
using NxRebuild.Client.Services;
using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.RyLib {
    public class RyouriEditBase : ComponentBase {
        [Parameter] public Guid Ry_Code { get; set; }
        [Parameter] public bool IsNew { get; set; }

        // 新規作成のとき、一覧(Mgr)にまだ入っていない仮保存済みオブジェクトを受け取る
        [Parameter] public IRyLibEntity? NewData { get; set; }
        private bool _committed;   // 新規料理を、初回のヘッダ保存で一覧に加えたか
        [Parameter] public EventCallback OnSaved { get; set; }
        [Parameter] public EventCallback OnClose { get; set; }
        [Inject] protected IJSRuntime JS { get; set; } = default!;

        // tan_m の単位名カラム
        protected const string TanNameKey = "tan_Name";

        public IRyLibEntity? RyData { get; set; }

        // ヘッダ（w_Ry_Lib）のワーキングメモリ
        protected Dictionary<string, object?> WorkingRaw = new();

        protected RyLibRowEntityMgr? RowMgr { get; set; }

        // 画面上の1行 = 正本エンティティ + その行のワーキングメモリ
        protected class RowVm {
            public RyLibRowEntity Entity { get; init; } = default!;
            public Dictionary<string, object?> Working { get; init; } = new();
            public bool Dirty { get; set; }
            public bool Saving { get; set; }
        }

        protected List<RowVm> Rows { get; } = new();

        // 材料ドロップダウンの選択肢
        protected record ZairyouOption(int LocalCode, string Code, string Label);
        protected List<ZairyouOption> ZairyouOptions { get; private set; } = new();

        protected bool _saving;

        // ---------------------------------------------------------
        // 基本情報（スキーマ: w_Ry_Lib の Name / Ninzu / Rymemo）
        // ---------------------------------------------------------
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

        // ---------------------------------------------------------
        // 初期化
        // ---------------------------------------------------------
        protected override async Task OnInitializedAsync() {
            var rm = GlobalState.RyLibEntityMgr
                ?? throw new Exception("RyLibEntityMgr が初期化されていません。");

            RyData = (IsNew ? NewData : (IRyLibEntity?)rm.Get(Ry_Code))
                ?? throw new Exception($"Ry_Code={Ry_Code} の料理データがロードされていません。");

            RyData.CreateWorkingMemory(WorkingRaw);

            BuildZairyouOptions();

            RowMgr = new RyLibRowEntityMgr(RyData.DBcon, RyData.TenantCode, RyData.CurrUsrID);

            // 既存料理のときだけ、この料理(Ry_Code)の材料行を読み込む
            // ※ BaseDataObjMgr._BuildSql は PK 辞書で絞らないため、Ry_Code 条件を明示する
            if (!IsNew) {
                var q = new RecordQuery();
                q.And.Add(new Condition { Column = "Ry_Code", Operator = "=", Value = Ry_Code });
                await RowMgr.InitializeAsync(q);

                foreach (var e in RowMgr.DataList.OfType<RyLibRowEntity>().OrderBy(r => r.SortNo)) {
                    var vm = new RowVm { Entity = e };
                    e.CreateWorkingMemory(vm.Working);
                    Rows.Add(vm);
                }
            }
        }

        private void BuildZairyouOptions() {
            var zm = GlobalState.ZmstEntityMgr
                ?? throw new Exception("ZmstEntityMgr が初期化されていません。");

            ZairyouOptions = zm.DataList
                .OfType<IZmstEntity>()
                .Where(z => z is not CategoryEntity && z.DataType == NxDataType.Zairyou)
                .Select(z => {
                    return new ZairyouOption(z.DataID, z.ZCode, $"{z.ZCode}　{z.DataName}");
                })
                .OrderBy(o => o.Code, StringComparer.Ordinal)
                .ToList();
        }

        // 正味量(g) = 準備量 × 単位重量(g/1単位) × (1 − 廃棄率/100) × (重量変化率/100)
        // 単位重量は、選択中の単位(tan_cd)の tan_m.jyuuryou。材料・準備量・単位のどれかが変わったら再計算する
        protected float CalcNetWeight(Dictionary<string, object?> row) {
            var zmst = GlobalState.ZmstEntityMgr?.Get(IntOf(row, "LocalCode")) as IZmstEntity;
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

        // 計算した正味量をワーキングに入れる（保存対象の Syoumi 列）
        protected void RecalcSyoumi(RowVm vm) {
            vm.Working["Syoumi"] = CalcNetWeight(vm.Working);
        }

        // ---------------------------------------------------------
        // 行の表示ヘルパー（razor から使う）
        // ---------------------------------------------------------
        protected static int IntOf(Dictionary<string, object?> row, string key) =>
            row.TryGetValue(key, out var v) && int.TryParse(v?.ToString(), out var n) ? n : 0;

        protected static string junbiTxt(Dictionary<string, object?> row) =>
            row.TryGetValue("Junbi", out var v) && float.TryParse(v?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var f)
                ? f.ToString("0.###", CultureInfo.InvariantCulture)
                : "";

        protected static float FloatOf(Dictionary<string, object?> row, string key) {
            var kv = row.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            return kv.Value != null && float.TryParse(kv.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var f) ? f : 0f;
        }

        protected static string SyoumiText(Dictionary<string, object?> row) =>
            FloatOf(row, "Syoumi").ToString("0.##", CultureInfo.InvariantCulture);

        // 選択中の材料の単位リスト（Zmst の TanList）
        protected List<(int Code, string Label)> GetTanOptions(Dictionary<string, object?> row) {
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

        // ---------------------------------------------------------
        // 行の編集（値を変えたら Dirty にするだけ。保存はロストフォーカス時）
        // ---------------------------------------------------------
        protected void OnZairyouChanged(RowVm vm, object? value) {
            int.TryParse(value?.ToString(), out var localCode);
            var opt = ZairyouOptions.FirstOrDefault(o => o.LocalCode == localCode);

            vm.Working["LocalCode"] = localCode;
            vm.Working["Z_code"] = opt?.Code ?? "";

            // 材料が変わったら、その材料の最初の単位に合わせ直す
            var tans = GetTanOptions(vm.Working);
            vm.Working["tan_cd"] = tans.Count > 0 ? tans[0].Code : 0;
            RecalcSyoumi(vm);
            vm.Dirty = true;
        }

        protected void OnJunbiChanged(RowVm vm, object? value) {
            float.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var f);
            vm.Working["Junbi"] = f;
            RecalcSyoumi(vm);
            vm.Dirty = true;
        }

        protected void OnTanChanged(RowVm vm, object? value) {
            int.TryParse(value?.ToString(), out var code);
            vm.Working["tan_cd"] = code;
            RecalcSyoumi(vm);
            vm.Dirty = true;
        }

        // ---------------------------------------------------------
        // 行の追加・削除
        // ---------------------------------------------------------
        protected void AddRow() {
            if (RowMgr == null || RyData == null) return;

            var e = new RyLibRowEntity {
                DBcon = RowMgr.DBcon,
                SelfObjMgr = RowMgr,
                TenantCode = RyData.TenantCode,
                CurrUsrID = RyData.CurrUsrID
            };

            // 全カラムを既定値で初期化してから値を入れる（_rawData が空だと DataID の取得で落ちるため）
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

            var vm = new RowVm { Entity = e };
            e.CreateWorkingMemory(vm.Working);
            Rows.Add(vm);
        }

        protected async Task RemoveRowAsync(RowVm vm) {
            if (RowMgr == null) return;

            if (vm.Entity.DataID > 0) {
                // 保存済みの行は DB からも削除（物理削除）
                var r = await RowMgr.DeleteDataAsync(new List<int> { vm.Entity.DataID }, false);
                if (r.FailedIds.Count > 0) {
                    await JS.InvokeVoidAsync("aknAlert", $"削除失敗: {r.Message}");
                    return;
                }
            } else {
                // 未保存の行はメモリ上から外すだけ
                RowMgr._RemoveFromList(vm.Entity);
            }

            Rows.Remove(vm);
        }

        // ---------------------------------------------------------
        // 行の保存（RyLibRowEntity.SaveAsync）
        // ---------------------------------------------------------
        private async Task<bool> SaveRowAsync(RowVm vm) {
            if (vm.Saving) return true;
            if (RyData == null) return false;

            vm.Saving = true;
            try {
                vm.Working["Ry_Code"] = Ry_Code;
                vm.Working["tenant_code"] = RyData.TenantCode;
                vm.Working["sort_no"] = Rows.IndexOf(vm) + 1;

                var result = await vm.Entity.SaveAsync(vm.Working);
                if (!result.Success) {
                    await JS.InvokeVoidAsync("aknAlert", $"行の保存失敗: {result.Message}");
                    return false;
                }

                vm.Dirty = false;
                return true;
            } finally {
                vm.Saving = false;
            }
        }

        // 行からフォーカスが外れたとき（razor の <tr @onfocusout>）
        protected async Task OnRowFocusOut(RowVm vm) {
            if (!vm.Dirty) return;

            // 材料が未選択の行は保存しない（保存ボタン時に警告する）
            if (IntOf(vm.Working, "LocalCode") == 0) return;

            await SaveRowAsync(vm);
        }

        // 変更が残っている行をまとめて保存（行の削除で詰まった sort_no もここで反映）
        private async Task<bool> FlushRowsAsync() {
            for (int i = 0; i < Rows.Count; i++) {
                var vm = Rows[i];
                if (IntOf(vm.Working, "sort_no") != i + 1) vm.Dirty = true;
                if (!vm.Dirty) continue;
                if (!await SaveRowAsync(vm)) return false;
            }
            return true;
        }

        // ---------------------------------------------------------
        // ヘッダ保存 / 閉じる
        // ---------------------------------------------------------
        public async Task SaveAsync() {
            if (_saving || RyData == null) return;

            if (Rows.Any(r => IntOf(r.Working, "LocalCode") == 0)) {
                await JS.InvokeVoidAsync("aknAlert", "材料が未選択の行があります。");
                return;
            }

            _saving = true;
            try {
                // 1. ヘッダ（材料行はサブテーブルとして渡さない）
                var result = await RyData.SaveAsync(WorkingRaw);
                if (!result.Success) {
                    await JS.InvokeVoidAsync("aknAlert", $"保存失敗: {result.Message}");
                    return;
                }

                // 新規料理は、初めて正本へ保存できた時点で一覧に加える
                if (IsNew && !_committed) {
                    GlobalState.RyLibEntityMgr!.InsertNewDataItem(RyData);
                    _committed = true;
                }

                // 2. 未保存の材料行
                if (!await FlushRowsAsync()) return;

                await OnSaved.InvokeAsync();
                await JS.InvokeVoidAsync("aknAlert", "保存しました。");
            } catch (Exception ex) {
                Console.WriteLine($"RyouriEditBase.SaveAsync 例外: {ex.Message}");
                await JS.InvokeVoidAsync("aknAlert", $"例外発生: {ex.Message}");
            } finally {
                _saving = false;
            }
        }

        public async Task CloseAsync() {
            // 未確定の新規料理は DataOpen していないので DataClose も呼ばない
            if (RyData != null && (!IsNew || _committed)) await RyData.DataCloseAsync();
            await OnClose.InvokeAsync();
        }
    }
}