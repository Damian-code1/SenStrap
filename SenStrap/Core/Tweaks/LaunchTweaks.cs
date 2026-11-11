using System.Diagnostics;
using Microsoft.Win32;

namespace SenStrap.Core.Tweaks;

/// <summary>The Roblox options from the Optimizations page, applied every time SenStrap opens Roblox.</summary>
public static class LaunchTweaks
{
    private const string GpuKey = @"Software\Microsoft\DirectX\UserGpuPreferences";

    /// <summary>Before starting Roblox: graphics card and compatibility options for this copy of Roblox.</summary>
    public static void BeforeLaunch(string robloxExe) => Apply(TweakStore.Load(), robloxExe);

    /// <summary>An option changed on the Optimizations page: update the installed copy right away.</summary>
    public static void Refresh(TweakContext context)
    {
        var state = AppState.Load();
        string? exe = string.IsNullOrEmpty(state.InstallDir)
            ? null
            : Path.Combine(AppInfo.VersionsDir, state.InstallDir, "RobloxPlayerBeta.exe");
        Apply(context.Store, exe);
    }

    private static void Apply(TweakStore store, string? robloxExe)
    {
        try
        {
            SetGpuPreference(store.LaunchOptions.Contains(TweakIds.RobloxGpu) ? robloxExe : null);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Couldn't set the GPU preference: {ex.Message}");
        }
        Compatibility.Apply(robloxExe,
            store.LaunchOptions.Contains(TweakIds.RobloxFso),
            store.LaunchOptions.Contains(TweakIds.RobloxDpi));
    }

    /// <summary>Right after Roblox starts: CPU priority and power throttling.</summary>
    public static void AfterLaunch(Process roblox)
    {
        var store = TweakStore.Load();
        if (store.LaunchOptions.Contains(TweakIds.RobloxPriority))
        {
            try
            {
                roblox.PriorityClass = ProcessPriorityClass.AboveNormal;
                Logger.Info("Roblox CPU priority: above normal.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Couldn't change Roblox's CPU priority: {ex.Message}");
            }
        }
        if (store.LaunchOptions.Contains(TweakIds.RobloxEcoQos))
        {
            try
            {
                Logger.Info(Native.DisablePowerThrottling(roblox)
                    ? "Power throttling turned off for Roblox."
                    : "Windows didn't accept turning off power throttling for Roblox.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Couldn't turn off power throttling for Roblox: {ex.Message}");
            }
        }
    }

    /// <summary>Points Windows' per-app graphics setting at this copy of Roblox and removes entries of old copies.</summary>
    internal static void SetGpuPreference(string? robloxExe)
    {
        string prefix = AppInfo.VersionsDir + Path.DirectorySeparatorChar;
        foreach (var name in Sys.GetValueNames(Hive.HKCU, GpuKey))
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && !name.Equals(robloxExe, StringComparison.OrdinalIgnoreCase))
                Sys.DeleteValue(Hive.HKCU, GpuKey, name);
        }
        if (robloxExe is not null)
            Sys.SetValue(Hive.HKCU, GpuKey, robloxExe, "GpuPreference=2;", RegistryValueKind.String);
    }
}
