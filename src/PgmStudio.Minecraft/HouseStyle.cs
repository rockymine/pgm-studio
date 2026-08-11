using System.Text.Json.Serialization;
using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>
/// Which roof a house wears. They are the same building underneath — every one of them is a height field over
/// the same plan (<see cref="RoofField"/>) — which is why one stamper builds all six.
/// </summary>
public enum RoofForm
{
    /// <summary>Two slopes meeting at a ridge that runs the length of the building.</summary>
    Gable,

    /// <summary>A lid over the walls: the shell a wool structure and a spawn have always had, and the only form
    /// that can carry a hole.</summary>
    Flat,

    /// <summary>Four slopes, one off every wall, meeting at a ridge along the building's length — a pyramid
    /// where the footprint is square, because the ridge has no length left to run along.</summary>
    Hip,

    /// <summary>A barn roof: each slope steep for its first courses in from the eave, then shallow to the
    /// ridge, so the building carries a usable volume under a roof that still sheds.</summary>
    Gambrel,

    /// <summary>One plane, low at the front wall and climbing to the back — a lean-to, and what a porch wears
    /// by default.</summary>
    Shed,

    /// <summary>A gable whose two slopes climb at different rates, so they meet off centre: short and steep
    /// over the front, long and shallow over the back.</summary>
    Saltbox,
}

/// <summary>
/// The course players stand on, in plan. The courses <em>below</em> it are the floor part's stack, which reads
/// downward and is the same everywhere; this is the one course that varies across the room, divided into zones
/// by how far a cell stands from the walls: a <see cref="Border"/> ring just inside them, a
/// <see cref="Field"/> across the rest, and an <see cref="Inlay"/> — a hearth, a rug, a plate of the room's
/// own colour — centred in it.
///
/// <para>Zoning is here rather than in a material because <b>a material does not know the room</b>. A checker,
/// a noise field and a wall run all resolve from the cell's own coordinates, so they can pattern a floor but
/// cannot put a border one block inside a wall that moves with the footprint. Anything that is a pattern stays
/// a material and is bound to <see cref="Field"/>; only what needs the room's own bounds is a zone.</para>
/// </summary>
public sealed record FloorSurface
{
    /// <summary>What the open floor is finished with, or null to leave the floor part's own top course showing.</summary>
    public TerrainMaterial? Field { get; init; }

    /// <summary>A ring hugging the walls, or null for a floor that runs to them unbroken.</summary>
    public TerrainMaterial? Border { get; init; }

    public int BorderWidth { get; init; } = 1;

    /// <summary>A centred plate, or null for none.</summary>
    public TerrainMaterial? Inlay { get; init; }

    /// <summary>How far in from the walls the inlay starts. Two leaves a walkable block between it and the
    /// border, which is what keeps it reading as laid <em>in</em> the floor rather than as a second floor.</summary>
    public int InlayInset { get; init; } = 2;

    /// <summary>The floor a style that never asked for one gets: the floor part, unzoned.</summary>
    public static FloorSurface Plain { get; } = new();

    /// <summary>Whether nothing is zoned — the fast path, and what a preview draws when a style has no floor
    /// of its own.</summary>
    public bool IsPlain => Field is null && Border is null && Inlay is null;

    /// <summary>The material a cell <paramref name="ring"/> blocks in from the nearest wall takes, or null to
    /// leave the floor part showing. A cell on the wall line is ring 0 and is never zoned: the walls stand on
    /// it, so what it is made of is the floor part's business and not the surface's.</summary>
    public TerrainMaterial? At(int ring)
    {
        if (ring <= 0) return null;
        if (Border is not null && ring <= Math.Max(1, BorderWidth)) return Border;
        if (Inlay is not null && ring >= Math.Max(1, InlayInset)) return Inlay;
        return Field;
    }
}

/// <summary>
/// A porch: a strip of the building's own footprint, given up by the walls and left open.
///
/// <para>It is <b>taken out of</b> the house rather than added to it, and that is the whole model. A room's
/// footprint comes from the piece it sits on and a style may never change it (WX1), so a porch that grew
/// outward would be a style deciding a footprint. Taken inward it is not: the foundation, the sill and the
/// floor still cover the whole footprint, the walls simply stand <see cref="Depth"/> blocks back from one of
/// them, and the strip they gave up is a deck with posts, a rail and its own roof over it.</para>
/// </summary>
public sealed record PorchStyle
{
    /// <summary>How deep a strip the walls give up, in blocks.</summary>
    public int Depth { get; init; } = 2;

    /// <summary>How far the deck stops short of each end of that wall. Zero runs it the building's full width;
    /// one or two pull it in to a porch that reads as a feature of the front rather than as the front itself.</summary>
    public int Inset { get; init; }

    /// <summary>Which wall gives the strip up, or null for the one the door is on — which is what a porch is
    /// for and what every house on the corpus does.</summary>
    public RoomEdge? Edge { get; init; }

    /// <summary>The canopy over the deck. Its ridge is seated under the house's eave whatever form it is, so a
    /// shed leans off the wall and a gable fronts the building with its own little end.</summary>
    public RoofForm Roof { get; init; } = RoofForm.Shed;

    /// <summary>The fence along the deck's open edges, or 0 for a deck left open to step off anywhere. The gap
    /// in front of the door is cut whatever this is.</summary>
    public int RailBlock { get; init; } = Blocks.OakFence;
}

/// <summary>
/// One storey of a building: the air a player stands in, the walls around it, the windows through them and
/// how its own floor is divided.
///
/// <para>A storey is measured by its <see cref="Clear"/> — the blocks of air, not the courses of wall — because
/// that is the number an author is actually deciding and the only one with a floor under it: a room a player
/// cannot stand up in is not a room, so three is the least it may be. The courses follow from it. A storey with
/// another above it spends one more course on the slab that separates them, and the top storey spends none,
/// because the roof is its lid.</para>
///
/// <para>Windows are the storey's own and are seated in its own frame, so a sill of two is two blocks above
/// <em>this</em> floor whichever storey it is. The seater needed no changes for that: it already takes a sill
/// and a wall height, and a storey is just a smaller wall.</para>
/// </summary>
public sealed record Storey
{
    /// <summary>Blocks of air between this floor and the next thing over it. Three is the floor — the room has
    /// to be stood up in — and there is no ceiling.</summary>
    public int Clear { get; init; } = 3;

    /// <summary>The wall around it, read upward from this storey's own floor. Its extent is ignored: a storey's
    /// height is its <see cref="Clear"/>, and the courses are counted from that.</summary>
    public RoomPart? Wall { get; init; }

    /// <summary>The four corner columns of this storey, or null for corners that are wall like the rest of it.
    /// Per storey rather than per building because a storey is the unit an author composes: a timbered ground
    /// floor under plain rooms is one preset beside another, and a post that belonged to the building could
    /// not be part of either.</summary>
    public TerrainMaterial? Post { get; init; }

    /// <summary>The windows through that wall, or none.</summary>
    public WindowStyle? Windows { get; init; }

    /// <summary>How this storey's floor is divided in plan.</summary>
    public FloorSurface? Surface { get; init; }

    /// <summary>The slab laid over this storey to carry the next, or null for the floor part's own top course.
    /// The top storey has none: the roof is what closes it.</summary>
    public TerrainMaterial? Ceiling { get; init; }

    /// <summary>Set only on the storey a plain shell resolves to — a building described by a wall height rather
    /// than by rooms. The distinction is real: a wall height is literal, so a two-course shed is two courses,
    /// while a room's clear is not, because a room has to be stood up in. Marking the fallback rather than
    /// branching lets the stamper walk one list whichever it was given.</summary>
    [JsonIgnore]
    public bool Shell { get; init; }

    /// <summary>The air a player stands in — never less than the three blocks a room needs, unless this is the
    /// <see cref="Shell"/> storey, whose height was stated as a wall and is taken as stated.</summary>
    public int Headroom => Shell ? Math.Max(1, Clear) : Math.Max(3, Clear);

    /// <summary>How many courses of wall this storey spends: its headroom, plus the course its slab sits in
    /// when something stands above it.</summary>
    public int Courses(bool topmost) => Headroom + (topmost ? 0 : 1);
}

/// <summary>
/// How a house is finished: a part or a material per piece of it, and the few numbers that decide its
/// proportions.
///
/// <para>The walls and the floor are <see cref="RoomPart"/> stacks rather than single materials. A stack is
/// not needed to band a wall — a <see cref="LayeredMaterial"/> does that, and any pattern may — but it counts
/// its courses <b>up from the floor</b>, so a stripe written at the fourth course stays at the fourth course
/// when the wall grows instead of sliding with the top. That is what it is for: pinning a band without
/// working out which layer combination lands where.</para>
/// </summary>
public sealed record HouseStyle
{
    // The species nibble the wood blocks share. Not in Blocks because nothing else has wanted them yet.
    private const int Oak = 0, Spruce = 1, DarkOak = 5;
    private const int Planks = Blocks.Planks;

    /// <summary>The course the walls stand on, laid one block proud of them on every side, so the building
    /// meets the ground on a footing instead of stopping dead at it.</summary>
    public TerrainMaterial Sill { get; init; } = new SolidMaterial(Blocks.Cobblestone);

    /// <summary>The infill between the posts, upward from the floor. Its extent is the wall's height.</summary>
    public RoomPart Wall { get; init; } = RoomPart.Of(new SolidMaterial(Planks, Spruce), 5);

    /// <summary>The four corner columns, or null for a building whose corners are wall like the rest of it.
    /// A house reads as framed because its corners differ from the panel between them, which is what every
    /// hand-built house on the corpus does; a plain shell has no such thing and says so by leaving this
    /// unset.</summary>
    public TerrainMaterial? Post { get; init; } = new SolidMaterial(Blocks.Log, Oak);

    /// <summary>The triangle of wall a sloped roof leaves standing at each end, or null to carry the wall's own
    /// top course up into it.
    ///
    /// <para>Its own material because the gable is the one piece of wall the <b>roof</b> shapes, and because a
    /// wall's stack has nothing left to say about it: the courses run out at the wall's top, so a wall that
    /// bands as it rises goes flat the moment it turns into a gable. Naming it separately is what makes the
    /// timbered gable over a plain wall — the thing nearly every hand-built house on the corpus does — sayable
    /// at all. Unset it is the wall's top course, which is what every shell was before there was a name for
    /// it.</para></summary>
    public TerrainMaterial? Gable { get; init; }

    /// <summary>The body of each roof slope.
    ///
    /// <para><b>A slope that climbs a whole block per block must be laid in whole blocks.</b> A slab fills only
    /// the lower half of the cube it sits in, so a course of slabs stepping up a full block leaves an open half
    /// between every pair and the roof can be seen straight through. Stairs are the other material that closes
    /// a 45° step; slabs belong on a slope that rises half a block at a time, which is a different roof.</para></summary>
    public TerrainMaterial Roof { get; init; } = new SolidMaterial(Planks, Spruce);

    /// <summary>The roof's own border — its eave course and its two verges. A roof laid in one material reads
    /// flat; bordering it is what gives the slope an edge to end on.</summary>
    public TerrainMaterial Verge { get; init; } = new SolidMaterial(Planks, DarkOak);

    /// <summary>Downward from the course players stand on, so a thicker floor digs into what the house sits
    /// on rather than lifting its inside off it.</summary>
    public RoomPart Floor { get; init; } = RoomPart.Of(new SolidMaterial(Planks, Oak));

    /// <summary>How the top course of that floor is divided across the room — a border, a field, an inlay.
    /// Plain by default, which is the floor part showing through unchanged.</summary>
    public FloorSurface Surface { get; init; } = FloorSurface.Plain;

    /// <summary>The windows cut through the walls, or none.</summary>
    public WindowStyle Windows { get; init; } = new();

    /// <summary>The storeys stacked inside the building, or empty for the single storey <see cref="Wall"/>,
    /// <see cref="Windows"/> and <see cref="Surface"/> describe on their own.
    ///
    /// <para>Empty is not a missing answer, it is the ordinary one — a house is one storey until it is told
    /// otherwise, and every style saved before storeys existed reads as exactly the building it always was.
    /// <see cref="Levels"/> is what the stamper walks, so nothing downstream has to know which of the two it
    /// was given.</para></summary>
    public IReadOnlyList<Storey> Storeys { get; init; } = [];

    /// <summary>The storeys this building actually has, bottom up — the list where one was given, else the
    /// single storey the flat parts describe. A storey that names no wall, windows or floor of its own falls
    /// back to the building's, so a stack of identical storeys is a count rather than a repeated description.</summary>
    [JsonIgnore]
    public IReadOnlyList<Storey> Levels => Storeys.Count > 0
        ? [.. Storeys.Select(storey => storey with
        {
            Wall = storey.Wall ?? Wall,
            Post = storey.Post ?? Post,
            Windows = storey.Windows ?? Windows,
            Surface = storey.Surface ?? Surface,
        })]
        : [new Storey
        {
            Shell = true, Clear = Wall.Extent,
            Wall = Wall, Post = Post, Windows = Windows, Surface = Surface,
        }];

    /// <summary>Courses of wall from the floor to the eave — every storey's headroom, plus one slab course
    /// between each pair of them.</summary>
    [JsonIgnore]
    public int WallCourses
    {
        get
        {
            var levels = Levels;
            var total = 0;
            for (var at = 0; at < levels.Count; at++) total += levels[at].Courses(at == levels.Count - 1);
            return Math.Max(1, total);
        }
    }

    /// <summary>The course each storey's own floor sits at, as a layer above the building's floor — 0 for the
    /// ground storey, and the slab course for every storey over it.</summary>
    [JsonIgnore]
    public IReadOnlyList<int> LevelBases
    {
        get
        {
            var levels = Levels;
            var bases = new List<int>(levels.Count) { 0 };
            for (var at = 0; at < levels.Count - 1; at++) bases.Add(bases[at] + levels[at].Courses(false));
            return bases;
        }
    }

    /// <summary>The strip of footprint given up to a porch, or null for a building whose walls stand on the
    /// whole of it.</summary>
    public PorchStyle? Porch { get; init; }

    public RoofForm Form { get; init; } = RoofForm.Gable;

    /// <summary>Whether the line the slopes meet on is laid in the <see cref="Verge"/> rather than the roof's
    /// own material — the capping course a real ridge is finished with. A flat lid has no ridge to cap.</summary>
    public bool RidgeCap { get; init; }

    /// <summary>Whether a flat roof carries a centred hole, the way the shipped shell does — the light a
    /// windowless room otherwise has none of. A gable has its own volume and never takes one.</summary>
    public bool RoofHole { get; init; }

    /// <summary>How far the roof reaches past the walls. One block is an eave; zero ends the roof flush and
    /// leaves the wall to carry the weather.</summary>
    public int Overhang { get; init; } = 1;

    /// <summary>How steep the slope climbs: courses of rise per block travelled inward. One is the vanilla
    /// pitch; two is a steep alpine roof.</summary>
    public int Pitch { get; init; } = 1;

    public DoorMaterial Door { get; init; } = DoorMaterial.Air;

    /// <summary>The doorway, which is never smaller than two blocks wide by three tall however it is set. A
    /// single-width opening is a gap in a wall rather than a door, and a room a player carries an objective
    /// out of has to read as somewhere to walk through.</summary>
    public int DoorWidth { get; init; } = 2;

    public int DoorHeight { get; init; } = 3;

    /// <summary>The highest course a flat lid reaches, as a layer above the floor. A gable climbs with the
    /// footprint it spans, so ask <see cref="HouseHeights.TopLayerOver"/> for one of those. Derived, so a
    /// snapshot does not carry it.</summary>
    [JsonIgnore]
    public int TopLayer => WallCourses + 1;

    /// <summary>Two styles are the same style when every piece of them matches, the storey stack included
    /// <b>course for course</b>. Spelled out for the same reason <see cref="RoomPart.Equals(RoomPart)"/> is:
    /// the generated equality compares <see cref="Storeys"/> by <em>reference</em>, so a style read back from
    /// its snapshot could never equal the style it was written from — which is the comparison a round trip is
    /// made of, and a whole shell's worth of members would silently ride on it.</summary>
    public bool Equals(HouseStyle? other)
        => other is not null
           && Sill == other.Sill && Wall == other.Wall && Post == other.Post && Gable == other.Gable
           && Roof == other.Roof && Verge == other.Verge && Floor == other.Floor
           && Surface == other.Surface && Windows == other.Windows
           && Storeys.SequenceEqual(other.Storeys)
           && Porch == other.Porch && Form == other.Form
           && RidgeCap == other.RidgeCap && RoofHole == other.RoofHole
           && Overhang == other.Overhang && Pitch == other.Pitch
           && Door == other.Door && DoorWidth == other.DoorWidth && DoorHeight == other.DoorHeight;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Sill); hash.Add(Wall); hash.Add(Post); hash.Add(Gable);
        hash.Add(Roof); hash.Add(Verge); hash.Add(Floor);
        hash.Add(Surface); hash.Add(Windows);
        foreach (var storey in Storeys) hash.Add(storey);
        hash.Add(Porch); hash.Add(Form);
        hash.Add(RidgeCap); hash.Add(RoofHole);
        hash.Add(Overhang); hash.Add(Pitch);
        hash.Add(Door); hash.Add(DoorWidth); hash.Add(DoorHeight);
        return hash.ToHashCode();
    }

    /// <summary>The shipped wool structure: a flat bedrock shell, a wool band at the fourth course, a light
    /// slit at the sixth, and a stained-glass-pane door. No posts and no sill — a shell's corners are wall
    /// like the rest of it, and it meets the ground without a footing.</summary>
    public static HouseStyle Wool { get; } = Shell(Banded(Blocks.Wool));

    /// <summary>The shipped spawn structure: the same shell with a stained-clay band, and an open doorway a
    /// player can walk straight out of.</summary>
    public static HouseStyle Spawn { get; } = Shell(Banded(Blocks.StainedClay)) with
    {
        Door = DoorMaterial.Air,
        DoorHeight = 4,
    };

    /// <summary>The tallest shell the shipped styles build — what a caller clamps a floor against so a stamp
    /// cannot run past the world ceiling.</summary>
    public static int MaxTopLayer { get; } = Math.Max(Wool.TopLayer, Spawn.TopLayer);

    private static HouseStyle Shell(RoomPart wall) => new()
    {
        Form = RoofForm.Flat,
        Wall = wall,
        Floor = RoomPart.Of(new SolidMaterial(Blocks.Bedrock)),
        Roof = new SolidMaterial(Blocks.Bedrock),
        Verge = new SolidMaterial(Blocks.Bedrock),
        Post = null,
        Sill = new SolidMaterial(Blocks.Air),
        Overhang = 0,
        RoofHole = true,
        Door = DoorMaterial.StainedGlassPane,
        DoorHeight = 3,
    };

    /// <summary>The shipped wall: three courses of bedrock, a coloured band, bedrock, an open course, bedrock.
    /// The band and the slit are courses like any other, and a stack counts up from the floor, so the band
    /// stays at the fourth course whatever the wall's height becomes.</summary>
    private static RoomPart Banded(int bandBlock) => new(
    [
        new RoomCourse(new SolidMaterial(Blocks.Bedrock), 3),
        new RoomCourse(new TeamTintedMaterial(bandBlock, new SolidMaterial(Blocks.Bedrock))),
        new RoomCourse(new SolidMaterial(Blocks.Bedrock)),
        new RoomCourse(new SolidMaterial(Blocks.Air)),
        new RoomCourse(new SolidMaterial(Blocks.Bedrock)),
    ], Extent: 7);
}


/// <summary>Courses above the floor this style can reach on a footprint of the given size — what a caller
/// reserves headroom for and what a preview draws to. A flat lid is one course over the wall whatever the
/// footprint; every sloped form climbs with the span it crosses, so the question is asked about a footprint
/// rather than about the style alone, and it is asked of the roof itself rather than of a formula beside it —
/// there are six forms and a second copy of their arithmetic is how one of them comes to be drawn short.</summary>
public static class HouseHeights
{
    public static int TopLayerOver(this HouseStyle style, int width, int depth)
    {
        var wallTop = style.WallCourses;
        if (style.Form == RoofForm.Flat) return wallTop + 1;
        var field = new RoofField(
            style.Form, 0, 0, Math.Max(0, width - 1), Math.Max(0, depth - 1),
            Math.Max(0, style.Overhang), wallTop + 1, Math.Max(1, style.Pitch), RoomEdge.NegZ);
        return field.Peak;
    }
}
