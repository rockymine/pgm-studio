using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The sketch layout wire model — the authoring blob (<c>{setup, layers:[{id, name, base_y,
/// layout:{shapes, groups}}]}</c>) shared by
/// the rasterizer that reads it and the generators that write it. camelCase by default (Web options);
/// snake_case and reserved-word fields carry an explicit name. Kept as the single definition so a
/// generated layout and a hand-drawn one parse through exactly the same shape.
/// </summary>
public sealed class SketchLayout
{
    /// <summary>The board-wide settings the shapes are drawn against: the symmetry the author draws through,
    /// its centre, and the chunk grid.</summary>
    [JsonPropertyName("setup")]  public SketchSetup? Setup { get; set; }

    /// <summary>The drawing, as ordered layers of shapes. Later layers are drawn over earlier ones, and a
    /// shape's operation says whether it adds ground or takes it away. A flat board is a stack of one — the
    /// ground is a layer like any other, which is what <see cref="Stack"/> reads.</summary>
    [JsonPropertyName("layers")] public List<SketchLayer>? Layers { get; set; }

    /// <summary>The terrain themes this map paints with, id → the theme JSON the painter deserializes. Held
    /// as snapshots rather than library ids, so editing a library row never repaints a shipped map. Absent on
    /// a sketch that never picked one, which paints unthemed stone.</summary>
    [JsonPropertyName("themes")]   public Dictionary<string, JsonElement>? Themes { get; set; }

    /// <summary>Which of <see cref="Themes"/> covers every cell no shape's own theme scope claims.</summary>
    [JsonPropertyName("mapTheme")] public string? MapTheme { get; set; }

    /// <summary>The finish of the map's stamped rooms: one style for every wool cage and one for every spawn
    /// cube. Map-wide on purpose — a cage that differed between teams would be a sightline that differed
    /// between teams. Snapshots, like the themes. Absent stamps the built-in shells.</summary>
    [JsonPropertyName("roomStyles")] public SketchRoomStyles? RoomStyles { get; set; }

    /// <summary>What the author put on the map — paths, trees, boulders, areas of cover — each carrying its
    /// own position and its own knobs. A prop is stored as the placement itself rather than as a named recipe,
    /// which is what separates it from a theme. Absent dresses nothing.</summary>
    [JsonPropertyName("dressing")] public JsonElement? Dressing { get; set; }

    /// <summary>Which biome each chunk of the exported world carries — the byte a client reads to tint grass,
    /// leaves and water. Map-wide and answered per chunk, because a biome's tint is blended across a radius
    /// and a region drawn to a finer edge never reaches its own colour there. Absent is plains everywhere,
    /// which is what every board already exported as.
    /// <para>Carried as raw JSON for the reason <see cref="Dressing"/> is: the field's own type is
    /// <c>Minecraft.Painting.BiomeField</c> and this project does not reach that one. The export reads it
    /// through <c>BiomeScope</c>, which does.</para></summary>
    [JsonPropertyName("biome")] public JsonElement? Biome { get; set; }

    /// <summary>Interior elevation, keyed by group id. It rides at the top level rather than inside the
    /// shapes because a plan recompile replaces every shape it produced and a relief is hand work a plan
    /// cannot express, so it is carried across one under its own rule.</summary>
    [JsonPropertyName("relief")] public Dictionary<string, SketchReliefJson>? Relief { get; set; }

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static SketchLayout? Parse(string json) => JsonSerializer.Deserialize<SketchLayout>(json, Json);

    /// <summary>What a body states as a layout, or null where it states none. A body that will not read as
    /// one is the request's own fault (<c>RQ1</c>), answered where the body is read — so a reader only
    /// asking what the board says takes it this way, and <see cref="Parse"/> stays the read that raises.</summary>
    public static SketchLayout? Stated(string json)
    {
        try { return Parse(json); }
        catch (JsonException) { return null; }
    }

    /// <summary>The keys that hold a map's finish rather than its shape: the terrain-theme registry and the
    /// map default, the two bound room shells, every placed prop, and the biome the ground is tinted by. A
    /// plan states where the ground is and nothing about how it looks, so a layout compiled from one carries
    /// none of them.</summary>
    public static readonly string[] FinishKeys = ["themes", "mapTheme", "roomStyles", "dressing", "biome"];

    /// <summary>
    /// A freshly compiled layout with the finish of the layout the map already holds carried onto it.
    /// Geometry always comes from <paramref name="compiledJson"/> — that is the point of recompiling — but a
    /// plain replace would also drop every <see cref="FinishKeys">finish key</see>, because a plan cannot
    /// express one and the compiler therefore never writes one. Rebuilding a themed, dressed map from its
    /// plan would strip it back to bare stone; this keeps the theming while the board underneath it changes.
    /// <para>Only the compile path merges. The sketch editor's own save replaces the blob verbatim, which is
    /// what lets an author delete a theme or the last prop and have the deletion stick.</para>
    /// </summary>
    public static string CarryFinish(string compiledJson, string? storedJson)
    {
        if (string.IsNullOrWhiteSpace(storedJson)) return compiledJson;
        JsonNode? compiled, stored;
        try { compiled = JsonNode.Parse(compiledJson); stored = JsonNode.Parse(storedJson); }
        catch (JsonException) { return compiledJson; }
        if (compiled is not JsonObject target || stored is not JsonObject source) return compiledJson;

        // An explicit null counts as absent, not as a stated empty finish: the compiler's layout is serialized
        // from a typed model that writes every property, so it arrives carrying `"dressing": null` rather than
        // no dressing key at all, and a plain ContainsKey test would decide the finish had already been spoken
        // for and carry nothing.
        var carried = false;
        foreach (var key in FinishKeys)
            if (target[key] is null && source[key] is { } value)
            {
                target[key] = value.DeepClone();
                carried = true;
            }
        return carried ? target.ToJsonString(Json) : compiledJson;
    }

    /// <summary>The group ids a stored relief is bound to that the freshly compiled layout has no group
    /// for. Each one is hand-authored terrain the recompile would silently discard, which is why the compile
    /// path refuses rather than carrying what it can: group identity is derived from the geometry, so a
    /// re-fused group does not merely move — it becomes a different group, and a relief authored against
    /// the old fusion has nowhere correct to land.</summary>
    public static IReadOnlyList<string> OrphanedRelief(string compiledJson, string? storedJson)
    {
        var stored = string.IsNullOrWhiteSpace(storedJson) ? null : Parse(storedJson);
        if (stored?.Relief is not { Count: > 0 } relief) return [];

        var groups = new HashSet<string>(GroupIds(Parse(compiledJson)));
        return relief.Keys.Where(id => !groups.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    /// <summary>The layers a document draws, in draw order — the one place the stack is read, so a gate, a
    /// rasterizer and a theme scope cannot disagree about which shapes a document holds. A document stating
    /// none draws nothing, which is not the same as a document stating an empty layer.
    ///
    /// <para>A layer that named itself keeps its id; one that did not is given its position, so every segment
    /// a rasterize produces can say which layer drew it. Naming here rather than at each reader is what keeps
    /// two readers from inventing different ids for the same unnamed layer.</para></summary>
    public static IReadOnlyList<SketchLayer> Stack(SketchLayout? state)
    {
        if (state?.Layers is not { Count: > 0 } layers) return [];
        for (var i = 0; i < layers.Count; i++)
            if (layers[i].Id is not { Length: > 0 }) layers[i].Id = $"layer{i}";
        return layers;
    }

    /// <summary>Every group id a layout names, across all its layers.</summary>
    public static IEnumerable<string> GroupIds(SketchLayout? state)
    {
        foreach (var layer in Stack(state))
            foreach (var group in layer.Groups)
                if (group.Id is { Length: > 0 } id) yield return id;
    }

    /// <summary>A freshly compiled layout with the stored relief carried onto it. Only call this once
    /// <see cref="OrphanedRelief"/> is empty or the author has accepted the loss — it carries what still
    /// binds and drops the rest.</summary>
    public static string CarryRelief(string compiledJson, string? storedJson)
    {
        var stored = string.IsNullOrWhiteSpace(storedJson) ? null : Parse(storedJson);
        if (stored?.Relief is not { Count: > 0 } relief) return compiledJson;

        JsonNode? node;
        try { node = JsonNode.Parse(compiledJson); } catch (JsonException) { return compiledJson; }
        if (node is not JsonObject target) return compiledJson;

        var groups = new HashSet<string>(GroupIds(Parse(compiledJson)));
        var kept = relief.Where(entry => groups.Contains(entry.Key))
                         .ToDictionary(entry => entry.Key, entry => entry.Value);
        if (kept.Count == 0) return compiledJson;

        target["relief"] = JsonNode.Parse(JsonSerializer.Serialize(kept, Json));
        return target.ToJsonString(Json);
    }

    /// <summary>A freshly compiled layout with every author-stated structural height carried onto it —
    /// the shape-level counterpart to <see cref="CarryRelief"/>. A plan recompile writes a fresh Floor/
    /// BaseHeight for every Role-tagged shape it holds a group's relief against, because that is the only
    /// height a plan-space piece can state before any terrain exists; once the ground is real and an author
    /// corrects that number in the sketch, <see cref="SketchShape.HeightAuthored"/> marks the shape so this
    /// carries its Floor/BaseHeight/AnchorHeights forward instead of letting the recompile hand back the
    /// plan's flat number. Matched by <see cref="SketchShape.IntentRef"/> — the identity that survives a
    /// recompile — not by shape id, which the compiler regenerates every time. A shape with no author-owned
    /// match in the stored layout is untouched, so a piece never corrected keeps tracking the plan.</summary>
    public static string CarryStructuralHeight(string compiledJson, string? storedJson)
    {
        var stored = string.IsNullOrWhiteSpace(storedJson) ? null : Parse(storedJson);
        var authored = StructuralHeights(stored);
        if (authored.Count == 0) return compiledJson;

        JsonNode? node;
        try { node = JsonNode.Parse(compiledJson); } catch (JsonException) { return compiledJson; }
        if (node is not JsonObject target) return compiledJson;

        var carried = false;
        foreach (var shapes in ShapeArrays(target))
            foreach (var shapeNode in shapes)
            {
                if (shapeNode is not JsonObject shape) continue;
                if (shape["role"] is not JsonValue) continue;                 // not a structural annotation
                if (shape["intentRef"]?.GetValue<string?>() is not { } intentRef) continue;
                if (!authored.TryGetValue(intentRef, out var height)) continue;

                shape["floor"] = height.Floor is { } floor ? JsonValue.Create(floor) : null;
                shape["base_height"] = height.BaseHeight is { } baseHeight ? JsonValue.Create(baseHeight) : null;
                shape["anchor_heights"] = height.AnchorHeights is { } anchorHeights
                    ? JsonSerializer.SerializeToNode(anchorHeights, Json) : null;
                shape["height_authored"] = JsonValue.Create(true);
                carried = true;
            }
        return carried ? target.ToJsonString(Json) : compiledJson;
    }

    // Every Role-tagged shape's stated height, keyed by IntentRef, over every layer a stored layout carries
    // — only the ones the author actually corrected.
    private static Dictionary<string, StructuralHeight> StructuralHeights(SketchLayout? state)
    {
        var shapes = Stack(state).SelectMany(layer => layer.Shapes);
        return shapes.Where(s => s.Role is not null && s.HeightAuthored == true && s.IntentRef is { Length: > 0 })
                     .ToDictionary(s => s.IntentRef!, s => new StructuralHeight(s.Floor, s.BaseHeight, s.AnchorHeights));
    }

    // The shape arrays a compiled layout holds its structural annotations in, one per layer.
    private static IEnumerable<JsonArray> ShapeArrays(JsonObject root)
    {
        if (root["layers"] is not JsonArray layers) yield break;
        foreach (var layer in layers)
            if (layer?["layout"]?["shapes"] is JsonArray shapes) yield return shapes;
    }

    private readonly record struct StructuralHeight(double? Floor, double? BaseHeight, double[]? AnchorHeights);
}

/// <summary>
/// The two room-style snapshots a map binds: the shell every wool structure is stamped with, and the one every
/// spawn is. Held as raw JSON because a layout cannot know the stamper's model — the same reason a theme rides
/// here as a <see cref="JsonElement"/> — and read back by the export's scope.
///
/// <para>Each answers in one of <b>three</b> states, which is why these are bare <see cref="JsonElement"/>s
/// rather than nullable ones: an <b>object</b> is the bound style; <b>absent</b> is a sketch that never picked
/// one, which stamps that kind's built-in shell; and an explicit <b>null</b> is no building at all — a pad on
/// open ground, which the stampers have always accepted and nothing could ask them for. A nullable property
/// collapses the last two into one, and the collapse is not harmless: loading a map that bound nothing and
/// saving it again would write the null and turn every one of its rooms into open ground.</para>
/// </summary>
public sealed class SketchRoomStyles
{
    // The wire word stays "cage": it is written into stored layouts by the sketch bridge, and renaming it
    // would leave every bound wool style silently falling back to the built-in one on load.
    /// <summary>The style every wool cage is stamped in, as the stamper's own JSON. A snapshot rather than a
    /// library id, so editing a library row never rebuilds a shipped map's cages.</summary>
    [JsonPropertyName("cage"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Wool { get; set; }

    /// <summary>The style every spawn cube is stamped in, likewise snapshotted.</summary>
    [JsonPropertyName("spawn"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Spawn { get; set; }

    /// <summary>The JSON literal <c>null</c>, as this type spells "no building".</summary>
    public static JsonElement Open { get; } = JsonDocument.Parse("null").RootElement;
}

/// <summary>A stacked slab (S7): its shapes/groups at a Y offset. The whole 2-D editor authors one layer;
/// the rasterizer stacks them — a cell's column is the layer's <c>[floor, top]</c> shifted by <c>base_y</c>.</summary>
/// <summary>One layer of the drawing: the shapes on it, and the height its ground starts at.</summary>
public sealed class SketchLayer
{
    /// <summary>The id and name a compiled plan's single layer takes. A flat board is a stack of one, and
    /// that one is the ground — so the words are stated here rather than spelled at each site that makes
    /// one.</summary>
    public const string GroundId = "ground";
    public const string GroundName = "Ground";

    /// <summary>The single layer a flat board is — the ground, at <c>base_y</c> 0, holding these shapes and
    /// the groups they group into. Every producer of an unstacked document goes through this, so a board
    /// drawn by the plan compiler, the group simplifier and the catalogue grid all state their one layer
    /// the same way.</summary>
    public static SketchLayer Ground(List<SketchShape>? shapes = null, List<SketchGroup>? groups = null) =>
        new()
        {
            Id = GroundId,
            Name = GroundName,
            BaseY = 0,
            Layout = new SketchShapes { Shapes = shapes ?? [], Groups = groups ?? [] },
        };

    /// <summary>What the rest of the document names the layer by.</summary>
    [JsonPropertyName("id")]     public string? Id { get; set; }

    /// <summary>What the layer is called on screen.</summary>
    [JsonPropertyName("name")]   public string? Name { get; set; }

    /// <summary>The height this layer's ground starts at, in blocks.</summary>
    [JsonPropertyName("base_y")] public double BaseY { get; set; }

    /// <summary>What this layer holds: <c>ground</c> — terrain, the stacking model every rule below is written
    /// for — or <c>made</c>, a made thing standing on the ground rather than being it. Absent is ground.
    ///
    /// <para>A made thing is neither terrain nor a dressing prop. It is drawn out of layers because that is
    /// what can hold it, and the word is what keeps the stacking rules off it: a solid sculpture sinking into a
    /// hill has no gap to lose, and a raised arm is not standable ground somebody forgot a stair to.</para></summary>
    [JsonPropertyName("kind")]   public string? Kind { get; set; }

    /// <summary>Which made thing this layer belongs to, where it belongs to one. A sculpture is one thing to
    /// an author and many layers to the rasterizer — a column's runs are split across them — so the name is
    /// what lets a strip draw one row for it, a filter address it, and a drag move every layer of it
    /// together.</summary>
    [JsonPropertyName("prop")]   public string? Prop { get; set; }

    /// <summary>How the layer's floors meet the ground: absent, every shape's <c>floor</c> is the absolute
    /// height it states; <c>ground</c> takes the whole layer down onto the lowest solid column under its own
    /// footprint, so a made thing settles into a slope instead of floating over it or being buried by
    /// it.</summary>
    [JsonPropertyName("seat")]   public string? Seat { get; set; }

    /// <summary>Whether this layer is a made thing rather than terrain.</summary>
    [JsonIgnore] public bool IsMade => string.Equals(Kind, MadeKind, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this layer's floors are taken from the ground under it.</summary>
    [JsonIgnore] public bool SeatsOnGround => string.Equals(Seat, GroundSeat, StringComparison.OrdinalIgnoreCase);

    /// <summary>The two words <see cref="Kind"/> takes, and the one <see cref="Seat"/> does.</summary>
    public const string GroundKind = "ground";
    public const string MadeKind = "made";
    public const string GroundSeat = "ground";

    /// <summary>The shapes drawn on it, and the groups they group into.</summary>
    [JsonPropertyName("layout")] public SketchShapes? Layout { get; set; }

    /// <summary>The shapes on this layer — empty where it states none, so a caller walking a stack never
    /// branches on a layer that was left blank.</summary>
    [JsonIgnore] public List<SketchShape> Shapes => Layout?.Shapes ?? [];

    /// <summary>The groups this layer's shapes group into, empty where it states none.</summary>
    [JsonIgnore] public List<SketchGroup> Groups => Layout?.Groups ?? [];
}

/// <summary>The mirror mode + centre that fan a mirroring group's shapes onto their orbit images, plus the
/// optional working bounds the editor frames the canvas to (hand-drawn sketches carry it; the rasterizer
/// ignores it and reads only the centre + mode).</summary>
public sealed class SketchSetup
{
    /// <summary>How a mirroring group's shapes are fanned onto their orbit images — <c>rot_180</c>,
    /// <c>rot_90</c>, <c>mirror_x</c>, <c>mirror_z</c>, or <c>none</c>.</summary>
    [JsonPropertyName("mirror_mode")] public string MirrorMode { get; set; } = "rot_180";

    /// <summary>The point they are fanned about.</summary>
    [JsonPropertyName("center")]      public SketchCenter? Center { get; set; }

    /// <summary>The editor's working bounds.</summary>
    [JsonPropertyName("bbox")]        public SketchBbox? Bbox { get; set; }
}

/// <summary>The point a drawing folds about. Half-integers are ordinary: a fold between two blocks sits on
/// the seam rather than on either.</summary>
public sealed class SketchCenter
{
    /// <summary>Where it folds, east–west.</summary>
    [JsonPropertyName("cx")] public double Cx { get; set; }

    /// <summary>Where it folds, north–south.</summary>
    [JsonPropertyName("cz")] public double Cz { get; set; }
}

/// <summary>The editor's working bounds — the square the canvas fits to on open.</summary>
public sealed class SketchBbox
{
    /// <summary>Its west edge, in blocks.</summary>
    [JsonPropertyName("min_x")] public double MinX { get; set; }

    /// <summary>Its east edge.</summary>
    [JsonPropertyName("max_x")] public double MaxX { get; set; }

    /// <summary>Its north edge.</summary>
    [JsonPropertyName("min_z")] public double MinZ { get; set; }

    /// <summary>Its south edge.</summary>
    [JsonPropertyName("max_z")] public double MaxZ { get; set; }
}

/// <summary>What one layer holds: the shapes drawn on it, and how they group into landmasses.</summary>
public sealed class SketchShapes
{
    /// <summary>Every shape on the layer, in draw order — a later shape is drawn over an earlier one.</summary>
    [JsonPropertyName("shapes")]  public List<SketchShape> Shapes { get; set; } = [];

    /// <summary>The landmasses those shapes group into.</summary>
    [JsonPropertyName("groups")] public List<SketchGroup> Groups { get; set; } = [];
}

/// <summary>Groups shapes into a landmass and records whether the group is copied onto the mirror.</summary>
public sealed class SketchGroup
{
    /// <summary>What the rest of the document names the group by — a relief is keyed on it.</summary>
    [JsonPropertyName("id")]       public string? Id { get; set; }

    /// <summary>What the group is called on screen.</summary>
    [JsonPropertyName("name")]     public string? Name { get; set; }

    /// <summary>Whether the group is copied onto its orbit images. An on-axis neutral group sets it false,
    /// so it is not doubled onto itself.</summary>
    [JsonPropertyName("mirrors")]  public bool Mirrors { get; set; } = true;

    /// <summary>The shapes that make it up, by id.</summary>
    [JsonPropertyName("shapeIds")] public List<string> ShapeIds { get; set; } = [];
}

/// <summary>Bézier control points for a polygon edge (the segment leaving / arriving at a vertex).</summary>
public sealed class SketchControl
{
    /// <summary>The handle on the segment arriving at the vertex, as an <c>[x, z]</c> offset from it.</summary>
    [JsonPropertyName("in")]  public double[]? In { get; set; }

    /// <summary>The handle on the segment leaving it.</summary>
    [JsonPropertyName("out")] public double[]? Out { get; set; }
}

/// <summary>One shape: a rectangle / circle / polygon (or lasso) with its set-algebra role.</summary>
public sealed class SketchShape
{
    /// <summary>What the group lists and the theme scope names this shape by.</summary>
    [JsonPropertyName("id")]        public string Id { get; set; } = "";

    /// <summary>What it is: <c>rectangle</c>, <c>circle</c>, <c>polygon</c>, <c>lasso</c>, <c>path</c> —
    /// which says which of the numbers below it carries.</summary>
    [JsonPropertyName("type")]      public string Type { get; set; } = "";

    /// <summary>Whether it adds ground or takes it away: <c>add</c> or <c>subtract</c>.</summary>
    [JsonPropertyName("operation")] public string Operation { get; set; } = "add";

    /// <summary>Whether it overrides the shapes under it rather than combining with them.</summary>
    [JsonPropertyName("override")]  public bool Override { get; set; }

    /// <summary>Whether the columns it covers are ground the dressing pass must leave alone. A shape drawn to
    /// <em>be</em> something — a town wall, a crop bed, a well's rim, a flight of stairs — is terrain by
    /// construction and indistinguishable from the ground beside it by material or by layer, so a road
    /// repaints its top course and a channel cuts it down to the water line. Marking it keeps every prop off
    /// its columns exactly (no margin, so a road still runs to a gate), and a prop that lands there is
    /// <c>DR-KEEP</c>.</summary>
    [JsonPropertyName("keepClear")] public bool KeepClear { get; set; }

    /// <summary>A rectangle's west edge, in blocks.</summary>
    [JsonPropertyName("min_x")] public double? MinX { get; set; }

    /// <summary>A rectangle's north edge.</summary>
    [JsonPropertyName("min_z")] public double? MinZ { get; set; }

    /// <summary>A rectangle's east edge.</summary>
    [JsonPropertyName("max_x")] public double? MaxX { get; set; }

    /// <summary>A rectangle's south edge.</summary>
    [JsonPropertyName("max_z")] public double? MaxZ { get; set; }

    /// <summary>A circle's centre, east–west.</summary>
    [JsonPropertyName("center_x")] public double? CenterX { get; set; }

    /// <summary>A circle's centre, north–south.</summary>
    [JsonPropertyName("center_z")] public double? CenterZ { get; set; }

    /// <summary>A circle's radius, or a path's half-width.</summary>
    [JsonPropertyName("radius")]   public double? Radius { get; set; }

    /// <summary>A polygon's or lasso's outline as <c>[x, z]</c> pairs — or, for a path, its open centreline,
    /// which is the one shape not stored as its own outline.</summary>
    [JsonPropertyName("vertices")] public double[][]? Vertices { get; set; }

    /// <summary>Bézier handles per polygon edge, keyed by the vertex the edge leaves. Absent leaves every
    /// edge straight.</summary>
    [JsonPropertyName("controls")] public Dictionary<string, SketchControl>? Controls { get; set; }

    // A path is the one shape not stored as its own outline: Vertices are an OPEN centerline and Radius its
    // half-width, and the band those imply is derived wherever a ring is wanted. That keeps it editable as
    // the line it was drawn as while every consumer below still sees a ring.
    /// <summary>How a path's two long edges are drawn — <c>solid</c> holds one width the whole way,
    /// <c>rough</c> lets it wander so the outline reads organic, <c>tapered</c> runs it fat in the middle and
    /// thin at the ends. Absent is solid.</summary>
    [JsonPropertyName("path_edge")] public string? PathEdge { get; set; }

    /// <summary>The noise row a rough edge reads, so a path's wander is identical on every export until the
    /// author rerolls it.</summary>
    [JsonPropertyName("path_seed")] public uint? PathSeed { get; set; }

    // Height. Floor = the shape's elevation (where its base sits), BaseHeight = its thickness: the column
    // spans [Floor, Floor + BaseHeight]. For a polygon/lasso whose AnchorHeights line up with its Vertices,
    // the thickness varies per vertex (TIN-interpolated across the footprint). All optional; absent = the
    // flat one-block Y=0 behaviour.
    /// <summary>How this shape's top is decided, once a group carries a relief. Absent, the shape is
    /// ordinary ground and the relief is the ground — which is what a shape drawn to make a landmass wants.
    /// The three words are for a shape that is meant to stand OUT of the field rather than be part of it:
    /// <c>level</c> cuts a flat top at an absolute height (a mesa, whose faces are cliffs), <c>raise</c> holds
    /// it a fixed amount above the ground under it (a monolith or a plinth, which keeps its prominence
    /// wherever it is dragged), and <c>sink</c> the same downward (a quarry, a sunken arena).</summary>
    [JsonPropertyName("height_mode")]    public string? HeightMode { get; set; }

    /// <summary>How far in from its own outline an erected shape eases back into the ground it meets, in
    /// blocks. Zero is a sheer face — right for a plinth or a monolith, which is a built thing standing on the
    /// ground, and wrong for a landform: an unskirted mesa drops its whole height in one cell. A few blocks
    /// sits it IN the terrain instead of on it.</summary>
    [JsonPropertyName("skirt")]          public int? Skirt { get; set; }

    /// <summary>Whether this shape's ground takes part in its group's relief. The group is the unit a relief
    /// is solved over, because a relief solved per shape leaves a seam wherever two of them meet and disagree
    /// about the height they share. The fusion is not always what an author wants, and the case that decides
    /// it is a built thing standing on the ground — a city, a keep, a walled compound — whose floor is not
    /// terrain and which is themed as a unit. <c>hold</c> pins the shape at its own stated top, so the
    /// surrounding surface is solved knowing where it has to arrive; <c>exclude</c> pins nothing and takes the
    /// footprint out of the solve entirely, so the land is whatever that outline would have produced and the
    /// shape keeps its own height. Absent is <c>inherit</c> — the shape is part of the group's ground.
    /// <para>Not read on a shape that declares a <see cref="HeightMode"/>: such a shape already stands out of
    /// the field, and <c>raise</c>/<c>sink</c> read the ground under their own footprint to know where to
    /// stand, which an excluded footprint would not have.</para></summary>
    [JsonPropertyName("relief_scope")]   public string? ReliefScope { get; set; }

    /// <summary>The shape's thickness: its column spans <c>[floor, floor + base_height]</c>. Absent is the
    /// flat one-block behaviour at y=0.</summary>
    [JsonPropertyName("base_height")]    public double? BaseHeight { get; set; }

    /// <summary>A thickness per vertex, for a polygon or lasso whose count lines up with its
    /// <see cref="Vertices"/> — interpolated across the footprint, so the top varies rather than sitting
    /// flat.</summary>
    [JsonPropertyName("anchor_heights")] public double[]? AnchorHeights { get; set; }

    /// <summary>Where the shape's base sits, in blocks.</summary>
    [JsonPropertyName("floor")]          public double? Floor { get; set; }

    // Structural annotation (S25). A shape carrying a Role is not terrain the author drew — it is the spawn
    // or wool-room piece the plan already placed, projected in from the map intent so it stays visible while
    // a plan is refined. Role-tagged shapes are locked (read-only) and contribute nothing to the terrain:
    // the rasterizer skips them, so they never carve or double-cover the ground the fused group already
    // holds. IntentRef links back to the intent entity (a team id for a spawn, owner:colour for a wool);
    // Colour is the dye/team slug the client fills the labelled box with.
    /// <summary>What this shape is, where it is not terrain the author drew but a piece the plan placed —
    /// a spawn or a wool room, projected in from the map intent so it stays visible while a plan is refined.
    /// A shape carrying one is locked and contributes no terrain: the rasterizer skips it.</summary>
    [JsonPropertyName("role")]       public string? Role { get; set; }

    /// <summary>Which intent entity it stands for — a team id for a spawn, <c>owner:colour</c> for a
    /// wool.</summary>
    [JsonPropertyName("intentRef")]  public string? IntentRef { get; set; }

    /// <summary>The dye or team slug the labelled box is filled with.</summary>
    [JsonPropertyName("color")]      public string? Color { get; set; }

    // Whether Floor/BaseHeight on a Role-tagged shape were stated by the author rather than derived from the
    // plan's flat Surface. A compile always writes a fresh Floor/BaseHeight for the shape it holds the
    // group's relief against (AppendStructuralShape), because that is the only way a plan-space piece can
    // state a height at all before any terrain exists. Once a relief is solved the author can see where that
    // flat number lands and correct it — and the correction has to outlive the next recompile, which
    // otherwise overwrites every structural shape it produces. This flag is what tells the recompile which
    // shapes to leave alone: absent (or false), Floor/BaseHeight track the plan's Surface on every compile,
    // same as before; true, the stored Floor/BaseHeight/AnchorHeights carry forward onto the freshly compiled
    // shape with the same IntentRef instead (SketchLayout.CarryStructuralHeight), the same way a relief
    // outlives the shapes it was solved over. Never set by the compiler itself.
    /// <summary>Whether a role-tagged shape's <see cref="Floor"/> and <see cref="BaseHeight"/> were stated by
    /// the author rather than derived from the plan's flat surface. It is what tells a recompile which shapes
    /// to leave alone: absent, they track the plan on every compile; true, the stored heights carry forward
    /// onto the freshly compiled shape with the same <see cref="IntentRef"/>. Never set by the
    /// compiler.</summary>
    [JsonPropertyName("height_authored")] public bool? HeightAuthored { get; set; }

    /// <summary>Which sides of a structural room its doors stand on (<c>-z</c>, <c>+z</c>, <c>-x</c>,
    /// <c>+x</c>) — one for a spawn, one or more for a wool room, whose entries are every land seam and
    /// frontline edge the room presents. A room is a level rectangle and can never slope, so this says nothing
    /// about the room; it says which ground the room has to be level <em>with</em>, which is the ground a
    /// player crosses on the way in or out.</summary>
    [JsonPropertyName("doors")] public string[]? Doors { get; set; }

    // Terrain-paint theme override (docs/world-export/terrain-painting.md TP10): the id (into SketchLayout.Themes) of the theme this
    // shape paints; null falls to the map default. The scope is the shape, so a reshape moves the paint. Group
    // and full-map assignment are UI conveniences that write this per member shape / the map default.
    /// <summary>The theme this shape paints with, by id into the layout's registry. Absent falls to the map
    /// default. The scope is the shape, so a reshape moves the paint.</summary>
    [JsonPropertyName("theme")]      public string? Theme { get; set; }

}
