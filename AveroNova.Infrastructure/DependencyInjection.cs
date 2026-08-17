using System.Text;
using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Auth;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Application.Interfaces.Security;
using AveroNova.Application.Interfaces.License;
using AveroNova.Application.Interfaces.Sync;
using AveroNova.Infrastructure.Licensing;
using AveroNova.Application.Services;
using AveroNova.Infrastructure.Auth;
using AveroNova.Infrastructure.Persistence;
using AveroNova.Infrastructure.Repositories;
using AveroNova.Infrastructure.Security;
using AveroNova.Infrastructure.Sync;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AveroNova.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AuthSecurityOptions>(configuration.GetSection(AuthSecurityOptions.SectionName));

        var useSqlServer = IsSqlServer(connectionString);
        services.AddDbContext<AppDbContext>(options =>
        {
            if (useSqlServer)
            {
                options.UseSqlServer(connectionString);
                // Migrations/snapshot were generated with SQLite store types; SQL Server runtime model
                // differs by provider annotations only (false-positive pending model changes).
                options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            }
            else
            {
                options.UseSqlite(NormalizeSqlite(connectionString));
            }
        });

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ISyncQueueRepository, SyncQueueRepository>();
        services.AddScoped<ISyncEngine, SyncEngine>();
        services.AddSingleton<IConnectivityProbe, AlwaysOnlineConnectivityProbe>();

        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IAuthAuditLogger, AuthAuditLogger>();
        services.AddSingleton<ILoginAttemptProtector, LoginAttemptProtector>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ILicenseService, LicenseService>();

        services.AddPermissionAuthorization();
        services.AddJwtAuthentication(configuration);

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
            throw new InvalidOperationException(
                "Configure Jwt:SigningKey (min 32 characters) via user-secrets, environment variable Jwt__SigningKey, or Development settings.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();
        return services;
    }

    private static bool IsSqlServer(string connectionString)
    {
        var cs = connectionString ?? string.Empty;
        return cs.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase)
               || cs.Contains("Server=", StringComparison.OrdinalIgnoreCase)
               || cs.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase)
               || cs.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSqlite(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return "Data Source=AveroNovaDev.db";
        if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            return connectionString;
        return $"Data Source={connectionString}";
    }
}
