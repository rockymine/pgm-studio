using PgmStudio.Domain;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
/// A dressing document — or one prop inside it — did not parse. Thrown rather than swallowed (DR-DOC): the
/// export gate turns this into a named refusal instead of a map that quietly builds with fewer props than it
/// was asked for. <see cref="Subject"/> names the prop (by id, or by position when it has none yet);
/// <see cref="Field"/> names the property inside it that could not be read, or is null when the fault is the
/// document's shape rather than one field of it.
/// </summary>
public sealed class DressingParseException(string subject, string? field, string detail)
    : Exception(field is null ? $"{subject} {detail}." : $"{subject}: field '{field}' {detail}.")
{
    /// <summary>The rule this refusal carries into the export gate, so a caller can act on the id rather than
    /// parse the sentence.</summary>
    public const string Rule = "DR-DOC";

    public string Subject { get; } = subject;
    public string? Field { get; } = field;

    /// <summary>The same refusal as a <see cref="Finding"/>, which is what the export gate answers in. Thrown
    /// rather than returned because a parse cannot carry on to collect a second fault, and a finding rather
    /// than a bare message because every other gate in the studio answers in one.</summary>
    public Finding Finding => new(Rule, Message, Field: Field, Subjects: [Subject]);
}

/// <summary>
/// Serialization for placed dressing. The JSON <b>is</b> the record graph round-tripped, the same contract
/// <see cref="TerrainThemeJson"/> holds for a theme: what the canvas writes is the wire format the pass
/// deserializes, so there is no second model of a prop to fall out of step with this one.
///
/// <para>Props are polymorphic on a <c>kind</c> discriminator (<see cref="PlacedProp"/>), which is what lets
/// one list hold a path, a tree and an area without the reader having to sort them first.</para>
///
/// <para>A document that does not parse throws <see cref="DressingParseException"/> naming the prop and the
/// field, rather than being read as though it had no props — a hand-edited or generated blob with one bad
/// field must not cost the whole map its dressing.</para>
/// </summary>
public static class DressingJson
{
    /// <summary>Canonical options: camelCase names, compact, enums by name — a form written by hand or read in
    /// a diff should say <c>"cairn"</c>, not <c>3</c>. <see cref="JsonSerializerOptions.AllowOutOfOrderMetadataProperties"/>
    /// reads a prop's or a material's <c>kind</c> wherever it falls in the object rather than only as the first
    /// key — a discriminator's position is a serialization detail, not something an author authoring by hand
    /// should have to get right.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(DressingDoc doc) => JsonSerializer.Serialize(doc, Options);

    /// <summary>Every prop, parsed and upgraded. A document with no <c>props</c> key at all is what a map that
    /// never opened the phase carries and reads as <see cref="DressingDoc.Empty"/>'s shape; anything else that
    /// does not parse throws <see cref="DressingParseException"/> naming the prop and the field.</summary>
    public static DressingDoc Deserialize(string json)
    {
        var node = ParseNode(json, "the document");
        if (node is not JsonObject root)
            throw new DressingParseException("the document", null, $"is {Describe(node)}, not an object of props");

        var propsNode = root["props"];
        if (propsNode is null) return DressingDoc.Empty;
        if (propsNode is not JsonArray propsArray)
            throw new DressingParseException("the document", "props", $"is {Describe(propsNode)}, not a list");

        var props = new List<PlacedProp>(propsArray.Count);
        for (var index = 0; index < propsArray.Count; index++)
            props.Add(ParseProp(propsArray[index], Label(propsArray[index], index)));
        return new DressingDoc { Props = props };
    }

    public static string SerializeProp(PlacedProp prop) => JsonSerializer.Serialize(prop, Options);

    /// <summary>One prop, parsed and upgraded. Throws <see cref="DressingParseException"/> naming the field
    /// that did not parse, rather than returning null for every kind of failure alike.</summary>
    public static PlacedProp DeserializeProp(string json)
    {
        var node = ParseNode(json, "the prop");
        return ParseProp(node, Label(node, index: null));
    }

    private static JsonNode? ParseNode(string json, string subject)
    {
        try { return Upgraded(json); }
        catch (JsonException ex) { throw new DressingParseException(subject, null, StripPath(ex.Message)); }
    }

    private static PlacedProp ParseProp(JsonNode? node, string subject)
    {
        try
        {
            return JsonSerializer.Deserialize<PlacedProp>(node, Options)
                ?? throw new DressingParseException(subject, null, "read as nothing");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw Explain(subject, ex);
        }
    }

    /// <summary>Names a prop for a refusal: by its own id when it has one, otherwise by its position in the
    /// list — a prop that fails to parse may not have gotten far enough to be identified any other way.</summary>
    private static string Label(JsonNode? node, int? index)
    {
        var id = node is JsonObject obj && obj["id"] is JsonValue value && value.TryGetValue<string>(out var text)
            && text.Length > 0 ? text : null;
        return (id, index) switch
        {
            ({ } named, { } at) => $"prop '{named}' (#{at})",
            ({ } named, null) => $"prop '{named}'",
            (null, { } at) => $"prop #{at}",
            (null, null) => "the prop",
        };
    }

    private static readonly Regex UnrecognizedKind = new("unrecognized type discriminator id '([^']*)'");
    private static readonly Regex ConvertedTo = new(@"could not be converted to (.+?)\.(?:\s+Path:|$)");
    private static readonly Regex PathOf = new(@"Path:\s*(\S+)");

    /// <summary>Turns a raw <see cref="JsonException"/>/<see cref="NotSupportedException"/> — framework text
    /// naming a CLR type and a JSON pointer — into a message an author can act on: which field, and what was
    /// expected there instead.</summary>
    private static DressingParseException Explain(string subject, Exception ex)
    {
        var pathMatch = PathOf.Match(ex.Message);
        var field = FieldFrom(pathMatch.Success ? pathMatch.Groups[1].Value : null);
        var isMaterial = ex.Message.Contains(nameof(TerrainMaterial));

        var unrecognized = UnrecognizedKind.Match(ex.Message);
        if (unrecognized.Success)
            return new DressingParseException(subject, KindField(field),
                $"names kind '{unrecognized.Groups[1].Value}', which is not one of {KnownKinds(isMaterial)}");

        if (ex.Message.Contains("must specify a type discriminator"))
            return new DressingParseException(subject, KindField(field),
                $"does not name a kind — expected one of {KnownKinds(isMaterial)}");

        var converted = ConvertedTo.Match(ex.Message);
        if (converted.Success)
            return new DressingParseException(subject, field, $"could not be read — expected {Friendly(converted.Groups[1].Value)}");

        return new DressingParseException(subject, field, StripPath(ex.Message));
    }

    private static string? FieldFrom(string? path)
    {
        if (string.IsNullOrEmpty(path) || path == "$") return null;
        var trimmed = path.TrimStart('$', '.');
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string KindField(string? field) => field is null ? "kind" : $"{field}.kind";

    private static string KnownKinds(bool material) => string.Join(", ", material ? MaterialKinds : PropKinds);

    private static readonly string[] PropKinds = KindsOf<PlacedProp>();
    private static readonly string[] MaterialKinds = KindsOf<TerrainMaterial>();

    private static string[] KindsOf<T>() => typeof(T)
        .GetCustomAttributes(typeof(JsonDerivedTypeAttribute), inherit: false)
        .Cast<JsonDerivedTypeAttribute>()
        .Select(attribute => (string)attribute.TypeDiscriminator!)
        .ToArray();

    private static string Friendly(string clrTypeName) => clrTypeName switch
    {
        "System.Double" or "System.Single" or "System.Decimal" => "a number",
        "System.Int32" or "System.Int64" or "System.Int16" or "System.Byte" or "System.UInt32" => "a whole number",
        "System.Boolean" => "true or false",
        "System.String" => "text",
        _ when clrTypeName.Contains("Double[]") => "a list of [x, z] points",
        _ => clrTypeName,
    };

    private static string Describe(JsonNode? node) => node switch
    {
        null => "empty",
        JsonArray => "a list",
        JsonValue value when value.TryGetValue<string>(out _) => "text",
        JsonValue value when value.TryGetValue<bool>(out _) => "true/false",
        JsonValue => "a number",
        _ => "not an object",
    };

    private static string StripPath(string message)
    {
        var at = message.IndexOf(" Path:", StringComparison.Ordinal);
        return at < 0 ? message : message[..at].TrimEnd();
    }

    // The grid a cobbled path was tiled by, and the salt its sites were hashed with — kept so a stored one
    // upgrades onto the same patches it already had rather than onto a different road of the same idea.
    private const int CobbleGrid = 3;
    private const uint CobbleSalt = 29;

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
    ///
    /// <para><b>A house's <c>points</c> → <c>wings</c>.</b> A placed building was exactly two corners and is
    /// now a list of one or more touching rectangles (`G177`); a stored one carries a single wing, so its own
    /// two corners become that wing's own two corners, wrapped in the list they were always the only entry
    /// of.</para>
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

        if (kind == "house" && prop["wings"] is null && prop["points"] is JsonArray points)
        {
            prop["wings"] = new JsonArray(points.DeepClone());
            prop.Remove("points");
        }

        // A wing was two corners and is now a rectangle plus what it states about itself, so the older shape —
        // the corner pair on its own — becomes an entry that states nothing and therefore wears the building's
        // own everything, which is exactly what it meant before there was anything else to say.
        if (kind == "house" && prop["wings"] is JsonArray wings)
            for (var index = 0; index < wings.Count; index++)
                if (wings[index] is JsonArray corners)
                    wings[index] = new JsonObject { ["corners"] = corners.DeepClone() };

        // A house prop carries a whole style, so it is the second place a style is stored and has to be read
        // forward by the same walk the standalone snapshot is.
        if (kind == "house") HouseStyleJson.Upgrade(prop["style"]);
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
