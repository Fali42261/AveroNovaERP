using AveroNova.Infrastructure;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AveroNova API",
        Version = "v1",
        Description = "AveroNova ERP Auth and Offline-First API"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var configured = builder.Configuration.GetConnectionString("DefaultConnection");
string connectionString;
string providerLabel;
if (!string.IsNullOrWhiteSpace(configured)
    && (configured.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase)
        || configured.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase)
        || (configured.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            && !builder.Environment.IsDevelopment())))
{
    // Production / non-Development SQL Server / Azure SQL only.
    // Development never requires SQL Server — use SQLite (AveroNovaDev.db).
    connectionString = configured;
    providerLabel = configured.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase)
        ? "Azure SQL"
        : "SQL Server";
}
else if (!string.IsNullOrWhiteSpace(configured)
         && configured.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    connectionString = ResolveSqliteConnectionString(configured, builder.Environment.ContentRootPath);
    providerLabel = "SQLite";
}
else
{
    // Development default: SQLite under API/Data/AveroNovaDev.db
    var dbPath = AveroNova.Shared.Helpers.DatabasePath.GetDatabasePath(builder.Environment.ContentRootPath);
    connectionString = $"Data Source={dbPath}";
    providerLabel = "SQLite";
}

EnsureSqliteDirectoryExists(connectionString);
Console.WriteLine($"Database provider: {providerLabel}");
Console.WriteLine($"Database: {RedactConnectionString(connectionString)}");
builder.Services.AddInfrastructure(builder.Configuration, connectionString);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (providerLabel is "Azure SQL" or "SQL Server")
    {
        // Production Azure SQL / SQL Server schema is applied via deployment pipeline.
        // Development always uses SQLite + Migrate (below).
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    var canConnect = await db.Database.CanConnectAsync();
    Console.WriteLine($"Database connectivity: {(canConnect ? "OK" : "FAILED")}");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AveroNova API v1");
    });
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

static string ResolveSqliteConnectionString(string connectionString, string contentRootPath)
{
    const string prefix = "Data Source=";
    var idx = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
    if (idx < 0)
        return connectionString;

    var path = connectionString[(idx + prefix.Length)..].Trim().Trim('"');
    if (string.IsNullOrWhiteSpace(path)
        || path is ":memory:"
        || path.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase)
        || Path.IsPathRooted(path))
    {
        return connectionString;
    }

    var absolute = Path.GetFullPath(Path.Combine(contentRootPath, path));
    return $"Data Source={absolute}";
}

static void EnsureSqliteDirectoryExists(string connectionString)
{
    const string prefix = "Data Source=";
    var idx = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
    if (idx < 0) return;
    var path = connectionString[(idx + prefix.Length)..].Trim().Trim('"');
    if (string.IsNullOrWhiteSpace(path) || path is ":memory:" || path.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
        return;
    var dir = Path.GetDirectoryName(Path.GetFullPath(path));
    if (!string.IsNullOrWhiteSpace(dir))
        Directory.CreateDirectory(dir);
}

static string RedactConnectionString(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return "(empty)";

    var redacted = Regex.Replace(
        connectionString,
        "(Password|Pwd|User ID|UID|AccountKey)=([^;]+)",
        "$1=***",
        RegexOptions.IgnoreCase);

    return redacted;
}

public partial class Program;
