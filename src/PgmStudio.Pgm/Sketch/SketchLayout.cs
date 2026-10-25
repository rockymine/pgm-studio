using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PgmStudio.Geom;
using PgmStudio.Geom.Relief;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

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

    /// <summary>Which library row each of <see cref="Themes"/> was copied from, id → row id. A theme authored
    /// on the board has no entry, and neither has one whose row has since been forgotten.
    ///
    /// <para><b>It is what makes a snapshot answerable about the row behind it.</b> A copy is matched back to
    /// its row by this and never by its name, so a theme renamed on the board or in the library is still the
    /// one theme — copying it in again replaces the snapshot it made rather than growing a second — and
    /// whether the snapshot is behind the row is answered by reading that row and comparing.</para></summary>
    [JsonPropertyName("themeSources")] public Dictionary<string, long>? ThemeSources { get; set; }

    /// <summary>Which of <see cref="Themes"/> covers every cell no shape's own theme scope claims.</summary>
    [JsonPropertyName("mapTheme")] public string? MapTheme { get; set; }

    /// <summary>The finish of the map's stamped rooms: one style for every wool room and one for every spawn
    /// room. Map-wide on purpose — a shell that differed between teams would be a sightline that differed
    /// between teams. Snapshots, like the themes. Absent stamps the built-in shells.</summary>
    [JsonPropertyName("roomStyles")] public SketchRoomStyles? RoomStyles { get; set; }

    /// <summary>What the author put on the map — paths, trees, boulders, areas of cover — each carrying its
    /// own position and its own knobs. A prop is stored as the placement itself rather than as a named recipe,
    /// which is what separates it from a theme. Absent dresses nothing.
    /// <para>A <c>Minecraft.Dressing.DressingDoc</c>, carried as raw JSON because this project does not reach
    /// that type; the published schema names it.</para></summary>
    [JsonPropertyName("dressing")] public JsonElement? Dressing { get; set; }

    /// <summary>Which biome each chunk of the exported world carries — the byte a client reads to tint grass,
    /// leaves and water. Map-wide and answered per chunk, because a biome's tint is blended across a radius
    /// and a region drawn to a finer edge never reaches its own colour there. Absent is plains everywhere,
    /// which is what every board already exported as.
    /// <para>A <c>Minecraft.Painting.BiomeField</c>, carried as raw JSON for the reason <see cref="Dressing"/>
    /// is; the published schema names it, and the export reads it through <c>BiomeScope</c>.</para></summary>
    [JsonPropertyName("biome")] public JsonElement? Biome { get; set; }

    /// <summary>Which library row <see cref="Biome"/> was copied from, or absent for a board whose field came
    /// from none — one written over HTTP, or none at all. The field itself is the snapshot the export reads;
    /// this only says which row it came from, so the phase that picked it can show which one is held. It is
    /// the note a copied theme carries in <see cref="ThemeSources"/>, for one field rather than a registry.</summary>
    [JsonPropertyName("biomeSource")] public long? BiomeSource { get; set; }

    /// <summary>The board's paint rules, in the order they are read: a material laid over a column's top courses
    /// where its slope, height, noise or nearness to a modifier say so, the first rule a column meets winning over
    /// its theme (<see cref="SketchPaintRuleJson"/>). Absent, every column paints its theme.</summary>
    [JsonPropertyName("paint")] public List<SketchPaintRuleJson>? Paint { get; set; }

    /// <summary>The terraform operations run over the built ground, in the order they run
    /// (<see cref="SketchModifierJson"/>).</summary>
    [JsonPropertyName("modifiers")] public List<SketchModifierJson>? Modifiers { get; set; }

    /// <summary>What the layout states about the ground under each play room, by the id of the shape the room
    /// is drawn as (<see cref="RoomGround"/>). The rooms themselves are the intent's, drawn in by
    /// <see cref="RoomShapes.Project"/>; absent, every room holds its own ground against the modifiers.</summary>
    [JsonPropertyName("rooms")] public Dictionary<string, RoomGround>? Rooms { get; set; }

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

    /// <summary>The keys that hold a map's finish rather than its shape: the terrain-theme registry, where
    /// each of its themes was copied from and the map default, the two bound room shells, every placed prop,
    /// the biome the ground is tinted by, and the paint rules. A plan states where the ground is and nothing about
    /// how it looks, so a layout compiled from one carries none of them.</summary>
    public static readonly string[] FinishKeys =
        ["themes", "themeSources", "mapTheme", "roomStyles", "dressing", "biome", "biomeSource", "paint"];

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

    /// <summary>The mode a document's mirroring groups are fanned by: its setup's, or the setup's own default
    /// where it states no setup — so every reader of the orbit fans a setup-less document the same way.</summary>
    public static string MirrorModeOf(SketchLayout? state) => state?.Setup?.MirrorMode ?? new SketchSetup().MirrorMode;

    /// <summary>Every group id a layout names, across all its layers.</summary>
    public static IEnumerable<string> GroupIds(SketchLayout? state)
    {
        foreach (var layer in Stack(state))
            foreach (var group in layer.Groups)
                if (group.Id is { Length: > 0 } id) yield return id;
    }

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
    /// <summary>The style every wool room is stamped in, as the stamper's own JSON. A snapshot rather than a
    /// library id, so editing a library row never rebuilds a shipped map's rooms.</summary>
    [JsonPropertyName("wool"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Wool { get; set; }

    /// <summary>The style every spawn room is stamped in, likewise snapshotted.</summary>
    [JsonPropertyName("spawn"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement Spawn { get; set; }

    /// <summary>The JSON literal <c>null</c>, as this type spells "no building".</summary>
    public static JsonElement Open { get; } = JsonDocument.Parse("null").RootElement;
}

/// <summary>One layer of the drawing: the shapes on it, and the height its ground starts at. The 2-D editor
/// authors one layer at a time and the rasterizer stacks them, so a cell's column is the layer's
/// <c>[floor, top]</c> shifted by <c>base_y</c>.</summary>
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

    /// <summary>The made thing this layer is one slice of, where it is a slice of one. A sculpture is a
    /// single thing to an author and many layers to the rasterizer — a column's runs are split across them —
    /// so this is what lets a strip draw one row for it, a filter address it, and a drag move every layer of
    /// it together. Absent, the layer is a whole by itself and answers to its own <see cref="Id"/>: the name
    /// states the relation rather than the thing, because the reason the field exists is that one balloon is
    /// eight layers.</summary>
    [JsonPropertyName("part_of")] public string? PartOf { get; set; }

    /// <summary>How the layer's floors meet the ground: absent, every shape's <c>floor</c> is the absolute
    /// height it states; <c>ground</c> takes the whole layer down onto the lowest solid column under its own
    /// footprint, so a made thing settles into a slope instead of floating over it or being buried by
    /// it.</summary>
    [JsonPropertyName("seat")]   public string? Seat { get; set; }

    /// <summary>The rock hanging under this layer's ground: absent, every column is the flat slab its shapes
    /// state; stated, each column's floor is lowered to an underside that tapers in from the rim
    /// (<see cref="UndersideSpec"/>), and rests on the terrain below where less than
    /// <see cref="PgmStudio.Geom.Relief.Underside.Clear"/> blocks would stand clear of it. One layer is one landmass, so the underside
    /// is the layer's and not a shape's: two shapes of a layer that touch hang as one island.</summary>
    [JsonPropertyName("underside")] public UndersideSpec? Underside { get; set; }

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

    /// <summary>The symmetry the board is drawn through, as an intent states one, or null for a board with
    /// none.</summary>
    [JsonIgnore]
    public SymmetryIntent? Board => Symmetry.Order(MirrorMode) > 1
        ? new SymmetryIntent { Mode = MirrorMode, CenterX = Center?.Cx ?? 0, CenterZ = Center?.Cz ?? 0 }
        : null;
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
    /// <summary>What the rest of the document names the group by.</summary>
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

    /// <summary>What it is: <c>rectangle</c>, <c>circle</c>, <c>polygon</c>, <c>lasso</c>, <c>polyline</c>, or a
    /// solid — <c>box</c>, <c>wedge</c>, <c>cylinder</c>, <c>sphere</c>, <c>torus</c>, <c>arch</c> — which says
    /// which of the numbers below it carries.</summary>
    [WordSet(typeof(ShapeKinds))]
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
    [JsonPropertyName("stroke_edge")] public string? StrokeEdge { get; set; }

    /// <summary>The noise row a rough edge reads, so a path's wander is identical on every export until the
    /// author rerolls it.</summary>
    [JsonPropertyName("stroke_seed")] public uint? StrokeSeed { get; set; }

    // Height. Floor = the shape's elevation (where its base sits), BaseHeight = its thickness: the column
    // spans [Floor, Floor + BaseHeight]. For a polygon/lasso whose AnchorHeights line up with its Vertices,
    // the thickness varies per vertex (TIN-interpolated across the footprint). All optional; absent = the
    // flat one-block Y=0 behaviour.
    /// <summary>How this shape's top is decided. Absent, the shape is ordinary ground, its top the height
    /// it states, and the modifier stack shapes it — which is what a shape drawn to make a landmass wants.
    /// The words are for a shape that is meant to stand OUT of the ground rather than be part of it, and the
    /// modifiers leave its columns out:
    /// <c>level</c> cuts a flat top at an absolute height (a mesa, whose faces are cliffs), <c>raise</c> holds
    /// it a fixed amount above the middle of the ground under it (a monolith or a plinth, which keeps its
    /// prominence wherever it is dragged), <c>sink</c> the same downward (a quarry, a sunken arena), and
    /// <c>drape</c> holds it that amount above the ground at every cell (a field wall or a hedge laid over the
    /// hillside).</summary>
    [WordSet(typeof(HeightModes))]
    [JsonPropertyName("height_mode")]    public string? HeightMode { get; set; }

    /// <summary>How far in from its own outline an erected shape eases back into the ground it meets, in
    /// blocks. Zero is a sheer face — right for a plinth or a monolith, which is a built thing standing on the
    /// ground, and wrong for a landform: an unskirted mesa drops its whole height in one cell. A few blocks
    /// sits it IN the terrain instead of on it.</summary>
    [JsonPropertyName("skirt")]          public int? Skirt { get; set; }

    /// <summary>Whether the modifier stack leaves this shape's ground alone. <c>hold</c> keeps its columns at
    /// the top the shape states — a city, a keep, a walled compound, whose floor is not terrain and is themed as
    /// a unit — and the ground round it is shaped up to its edge. Absent, the shape is part of the ground and
    /// the modifiers shape it (<see cref="ReliefScopes"/>).
    /// <para>A shape that declares a <see cref="HeightMode"/> is held whatever this says.</para></summary>
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

    /// <summary>What this shape is, where it is not terrain the author drew but a play room — a spawn, a wool
    /// room or the building on one, drawn in from the intent by <see cref="RoomShapes.Project"/> and never
    /// stored. A shape carrying one is locked and contributes no terrain: the rasterizer skips it.</summary>
    [WordSet(typeof(StructuralRoles))]
    [JsonPropertyName("role")]       public string? Role { get; set; }

    /// <summary>The dye or team slug the labelled box is filled with.</summary>
    [JsonPropertyName("color")]      public string? Color { get; set; }

    /// <summary>Whether a room shape's <see cref="Floor"/> and <see cref="BaseHeight"/> were stated by the
    /// author rather than given by whatever wrote the layout. Never set by the compiler.</summary>
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

    // What the shape is MADE OF, as against what paints the ground it is part of (TP22). A theme is a
    // five-bucket recipe for terrain and its buckets are chosen per column by whether that column is an edge,
    // which is the right question for ground and the wrong one for an object: on a shape with no interior
    // column only the rim and the wall ever paint, so a two-block stilt themed like its platform comes out a
    // course of the rim material over the wall material and the theme's own surface is nowhere on it.
    /// <summary>One material this shape is made of, in place of a theme's buckets — a road, a rail, a stilt,
    /// a stair tread, a kerb: something drawn as terrain, walked on and stacked on like terrain, that is
    /// nonetheless one thing rather than ground with a top and a face. Stated as a
    /// <c>TerrainMaterial</c> — the same shape a theme's buckets take, so a solid, a pattern or a band stack
    /// all fit — and painted over the shape's whole span with no rim, no wall and no surface depth.
    ///
    /// <para><b>It is not <c>kind: "made"</c>, and the two are independent.</b> A made layer is out of the
    /// walks, out of the stacking rules and out of the ground everything rests on — right for a train or a
    /// statue, wrong for a stair, which is the way onto a storey and has to stay walkable. This says only
    /// what the shape is made of.</para>
    ///
    /// <para>A shape stating both this and a <see cref="Theme"/> is refused (<c>SK24</c>): the two answer the
    /// same question and a document that states both says nothing about which was meant.</para></summary>
    [JsonPropertyName("material")]   public JsonElement? Material { get; set; }

    // The solids. A box and a wedge read a rectangle's bounds; a cylinder, a sphere and a torus a circle's
    // centre and radius; an arch a polyline's vertices and radius. Height is `floor` and `base_height`.
    /// <summary>Degrees a solid is turned about the vertical through its centre, clockwise as the canvas draws
    /// it: its width axis points <c>(cos turn, sin turn)</c> on the plan. Absent is none.</summary>
    [JsonPropertyName("turn")]  public double? Turn { get; set; }

    /// <summary>Degrees a solid is tilted about its own width axis, after <c>turn</c>: its depth end rises, so
    /// a box of tilt 45 is a ramp climbing along its depth and a torus of tilt 90 stands on its edge. Absent is
    /// upright.</summary>
    [JsonPropertyName("tilt")]  public double? Tilt { get; set; }

    /// <summary>Blocks a solid is bedded below its <c>floor</c>: its lowest point stands <c>floor − sunk</c>,
    /// so a sphere floored at the ground's top and sunk by 3 has three blocks of it under the surface.</summary>
    [JsonPropertyName("sunk")]  public double? Sunk { get; set; }

    /// <summary>A torus's tube radius, in blocks. The ring's outer edge is the shape's <c>radius</c>.</summary>
    [JsonPropertyName("tube")]  public double? Tube { get; set; }

    /// <summary>An arch's deck as <c>[crown, end]</c> or <c>[crown, end, power]</c>: the height, over the
    /// layer's base, of the walkable surface at the middle of the span and at its feet, eased by the power
    /// (1 is straight). Absent is a flat deck at <c>floor + base_height</c>.</summary>
    [JsonPropertyName("deck")]  public double[]? Deck { get; set; }

    /// <summary>The rock under an arch's deck as <c>[crown, end]</c> or <c>[crown, end, power]</c>, in blocks
    /// thick. Absent is <c>[4, 12, 1.5]</c>: thin at the crown and heavy at the feet.</summary>
    [JsonPropertyName("thick")] public double[]? Thick { get; set; }

    /// <summary>Blocks an arch's deck falls towards its two sides, as the square of the distance from the
    /// centreline over the half-width. Absent is a deck level across.</summary>
    [JsonPropertyName("camber")] public double? Camber { get; set; }
}
