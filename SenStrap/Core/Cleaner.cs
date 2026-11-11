namespace SenStrap.Core;

/// <summary>Limpieza automática: borra descargas y registros antiguos al abrir Roblox.</summary>
public static class Cleaner
{
    public static void Run(Settings settings)
    {
        if (settings.CleanerDays <= 0) return;
        var limit = DateTime.UtcNow.AddDays(-settings.CleanerDays);
        int deleted = 0;

        if (settings.CleanDownloads)
            deleted += Clean(AppInfo.DownloadsDir, "*", limit);
        if (settings.CleanRobloxLogs)
            deleted += Clean(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs"), "*.log", limit);
        if (settings.CleanAppLogs)
            deleted += Clean(AppInfo.LogsDir, "*.log", limit);

        if (deleted > 0)
            Logger.Info($"Limpieza automática: {deleted} archivos antiguos borrados.");
    }

    private static int Clean(string dir, string pattern, DateTime limit)
    {
        if (!Directory.Exists(dir)) return 0;
        int count = 0;
        foreach (var file in Directory.EnumerateFiles(dir, pattern))
        {
            try
            {
                if (string.Equals(file, Logger.FilePath, StringComparison.OrdinalIgnoreCase)) continue;
                if (File.GetLastWriteTimeUtc(file) >= limit) continue;
                File.Delete(file);
                count++;
            }
            catch
            {
                // En uso (por ejemplo, el log de una partida abierta): se deja
            }
        }
        return count;
    }
}
