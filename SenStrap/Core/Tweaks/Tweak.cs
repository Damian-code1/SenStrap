namespace SenStrap.Core.Tweaks;

public enum TweakCategory
{
    Roblox,
    Gaming,
    Input,
    Visual,
    Privacy,
    System,
    Power,
    Services,
    Explorer,
    Cleanup,
}

/// <summary>When a change takes effect, if not right away.</summary>
public enum TweakNote
{
    None,
    NextLaunch,
    Explorer,
    SignOut,
    Restart,
}

/// <summary>
/// A change to Windows or to how Roblox is started. Every tweak can be undone: before the first change,
/// the previous state is saved in Tweaks.json and Revert puts it back.
/// </summary>
public abstract class Tweak
{
    public required string Id { get; init; }
    public required TweakCategory Category { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? NameEs { get; init; }
    public string? DescriptionEs { get; init; }

    /// <summary>Changes machine-wide settings: needs administrator permission.</summary>
    public bool Admin { get; init; }

    /// <summary>Included in "Apply recommended". Safe for everyone.</summary>
    public bool Recommended { get; init; }

    /// <summary>Has a side effect worth reading about before turning it on.</summary>
    public bool Caution { get; init; }

    public TweakNote Note { get; init; }

    /// <summary>Tweaks in the same group can't be on at the same time (power plans).</summary>
    public string? ExclusiveGroup { get; init; }

    /// <summary>Runs once (cleanup) instead of staying on.</summary>
    public virtual bool IsAction => false;

    public virtual bool IsAvailable() => true;

    public abstract bool IsApplied(TweakContext context);

    public abstract void Apply(TweakContext context);

    public abstract void Revert(TweakContext context);

    private static bool Spanish => Lang.Current.StartsWith("es", StringComparison.OrdinalIgnoreCase);

    public string DisplayName => Spanish && NameEs is not null ? NameEs : Name;

    public string DisplayDescription => Spanish && DescriptionEs is not null ? DescriptionEs : Description;
}

/// <summary>What a tweak needs while it runs: the saved backups and, for cleanups, how much space was freed.</summary>
public sealed class TweakContext(TweakStore store, string? userSid = null)
{
    public TweakStore Store { get; } = store;

    /// <summary>
    /// The Windows account the change is for. The elevated copy can run as another administrator account,
    /// so the normal copy sends its own SID along.
    /// </summary>
    public string UserSid { get; } = userSid ?? System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? "";

    public long FreedBytes { get; set; }
}

/// <summary>A registry value as it was before SenStrap changed it.</summary>
public sealed class RegBackup
{
    public Hive Hive { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Existed { get; set; }
    public string? Kind { get; set; }
    public string? Data { get; set; }
}

/// <summary>Everything needed to undo one tweak.</summary>
public sealed class TweakBackup
{
    public List<RegBackup> Values { get; set; } = new();

    /// <summary>Keys that didn't exist before (deepest first), deleted again on undo if they're empty.</summary>
    public List<string> CreatedKeys { get; set; } = new();

    /// <summary>Other saved state (previous power plan, SPI values...).</summary>
    public Dictionary<string, string> Extra { get; set; } = new();
}

/// <summary>Tweaks.json: the Roblox launch options that are on and the backups of every applied tweak.</summary>
public sealed class TweakStore
{
    public HashSet<string> LaunchOptions { get; set; } = new();

    public Dictionary<string, TweakBackup> Backups { get; set; } = new();

    public static string FilePath => Path.Combine(AppInfo.BaseDir, "Tweaks.json");

    public static TweakStore Load()
    {
        if (File.Exists(FilePath))
            return JsonStore.Load<TweakStore>(FilePath);

        // Before 1.3 the compatibility options lived in the Launcher settings
        var store = new TweakStore();
        var settings = Settings.Load();
        if (settings.DisableFullscreenOptimizations) store.LaunchOptions.Add(TweakIds.RobloxFso);
        if (settings.HighDpiOverride) store.LaunchOptions.Add(TweakIds.RobloxDpi);
        return store;
    }

    public void Save() => JsonStore.Save(FilePath, this);
}
