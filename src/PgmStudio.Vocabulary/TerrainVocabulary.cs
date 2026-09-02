namespace PgmStudio.Vocabulary;

/// <summary>
/// The <c>kind</c> discriminator of every terrain-paint material — the value a style is tagged, stored and
/// browsed by ("show every voronoi"). It lives here because it is wire vocabulary: the client's editor, the
/// HTTP surface and the <c>style.kind</c> column all have to agree on the same strings, and this is the one
/// leaf all three can reach. The painter's own polymorphic attributes carry the same strings by necessity.
/// </summary>
public static class MaterialKind
{
    public const string Solid = "solid";
    public const string Layered = "layered";
    public const string TeamTint = "teamTint";
    public const string Voronoi = "voronoi";
    public const string Cell = "cell";
    public const string Noise = "noise";
    public const string Turbulence = "turbulence";
    public const string Electric = "electric";
    public const string WallRun = "wallRun";
    public const string WallDiagonal = "wallDiagonal";
    public const string Checker = "checker";
    public const string LogChecker = "logChecker";
    public const string LaidLog = "laidLog";
    public const string WallFrame = "wallFrame";

    /// <summary>The closed set of words a stored style's <c>kind</c> may be — the wire's own vocabulary, and
    /// what the published schema names instead of calling the field a string.
    ///
    /// <para><b>The words and nothing else.</b> What each kind is <em>called</em>, what it draws and what it
    /// takes are answered by <c>GET /api/terrain/patterns</c>, which reads them off the records themselves; a
    /// label beside each word here was a second copy of that, read by nothing once the editor started asking
    /// the route.</para></summary>
    public static readonly string[] All =
    [
        Solid, Layered, TeamTint, Voronoi, Cell, Noise, Turbulence, Electric,
        WallRun, WallDiagonal, Checker, LogChecker, LaidLog, WallFrame,
    ];
}

/// <summary>The four themeable buckets a theme binds a style to (bedrock is fixed and never themed). Same
/// reasoning as <see cref="MaterialKind"/>: the <c>theme_bucket.bucket</c> column, the bindings on the wire and
/// the client's editor all name the same four.</summary>
public static class ThemeBuckets
{
    public const string Rim = "rim";
    public const string Surface = "surface";
    public const string Wall = "wall";
    public const string Fill = "fill";

    /// <summary>The buckets top-down: the cap, the interior stack under it, the riser it sits on, then the body
    /// everything else falls to.</summary>
    public static readonly string[] All = [Rim, Surface, Wall, Fill];

    /// <summary>Whether the bucket claims a configurable number of top courses — the rim and the surface do; the
    /// wall's depth is the riser it finds and the fill takes what is left.</summary>
    public static bool HasDepth(string bucket) => bucket is Rim or Surface;
}

/// <summary>Which edges a theme's rim caps — the wire words for the painter's <c>RimEdges</c>, shared by the
/// <c>theme.rim_edges</c> column, the theme JSON a map snapshots, and both authoring surfaces.
/// <see cref="Void"/> caps only where the ground borders the void, so a staircase of stacked plateaus takes
/// one rim around its outside rather than a lip on every tread; <see cref="Drop"/> caps wherever the ground
/// falls away; <see cref="Boundary"/> caps every plateau boundary, a face against a structure included.</summary>
public static class RimEdgeModes
{
    public const string Void = "void";
    public const string Drop = "drop";
    public const string Boundary = "boundary";

    /// <summary>The modes narrowest first — how many edges each one calls a rim.</summary>
    public static readonly string[] All = [Void, Drop, Boundary];

    /// <summary>What each mode caps, in the words an authoring surface offers it in.</summary>
    public static string LabelOf(string mode) => mode switch
    {
        Void => "Only where the ground meets the void",
        Boundary => "Every plateau boundary",
        _ => "Wherever the ground drops",
    };

    /// <summary>Fold an unknown or absent mode down to the default, so a hand-edited theme shows a rim rather
    /// than throwing the editor.</summary>
    public static string Canonical(string? mode) => All.Contains(mode) ? mode! : Drop;
}

/// <summary>The parts of a room shell a style binds courses to (the pad and the doorway are stamped over them
/// and are never a part). Same reasoning as <see cref="ThemeBuckets"/>: the <c>room_style_course.part</c>
/// column, the courses on the wire and the client's editor all name the same set.</summary>
public static class RoomParts
{
    public const string Floor = "floor";
    public const string Wall = "wall";
    public const string Roof = "roof";

    /// <summary>The three a house has that a plain shell does not: the corner columns that frame it, the
    /// footing it meets the ground on, and the trim along its roof's edge. Each takes one material rather
    /// than a stack — a post is a post all the way up — so only the first course of theirs is read.</summary>
    public const string Post = "post";
    public const string Sill = "sill";
    public const string Verge = "verge";

    /// <summary>The triangle of wall a sloped roof leaves standing at each end — the face a timbered or
    /// shingled gable is made of. It is a part rather than a course of the wall because it is the one piece of
    /// wall the <em>roof</em> decides the shape of, and because a wall that bands as it rises has no band left
    /// to give it: the courses run out at the wall's top and the gable is whatever the last one was.</summary>
    public const string Gable = "gable";

    /// <summary>The three zones of the floor's <em>top</em> course — the ring hugging the walls, the open
    /// floor across the rest, and a plate centred in it. They are parts rather than courses of the floor
    /// because they divide it in plan and the floor's own stack divides it in depth; each takes one material,
    /// so only the first course bound to one is read.</summary>
    public const string Border = "border";
    public const string Field = "field";
    public const string Inlay = "inlay";

    /// <summary>The slab laid across a storey's interior to carry the one above it — the ceiling of this room
    /// seen from below and the floor of that one seen from above. A storey's rather than a house's, because a
    /// building may close its ground floor in one thing and the storey over it in another, and the top storey
    /// has none at all: the roof is what closes that one. Unbound it is the house floor's own top material,
    /// which is what every storey stack was before the slab had a name.
    ///
    /// <para>One material rather than a stack, and the plan zones are why: what a player actually stands on
    /// up there is the slab's top course, and that is already divided by the storey <em>above</em>'s border,
    /// field and inlay. A stack here would be a second answer to a question the zones have settled.</para></summary>
    public const string Deck = "deck";

    /// <summary>The parts bottom-up, the order a shell is stamped in.</summary>
    public static readonly string[] All =
        [Floor, Field, Border, Inlay, Wall, Deck, Gable, Roof, Post, Sill, Verge];

    /// <summary>The part a name means, or an empty string for one that names none — what a preview asked to
    /// cut to an unknown part falls back to, which is the whole building.</summary>
    public static string Canonical(string? part) => All.Contains(part) ? part! : "";
}

/// <summary>
/// The sample footprints a house style is previewed on, by the <b>shell</b> each names — the room a player
/// stands in, which is what an author reads a building at. A style states nothing about the rectangle it will
/// be stamped over, only storey heights and a roof's pitch, while a ridge follows that rectangle's own
/// proportions: one style on a square and on a long shell is two different roofs rather than one roof
/// stretched.
///
/// <para>The piece a shell is resolved from is two blocks larger on each axis (<c>WX1</c>), and 6×6 is the
/// least shell a room may be (<c>WX2</c>) — which is why the smallest offered is exactly that.</para>
/// </summary>
public static class HouseFootprints
{
    public const string Small = "6x6";
    public const string Square = "8x8";
    public const string Long = "10x15";
    public const string Large = "16x16";

    /// <summary>The four, smallest first, each as the shell it draws.</summary>
    public static readonly (string Id, int Width, int Depth)[] All =
        [(Small, 6, 6), (Square, 8, 8), (Long, 10, 15), (Large, 16, 16)];

    /// <summary>The shell a card is judged at, and what a caller naming none is answered on.</summary>
    public const string Default = Square;

    /// <summary>The shell a word names, folding anything outside the set onto <see cref="Default"/> — a
    /// picture is how a style is looked at rather than part of the question, so a bad word costs the wrong
    /// proportion and never the picture.</summary>
    public static string Canonical(string? id)
        => All.FirstOrDefault(one => string.Equals(one.Id, id, StringComparison.OrdinalIgnoreCase)).Id ?? Default;

    /// <summary>The piece a footprint's shell resolves out of: two blocks larger on each axis.</summary>
    public static (int Width, int Depth) PieceOf(string? id)
    {
        var found = All.First(one => one.Id == Canonical(id));
        return (found.Width + 2, found.Depth + 2);
    }
}

/// <summary>Which roof a stored style asks for — the wire words for <c>RoofForm</c>. Every one of them is a
/// height field over the same plan, so the list grows without the stamper branching.</summary>
/// <summary>
/// Which distance a band stack is read along — the wire words for <c>BandAxis</c>. The stack states its bands
/// and where they run out and never the axis, so the material doing the reading is where the choice belongs.
/// </summary>
/// <summary>Which of the two trees a recipe is — the wire words for <c>TreeForm</c>. The two are genuinely two
/// trees rather than one with a switch: a template is a vanilla species with a drawn canopy profile, a grown
/// one is a recursive skeleton, and neither is a knob setting of the other.</summary>
public static class TreeForms
{
    /// <summary>The vanilla tree of a named species.</summary>
    public const string Template = "template";

    /// <summary>The grown skeleton, in a chosen wood.</summary>
    public const string Grown = "grown";

    public static readonly string[] All = [Template, Grown];

    public static string Describe(string? form) => Canonical(form) == Grown
        ? "Grown from a branch skeleton you shape"
        : "A vanilla tree of a named species";

    public static string Canonical(string? form) => All.Contains(form) ? form! : Template;
}

/// <summary>The six vanilla species a template tree may be — the wire words for <c>DressingPalette.Species</c>.
/// The profiles are what separate them: a notched cone is a spruce and a flat umbrella on a leaning trunk is an
/// acacia.</summary>
public static class TreeSpeciesNames
{
    public static readonly string[] All = ["oak", "birch", "spruce", "jungle", "acacia", "dark oak"];

    public static string Canonical(string? name) => All.Contains(name) ? name! : All[0];
}

/// <summary>The six woods a tree of either form is cut from — the wire words for
/// <c>DressingPalette.Woods</c>. Separate from the species because both trees need a wood and only one has a
/// species.</summary>
public static class TreeWoodNames
{
    public static readonly string[] All = ["oak", "birch", "spruce", "jungle", "acacia", "dark oak"];

    public static string Canonical(string? name) => All.Contains(name) ? name! : All[0];
}

/// <summary>An erratic's shape — the wire words for <c>BoulderForm</c>.</summary>
public static class BoulderForms
{
    /// <summary>One rounded mass standing on the ground, weathered into broad facets.</summary>
    public const string Round = "round";

    /// <summary>The same mass broken up — angular and heavily weathered.</summary>
    public const string Angular = "angular";

    /// <summary>Wide, flat lobes with their middle at the surface: a low outcrop rather than a rock.</summary>
    public const string Outcrop = "outcrop";

    /// <summary>Three shrinking lobes stacked up.</summary>
    public const string Cairn = "cairn";

    public static readonly string[] All = [Round, Angular, Outcrop, Cairn];

    public static string Describe(string? form) => Canonical(form) switch
    {
        Angular => "Broken up and heavily weathered",
        Outcrop => "Wide flat lobes, half in the ground",
        Cairn => "Three shrinking lobes stacked",
        _ => "One rounded mass, broadly facetted",
    };

    public static string Canonical(string? form) => All.Contains(form) ? form! : Round;
}

public static class BandAxes
{
    /// <summary>Down from the top of the bucket: grass over two dirt.</summary>
    public const string Depth = "depth";

    /// <summary>In from the landmass's void-facing edge: a cobble rim, two rings of stone brick, then a
    /// field. The bands run as concentric rings round a shape rather than as courses down a column.</summary>
    public const string Inward = "inward";

    /// <summary>Up from a stated world Y, so the bands are pinned to the world rather than to the column and
    /// one span carries a stack of colours landing at the same height in every column it covers.</summary>
    public const string Height = "height";

    /// <summary>The axes in the order the editor offers them: the reading every stack had before there was a
    /// second one, then the two that were added to it.</summary>
    public static readonly string[] All = [Depth, Inward, Height];

    /// <summary>What each reads along, in the words the picker offers it in.</summary>
    public static string Describe(string? axis) => Canonical(axis) switch
    {
        Inward => "In from the edge — rings round the shape",
        Height => "Up from a world height",
        _ => "Down the column from the top",
    };

    /// <summary>What one band claims, along that axis — the caption its number wants.</summary>
    public static string Extent(string? axis) => Canonical(axis) switch
    {
        Inward => "Rings",
        _ => "Courses",
    };

    public static string Canonical(string? axis) => All.Contains(axis) ? axis! : Depth;
}

/// <summary>What a band stack answers past its last band — the wire words for <c>BandEnding</c>. Not implied by
/// the axis, which is why it is stated: the two are independent choices.</summary>
public static class BandEndings
{
    /// <summary>The last band claims everything beyond it — right where the stack owns its whole space.</summary>
    public const string Repeat = "repeat";

    /// <summary>Nothing is claimed past the last band, and whatever is under the stack shows — right where the
    /// stack is a band inside a larger space.</summary>
    public const string HandOver = "handOver";

    public static readonly string[] All = [Repeat, HandOver];

    public static string Describe(string? ending) => Canonical(ending) == HandOver
        ? "Nothing more is claimed"
        : "The last band repeats";

    public static string Canonical(string? ending) => All.Contains(ending) ? ending! : Repeat;
}

public static class RoofForms
{
    public const string Gable = "gable";
    public const string Flat = "flat";
    public const string Hip = "hip";
    public const string Gambrel = "gambrel";
    public const string Shed = "shed";
    public const string Saltbox = "saltbox";

    /// <summary>The forms in the order the editor offers them: the lid every shell has always had, then the
    /// slopes, simplest first.</summary>
    public static readonly string[] All = [Flat, Gable, Hip, Shed, Gambrel, Saltbox];

    /// <summary>What each looks like, in the words the picker offers it in.</summary>
    public static string Describe(string? form) => Canonical(form) switch
    {
        Gable => "Two slopes to a ridge",
        Hip => "Four slopes, one off every wall",
        Shed => "One plane, leaning off the front",
        Gambrel => "A barn: steep, then shallow",
        Saltbox => "A gable with its ridge off centre",
        _ => "A flat lid",
    };

    /// <summary>Fold an unknown or absent form down to the flat lid, so a hand-edited row still stamps.</summary>
    public static string Canonical(string? form) => All.Contains(form) ? form! : Flat;
}

/// <summary>Which wall a porch takes its strip from — the wire words for <c>RoomEdge</c>, plus the one that is
/// not an edge at all. <see cref="Front"/> is the default and the answer almost every style wants: the wall the
/// doors are cut through, whichever that turns out to be once the frame has spoken. Naming a compass edge
/// instead pins the porch to a side of the world regardless of where the building's door ends up.</summary>
public static class PorchEdges
{
    public const string Front = "front";
    public const string NegZ = "negZ";
    public const string PosZ = "posZ";
    public const string NegX = "negX";
    public const string PosX = "posX";

    public static readonly string[] All = [Front, NegZ, PosZ, NegX, PosX];

    public static string Describe(string? edge) => Canonical(edge) switch
    {
        NegZ => "The −z wall",
        PosZ => "The +z wall",
        NegX => "The −x wall",
        PosX => "The +x wall",
        _ => "Whichever wall the door is on",
    };

    public static string Canonical(string? edge) => All.Contains(edge) ? edge! : Front;
}

/// <summary>Which window a stored style asks for — the wire words for <c>WindowForm</c>. Two of the three are
/// openings rather than glass: a lattice of stairs turned back to back, and a band between a slab sill and a
/// slab lintel.</summary>
public static class WindowForms
{
    public const string None = "none";
    public const string StairLattice = "stairLattice";
    public const string SlabBanded = "slabBanded";
    public const string Pane = "pane";

    /// <summary>The hole and nothing in it — cut and left, which is not the same as asking for none.</summary>
    public const string Open = "open";

    /// <summary>An opening with its two top corners rounded off by upside-down stairs — the door head's trick
    /// on a window.</summary>
    public const string Arched = "arched";

    public static readonly string[] All = [None, StairLattice, SlabBanded, Pane, Open, Arched];

    public static string Describe(string? form) => Canonical(form) switch
    {
        StairLattice => "2×2 of stairs, open in the middle",
        SlabBanded => "A band between a slab sill and lintel",
        Pane => "Panes, glazed",
        Open => "An opening, nothing in it",
        Arched => "An opening, its top corners rounded",
        _ => "No windows",
    };

    public static string Canonical(string? form) => All.Contains(form) ? form! : None;
}

/// <summary>How a doorway's top course is dressed (<c>door_head_form</c>). A head is what carries the wall over
/// the opening; the door is what fills it, and they are different questions with different closed sets.</summary>
public static class DoorHeadForms
{
    public const string None = "none";
    public const string Arched = "arched";

    public static readonly (string Id, string Name)[] All =
    [
        (None, "Square"),
        (Arched, "Arched — stairs in the corners"),
    ];

    public static string Canonical(string? form) => All.Any(entry => entry.Id == form) ? form! : None;
}

/// <summary>What spans the middle of an arched head on an opening wider than its two corners.</summary>
public static class DoorHeadFills
{
    public const string UpperSlab = "upperSlab";
    public const string Solid = "solid";

    public static readonly (string Id, string Name)[] All =
    [
        (UpperSlab, "Upside-down slab"),
        (Solid, "A whole block"),
    ];

    public static string Canonical(string? fill) => All.Any(entry => entry.Id == fill) ? fill! : UpperSlab;
}

/// <summary>
/// What kind of ground a relief is meant to be. The word an island states about itself and the word a read
/// answers back, so the two can be compared: an island that says <c>plain</c> and measures <c>hills</c> is a
/// board that got away from its author.
///
/// <para>It is vocabulary rather than one consumer's constant because three parties spell it — the relief
/// document states it, the read-back answers it, and the client shows both — and the leaf is where all three
/// reach. What each word <em>means</em> in blocks is not here: that is a measurement, and it lives with the
/// read that takes it (<c>ReliefReadback</c>).</para>
/// </summary>
public static class Landform
{
    /// <summary>Ground a player crosses without thinking about it. A board's default reading.</summary>
    public const string Plain = "plain";

    /// <summary>Ground that rises and falls enough to break a sight line and shape a route.</summary>
    public const string Rolling = "rolling";

    /// <summary>Ground with real climbs in it — a route goes round or over, and the choice matters.</summary>
    public const string Hills = "hills";

    /// <summary>Ground the map is built against rather than on: a range, a rim, a wall of land.</summary>
    public const string Mountain = "mountain";

    /// <summary>The four in order of how much ground they move, which is the order they are measured in.</summary>
    public static readonly string[] All = [Plain, Rolling, Hills, Mountain];

    /// <summary>Whether a word is one of the four.</summary>
    public static bool IsKnown(string? word) => word is { Length: > 0 } && All.Contains(word);
}
