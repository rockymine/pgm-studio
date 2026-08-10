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

    // Terrain-paint theming lives on the sketch model (docs/world-export/finishing-model.md §4): a registry of
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

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static SketchLayout? Parse(string json) => JsonSerializer.Deserialize<SketchLayout>(json, Json);

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
}

/// <summary>The two room-style snapshots a map binds: the shell every wool structure is stamped with, and the
/// one every spawn is. Held as raw JSON because a layout cannot know the stamper's model — the same reason a
/// theme rides here as a <see cref="JsonElement"/> — and read back by the export's scope. Either may be absent,
/// which stamps that kind's built-in shell.</summary>
public sealed class SketchRoomStyles
{
    // The wire word stays "cage": it is written into stored layouts by the sketch bridge, and renaming it
    // would leave every bound wool style silently falling back to the built-in one on load.
    [JsonPropertyName("cage")]  public JsonElement? Wool { get; set; }
    [JsonPropertyName("spawn")] public JsonElement? Spawn { get; set; }
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

    // Height. Floor = the shape's elevation (where its base sits), BaseHeight = its thickness: the column
    // spans [Floor, Floor + BaseHeight]. For a polygon/lasso whose AnchorHeights line up with its Vertices,
    // the thickness varies per vertex (TIN-interpolated across the footprint). All optional; absent = the
    // flat one-block Y=0 behaviour.
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

    // Terrain-paint theme override (finishing-model.md §4): the id (into SketchLayout.Themes) of the theme this
    // shape paints; null falls to the map default. The scope is the shape, so a reshape moves the paint. Island
    // and full-map assignment are UI conveniences that write this per member shape / the map default.
    [JsonPropertyName("theme")]      public string? Theme { get; set; }

}
