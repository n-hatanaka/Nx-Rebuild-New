using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NxRebuild.Client;
using NxRebuild.Client.Pages.Auth;
using NxRebuild.Client.Pages.NxPrograms.MDI;
using System.Net.NetworkInformation;
using MudBlazor.Services;
using NxRebuild.Client.Pages.NxPrograms.DB;
using System.Data.Common;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
//builder.Services.AddScoped(sp => new HttpClient
//{
//    BaseAddress = new Uri("http://localhost:5296/") // API の URL
//});

// jwt自動付与ハンドラーをDIコンテナに登録
builder.Services.AddTransient<JwtAuthorizationMessageHandler>();

// ハンドラー付きの HttpClient を設定
builder.Services.AddHttpClient("SecureClient", client => {
    Uri baseAddress;

    if (builder.HostEnvironment.IsDevelopment()) {
        baseAddress = new Uri("http://localhost:5296/");
    } else {
        baseAddress = new Uri(builder.HostEnvironment.BaseAddress);
    }

    client.BaseAddress = baseAddress;
})
.AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

//builder.Services.AddHttpClient("SecureClient", client =>
//{
//    // 基本は自分自身のURL（本番用）を指す
//    var baseAddress = builder.HostEnvironment.BaseAddress;

//    // 開発環境（ローカルPC）のときだけ、APIのポート（5296）に差し替える
//    if (builder.HostEnvironment.IsDevelopment())
//    {
//        baseAddress = "http://localhost:5296/";
//    }

//    client.BaseAddress = new Uri(baseAddress);
//})
//.AddHttpMessageHandler<JwtAuthorizationMessageHandler>(); // ここで連結

// デフォルトの HttpClient として「SecureClient」を使うように設定
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("SecureClient"));

// MudBlazor 必須サービス
builder.Services.AddMudServices();

builder.Services.AddSingleton<WindowManagerBase>();

// ★ インメモリDB管理サービスをシングルトンとして登録
builder.Services.AddSingleton<InMemoryDatabaseState>();
// ホルダーサービスを登録（Sync 系オブジェクトの遅延初期化を行う）
builder.Services.AddSingleton<SyncDataObjMgrServices>();

await builder.Build().RunAsync();
