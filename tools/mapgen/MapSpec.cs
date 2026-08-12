using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.MapGen;

/// <summary>
/// One map, said as JSON — the whole of what an author (or an agent) states, and nothing it would have to
/// compute.
///
/// <para>The spec is deliberately a layer <em>above</em> the plan document. A plan says where every piece and
/// marker sits in cells; this says what the map is about and lets the generator answer the rest. The two ways
/// in are <see cref="Compose"/>, which asks the layout generator for a board at a player count, and
/// <see cref="Plan"/>, which carries a literal plan document for a board that is drawn rather than
/// generated. Everything after that — the paint, the interior elevation, the trees and the buildings — is
/// stated the same way whichever produced the ground.</para>
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

    /// <summary>Ask the layout generator for a board. Exclusive with <see cref="Plan"/>.</summary>
    [JsonPropertyName("compose")] public ComposeSpec? Compose { get; set; }

    /// <summary>A literal plan document, for a board that is drawn rather than generated. Exclusive with
    /// <see cref="Compose"/>.</summary>
    [JsonPropertyName("plan")] public JsonElement? Plan { get; set; }

    /// <summary>What the ground is painted with. One theme paints the whole map unless a shape names its
    /// own; a map of one material reads as one place, which is what a theme is for.</summary>
    [JsonPropertyName("theme")] public ThemeSpec? Theme { get; set; }

    /// <summary>The interior elevation, applied to every island the board compiled to. A board with no relief
    /// is flat, which is the far end of what the corpus ships.</summary>
    [JsonPropertyName("relief")] public ReliefSpec? Relief { get; set; }

    /// <summary>Trees scattered over the ground.</summary>
    [JsonPropertyName("trees")] public TreeSpec? Trees { get; set; }

    /// <summary>Buildings placed as scenery, each at a stated spot.</summary>
    [JsonPropertyName("houses")] public List<HouseSpec>? Houses { get; set; }

    /// <summary>Buildings scattered onto whatever ground will take them, the way <see cref="Trees"/> is a
    /// population rather than a list. It is the form to reach for on a generated board: the ground is not
    /// known until the plan compiles, so a stated coordinate is a guess and a guess lands in the void.</summary>
    [JsonPropertyName("village")] public VillageSpec? Village { get; set; }

    /// <summary>The buildings a wool room and a spawn room are stamped as, named from the house presets.
    /// Absent leaves the built-in bedrock shell, which is a lid rather than a building.</summary>
    [JsonPropertyName("room_shell")] public RoomShellSpec? RoomShell { get; set; }

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

    /// <summary>What the board is played for: <c>ctw</c> (the wools the generator places), <c>dtm</c> (each
    /// of those goals becomes a monument to destroy), or <c>dtcm</c> (monuments and cores together, the way
    /// the category's own corpus mixes them).
    ///
    /// <para>The generator budgets and places a goal per team without caring what kind of goal it is — a wool
    /// room, a monument and a core are all one team's thing to defend, in the same place on the board. So the
    /// objective is a retarget of the markers it already placed rather than a second generator.</para></summary>
    [JsonPropertyName("objective_mode")] public string ObjectiveMode { get; set; } = "ctw";

    /// <summary>Blocks per plan cell — the scale the board is drawn at. The generator budgets in cells, so
    /// this is what decides how big the finished map is on the ground without changing its layout: the
    /// destroy-the-monument corpus runs a median 148×164 blocks, which a five-block cell does not reach.</summary>
    [JsonPropertyName("cell")] public int Cell { get; set; } = 5;
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

/// <summary>Trees scattered over the ground, stated as a population rather than one by one.</summary>
public sealed class TreeSpec
{
    [JsonPropertyName("count")] public int Count { get; set; } = 40;

    /// <summary><c>grown</c> (the recursive skeleton) or <c>template</c> (vanilla), or <c>mixed</c>.</summary>
    [JsonPropertyName("form")] public string Form { get; set; } = "grown";

    /// <summary>Woods to draw from: <c>oak</c>, <c>birch</c>, <c>spruce</c>, <c>jungle</c>, <c>acacia</c>,
    /// <c>dark oak</c>.</summary>
    [JsonPropertyName("woods")] public List<string>? Woods { get; set; }

    [JsonPropertyName("min_height")] public double MinHeight { get; set; } = 8;
    [JsonPropertyName("max_height")] public double MaxHeight { get; set; } = 20;

    /// <summary>Whether the branches gather into whorls — the conifer against the broadleaf.</summary>
    [JsonPropertyName("whorled")] public bool? Whorled { get; set; }

    [JsonPropertyName("seed")] public int Seed { get; set; } = 1;

    /// <summary>Keep trees this far from any objective marker, so scenery never grows through a room.</summary>
    [JsonPropertyName("clearance")] public int Clearance { get; set; } = 10;
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

/// <summary>A handful of buildings dropped onto ground flat enough to stand them on.</summary>
public sealed class VillageSpec
{
    [JsonPropertyName("count")] public int Count { get; set; } = 4;

    /// <summary>Presets to draw from. Absent takes the five cut from one masonry, which is what makes a
    /// scatter of buildings read as one settlement rather than as a showroom.</summary>
    [JsonPropertyName("presets")] public List<string>? Presets { get; set; }

    [JsonPropertyName("seed")] public int Seed { get; set; } = 1;

    /// <summary>Keep buildings this far from any objective.</summary>
    [JsonPropertyName("clearance")] public int Clearance { get; set; } = 14;
}

/// <summary>One building placed as scenery: which preset, where, and which way its door faces.</summary>
public sealed class HouseSpec
{
    /// <summary>A row in the house presets: <c>alpine mining</c>, <c>desert brick</c>, <c>diorite pyramid</c>,
    /// <c>townside</c>, <c>townside on stilts</c>, <c>cottage</c>, <c>longhouse</c>, <c>terrace</c>,
    /// <c>counting house</c>, <c>workshop</c>.</summary>
    [JsonPropertyName("preset")] public string Preset { get; set; } = "cottage";

    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("z")] public int Z { get; set; }

    /// <summary>Override the preset's own footprint. Held to 20 a side and to the 192 blocks a placed
    /// building is allowed.</summary>
    [JsonPropertyName("width")] public int? Width { get; set; }
    [JsonPropertyName("depth")] public int? Depth { get; set; }

    /// <summary><c>negz</c> / <c>posz</c> / <c>negx</c> / <c>posx</c>, or absent to let it choose.</summary>
    [JsonPropertyName("front")] public string? Front { get; set; }
}
