using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>The dressing a map carries: everything the author placed, in the order they placed it. Stored
/// beside the sketch geometry, because a prop's position is as much a part of the map as a shape's.</summary>
public sealed record DressingDoc
{
    public List<PlacedProp> Props { get; init; } = [];

    /// <summary>Nothing placed — what a map that never opened the phase carries, and what makes the pass a
    /// no-op rather than a walk over an empty world.</summary>
    public static DressingDoc Empty { get; } = new();
}

/// <summary>
/// Serialization for placed dressing. The JSON <b>is</b> the record graph round-tripped, the same contract
/// <see cref="TerrainThemeJson"/> holds for a theme: what the canvas writes is the wire format the pass
/// deserializes, so there is no second model of a prop to fall out of step with this one.
///
/// <para>Props are polymorphic on a <c>kind</c> discriminator (<see cref="PlacedProp"/>), which is what lets
/// one list hold a path, a tree and an area without the reader having to sort them first.</para>
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

    public static string Serialize(DressingDoc doc) => JsonSerializer.Serialize(doc, Options);

    public static DressingDoc Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<DressingDoc>(Upgraded(json), Options) ?? DressingDoc.Empty; }
        catch (JsonException) { return DressingDoc.Empty; }   // a hand-edited blob must not fail an export
    }

    public static string SerializeProp(PlacedProp prop) => JsonSerializer.Serialize(prop, Options);

    public static PlacedProp? DeserializeProp(string json)
    {
        try { return JsonSerializer.Deserialize<PlacedProp>(Upgraded(json), Options); }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// Carry stored dressing forward onto the current model, in place — the sibling of
    /// <see cref="TerrainThemeJson.Upgrade"/>, and it delegates to that one for the materials a prop now holds,
    /// so a bank, a pave and a rock are all read by the same rules a theme's buckets are.
    ///
    /// <para><b>A boulder's <c>blockId</c>/<c>blockData</c> → <c>rock</c>.</b> A rock was one block and is now a
    /// material, so the stored pair becomes the solid it always was.</para>
    ///
    /// <para><b>A path's <c>blocks</c> → <c>pave</c>.</b> Same move, with one wrinkle: the retired
    /// <c>cobble</c> style tiled a path's several blocks over a jittered grid, which is exactly what the
    /// <c>cell</c> pattern does. A stored cobbled path therefore becomes a cell material over the same grid it
    /// was already tiled by, and its style falls back to <c>solid</c> — the band it always paved. Any other
    /// style spent only the first block, so that is the solid it becomes.</para>
    /// </summary>
    private static JsonNode Upgraded(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new JsonException("empty dressing JSON");
        // Either a whole document or one bare prop — both readers upgrade, since a prop is edited on its own.
        var props = node is JsonObject doc && doc["props"] is JsonArray list ? list.AsEnumerable() : [node];
        foreach (var prop in props) UpgradeProp(prop as JsonObject);
        TerrainThemeJson.Upgrade(node);
        return node;
    }

    // The grid a cobbled path was tiled by, and the salt its sites were hashed with — kept so a stored one
    // upgrades onto the same patches it already had rather than onto a different road of the same idea.
    private const int CobbleGrid = 3;
    private const uint CobbleSalt = 29;

    private static void UpgradeProp(JsonObject? prop)
    {
        if (prop is null) return;
        var kind = prop["kind"] is JsonValue k && k.TryGetValue<string>(out var name) ? name : null;

        if (kind == "boulder" && prop["rock"] is null && prop["blockId"] is JsonValue id)
        {
            prop["rock"] = Solid(id, prop["blockData"]);
            prop.Remove("blockId");
            prop.Remove("blockData");
        }

        if (kind == "path" && prop["pave"] is null && prop["blocks"] is JsonArray blocks && blocks.Count > 0)
        {
            var cobbled = prop["style"] is JsonValue style && style.TryGetValue<string>(out var s)
                && string.Equals(s, "cobble", StringComparison.OrdinalIgnoreCase);
            var palette = new JsonArray([.. blocks.OfType<JsonObject>()
                .Select(block => (JsonNode)Solid(block["id"], block["data"]))]);
            prop["pave"] = cobbled && palette.Count > 1
                ? new JsonObject
                {
                    ["kind"] = "cell",
                    ["seed"] = Seed(prop) + CobbleSalt,
                    ["cellSize"] = CobbleGrid,
                    ["jitter"] = 100,
                    ["warp"] = 0,
                    ["palette"] = palette,
                }
                : palette[0]!.DeepClone();
            if (cobbled) prop["style"] = "solid";
            prop.Remove("blocks");
        }
    }

    private static uint Seed(JsonObject prop)
        => prop["seed"] is JsonValue seed && seed.TryGetValue<uint>(out var value) ? value : 0;

    private static JsonObject Solid(JsonNode? id, JsonNode? data) => new()
    {
        ["kind"] = "solid",
        ["id"] = id?.DeepClone() ?? 1,
        ["data"] = data?.DeepClone() ?? 0,
    };
}
