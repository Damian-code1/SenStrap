using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SenStrap.Core;

public record ClientVersion(string Version, string VersionGuid);

public record PackageInfo(string Name, string Md5, long PackedSize, long UnpackedSize);

/// <summary>
/// Habla con los servidores oficiales de Roblox: cuál es la versión actual
/// y qué paquetes forman el cliente.
/// </summary>
public static partial class RobloxDeployment
{
    public static readonly HttpClient Http = CreateHttpClient();

    private static readonly string[] ClientSettingsHosts =
    [
        "https://clientsettingscdn.roblox.com",
        "https://clientsettings.roblox.com",
    ];

    private static readonly string[] SetupHosts =
    [
        "https://setup.rbxcdn.com",
        "https://setup-aws.rbxcdn.com",
        "https://setup-ak.rbxcdn.com",
        "https://roblox-setup.cachefly.net",
        "https://s3.amazonaws.com/setup.roblox.com",
    ];

    private static string? _workingSetupHost;

    /// <summary>Carpeta (dentro de la versión) donde se descomprime cada paquete.</summary>
    private static readonly Dictionary<string, string> PackageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RobloxApp.zip"] = "",
        ["redist.zip"] = "",
        ["Libraries.zip"] = "",
        ["shaders.zip"] = @"shaders\",
        ["ssl.zip"] = @"ssl\",
        ["WebView2.zip"] = "",
        ["WebView2RuntimeInstaller.zip"] = @"WebView2RuntimeInstaller\",

        ["content-avatar.zip"] = @"content\avatar\",
        ["content-configs.zip"] = @"content\configs\",
        ["content-fonts.zip"] = @"content\fonts\",
        ["content-sky.zip"] = @"content\sky\",
        ["content-sounds.zip"] = @"content\sounds\",
        ["content-textures2.zip"] = @"content\textures\",
        ["content-models.zip"] = @"content\models\",

        ["content-platform-fonts.zip"] = @"PlatformContent\pc\fonts\",
        ["content-platform-dictionaries.zip"] = @"PlatformContent\pc\shared_compression_dictionaries\",
        ["content-terrain.zip"] = @"PlatformContent\pc\terrain\",
        ["content-textures3.zip"] = @"PlatformContent\pc\textures\",

        ["extracontent-luapackages.zip"] = @"ExtraContent\LuaPackages\",
        ["extracontent-translations.zip"] = @"ExtraContent\translations\",
        ["extracontent-models.zip"] = @"ExtraContent\models\",
        ["extracontent-textures.zip"] = @"ExtraContent\textures\",
        ["extracontent-places.zip"] = @"ExtraContent\places\",
    };

    [GeneratedRegex("^version-[0-9a-f]+$")]
    private static partial Regex VersionGuidRegex();

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex Md5Regex();

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(15),
        });
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Name}/{AppInfo.Version}");
        return http;
    }

    /// <summary>Pregunta a Roblox cuál es la versión actual del cliente de Windows.</summary>
    public static async Task<ClientVersion> GetClientVersionAsync(CancellationToken ct)
    {
        Exception? last = null;
        foreach (var host in ClientSettingsHosts)
        {
            try
            {
                string json = await Http.GetStringAsync($"{host}/v2/client-version/WindowsPlayer", ct);
                using var doc = JsonDocument.Parse(json);
                string? version = doc.RootElement.GetProperty("version").GetString();
                string? guid = doc.RootElement.GetProperty("clientVersionUpload").GetString();

                if (string.IsNullOrEmpty(version) || guid is null || !VersionGuidRegex().IsMatch(guid))
                    throw new InvalidDataException("Respuesta inesperada de Roblox: " + json);

                return new ClientVersion(version, guid);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                last = ex;
                Logger.Warn($"No se pudo consultar {host}: {ex.Message}");
            }
        }
        throw new Exception("No se pudo consultar la versión de Roblox. ¿Tienes conexión a internet?", last);
    }

    /// <summary>Descarga la lista de paquetes (rbxPkgManifest.txt) de una versión.</summary>
    public static async Task<List<PackageInfo>> GetPackagesAsync(string versionGuid, CancellationToken ct)
    {
        using var response = await GetFromSetupAsync($"/{versionGuid}-rbxPkgManifest.txt", ct);
        string text = await response.Content.ReadAsStringAsync(ct);

        string[] lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        if (lines.Length == 0 || lines[0] != "v0")
            throw new InvalidDataException("Formato de manifiesto desconocido: " + lines.FirstOrDefault());

        // Después de "v0" van grupos de 4 líneas: nombre, md5, tamaño comprimido, tamaño descomprimido
        var packages = new List<PackageInfo>();
        for (int i = 1; i + 3 < lines.Length; i += 4)
        {
            string md5 = lines[i + 1].ToLowerInvariant();
            if (!Md5Regex().IsMatch(md5))
                throw new InvalidDataException($"MD5 no válido para {lines[i]}: {md5}");
            packages.Add(new PackageInfo(lines[i], md5, long.Parse(lines[i + 2]), long.Parse(lines[i + 3])));
        }
        return packages;
    }

    /// <summary>Empieza la descarga de un paquete. Hay que leer y cerrar la respuesta.</summary>
    public static Task<HttpResponseMessage> DownloadPackageAsync(string versionGuid, string packageName, CancellationToken ct) =>
        GetFromSetupAsync($"/{versionGuid}-{packageName}", ct);

    /// <summary>Carpeta donde va un paquete, o null si no hay que instalarlo.</summary>
    public static string? GetExtractDir(string packageName)
    {
        if (PackageMap.TryGetValue(packageName, out var dir))
            return dir;

        if (!packageName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return null; // p. ej. RobloxPlayerInstaller.exe: no hace falta

        // Paquete nuevo que todavía no está en la tabla: intentamos adivinar su carpeta.
        string stem = packageName[..^4];
        string? guess = null;
        if (stem.StartsWith("content-", StringComparison.OrdinalIgnoreCase))
            guess = $@"content\{stem["content-".Length..]}\";
        else if (stem.StartsWith("extracontent-", StringComparison.OrdinalIgnoreCase))
            guess = $@"ExtraContent\{stem["extracontent-".Length..]}\";

        Logger.Warn(guess is null
            ? $"Paquete desconocido {packageName}: se omite. Añádelo a PackageMap en RobloxDeployment.cs."
            : $"Paquete desconocido {packageName}: se instalará en {guess}. Revisa PackageMap en RobloxDeployment.cs.");
        return guess;
    }

    private static async Task<HttpResponseMessage> GetFromSetupAsync(string resource, CancellationToken ct)
    {
        var hosts = _workingSetupHost is null ? SetupHosts : SetupHosts.Prepend(_workingSetupHost).Distinct();
        Exception? last = null;

        foreach (var host in hosts)
        {
            try
            {
                var response = await Http.GetAsync(host + resource, HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.IsSuccessStatusCode)
                {
                    _workingSetupHost = host;
                    return response;
                }
                last = new HttpRequestException($"{host}{resource} respondió {(int)response.StatusCode}");
                response.Dispose();
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                last = ex;
            }
            Logger.Warn($"Fallo al descargar desde {host}: {last?.Message}");
        }
        throw new Exception($"No se pudo descargar {resource.TrimStart('/')} de ningún servidor de Roblox.", last);
    }
}
