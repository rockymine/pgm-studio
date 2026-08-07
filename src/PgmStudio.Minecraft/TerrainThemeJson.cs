using System.Text.Json;

namespace PgmStudio.Minecraft;

/// <summary>
/// Serialization for a terrain-paint theme (docs/world-export/terrain-painting.md §3). The theme JSON <b>is</b>
/// the record graph round-tripped: the depth/toggle knobs plus one material per bucket, each material tagged
/// with its <c>kind</c> discriminator so a pattern — or a team tint nested inside one — serializes without loss.
/// This closes the material model: it is the data a TP10 scope will attach to a piece or collection, and the
/// shape a future authoring surface reads and writes.
/// </summary>
public static class TerrainThemeJson
{
    /// <summary>Canonical options: camelCase names, compact. The <c>kind</c> discriminator comes from the
    /// polymorphism attributes on <see cref="TerrainMaterial"/>.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string Serialize(TerrainTheme theme) => JsonSerializer.Serialize(theme, Options);

    /// <summary>Read a theme, honouring the two-value <c>closed</c> knob <see cref="TerrainTheme.RimEdges"/>
    /// replaced. A theme written before the void-only mode existed says <c>closed: true</c> for what is now
    /// <see cref="RimEdges.Boundary"/> and nothing for <see cref="RimEdges.Drop"/>; without this a stored map
    /// would quietly repaint to the default rim on its next export. Only consulted when the theme names no
    /// <c>rimEdges</c> of its own, so a current theme costs nothing but the parse.</summary>
    public static TerrainTheme Deserialize(string json)
    {
        var theme = JsonSerializer.Deserialize<TerrainTheme>(json, Options)!;
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && !root.TryGetProperty("rimEdges", out _)
            && root.TryGetProperty("closed", out var closed)
            && closed.ValueKind == JsonValueKind.True)
            return theme with { RimEdges = RimEdges.Boundary };
        return theme;
    }
    public static string Serialize(TerrainMaterial material) => JsonSerializer.Serialize(material, Options);
    public static TerrainMaterial DeserializeMaterial(string json) => JsonSerializer.Deserialize<TerrainMaterial>(json, Options)!;
}
