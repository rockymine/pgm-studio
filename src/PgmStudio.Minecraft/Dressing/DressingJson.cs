using PgmStudio.Domain;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>The dressing a map carries: everything the author placed, in the order they placed it. Stored
/// beside the sketch geometry, because a prop's position is as much a part of the map as a shape's.</summary>
public sealed record DressingDoc
{
    /// <summary>The placements, in the order they were placed. The order is read rather than decorative: the
    /// pass runs a kind at a time and a prop meets the claims of everything placed before it, so where a prop
    /// sits in this list is part of what it is allowed to stand on.</summary>
    public List<PlacedProp> Props { get; init; } = [];

    /// <summary>The recipes this document's placements name, by key — what a tree, a boulder or a building is
    /// made of, stated once however many placements wear it. A library row is pulled in here and the key is
    /// what the placements carry (<see cref="PropStyle"/>).</summary>
    public Dictionary<string, PropStyle> Styles { get; init; } = [];

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
    /// <remarks>The finding names the prop and the field inside it. Fix that field: the usual causes are a <c>kind</c> the reader does not know, a <c>kind</c> missing outright, and a property of the wrong JSON shape.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Request, RuleConcern.Feature)]
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

    /// <summary>The document, with every placement naming its recipe. A document assembled in code carries its
    /// recipes on the placements and no registry; writing it names them, identical recipes collapsing onto one
    /// key, so what is stored is always referenced however it was built.</summary>
    public static string Serialize(DressingDoc doc) => JsonSerializer.Serialize(Named(doc), Options);

    /// <summary>A document whose placements name their recipes. Keys already in the registry are kept, so a
    /// document read and written back keeps the names an author sees.</summary>
    public static DressingDoc Named(DressingDoc doc)
    {
        var styles = new Dictionary<string, PropStyle>(doc.Styles, StringComparer.Ordinal);
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, style) in styles) keys[Body(style)] = key;

        var props = new List<PlacedProp>(doc.Props.Count);
        foreach (var prop in doc.Props)
        {
            var (style, kind, keyed) = RecipeOf(prop);
            if (style is null) { props.Add(prop); continue; }
            var body = Body(style);
            if (!keys.TryGetValue(body, out var key))
            {
                key = Unique(KeyFor(JsonNode.Parse(body) as JsonObject ?? [], kind), null, styles.Keys);
                keys[body] = key;
                styles[key] = style;
            }
            props.Add(keyed(key));
        }
        return doc with { Props = props, Styles = styles };
    }

    private static string Body(PropStyle style) => JsonSerializer.Serialize(style, Options);

    /// <summary>What a placement is made of, what kind of recipe that is, and how to hand it a key — the one
    /// place the three referencing kinds are listed, so a fourth is added here and nowhere else.</summary>
    private static (PropStyle? Style, string Kind, Func<string, PlacedProp> Keyed) RecipeOf(PlacedProp prop)
        => prop switch
        {
            TreeProp tree => (tree.Style, "tree", key => tree with { StyleKey = key }),
            BoulderProp boulder => (boulder.Style, "boulder", key => boulder with { StyleKey = key }),
            HouseProp house => (new HouseStyleRef { Shell = house.Style }, "house", key => house with { StyleKey = key }),
            _ => (null, "", _ => prop),
        };

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

        var styles = ParseStyles(root["styles"]);
        var props = new List<PlacedProp>(propsArray.Count);
        for (var index = 0; index < propsArray.Count; index++)
            props.Add(Resolved(ParseProp(propsArray[index], Label(propsArray[index], index)),
                               styles, Label(propsArray[index], index)));
        return new DressingDoc { Props = props, Styles = styles };
    }

    /// <summary>The document's recipe registry. Absent is a document whose placements name nothing, which is
    /// every document written before recipes were named and every one carrying only drawn props.</summary>
    private static Dictionary<string, PropStyle> ParseStyles(JsonNode? node)
    {
        if (node is null) return [];
        if (node is not JsonObject entries)
            throw new DressingParseException("the document", "styles", $"is {Describe(node)}, not a table of recipes");

        var styles = new Dictionary<string, PropStyle>(StringComparer.Ordinal);
        foreach (var (key, value) in entries)
        {
            try
            {
                if (value.Deserialize<PropStyle>(Options) is { } style) styles[key] = style;
            }
            catch (JsonException ex)
            {
                throw new DressingParseException($"recipe '{key}'", null, StripPath(ex.Message));
            }
        }
        return styles;
    }

    /// <summary>A placement carrying the recipe its key names. A key that names nothing is a refusal rather
    /// than a default: a tree built as a stock oak because its recipe was dropped is a map that differs from
    /// the one an author drew, and nothing downstream could tell.</summary>
    private static PlacedProp Resolved(
        PlacedProp prop, Dictionary<string, PropStyle> styles, string subject) => prop switch
    {
        TreeProp tree => tree with { Style = Recipe<TreeStyle>(styles, tree.StyleKey, subject) },
        BoulderProp boulder => boulder with { Style = Recipe<BoulderStyle>(styles, boulder.StyleKey, subject) },
        HouseProp house => house with { Style = Recipe<HouseStyleRef>(styles, house.StyleKey, subject).Shell },
        _ => prop,
    };

    private static TStyle Recipe<TStyle>(
        Dictionary<string, PropStyle> styles, string key, string subject) where TStyle : PropStyle, new()
    {
        if (key.Length == 0) return new TStyle();
        if (!styles.TryGetValue(key, out var style))
            throw new DressingParseException(subject, "style", $"names the recipe '{key}', which the document does not state");
        if (style is not TStyle typed)
            throw new DressingParseException(subject, "style", $"names the recipe '{key}', which is a {StyleWord(style)} recipe");
        return typed;
    }

    private static string StyleWord(PropStyle style) => style switch
    {
        TreeStyle => "tree", BoulderStyle => "boulder", HouseStyleRef => "house", _ => "different",
    };

    public static string SerializeProp(PlacedProp prop) => JsonSerializer.Serialize(prop, Options);

    /// <summary>One recipe as a document states it — what a library row is pulled into a map's registry as.</summary>
    public static string SerializeStyle(PropStyle style) => JsonSerializer.Serialize(style, Options);

    /// <summary>One prop, parsed and upgraded. Throws <see cref="DressingParseException"/> naming the field
    /// that did not parse, rather than returning null for every kind of failure alike.</summary>
    public static PlacedProp DeserializeProp(string json)
    {
        var node = ParseNode(json, "the prop");
        var subject = Label(node, index: null);
        // A prop on its own has no document behind it, so its recipe travels with it: the lift puts it in a
        // registry of one, which is what the preview and the picker cards each hand over.
        var styles = node is JsonObject bare ? ParseStyles(bare["styles"]) : [];
        return Resolved(ParseProp(node, subject), styles, subject);
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
        if (node is JsonObject document) Registered(document);
        return node;
    }

    /// <summary>
    /// Lift every placement's inline recipe into the document's registry, under a key one recipe owns.
    ///
    /// <para>A stored placement states what it is made of on itself, because it was written before a recipe had
    /// a name. Reading it forward names it: identical recipes collapse onto one key, so a board's hundreds of
    /// trees arrive as the few dozen recipes they always were, and the placement is left carrying the key. A
    /// document that already names its recipes has nothing inline to lift and passes through.</para>
    ///
    /// <para>The key is minted from the recipe rather than counted, so re-reading one document twice mints the
    /// same names and a diff between two saves shows what changed rather than a renumbering.</para>
    /// </summary>
    private static void Registered(JsonObject document)
    {
        // A document's list, or the one prop this is: an edited prop arrives on its own and states its recipe
        // inline exactly as a stored placement does.
        var props = document["props"] as JsonArray ?? (document["kind"] is not null ? [document] : null);
        if (props is null) return;
        var styles = document["styles"] as JsonObject;
        var minted = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var node in props)
        {
            if (node is not JsonObject prop) continue;
            var kind = prop["kind"] is JsonValue k && k.TryGetValue<string>(out var name) ? name : null;
            if (kind is not ("tree" or "boulder" or "house")) continue;
            // A prop already naming its recipe is one this has read before; a house's `style` is an object
            // until it is lifted, so the string is what says "already named".
            // A prop already naming its recipe is one this has read before: the key is a string where an
            // unlifted recipe is an object (a house's shell) or a spread of fields (a tree's, a boulder's).
            if (prop["style"] is JsonValue) continue;

            var recipe = Lifted(prop, kind);
            if (recipe is null) continue;
            var body = recipe.ToJsonString();
            if (!minted.TryGetValue(body, out var key))
            {
                key = Unique(KeyFor(recipe, kind), styles, minted.Values);
                minted[body] = key;
                styles ??= [];
                styles[key] = recipe;
            }
            prop["style"] = key;
        }

        if (styles is not null) document["styles"] = styles;
    }

    /// <summary>One placement's inline recipe, taken off it — the fields that say what it is made of, leaving
    /// the ones that say where it stands.</summary>
    private static JsonObject? Lifted(JsonObject prop, string kind)
    {
        if (kind == "house")
            return prop["style"] is JsonObject shell
                ? new JsonObject { ["kind"] = "house", ["shell"] = shell.DeepClone() }
                : new JsonObject { ["kind"] = "house", ["shell"] = new JsonObject() };

        string[] fields = kind == "tree"
            ? ["form", "species", "wood", "height", "stems", "leader", "flow", "branchAngle", "levels", "whorled", "leafSize", "body"]
            : ["form", "size", "rock", "mossy"];

        var recipe = new JsonObject { ["kind"] = kind };
        foreach (var field in fields)
            if (prop[field] is { } value) { recipe[field] = value.DeepClone(); prop.Remove(field); }
        return recipe;
    }

    /// <summary>A recipe's name, read off what it is: a tree by its wood and height, a boulder by its form and
    /// size, a building by its shell's own name where it has one. Readable, because a key is what an author
    /// picks a recipe by once it is in the registry.</summary>
    private static string KeyFor(JsonObject recipe, string kind)
    {
        string Text(string field, string fallback)
            => recipe[field] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : fallback;
        int Number(string field, int fallback)
            => recipe[field] is JsonValue v && v.TryGetValue<double>(out var d) ? (int)Math.Round(d) : fallback;

        if (kind == "house")
            return recipe["shell"]?["name"] is JsonValue n && n.TryGetValue<string>(out var shell) && shell.Length > 0
                ? Slug(shell) : "building";
        if (kind == "boulder") return $"{Text("form", "round")}-{Number("size", 4)}";

        var form = Text("form", "template");
        if (form == "copied")
            return $"copied-{(recipe["body"] is JsonArray body ? body.Count : 0)}";
        var grown = form == "grown";
        var wood = grown ? Text("wood", "oak") : Text("species", "oak");
        return $"{(grown ? "grown-" : "")}{Slug(wood)}-{Number("height", 12)}";
    }

    private static string Slug(string name)
        => new string([.. name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')]).Trim('-');

    /// <summary>The name, numbered where it is taken — two recipes that read the same way are still two
    /// recipes, and the second may not quietly become the first.</summary>
    private static string Unique(string name, JsonObject? styles, IEnumerable<string> taken)
    {
        var used = new HashSet<string>(taken, StringComparer.Ordinal);
        if (styles is not null) foreach (var (key, _) in styles) used.Add(key);
        if (!used.Contains(name)) return name;
        for (var n = 2; ; n++) if (!used.Contains($"{name}-{n}")) return $"{name}-{n}";
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

        // A stored stroke was written under the name of the one reading of it, so the kind is carried forward
        // to the name of the thing itself; a route is declared rather than assumed, which is what the standoff
        // is measured to.
        if (kind == "path") { prop["kind"] = "stroke"; kind = "stroke"; }

        if (kind == "stroke" && prop["pave"] is null && prop["blocks"] is JsonArray blocks && blocks.Count > 0)
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
