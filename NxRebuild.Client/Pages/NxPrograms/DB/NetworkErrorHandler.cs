using System.Net;
namespace NxRebuild.Client.Pages.NxPrograms.DB {
    public static class NxHttpClientFactory {
        public static HttpClient Create() {
            // 1. ハンドラをインスタンス化
            var errorHandler = new NetworkErrorHandler();

            // 2. その裏側に通常の通信ハンドラをぶら下げる
            errorHandler.InnerHandler = new HttpClientHandler();

            // 3. エラーハンドラをラップした HttpClient を生成して返す
            return new HttpClient(errorHandler);
        }
    }

    public sealed class NetworkErrorHandler : DelegatingHandler {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            const int retryCount = 1;
            Console.WriteLine("NetworkErrorHandler");
            for (int retry = 0; ; retry++) {
                try {
                    return await base.SendAsync(request, cancellationToken);
                } catch (HttpRequestException ex) {
                    // GETだけ1回リトライ
                    if (request.Method == HttpMethod.Get && retry < retryCount) {
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    return NetworkDown(ex);
                } catch (TaskCanceledException ex)
                      when (!cancellationToken.IsCancellationRequested) {
                    // Timeout

                    if (request.Method == HttpMethod.Get && retry < retryCount) {
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    return NetworkDown(ex);
                }
            }
        }

        private static HttpResponseMessage NetworkDown(Exception ex)
            => new(HttpStatusCode.ServiceUnavailable) {
                ReasonPhrase = $"Network error: {ex.Message}"
            };
    }

}
