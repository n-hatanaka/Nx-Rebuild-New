using System.Data;
using System.Runtime.CompilerServices;

namespace NxRebuild.Client.Pages.NxPrograms.DB {
    // 接続ごとに1つ。「ユーザー操作中は同期を止める」ための制御
    public sealed class NxSyncControl {
        private static readonly ConditionalWeakTable<IDbConnection, NxSyncControl> _map = new();
        public static NxSyncControl For(IDbConnection con) => _map.GetValue(con, _ => new NxSyncControl());

        private int _pauseCount;                       // ユーザー操作の重なりを数える
        private readonly SemaphoreSlim _slot = new(1, 1); // 同期の実行枠(接続全体で1つ)

        // ---- ユーザー操作側 ----
        // 同期を止め、実行中の同期があれば終わるまで待つ。Dispose で再開(入れ子OK)
        public async Task<IDisposable> PauseAsync() {
            Interlocked.Increment(ref _pauseCount);
            await _slot.WaitAsync();      // 実行中の同期が終わるのを待つ
            _slot.Release();              // 以後の同期は _pauseCount を見て自分で退く
            return new Resumer(this);
        }

        // ---- 同期側 ----
        // 一時停止中、または他の同期が実行中なら null(その周期はスキップ)
        public IDisposable? TryBeginSync() {
            if (Volatile.Read(ref _pauseCount) > 0) return null;
            if (!_slot.Wait(0)) return null;
            if (Volatile.Read(ref _pauseCount) > 0) {   // 枠を取った後でもう一度確認
                _slot.Release();
                return null;
            }
            return new SlotReleaser(_slot);
        }

        private sealed class Resumer : IDisposable {
            private NxSyncControl? _c;
            public Resumer(NxSyncControl c) => _c = c;
            public void Dispose() {
                var c = Interlocked.Exchange(ref _c, null);
                if (c != null) Interlocked.Decrement(ref c._pauseCount);
            }
        }

        private sealed class SlotReleaser : IDisposable {
            private SemaphoreSlim? _s;
            public SlotReleaser(SemaphoreSlim s) => _s = s;
            public void Dispose() => Interlocked.Exchange(ref _s, null)?.Release();
        }
    }
}