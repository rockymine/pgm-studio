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
    public int TopLayer => Wall.Extent + 1;

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
        var wallTop = Math.Max(1, style.Wall.Extent);
        if (style.Form == RoofForm.Flat) return wallTop + 1;
        var field = new RoofField(
            style.Form, 0, 0, Math.Max(0, width - 1), Math.Max(0, depth - 1),
            Math.Max(0, style.Overhang), wallTop + 1, Math.Max(1, style.Pitch), RoomEdge.NegZ);
        return field.Peak;
    }
}
