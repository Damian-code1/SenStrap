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

    /// <summary>Paths (relative to Roblox's folder) that are deleted after the mods are copied, one per line.</summary>
    private const string RemovalsName = "_remove.txt";

    /// <summary>Roblox's head meshes. Deleting the folder makes heads disappear on your screen only.</summary>
    public const string HeadsFolder = @"content\avatar\heads";

    /// <summary>The classic body part meshes Roblox ships with (Avatar Editor).</summary>
    public static readonly (string Key, string Path)[] BodyParts =
    [
        ("torso", @"content\avatar\meshes\torso.mesh"),
        ("leftarm", @"content\avatar\meshes\leftarm.mesh"),
        ("rightarm", @"content\avatar\meshes\rightarm.mesh"),
        ("leftleg", @"content\avatar\meshes\leftleg.mesh"),
        ("rightleg", @"content\avatar\meshes\rightleg.mesh"),
    ];

    private static string RemovalsFile => Path.Combine(AppInfo.ModsDir, RemovalsName);

    public static List<string> Removals() =>
        File.Exists(RemovalsFile)
            ? File.ReadAllLines(RemovalsFile).Select(l => l.Trim()).Where(IsSafeRelative).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : [];

    public static bool IsRemoved(string relativePath) => Removals().Contains(relativePath, StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds or takes a path off the list of things deleted from Roblox on every install.</summary>
    public static void SetRemoved(string relativePath, bool removed)
    {
        var list = Removals();
        list.RemoveAll(p => p.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
        if (removed) list.Add(relativePath);
        if (list.Count == 0)
        {
            if (File.Exists(RemovalsFile)) File.Delete(RemovalsFile);
        }
        else
        {
            Directory.CreateDirectory(AppInfo.ModsDir);
            File.WriteAllLines(RemovalsFile, list);
        }
        Logger.Info($"Mod {(removed ? "removal added" : "removal taken off")}: {relativePath}");
    }

    /// <summary>Only paths inside Roblox's content folder, never outside it.</summary>
    private static bool IsSafeRelative(string path) =>
        path.Length > 0 && !Path.IsPathRooted(path) && !path.Contains("..")
        && path.StartsWith(@"content\", StringComparison.OrdinalIgnoreCase);

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
            .Where(f => !string.Equals(Path.GetRelativePath(AppInfo.ModsDir, f), RemovalsName, StringComparison.OrdinalIgnoreCase))
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
        foreach (var removal in Removals())
            sb.Append("remove|").Append(removal).Append('\n');
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
        if (File.Exists(RemovalsFile)) File.Delete(RemovalsFile);
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
        string root = Path.GetFullPath(versionDir).TrimEnd('\\') + "\\";
        foreach (var removal in Removals())
        {
            string target = Path.GetFullPath(Path.Combine(versionDir, removal));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            else if (File.Exists(target)) File.Delete(target);
            else continue;
            Logger.Info($"Mod removal applied: {removal}");
            count++;
        }
        if (count > 0)
            Logger.Info($"{count} archivos de mods aplicados.");
        return count;
    }
}
