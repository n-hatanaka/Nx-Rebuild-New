using Microsoft.AspNetCore.Components;
using NxRebuild.shared;

public partial class Edit
{
    [Parameter] public int LocalCode { get; set; }

    public ZmstEntity Entity { get; set; }

    public List<NutritionProperty> NutritionList { get; set; } = new();
    public Dictionary<string, decimal?> NutritionValues { get; set; } = new();

    // 単位追加用
    public int NewTanCd { get; set; }
    public string NewTanName { get; set; }
    public float NewJun { get; set; }
    public float NewJyuuryou { get; set; }

    // 基本情報
    public string ZName
    {
        get => Entity._rawData["Z_name"]?.ToString() ?? "";
        set => Entity._rawData["Z_name"] = value;
    }

    public int GunCd
    {
        get => Convert.ToInt32(Entity._rawData["gun_cd"]);
        set => Entity._rawData["gun_cd"] = value;
    }

    public string Txt
    {
        get => Entity._rawData["Txt"]?.ToString() ?? "";
        set => Entity._rawData["Txt"] = value;
    }

    protected override async Task OnInitializedAsync()
    {
        Entity = await ZmstMgr.LoadAsync(LocalCode);

        // 栄養素メタデータ
        var nutRecords = await NutMgr.LoadRecordsAsync();
        NutritionList = nutRecords
            .Select(r => new NutritionProperty().LoadRawData(r))
            .Where(n => n.Nutrition)
            .OrderBy(n => n.SortNo)
            .ToList();

        // 栄養素値を RawData から読み込む
        foreach (var nut in NutritionList)
        {
            if (Entity._rawData.TryGetValue(nut.Col, out var v))
                NutritionValues[nut.Col] = v != null ? Convert.ToDecimal(v) : null;
            else
                NutritionValues[nut.Col] = null;
        }

        // tan_m は ZmstMgr.LoadAsync 内でロード済みとする
        Entity.TanList = Entity.TanList
            .OrderBy(t => Convert.ToDouble(t.Raw["jun"]))
            .ToList();
    }

    public void AddTanUnit()
    {
        var raw = new Dictionary<string, object?>
        {
            ["tenant_code"] = Entity._rawData["tenant_code"],
            ["Z_Code"] = Entity._rawData["Z_code"],
            ["LocalCode"] = Entity.LocalCode,
            ["tan_cd"] = NewTanCd,
            ["tan_Name"] = NewTanName,
            ["jun"] = NewJun,
            ["jyuuryou"] = NewJyuuryou
        };

        var t = new TanMEntity(raw);

        Entity.TanList.Add(t);

        // 入力欄クリア
        NewTanCd = 0;
        NewTanName = "";
        NewJun = 0;
        NewJyuuryou = 0;
    }

    public async Task SaveAsync()
    {
        // 栄養素の反映
        foreach (var kv in NutritionValues)
            Entity._rawData[kv.Key] = kv.Value;

        // 単位は ZmstEntity.SaveAsync 内でまとめて保存する
        await Entity.SaveAsync();
    }
}