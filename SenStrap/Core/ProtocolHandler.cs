using Microsoft.Win32;

namespace SenStrap.Core;

/// <summary>
/// Registra SenStrap como el programa que abre los enlaces roblox:// y roblox-player:
/// (los botones de "Jugar" de roblox.com). Solo toca HKEY_CURRENT_USER, no hace falta ser administrador.
/// </summary>
public static class ProtocolHandler
{
    private static readonly string[] Protocols = ["roblox", "roblox-player"];

    /// <summary>Aquí se guarda el launcher que había antes (Froststrap, Bloxstrap, el oficial...).</summary>
    private const string BackupKey = @"Software\SenStrap\ProtocolBackup";

    public static void Register(string exePath)
    {
        foreach (var protocol in Protocols)
        {
            BackupCurrent(protocol, exePath);
            Write(protocol, $"\"{exePath}\" -player \"%1\"", $"\"{exePath}\",0");
        }
        Logger.Info("Enlaces de Roblox registrados para SenStrap.");
    }

    public static bool IsRegisteredTo(string exePath) => Protocols.All(p => PointsTo(p, exePath));

    /// <summary>
    /// Quita SenStrap de los enlaces y se los devuelve al launcher que había antes.
    /// Si ya no existe, al launcher oficial de Roblox; si tampoco está, borra los enlaces.
    /// </summary>
    public static void Unregister(string exePath)
    {
        string? official = FindOfficialRoblox();
        foreach (var protocol in Protocols)
        {
            if (!PointsTo(protocol, exePath)) continue;

            if (ReadBackup(protocol) is { } previous && File.Exists(ExeFromCommand(previous.Command)))
            {
                Write(protocol, previous.Command, previous.Icon);
                Logger.Info($"{protocol}: devuelto al launcher anterior ({ExeFromCommand(previous.Command)}).");
            }
            else if (official is not null)
            {
                Write(protocol, $"\"{official}\" %1", $"\"{official}\",0");
                Logger.Info($"{protocol}: devuelto al launcher oficial de Roblox.");
            }
            else
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{protocol}", throwOnMissingSubKey: false);
                Logger.Info($"{protocol}: eliminado (no hay otro launcher).");
            }
        }
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\SenStrap", throwOnMissingSubKey: false);
    }

    private static void BackupCurrent(string protocol, string ourExe)
    {
        using var cmd = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{protocol}\shell\open\command");
        if (cmd?.GetValue("") is not string command || command.Contains(ourExe, StringComparison.OrdinalIgnoreCase))
            return;

        using var icon = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{protocol}\DefaultIcon");
        using var backup = Registry.CurrentUser.CreateSubKey($@"{BackupKey}\{protocol}");
        backup.SetValue("Command", command);
        backup.SetValue("Icon", icon?.GetValue("") as string ?? "");
        Logger.Info($"{protocol}: guardado el launcher anterior ({ExeFromCommand(command)}).");
    }

    private static (string Command, string Icon)? ReadBackup(string protocol)
    {
        using var backup = Registry.CurrentUser.OpenSubKey($@"{BackupKey}\{protocol}");
        return backup?.GetValue("Command") is string command
            ? (command, backup.GetValue("Icon") as string ?? "")
            : null;
    }

    /// <summary>Saca la ruta del .exe de un comando como: "C:\...\froststrap.exe" --player "%1"</summary>
    private static string ExeFromCommand(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : "";
        }
        int space = command.IndexOf(' ');
        return space < 0 ? command : command[..space];
    }

    private static void Write(string protocol, string command, string icon)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}");
        key.SetValue("", "URL: Roblox Protocol");
        key.SetValue("URL Protocol", "");
        using (var iconKey = key.CreateSubKey("DefaultIcon"))
            iconKey.SetValue("", icon);
        using (var cmd = key.CreateSubKey(@"shell\open\command"))
            cmd.SetValue("", command);
    }

    private static bool PointsTo(string protocol, string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{protocol}\shell\open\command");
        return key?.GetValue("") is string command && command.Contains(exePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Busca el RobloxPlayerBeta.exe del instalador oficial de Roblox.</summary>
    private static string? FindOfficialRoblox()
    {
        string[] roots =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Roblox", "Versions"),
        ];

        return roots
            .Where(Directory.Exists)
            .SelectMany(r => Directory.EnumerateDirectories(r, "version-*"))
            .Select(d => Path.Combine(d, "RobloxPlayerBeta.exe"))
            .Where(File.Exists)
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .FirstOrDefault();
    }
}
