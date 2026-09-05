using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AveroNova.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF migrations against the development SQLite database.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "AveroNova.API"));
        if (!Directory.Exists(basePath))
            basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "AveroNova.API"));

        var databaseFolder = Path.Combine(basePath, "Database");
        Directory.CreateDirectory(databaseFolder);
        var dbPath = Path.Combine(databaseFolder, "AveroNovaDev.db");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        return new AppDbContext(optionsBuilder.Options);
    }
}
