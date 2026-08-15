using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Pgm;

namespace PgmStudio.MapGen;

/// <summary>
/// One map, said as JSON — the whole of what an author (or an agent) states, and nothing it would have to
/// compute.
///
/// <para>The spec is a thin addressing layer over the real documents (`docs/tools/capabilities.md`), not a
/// reduction of them (`docs/tools/mapgen-review.md` MG29). The two ways to state the board are
/// <see cref="Compose"/>, which asks the layout generator for a board at a player count, and
/// <see cref="Plan"/>, which carries a literal <c>PlanModel</c> document for a board that is drawn rather
/// than generated — a destroy board's destroyables and cores are authored here, on the plan, since the
/// generator does not compose for a destroy objective. <see cref="Layout"/> and <see cref="Intent"/> carry
/// a fragment of <c>SketchLayout</c> and <c>MapIntent</c> verbatim, merged onto whatever <see cref="Compose"/>
/// or <see cref="Plan"/> produced (<see cref="DocumentOverlay"/>) — an extra shape with its own theme and
/// height, a fuller theme than <see cref="Theme"/> can name, a defence wall, a hand-placed prop. Everything
/// a convenience field below states is shorthand for a fragment of one of those two documents; anything a
/// spec cannot say through either is a gap in the system, not in this format.</para>
///
/// <para>Names, not block ids. A theme names a palette family and the tool resolves it, because the set of
/// families is knowledge the tool already has and an id is knowledge the author would have to look up.</para>
/// </summary>
public sealed class MapSpec
{
    [JsonPropertyName("slug")]      public string Slug { get; set; } = "";
    [JsonPropertyName("name")]      public string Name { get; set; } = "";
    [JsonPropertyName("objective")] public string Objective { get; set; } = "";
    [JsonPropertyName("authors")]   public List<string>? Authors { get; set; }

    /// <summary>Where the map folder is written. Absent puts it under the community destroy-the-monument
    /// corpus, which is where a generated map goes to be looked at.</summary>
    [JsonPropertyName("out_dir")] public string? OutDir { get; set; }

    /// <summary>Ask the layout generator for a board. Exclusive with <see cref="Plan"/> and
    /// <see cref="Grid"/>.</summary>
    [JsonPropertyName("compose")] public ComposeSpec? Compose { get; set; }

    /// <summary>A literal plan document, for a board that is drawn rather than generated. Exclusive with
    /// <see cref="Compose"/> and <see cref="Grid"/>.</summary>
    [JsonPropertyName("plan")] public JsonElement? Plan { get; set; }

    /// <summary>A row of unrelated islands on a grid — a catalogue rather than a board, which is what a map
    /// built to be looked at is. Exclusive with <see cref="Compose"/> and <see cref="Plan"/>, and the one of
    /// the three that skips the plan entirely: a plot is a disc, an octagon or a cross, and a plan piece is a
    /// rectangle, so this emits its <c>SketchLayout</c> directly.</summary>
    [JsonPropertyName("grid")] public GridSpec? Grid { get; set; }

    /// <summary>What the ground is painted with. One theme paints the whole map unless a shape names its
    /// own; a map of one material reads as one place, which is what a theme is for.</summary>
    [JsonPropertyName("theme")] public ThemeSpec? Theme { get; set; }

    /// <summary>The interior elevation, applied to every island the board compiled to. A board with no relief
    /// is flat, which is the far end of what the corpus ships.</summary>
    [JsonPropertyName("relief")] public ReliefSpec? Relief { get; set; }

    /// <summary>The buildings a wool room and a spawn room are stamped as, named from the house presets.
    /// Absent leaves the built-in bedrock shell, which is a lid rather than a building.</summary>
    [JsonPropertyName("room_shell")] public RoomShellSpec? RoomShell { get; set; }

    /// <summary>A <c>SketchLayout</c> fragment, verbatim, merged onto the compiled layout
    /// (<see cref="DocumentOverlay"/>) after <see cref="Theme"/>, <see cref="Relief"/> and
    /// <see cref="RoomShell"/> have been applied. This is where a shape the convenience fields cannot state
    /// goes — an extra shape with its own theme, floor, base height, per-vertex anchor heights and
    /// relief_scope; a theme carrying a rim band or a per-shape scope <see cref="Theme"/> has no field for;
    /// dressing (there is no sampler — a prop is placed by writing <c>dressing.props</c> here, the studio's
    /// own authored form, `docs/tools/sketch.md`).</summary>
    [JsonPropertyName("layout")] public JsonElement? Layout { get; set; }

    /// <summary>A <c>MapIntent</c> fragment, verbatim, merged onto the compiled intent
    /// (<see cref="DocumentOverlay"/>) — a defence wall, an iron cube, or anything else the plan the board
    /// compiled from did not carry.</summary>
    [JsonPropertyName("intent")] public JsonElement? Intent { get; set; }

    /// <summary>Emit the named stage-image set (<c>plan</c>, <c>heightmap</c>, <c>surface</c>, <c>contour</c>,
    /// <c>dressing</c>, <c>traversability</c>, <c>structures</c>, <c>topdown</c>) into a <c>stages/</c> folder
    /// beside <c>region/</c> and <c>map.xml</c>. Off by default — a batch run over many specs should not pay
    /// for pictures it will not look at — turned on here or with the CLI's <c>--stages</c> flag, which forces
    /// it on regardless of what a spec says.</summary>
    [JsonPropertyName("stages")] public bool Stages { get; set; }

    public static MapSpec Parse(string json) =>
        JsonSerializer.Deserialize<MapSpec>(json, Options)
        ?? throw new ArgumentException("the spec did not parse as an object");

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

/// <summary>What to ask the layout generator for.</summary>
public sealed class ComposeSpec
{
    /// <summary>Players per team, 5–32. A thirty-player map is fifteen a side.</summary>
    [JsonPropertyName("players_per_team")] public int PlayersPerTeam { get; set; } = 15;

    [JsonPropertyName("teams")]    public int Teams { get; set; } = 2;

    /// <summary><c>rot_180</c> / <c>mirror_x</c> / <c>mirror_z</c> for two teams, <c>rot_90</c> for four.</summary>
    [JsonPropertyName("symmetry")] public string? Symmetry { get; set; }

    [JsonPropertyName("seed")]     public ulong Seed { get; set; }

    /// <summary>Blocks per plan cell — the scale the board is drawn at. The generator budgets in cells, so
    /// this is what decides how big the finished map is on the ground without changing its layout: the
    /// destroy-the-monument corpus runs a median 148×164 blocks, which a five-block cell does not reach.</summary>
    [JsonPropertyName("cell")] public int Cell { get; set; } = 5;
}

/// <summary>A grid of plots, each an island of its own. The grid decides where they sit; each plot says only
/// what it is and what paints it.</summary>
public sealed class GridSpec
{
    /// <summary>Plots per row. They run left to right and then down, so the order stated is the order walked.</summary>
    [JsonPropertyName("columns")] public int Columns { get; set; } = 5;

    /// <summary>Centre-to-centre spacing in blocks. A plot reaching past half of it would join its neighbour,
    /// which is refused.</summary>
    [JsonPropertyName("pitch")] public int Pitch { get; set; } = 56;

    /// <summary>The lowest course a plot's column is filled from, just above bedrock.</summary>
    [JsonPropertyName("floor")] public int Floor { get; set; } = 1;

    /// <summary>The course a player walks on, so every plot carries a wall of that height above the void.</summary>
    [JsonPropertyName("top")] public int Top { get; set; } = 26;

    [JsonPropertyName("plots")] public List<PlotSpec> Plots { get; set; } = [];
}

/// <summary>One plot: the outline, and the theme it is painted through. The theme is a key into the layout's
/// own <c>themes</c> registry, which a <see cref="MapSpec.Layout"/> overlay is where you put.</summary>
public sealed class PlotSpec
{
    /// <summary><c>rectangle</c> · <c>circle</c> · <c>polygon</c> — the sketch's own shape vocabulary.</summary>
    [JsonPropertyName("kind")] public string Kind { get; set; } = "rectangle";

    /// <summary>A theme id in the layout's registry. Absent leaves the plot painted by the map theme.</summary>
    [JsonPropertyName("theme")] public string? Theme { get; set; }

    /// <summary>What this plot is showing. Carried onto the island, so a report names it.</summary>
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("width")]  public double Width { get; set; } = 34;
    [JsonPropertyName("depth")]  public double Depth { get; set; } = 26;
    [JsonPropertyName("radius")] public double Radius { get; set; } = 14;

    /// <summary>A polygon's outline as <c>[dx, dz]</c> offsets from the plot's own centre, so one outline can
    /// be laid at every plot without restating its coordinates.</summary>
    [JsonPropertyName("vertices")] public double[][]? Vertices { get; set; }
}

/// <summary>The paint, named. Every field is a palette family name except <see cref="Surface"/>, which also
/// takes <c>grass</c> — the one course of a top-faced block that reads from above.</summary>
public sealed class ThemeSpec
{
    /// <summary>The top course a player walks on. A family name, or <c>grass</c>.</summary>
    [JsonPropertyName("surface")] public string Surface { get; set; } = "grass";

    /// <summary>The face of every drop — the riser down from the surface to whatever is below.</summary>
    [JsonPropertyName("wall")] public string Wall { get; set; } = "grey stone";

    /// <summary>The one-block band inked round an island's outline, so the edge reads as an edge.</summary>
    [JsonPropertyName("rim")] public string? Rim { get; set; } = "ash";

    /// <summary>What the volume under the surface is packed with.</summary>
    [JsonPropertyName("fill")] public string Fill { get; set; } = "grey stone";

    /// <summary>How the surface is patterned rather than laid solid: <c>solid</c>, <c>voronoi</c>,
    /// <c>cell</c>, <c>noise</c>, <c>turbulence</c>, <c>electric</c>. A pattern reads the surface family.</summary>
    [JsonPropertyName("pattern")] public string Pattern { get; set; } = "solid";
}

/// <summary>The interior elevation. Marks state heights and pushes lift whole shapes of ground; the solver
/// interpolates the rest.</summary>
public sealed class ReliefSpec
{
    [JsonPropertyName("base")]   public double Base { get; set; } = 6;
    [JsonPropertyName("reach")]  public double Reach { get; set; }
    [JsonPropertyName("step")]   public int Step { get; set; } = 1;
    [JsonPropertyName("stairs")] public bool Stairs { get; set; } = true;

    [JsonPropertyName("grain")]  public GrainSpec? Grain { get; set; }

    /// <summary>Marks, verbatim in the stored relief's own vocabulary: <c>point</c> / <c>line</c> /
    /// <c>area</c> / <c>rim</c> / <c>scarp</c>.</summary>
    [JsonPropertyName("marks")]  public JsonElement? Marks { get; set; }

    /// <summary>Pushes, verbatim: a drawn ring lifted or lowered.</summary>
    [JsonPropertyName("pushes")] public JsonElement? Pushes { get; set; }

    /// <summary>Scatter this many random hills and hollows over each island instead of (or beside) the
    /// stated marks — the quick way to get ground that is not a table.</summary>
    [JsonPropertyName("scatter")] public ScatterSpec? Scatter { get; set; }
}

public sealed class GrainSpec
{
    [JsonPropertyName("amplitude")] public double Amplitude { get; set; } = 1.5;
    [JsonPropertyName("scale")]     public int Scale { get; set; } = 9;
    [JsonPropertyName("seed")]      public uint Seed { get; set; } = 1;
}

/// <summary>Randomly placed relief, so an island can be given terrain without every hill being written out.
/// The marks it generates are ordinary point marks — nothing here reaches past what an author could draw.</summary>
public sealed class ScatterSpec
{
    [JsonPropertyName("count")]  public int Count { get; set; } = 10;
    [JsonPropertyName("min_h")]  public double MinHeight { get; set; } = -3;
    [JsonPropertyName("max_h")]  public double MaxHeight { get; set; } = 8;
    [JsonPropertyName("radius")] public double Radius { get; set; } = 14;
    [JsonPropertyName("seed")]   public int Seed { get; set; } = 1;
}

/// <summary>What the two rooms a map is played through are built as. Both take a house preset, because a
/// wool cage and a spawn room are buildings a player walks into — the built-in shell is a bedrock lid, which
/// says "objective here" and nothing about the place it stands in.</summary>
public sealed class RoomShellSpec
{
    /// <summary>The building stamped over a wool room (or, on a destroy map, over the goal's piece).</summary>
    [JsonPropertyName("wool")] public string? Wool { get; set; }

    /// <summary>The building stamped over a spawn. <c>open</c> leaves the ground bare, which is right where
    /// the plateau itself is the room.</summary>
    [JsonPropertyName("spawn")] public string? Spawn { get; set; }
}
