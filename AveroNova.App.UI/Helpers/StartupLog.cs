namespace AveroNova.App.UI.Helpers;

internal static class StartupLog
{
    private static readonly string PathName = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AveroNova.startup.log");

    public static void Write(string message)
    {
        try
        {
            File.AppendAllText(PathName, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // ignore
        }
    }
}
