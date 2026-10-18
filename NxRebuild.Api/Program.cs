using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using NxRebuild.Api.Data;
using NxRebuild.Api.Models;
using NxRebuild.Api.Schema;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

public class ApiProgram
{
    public static void Main(string[] args)
    {
        // ★ sub → NameIdentifier の自動変換を防ぐ
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        var builder = WebApplication.CreateBuilder(args);

        // PostgreSQL
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Identity（Cookie 認証をデフォルトにしない）
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // ★ JWT をデフォルト認証方式に固定
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
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

        // DB 接続 をDI登録すると例外エラーの原因になる
       // var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
       // builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(connectionString));

        builder.Services.AddControllers();


        builder.Services.AddScoped<IDatabaseSchemaProvider, DatabaseSchemaProvider>();



        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowWasm", policy =>
            {
                policy.WithOrigins("http://localhost:5270")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();   
            });
        });

        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // ★★★ ユーザー自動作成（新仕様でもそのまま使える）★★★
        using (var scope = app.Services.CreateScope()) {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var email = "test@example.com";
            var password = "Test123!";

            if (userManager.FindByEmailAsync(email).Result == null) {
                var user = new ApplicationUser {
                    UserName = email,
                    Email = email,
                    TenantCode = "00000000-0001-7000-8000-000000000000"
                };

                var result = userManager.CreateAsync(user, password).Result;

                if (!result.Succeeded) {
                    Console.WriteLine("ユーザー1作成失敗:");
                    foreach (var err in result.Errors)
                        Console.WriteLine($" - {err.Description}");
                } else {
                    Console.WriteLine("テストユーザー1作成完了");
                }
            }

            email = "test2@example2.com";
            password = "Test223!";

            if (userManager.FindByEmailAsync(email).Result == null) {
                var user = new ApplicationUser {
                    UserName = email,
                    Email = email,
                    TenantCode = "00000000-0001-7000-8000-000000000000"
                };

                var result = userManager.CreateAsync(user, password).Result;

                if (!result.Succeeded) {
                    Console.WriteLine("ユーザー2作成失敗:");
                    foreach (var err in result.Errors)
                        Console.WriteLine($" - {err.Description}");
                } else {
                    Console.WriteLine("テストユーザー2作成完了");
                }
            }
        }


        // --- ミドルウェア（順番が超重要） ---
        app.UseRouting(); // ← 1回だけ

        //if (app.Environment.IsDevelopment())
        //{
        //    app.UseCors("AllowWasm");
        //}
        app.UseCors("AllowWasm");

        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment()) {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();

        app.Run();
    }
}