using TextLayer.Application.Abstractions;

namespace TextLayer.Infrastructure.Logging;

public sealed class FileLogService : ILogService
{
    private const long MaxBytes = 1024 * 1024;
    private const int MaxArchives = 5;
    private readonly object syncRoot = new();

    public FileLogService()
    {
        Directory.CreateDirectory(AppDataPaths.LogsDirectory);
    }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null)
        => Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        lock (syncRoot)
        {
            RotateIfNeeded();
            File.AppendAllText(
                AppDataPaths.MainLogFilePath,
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}");
        }
    }

    private static void RotateIfNeeded()
    {
        var logPath = AppDataPaths.MainLogFilePath;
        if (!File.Exists(logPath))
        {
            return;
        }

        var fileInfo = new FileInfo(logPath);
        if (fileInfo.Length < MaxBytes)
        {
            return;
        }

        for (var index = MaxArchives - 1; index >= 1; index--)
        {
            var current = $"{logPath}.{index}";
            var next = $"{logPath}.{index + 1}";
            if (!File.Exists(current))
            {
                continue;
            }

            if (File.Exists(next))
            {
                File.Delete(next);
            }

            File.Move(current, next);
        }

        var firstArchive = $"{logPath}.1";
        if (File.Exists(firstArchive))
        {
            File.Delete(firstArchive);
        }

        File.Move(logPath, firstArchive);
    }
}
