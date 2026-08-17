using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AveroNova.OfflineAuth.Tests;

/// <summary>
/// Development API host with an isolated server SQLite file (never AveroNovaLocal.db).
/// </summary>
public sealed class GateWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _serverDbPath;
    private readonly string? _previousConnectionString;
    private readonly string? _previousSigningKey;

    public string ServerDbPath => _serverDbPath;

    public GateWebApplicationFactory()
    {
        _serverDbPath = Path.Combine(Path.GetTempPath(), $"AveroNovaDev-gate-{Guid.NewGuid():N}.db");
        _previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        _previousSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $"Data Source={_serverDbPath}");
        Environment.SetEnvironmentVariable(
            "Jwt__SigningKey",
            "DEV_ONLY_AveroNova_Test_Signing_Key_Change_In_Production_32+");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_serverDbPath}");
        builder.UseSetting("Jwt:SigningKey", "DEV_ONLY_AveroNova_Test_Signing_Key_Change_In_Production_32+");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_serverDbPath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _previousConnectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", _previousSigningKey);
        try
        {
            if (File.Exists(_serverDbPath))
                File.Delete(_serverDbPath);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
