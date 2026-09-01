using System.Text.Json.Nodes;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>
/// The theme JSON's property names and per-kind defaults, in one place. The theme node <b>is</b> the wire
/// format the painter deserializes, so the editor writes it directly rather than through a second model that
/// could drift; naming every field once here is what keeps that safe. The <c>kind</c> and bucket vocabularies
/// themselves are <see cref="MaterialKind"/> / <see cref="ThemeBuckets"/> in the contracts, shared with the
/// column that stores them and the HTTP surface that carries them.
/// </summary>
public static class ThemeFields
{
    // material
    public const string Kind = "kind";
    public const string Id = "id";
    public const string Data = "data";
    public const string BlockId = "blockId";
    public const string Neutral = "neutral";
    /// <summary>A layered material's own <c>BandStack</c>: the bands under a name of their own, plus what the
    /// stack does where they run out. The bands do not sit on the material, because where they end is the
    /// stack's statement and along which distance they are read is the material's.</summary>
    public const string Stack = "stack";
    /// <summary>Which distance a stack is read along — <c>depth</c>, <c>inward</c> or <c>height</c>.</summary>
    public const string Axis = "axis";
    /// <summary>What shows where a stack claims nothing: past its last band under <c>handOver</c>, and off the
    /// footprint entirely on the inward axis. Absent is stone.</summary>
    public const string Beyond = "beyond";
    /// <summary>What a stack answers past its last band — <c>repeat</c> or <c>handOver</c>.</summary>
    public const string Ending = "ending";
    /// <summary>The world Y a height stack's first band sits at. Read on no other axis.</summary>
    public const string From = "from";
    /// <summary>The path from a layered material to its bands. A path rather than a field because the list is
    /// one level down, and <c>bands</c> alone names two lists — a voronoi's, measured in depth, and a stack's,
    /// measured in thickness.</summary>
    public const string StackBands = "stack/bands";
    public const string Material = "material";
    public const string Thickness = "thickness";
    public const string Palette = "palette";
    public const string Stops = "stops";
    public const string Runs = "runs";
    public const string Width = "width";
    public const string Seed = "seed";
    public const string CellSize = "cellSize";
    /// <summary>A voronoi's ordered bands, measured inward from the cell boundary — band 0 draws the grid, the
    /// last takes the middle. Each entry is a material plus its <see cref="Depth"/> in blocks.</summary>
    public const string Bands = "bands";
    /// <summary>How far a cell pattern's sites may sit from the middle of their grid square, 0–100%.</summary>
    public const string Jitter = "jitter";
    /// <summary>How many blocks a cell pattern's boundaries wander — what makes its patches organic rather than
    /// the straight-edged diagram a voronoi draws.</summary>
    public const string Warp = "warp";
    public const string Scale = "scale";
    public const string Octaves = "octaves";
    /// <summary>How many cells along the wall a diagonal's stripes move for every course up — 1 is 45°, larger
    /// lays them flatter, negative leans them the other way, 0 is a plain vertical run.</summary>
    public const string Slope = "slope";
    /// <summary>The side of one checkerboard square, in blocks.</summary>
    public const string Size = "size";
    /// <summary>How sharp a boundary turn must be, in degrees, for a wall frame to ink it as a corner. Also
    /// sets how far the ink wraps round one, since the measured turn ramps to a vertex rather than switching
    /// on at it. A wall frame's own courses are <see cref="Thickness"/>, its panel <see cref="Fill"/>.</summary>
    public const string Angle = "angle";
    public const string Edge = "edge";

    /// <summary>A checkerboard's two squares, by the parity they take.</summary>
    public const string Even = "even";
    public const string Odd = "odd";
    /// <summary>The vertical period of an area pattern's field, in blocks — 0 keeps it flat, so every block in
    /// a column takes the same answer and the pattern shows only on the ground.</summary>
    public const string Rise = "rise";

    // theme — the four bucket properties are named by the bucket ids themselves
    public const string Bedrock = "bedrock";
    public const string Relative = "relative";
    public const string Value = "value";
    /// <summary>Which edges the rim caps — one of <see cref="RimEdgeModes"/>. Absent means the default.</summary>
    public const string RimEdges = "rimEdges";
    public const string WallOnTerrainFaces = "wallOnTerrainFaces";
    public const string Rim = ThemeBuckets.Rim;
    public const string Surface = ThemeBuckets.Surface;
    public const string Wall = ThemeBuckets.Wall;
    public const string WallEnabled = "wallEnabled";
    public const string Fill = ThemeBuckets.Fill;
    /// <summary>How deep a claim runs: courses for a theme's top band, blocks inward for a voronoi's band.</summary>
    public const string Depth = "depth";
    public const string Enabled = "enabled";

    /// <summary>
    /// What a fresh field starts at, where the schema cannot say.
    ///
    /// <para>The published schema names every field and its type, and states a default for the optional ones —
    /// but a <b>required</b> field has no default to state, and a list's default is empty, which is the one
    /// starting point that teaches an author nothing. A pattern arrives with entries whose blocks are far apart
    /// in the palette, because one entry, or two that happen to share a colour as stone and cobblestone do,
    /// renders flat and reads as broken.</para>
    ///
    /// <para><b>Keyed by field name rather than by kind</b>, so a kind added server-side that reuses
    /// <c>seed</c>, <c>scale</c> or <c>stops</c> starts sensibly without being named here at all. The per-kind
    /// table below it is for the handful where the field name is not enough: an <c>id</c> is a stone block on a
    /// solid and a log on the two kinds that lay one.</para>
    /// </summary>
    private static readonly Dictionary<string, Func<JsonNode>> Starters = new(StringComparer.Ordinal)
    {
        [Id] = () => JsonValue.Create(1),
        [Data] = () => JsonValue.Create(0),
        [BlockId] = () => JsonValue.Create(159),
        [Seed] = () => JsonValue.Create(1),
        [CellSize] = () => JsonValue.Create(10),
        [Scale] = () => JsonValue.Create(16),
        [Octaves] = () => JsonValue.Create(3),
        [Jitter] = () => JsonValue.Create(50),
        [Warp] = () => JsonValue.Create(4),
        [Size] = () => JsonValue.Create(1),
        [Angle] = () => JsonValue.Create(45),
        [Thickness] = () => JsonValue.Create(1),
        [Slope] = () => JsonValue.Create(1),
        [Rise] = () => JsonValue.Create(0),

        // The nested materials, each started at what shows the part it plays.
        [Neutral] = () => Solid(159, 8),
        [Even] = () => Solid(155),
        [Odd] = () => Solid(159, 15),
        [Edge] = () => Solid(159, 15),
        [Fill] = () => Solid(155),

        // The lists. A grid line, a thin course just inside it, then the body — the shape a voronoi has to
        // arrive in to read as cells at all; four stops because the point of a ramp is that the ends are
        // accents, which one boundary cannot show; grass over two dirt for a stack, read down the column.
        [Stack] = () => NewStack(Layer(Solid(2), 1), Layer(Solid(3), 2)),
        [Bands] = () => new JsonArray(Band(Solid(155), 1), Band(Solid(3), 2), Band(Solid(1), 1)),
        [Palette] = () => new JsonArray(Solid(1), Solid(24), Solid(3, 1)),
        [Stops] = () => new JsonArray(Solid(1), Solid(2), Solid(3), Solid(24)),
        [Runs] = () => new JsonArray(Stripe(Solid(155), 3), Stripe(Solid(159, 8), 2)),
    };

    /// <summary>Where a field name alone is not enough. Acacia laid upright against acacia on its side is the
    /// timbering the corpus houses use, so it is what a fresh log pattern offers — and a laid log is a log
    /// however the field is named the same as a solid's.</summary>
    private static readonly Dictionary<(string Kind, string Field), Func<JsonNode>> PerKind = new()
    {
        [(MaterialKind.LogChecker, Id)] = () => JsonValue.Create(162),
        [(MaterialKind.LaidLog, Id)] = () => JsonValue.Create(17),
    };

    /// <summary>What this field starts at on this kind, or null to take whatever the schema states.</summary>
    public static JsonNode? Starter(string kind, string field, string type)
    {
        if (PerKind.TryGetValue((kind, field), out var perKind)) return perKind();
        if (Starters.TryGetValue(field, out var starter)) return starter();
        // A list the editor has no starter for is still a list: empty beats absent, since the control can add
        // to one and cannot conjure the field.
        return type.EndsWith("[]", StringComparison.Ordinal) ? new JsonArray() : null;
    }

    /// <summary>A fresh entry for one of a material's child lists — a bare material for a pattern's palette or
    /// stops, a material plus its extent for a layer stack, a wall run or a voronoi band.</summary>
    public static JsonNode NewEntry(string field) => Entry(field, Solid(1));

    /// <summary>The same entry around a material already chosen — what filling a list from a family builds, one
    /// entry per block. The extent is the list's own default, since a family names the blocks and not how far
    /// each one reaches.</summary>
    public static JsonNode Entry(string field, JsonObject material) => field switch
    {
        StackBands => Layer(material, 1),
        Runs => Stripe(material, 2),
        Bands => Band(material, 1),
        _ => material,
    };

    /// <summary>A fresh field pattern — the three share every knob and differ only in how the field is bent, so
    /// they start from the same numbers and the same four-stop ramp. Four stops rather than two because the
    /// point of a ramp is that the ends are accents, which one boundary cannot show.</summary>
    private static JsonObject Field(string kind) => new()
    {
        [Kind] = kind,
        [Seed] = 1,
        [Scale] = 16,
        [Octaves] = 3,
        [Stops] = new JsonArray(Solid(1), Solid(2), Solid(3), Solid(24)),
        [Rise] = 0,
    };

    /// <summary>A solid-block material node — the leaf every other kind bottoms out in.</summary>
    public static JsonObject Solid(int id, int data = 0)
        => new() { [Kind] = MaterialKind.Solid, [Id] = id, [Data] = data };

    /// <summary>A stack of the given bands, claiming everything past the last of them.</summary>
    public static JsonObject NewStack(params JsonNode[] bands) =>
        new() { [Ending] = BandEndings.Repeat, [ThemeFields.Bands] = new JsonArray(bands) };

    private static JsonObject Layer(JsonNode material, int thickness) => new() { [Material] = material, [Thickness] = thickness };
    private static JsonObject Stripe(JsonNode material, int width) => new() { [Material] = material, [Width] = width };
    private static JsonObject Band(JsonNode material, int depth) => new() { [Material] = material, [Depth] = depth };
}

/// <summary>
/// What a themeable bucket is, in the words an authoring surface shows. The sketch's theme editor lays the four
/// out as sections and the library's theme composer lays them out as bindings, but they describe the same four
/// buckets — so they describe them the same way, from here.
/// </summary>
/// <param name="Id">The bucket's wire id (<see cref="ThemeBuckets"/>).</param>
/// <param name="Title">Its heading.</param>
/// <param name="Blurb">What it claims, in one sentence.</param>
/// <param name="FallsTo">Where its blocks go when it is switched off.</param>
/// <param name="CanDisable">Whether it may be switched off at all — the fill is the base and never can be.</param>
public sealed record ThemeBucketInfo(string Id, string Title, string Blurb, string FallsTo, bool CanDisable)
{
    /// <summary>Whether the bucket claims a configurable number of top courses.</summary>
    public bool HasDepth => ThemeBuckets.HasDepth(Id);

    /// <summary>The four buckets in the order an editor lays them out — the cap, the interior stack under it,
    /// the riser it sits on, then the body everything else falls to.</summary>
    public static readonly IReadOnlyList<ThemeBucketInfo> All =
    [
        new(ThemeBuckets.Rim, "Rim",
            "The cap on the top course of every edge column — what the ground reads as from across the void.",
            "the surface", CanDisable: true),
        new(ThemeBuckets.Surface, "Surface",
            "The stack finishing the top of interior columns, claimed downward — grass over two dirt.",
            "the fill", CanDisable: true),
        new(ThemeBuckets.Wall, "Wall",
            "The exposed riser under the rim, down to the shallowest drop. A team tint here is what makes a team's ground read as theirs.",
            "the fill", CanDisable: true),
        new(ThemeBuckets.Fill, "Fill",
            "Every block no other bucket claimed — the body of the terrain, under the surface and behind the wall.",
            "nothing", CanDisable: false),
    ];

    public static ThemeBucketInfo Of(string bucket) => All.First(info => info.Id == bucket);
}

/// <summary>Reading and writing a wire-format JSON node without a second model of it: every accessor tolerates a
/// missing or wrong-typed property and answers the caller's default, so a hand-edited theme can never throw
/// the editor — it simply shows the default until the field is set.</summary>
public static class JsonEdit
{
    public static string KindOf(JsonObject? node)
        => node?[ThemeFields.Kind]?.GetValue<string>() ?? MaterialKind.Solid;

    public static int Int(JsonObject? node, string field, int fallback)
    {
        if (node?[field] is not JsonValue value) return fallback;
        return value.TryGetValue<int>(out var i) ? i
            : value.TryGetValue<double>(out var d) ? (int)d
            : fallback;
    }

    public static bool Bool(JsonObject? node, string field, bool fallback)
        => node?[field] is JsonValue value && value.TryGetValue<bool>(out var b) ? b : fallback;

    public static double Double(JsonObject? node, string field, double fallback)
    {
        if (node?[field] is not JsonValue value) return fallback;
        return value.TryGetValue<double>(out var d) ? d
            : value.TryGetValue<int>(out var i) ? i
            : fallback;
    }

    public static string Text(JsonObject? node, string field, string fallback)
        => node?[field] is JsonValue value && value.TryGetValue<string>(out var s) ? s : fallback;

    public static void Set(JsonObject node, string field, int value) => node[field] = value;
    public static void Set(JsonObject node, string field, bool value) => node[field] = value;
    public static void Set(JsonObject node, string field, double value) => node[field] = value;
    public static void Set(JsonObject node, string field, string value) => node[field] = value;

    /// <summary>Add or remove a child object — how a part of a recipe is switched on and off, since the pass
    /// reads an absent part as "grow none of this" rather than needing a flag of its own.</summary>
    public static void Toggle(JsonObject node, string field, Func<JsonObject> ifAdding)
    {
        if (node[field] is JsonObject) node.Remove(field);
        else node[field] = ifAdding();
    }

    /// <summary>Whether a node carries a child object at all.</summary>
    public static bool Has(JsonObject? node, string field) => node?[field] is JsonObject;

    /// <summary>A string array's members, as a set the caller can test and toggle against.</summary>
    public static HashSet<string> Texts(JsonObject? node, string field)
        => node?[field] is JsonArray array
            ? [.. array.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>()]
            : [];

    public static void SetTexts(JsonObject node, string field, IEnumerable<string> values)
        => node[field] = new JsonArray([.. values.Select(v => (JsonNode)JsonValue.Create(v)!)]);

    /// <summary>Replace a child, detaching the incoming node first — a <see cref="JsonNode"/> may have only
    /// one parent, so anything reused has to be cloned.</summary>
    public static void SetChild(JsonObject node, string field, JsonNode child)
        => node[field] = child.DeepClone();

    /// <summary>
    /// Make <paramref name="node"/> hold exactly what <paramref name="material"/> holds, in place — the whole
    /// material swapped rather than merged, so no field the incoming kind cannot read is left behind to be
    /// silently dropped and reappear if the author switches back.
    ///
    /// <para>In place, because the editors bind to the node itself: the tree above it holds this object, and
    /// handing back a new one would leave the parent pointing at the old. Each value is detached from its
    /// source first, since a <see cref="JsonNode"/> may have only one parent.</para>
    /// </summary>
    public static void Replace(JsonObject node, JsonObject material)
    {
        node.Clear();
        foreach (var (name, value) in material) node[name] = value?.DeepClone();
    }

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

    /// <summary>The array a slash-separated path names, creating each step that is missing — so
    /// <c>stack/bands</c> reaches a layered material's bands the same way <c>bands</c> reaches a voronoi's,
    /// and a caller never has to know which of the two it is holding.</summary>
    public static JsonArray ArrayAt(JsonObject? node, string path)
    {
        if (node is null) return [];
        var steps = path.Split('/');
        var owner = node;
        for (var i = 0; i < steps.Length - 1; i++) owner = Child(owner, steps[i], () => []);
        return Array(owner, steps[^1]);
    }
}

/// <summary>
/// What each part of a room shell is, in the words the editor offers it in — the sibling of
/// <see cref="ThemeBucketInfo"/> for the room-style composer.
/// </summary>
/// <param name="Id">The part's wire id (<see cref="RoomParts"/>).</param>
/// <param name="Title">Its heading.</param>
/// <param name="Blurb">What it is and which way its stack is read, in one sentence.</param>
/// <param name="ExtentLabel">What that part's extent means — a floor's depth is not a wall's height — or null
/// for a part that takes one material rather than a stack, and so has no extent to state.</param>
public sealed record RoomPartInfo(string Id, string Title, string Blurb, string? ExtentLabel = null)
{
    /// <summary>Whether the part is a stack of courses with a depth of its own, as against one material read
    /// straight through. A post is a post all the way up, and so is a sill, a verge and each zone of the
    /// floor's top course — for those the editor offers one picker instead of a stack.</summary>
    public bool Stacked => ExtentLabel is not null;

    /// <summary>The parts a shell is built from, bottom-up, in the order it is stamped.
    ///
    /// <para>The floor and the walls stack; the <b>roof does not</b>. A stack counts courses from its part's own
    /// base, which is a thing a wall has and a slope has not: how deep a roof runs at a cell is however many
    /// courses close the step down to its neighbour, so the roof is one pass and there is no second course for
    /// a stack to name. It kept an extent and a stack for a while and neither was ever read — a roof authored
    /// with three courses built exactly the roof its first course described.</para></summary>
    public static readonly IReadOnlyList<RoomPartInfo> All =
    [
        new(RoomParts.Floor, "Floor",
            "Read downward from the course players stand on, so a deeper floor digs into the platform rather than lifting the room off it. The wool pad is stamped over it and is never a course.",
            "Courses deep"),
        new(RoomParts.Wall, "Walls",
            "The perimeter ring, read upward from the floor. A coloured course takes the room's own colour, and a course of air is the light slit. The last course repeats, so a taller wall grows in whatever tops it.",
            "Courses tall"),
        new(RoomParts.Roof, "Roof",
            "The plane over the walls — one material, not a stack: how deep it runs at a cell is whatever closes the step down to its neighbour. Its hole is measured on the shell it covers, so an overhanging eave does not move it."),
    ];

    /// <summary>The floor's top course divided across the room. These are zones rather than courses because
    /// they divide the floor in <em>plan</em> while its stack divides it in depth — and they are here rather
    /// than inside a material because a material resolves from the cell's own coordinates and cannot know
    /// where the walls are.</summary>
    public static readonly IReadOnlyList<RoomPartInfo> FloorZones =
    [
        new(RoomParts.Field, "Field",
            "The open floor between the border and the inlay. Unbound, the floor part's own top course shows through."),
        new(RoomParts.Border, "Border",
            "A ring hugging the walls, as wide as the border width below."),
        new(RoomParts.Inlay, "Inlay",
            "A plate centred in the room — a hearth, a rug, a floor in the room's own colour."),
    ];

    /// <summary>What a house has that a plain shell does not: framed corners, a footing, and trim along the
    /// roof's edge. Each takes one material.</summary>
    public static readonly IReadOnlyList<RoomPartInfo> Trim =
    [
        new(RoomParts.Post, "Corner posts",
            "The four columns the walls run between. Unbound, the corners are wall like the rest of the building — which is what a plain shell is."),
        new(RoomParts.Sill, "Sill",
            "The course the walls stand on, laid one block proud of them on every side, so the building meets the ground on a footing instead of stopping dead at it."),
        new(RoomParts.Verge, "Verge",
            "The roof's own border — its eave course and its two verges, and the ridge cap where the roof wears one. Unbound, it is the roof's material."),
        new(RoomParts.Gable, "Gable face",
            "The triangle a sloped roof leaves standing at each end. Unbound, it is the wall's top course carried up — a wall that bands as it rises has no band left to give it, since the courses run out at the wall's top."),
        new(RoomParts.Deck, "Deck",
            "The plate this storey stands on, infilled across its interior at a course the walls already span. Unbound, it is the house floor's own top material. The ground storey's deck is the building's floor, and the topmost storey has nothing over it — the roof is what closes that one. Its border, field and inlay are this storey's own, because the plate is its floor rather than the ceiling of the one below."),
    ];

    public static RoomPartInfo Of(string part)
        => All.Concat(FloorZones).Concat(Trim).First(info => info.Id == part);
}

/// <summary>
/// What a house part's editor offers beyond its identity: which of its pieces take a course stack, which take
/// a single material, and what its sample building shows. The identity itself — route, title, glyph, blurb —
/// is the part's <see cref="LibraryKind"/>, so a roof is named in one place.
/// </summary>
public sealed record PartKindInfo(
    LibraryKind Kind, string KnobsTitle, string FigureNote,
    IReadOnlyList<RoomPartInfo> Stacked, IReadOnlyList<RoomPartInfo> Single)
{
    public static readonly IReadOnlyList<PartKindInfo> All =
    [
        // Every one of a roof's three pieces is a single material, and none of them stacks. A course stack
        // counts upward from its part's own base, which a wall has and a roof does not: a slope's depth at a
        // cell is however many courses close the step down to its neighbour, so there is no second course for
        // a stack to name. The body is one pass, the verge is one, the gable face is one.
        new(LibraryKinds.Roofs, "Shape",
            "The roof is drawn on the least wall that can carry its own eave.",
            [],
            [RoomPartInfo.Of(RoomParts.Roof), RoomPartInfo.Of(RoomParts.Verge), RoomPartInfo.Of(RoomParts.Gable)]),

        new(LibraryKinds.Storeys, "The room",
            "The storey is drawn as the one-storey building it makes — or, where it names a ceiling, as two of itself, since the slab it closes with only exists under something.",
            [RoomPartInfo.Of(RoomParts.Wall)],
            [RoomPartInfo.Of(RoomParts.Post), RoomPartInfo.Of(RoomParts.Deck),
             RoomPartInfo.Of(RoomParts.Field), RoomPartInfo.Of(RoomParts.Border),
             RoomPartInfo.Of(RoomParts.Inlay)]),

        new(LibraryKinds.Porches, "The deck",
            "The porch fronts a plain gabled building.",
            [], []),
    ];

    public static PartKindInfo Of(string? slug)
        => All.FirstOrDefault(part => part.Kind.Slug == slug) ?? All[0];
}

/// <summary>
/// How a material holds other materials: which field a kind keeps its entries in, what extent each entry
/// claims, and which single children it names. Stated once, so the form that edits a nest and the outline
/// that walks it cannot disagree about what a nest is.
/// </summary>
public static class MaterialTree
{
    /// <summary>The field a kind carries its entry list in, with the extent each entry claims — null where
    /// the entries are bare materials.</summary>
    /// <summary>The word a nested material is offered under. The schema says <em>which</em> fields are nested
    /// materials — a field typed <c>material</c> is one — and cannot say what to call them, because "Light
    /// square" against "Dark square" is the editor telling an author which half of the checker they are
    /// editing. A field with no word here is offered under its own name.</summary>
    public static string ChildLabel(string field) => field switch
    {
        ThemeFields.Neutral => "Neutral land",
        ThemeFields.Even => "Light square",
        ThemeFields.Odd => "Dark square",
        ThemeFields.Edge => "Frame",
        ThemeFields.Fill => "Panel",
        ThemeFields.Beyond => "Beyond the stack",
        _ => field,
    };

    /// <summary>What one entry of a list is called, by where it sits. A voronoi band is named for what it
    /// does — the first draws the grid, the last takes the middle — and every other list counts.</summary>
    public static string EntryLabel(string field, int index, int count) => field switch
    {
        ThemeFields.Bands when index == 0 => "Grid line",
        ThemeFields.Bands when index == count - 1 => "Middle",
        ThemeFields.Bands => $"Band {index}",
        ThemeFields.StackBands => $"Band {index + 1}",
        ThemeFields.Runs => $"Stripe {index + 1}",
        ThemeFields.Stops => $"Stop {index + 1}",
        _ => $"Patch {index + 1}",
    };

    /// <summary>Every material in a tree, outermost first, each with the path that reaches it and how deep it
    /// sits. The root's path is the empty string.
    ///
    /// <para>The shape it walks is the schema's — which fields are nested materials and which is the list —
    /// so a kind added server-side is walked without this being taught about it.</para></summary>
    public static IEnumerable<(string Path, string Label, JsonObject Node, int Depth)> Walk(
        JsonObject root, MaterialSchema schema, string label = "Material")
    {
        yield return ("", label, root, 0);
        foreach (var found in Below(root, schema, "", 1)) yield return found;
    }

    private static IEnumerable<(string, string, JsonObject, int)> Below(
        JsonObject node, MaterialSchema schema, string path, int depth)
    {
        var kind = JsonEdit.KindOf(node);

        foreach (var field in schema.ChildrenOf(kind))
        {
            if (node[field] is not JsonObject child) continue;
            var childPath = Join(path, field);
            yield return (childPath, ChildLabel(field), child, depth);
            foreach (var found in Below(child, schema, childPath, depth + 1)) yield return found;
        }

        if (schema.ListOf(kind) is not { } list) yield break;
        var array = JsonEdit.ArrayAt(node, list.Path);
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonNode entry) continue;
            if (Material(JsonEdit.AsObject(entry)) is not { } material) continue;
            var entryPath = Join(path, $"{list.Path}/{i}");
            yield return (entryPath, EntryLabel(list.Path, i, array.Count), material, depth);
            foreach (var found in Below(material, schema, entryPath, depth + 1)) yield return found;
        }
    }

    /// <summary>The material a path names, or null where the path no longer reaches one — which is what an
    /// outline row selected before its list was shortened becomes.</summary>
    public static JsonObject? At(JsonObject root, string path)
    {
        if (string.IsNullOrEmpty(path)) return root;
        var node = root;
        var steps = path.Split('/');
        for (var i = 0; i < steps.Length && node is not null; i++)
        {
            var field = steps[i];
            if (i + 1 < steps.Length && int.TryParse(steps[i + 1], out var index))
            {
                var array = JsonEdit.Array(node, field);
                node = index >= 0 && index < array.Count ? Material(JsonEdit.AsObject(array[index])) : null;
                i++;
                continue;
            }
            node = node[field] as JsonObject;
        }
        return node;
    }

    /// <summary>The material an entry carries: the wrapped child where the entry claims an extent, the entry
    /// itself where the list is bare. Read off the entry's own shape rather than off which list it came from —
    /// a wrapper states a <c>material</c> and a bare one states its <c>kind</c>, so this holds for a list the
    /// schema gains without anything here being told about it.</summary>
    private static JsonObject? Material(JsonObject entry)
        => entry[ThemeFields.Material] as JsonObject ?? (entry[ThemeFields.Kind] is not null ? entry : null);

    private static string Join(string path, string step) => path.Length == 0 ? step : $"{path}/{step}";
}
