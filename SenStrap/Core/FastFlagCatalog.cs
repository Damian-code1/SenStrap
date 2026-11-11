using System.Text.Json;

namespace SenStrap.Core;

/// <summary>
/// FastFlags que Roblox sigue aceptando (su lista permitida) y los que usan los ajustes rápidos.
/// Si Roblox cambia la lista, basta con tocar <see cref="Allowlist"/>.
/// </summary>
public static class FastFlagCatalog
{
    public const string Msaa = "FIntDebugForceMSAASamples";
    public const string PauseVoxelizer = "DFFlagDebugPauseVoxelizer";
    public const string GraySky = "FFlagDebugSkyGray";
    public const string GrassMin = "FIntFRMMinGrassDistance";
    public const string GrassMax = "FIntFRMMaxGrassDistance";
    public const string GrassMotion = "FIntGrassMovementReducedMotionFactor";
    public const string Mesh = "DFIntCSGLevelOfDetailSwitchingDistance";
    public const string MeshL12 = "DFIntCSGLevelOfDetailSwitchingDistanceL12";
    public const string MeshL23 = "DFIntCSGLevelOfDetailSwitchingDistanceL23";
    public const string MeshL34 = "DFIntCSGLevelOfDetailSwitchingDistanceL34";
    public const string DisableDpiScale = "DFFlagDisableDPIScale";
    public const string PreferD3D11 = "FFlagDebugGraphicsPreferD3D11";
    public const string PreferVulkan = "FFlagDebugGraphicsPreferVulkan";
    public const string PreferOpenGL = "FFlagDebugGraphicsPreferOpenGL";
    public const string AltEnter = "FFlagHandleAltEnterFullscreenManually";
    public const string QualityLevel = "DFIntDebugFRMQualityLevelOverride";
    public const string TextureEnabled = "DFFlagTextureQualityOverrideEnabled";
    public const string TextureLevel = "DFIntTextureQualityOverride";

    /// <summary>Lista permitida de Roblox: el resto de FastFlags los ignora.</summary>
    public static readonly HashSet<string> Allowlist = new(StringComparer.Ordinal)
    {
        Mesh, MeshL12, MeshL23, MeshL34,
        AltEnter, TextureEnabled, TextureLevel, Msaa, DisableDpiScale,
        PreferD3D11, PreferVulkan, PreferOpenGL,
        GraySky, PauseVoxelizer, QualityLevel,
        GrassMax, GrassMin, GrassMotion,
    };

    /// <summary>Los que se controlan desde "Ajustes rápidos".</summary>
    public static readonly HashSet<string> PresetFlags = new(Allowlist, StringComparer.Ordinal);

    public static string? Get(Dictionary<string, JsonElement> flags, string name)
    {
        if (!flags.TryGetValue(name, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    public static void Set(Dictionary<string, JsonElement> flags, string name, string? value)
    {
        if (value is null) flags.Remove(name);
        else flags[name] = JsonSerializer.SerializeToElement(value);
    }
}
