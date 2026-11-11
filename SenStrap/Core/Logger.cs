namespace SenStrap.Core;

/// <summary>Guarda un registro de lo que hace SenStrap en %LOCALAPPDATA%\SenStrap\Logs.</summary>
public static class Logger
{
    private static readonly object Lock = new();
    private const int LogsToKeep = 10;

    public static string? FilePath { get; private set; }

    public static void Init()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.LogsDir);
            FilePath = Path.Combine(AppInfo.LogsDir, $"SenStrap_{DateTime.Now:yyyyMMdd_HHmmss}_{Environment.ProcessId}.log");

            var old = new DirectoryInfo(AppInfo.LogsDir)
                .GetFiles("*.log")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(LogsToKeep);
            foreach (var f in old)
            {
                try { f.Delete(); } catch { /* da igual */ }
            }
        }
        catch
        {
            FilePath = null;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}";
        System.Diagnostics.Debug.WriteLine(line);
        if (FilePath is null) return;
        lock (Lock)
        {
            try { File.AppendAllText(FilePath, line + Environment.NewLine, System.Text.Encoding.UTF8); } catch { /* sin log */ }
        }
    }
}
