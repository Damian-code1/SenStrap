using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace SenStrap.Core;

/// <summary>
/// Traducciones. Cada idioma es un JSON en Resources\Languages (clave → texto).
/// Si falta una clave, se usa el inglés. También se puede poner un JSON en
/// %LOCALAPPDATA%\SenStrap\Languages\&lt;código&gt;.json para corregir o añadir textos.
/// </summary>
public static class Lang
{
    public sealed record Language(string Code, string NativeName, string EnglishName);

    public static readonly Language[] All =
    [
        new("en", "English", "English"),
        new("es", "Español", "Spanish"),
        new("pt-BR", "Português (Brasil)", "Portuguese (Brazil)"),
        new("fr", "Français", "French"),
        new("de", "Deutsch", "German"),
        new("it", "Italiano", "Italian"),
        new("ru", "Русский", "Russian"),
        new("uk", "Українська", "Ukrainian"),
        new("pl", "Polski", "Polish"),
        new("tr", "Türkçe", "Turkish"),
        new("id", "Bahasa Indonesia", "Indonesian"),
        new("fil", "Filipino", "Filipino"),
        new("vi", "Tiếng Việt", "Vietnamese"),
        new("ja", "日本語", "Japanese"),
        new("ko", "한국어", "Korean"),
        new("zh-CN", "简体中文", "Chinese (Simplified)"),
    ];

    private static Dictionary<string, string> _strings = new();
    private static Dictionary<string, string> _english = new();

    public static string Current { get; private set; } = "en";

    public static void Load(string code)
    {
        if (All.All(l => l.Code != code)) code = "en";
        _english = Read("en");
        _strings = code == "en" ? _english : Read(code);
        Current = code;

        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
        }
    }

    /// <summary>Texto traducido para una clave.</summary>
    public static string T(string key) =>
        _strings.TryGetValue(key, out var text) ? text
        : _english.TryGetValue(key, out text) ? text
        : key;

    /// <summary>Texto traducido con valores ({0}, {1}...).</summary>
    public static string F(string key, params object[] args)
    {
        try { return string.Format(T(key), args); }
        catch (FormatException) { return T(key); }
    }

    /// <summary>"1 h 20 min" / "45 min".</summary>
    public static string Duration(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1
            ? F("time.hm", (int)time.TotalHours, time.Minutes)
            : F("time.m", Math.Max(1, (int)time.TotalMinutes));
    }

    /// <summary>El idioma de Windows, si está entre los disponibles.</summary>
    public static string Detect()
    {
        var culture = CultureInfo.CurrentUICulture;
        var exact = All.FirstOrDefault(l => l.Code.Equals(culture.Name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact.Code;
        string two = culture.TwoLetterISOLanguageName;
        if (two == "pt") return "pt-BR";
        if (two == "zh") return "zh-CN";
        if (two == "tl") return "fil";
        return All.FirstOrDefault(l => l.Code == two)?.Code ?? "en";
    }

    private static Dictionary<string, string> Read(string code)
    {
        try
        {
            string external = Path.Combine(AppInfo.BaseDir, "Languages", code + ".json");
            string? json = null;
            if (File.Exists(external))
            {
                json = File.ReadAllText(external);
            }
            else
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Languages.{code}.json");
                if (stream is not null)
                {
                    using var reader = new StreamReader(stream);
                    json = reader.ReadToEnd();
                }
            }
            if (json is null) return new();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) ?? new();
        }
        catch (Exception ex)
        {
            Logger.Warn($"No se pudo cargar el idioma {code}: {ex.Message}");
            return new();
        }
    }
}
