using System.IO.Compression;

namespace SenStrap.Core;

/// <summary>Saves SenStrap's settings, FastFlags, history and mods in a .zip, and restores them.</summary>
public static class Backup
{
    private const string ModsPrefix = "Modifications/";
    private static readonly string[] Files = ["Settings.json", "History.json"];

    public static void Export(string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var name in Files)
        {
            string path = Path.Combine(AppInfo.BaseDir, name);
            if (File.Exists(path)) zip.CreateEntryFromFile(path, name);
        }
        if (Directory.Exists(AppInfo.ModsDir))
        {
            foreach (var file in Directory.EnumerateFiles(AppInfo.ModsDir, "*", SearchOption.AllDirectories))
                zip.CreateEntryFromFile(file, ModsPrefix + Path.GetRelativePath(AppInfo.ModsDir, file).Replace(Path.DirectorySeparatorChar, '/'));
        }
        Logger.Info($"Backup saved: {zipPath}");
    }

    /// <summary>Replaces the current settings, history and mods with the backup's. Returns false if it isn't a SenStrap backup.</summary>
    public static bool Import(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.GetEntry("Settings.json") is null) return false;

        if (Directory.Exists(AppInfo.ModsDir)) Directory.Delete(AppInfo.ModsDir, recursive: true);
        string modsRoot = Path.GetFullPath(AppInfo.ModsDir) + Path.DirectorySeparatorChar;
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/')) continue;
            string target;
            if (Files.Contains(entry.FullName))
            {
                target = Path.Combine(AppInfo.BaseDir, entry.FullName);
            }
            else if (entry.FullName.StartsWith(ModsPrefix, StringComparison.Ordinal))
            {
                target = Path.GetFullPath(Path.Combine(AppInfo.ModsDir, entry.FullName[ModsPrefix.Length..]));
                if (!target.StartsWith(modsRoot, StringComparison.OrdinalIgnoreCase)) continue; // never outside the mods folder
            }
            else
            {
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
        Logger.Info($"Backup restored: {zipPath}");
        return true;
    }
}
