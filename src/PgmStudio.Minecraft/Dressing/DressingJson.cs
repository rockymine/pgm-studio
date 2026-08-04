using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>
/// Serialization for a dressing recipe. The JSON <b>is</b> the record graph round-tripped, the same contract
/// <see cref="TerrainThemeJson"/> holds for a theme: what an authoring surface edits is the wire format the
/// pass deserializes, so there is no second model of a dressing to fall out of step with this one.
/// </summary>
public static class DressingJson
{
    /// <summary>Canonical options: camelCase names, compact, enums by name — a form written by hand or read in
    /// a diff should say <c>"cairn"</c>, not <c>3</c>.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(DressingRecipe recipe) => JsonSerializer.Serialize(recipe, Options);

    public static DressingRecipe Deserialize(string json)
        => JsonSerializer.Deserialize<DressingRecipe>(json, Options) ?? DressingRecipe.Bare;
}
