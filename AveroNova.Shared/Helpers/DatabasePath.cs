namespace AveroNova.Shared.Helpers;

/// <summary>
/// Server development SQLite path helper (API only).
/// MAUI uses a separate local file (AveroNovaLocal.db) under AppDataDirectory.
/// </summary>
public static class DatabasePath
{
    public const string DevelopmentDatabaseFileName = "AveroNovaDev.db";

    public static string GetDatabasePath(string contentRootPath)
    {
        var folder = Path.Combine(contentRootPath, "Data");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, DevelopmentDatabaseFileName);
    }
}
