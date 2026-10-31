using NxRebuild.shared;

namespace NxRebuild.Client.Pages.NxPrograms.DB {

    // =======================================================
    // SyncScheduler
    //
    // 定期的に同期処理を実行するクラス。
    //
    // SyncBaseDataObjMgr とは責務を分離し、
    // 「いつ同期するか」だけを管理する。
    //
    // 同期そのものは _syncFunc に委譲する。
    //
    // Start()
    //      ↓
    // 一定間隔で _syncFunc 実行
    //      ↓
    // Stop() で停止
    //
    // SyncBaseDataObjMgr は同期処理のみを担当し、
    // 本クラスはスケジューリングのみを担当する。
    // =======================================================
    public class SyncScheduler<TKey> : IDisposable {

        // ===================================================
        // 実際の同期処理
        //
        // 例)
        // () => SyncMgr.SyncData()
        // ===================================================
        private readonly Func<Task<NxOperationResult<TKey>>> _syncFunc;

        // ===================================================
        // スケジューラ停止用トークン
        // ===================================================
        private CancellationTokenSource? _cts;

        // ===================================================
        // 実行中判定
        // ===================================================
        public bool IsRunning => _cts != null;

        // ===================================================
        // 同期間隔
        // コンストラクタで設定
        // 指定がない場合は30秒
        // ===================================================
        public TimeSpan Interval { get; }


        // ===================================================
        // コンストラクタ
        // ===================================================
        public SyncScheduler(
            Func<Task<NxOperationResult<TKey>>> syncFunc,
            long syncIntervalSeconds = 30) {
            _syncFunc = syncFunc;
            
            if (syncIntervalSeconds <= 0)
                syncIntervalSeconds = 30;

            Interval = TimeSpan.FromSeconds(syncIntervalSeconds);
        }

        // ===================================================
        // 定期同期開始
        //
        // 二重起動防止
        // ===================================================
        public void Start() {

            if (_cts != null)
                return;

            _cts = new CancellationTokenSource();

            _ = RunAsync(_cts.Token);
        }

        // ===================================================
        // 定期同期停止
        // ===================================================
        public void Stop() {

            _cts?.Cancel();
            _cts?.Dispose();

            _cts = null;
        }

        // ===================================================
        // 定期実行ループ
        //
        // Sync処理
        //   ↓
        // Interval待機
        //   ↓
        // 繰り返し
        //
        // 通信エラーが発生しても
        // スケジューラ自体は停止しない。
        // ===================================================
        private async Task RunAsync(
            CancellationToken token) {

            while (!token.IsCancellationRequested) {

                try {

                    await _syncFunc();

                } catch (Exception ex) {

                    Console.WriteLine(ex);
                }

                try {

                    await Task.Delay(
                        Interval,
                        token);

                } catch {

                    // Stop() による終了
                    break;
                }
            }
        }

        // ===================================================
        // リソース解放
        // ===================================================
        public void Dispose() {

            Stop();
        }
    }
}