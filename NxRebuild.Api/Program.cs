using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using NxRebuild.Api.Data;
using NxRebuild.Api.Models;
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

        // DB 接続
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(connectionString));

        builder.Services.AddControllers();

        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowWasm", policy =>
            {
                policy.WithOrigins("http://localhost:5270")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // --- ミドルウェア（順番が超重要） ---
        app.UseRouting(); // ← 1回だけ

        if (app.Environment.IsDevelopment())
        {
            app.UseCors("AllowWasm");
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}