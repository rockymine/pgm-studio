using System.Text.Json.Serialization;
using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>Which roof a house wears. A <see cref="Flat"/> one is the shell a wool structure and a spawn have
/// always had — a lid over the walls — and a <see cref="Gable"/> one is two slopes meeting at a ridge. They
/// are the same building underneath, which is why one stamper builds both.</summary>
public enum RoofForm { Gable, Flat }

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
    // Wood ids and the species nibble they share. Not in Blocks because nothing else has wanted them yet.
    private const int Planks = 5, WoodSlab = 126;
    private const int Oak = 0, Spruce = 1, DarkOak = 5;

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

    public RoofForm Form { get; init; } = RoofForm.Gable;

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
/// footprint; a gable's ridge climbs with the span it crosses, so that one has to be asked about a footprint
/// rather than about the style alone.</summary>
public static class HouseHeights
{
    public static int TopLayerOver(this HouseStyle style, int width, int depth)
    {
        if (style.Form == RoofForm.Flat) return style.Wall.Extent + 1;
        var span = Math.Min(width, depth) + 2 * Math.Max(0, style.Overhang);
        return style.Wall.Extent + 1 + (span + 1) / 2 * Math.Max(1, style.Pitch);
    }
}
