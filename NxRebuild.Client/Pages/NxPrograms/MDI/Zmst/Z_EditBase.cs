using Microsoft.AspNetCore.Components;
using NxRebuild.Client.Pages.NxPrograms.DB;
using NxRebuild.Client.Services;
using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.MDI.Zmst {
    public partial class Z_EditBase : ComponentBase {
        [Parameter] public int LocalCode { get; set; }
        public List<CategoryEntity> GunList { get => GlobalState.ZmstEntityMgr.GunList; }
        public IZmstEntity Entity { get; set; }

        public List<INutritionProperty> NutritionList { get; set; } = new();
        public Dictionary<string, decimal?> NutritionValues { get; set; } = new();

        public Dictionary<string, object?> WorkingRaw { get; set; } = new();
        public List<TanMEntity> WorkingTanList { get; set; } = new();

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


        protected override async Task OnInitializedAsync() {
            var zm = GlobalState.ZmstEntityMgr
                ?? throw new Exception($"ZmstEntityMgr が初期化されていません。");

            var nut = GlobalState.NutProperties
                ?? throw new Exception("NutProperties が初期化されていません。");

            // すでにロード済みの読み出すだけ
            Entity = zm.Get(LocalCode)
                ?? throw new Exception($"LocalCode={LocalCode} の Zmst がロードされていません。");

            // ★ DeepCopy を Entity にやらせる
            Entity.CreateWorkingMemory(WorkingRaw, WorkingTanList);

            // UI バインド用の値を WorkingRaw から取り出す
            ZName = WorkingRaw["ZName"]?.ToString();
            GunCd = Convert.ToInt32(WorkingRaw["GunCd"]);
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
            foreach (var np in NutritionList)
                NutritionValues[np.Col] = Convert.ToDecimal(WorkingRaw[np.Col]);

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

            var t = new TanMEntity(raw);

            // ★ 正本ではなく WorkingTanList に追加する
            WorkingTanList.Add(t);

            // 入力欄クリア
            NewTanCd = 0;
            NewTanName = "";
            NewJun = 0;
            NewJyuuryou = 0;
        }


        public async Task SaveAsync() {
            // 栄養素の反映
            foreach (var kv in NutritionValues)
                Entity.SetNutritionValue(kv.Key, kv.Value);

            // 単位は ZmstEntity.SaveAsync 内でまとめて保存する
            await Entity.SaveAsync();
        }
    }
}