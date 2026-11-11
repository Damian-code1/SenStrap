using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenStrap.Core;

/// <summary>Background effect. Only snow exists now; Brasas and Estrellas are kept so old settings still load (they show snow).</summary>
public enum BackgroundEffect
{
    Nieve,
    Brasas,
    Estrellas,
    Ninguno,
}

/// <summary>Ajustes que elige el usuario.</summary>
public class Settings
{
    /// <summary>Código del idioma ("es", "en", "pt-BR"...). Vacío = todavía no se ha elegido.</summary>
    public string Language { get; set; } = "";

    // ---------- FastFlags ----------

    /// <summary>FastFlags tal cual se escriben en ClientAppSettings.json.</summary>
    public Dictionary<string, JsonElement> FastFlags { get; set; } = new();

    /// <summary>Si es false, no se aplica ningún FastFlag.</summary>
    public bool ManageFastFlags { get; set; } = true;

    /// <summary>FPS counter drawn by SenStrap over Roblox (not a FastFlag: Roblox's own one isn't allowed).</summary>
    public bool FpsCounter { get; set; }

    /// <summary>Perfiles de FastFlags guardados por el usuario.</summary>
    public Dictionary<string, Dictionary<string, JsonElement>> FlagProfiles { get; set; } = new();

    // ---------- Apariencia ----------

    public BackgroundEffect Effect { get; set; } = BackgroundEffect.Nieve;

    /// <summary>Snow is falling (any effect except "none").</summary>
    [JsonIgnore]
    public bool Snow => Effect != BackgroundEffect.Ninguno;

    public bool EffectInLauncher { get; set; } = true;

    /// <summary>Logo animation when SenStrap opens.</summary>
    public bool IntroAnimation { get; set; } = true;

    // ---------- Lanzador ----------

    /// <summary>Update SenStrap from GitHub when it opens.</summary>
    public bool AutoUpdate { get; set; } = true;

    public bool ConfirmLaunch { get; set; } = true;
    public bool CloseCrashHandler { get; set; }

    // Before 1.3 these lived here; now they're launch options in Tweaks.json (read once to move them over)
    public bool DisableFullscreenOptimizations { get; set; }
    public bool HighDpiOverride { get; set; }

    /// <summary>Si ya hay una versión jugable, abrir Roblox al momento y descargar la nueva en segundo plano.</summary>
    public bool BackgroundUpdates { get; set; }

    /// <summary>Días tras los que se borran los archivos antiguos (0 = nunca).</summary>
    public int CleanerDays { get; set; } = 7;
    public bool CleanDownloads { get; set; } = true;
    public bool CleanRobloxLogs { get; set; } = true;
    public bool CleanAppLogs { get; set; } = true;

    // ---------- Integraciones ----------

    public bool ActivityTracking { get; set; } = true;
    public bool ServerLocation { get; set; } = true;
    public bool PlaytimeCounter { get; set; } = true;
    public bool SaveGameHistory { get; set; } = true;

    /// <summary>Volver a entrar al mismo servidor si Roblox se cierra solo por un error/desconexión.</summary>
    public bool AutoRejoin { get; set; }

    public static Settings Load() => JsonStore.Load<Settings>(AppInfo.SettingsFile);

    public void Save() => JsonStore.Save(AppInfo.SettingsFile, this);
}

/// <summary>Estado interno: qué versión de Roblox hay instalada, con qué mods y cuánto se ha jugado.</summary>
public class AppState
{
    public string? VersionGuid { get; set; }

    /// <summary>Nombre de la carpeta (dentro de Versions) donde está instalada.</summary>
    public string? InstallDir { get; set; }

    public string? RobloxVersion { get; set; }
    public string? AppliedModsHash { get; set; }

    /// <summary>Tiempo total jugado (contador de tiempo de juego).</summary>
    public double TotalPlaySeconds { get; set; }

    /// <summary>Cuándo se hicieron las últimas reconexiones automáticas (para no entrar en bucle).</summary>
    public List<DateTime> RecentAutoRejoins { get; set; } = new();

    public static AppState Load() => JsonStore.Load<AppState>(AppInfo.StateFile);

    public void Save() => JsonStore.Save(AppInfo.StateFile, this);
}

public static class JsonStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static T Load<T>(string path) where T : new()
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T();
        }
        catch (Exception ex)
        {
            Logger.Warn($"No se pudo leer {path}: {ex.Message}");
        }
        return new T();
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
