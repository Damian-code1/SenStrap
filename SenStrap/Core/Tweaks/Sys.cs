using System.Diagnostics;
using Microsoft.Win32;

namespace SenStrap.Core.Tweaks;

public enum Hive { HKCU, HKLM }

/// <summary>
/// Registry, commands and folders for the tweaks.
/// When the SENSTRAP_TWEAK_SANDBOX variable is set (tests only), every registry key is redirected to
/// HKCU\&lt;sandbox&gt;\&lt;hive&gt;\..., commands are recorded instead of run and folders point inside SenStrap's
/// data folder, so tests never touch the real system.
/// </summary>
public static class Sys
{
    public static string? SandboxKey { get; } =
        Environment.GetEnvironmentVariable("SENSTRAP_TWEAK_SANDBOX") is { Length: > 0 } key ? key : null;

    public static bool Sandboxed => SandboxKey is not null;

    /// <summary>Commands that would have run (sandbox only).</summary>
    public static List<string> SandboxCommands { get; } = new();

    // ---------- Registry ----------

    private static string Map(Hive hive, string path) => Sandboxed ? $@"{SandboxKey}\{hive}\{path}" : path;

    private static RegistryKey? Open(Hive hive, string path, bool write)
    {
        if (Sandboxed || hive == Hive.HKCU)
            return Registry.CurrentUser.OpenSubKey(Map(hive, path), write);
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        return machine.OpenSubKey(path, write);
    }

    private static RegistryKey Create(Hive hive, string path)
    {
        if (Sandboxed || hive == Hive.HKCU)
            return Registry.CurrentUser.CreateSubKey(Map(hive, path), true);
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        return machine.CreateSubKey(path, true);
    }

    public static bool KeyExists(Hive hive, string path)
    {
        using var key = Open(hive, path, false);
        return key is not null;
    }

    /// <summary>The value, or null if it doesn't exist. Environment variables are not expanded.</summary>
    public static object? GetValue(Hive hive, string path, string name)
    {
        using var key = Open(hive, path, false);
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
    }

    public static RegistryValueKind? GetKind(Hive hive, string path, string name)
    {
        using var key = Open(hive, path, false);
        if (key is null || key.GetValue(name) is null) return null;
        return key.GetValueKind(name);
    }

    public static void SetValue(Hive hive, string path, string name, object value, RegistryValueKind kind)
    {
        using var key = Create(hive, path);
        key.SetValue(name, value, kind);
    }

    public static void DeleteValue(Hive hive, string path, string name)
    {
        using var key = Open(hive, path, true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public static string[] GetValueNames(Hive hive, string path)
    {
        using var key = Open(hive, path, false);
        return key?.GetValueNames() ?? [];
    }

    /// <summary>Deletes a key only if it has no values and no subkeys left.</summary>
    public static void DeleteKeyIfEmpty(Hive hive, string path)
    {
        using (var key = Open(hive, path, false))
        {
            if (key is null || key.ValueCount > 0 || key.SubKeyCount > 0) return;
        }
        int cut = path.LastIndexOf('\\');
        if (cut <= 0) return;
        using var parent = Open(hive, path[..cut], true);
        parent?.DeleteSubKey(path[(cut + 1)..], throwOnMissingSubKey: false);
    }

    /// <summary>The keys of the path that don't exist yet, deepest first.</summary>
    public static List<string> MissingKeys(Hive hive, string path)
    {
        var missing = new List<string>();
        string current = path;
        while (current.Length > 0 && !KeyExists(hive, current))
        {
            missing.Add(current);
            int cut = current.LastIndexOf('\\');
            if (cut <= 0) break;
            current = current[..cut];
        }
        return missing; // deepest first
    }

    // ---------- Commands ----------

    /// <summary>Runs a Windows tool (powercfg, sc...) hidden and returns its exit code and output.</summary>
    public static (int Code, string Output) Run(string file, string arguments)
    {
        if (Sandboxed)
        {
            SandboxCommands.Add($"{file} {arguments}");
            return (0, "");
        }

        var info = new ProcessStartInfo(file, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(info) ?? throw new Exception($"Couldn't start {file}");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            try { process.Kill(); } catch { }
            throw new TimeoutException($"{file} took too long");
        }
        return (process.ExitCode, output.Result + error.Result);
    }

    // ---------- Folders ----------

    /// <summary>A real folder, or a stand-in inside SenStrap's data folder when sandboxed.</summary>
    public static string Folder(string path)
    {
        if (!Sandboxed) return path;
        string safe = string.Concat(path.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        return Path.Combine(AppInfo.BaseDir, "sandbox-fs", safe);
    }
}
