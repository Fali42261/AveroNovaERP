using AveroNova.API.Filters;
using AveroNova.Infrastructure;
using AveroNova.Infrastructure.Persistence;
using AveroNova.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<RequireActiveSubscriptionFilter>();
});
builder.Services.AddScoped<RequireActiveSubscriptionFilter>();

var dbPath = DatabasePath.GetDatabasePath(
    builder.Environment.ContentRootPath);

Console.WriteLine($"Database Path: {dbPath}");

builder.Services.AddInfrastructure(dbPath);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migrate failed, EnsureCreated: {ex.Message}");
        await db.Database.EnsureCreatedAsync();
    }

    await SqliteSubscriptionSchema.EnsureAsync(db);
    await SqliteUserRoleSchema.EnsureAsync(db);
    await SubscriptionCatalogSeeder.SeedAsync(db);
}

app.UseAuthorization();
app.MapControllers();
app.Run();
