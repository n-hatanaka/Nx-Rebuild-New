using System.Net.Http;
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace NxRebuild.Client.Pages.Auth
{
    public class JwtAuthorizationMessageHandler : DelegatingHandler
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly SessionEvents _events;

        public JwtAuthorizationMessageHandler(IJSRuntime jsRuntime, SessionEvents events) {
            _jsRuntime = jsRuntime; _events = events;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            string token = null;

            try {
                token = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "authToken");
            } catch {
                // JSInterop が未初期化のときは token = null のまま
                token = null;
            }

            if (!string.IsNullOrEmpty(token)) {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            try {
                //return await base.SendAsync(request, cancellationToken);
                var res = await base.SendAsync(request, cancellationToken);
                if (res.StatusCode == HttpStatusCode.Unauthorized &&
                    request.RequestUri!.AbsolutePath.EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase) == false)
                    _events.RaiseUnauthorized();      // ログイン自体の401(パスワード誤り)は対象外
                return res;
            } catch (HttpRequestException ex) {
                // JSInterop が未初期化の可能性があるので、ここも try/catch で包む
                try {
                    await _jsRuntime.InvokeVoidAsync("console.error", ex.ToString());
                } catch {
                    // 何もしない
                }

                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) {
                    ReasonPhrase = "Network error: " + ex.Message
                };
            }
        }

    }
}
