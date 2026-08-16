using AveroNova.App.Pages;
using AveroNova.App.ViewModels;
using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Application.Services;
using AveroNova.Infrastructure;
//using AveroNova.Infrastructure.Persistence;
using AveroNova.Infrastructure.Repositories;
//using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AveroNova.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            //Database connection file path
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "AveroNova.db");

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            //AddDbContext
            //builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

            // Infrastructure Registration
            //builder.Services.AddInfrastructure(dbPath);

            builder.Services.AddSingleton<App>();
            builder.Services.AddTransient<CompanyPage>();
            builder.Services.AddTransient<CompanyViewModel>();
            //builder.Services.AddTransient<ICompanyService, CompanyService>();
            //builder.Services.AddTransient<ICompanyRepository, CompanyRepository>();
            
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif
            var app = builder.Build();

            //using var scope = app.Services.CreateScope();
            //var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            //db.Database.Migrate();
            return app;
        }
    }
}
