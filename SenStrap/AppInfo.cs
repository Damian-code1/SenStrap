namespace SenStrap;

/// <summary>Nombre, versión y carpetas que usa SenStrap.</summary>
public static class AppInfo
{
    public const string Name = "SenStrap";

    public static string Version =>
        typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>
    /// Carpeta principal: %LOCALAPPDATA%\SenStrap.
    /// Se puede cambiar con la variable de entorno SENSTRAP_HOME (útil para pruebas).
    /// </summary>
    public static readonly string BaseDir =
        Environment.GetEnvironmentVariable("SENSTRAP_HOME") is { Length: > 0 } custom
            ? Path.GetFullPath(custom)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Name);

    public static readonly string VersionsDir = Path.Combine(BaseDir, "Versions");
    public static readonly string DownloadsDir = Path.Combine(BaseDir, "Downloads");
    public static readonly string ModsDir = Path.Combine(BaseDir, "Modifications");
    public static readonly string LogsDir = Path.Combine(BaseDir, "Logs");
    public static readonly string SettingsFile = Path.Combine(BaseDir, "Settings.json");
    public static readonly string StateFile = Path.Combine(BaseDir, "State.json");
    public static readonly string InstalledExe = Path.Combine(BaseDir, Name + ".exe");

    public static string CurrentExe => Environment.ProcessPath ?? InstalledExe;

    public static bool IsRunningFromInstallDir =>
        string.Equals(Path.GetFullPath(CurrentExe), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);
}
