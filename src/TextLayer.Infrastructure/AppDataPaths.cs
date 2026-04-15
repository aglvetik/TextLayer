namespace TextLayer.Infrastructure;

internal static class AppDataPaths
{
    public static string BaseDirectory => Path.Combine(
        GetLocalAppDataDirectory(),
        "TextLayer");

    public static string SettingsFilePath => Path.Combine(BaseDirectory, "settings.json");

    public static string LogsDirectory => Path.Combine(BaseDirectory, "Logs");

    public static string MainLogFilePath => Path.Combine(LogsDirectory, "textlayer.log");

    private static string GetLocalAppDataDirectory()
    {
        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return localAppData;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }
}
