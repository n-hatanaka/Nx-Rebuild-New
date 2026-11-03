using System.Net;

namespace NxRebuild.Client.Pages {
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
