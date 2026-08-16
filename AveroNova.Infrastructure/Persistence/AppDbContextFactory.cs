using AveroNova.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;


namespace AveroNova.Infrastructure.Persistence
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "AveroNova.API");

            var dataFolder = Path.Combine(basePath, "Data");

            Directory.CreateDirectory(dataFolder);

            var dbPath = Path.Combine(dataFolder, "AveroNova.db");

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
