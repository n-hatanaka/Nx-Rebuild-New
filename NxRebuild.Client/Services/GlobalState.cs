using NxRebuild.shared;
using NxRebuild.Client.Pages.NxPrograms.DB;

namespace NxRebuild.Client.Services
{
    // 簡易グローバルストレージ：NutritionPropertiesMgr を格納するために使います。
    // 起動後に必要なタイミングで GlobalState.NutritionPropertiesMgr = new NutritionPropertiesMgr(...);
    public static class GlobalState
    {
        public static SyncNutPropertyObjMgr? NutProperties { get; set; }

        //一旦スタンドアロン状態で開発を進めていく（あとで直すのを忘れないように
        public static IZmstEntityMgr? ZmstEntityMgr { get; set; }
    }
}
