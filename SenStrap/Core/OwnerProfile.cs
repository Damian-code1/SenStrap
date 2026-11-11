using System.Text.Json;

namespace SenStrap.Core;

/// <summary>
/// SenStrap's developer, shown on the Credits page. The picture and banner come with the app and are
/// refreshed from Discord at most once a day, so the page follows profile changes.
/// </summary>
public static class OwnerProfile
{
    public const string UserId = "1407737422732853331";
    public const string Username = "evosen.";
    public const string ProfileUrl = "https://discord.com/users/" + UserId;
    public const string RepoUrl = "https://github.com/Damian-code1/SenStrap";

    public sealed class Info
    {
        public string Username { get; set; } = OwnerProfile.Username;
        public string? DisplayName { get; set; }
        public string? AvatarHash { get; set; }
        public string? BannerHash { get; set; }
        public DateTime FetchedUtc { get; set; }
    }

    private static string CacheDir => Path.Combine(AppInfo.BaseDir, "Cache");
    private static string InfoFile => Path.Combine(CacheDir, "owner.json");
    public static string AvatarFile => Path.Combine(CacheDir, "owner-avatar.png");
    public static string BannerFile => Path.Combine(CacheDir, "owner-banner.png");

    /// <summary>What was saved last time (or null).</summary>
    public static Info? Cached() => File.Exists(InfoFile) ? JsonStore.Load<Info>(InfoFile) : null;

    /// <summary>Downloads the current name, picture and banner if the saved copy is older than a day.</summary>
    /// <returns>The fresh info, or null if nothing changed or Discord couldn't be reached.</returns>
    public static async Task<Info?> RefreshAsync(CancellationToken ct = default)
    {
        var cached = Cached();
        if (cached is not null && DateTime.UtcNow - cached.FetchedUtc < TimeSpan.FromDays(1)
            && File.Exists(AvatarFile))
            return null;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var http = RobloxDeployment.Http;

            string json = await http.GetStringAsync($"https://japi.rest/discord/v1/user/{UserId}", timeout.Token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.TryGetProperty("data", out var data) ? data : doc.RootElement;

            var info = new Info
            {
                Username = Text(root, "username") ?? Username,
                DisplayName = Text(root, "global_name"),
                AvatarHash = Text(root, "avatar"),
                BannerHash = Text(root, "banner"),
                FetchedUtc = DateTime.UtcNow,
            };
            Directory.CreateDirectory(CacheDir);

            if (info.AvatarHash is { } avatar && IsHash(avatar)
                && (avatar != cached?.AvatarHash || !File.Exists(AvatarFile)))
                await DownloadAsync($"https://cdn.discordapp.com/avatars/{UserId}/{avatar}.png?size=256", AvatarFile, timeout.Token);
            if (info.BannerHash is { } banner && IsHash(banner)
                && (banner != cached?.BannerHash || !File.Exists(BannerFile)))
                await DownloadAsync($"https://cdn.discordapp.com/banners/{UserId}/{banner}.png?size=600", BannerFile, timeout.Token);

            JsonStore.Save(InfoFile, info);
            return info;
        }
        catch (Exception ex)
        {
            Logger.Info($"Couldn't refresh the developer's Discord profile: {ex.Message}");
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Discord hashes are hex, with "a_" in front for animated ones.</summary>
    private static bool IsHash(string value) =>
        value.Length is > 8 and < 80 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static async Task DownloadAsync(string url, string path, CancellationToken ct)
    {
        byte[] bytes = await RobloxDeployment.Http.GetByteArrayAsync(url, ct);
        // PNG signature: don't save an error page as a picture
        if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != (byte)'P') return;
        string tmp = path + ".tmp";
        await File.WriteAllBytesAsync(tmp, bytes, ct);
        File.Move(tmp, path, overwrite: true);
    }
}
