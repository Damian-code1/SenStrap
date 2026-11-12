using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SenStrap.Core;

/// <summary>Instala y desinstala SenStrap en el PC (sin permisos de administrador).</summary>
public static class SelfInstaller
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SenStrap";
    private const string PlayShortcutName = "Roblox (SenStrap).lnk";
    private const string MenuShortcutName = "SenStrap.lnk";

    private static string StartMenuDir => Environment.GetFolderPath(Environment.SpecialFolder.Programs);
    private static string DesktopDir => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static bool IsInstalled() =>
        File.Exists(AppInfo.InstalledExe) && ProtocolHandler.IsRegisteredTo(AppInfo.InstalledExe);

    public static void Install(bool desktopShortcut)
    {
        Directory.CreateDirectory(AppInfo.BaseDir);
        string exe = AppInfo.InstalledExe;

        // 1. Copia el .exe a %LOCALAPPDATA%\SenStrap
        if (!AppInfo.IsRunningFromInstallDir)
            File.Copy(AppInfo.CurrentExe, exe, overwrite: true);

        // 2. Los botones de "Jugar" de roblox.com abrirán SenStrap
        ProtocolHandler.Register(exe);

        // 3. Accesos directos: el del escritorio abre SenStrap (no Roblox directamente)
        CreateShortcut(Path.Combine(StartMenuDir, MenuShortcutName), exe, "", "SenStrap");
        CreateShortcut(Path.Combine(StartMenuDir, PlayShortcutName), exe, "-play", "Roblox (SenStrap)");
        if (desktopShortcut)
            CreateShortcut(Path.Combine(DesktopDir, MenuShortcutName), exe, "", "SenStrap");

        // 4. Aparece en Configuración > Aplicaciones para poder desinstalarlo
        using (var key = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            key.SetValue("DisplayName", AppInfo.Name);
            key.SetValue("DisplayIcon", $"\"{exe}\",0");
            key.SetValue("DisplayVersion", AppInfo.Version);
            key.SetValue("Publisher", AppInfo.Name);
            key.SetValue("InstallLocation", AppInfo.BaseDir);
            key.SetValue("UninstallString", $"\"{exe}\" -uninstall");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
        }

        Mods.EnsureFolder();
        Logger.Info($"SenStrap instalado en {AppInfo.BaseDir}");
    }

    /// <summary>
    /// Las versiones 1.0.x ponían en el escritorio "Roblox (SenStrap)", que abría Roblox directamente.
    /// Ahora el acceso directo del escritorio abre SenStrap.
    /// </summary>
    public static void MigrateShortcuts()
    {
        try
        {
            string oldDesktop = Path.Combine(DesktopDir, PlayShortcutName);
            if (!File.Exists(oldDesktop) || !File.Exists(AppInfo.InstalledExe)) return;
            File.Delete(oldDesktop);
            CreateShortcut(Path.Combine(DesktopDir, MenuShortcutName), AppInfo.InstalledExe, "", "SenStrap");
            Logger.Info("Acceso directo del escritorio actualizado: ahora abre SenStrap.");
        }
        catch (Exception ex)
        {
            Logger.Warn($"No se pudo actualizar el acceso directo del escritorio: {ex.Message}");
        }
    }

    /// <summary>
    /// Si se abre un SenStrap.exe distinto del instalado (por ejemplo, uno recién compilado),
    /// actualiza la copia instalada, que es la que abren los botones de Jugar de roblox.com.
    /// </summary>
    public static void UpdateInstalledCopy()
    {
        try
        {
            if (AppInfo.IsRunningFromInstallDir || !File.Exists(AppInfo.InstalledExe)) return;

            // "dotnet run" (modo desarrollo) no es un .exe autónomo: no se copia
            string currentDir = Path.GetDirectoryName(AppInfo.CurrentExe)!;
            if (File.Exists(Path.Combine(currentDir, "SenStrap.dll"))) return;

            var current = new FileInfo(AppInfo.CurrentExe);
            var installed = new FileInfo(AppInfo.InstalledExe);
            if (current.Length == installed.Length && current.LastWriteTimeUtc == installed.LastWriteTimeUtc) return;

            // An older SenStrap.exe (for example one left in Downloads) must not replace a newer installed copy
            var installedVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(installed.FullName);
            if (Version.TryParse(installedVersion.FileVersion, out var other) && other > Version.Parse(AppInfo.Version + ".0"))
            {
                Logger.Info($"The installed copy ({other}) is newer than this one ({AppInfo.Version}): not replaced.");
                return;
            }

            File.Copy(current.FullName, installed.FullName, overwrite: true);
            using (var key = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true))
                key?.SetValue("DisplayVersion", AppInfo.Version);
            Logger.Info($"Copia instalada actualizada a la versión {AppInfo.Version}.");
        }
        catch (Exception ex)
        {
            Logger.Warn($"No se pudo actualizar la copia instalada (¿está abierta?): {ex.Message}");
        }
    }

    public static void Uninstall()
    {
        ProtocolHandler.Unregister(AppInfo.InstalledExe);

        foreach (var shortcut in new[]
        {
            Path.Combine(StartMenuDir, MenuShortcutName),
            Path.Combine(StartMenuDir, PlayShortcutName),
            Path.Combine(DesktopDir, MenuShortcutName),
            Path.Combine(DesktopDir, PlayShortcutName),
        })
        {
            try { File.Delete(shortcut); } catch { /* no existe */ }
        }

        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
        Logger.Info("SenStrap desinstalado.");
    }

    /// <summary>
    /// Borra la carpeta de SenStrap unos segundos después de cerrarse
    /// (mientras está abierto no puede borrarse a sí mismo). Llamar justo antes de salir.
    /// </summary>
    public static void ScheduleFolderDeletion()
    {
        Process.Start(new ProcessStartInfo("cmd.exe",
            $"/c ping 127.0.0.1 -n 4 >nul & rmdir /s /q \"{AppInfo.BaseDir}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }

    /// <summary>
    /// A desktop shortcut that opens a game through SenStrap, with the game's icon
    /// (the PNG from Roblox is wrapped in an .ico file).
    /// </summary>
    public static async Task CreateGameShortcutAsync(GameEntry game)
    {
        string exe = IsInstalled() ? AppInfo.InstalledExe : AppInfo.CurrentExe;
        string? icon = null;
        if (game.IconUrl is { Length: > 0 } url)
        {
            try
            {
                byte[] png = await RobloxDeployment.Http.GetByteArrayAsync(url);
                if (png.Length > 8 && png[0] == 0x89 && png[1] == (byte)'P')
                {
                    string iconsDir = Path.Combine(AppInfo.BaseDir, "Icons");
                    Directory.CreateDirectory(iconsDir);
                    icon = Path.Combine(iconsDir, $"{game.PlaceId}.ico");
                    await File.WriteAllBytesAsync(icon, PngToIco(png));
                }
            }
            catch (Exception ex)
            {
                Logger.Info($"Game icon not available: {ex.Message}");
            }
        }

        string name = string.Concat(game.DisplayName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        if (name.Length == 0) name = $"Roblox {game.PlaceId}";
        string link = Path.Combine(DesktopDir, name + ".lnk");
        CreateShortcut(link, exe, $"-player \"roblox://experiences/start?placeId={game.PlaceId}\"", $"{game.DisplayName} (SenStrap)", icon);
        Logger.Info($"Game shortcut created: {link}");
    }

    /// <summary>An .ico file that holds a single PNG image (supported since Windows Vista).</summary>
    private static byte[] PngToIco(byte[] png)
    {
        int width = png[16] << 24 | png[17] << 16 | png[18] << 8 | png[19];
        int height = png[20] << 24 | png[21] << 16 | png[22] << 8 | png[23];
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((short)0);          // reserved
        writer.Write((short)1);          // type: icon
        writer.Write((short)1);          // one image
        writer.Write((byte)(width >= 256 ? 0 : width));
        writer.Write((byte)(height >= 256 ? 0 : height));
        writer.Write((byte)0);           // no palette
        writer.Write((byte)0);
        writer.Write((short)1);          // color planes
        writer.Write((short)32);         // bits per pixel
        writer.Write(png.Length);
        writer.Write(6 + 16);            // image data offset
        writer.Write(png);
        writer.Flush();
        return stream.ToArray();
    }

    private static void CreateShortcut(string path, string target, string arguments, string description, string? icon = null)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell no está disponible.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic link = shell.CreateShortcut(path);
                link.TargetPath = target;
                link.Arguments = arguments;
                link.WorkingDirectory = Path.GetDirectoryName(target);
                link.IconLocation = icon is not null ? icon + ",0" : target + ",0";
                link.Description = description;
                link.Save();
                Marshal.FinalReleaseComObject(link);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"No se pudo crear el acceso directo {path}: {ex.Message}");
        }
    }
}
