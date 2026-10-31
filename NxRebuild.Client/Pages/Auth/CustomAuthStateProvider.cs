using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

//UI 側で：
//var tenant_code = (await AuthStateProvider.GetAuthenticationStateAsync())
//    .User.FindFirst("tenant_code")?.Value;
//が どこでも使えるようになる。

namespace NxRebuild.Client.Pages.Auth
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _js;

        public CustomAuthStateProvider(IJSRuntime js)
        {
            _js = js;
        }

        private static ClaimsPrincipal BuildUser(string token) {
            try {
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                if (jwt.ValidTo <= DateTime.UtcNow)                 // 期限切れ
                    return new ClaimsPrincipal(new ClaimsIdentity());
                return new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "jwt"));
            } catch {
                return new ClaimsPrincipal(new ClaimsIdentity());   // 壊れたトークン
            }
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync() {
            var token = await _js.InvokeAsync<string>("sessionStorage.getItem", "authToken");
            return string.IsNullOrEmpty(token)
                ? new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))
                : new AuthenticationState(BuildUser(token));
        }

        public void NotifyUserAuthentication(string token) =>
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(BuildUser(token))));

        public void NotifyUserLogout()
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymous)));
        }
    }
}
