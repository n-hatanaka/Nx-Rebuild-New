using System.Data.Common;
using NxRebuild.Client.Pages.Auth;
using System.Collections.Concurrent;

namespace NxRebuild.Client.Pages.NxPrograms.DB
{
    // ホルダーサービス：複数の Sync 系 DataObjMgr を遅延初期化して保存する
    public class SyncDataObjMgrServices
    {
        private readonly ConcurrentDictionary<Type, object> _managers = new();

        public bool IsInitialized<T>() where T : class
        {
            return _managers.ContainsKey(typeof(T));
        }

        public T? GetManager<T>() where T : class
        {
            if (_managers.TryGetValue(typeof(T), out var obj))
                return obj as T;
            return null;
        }

        public async Task InitializeAsync<T>(Func<T> factory, Func<T, Task>? init = null) where T : class
        {
            var t = typeof(T);
            if (_managers.ContainsKey(t)) return;

            var mgr = factory();
            if (init != null)
            {
                await init(mgr);
            }

            _managers[t] = mgr!;
        }

        // 便宜的なヘルパー：NutritionProperty 用の初期化
        public Task InitializeNutPropertyAsync(DbConnection conn, HttpClient http, CustomAuthStateProvider auth, Guid tenantCode, Guid currentUserId)
        {
            return InitializeAsync(() => new SyncNutPropertyObjMgr(conn, http, auth, tenantCode, currentUserId), (SyncNutPropertyObjMgr m) => m.Initialize());
        }
    }
}
