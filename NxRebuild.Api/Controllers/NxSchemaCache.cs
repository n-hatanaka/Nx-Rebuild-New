using NxRebuild.Api.Schema;
using NxRebuild.shared;

namespace NxRebuild.Api.Controllers {
    //型マップは、スキーマが変わらない限り同じ内容なので、プロセス内で1回だけ作って使い回す。
    //NxTypeMap は、構築後に読み取りしかしないので、複数のリクエストから同時に使っても問題ない。
    //スキーマ変更のあとは、アプリを再起動すれば、新しい型マップになる。
    internal static class NxSchemaCache {
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private static NxTypeMap? _map;

        public static async Task<NxTypeMap> GetAsync(IDatabaseSchemaProvider provider) {
            if (_map != null) return _map;

            await _lock.WaitAsync();
            try {
                _map ??= NxTypeMapBuilder.FromSchemas(await provider.GetSchemasAsync());
                return _map;
            } finally {
                _lock.Release();
            }
        }

        // スキーマを変えた後に再取得させたいとき用(通常はアプリの再起動でよい)
        public static void Invalidate() => _map = null;
    }
}
