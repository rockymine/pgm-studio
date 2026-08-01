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
    public static TerrainTheme Deserialize(string json) => JsonSerializer.Deserialize<TerrainTheme>(json, Options)!;
    public static string Serialize(TerrainMaterial material) => JsonSerializer.Serialize(material, Options);
    public static TerrainMaterial DeserializeMaterial(string json) => JsonSerializer.Deserialize<TerrainMaterial>(json, Options)!;
}
