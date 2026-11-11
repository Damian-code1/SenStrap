using System.Text.Json;
using System.Text.Json.Serialization;

namespace SenStrap.Core;

/// <summary>Una partida: juego, servidor, cuándo y cuánto tiempo.</summary>
public class GameEntry
{
    public long PlaceId { get; set; }
    public long UniverseId { get; set; }
    public string JobId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? IconUrl { get; set; }
    public string? Location { get; set; }
    public DateTime JoinedAt { get; set; }
    public double Seconds { get; set; }

    /// <summary>IP del servidor (solo en memoria, para buscar su ubicación).</summary>
    [JsonIgnore]
    public string Ip { get; set; } = "";

    [JsonIgnore]
    public string DisplayName => Name.Length > 0 ? Name : Lang.F("history.unknownGame", PlaceId);

    /// <summary>Enlace para volver a ese mismo servidor.</summary>
    [JsonIgnore]
    public string RejoinUri => JobId.Length > 0
        ? $"roblox://experiences/start?placeId={PlaceId}&gameInstanceId={JobId}"
        : $"roblox://experiences/start?placeId={PlaceId}";
}

/// <summary>Historial de partidas (History.json) y datos públicos de Roblox e ipinfo.io.</summary>
public static class GameHistory
{
    private const int MaxEntries = 60;
    private static readonly object Lock = new();
    private static string FilePath => Path.Combine(AppInfo.BaseDir, "History.json");
    private static HttpClient Http => RobloxDeployment.Http;

    public static List<GameEntry> Load()
    {
        lock (Lock) return JsonStore.Load<List<GameEntry>>(FilePath);
    }

    public static void Add(GameEntry entry)
    {
        lock (Lock)
        {
            var list = JsonStore.Load<List<GameEntry>>(FilePath);
            list.Insert(0, entry);
            if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
            JsonStore.Save(FilePath, list);
        }
    }

    public static void Update(GameEntry entry)
    {
        lock (Lock)
        {
            var list = JsonStore.Load<List<GameEntry>>(FilePath);
            int i = list.FindIndex(e => e.JobId == entry.JobId && e.JoinedAt == entry.JoinedAt);
            if (i < 0) return;
            list[i] = entry;
            JsonStore.Save(FilePath, list);
        }
    }

    public static void Clear()
    {
        lock (Lock) JsonStore.Save(FilePath, new List<GameEntry>());
    }

    /// <summary>Nombre e icono del juego (APIs públicas de Roblox, sin iniciar sesión).</summary>
    public static async Task FillGameInfoAsync(GameEntry entry)
    {
        try
        {
            using (var doc = JsonDocument.Parse(await Http.GetStringAsync($"https://apis.roblox.com/universes/v1/places/{entry.PlaceId}/universe")))
                entry.UniverseId = doc.RootElement.GetProperty("universeId").GetInt64();

            using (var doc = JsonDocument.Parse(await Http.GetStringAsync($"https://games.roblox.com/v1/games?universeIds={entry.UniverseId}")))
                entry.Name = doc.RootElement.GetProperty("data")[0].GetProperty("name").GetString() ?? "";

            using (var doc = JsonDocument.Parse(await Http.GetStringAsync(
                $"https://thumbnails.roblox.com/v1/games/icons?universeIds={entry.UniverseId}&returnPolicy=PlaceHolder&size=150x150&format=Png&isCircular=false")))
                entry.IconUrl = doc.RootElement.GetProperty("data")[0].GetProperty("imageUrl").GetString();
        }
        catch (Exception ex)
        {
            Logger.Warn($"No se pudieron obtener los datos del juego {entry.PlaceId}: {ex.Message}");
        }
    }

    /// <summary>Ciudad y país del servidor, consultando su IP en ipinfo.io.</summary>
    public static async Task<string?> LocateServerAsync(string ip)
    {
        if (!System.Net.IPAddress.TryParse(ip, out _)) return null;
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync($"https://ipinfo.io/{ip}/json"));
            string Get(string name) => doc.RootElement.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";
            var parts = new[] { Get("city"), Get("region"), Get("country") }
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            string text = string.Join(", ", parts);
            return text.Length > 0 ? text : null;
        }
        catch (Exception ex)
        {
            Logger.Warn($"No se pudo localizar el servidor: {ex.Message}");
            return null;
        }
    }
}
