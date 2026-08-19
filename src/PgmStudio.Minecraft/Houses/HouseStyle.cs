using System.Text.Json.Serialization;
using PgmStudio.Domain;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Houses;

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
/// The logs that run <b>out past the corners</b> where two storeys meet — the ends of the beams that carry the
/// floor, left long the way a log building leaves them.
///
/// <para>In plan the seam reads as a <b>hash</b>: the walls are the square in the middle of it, and at each of
/// the four corners two log ends carry on outward, one along each axis. Eight in all, and each shows its
/// <em>sawn end</em> rather than its bark, because that is what the end of a log is — the one place in the
/// building where a cut face outward is the point rather than the mistake.</para>
///
/// <para>The course <em>inside</em> the wall is not this. That is an ordinary course of the wall's own stack,
/// laid in a <see cref="LaidLogMaterial"/> so the log follows the wall and shows bark; this is only what
/// happens where the wall stops. Keeping them apart is what lets a beam course run in one material and its
/// ends in another, and lets a building have the course without the ends or the other way about.</para>
///
/// <para><b>It is the one thing a house writes outside its own footprint.</b> Every other block a style lays
/// falls inside the walls plus the roof's overhang; these reach one block past each corner, which is why they
/// are asked for rather than assumed.</para>
/// </summary>
public sealed record BeamStyle
{
    /// <summary>The log the ends are cut from, or -1 for a building whose storeys meet without them.</summary>
    public int Block { get; init; } = -1;

    /// <summary>That log's own species nibble. Which way it lies is the stamper's.</summary>
    public int Data { get; init; }

    /// <summary>How far past each corner the ends run.</summary>
    public int Reach { get; init; } = 1;

    /// <summary>Whether this building has them at all.</summary>
    public bool Any => Block >= 0 && Reach > 0;
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

    /// <summary>The plate this storey <b>stands on</b> — one course, infilled across the interior at a level
    /// the walls already span, which is what a floor is when a building is put up rather than drawn.
    ///
    /// <para><b>One plate, one owner.</b> The course between two storeys is the ceiling of the lower seen from
    /// below and the floor of the upper seen from above, and a block has only one identity — so it belongs to
    /// the storey standing on it. Named the other way it had two: its material came from the storey below and
    /// its zoning from the storey above, with the upper one winning wherever it bothered to speak. The preset
    /// that set it wrote <c>// the deck underfoot</c> beside the word <c>Ceiling</c>, which is the whole
    /// argument in one comment.</para>
    ///
    /// <para>The ground storey's deck is the building's own floor, so unset takes the floor part's top course.
    /// Nothing is laid over the topmost storey: the roof is what closes it.</para></summary>
    public TerrainMaterial? Deck { get; init; }

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
/// What a building stands on: the plate under its floor, how that floor's top course is divided, and the
/// footing that rings it.
///
/// <para><b>The plate is the floor.</b> It claims downward from the course players walk on, so a thicker one
/// digs into the ground the house sits on rather than lifting its inside off it, and its top course is what a
/// ground storey's feet are on — which is why a ground storey names no <see cref="Storey.Deck"/>: this is its
/// deck. A house always has one, because a building with no floor is a building standing in a hole.</para>
///
/// <para><b>The footing is the foundation proper, and it is optional.</b> One course ringing the plate a block
/// proud on every side, so the building meets the ground on something instead of stopping dead at it. Null is
/// no footing — a real state rather than a bare air material standing in for one, which is what a building
/// seated into terrain wants (author). <see cref="Depth"/> and what the plate is laid in are the two things
/// that vary about it, and both are the plate's.</para>
/// </summary>
public sealed record Foundation
{
    /// <summary>Downward from the course players stand on. Its top course is the ground storey's deck.</summary>
    public RoomPart Plate { get; init; } = RoomPart.Of(new SolidMaterial(Blocks.Planks));

    /// <summary>How that top course is divided across the room — a border, a field, an inlay. Plain by
    /// default, which is the plate showing through unchanged.</summary>
    public FloorSurface Surface { get; init; } = FloorSurface.Plain;

    /// <summary>The course ringing the plate one block proud, or null for a building that meets the ground
    /// without one.</summary>
    public TerrainMaterial? Footing { get; init; } = new SolidMaterial(Blocks.Cobblestone);

    /// <summary>How far the plate claims downward — the whole of what "how deep is the foundation" means.</summary>
    [JsonIgnore]
    public int Depth => Math.Max(1, Plate.Extent);

    /// <summary>What a ground storey stands on, and what a deck falls back to.</summary>
    [JsonIgnore]
    public TerrainMaterial Deck => Plate.At(0).Material;

    /// <summary>A floor and nothing round it: the plate alone, which is what a building seated into finished
    /// terrain wants.</summary>
    public Foundation WithoutFooting() => this with { Footing = null };
}

/// <summary>
/// The roof a building wears: its shape, how steep it climbs, how far it reaches past the walls, and what
/// every part of it is laid in.
///
/// <para><b>The shape is <see cref="RoofField"/>'s and the finish is here.</b> That type answers what course
/// each column tops out at, one formula per form over the same two distances; this says which form to ask
/// for, at what pitch, and what to lay the answer in. Eleven fields sat loose in the house's own record — one
/// arriving per feature over as many changes, so the record read as its own changelog — and they are one
/// thing: everything above the eave.</para>
///
/// <para>A wing may override three of them for itself (<see cref="WingSpec"/>), which is why the form, the
/// pitch and the slab resolve together as one decision rather than three.</para>
/// </summary>
public sealed record RoofStyle
{
    private const int Planks = Blocks.Planks;
    private const int Spruce = 1, DarkOak = 5;

    public RoofForm Form { get; init; } = RoofForm.Gable;

    /// <summary>How steep the slope climbs per block travelled inward — courses on a roof laid in cubes, and
    /// <em>half</em> courses on one that names a <see cref="Slab"/>. One is the vanilla pitch; two is a
    /// steep alpine roof, or on a slab roof the same 45° a cube roof gets at one.</summary>
    public int Pitch { get; init; } = 1;

    /// <summary>The slab a roof steps in, or -1 for a roof laid in whole blocks.
    ///
    /// <para>Naming one puts the roof on <b>half courses</b>: it climbs half a block per block travelled and
    /// lays this slab on every odd step, with the style's own <see cref="Body"/> filling the cubes between. It
    /// is the gentler slope a slab is actually for — at a whole block of rise a course of slabs leaves an open
    /// half between every pair and the roof can be seen straight through, which is why
    /// <see cref="Body"/> documents stairs and slabs as different roofs.</para>
    ///
    /// <para>A block id rather than a material, for the reason a window's is: which half of its cube a slab
    /// fills is <b>geometry</b>, and a material resolving that from where the cell sits would lay half of them
    /// upside down.</para></summary>
    public int Slab { get; init; } = -1;

    /// <summary>That slab's own variant nibble — which wood, which stone. The half it fills is the stamper's
    /// and is not part of this.</summary>
    public int SlabData { get; init; }

    /// <summary>How far the roof reaches past the walls. One block is an eave; zero ends the roof flush and
    /// leaves the wall to carry the weather.</summary>
    public int Overhang { get; init; } = 1;

    /// <summary>Whether the line the slopes meet on is laid in the <see cref="Verge"/> rather than the roof's
    /// own material — the capping course a real ridge is finished with. A flat lid has no ridge to cap.</summary>
    public bool RidgeCap { get; init; }

    /// <summary>Whether a flat roof carries a centred hole, the way the shipped shell does — the light a
    /// windowless room otherwise has none of. A gable has its own volume and never takes one.</summary>
    public bool Hole { get; init; }

    /// <summary>The body of each roof slope.
    ///
    /// <para><b>A slope that climbs a whole block per block must be laid in whole blocks.</b> A slab fills only
    /// the lower half of the cube it sits in, so a course of slabs stepping up a full block leaves an open half
    /// between every pair and the roof can be seen straight through. Stairs are the other material that closes
    /// a 45° step; slabs belong on a slope that rises half a block at a time, which is a different roof.</para></summary>
    public TerrainMaterial Body { get; init; } = new SolidMaterial(Planks, Spruce);

    /// <summary>The roof's own border — its eave course and its two verges. A roof laid in one material reads
    /// flat; bordering it is what gives the slope an edge to end on.</summary>
    public TerrainMaterial Verge { get; init; } = new SolidMaterial(Planks, DarkOak);

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

    /// <summary>The window cut through each <see cref="Gable"/> face, or none.
    ///
    /// <para>Its own style rather than the walls', because a gable is not a wall: it is a triangle, its height
    /// runs out toward both ends, and the one place a window certainly fits is the middle. So one is cut per
    /// face and centred, where a wall takes as many as its run will hold. Its <see cref="WindowStyle.Sill"/> is
    /// counted from the <b>wall top</b> — the course the gable starts at — since that is the datum an author is
    /// looking at when they place it, and the floor is storeys away by then.</para></summary>
    public WindowStyle GableWindows { get; init; } = new();

    /// <summary>Whether the roof climbs half a block at a time, which is what naming a slab means.</summary>
    [JsonIgnore]
    public bool InHalves => Slab >= 0;
}

/// <summary>
/// The way into a building: how big the opening is, what carries the wall over it, and what stands in it.
///
/// <para>Which wall it is cut through is <b>not</b> here. That is the building's <see cref="HouseStyle.Front"/>
/// — the same wall a <see cref="RoofForm.Shed"/> falls toward and a porch stands on — so a roof asking which
/// way it falls would otherwise have to ask the doorway.</para>
/// </summary>
public sealed record Doorway
{
    /// <summary>What fills the opening, and a closed set because the wool room's break rule whitelists it.
    /// Separate from <see cref="Head"/>, which carries the wall over the opening and is never something a
    /// player goes through.</summary>
    public DoorMaterial Door { get; init; } = DoorMaterial.Air;

    /// <summary>The beam over the opening, or a plain rectangular top where it names no form.</summary>
    public DoorHeadStyle Head { get; init; } = new();

    /// <summary>How wide the opening is asked for. A single-width opening is a gap in a wall rather than a
    /// door, and a room a player carries an objective out of has to read as somewhere to walk through — so
    /// what is actually cut is <see cref="CutWidth"/>.</summary>
    public int Width { get; init; } = 2;

    public int Height { get; init; } = 3;

    /// <summary>The width an opening is actually cut at: never under two, however it is set.</summary>
    [JsonIgnore]
    public int CutWidth => Math.Max(2, Width);

    /// <summary>The clear height, in blocks, this doorway actually leaves once its <see cref="Head"/> is
    /// written into the top course — the author's own arithmetic (<see cref="DoorHeadStyle"/>'s own docstring:
    /// "a three-course door keeps two clear courses"), not a re-derivation. A door with no head, or a head too
    /// small to fit, is not touched and clears its own <see cref="Height"/>; a head that fits takes the top
    /// course and gives back half of it only when the fill is genuinely an upper slab — a claim
    /// <see cref="DoorHeadFill.UpperSlab"/> can make falsely if <see cref="DoorHeadStyle.FillBlock"/> is not
    /// really a slab, which is why this checks the block and not only the enum.</summary>
    [JsonIgnore]
    public decimal Clearance
    {
        get
        {
            if (!Head.Fits(Width, Height)) return Height;
            var genuineSlab = Head.Fill == DoorHeadFill.UpperSlab && BlockFamilies.IsSlab(Head.FillBlock);
            return (Height - 1) + (genuineSlab ? 0.5m : 0m);
        }
    }
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

    /// <summary>What the building stands on: the floor plate, its zoning, and the footing that rings it.</summary>
    public Foundation Foundation { get; init; } = new();

    /// <summary>Everything above the eave: the roof's form, its pitch, its reach and its materials.</summary>
    public RoofStyle Roof { get; init; } = new();

    /// <summary>The infill between the posts, upward from the floor. Its extent is the wall's height.</summary>
    public RoomPart Wall { get; init; } = RoomPart.Of(new SolidMaterial(Planks, Spruce), 5);

    /// <summary>The four corner columns, or null for a building whose corners are wall like the rest of it.
    /// A house reads as framed because its corners differ from the panel between them, which is what every
    /// hand-built house on the corpus does; a plain shell has no such thing and says so by leaving this
    /// unset.</summary>
    public TerrainMaterial? Post { get; init; } = new SolidMaterial(Blocks.Log, Oak);


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
    /// single storey the flat parts describe. A storey that names no wall, windows, floor or deck of its own
    /// falls back to the building's, so a stack of identical storeys is a count rather than a repeated
    /// description.
    ///
    /// <para><b>Every fallback a storey has is here.</b> The deck's used to be resolved inline in the stamper
    /// two hundred lines away and against a different default, so a reader of this list would have concluded a
    /// deck had no fallback at all — which it does, and has always had.</para></summary>
    [JsonIgnore]
    public IReadOnlyList<Storey> Levels => Storeys.Count > 0
        ? [.. Storeys.Select(storey => storey with
        {
            Wall = storey.Wall ?? Wall,
            Post = storey.Post ?? Post,
            Windows = storey.Windows ?? Windows,
            Surface = storey.Surface ?? Foundation.Surface,
            Deck = storey.Deck ?? Foundation.Deck,
        })]
        : [new Storey
        {
            Shell = true, Clear = Wall.Extent,
            Wall = Wall, Post = Post, Windows = Windows, Surface = Foundation.Surface,
            Deck = Foundation.Deck,
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

    /// <summary>The topmost course of wall the stamp actually <b>writes</b>, as a layer above the floor, or
    /// 0 for a stack that writes no wall at all. <see cref="WallCourses"/> is what the geometry
    /// <em>reserves</em> — every storey's headroom, whatever each course resolves to — and the two differ
    /// wherever a stack ends in air: a roof terrace is a storey whose wall is one course of parapet over two
    /// of air (docs/world-export/structures.md §7.6), so it reserves two courses nothing is laid in.
    ///
    /// <para>Walked from the top down, storey by storey, and a storey's <see cref="Storey.Post"/> answers
    /// beside its wall — four columns standing on the deck at the corners are blocks laid at that course even
    /// where the wall between them is air.</para></summary>
    [JsonIgnore]
    public int HighestWallCourse
    {
        get
        {
            var levels = Levels;
            var bases = LevelBases;
            for (var level = levels.Count - 1; level >= 0; level--)
            {
                var storey = levels[level];
                var wall = storey.Wall ?? Wall;
                var post = storey.Post ?? Post;
                for (var course = storey.Courses(level == levels.Count - 1); course >= 1; course--)
                    if (!post.IsAir() || !wall.At(course - 1).Material.IsAir())
                        return bases[level] + course;
            }
            return 0;
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

    /// <summary>The wall this building fronts on — where it cuts its own doorway, and the wall a
    /// <see cref="RoofForm.Shed"/> falls toward. Null leaves it to the proportions, which is the long side.
    ///
    /// <para>It matters most on a <b>hall</b>, and the reason is the row of windows. A wall's windows are spread
    /// and centred on the run between its corners, and a doorway is centred on the same run, so on a long
    /// building the two land on each other: the seats a door meets are dropped rather than shifted, and a
    /// twenty-one-wide wall entered in the middle loses the two windows either side of its door and reads as a
    /// row with a hole in it. Entering at the gable end instead — which is what a hall does anyway — leaves both
    /// long walls to their windows and puts the door where the short wall has no row to break.</para>
    ///
    /// <para>A frame's doors still win over it: where a room states its entries the entry contract is the
    /// frame's, and this is only what a building picks when nothing has picked for it. A
    /// <see cref="PorchStyle.Edge"/> wins too, since a porch names the wall it fronts.</para></summary>
    public RoomEdge? Front { get; init; }

    /// <summary>The logs that run out past the corners where two storeys meet, or none.</summary>
    public BeamStyle Beams { get; init; } = new();

    /// <summary>The way in and what is in it.</summary>
    public Doorway Doorway { get; init; } = new();

    /// <summary>The highest course a flat lid reaches, as a layer above the floor. A gable climbs with the
    /// footprint it spans, so ask <see cref="HouseHeights.TopLayerOver"/> for one of those — which is what
    /// this asks, over a footprint with no run to climb, so the two can never answer differently about the
    /// same style. Derived, so a snapshot does not carry it.</summary>
    [JsonIgnore]
    public int TopLayer => this.TopLayerOver(0, 0);

    /// <summary>Two styles are the same style when every piece of them matches, the storey stack included
    /// <b>course for course</b>. Spelled out for the same reason <see cref="RoomPart.Equals(RoomPart)"/> is:
    /// the generated equality compares <see cref="Storeys"/> by <em>reference</em>, so a style read back from
    /// its snapshot could never equal the style it was written from — which is the comparison a round trip is
    /// made of, and a whole shell's worth of members would silently ride on it.</summary>
    public bool Equals(HouseStyle? other)
        => other is not null
           && Foundation == other.Foundation && Roof == other.Roof
           && Wall == other.Wall && Post == other.Post && Windows == other.Windows
           && Storeys.SequenceEqual(other.Storeys)
           && Porch == other.Porch && Front == other.Front
           && Beams == other.Beams && Doorway == other.Doorway;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Foundation); hash.Add(Roof);
        hash.Add(Wall); hash.Add(Post); hash.Add(Windows);
        foreach (var storey in Storeys) hash.Add(storey);
        hash.Add(Porch); hash.Add(Front);
        hash.Add(Beams); hash.Add(Doorway);
        return hash.ToHashCode();
    }

    /// <summary>The shipped wool structure: a flat bedrock shell, a wool band at the fourth course, a light
    /// slit at the sixth, and a stained-glass-pane door. No posts and no footing — a shell's corners are wall
    /// like the rest of it, and it meets the ground flush rather than standing on a course proud of it.</summary>
    public static HouseStyle Wool { get; } = Shell(Banded(Blocks.Wool));

    /// <summary>The shipped spawn structure: the same shell with a stained-clay band, and an open doorway a
    /// player can walk straight out of.</summary>
    public static HouseStyle Spawn { get; } = Shell(Banded(Blocks.StainedClay)) with
    {
        Doorway = new Doorway { Door = DoorMaterial.Air, Height = 4 },
    };

    /// <summary>The tallest shell the shipped styles build — what a caller clamps a floor against so a stamp
    /// cannot run past the world ceiling.</summary>
    public static int MaxTopLayer { get; } = Math.Max(Wool.TopLayer, Spawn.TopLayer);

    private static HouseStyle Shell(RoomPart wall) => new()
    {
        Wall = wall,
        Foundation = new Foundation
        {
            Plate = RoomPart.Of(new SolidMaterial(Blocks.Bedrock)),
            Footing = null,                        // a shell meets the ground flush
        },
        Roof = new RoofStyle
        {
            Form = RoofForm.Flat,
            Overhang = 0,
            Hole = true,
            Body = new SolidMaterial(Blocks.Bedrock),
            Verge = new SolidMaterial(Blocks.Bedrock),
        },
        Post = null,
        Doorway = new Doorway { Door = DoorMaterial.StainedGlassPane, Height = 3 },
    };

    /// <summary>The shipped wall: three courses of bedrock, a coloured band, bedrock, an open course, bedrock.
    /// The band and the slit are courses like any other, and a stack counts up from the floor, so the band
    /// stays at the fourth course whatever the wall's height becomes.</summary>
    private static RoomPart Banded(int bandBlock) => new(new BandStack(
    [
        new Band(new SolidMaterial(Blocks.Bedrock), 3),
        new Band(new TeamTintedMaterial(bandBlock, new SolidMaterial(Blocks.Bedrock))),
        new Band(new SolidMaterial(Blocks.Bedrock)),
        new Band(new SolidMaterial(Blocks.Air)),
        new Band(new SolidMaterial(Blocks.Bedrock)),
    ]), Extent: 7);
}

/// <summary>Courses above the floor this style can reach on a footprint of the given size — what a caller
/// reserves headroom for and what a preview draws to. A flat lid is one course over the wall whatever the
/// footprint; every sloped form climbs with the span it crosses, so the question is asked about a footprint
/// rather than about the style alone, and it is asked of the roof itself rather than of a formula beside it —
/// there are six forms and a second copy of their arithmetic is how one of them comes to be drawn short.
///
/// <para><b>It answers where the highest block lands, not what the stack reserves.</b> Air is a gap rather
/// than a block everywhere in a style, so a building can reserve courses it never writes — a roof terrace is
/// a parapet storey under a flat lid laid in air, and nothing at all is stamped over its deck. Answering from
/// the reservation refused a tall terraced building headroom it did not need and drew it under a band of
/// empty sky.</para></summary>
public static class HouseHeights
{
    /// <param name="front">The wall the doors are cut through. A <see cref="RoofForm.Gable"/>, a
    /// <see cref="RoofForm.Hip"/> and a <see cref="RoofForm.Gambrel"/> are symmetric and ignore it, but a
    /// <see cref="RoofForm.Shed"/> and a <see cref="RoofForm.Saltbox"/> fall toward the front, so their ridge
    /// climbs with the span <em>perpendicular to that wall</em> — which on a long building is the long span.
    /// Answering as though the front were always one edge under-reserves by the difference between the two
    /// spans, and a roof reserved short is clipped at the world ceiling rather than lowered. Null is the edge
    /// a house with no doors picks for itself, so the default answer is a real one rather than a guess.</param>
    public static int TopLayerOver(this HouseStyle style, int width, int depth, RoomEdge? front = null)
    {
        // The roof stands on what the walls reserve, air courses and all — the stamper seats it at
        // WallCourses whatever those courses resolve to — so the reservation is what the field is built over
        // and only the answer is trimmed.
        var wallTop = style.WallCourses;
        var peak = style.Roof.Form == RoofForm.Flat
            ? wallTop + 1
            : new RoofField(
                style.Roof.Form, 0, 0, Math.Max(0, width - 1), Math.Max(0, depth - 1),
                Math.Max(0, style.Roof.Overhang), wallTop + 1, Math.Max(1, style.Roof.Pitch),
                front ?? style.Porch?.Edge ?? style.Front ?? HouseStamper.DefaultFront(width, depth),
                style.Roof.InHalves).Peak;
        return WritesOverTheWall(style) ? peak : style.HighestWallCourse;
    }

    /// <summary>Whether anything at all is laid above the wall top: the roof's own two materials, and — under
    /// a sloped roof, which leaves a triangle of wall to close — the gable face, which is the style's own
    /// where it names one and the top storey's last course carried up where it does not. All three air is a
    /// roof terrace, and then the highest block is the wall's.</summary>
    private static bool WritesOverTheWall(HouseStyle style)
    {
        if (!style.Roof.Body.IsAir() || !style.Roof.Verge.IsAir()) return true;
        if (style.Roof.Form == RoofForm.Flat) return false;
        var topWall = style.Levels[^1].Wall ?? style.Wall;
        return !(style.Roof.Gable ?? topWall.At(topWall.Extent - 1).Material).IsAir();
    }
}
