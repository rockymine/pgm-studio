using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// The sketch layout wire model — the authoring blob (<c>{setup, layout:{shapes, islands}}</c>) shared by
/// the rasterizer that reads it and the generators that write it. camelCase by default (Web options);
/// snake_case and reserved-word fields carry an explicit name. Kept as the single definition so a
/// generated layout and a hand-drawn one parse through exactly the same shape.
/// </summary>
public sealed class SketchLayout
{
    [JsonPropertyName("setup")]  public SketchSetup? Setup { get; set; }
    [JsonPropertyName("layout")] public SketchShapes? Layout { get; set; }   // legacy single-layer (pre-S7)
    [JsonPropertyName("layers")] public List<SketchLayer>? Layers { get; set; }

    // Terrain-paint theming lives on the sketch model (docs/world-export/terrain-painting.md TP10): a registry of
    // named themes (id → the theme JSON the painter deserializes) and the map-default id covering every cell no
    // shape scope claims. A shape's own theme override rides on SketchShape.Theme; a cell resolves shape → map
    // default at export (TerrainThemeScope). Absent on a plain/unthemed sketch, which paints the built-in default.
    [JsonPropertyName("themes")]   public Dictionary<string, JsonElement>? Themes { get; set; }
    [JsonPropertyName("mapTheme")] public string? MapTheme { get; set; }

    // The finish of the map's stamped rooms (docs/world-export/structures.md §9): one style for every wool cage
    // and one for every spawn cube. Two snapshots rather than a registry-and-id, because there are exactly two
    // bindings and nothing references them individually — a room style is map-wide on purpose. A cage that
    // differed between teams would be a sightline that differed between teams, and the rooms are fanned across
    // the symmetry orbit precisely so both sides face the same building.
    // Snapshots, not library ids: editing a library row must never rebuild a shipped map's spawn rooms, the
    // same rule the applied terrain theme follows. Absent on a sketch that never picked one, which stamps the
    // built-in shells.
    [JsonPropertyName("roomStyles")] public SketchRoomStyles? RoomStyles { get; set; }

    // Dressing (docs/world-export/decoration.md) does NOT ride beside theming, and the difference is the point.
    // A theme is a recipe applied to a footprint, so it is named, stored once and referenced; a prop was placed
    // somewhere, so what is stored is the placement itself. This is the list of what the author put on the map —
    // paths, trees, boulders, areas of cover — each carrying its own position and its own knobs. Absent on a
    // sketch that never opened the phase, which dresses nothing.
    [JsonPropertyName("dressing")] public JsonElement? Dressing { get; set; }

    // Interior elevation (docs/world-export/relief.md), keyed by island id. It rides top-level rather
    // than inside the shapes for one reason: a plan recompile replaces every shape it produced, and a relief
    // is expensive hand work a plan cannot express. It is not a finish key — a relief is geometry, it decides
    // what the rasterizer emits, and it is carried across a recompile under its own rule (CarryRelief).
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
    /// map default, the two bound room shells, and every placed prop. A plan states where the ground is and
    /// nothing about how it looks, so a layout compiled from one carries none of them.</summary>
    public static readonly string[] FinishKeys = ["themes", "mapTheme", "roomStyles", "dressing"];

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

    /// <summary>The island ids a stored relief is bound to that the freshly compiled layout has no island
    /// for. Each one is hand-authored terrain the recompile would silently discard, which is why the compile
    /// path refuses rather than carrying what it can: island identity is derived from the geometry, so a
    /// re-fused island does not merely move — it becomes a different island, and a relief authored against
    /// the old fusion has nowhere correct to land.</summary>
    public static IReadOnlyList<string> OrphanedRelief(string compiledJson, string? storedJson)
    {
        var stored = string.IsNullOrWhiteSpace(storedJson) ? null : Parse(storedJson);
        if (stored?.Relief is not { Count: > 0 } relief) return [];

        var islands = new HashSet<string>(IslandIds(Parse(compiledJson)));
        return relief.Keys.Where(id => !islands.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    /// <summary>Every island id a layout names, across all its layers.</summary>
    public static IEnumerable<string> IslandIds(SketchLayout? state)
    {
        if (state?.Layers is { Count: > 0 } layers)
            foreach (var layer in layers)
                foreach (var island in layer.Layout?.Islands ?? [])
                    if (island.Id is { Length: > 0 } id) yield return id;
        if (state?.Layout is { } single)
            foreach (var island in single.Islands)
                if (island.Id is { Length: > 0 } id) yield return id;
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

        var islands = new HashSet<string>(IslandIds(Parse(compiledJson)));
        var kept = relief.Where(entry => islands.Contains(entry.Key))
                         .ToDictionary(entry => entry.Key, entry => entry.Value);
        if (kept.Count == 0) return compiledJson;

        target["relief"] = JsonNode.Parse(JsonSerializer.Serialize(kept, Json));
        return target.ToJsonString(Json);
    }

    /// <summary>A freshly compiled layout with every author-stated structural height carried onto it —
    /// the shape-level counterpart to <see cref="CarryRelief"/>. A plan recompile writes a fresh Floor/
    /// BaseHeight for every Role-tagged shape it holds an island's relief against, because that is the only
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
    // (legacy single `layout` and S7 `layers`) — only the ones the author actually corrected.
    private static Dictionary<string, StructuralHeight> StructuralHeights(SketchLayout? state)
    {
        var shapes = state?.Layers is { Count: > 0 } layers
            ? layers.SelectMany(l => l.Layout?.Shapes ?? [])
            : state?.Layout?.Shapes ?? [];
        return shapes.Where(s => s.Role is not null && s.HeightAuthored == true && s.IntentRef is { Length: > 0 })
                     .ToDictionary(s => s.IntentRef!, s => new StructuralHeight(s.Floor, s.BaseHeight, s.AnchorHeights));
    }

    // The shape arrays a compiled layout can hold its structural annotations in — legacy `layout.shapes`
    // (what PlanCompiler emits today) and S7 `layers[].layout.shapes`, checked too so this keeps working if
    // the compiler ever starts emitting stacked layers.
    private static IEnumerable<JsonArray> ShapeArrays(JsonObject root)
    {
        if (root["layout"]?["shapes"] is JsonArray single) yield return single;
        if (root["layers"] is JsonArray layers)
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
    [JsonPropertyName("cage"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Wool { get; set; }

    [JsonPropertyName("spawn"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Spawn { get; set; }

    /// <summary>The JSON literal <c>null</c>, as this type spells "no building".</summary>
    public static JsonElement Open { get; } = JsonDocument.Parse("null").RootElement;
}

/// <summary>A stacked slab (S7): its shapes/islands at a Y offset. The whole 2-D editor authors one layer;
/// the rasterizer stacks them — a cell's column is the layer's <c>[floor, top]</c> shifted by <c>base_y</c>.</summary>
public sealed class SketchLayer
{
    [JsonPropertyName("id")]     public string? Id { get; set; }
    [JsonPropertyName("name")]   public string? Name { get; set; }
    [JsonPropertyName("base_y")] public double BaseY { get; set; }
    [JsonPropertyName("layout")] public SketchShapes? Layout { get; set; }
}

/// <summary>The mirror mode + centre that fan a mirroring island's shapes onto their orbit images, plus the
/// optional working bounds the editor frames the canvas to (hand-drawn sketches carry it; the rasterizer
/// ignores it and reads only the centre + mode).</summary>
public sealed class SketchSetup
{
    [JsonPropertyName("mirror_mode")] public string MirrorMode { get; set; } = "rot_180";
    [JsonPropertyName("center")]      public SketchCenter? Center { get; set; }
    [JsonPropertyName("bbox")]        public SketchBbox? Bbox { get; set; }
}

public sealed class SketchCenter
{
    [JsonPropertyName("cx")] public double Cx { get; set; }
    [JsonPropertyName("cz")] public double Cz { get; set; }
}

/// <summary>The editor's working bounds — the square the canvas fits to on open.</summary>
public sealed class SketchBbox
{
    [JsonPropertyName("min_x")] public double MinX { get; set; }
    [JsonPropertyName("max_x")] public double MaxX { get; set; }
    [JsonPropertyName("min_z")] public double MinZ { get; set; }
    [JsonPropertyName("max_z")] public double MaxZ { get; set; }
}

public sealed class SketchShapes
{
    [JsonPropertyName("shapes")]  public List<SketchShape> Shapes { get; set; } = [];
    [JsonPropertyName("islands")] public List<SketchIsland> Islands { get; set; } = [];
}

/// <summary>Groups shapes into a landmass and records whether the group is copied onto the mirror.</summary>
public sealed class SketchIsland
{
    [JsonPropertyName("id")]       public string? Id { get; set; }
    [JsonPropertyName("name")]     public string? Name { get; set; }
    [JsonPropertyName("mirrors")]  public bool Mirrors { get; set; } = true;
    [JsonPropertyName("shapeIds")] public List<string> ShapeIds { get; set; } = [];
}

/// <summary>Bézier control points for a polygon edge (the segment leaving / arriving at a vertex).</summary>
public sealed class SketchControl
{
    [JsonPropertyName("in")]  public double[]? In { get; set; }
    [JsonPropertyName("out")] public double[]? Out { get; set; }
}

/// <summary>One shape: a rectangle / circle / polygon (or lasso) with its set-algebra role.</summary>
public sealed class SketchShape
{
    [JsonPropertyName("id")]        public string Id { get; set; } = "";
    [JsonPropertyName("type")]      public string Type { get; set; } = "";
    [JsonPropertyName("operation")] public string Operation { get; set; } = "add";
    [JsonPropertyName("override")]  public bool Override { get; set; }
    [JsonPropertyName("min_x")] public double? MinX { get; set; }
    [JsonPropertyName("min_z")] public double? MinZ { get; set; }
    [JsonPropertyName("max_x")] public double? MaxX { get; set; }
    [JsonPropertyName("max_z")] public double? MaxZ { get; set; }
    [JsonPropertyName("center_x")] public double? CenterX { get; set; }
    [JsonPropertyName("center_z")] public double? CenterZ { get; set; }
    [JsonPropertyName("radius")]   public double? Radius { get; set; }
    [JsonPropertyName("vertices")] public double[][]? Vertices { get; set; }
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
    /// <summary>How this shape's top is decided, once an island carries a relief. Absent, the shape is
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

    /// <summary>Whether this shape's ground takes part in its island's relief. The island is the unit a relief
    /// is solved over, because a relief solved per shape leaves a seam wherever two of them meet and disagree
    /// about the height they share. The fusion is not always what an author wants, and the case that decides
    /// it is a built thing standing on the ground — a city, a keep, a walled compound — whose floor is not
    /// terrain and which is themed as a unit. <c>hold</c> pins the shape at its own stated top, so the
    /// surrounding surface is solved knowing where it has to arrive; <c>exclude</c> pins nothing and takes the
    /// footprint out of the solve entirely, so the land is whatever that outline would have produced and the
    /// shape keeps its own height. Absent is <c>inherit</c> — the shape is part of the island's ground.
    /// <para>Not read on a shape that declares a <see cref="HeightMode"/>: such a shape already stands out of
    /// the field, and <c>raise</c>/<c>sink</c> read the ground under their own footprint to know where to
    /// stand, which an excluded footprint would not have.</para></summary>
    [JsonPropertyName("relief_scope")]   public string? ReliefScope { get; set; }

    [JsonPropertyName("base_height")]    public double? BaseHeight { get; set; }
    [JsonPropertyName("anchor_heights")] public double[]? AnchorHeights { get; set; }
    [JsonPropertyName("floor")]          public double? Floor { get; set; }

    // Structural annotation (S25). A shape carrying a Role is not terrain the author drew — it is the spawn
    // or wool-room piece the plan already placed, projected in from the map intent so it stays visible while
    // a plan is refined. Role-tagged shapes are locked (read-only) and contribute nothing to the terrain:
    // the rasterizer skips them, so they never carve or double-cover the ground the fused island already
    // holds. IntentRef links back to the intent entity (a team id for a spawn, owner:colour for a wool);
    // Colour is the dye/team slug the client fills the labelled box with.
    [JsonPropertyName("role")]       public string? Role { get; set; }
    [JsonPropertyName("intentRef")]  public string? IntentRef { get; set; }
    [JsonPropertyName("color")]      public string? Color { get; set; }

    // Whether Floor/BaseHeight on a Role-tagged shape were stated by the author rather than derived from the
    // plan's flat Surface. A compile always writes a fresh Floor/BaseHeight for the shape it holds the
    // island's relief against (AppendStructuralShape), because that is the only way a plan-space piece can
    // state a height at all before any terrain exists. Once a relief is solved the author can see where that
    // flat number lands and correct it — and the correction has to outlive the next recompile, which
    // otherwise overwrites every structural shape it produces. This flag is what tells the recompile which
    // shapes to leave alone: absent (or false), Floor/BaseHeight track the plan's Surface on every compile,
    // same as before; true, the stored Floor/BaseHeight/AnchorHeights carry forward onto the freshly compiled
    // shape with the same IntentRef instead (SketchLayout.CarryStructuralHeight), the same way a relief
    // outlives the shapes it was solved over. Never set by the compiler itself.
    [JsonPropertyName("height_authored")] public bool? HeightAuthored { get; set; }

    // Terrain-paint theme override (docs/world-export/terrain-painting.md TP10): the id (into SketchLayout.Themes) of the theme this
    // shape paints; null falls to the map default. The scope is the shape, so a reshape moves the paint. Island
    // and full-map assignment are UI conveniences that write this per member shape / the map default.
    [JsonPropertyName("theme")]      public string? Theme { get; set; }

}
