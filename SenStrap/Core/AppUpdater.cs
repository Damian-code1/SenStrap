using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SenStrap.Core;

/// <summary>A newer SenStrap release on GitHub.</summary>
public sealed record AppUpdate(Version Version, string DownloadUrl, long Size, string? Sha256);

/// <summary>
/// Updates SenStrap itself from GitHub releases. Only SenStrap.exe is replaced: settings, mods, FastFlags,
/// optimizations and the installed copy of Roblox live in %LOCALAPPDATA%\SenStrap and are not touched.
/// </summary>
public static partial class AppUpdater
{
    private const string LatestReleaseApi = "https://api.github.com/repos/Damian-code1/SenStrap/releases/latest";
    private const string AssetName = "SenStrap.exe";

    /// <summary>Passed to the updated copy so it doesn't check again right away.</summary>
    public const string SkipArgument = "-noupdate";

    [GeneratedRegex(@"\b[0-9a-fA-F]{64}\b")]
    private static partial Regex Sha256Regex();

    private static string Exe => AppInfo.CurrentExe;
    private static string OldExe => Exe + ".old";
    private static string DownloadDir => Path.Combine(AppInfo.BaseDir, "Updates");

    /// <summary>False when running from source ("dotnet run") or when updates are turned off for tests.</summary>
    public static bool CanUpdate =>
        Environment.GetEnvironmentVariable("SENSTRAP_NO_UPDATE") is null
        && Exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        && File.Exists(Exe)
        && !File.Exists(Path.Combine(Path.GetDirectoryName(Exe)!, "SenStrap.dll"));

    /// <summary>Removes what a previous update left behind (the replaced .exe and the download folder).</summary>
    public static void CleanUp()
    {
        try { if (File.Exists(OldExe)) File.Delete(OldExe); } catch { /* still running: next time */ }
        try { if (Directory.Exists(DownloadDir)) Directory.Delete(DownloadDir, recursive: true); } catch { }
    }

    /// <summary>Asks GitHub for the latest release. Returns null if there's nothing newer or GitHub can't be reached.</summary>
    public static async Task<AppUpdate?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await RobloxDeployment.Http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Info($"Update check: GitHub answered {(int)response.StatusCode}.");
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var root = doc.RootElement;
            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;

            string tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
            var current = Version.Parse(AppInfo.Version);
            if (Normalize(latest) <= Normalize(current)) return null;

            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                if (!string.Equals(asset.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase)) continue;
                string url = asset.GetProperty("browser_download_url").GetString() ?? "";
                if (!url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) return null;

                // The release notes include the SHA-256 of the file: use it to check the download
                string notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
                var sha = Sha256Regex().Match(notes);
                Logger.Info($"Update available: {current} -> {latest}.");
                return new AppUpdate(latest, url, asset.GetProperty("size").GetInt64(), sha.Success ? sha.Value.ToLowerInvariant() : null);
            }
            return null;
        }
        catch (Exception ex)
        {
            Logger.Info($"Update check skipped: {ex.Message}");
            return null;
        }
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    /// <summary>Downloads the new SenStrap.exe, checks it and puts it in place of the running one.</summary>
    public static async Task InstallAsync(AppUpdate update, IProgress<(long Done, long Total)> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(DownloadDir);
        string download = Path.Combine(DownloadDir, $"SenStrap-{update.Version}.exe");

        using (var response = await RobloxDeployment.Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? update.Size;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(download);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                progress.Report((done, total));
            }
        }

        var info = new FileInfo(download);
        if (info.Length != update.Size)
            throw new Exception($"The download is incomplete ({info.Length} of {update.Size} bytes).");
        if (update.Sha256 is { } expected)
        {
            await using var stream = File.OpenRead(download);
            string actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
            if (actual != expected)
                throw new Exception("The downloaded file doesn't match the SHA-256 in the release notes.");
        }

        ct.ThrowIfCancellationRequested();

        // Windows lets a running .exe be renamed: move the current one aside and put the new one in its place
        if (File.Exists(OldExe)) File.Delete(OldExe);
        File.Move(Exe, OldExe);
        try
        {
            File.Move(download, Exe);
        }
        catch
        {
            File.Move(OldExe, Exe);
            throw;
        }
        Logger.Info($"SenStrap updated to {update.Version}.");
    }

    /// <summary>Starts the updated SenStrap with the same arguments (for example the game link from roblox.com).</summary>
    public static void Restart(IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(Exe) { UseShellExecute = false };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        info.ArgumentList.Add(SkipArgument);
        Process.Start(info);
    }
}
