using NxRebuild.shared;
using NxRebuild.Client.Pages.NxPrograms.DB;

namespace NxRebuild.Client.Services
{
    // 簡易グローバルストレージ：NutritionPropertiesMgr を格納するために使います。
    // 起動後に必要なタイミングで GlobalState.NutritionPropertiesMgr = new NutritionPropertiesMgr(...);
    public static class GlobalState
    {
        public static SyncNutPropertyObjMgr? NutProperties { get; set; }

        public static SyncScheduler<int>? NutPropScheduler { get; set; }

        //public static IZmstEntityMgr? ZmstEntityMgr { get; set; }
        public static IZmstEntityMgr? ZmstEntityMgr { get; set; }

        public static SyncScheduler<int>? ZmstScheduler{ get; set; }

        //MainWnd の初期化は、!DbState.IsInitialized のときだけ走ります。
        //ログアウト→再ログインをリロードなしで行うと、初期化が丸ごとスキップされて、
        //前のユーザー・前のテナントのオブジェクトとデータが、そのまま使われます。
        //WASMごと作り直すのが、いちばん確実です。
        public static void Reset() {
            NutPropScheduler?.Dispose(); NutPropScheduler = null;
            ZmstScheduler?.Dispose(); ZmstScheduler = null;
            NutProperties = null;
            ZmstEntityMgr = null;
        }
    }
}
