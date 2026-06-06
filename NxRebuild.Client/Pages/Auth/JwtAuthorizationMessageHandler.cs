using System.Net.Http;
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

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // ★ 末尾の cancellationToken を削除し、"authToken" だけを引数にします
            var token = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "authToken");

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
