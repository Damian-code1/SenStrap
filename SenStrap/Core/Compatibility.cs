using Microsoft.Win32;

namespace SenStrap.Core;

/// <summary>
/// Opciones de compatibilidad de Windows para RobloxPlayerBeta.exe (como en Propiedades > Compatibilidad).
/// Solo escribe en HKEY_CURRENT_USER.
/// </summary>
public static class Compatibility
{
    private const string LayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

    public static void Apply(string robloxExe, Settings settings)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(LayersKey);

            // Quita las entradas de versiones antiguas de SenStrap
            foreach (var name in key.GetValueNames())
            {
                if (name.StartsWith(AppInfo.VersionsDir, StringComparison.OrdinalIgnoreCase)
                    && !name.Equals(robloxExe, StringComparison.OrdinalIgnoreCase))
                    key.DeleteValue(name, throwOnMissingValue: false);
            }

            var layers = new List<string>();
            if (settings.DisableFullscreenOptimizations) layers.Add("DISABLEDXMAXIMIZEDWINDOWEDMODE");
            if (settings.HighDpiOverride) layers.Add("HIGHDPIAWARE");

            if (layers.Count == 0)
                key.DeleteValue(robloxExe, throwOnMissingValue: false);
            else
                key.SetValue(robloxExe, "~ " + string.Join(' ', layers));
        }
        catch (Exception ex)
        {
            Logger.Warn($"No se pudieron aplicar las opciones de compatibilidad: {ex.Message}");
        }
    }
}
