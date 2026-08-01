using System.Text.Json.Nodes;

namespace PgmStudio.Client.Features.Plan;

/// <summary>One block the Theme rail's picker offers — the payload of <c>GET /api/terrain/blocks</c>, which
/// serves <c>TerrainPalette.Paintable</c>. <see cref="Hex"/> is the colour the export actually places, so a
/// swatch cannot promise a block a different colour.</summary>
public sealed record PaintBlockDto(int Id, int Data, string Name, string Group, string Hex);

/// <summary>The <c>kind</c> discriminator of every terrain-paint material, with the label the editor offers it
/// under (docs/world-export/terrain-painting.md §3). Const strings so a Razor <c>switch</c> can case on them.</summary>
public static class MaterialKinds
{
    public const string Solid = "solid";
    public const string Layered = "layered";
    public const string TeamTint = "teamTint";
    public const string Voronoi = "voronoi";
    public const string Noise = "noise";
    public const string WallRun = "wallRun";

    /// <summary>The kinds in offer order — plain blocks first, then the composites, then the patterns.</summary>
    public static readonly (string Id, string Name)[] All =
    [
        (Solid, "Solid block"),
        (Layered, "Layer stack"),
        (TeamTint, "Team tint"),
        (Voronoi, "Voronoi patches"),
        (Noise, "Noise ramp"),
        (WallRun, "Wall stripes"),
    ];

    public static string NameOf(string kind) => All.FirstOrDefault(k => k.Id == kind).Name ?? kind;
}

/// <summary>
/// The theme JSON's property names and per-kind defaults, in one place. The theme node <b>is</b> the wire
/// format the painter deserializes, so the editor writes it directly rather than through a second model that
/// could drift; naming every field once here is what keeps that safe.
/// </summary>
public static class ThemeFields
{
    // material
    public const string Kind = "kind";
    public const string Id = "id";
    public const string Data = "data";
    public const string BlockId = "blockId";
    public const string Neutral = "neutral";
    public const string Layers = "layers";
    public const string Material = "material";
    public const string Thickness = "thickness";
    public const string Palette = "palette";
    public const string Stops = "stops";
    public const string Runs = "runs";
    public const string Width = "width";
    public const string Seed = "seed";
    public const string CellSize = "cellSize";
    public const string Scale = "scale";
    public const string Octaves = "octaves";

    // theme
    public const string Bedrock = "bedrock";
    public const string Relative = "relative";
    public const string Value = "value";
    public const string Closed = "closed";
    public const string WallOnTerrainFaces = "wallOnTerrainFaces";
    public const string Rim = "rim";
    public const string Surface = "surface";
    public const string Wall = "wall";
    public const string WallEnabled = "wallEnabled";
    public const string Fill = "fill";
    public const string Depth = "depth";
    public const string Enabled = "enabled";

    /// <summary>A fresh material node of the given kind, with defaults that show what the kind does: a pattern
    /// starts with two entries whose blocks are far apart in the palette, since one entry — or two blocks that
    /// happen to share a colour, as stone and cobblestone do — renders flat and reads as broken.</summary>
    public static JsonObject NewMaterial(string kind) => kind switch
    {
        MaterialKinds.Layered => new JsonObject
        {
            [Kind] = MaterialKinds.Layered,
            [Layers] = new JsonArray(Layer(Solid(2), 1), Layer(Solid(3), 2)),
        },
        MaterialKinds.TeamTint => new JsonObject
        {
            [Kind] = MaterialKinds.TeamTint,
            [BlockId] = 159,
            [Neutral] = Solid(159, 8),
        },
        MaterialKinds.Voronoi => new JsonObject
        {
            [Kind] = MaterialKinds.Voronoi,
            [Seed] = 1,
            [CellSize] = 8,
            [Palette] = new JsonArray(Solid(1), Solid(24)),
        },
        MaterialKinds.Noise => new JsonObject
        {
            [Kind] = MaterialKinds.Noise,
            [Seed] = 1,
            [Scale] = 16,
            [Octaves] = 3,
            [Stops] = new JsonArray(Solid(2), Solid(12)),
        },
        MaterialKinds.WallRun => new JsonObject
        {
            [Kind] = MaterialKinds.WallRun,
            [Runs] = new JsonArray(Stripe(Solid(155), 3), Stripe(Solid(159, 8), 2)),
        },
        _ => Solid(1),
    };

    /// <summary>A fresh entry for one of a material's child lists — a bare material for a pattern's palette or
    /// stops, a material plus its extent for a layer stack or a wall run.</summary>
    public static JsonNode NewEntry(string field) => field switch
    {
        Layers => Layer(Solid(1), 1),
        Runs => Stripe(Solid(1), 2),
        _ => Solid(1),
    };

    /// <summary>A solid-block material node — the leaf every other kind bottoms out in.</summary>
    public static JsonObject Solid(int id, int data = 0)
        => new() { [Kind] = MaterialKinds.Solid, [Id] = id, [Data] = data };

    private static JsonObject Layer(JsonNode material, int thickness) => new() { [Material] = material, [Thickness] = thickness };
    private static JsonObject Stripe(JsonNode material, int width) => new() { [Material] = material, [Width] = width };
}

/// <summary>Reading and writing the theme node without a second model of it: every accessor tolerates a
/// missing or wrong-typed property and answers the caller's default, so a hand-edited theme can never throw
/// the editor — it simply shows the default until the field is set.</summary>
public static class ThemeNode
{
    public static string KindOf(JsonObject? node)
        => node?[ThemeFields.Kind]?.GetValue<string>() ?? MaterialKinds.Solid;

    public static int Int(JsonObject? node, string field, int fallback)
    {
        if (node?[field] is not JsonValue value) return fallback;
        return value.TryGetValue<int>(out var i) ? i
            : value.TryGetValue<double>(out var d) ? (int)d
            : fallback;
    }

    public static bool Bool(JsonObject? node, string field, bool fallback)
        => node?[field] is JsonValue value && value.TryGetValue<bool>(out var b) ? b : fallback;

    public static void Set(JsonObject node, string field, int value) => node[field] = value;
    public static void Set(JsonObject node, string field, bool value) => node[field] = value;

    /// <summary>Replace a child, detaching the incoming node first — a <see cref="JsonNode"/> may have only
    /// one parent, so anything reused has to be cloned.</summary>
    public static void SetChild(JsonObject node, string field, JsonNode child)
        => node[field] = child.DeepClone();

    /// <summary>A child object, created from <paramref name="ifMissing"/> when absent or the wrong shape so the
    /// editor always has something to bind to.</summary>
    public static JsonObject Child(JsonObject? node, string field, Func<JsonObject> ifMissing)
    {
        if (node is null) return ifMissing();
        if (node[field] is JsonObject existing) return existing;
        var created = ifMissing();
        node[field] = created;
        return created;
    }

    /// <summary>A child array, created empty when absent or the wrong shape.</summary>
    public static JsonArray Array(JsonObject? node, string field)
    {
        if (node is null) return [];
        if (node[field] is JsonArray existing) return existing;
        var created = new JsonArray();
        node[field] = created;
        return created;
    }

    /// <summary>An array element as an object — the shape every entry in these lists has.</summary>
    public static JsonObject AsObject(JsonNode? entry) => entry as JsonObject ?? [];
}
