using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SenStrap.Core.Tweaks;

/// <summary>A registry value and what the tweak sets it to.</summary>
public sealed record RegValue(Hive Hive, string Key, string Name, object On, RegistryValueKind Kind = RegistryValueKind.DWord)
{
    /// <summary>What Windows assumes when the value doesn't exist (only used to show the current state).</summary>
    public object? AbsentMeans { get; init; }

    /// <summary>Written on undo when there's no backup. Null deletes the value.</summary>
    public object? Default { get; init; }
}

internal static class RegBackups
{
    private static readonly RegistryValueKind[] Restorable =
    [
        RegistryValueKind.DWord, RegistryValueKind.QWord, RegistryValueKind.String,
        RegistryValueKind.ExpandString, RegistryValueKind.Binary, RegistryValueKind.MultiString,
    ];

    public static bool Same(object? actual, object expected, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => actual is int i && i == Convert.ToInt32(expected, CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => actual is long l && l == Convert.ToInt64(expected, CultureInfo.InvariantCulture),
        _ => actual is not null && string.Equals(actual.ToString(), expected.ToString(), StringComparison.Ordinal),
    };

    public static RegBackup Capture(Hive hive, string key, string name)
    {
        var kind = Sys.GetKind(hive, key, name);
        var data = Sys.GetValue(hive, key, name);
        return new RegBackup
        {
            Hive = hive,
            Key = key,
            Name = name,
            Existed = data is not null,
            Kind = kind?.ToString(),
            Data = data switch
            {
                null => null,
                byte[] bytes => Convert.ToHexString(bytes),
                string[] lines => string.Join('\0', lines),
                _ => Convert.ToString(data, CultureInfo.InvariantCulture),
            },
        };
    }

    /// <summary>Puts a value back as it was. Only values the tweak declares are ever touched.</summary>
    public static void Restore(RegValue value, RegBackup? saved)
    {
        if (saved is null)
        {
            if (value.Default is null) Sys.DeleteValue(value.Hive, value.Key, value.Name);
            else Sys.SetValue(value.Hive, value.Key, value.Name, value.Default, value.Kind);
            return;
        }
        Restore(value.Hive, value.Key, value.Name, saved);
    }

    public static void Restore(Hive hive, string key, string name, RegBackup saved)
    {
        if (!saved.Existed)
        {
            Sys.DeleteValue(hive, key, name);
            return;
        }
        if (saved.Data is null || !Enum.TryParse<RegistryValueKind>(saved.Kind, out var kind) || !Restorable.Contains(kind))
            return;

        object data = kind switch
        {
            RegistryValueKind.DWord => int.Parse(saved.Data, CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => long.Parse(saved.Data, CultureInfo.InvariantCulture),
            RegistryValueKind.Binary => Convert.FromHexString(saved.Data),
            RegistryValueKind.MultiString => saved.Data.Split('\0'),
            _ => saved.Data,
        };
        Sys.SetValue(hive, key, name, data, kind);
    }

    public static RegBackup? Find(TweakBackup? backup, Hive hive, string key, string name) =>
        backup?.Values.FirstOrDefault(b => b.Hive == hive
            && b.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
            && b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Records the keys that don't exist yet so undo can remove them again.</summary>
    public static void RecordMissingKeys(TweakBackup backup, IEnumerable<(Hive Hive, string Key)> keys)
    {
        foreach (var (hive, key) in keys.Distinct())
        {
            foreach (var missing in Sys.MissingKeys(hive, key))
            {
                string entry = $@"{hive}\{missing}";
                if (!backup.CreatedKeys.Contains(entry, StringComparer.OrdinalIgnoreCase))
                    backup.CreatedKeys.Add(entry);
            }
        }
    }

    /// <summary>Deletes the keys the tweak created, if they're empty. Only keys on the tweak's own paths.</summary>
    public static void DeleteCreatedKeys(TweakBackup backup, IEnumerable<(Hive Hive, string Key)> declared)
    {
        var allowed = declared.ToList();
        foreach (var entry in backup.CreatedKeys.OrderByDescending(k => k.Count(c => c == '\\')))
        {
            int cut = entry.IndexOf('\\');
            if (cut <= 0 || !Enum.TryParse<Hive>(entry[..cut], out var hive)) continue;
            string key = entry[(cut + 1)..];
            bool onPath = allowed.Any(d => d.Hive == hive
                && (d.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                    || d.Key.StartsWith(key + "\\", StringComparison.OrdinalIgnoreCase)));
            if (onPath) Sys.DeleteKeyIfEmpty(hive, key);
        }
    }
}

/// <summary>Sets one or more registry values.</summary>
public sealed class RegistryTweak : Tweak
{
    public required RegValue[] Values { get; init; }

    public Func<bool>? Available { get; init; }

    /// <summary>Applies the change to the running session right away (true = applied, false = undone). Not run in tests.</summary>
    public Action<bool>? AfterChange { get; init; }

    private IEnumerable<(Hive, string)> Keys => Values.Select(v => (v.Hive, v.Key));

    public override bool IsAvailable() => Available?.Invoke() ?? true;

    public override bool IsApplied(TweakContext context) =>
        Values.All(v => RegBackups.Same(Sys.GetValue(v.Hive, v.Key, v.Name) ?? v.AbsentMeans, v.On, v.Kind));

    public override void Apply(TweakContext context)
    {
        if (!context.Store.Backups.ContainsKey(Id))
        {
            var backup = new TweakBackup();
            foreach (var v in Values)
                backup.Values.Add(RegBackups.Capture(v.Hive, v.Key, v.Name));
            RegBackups.RecordMissingKeys(backup, Keys);
            context.Store.Backups[Id] = backup;
        }

        foreach (var v in Values)
            Sys.SetValue(v.Hive, v.Key, v.Name, v.On, v.Kind);
        if (!Sys.Sandboxed) AfterChange?.Invoke(true);
    }

    public override void Revert(TweakContext context)
    {
        context.Store.Backups.TryGetValue(Id, out var backup);
        foreach (var v in Values)
            RegBackups.Restore(v, RegBackups.Find(backup, v.Hive, v.Key, v.Name));
        if (backup is not null)
            RegBackups.DeleteCreatedKeys(backup, Keys);
        context.Store.Backups.Remove(Id);
        if (!Sys.Sandboxed) AfterChange?.Invoke(false);
    }
}

/// <summary>Clears (or sets) one bit of a number stored as text, keeping the rest (accessibility shortcuts).</summary>
public sealed class FlagBitTweak : Tweak
{
    public required string Key { get; init; }
    public string ValueName { get; init; } = "Flags";
    public required int Bit { get; init; }
    public required int DefaultFlags { get; init; }
    public Action<bool>? AfterChange { get; init; }

    private int Current() =>
        int.TryParse(Sys.GetValue(Hive.HKCU, Key, ValueName)?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v : DefaultFlags;

    public override bool IsApplied(TweakContext context) => (Current() & Bit) == 0;

    public override void Apply(TweakContext context)
    {
        if (!context.Store.Backups.ContainsKey(Id))
        {
            var backup = new TweakBackup();
            backup.Values.Add(RegBackups.Capture(Hive.HKCU, Key, ValueName));
            context.Store.Backups[Id] = backup;
        }
        Sys.SetValue(Hive.HKCU, Key, ValueName, (Current() & ~Bit).ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
        if (!Sys.Sandboxed) AfterChange?.Invoke(true);
    }

    public override void Revert(TweakContext context)
    {
        var saved = context.Store.Backups.TryGetValue(Id, out var backup) ? RegBackups.Find(backup, Hive.HKCU, Key, ValueName) : null;
        if (saved is not null)
            RegBackups.Restore(Hive.HKCU, Key, ValueName, saved);
        else
            Sys.SetValue(Hive.HKCU, Key, ValueName, (Current() | Bit).ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
        context.Store.Backups.Remove(Id);
        if (!Sys.Sandboxed) AfterChange?.Invoke(false);
    }
}

/// <summary>One "Name=value;" entry inside DirectXUserGlobalSettings (Graphics settings in Windows).</summary>
public sealed class DxTokenTweak : Tweak
{
    public const string Key = @"Software\Microsoft\DirectX\UserGpuPreferences";
    public const string ValueName = "DirectXUserGlobalSettings";

    public required string Token { get; init; }
    public string On { get; init; } = "1";

    private static List<KeyValuePair<string, string>> Parse(string? text) =>
        (text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part =>
            {
                int eq = part.IndexOf('=');
                return eq < 0 ? new KeyValuePair<string, string>(part, "") : new(part[..eq], part[(eq + 1)..]);
            })
            .ToList();

    private string? Get() =>
        Parse(Sys.GetValue(Hive.HKCU, Key, ValueName) as string)
            .Where(p => p.Key.Equals(Token, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Value)
            .FirstOrDefault();

    private void Set(string? value)
    {
        var entries = Parse(Sys.GetValue(Hive.HKCU, Key, ValueName) as string);
        entries.RemoveAll(p => p.Key.Equals(Token, StringComparison.OrdinalIgnoreCase));
        if (value is not null) entries.Add(new(Token, value));
        if (entries.Count == 0)
            Sys.DeleteValue(Hive.HKCU, Key, ValueName);
        else
            Sys.SetValue(Hive.HKCU, Key, ValueName, string.Concat(entries.Select(p => $"{p.Key}={p.Value};")), RegistryValueKind.String);
    }

    public override bool IsApplied(TweakContext context) => Get() == On;

    public override void Apply(TweakContext context)
    {
        if (!context.Store.Backups.ContainsKey(Id))
        {
            var previous = Get();
            var backup = new TweakBackup();
            if (previous is not null) backup.Extra["previous"] = previous;
            context.Store.Backups[Id] = backup;
        }
        Set(On);
    }

    public override void Revert(TweakContext context)
    {
        string? previous = context.Store.Backups.TryGetValue(Id, out var backup) ? backup.Extra.GetValueOrDefault("previous") : null;
        Set(previous);
        context.Store.Backups.Remove(Id);
    }
}

/// <summary>On/off settings that Windows reads through SystemParametersInfo (menu fades, smooth scrolling...).</summary>
public sealed class SpiTweak : Tweak
{
    public required (uint Get, uint Set)[] Settings { get; init; }
    public bool On { get; init; }

    private static bool Read(uint get) =>
        Sys.Sandboxed ? Sys.GetValue(Hive.HKCU, "SPI", $"{get:X4}") is not int v || v != 0 : Native.GetBool(get);

    private static void Write((uint Get, uint Set) setting, bool value)
    {
        if (Sys.Sandboxed)
            Sys.SetValue(Hive.HKCU, "SPI", $"{setting.Get:X4}", value ? 1 : 0, RegistryValueKind.DWord);
        else
            Native.SetBool(setting.Set, value);
    }

    public override bool IsApplied(TweakContext context) => Settings.All(s => Read(s.Get) == On);

    public override void Apply(TweakContext context)
    {
        if (!context.Store.Backups.ContainsKey(Id))
        {
            var backup = new TweakBackup();
            foreach (var s in Settings)
                backup.Extra[$"{s.Get:X4}"] = Read(s.Get) ? "1" : "0";
            context.Store.Backups[Id] = backup;
        }
        foreach (var s in Settings) Write(s, On);
    }

    public override void Revert(TweakContext context)
    {
        context.Store.Backups.TryGetValue(Id, out var backup);
        foreach (var s in Settings)
        {
            bool value = backup?.Extra.TryGetValue($"{s.Get:X4}", out var saved) == true ? saved == "1" : !On;
            Write(s, value);
        }
        context.Store.Backups.Remove(Id);
    }
}

/// <summary>Windows power plans, through powercfg.</summary>
internal static partial class PowerPlans
{
    public const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string Ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    // In tests the plans are simulated in the sandbox key
    private const string FakeKey = "Power";

    public static string Active()
    {
        if (Sys.Sandboxed) return Sys.GetValue(Hive.HKCU, FakeKey, "Active") as string ?? Balanced;
        var match = GuidRegex().Match(Sys.Run("powercfg", "/getactivescheme").Output);
        return match.Success ? match.Value.ToLowerInvariant() : "";
    }

    public static List<string> List()
    {
        if (Sys.Sandboxed)
            return (Sys.GetValue(Hive.HKCU, FakeKey, "Schemes") as string[] ?? [Balanced, HighPerformance]).ToList();
        return GuidRegex().Matches(Sys.Run("powercfg", "/list").Output).Select(m => m.Value.ToLowerInvariant()).ToList();
    }

    public static void SetActive(string guid)
    {
        if (Sys.Sandboxed)
        {
            Sys.SetValue(Hive.HKCU, FakeKey, "Active", guid, RegistryValueKind.String);
            return;
        }
        var (code, output) = Sys.Run("powercfg", $"/setactive {guid}");
        if (code != 0) throw new Exception(output.Trim());
    }

    /// <summary>Copies a plan (needed for hidden ones like Ultimate Performance) and returns the new plan's id.</summary>
    public static string Duplicate(string guid)
    {
        if (Sys.Sandboxed)
        {
            string fake = Guid.NewGuid().ToString();
            Sys.SetValue(Hive.HKCU, FakeKey, "Schemes", List().Append(fake).ToArray(), RegistryValueKind.MultiString);
            return fake;
        }
        var (code, output) = Sys.Run("powercfg", $"/duplicatescheme {guid}");
        var match = GuidRegex().Match(output);
        if (code != 0 || !match.Success) throw new Exception(output.Trim());
        return match.Value.ToLowerInvariant();
    }

    public static void Delete(string guid)
    {
        if (Sys.Sandboxed)
        {
            Sys.SetValue(Hive.HKCU, FakeKey, "Schemes", List().Where(g => g != guid).ToArray(), RegistryValueKind.MultiString);
            return;
        }
        Sys.Run("powercfg", $"/delete {guid}");
    }
}

/// <summary>
/// Switches to a power plan, creating it if Windows hides it. Undo goes back to the plan that was active
/// before SenStrap first changed it (shared by all power plan tweaks, so the order of undoing doesn't matter).
/// </summary>
public sealed class PowerPlanTweak : Tweak
{
    /// <summary>Backup entry shared by the power plan tweaks.</summary>
    public const string SharedKey = "shared.power";

    public required string Scheme { get; init; }

    private static bool IsGuid(string? value) => Guid.TryParse(value, out _);

    private string Target(TweakContext context) =>
        context.Store.Backups.TryGetValue(Id, out var b) && b.Extra.TryGetValue("created", out var created) && IsGuid(created)
            ? created : Scheme;

    public override bool IsApplied(TweakContext context) => PowerPlans.Active() == Target(context);

    public override void Apply(TweakContext context)
    {
        var backups = context.Store.Backups;
        if (!backups.ContainsKey(SharedKey))
            backups[SharedKey] = new TweakBackup { Extra = { ["previous"] = PowerPlans.Active() } };
        if (!backups.TryGetValue(Id, out var backup))
        {
            backup = new TweakBackup();
            backups[Id] = backup;
        }

        var plans = PowerPlans.List();
        string target;
        if (backup.Extra.TryGetValue("created", out var created) && IsGuid(created) && plans.Contains(created))
        {
            target = created;
        }
        else if (plans.Contains(Scheme))
        {
            target = Scheme;
        }
        else
        {
            target = PowerPlans.Duplicate(Scheme);
            backup.Extra["created"] = target;
        }
        PowerPlans.SetActive(target);
    }

    public override void Revert(TweakContext context)
    {
        var backups = context.Store.Backups;
        if (!backups.TryGetValue(Id, out var backup)) return; // SenStrap didn't change the plan
        string target = Target(context);

        // Only switch back if this plan is still the active one
        if (PowerPlans.Active() == target)
        {
            string? previous = backups.TryGetValue(SharedKey, out var shared) ? shared.Extra.GetValueOrDefault("previous") : null;
            if (!IsGuid(previous) || previous == target || !PowerPlans.List().Contains(previous!))
                previous = PowerPlans.Balanced;
            PowerPlans.SetActive(previous!);
        }
        if (backup.Extra.TryGetValue("created", out var created) && IsGuid(created) && PowerPlans.Active() != created)
            PowerPlans.Delete(created);
        backups.Remove(Id);

        bool othersLeft = TweakCatalog.All.OfType<PowerPlanTweak>().Any(t => backups.ContainsKey(t.Id));
        if (!othersLeft) backups.Remove(SharedKey);
    }
}

/// <summary>A setting changed with a Windows command (for example powercfg /hibernate).</summary>
public sealed class CommandTweak : Tweak
{
    public required Func<bool> Check { get; init; }
    public required Action TurnOn { get; init; }
    public required Action TurnOff { get; init; }

    public override bool IsApplied(TweakContext context) => Check();

    public override void Apply(TweakContext context)
    {
        if (!context.Store.Backups.ContainsKey(Id))
            context.Store.Backups[Id] = new TweakBackup { Extra = { ["was"] = Check() ? "1" : "0" } };
        TurnOn();
    }

    public override void Revert(TweakContext context)
    {
        bool wasOn = context.Store.Backups.TryGetValue(Id, out var backup) && backup.Extra.GetValueOrDefault("was") == "1";
        if (!wasOn) TurnOff();
        context.Store.Backups.Remove(Id);
    }
}

/// <summary>Something SenStrap does every time it opens Roblox (GPU, priority...).</summary>
public sealed class LaunchOptionTweak : Tweak
{
    /// <summary>Updates the current Roblox copy right away when the option changes.</summary>
    public Action<TweakContext>? OnChange { get; init; }

    public override bool IsApplied(TweakContext context) => context.Store.LaunchOptions.Contains(Id);

    public override void Apply(TweakContext context)
    {
        context.Store.LaunchOptions.Add(Id);
        OnChange?.Invoke(context);
    }

    public override void Revert(TweakContext context)
    {
        context.Store.LaunchOptions.Remove(Id);
        OnChange?.Invoke(context);
    }
}

/// <summary>Deletes the contents of some folders (temporary files, caches). Runs once.</summary>
public sealed class CleanupTweak : Tweak
{
    public Func<IEnumerable<string>> Folders { get; init; } = () => [];

    /// <summary>Only files older than this are deleted (0 = all).</summary>
    public double MinAgeHours { get; init; }

    public bool NeedsRobloxClosed { get; init; }

    /// <summary>Extra work that isn't deleting files (flushing DNS). Not run in tests.</summary>
    public Action? Extra { get; init; }

    public override bool IsAction => true;

    public override bool IsApplied(TweakContext context) => false;

    public override void Apply(TweakContext context)
    {
        if (NeedsRobloxClosed && !Sys.Sandboxed && RobloxInstaller.IsRobloxRunning())
            throw new Exception(Lang.T("opt.closeRoblox"));

        foreach (var folder in Folders())
            context.FreedBytes += Empty(Sys.Folder(folder), MinAgeHours);
        if (!Sys.Sandboxed) Extra?.Invoke();
    }

    public override void Revert(TweakContext context)
    {
    }

    /// <summary>Deletes what it can inside a folder (files in use are skipped) and returns the bytes freed.</summary>
    internal static long Empty(string folder, double minAgeHours)
    {
        if (!Directory.Exists(folder)) return 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint, // never follow links out of the folder
        };
        var cutoff = DateTime.UtcNow.AddHours(-minAgeHours);
        long freed = 0;

        foreach (var path in Directory.EnumerateFiles(folder, "*", options))
        {
            try
            {
                var file = new FileInfo(path);
                if (minAgeHours > 0 && file.LastWriteTimeUtc > cutoff) continue;
                long size = file.Length;
                if (file.IsReadOnly) file.IsReadOnly = false;
                file.Delete();
                freed += size;
            }
            catch
            {
                // In use or protected: leave it
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(folder, "*", options).OrderByDescending(d => d.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch
            {
            }
        }
        return freed;
    }
}
