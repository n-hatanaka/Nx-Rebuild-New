using NxRebuild.shared;
using NxRebuild.Client.Pages.NxPrograms.DB;

namespace NxRebuild.Client.Services
{
    // 簡易グローバルストレージ：NutritionPropertiesMgr を格納するために使います。
    // 起動後に必要なタイミングで GlobalState.NutritionPropertiesMgr = new NutritionPropertiesMgr(...);
    public static class GlobalState
    {
        public static SyncNutPropertyObjMgr? NutProperties { get; set; }
    }
}
