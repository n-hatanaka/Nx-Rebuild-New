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

        public JwtAuthorizationMessageHandler(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            string token = null;

            try {
                token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
            } catch {
                // JSInterop が未初期化のときは token = null のまま
                token = null;
            }

            if (!string.IsNullOrEmpty(token)) {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            try {
                return await base.SendAsync(request, cancellationToken);
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
