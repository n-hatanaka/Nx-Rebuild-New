using Microsoft.AspNetCore.Components;
using NxRebuild.Client.Pages.NxPrograms.DB;
using NxRebuild.Client.Services;
using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.Zmst {
    // 栄養素値を動的に保持するモデルクラス
    // 栄養素値を動的に保持するモデルクラス
    public class NutrientModel {
        private readonly Dictionary<string, decimal?> _values = new();

        // 動的アクセス（実際の値）
        public decimal? this[string key] {
            get => _values.TryGetValue(key, out var v) ? v : null;
            set => _values[key] = value;
        }

        // Blazor が式ツリー用に必要とするダミープロパティ
        public decimal? ValueProxy { get; set; }

        // ★ WorkingRaw → NutrientModel に一括ロード
        public void LoadFromRaw(Dictionary<string, object?> raw, IEnumerable<string> cols) {
            foreach (var col in cols) {
                if (raw.TryGetValue(col, out var v) && v != null) {
                    if (decimal.TryParse(v.ToString(), out var d))
                        _values[col] = d;
                    else
                        _values[col] = null;
                } else {
                    _values[col] = null;
                }
            }
        }

        // ★ NutrientModel → WorkingRaw に書き戻し
        public void SaveToRaw(Dictionary<string, object?> raw) {
            foreach (var kv in _values)
                raw[kv.Key] = kv.Value;
        }
    }

    public partial class Z_EditBase : ComponentBase {
        [Parameter] public int LocalCode { get; set; }
        public List<CategoryEntity> GunList { get => GlobalState.ZmstEntityMgr.GunList; }
        public IZmstEntity Entity { get; set; }

        public NutrientModel Nut { get; set; } = new();
        public List<INutritionProperty> NutritionList { get; set; } = new();
        public Dictionary<string, decimal?> NutritionValues { get; set; } = new();

        public Dictionary<string, object?> WorkingRaw { get; set; } = new();
        public List<List<Dictionary<string, object?>>> WorkingSubTables { get; set; } = new();
        public List<Dictionary<string, object?>> WorkingTanList { get; set; } = new();

        // 単位追加用
        public int NewTanCd { get; set; }
        public string NewTanName { get; set; }
        public float NewJun { get; set; }
        public float NewJyuuryou { get; set; }


        // 基本情報
        public string ZName {
            get => WorkingRaw.TryGetValue("Z_name", out var v)
                    ? v?.ToString() ?? ""
                    : "";
            set => WorkingRaw["Z_name"] = value;
        }

        public int GunCd {
            get => WorkingRaw.TryGetValue("gun_cd", out var v)
                    ? Convert.ToInt32(v)
                    : 0;
            set => WorkingRaw["gun_cd"] = value;
        }

        public string Txt {
            get => WorkingRaw.TryGetValue("Txt", out var v)
                    ? v?.ToString() ?? ""
                    : "";
            set => WorkingRaw["Txt"] = value;
        }

        public decimal? GetNut(string key) {
            if (WorkingRaw.TryGetValue(key, out var v)) {
                if (decimal.TryParse(v?.ToString(), out var d))
                    return d;
            }
            return null;
        }

        public void SetNut(string key, decimal? value) {
            WorkingRaw[key] = value?.ToString() ?? "";
        }



        protected override async Task OnInitializedAsync() {
            var zm = GlobalState.ZmstEntityMgr
                ?? throw new Exception($"ZmstEntityMgr が初期化されていません。");

            var nut = GlobalState.NutProperties
                ?? throw new Exception("NutProperties が初期化されていません。");

            // すでにロード済みの読み出すだけ
            Entity = zm.Get(LocalCode)
                ?? throw new Exception($"LocalCode={LocalCode} の Zmst がロードされていません。");

            // ★ DeepCopy を Entity にやらせる
            Entity.CreateWorkingMemory(WorkingRaw, WorkingSubTables);



            // TanList は WorkingSubTables[0]
            if (WorkingSubTables.Count == 0)
                WorkingSubTables.Add(new List<Dictionary<string, object?>>());

            WorkingTanList = WorkingSubTables[0];



            // UI バインド用の値を WorkingRaw から取り出す
            ZName = WorkingRaw["Z_name"]?.ToString();
            GunCd = Convert.ToInt32(WorkingRaw["gun_cd"]);
            Txt = WorkingRaw["Txt"]?.ToString();


            // 栄養素メタデータ（これもロード済み）
            // ① 全メタデータ
            var all = nut.DataList.OfType<INutritionProperty>();

            // ② Nutrition = true のみ
            var filtered = all.Where(x => x.Nutrition);

            // ③ SortNo 順に並び替え
            var sorted = filtered.OrderBy(x => x.SortNo);

            // ④ 最終リスト
            NutritionList = sorted.ToList();

            // 栄養素値を WorkingRaw から読み込む
            Nut.LoadFromRaw(WorkingRaw, NutritionList.Select(np => np.Col));


        }


        public void AddTanUnit() {
            var raw = new Dictionary<string, object?> {
                ["tenant_code"] = WorkingRaw["tenant_code"],
                ["Z_Code"] = WorkingRaw["Z_code"],
                ["LocalCode"] = Entity.DataID,
                ["tan_cd"] = NewTanCd,
                ["tan_Name"] = NewTanName,
                ["jun"] = NewJun,
                ["jyuuryou"] = NewJyuuryou
            };

            var t = new Dictionary<string, object?>(raw);

            // ★ 正本ではなく WorkingTanList に追加する
            WorkingTanList.Add(t);

            // 入力欄クリア
            NewTanCd = 0;
            NewTanName = "";
            NewJun = 0;
            NewJyuuryou = 0;
        }


        public async Task SaveAsync() {
            // ★ NutrientModel の内容を WorkingRaw に反映
            Nut.SaveToRaw(WorkingRaw);

            // 単位は ZmstEntity.SaveAsync 内でまとめて保存する
            await Entity.SaveAsync(WorkingRaw, WorkingSubTables);
        }

    }

}