using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

//UI 側で：
//var groupCode = (await AuthStateProvider.GetAuthenticationStateAsync())
//    .User.FindFirst("group_code")?.Value;
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

            // group_code を取り出す
            var groupCode = jwt.Claims.FirstOrDefault(c => c.Type == "group_code")?.Value;

            // group_code を Claims に追
            if (!string.IsNullOrEmpty(groupCode))
            {
                claims.Add(new Claim("group_code", groupCode));
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

            var groupCode = jwt.Claims.FirstOrDefault(c => c.Type == "group_code")?.Value;
            if (!string.IsNullOrEmpty(groupCode))
            {
                claims.Add(new Claim("group_code", groupCode));
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
