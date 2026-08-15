using System.Text.Json;
using System.Text.Json.Nodes;

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

    /// <summary>Read a theme, upgrading anything written before the model it names existed. See
    /// <see cref="Upgrade"/> for what is carried forward and why.</summary>
    public static TerrainTheme Deserialize(string json)
        => JsonSerializer.Deserialize<TerrainTheme>(Upgraded(json), Options)!;

    private static JsonNode Upgraded(string json)
    {
        // A document that is absent reads the same way to an author as one that is empty, and both are the
        // "will not parse" case docs/refusals.md describes. JsonNode.Parse raises ArgumentNullException on a
        // null string rather than a JsonException, which is the one type every caller's catch does not name,
        // so the fault escaped the gate above as a stack trace instead of arriving as a finding.
        if (string.IsNullOrWhiteSpace(json)) throw new JsonException("no theme JSON was posted");
        var node = JsonNode.Parse(json) ?? throw new JsonException("empty theme JSON");
        // `closed` is a theme-level knob, so it is answered here rather than inside the material walk — a
        // material that happened to carry the word would otherwise grow a rim mode that means nothing to it.
        if (node is JsonObject root && root["closed"] is JsonValue closed && root["rimEdges"] is null)
            root["rimEdges"] = closed.TryGetValue<bool>(out var wasClosed) && wasClosed ? "boundary" : "drop";
        Upgrade(node);
        return node;
    }

    /// <summary>
    /// Carry a stored theme forward onto the current model, in place. Two shapes have been replaced, and a map
    /// that stored either must keep painting what it painted — a silent repaint on the next export is worse than
    /// a refusal, because nothing says it happened.
    ///
    /// <para><b><c>closed</c> → <c>rimEdges</c>.</b> The rim's two-value flag became a three-value word once
    /// "cap only the void" became sayable. <c>closed: true</c> is <c>boundary</c>; anything else is the default
    /// <c>drop</c>. Only consulted when the theme names no <c>rimEdges</c> of its own.</para>
    ///
    /// <para><b>A voronoi's <c>palette</c>/<c>rim</c>/<c>rimWidth</c> → <c>bands</c>.</b> The pattern used to
    /// pick one fill per region at random and trace the boundary with a separate rim; it is now a ramp inward
    /// from that boundary. The rim becomes band 0 at its old width and the first palette entry becomes the fill,
    /// which is what such a theme already looked like wherever it read as cells at all. The rest of the palette
    /// is dropped rather than guessed at: a random per-region fill is what the <c>cell</c> pattern is now for,
    /// and inventing depths for materials that never had any would be a different picture presented as the same
    /// one. Applied at every depth, since a voronoi nests inside stacks and other patterns.</para>
    /// </summary>
    internal static void Upgrade(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array) Upgrade(item);
            return;
        }
        if (node is not JsonObject obj) return;

        if (obj["kind"] is JsonValue kind && kind.TryGetValue<string>(out var k) && k == "voronoi"
            && obj["bands"] is null)
        {
            var bands = new JsonArray();
            int rimWidth = obj["rimWidth"] is JsonValue w && w.TryGetValue<int>(out var rw) ? rw : 0;
            if (rimWidth > 0 && obj["rim"] is JsonObject rim)
                bands.Add(new JsonObject { ["material"] = rim.DeepClone(), ["depth"] = rimWidth });
            if (obj["palette"] is JsonArray palette && palette.Count > 0 && palette[0] is { } fill)
                bands.Add(new JsonObject { ["material"] = fill.DeepClone(), ["depth"] = 1 });
            if (bands.Count > 0) obj["bands"] = bands;
            obj.Remove("palette"); obj.Remove("rim"); obj.Remove("rimWidth");
        }

        // A layer stack was a bare `layers` list — one rule written out beside two others that read the same
        // bands along a different distance. It is a `BandStack` now: the bands under a name of their own, plus
        // what the stack does where they run out, which had been assumed rather than said.
        if (obj["stack"] is null && obj["layers"] is JsonArray layers)
        {
            obj["stack"] = Stacked(layers, "thickness");
            obj.Remove("layers");
        }

        foreach (var (_, child) in obj.ToList()) Upgrade(child);
    }

    /// <summary>A stored list of bands as the stack it is now, reading each band's thickness from whichever key
    /// that list spelled it with. Both lists this reads owned their whole space, so both repeat.</summary>
    internal static JsonObject Stacked(JsonArray bands, string thickness)
    {
        var carried = new JsonArray();
        foreach (var band in bands.OfType<JsonObject>())
            carried.Add(new JsonObject
            {
                ["material"] = band["material"]?.DeepClone(),
                ["thickness"] = band[thickness]?.DeepClone() ?? 1,
            });
        return new JsonObject { ["bands"] = carried, ["ending"] = "repeat" };
    }

    public static string Serialize(TerrainMaterial material) => JsonSerializer.Serialize(material, Options);

    /// <summary>Read one material. A material is polymorphic on <c>kind</c>, and a kind that is missing or not
    /// one of the names raises <see cref="NotSupportedException"/> rather than a <see cref="JsonException"/> —
    /// a difference in how System.Text.Json reports it, not in what went wrong, so it is carried across here.
    /// Without this a misspelled <c>kind</c> left the gate above as a stack trace while a misspelled field name
    /// was accepted in silence, which is the wrong way round.</summary>
    public static TerrainMaterial DeserializeMaterial(string json)
    {
        try { return JsonSerializer.Deserialize<TerrainMaterial>(Upgraded(json), Options)!; }
        catch (NotSupportedException ex) { throw new JsonException(KindFault, ex); }
    }

    /// <summary>What a material whose <c>kind</c> cannot be resolved is refused with. Named once so the
    /// endpoints and their tests read the same sentence.</summary>
    internal const string KindFault =
        "a material names no kind, or names one that does not exist — see GET /api/terrain/patterns";
}
