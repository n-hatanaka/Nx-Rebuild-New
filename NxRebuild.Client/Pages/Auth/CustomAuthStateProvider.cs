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

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _js.InvokeAsync<string>("sessionStorage.getItem", "authToken");

            if (string.IsNullOrEmpty(token))
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            // JWT の claims をベースに identity を作る
            var claims = jwt.Claims.ToList();

            // tenant_code を取り出す
            var tenantCode = jwt.Claims.FirstOrDefault(c => c.Type == "tenant_code")?.Value;

            // tenant_code を Claims に追加
            if (!string.IsNullOrEmpty(tenantCode))
            {
                claims.Add(new Claim("tenant_code", tenantCode));
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            return new AuthenticationState(user);
        }

        public void NotifyUserAuthentication(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            var claims = jwt.Claims.ToList();

            var tenantCode = jwt.Claims.FirstOrDefault(c => c.Type == "tenant_code")?.Value;
            if (!string.IsNullOrEmpty(tenantCode))
            {
                claims.Add(new Claim("tenant_code", tenantCode));
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        public void NotifyUserLogout()
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymous)));
        }
    }
}
