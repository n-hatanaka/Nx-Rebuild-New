using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using NxRebuild.Api.Data;
using NxRebuild.Api.Models;
using System.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


// PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// JWT 認証
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

// 設定ファイルから接続文字列を取得する
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// データベース接続（IDbConnection）をDIコンテナに登録する
// 毎回新しい接続を作成するように「AddTransient」で登録する
builder.Services.AddTransient<IDbConnection>((sp) => new NpgsqlConnection(connectionString));

builder.Services.AddControllers();

//CORS ポリシーを登録
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWasm",
        policy =>
        {
            policy.WithOrigins("http://localhost:5270")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});


// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// 最初のテストユーザー作成
//リリースするときはここをDBから取るように修正する事。
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    var email = "test@example.com";
    var password = "Test123!";
    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
    {
        user = new ApplicationUser
        {
            UserName = email,
            Email = email
            // Id と GroupCode はコンストラクタで UUIDv7 自動生成
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded) {
            user.TenantCode = "00000000-0001-7000-8000-000000000000";
            await userManager.UpdateAsync(user);
            Console.WriteLine("testユーザーを作成しました。");

        }
    } else {
        // 存在する場合はグループコードが固定されているか確認（必要に応じて更新）
        if (user.TenantCode != "00000000-0001-7000-8000-000000000000") {
            user.TenantCode = "00000000-0001-7000-8000-000000000000";
            await userManager.UpdateAsync(user);
        }
    }
}

// ★ 開発環境（IsDevelopment）のときだけ CORS ルールを有効化する
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowWasm");
}

app.UseHttpsRedirection();

app.UseCors("AllowWasm");

// 🔥 認証は UseAuthorization の前に必ず置く
app.UseAuthentication();
app.UseAuthorization();

// WeatherForecast（既存）
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// 🔥 AuthController を有効化
app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
