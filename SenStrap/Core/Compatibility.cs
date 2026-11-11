using Microsoft.Win32;
using SenStrap.Core.Tweaks;

namespace SenStrap.Core;

/// <summary>
/// Windows compatibility options for RobloxPlayerBeta.exe (like Properties > Compatibility).
/// Only writes to HKEY_CURRENT_USER.
/// </summary>
public static class Compatibility
{
    private const string LayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

    /// <param name="robloxExe">The current copy of Roblox, or null to only remove old entries.</param>
    public static void Apply(string? robloxExe, bool disableFullscreenOptimizations, bool highDpiOverride)
    {
        try
        {
            // Entries for SenStrap's older copies of Roblox
            string prefix = AppInfo.VersionsDir + Path.DirectorySeparatorChar;
            foreach (var name in Sys.GetValueNames(Hive.HKCU, LayersKey))
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && !name.Equals(robloxExe, StringComparison.OrdinalIgnoreCase))
                    Sys.DeleteValue(Hive.HKCU, LayersKey, name);
            }
            if (robloxExe is null) return;

            var layers = new List<string>();
            if (disableFullscreenOptimizations) layers.Add("DISABLEDXMAXIMIZEDWINDOWEDMODE");
            if (highDpiOverride) layers.Add("HIGHDPIAWARE");

            if (layers.Count == 0)
                Sys.DeleteValue(Hive.HKCU, LayersKey, robloxExe);
            else
                Sys.SetValue(Hive.HKCU, LayersKey, robloxExe, "~ " + string.Join(' ', layers), RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Couldn't apply the compatibility options: {ex.Message}");
        }
    }
}
