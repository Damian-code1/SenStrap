using System.Text.Json;

namespace SenStrap.Core;

/// <summary>Escribe los FastFlags en ClientSettings\ClientAppSettings.json, que es donde Roblox los lee.</summary>
public static class FastFlags
{
    public static void Apply(string versionDir, Dictionary<string, JsonElement> flags)
    {
        string dir = Path.Combine(versionDir, "ClientSettings");
        string file = Path.Combine(dir, "ClientAppSettings.json");

        if (flags.Count == 0)
        {
            if (File.Exists(file)) File.Delete(file);
            return;
        }

        Directory.CreateDirectory(dir);
        File.WriteAllText(file, JsonSerializer.Serialize(flags, JsonStore.Options));
        Logger.Info($"{flags.Count} FastFlags aplicados.");
    }
}
