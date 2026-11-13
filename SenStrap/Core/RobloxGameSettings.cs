using System.Globalization;
using System.Xml.Linq;

namespace SenStrap.Core;

/// <summary>
/// Roblox's own settings file (GlobalBasicSettings_13.xml): the same options as the in-game Settings menu.
/// SenStrap writes the values you choose there before every launch, so they stick even if Roblox resets them.
/// Roblox rewrites the file when it closes, so it's only written while Roblox isn't running.
/// </summary>
public static class RobloxGameSettings
{
    public enum Kind { Bool, Int, Float, Token }

    /// <summary>A setting SenStrap can change, with the range Roblox's menu allows.</summary>
    public sealed record Option(string Name, Kind Kind, double Min = 0, double Max = 1);

    /// <summary>Range of the FPS override. Roblox's menu stops at 240, but it reads any number from the file at startup.</summary>
    public const int MinFps = 1, MaxFps = 9999;

    public static readonly Option[] Options =
    [
        new("FramerateCap", Kind.Int, MinFps, MaxFps),
        new("SavedQualityLevel", Kind.Token, 0, 10),
        new("Fullscreen", Kind.Bool),
        new("StartMaximized", Kind.Bool),
        new("MasterVolume", Kind.Float, 0, 1),
        new("VoiceChatVolume", Kind.Float, 0, 1),
        new("MouseSensitivity", Kind.Float, 0.1, 4),
        new("CameraYInverted", Kind.Bool),
        new("ReducedMotion", Kind.Bool),
        new("VignetteEnabled", Kind.Bool),
        new("PreferredTransparency", Kind.Float, 0, 1),
        new("PreferredTextSize", Kind.Token, 1, 4),
        new("PerformanceStatsVisible", Kind.Bool),
        new("PlayerNamesEnabled", Kind.Bool),
        new("ChatVisible", Kind.Bool),
        new("PlayerListVisible", Kind.Bool),
        new("ChatTranslationEnabled", Kind.Bool),
        new("AllTutorialsDisabled", Kind.Bool),
    ];

    public static Option? Find(string name) => Options.FirstOrDefault(o => o.Name == name);

    public static string FilePath =>
        Environment.GetEnvironmentVariable("SENSTRAP_ROBLOX_SETTINGS") ?? FindSettingsFile();

    /// <summary>
    /// The newest GlobalBasicSettings_N.xml (the number goes up when Roblox changes the format; today it's 13).
    /// Falls back to _13 when Roblox hasn't created one yet.
    /// </summary>
    private static string FindSettingsFile()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox");
        string fallback = Path.Combine(dir, "GlobalBasicSettings_13.xml");
        const string prefix = "GlobalBasicSettings_";
        try
        {
            if (!Directory.Exists(dir)) return fallback;
            string? best = null;
            int bestNumber = -1;
            foreach (string file in Directory.GetFiles(dir, prefix + "*.xml"))
            {
                // Skips the Studio file (GlobalBasicSettings_13_Studio.xml)
                if (!int.TryParse(Path.GetFileNameWithoutExtension(file)[prefix.Length..], out int number)) continue;
                if (number > bestNumber) { bestNumber = number; best = file; }
            }
            return best ?? fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public static bool Exists => File.Exists(FilePath);

    private static XElement? Properties(XDocument doc) =>
        doc.Descendants("Item").FirstOrDefault(i => (string?)i.Attribute("class") == "UserGameSettings")?.Element("Properties");

    /// <summary>The current values in Roblox's file (only the options SenStrap knows).</summary>
    public static Dictionary<string, string> Read()
    {
        var values = new Dictionary<string, string>();
        try
        {
            if (!Exists) return values;
            var props = Properties(XDocument.Load(FilePath, LoadOptions.PreserveWhitespace));
            if (props is null) return values;
            foreach (var element in props.Elements())
            {
                string? name = (string?)element.Attribute("name");
                if (name is not null && Find(name) is not null)
                    values[name] = element.Value.Trim();
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Couldn't read Roblox's settings: {ex.Message}");
        }
        return values;
    }

    /// <summary>Clamps and formats a value the way Roblox stores it. Null if it isn't valid.</summary>
    public static string? Normalize(Option option, string value)
    {
        switch (option.Kind)
        {
            case Kind.Bool:
                return bool.TryParse(value, out bool b) ? (b ? "true" : "false") : null;
            case Kind.Float:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double f)) return null;
                return Math.Clamp(f, option.Min, option.Max).ToString("0.###", CultureInfo.InvariantCulture);
            default:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return null;
                return ((int)Math.Clamp(i, option.Min, option.Max)).ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Writes the chosen values into Roblox's file. Does nothing while Roblox is open (it would overwrite them).</summary>
    public static bool Apply(Dictionary<string, string> overrides)
    {
        if (overrides.Count == 0 || !Exists) return false;
        if (RobloxInstaller.IsRobloxPlayerRunning())
        {
            Logger.Info("Roblox is open: its settings will be written the next time SenStrap opens it.");
            return false;
        }
        try
        {
            // Only the chosen values are replaced in the text, so the rest of Roblox's file stays byte for byte
            string text = File.ReadAllText(FilePath);
            int changed = 0;
            foreach (var (name, raw) in overrides)
            {
                if (Find(name) is not { } option || Normalize(option, raw) is not { } value) continue;
                var pattern = new System.Text.RegularExpressions.Regex(
                    "(<(bool|int|float|token) name=\"" + System.Text.RegularExpressions.Regex.Escape(name) + "\">)([^<]*)(</\\2>)");
                var match = pattern.Match(text);
                if (match.Success)
                {
                    if (match.Groups[3].Value == value) continue;
                    text = text[..match.Groups[3].Index] + value + text[(match.Groups[3].Index + match.Groups[3].Length)..];
                }
                else
                {
                    int end = text.IndexOf("</Properties>", StringComparison.Ordinal);
                    if (end < 0) continue;
                    string tag = option.Kind switch { Kind.Bool => "bool", Kind.Float => "float", Kind.Token => "token", _ => "int" };
                    text = text.Insert(end, $"<{tag} name=\"{name}\">{value}</{tag}>\n\t\t");
                }
                changed++;
            }
            if (changed == 0) return true;

            string tmp = FilePath + ".senstrap.tmp";
            File.WriteAllText(tmp, text, new System.Text.UTF8Encoding(false));
            File.Copy(FilePath, FilePath + ".senstrap.bak", overwrite: true);
            File.Move(tmp, FilePath, overwrite: true);
            Logger.Info($"Roblox settings written ({changed} changed).");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Couldn't write Roblox's settings: {ex.Message}");
            return false;
        }
    }
}
