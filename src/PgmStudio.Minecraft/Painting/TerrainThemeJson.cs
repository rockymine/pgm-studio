using System.Text.Json;
using System.Text.Json.Nodes;

namespace PgmStudio.Minecraft.Painting;

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
    /// polymorphism attributes on <see cref="TerrainMaterial"/>, and
    /// <see cref="JsonSerializerOptions.AllowOutOfOrderMetadataProperties"/> reads it wherever it falls in the
    /// object rather than only as the first key — a discriminator's position is a serialization detail, and
    /// key order carries no meaning in JSON, so any tool that reorders a document must not change what it
    /// says.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        AllowOutOfOrderMetadataProperties = true,
    };

    public static string Serialize(TerrainTheme theme) => JsonSerializer.Serialize(theme, Options);

    /// <summary>Read a theme, upgrading anything written before the model it names existed. See
    /// <see cref="Upgrade"/> for what is carried forward and why.</summary>
    public static TerrainTheme Deserialize(string json)
        => Materialized(Read<TerrainTheme>(Upgraded(json)));

    /// <summary>Read a theme and say what of it went unread — see
    /// <see cref="PgmStudio.Domain.DocumentShape"/> for why that is a complaint and not a refusal.</summary>
    public static TerrainTheme Deserialize(string json, out IReadOnlyList<string> unread)
    {
        var node = Upgraded(json);
        var theme = Materialized(Read<TerrainTheme>(node));
        unread = PgmStudio.Domain.DocumentShape.Unread(node, theme);
        return theme;
    }

    /// <summary>Refuse a bucket that was stated and carries no material. Every bucket has a default, so an
    /// absent key is fine; a key holding the wrong shape is not. <c>rim</c> and <c>surface</c> take a band —
    /// <c>{"material": …, "depth": N}</c> — so a bare material written at either leaves the band's own
    /// material null, and the painter reads it a whole raster later as a null dereference. Named here, at the
    /// read, the fault is the document's and says which field it is.</summary>
    private static TerrainTheme Materialized(TerrainTheme theme)
    {
        foreach (var (field, missing) in (ReadOnlySpan<(string, bool)>)
                 [("rim", theme.Rim?.Material is null), ("surface", theme.Surface?.Material is null),
                  ("wall", theme.Wall is null), ("fill", theme.Fill is null)])
            if (missing)
                throw new JsonException(
                    $"'{field}' names no material — rim and surface take a band, {{\"material\": …, " +
                    "\"depth\": N}, and wall and fill take a material directly");
        return theme;
    }

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

        // A stored stack may carry a bare `layers` list where today's shape is a `BandStack`: the bands under
        // a name of their own, plus what the stack does where they run out. The list is read forward into it.
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
    public static TerrainMaterial DeserializeMaterial(string json) => Read<TerrainMaterial>(Upgraded(json));

    /// <summary>Read one biome field. It is polymorphic on <c>kind</c> the way a material is, so it takes the
    /// same translation of a missing or unknown discriminator — and deliberately not <see cref="Upgraded"/>,
    /// whose renames are of fields a material once had and a biome field never did.</summary>
    public static BiomeField DeserializeBiome(string json) => Read<BiomeField>(JsonNode.Parse(json)!);

    /// <summary>Deserialize, carrying the kind fault across whatever depth it was found at. A material is
    /// polymorphic on <c>kind</c>, and System.Text.Json reports a missing discriminator as
    /// <see cref="NotSupportedException"/> rather than <see cref="JsonException"/> — a difference in how it is
    /// reported, not in what went wrong. Every reader that can contain a material goes through here, because a
    /// material nested inside a theme or a style is the same fault as one posted on its own and was answering
    /// 500 while the bare one answered 400.</summary>
    private static T Read<T>(JsonNode node)
    {
        try { return JsonSerializer.Deserialize<T>(node, Options)!; }
        catch (NotSupportedException ex) { throw new JsonException(KindFault, ex); }
    }

    /// <summary>Read one material and say what of it went unread. A material is polymorphic, so the walk goes
    /// down the value rather than the declared type — a misspelled knob on a voronoi is checked against the
    /// voronoi record, which a walk over <see cref="TerrainMaterial"/> alone could not do.</summary>
    public static TerrainMaterial DeserializeMaterial(string json, out IReadOnlyList<string> unread)
    {
        var node = Upgraded(json);
        TerrainMaterial material;
        try { material = JsonSerializer.Deserialize<TerrainMaterial>(node, Options)!; }
        catch (NotSupportedException ex) { throw new JsonException(KindFault, ex); }
        unread = PgmStudio.Domain.DocumentShape.Unread(node, material);
        return material;
    }

    /// <summary>What a material whose <c>kind</c> cannot be resolved is refused with. Named once so the
    /// endpoints, the style reader and their tests read the same sentence.</summary>
    internal const string KindFault =
        "a material names no kind, or names one that does not exist — see GET /api/terrain/patterns";

    /// <summary>The same sentence, for the readers outside this assembly's painting namespace that can also
    /// contain a material — a house style's courses.</summary>
    public const string MaterialKindFault = KindFault;
}
