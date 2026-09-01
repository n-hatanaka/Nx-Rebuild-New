using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using NxRebuild.Api.Data;
using NxRebuild.Api.Models;
using NxRebuild.Api.Schema;
using System.Data;
using System.Text;
using Microsoft.AspNetCore.Mvc;

public class ApiProgram
{
    public static void Main(string[] args)
    {
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

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddScoped<IDatabaseSchemaProvider, DatabaseSchemaProvider>(); 
     
        var app = builder.Build();

        if (app.Environment.IsDevelopment()) {
            app.UseSwagger();
            app.UseSwaggerUI();   // ← UI を有効化（新方式に対応済み）
        }



        //app.UseHttpsRedirection();

        app.UseRouting();

        // 最初のテストユーザー作成（同期ブロッキングで呼び出す）
        using (var scope = app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var email = "test@example.com";
            var password = "Test123!";
            var user = userManager.FindByEmailAsync(email).GetAwaiter().GetResult();
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email
                    // Id と GroupCode はコンストラクタで UUIDv7 自動生成
                };

                var result = userManager.CreateAsync(user, password).GetAwaiter().GetResult();
                if (result.Succeeded)
                {
                    user.TenantCode = "00000000-0001-7000-8000-000000000000";
                    userManager.UpdateAsync(user).GetAwaiter().GetResult();
                    Console.WriteLine("testユーザーを作成しました。");
                }
            }
            else
            {
                // 存在する場合はグループコードが固定されているか確認（必要に応じて更新）
                if (user.TenantCode != "00000000-0001-7000-8000-000000000000")
                {
                    user.TenantCode = "00000000-0001-7000-8000-000000000000";
                    userManager.UpdateAsync(user).GetAwaiter().GetResult();
                }
            }
        }

        // ★ 開発環境（IsDevelopment）のときだけ CORS ルールを有効化する
        app.UseRouting();

        if (app.Environment.IsDevelopment()) {
            app.UseCors("AllowWasm");
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // 🔥 AuthController を有効化
        app.MapControllers();

        app.Run();
    }

    
}
