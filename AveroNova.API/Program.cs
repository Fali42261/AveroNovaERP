using AveroNova.Shared.Helpers;
using AveroNova.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

var dbPath = DatabasePath.GetDatabasePath(
    builder.Environment.ContentRootPath);

Console.WriteLine($"Database Path: {dbPath}");

builder.Services.AddInfrastructure(dbPath);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseAuthorization();

app.MapControllers();

app.Run();