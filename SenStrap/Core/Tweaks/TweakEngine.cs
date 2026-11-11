using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace SenStrap.Core.Tweaks;

public sealed record TweakOp(string Id, bool Apply);

public sealed record TweakResult(string Id, bool Ok, string? Error = null, long Freed = 0);

/// <summary>What the elevated copy of SenStrap receives: the changes to make and the backups it needs to undo them.</summary>
public sealed class ElevatedRequest
{
    public List<TweakOp> Ops { get; set; } = new();
    public Dictionary<string, TweakBackup> Backups { get; set; } = new();
}

/// <summary>What the elevated copy sends back: the result of each change and the backups after them.</summary>
public sealed class ElevatedResponse
{
    public List<TweakResult> Results { get; set; } = new();
    public Dictionary<string, TweakBackup> Backups { get; set; } = new();
}

/// <summary>Applies and undoes tweaks. Machine-wide ones run in a copy of SenStrap started as administrator.</summary>
public static class TweakEngine
{
    /// <summary>Error text for changes the user didn't give administrator permission for.</summary>
    public const string Cancelled = "cancelled";

    private const string RequestName = "request.json";
    private const string ResultName = "result.json";

    public static bool IsElevated { get; } = CheckElevated();

    private static bool CheckElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Runs the changes in this process.</summary>
    public static List<TweakResult> RunHere(IEnumerable<TweakOp> ops, TweakStore store)
    {
        var results = new List<TweakResult>();
        foreach (var op in ops)
        {
            var tweak = TweakCatalog.Find(op.Id);
            if (tweak is null)
            {
                results.Add(new TweakResult(op.Id, false, "Unknown tweak"));
                continue;
            }

            var context = new TweakContext(store);
            try
            {
                if (op.Apply) tweak.Apply(context);
                else tweak.Revert(context);
                results.Add(new TweakResult(op.Id, true, null, context.FreedBytes));
                Logger.Info($"Tweak {(tweak.IsAction ? "run" : op.Apply ? "applied" : "undone")}: {op.Id}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Tweak {op.Id} failed: {ex.Message}");
                results.Add(new TweakResult(op.Id, false, ex.Message));
            }
        }
        return results;
    }

    /// <summary>
    /// Runs the changes: the ones for this user here, the machine-wide ones in an elevated copy of SenStrap
    /// (a single administrator prompt for all of them).
    /// </summary>
    public static async Task<List<TweakResult>> ExecuteAsync(IReadOnlyList<TweakOp> ops)
    {
        var store = TweakStore.Load();
        var userOps = ops.Where(o => TweakCatalog.Find(o.Id) is { Admin: false }).ToList();
        var adminOps = ops.Where(o => TweakCatalog.Find(o.Id) is { Admin: true }).ToList();

        var results = await Task.Run(() => RunHere(userOps, store));
        store.Save();
        if (adminOps.Count == 0) return results;

        if (IsElevated || Sys.Sandboxed)
        {
            results.AddRange(await Task.Run(() => RunHere(adminOps, store)));
        }
        else
        {
            var response = await RunElevatedAsync(adminOps, store);
            if (response is null)
            {
                results.AddRange(adminOps.Select(o => new TweakResult(o.Id, false, Cancelled)));
            }
            else
            {
                results.AddRange(response.Results);
                foreach (var key in adminOps.Select(o => o.Id).Concat(SharedKeys(store.Backups)).Concat(SharedKeys(response.Backups)).Distinct().ToList())
                {
                    if (response.Backups.TryGetValue(key, out var backup)) store.Backups[key] = backup;
                    else store.Backups.Remove(key);
                }
            }
        }
        store.Save();
        return results;
    }

    /// <returns>null if the user said no to the administrator prompt.</returns>
    private static async Task<ElevatedResponse?> RunElevatedAsync(List<TweakOp> ops, TweakStore store)
    {
        string dir = Path.Combine(Path.GetTempPath(), "SenStrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var request = new ElevatedRequest { Ops = ops };
            foreach (var key in ops.Select(o => o.Id).Concat(SharedKeys(store.Backups)))
            {
                if (store.Backups.TryGetValue(key, out var backup))
                    request.Backups[key] = backup;
            }
            string requestFile = Path.Combine(dir, RequestName);
            JsonStore.Save(requestFile, request);

            var info = new ProcessStartInfo(AppInfo.CurrentExe, $"-tweaks \"{requestFile}\"")
            {
                UseShellExecute = true,
                Verb = "runas",
            };
            Process? process;
            try
            {
                process = Process.Start(info);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Logger.Info("The administrator prompt was declined.");
                return null;
            }
            if (process is null) return null;
            using (process)
                await process.WaitForExitAsync();

            string resultFile = Path.Combine(dir, ResultName);
            if (File.Exists(resultFile))
                return JsonStore.Load<ElevatedResponse>(resultFile);

            return new ElevatedResponse
            {
                Results = ops.Select(o => new TweakResult(o.Id, false, "SenStrap couldn't run as administrator")).ToList(),
                Backups = request.Backups,
            };
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    /// <summary>Entry point of the elevated copy (SenStrap.exe -tweaks &lt;request.json&gt;). Only runs machine-wide tweaks.</summary>
    public static int RunElevatedRequest(string requestFile)
    {
        try
        {
            string path = Path.GetFullPath(requestFile);
            string dir = Path.GetDirectoryName(path)!;
            if (Path.GetFileName(path) != RequestName
                || !Path.GetFileName(dir).StartsWith("SenStrap-", StringComparison.Ordinal)
                || new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Logger.Warn($"Rejected tweak request: {path}");
                return 2;
            }
            string resultFile = Path.Combine(dir, ResultName);
            if (File.Exists(resultFile)) return 3;

            var request = JsonStore.Load<ElevatedRequest>(path);
            var store = new TweakStore { Backups = request.Backups };
            var ops = request.Ops.Where(o => TweakCatalog.Find(o.Id) is { Admin: true }).ToList();
            var response = new ElevatedResponse { Results = RunHere(ops, store) };
            foreach (var key in ops.Select(o => o.Id).Concat(SharedKeys(store.Backups)))
            {
                if (store.Backups.TryGetValue(key, out var backup))
                    response.Backups[key] = backup;
            }
            JsonStore.Save(resultFile, response);
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Error("Elevated tweaks failed", ex);
            return 1;
        }
    }

    /// <summary>Backups shared by several tweaks (the power plan one), passed along with any request.</summary>
    private static IEnumerable<string> SharedKeys(Dictionary<string, TweakBackup> backups) =>
        backups.Keys.Where(k => k.StartsWith("shared.", StringComparison.Ordinal)).ToList();

    /// <summary>Changes that undo everything SenStrap applied.</summary>
    public static List<TweakOp> RevertAllOps()
    {
        var store = TweakStore.Load();
        return TweakCatalog.All
            .Where(t => !t.IsAction && (store.Backups.ContainsKey(t.Id) || store.LaunchOptions.Contains(t.Id)))
            .Select(t => new TweakOp(t.Id, false))
            .ToList();
    }

    /// <summary>Restarts Windows Explorer (taskbar and folders) so its settings apply.</summary>
    public static async Task RestartExplorerAsync()
    {
        if (Sys.Sandboxed) return;
        int session = Process.GetCurrentProcess().SessionId;

        bool Running()
        {
            var processes = Process.GetProcessesByName("explorer");
            bool any = processes.Any(p => p.SessionId == session);
            foreach (var p in processes) p.Dispose();
            return any;
        }

        foreach (var explorer in Process.GetProcessesByName("explorer"))
        {
            using (explorer)
            {
                if (explorer.SessionId != session) continue;
                try
                {
                    explorer.Kill();
                    await explorer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                }
            }
        }

        // Windows usually starts it again by itself; if not, start it
        for (int i = 0; i < 12 && !Running(); i++)
            await Task.Delay(250);
        if (!Running())
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
    }
}
