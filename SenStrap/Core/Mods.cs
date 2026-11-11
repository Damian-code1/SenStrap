using System.Security.Cryptography;
using System.Text;

namespace SenStrap.Core;

/// <summary>
/// Mods: los archivos de %LOCALAPPDATA%\SenStrap\Modifications se copian encima de Roblox,
/// respetando la misma estructura de carpetas.
/// </summary>
public static class Mods
{
    private const string ReadmeName = "LEEME.txt";

    public static void EnsureFolder()
    {
        Directory.CreateDirectory(AppInfo.ModsDir);
        string readme = Path.Combine(AppInfo.ModsDir, ReadmeName);
        if (!File.Exists(readme))
        {
            File.WriteAllText(readme,
                "Pon aquí los archivos de Roblox que quieras cambiar, con la misma estructura de carpetas.\r\n" +
                "\r\n" +
                "Ejemplos:\r\n" +
                "  content\\sounds\\ouch.ogg                                    -> sonido al morir\r\n" +
                "  content\\textures\\Cursors\\KeyboardMouse\\ArrowCursor.png     -> cursor\r\n" +
                "  content\\textures\\Cursors\\KeyboardMouse\\ArrowFarCursor.png  -> cursor (de lejos)\r\n" +
                "\r\n" +
                "Los cambios se aplican la próxima vez que abras Roblox con SenStrap.\r\n" +
                "Para quitar un mod, borra el archivo de aquí.\r\n");
        }
    }

    private static IEnumerable<string> ModFiles()
    {
        if (!Directory.Exists(AppInfo.ModsDir))
            return [];

        return Directory.EnumerateFiles(AppInfo.ModsDir, "*", SearchOption.AllDirectories)
            .Where(f => !string.Equals(Path.GetRelativePath(AppInfo.ModsDir, f), ReadmeName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Huella de la carpeta de mods: si cambia, hay que reinstalar Roblox limpio y volver a aplicarlos.</summary>
    public static string ComputeHash()
    {
        var sb = new StringBuilder();
        foreach (var file in ModFiles())
        {
            var info = new FileInfo(file);
            sb.Append(Path.GetRelativePath(AppInfo.ModsDir, file)).Append('|')
              .Append(info.Length).Append('|')
              .Append(info.LastWriteTimeUtc.Ticks).Append('\n');
        }
        return sb.Length == 0 ? "" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    // Mods que se pueden poner desde la página de Mods
    public const string DeathSound = @"content\sounds\ouch.ogg";
    public const string ShiftLock = @"content\textures\MouseLockedCursor.png";
    public static readonly string[] Cursor =
    [
        @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png",
        @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png",
    ];

    /// <summary>Rutas (relativas) de todos los mods activos.</summary>
    public static List<string> List() =>
        ModFiles().Select(f => Path.GetRelativePath(AppInfo.ModsDir, f)).ToList();

    public static bool Has(string relativePath) => File.Exists(Path.Combine(AppInfo.ModsDir, relativePath));

    /// <summary>Copia un archivo del usuario como mod (p. ej. su .ogg como sonido al morir).</summary>
    public static void Set(string relativePath, string sourceFile)
    {
        string target = Path.Combine(AppInfo.ModsDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(sourceFile, target, overwrite: true);
        Logger.Info($"Mod puesto: {relativePath}");
    }

    public static void Remove(string relativePath)
    {
        string target = Path.Combine(AppInfo.ModsDir, relativePath);
        if (File.Exists(target)) File.Delete(target);
        DeleteEmptyFolders();
        Logger.Info($"Mod quitado: {relativePath}");
    }

    public static void RemoveAll()
    {
        foreach (var file in ModFiles().ToList())
            File.Delete(file);
        DeleteEmptyFolders();
        Logger.Info("Todos los mods quitados.");
    }

    private static void DeleteEmptyFolders()
    {
        if (!Directory.Exists(AppInfo.ModsDir)) return;
        foreach (var dir in Directory.EnumerateDirectories(AppInfo.ModsDir, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
    }

    public static int Apply(string versionDir)
    {
        int count = 0;
        foreach (var file in ModFiles())
        {
            string target = Path.Combine(versionDir, Path.GetRelativePath(AppInfo.ModsDir, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
            count++;
        }
        if (count > 0)
            Logger.Info($"{count} archivos de mods aplicados.");
        return count;
    }
}
