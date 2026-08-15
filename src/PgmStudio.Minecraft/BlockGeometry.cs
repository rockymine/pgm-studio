using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>
/// How a block is <b>turned</b>: which way a stair climbs, which half of its cube a slab fills, and which way
/// round a log is laid. In the legacy format a block's metadata <em>is</em> its geometry, so turning one is
/// arithmetic on a data nibble — and arithmetic written out at the call site is arithmetic that is written
/// out again at the next one.
///
/// <para><b>This is the writing half of block geometry, and <see cref="Views.BlockShapes"/> is the reading
/// half.</b> The reader has always been one place: given an id and a data value it answers what fraction of
/// its cube the block fills, which is how a stair lattice draws as light rather than as a solid patch. The
/// writer was five places — three of them the same corner-stair expression inside one file, twice with the
/// same explanatory comment beside it.</para>
///
/// <para><b>Nothing here is named for what it is used on.</b> A turned stair is a turned stair whether it
/// rounds off a window, carries a doorway's head, or bands a wall a theme painted: the terrain painter has as
/// much business asking for an upper slab as an opening does, and a helper called <c>WindowStair</c> would
/// have to be reinvented the moment anything else wanted one. It sits beside <see cref="Blocks"/>, whose
/// constants it composes, so it is reachable from everything that writes a block at all.</para>
/// </summary>
public static class BlockGeometry
{
    /// <summary>The two bits naming the side of its cube a stair's raised half sits on — the direction it
    /// climbs <em>toward</em>.</summary>
    public static int Facing(RoomEdge toward) => toward switch
    {
        RoomEdge.NegX => Blocks.StairWest,
        RoomEdge.PosX => Blocks.StairEast,
        RoomEdge.NegZ => Blocks.StairNorth,
        _ => Blocks.StairSouth,
    };

    /// <summary>A stair turned to climb toward <paramref name="toward"/>, upright or hung upside down.
    ///
    /// <para>A stair's whole data value is geometry — two bits of facing and the upside-down flag — so it
    /// carries no variant nibble of its own, and which wood or stone it is, is which block id it is.</para></summary>
    public static int Stair(RoomEdge toward, bool upsideDown = false) =>
        Facing(toward) | (upsideDown ? Blocks.StairUpsideDown : 0);

    /// <summary>A slab in the upper or lower half of its cube, keeping the three low bits that say what it is
    /// made of. An upper slab is the lintel over an opening and the underside of a course; a lower one is the
    /// sill beneath it and the tread of a step.</summary>
    public static int Slab(int variant, bool upper) =>
        (variant & 0x7) | (upper ? Blocks.SlabUpperHalf : 0);

    /// <summary>
    /// Which way the stair at one end of a <b>run</b> faces so its raised half is on the outside of the pair.
    ///
    /// <para>Two stairs turned back to back are the shape a great deal of this world is built from: the top
    /// corners of an arch, the four quarters of a lattice, the ends of a moulding. The pair is always the
    /// same — each stair climbing toward its own end of the run, so the quarter each is missing faces the
    /// other — and the only thing that varies is which axis the run lies on.</para>
    /// </summary>
    /// <param name="alongX">Whether the run's along axis is x, as a wall facing ±z has.</param>
    /// <param name="atLowEnd">Whether this is the low end of the run rather than the high one.</param>
    public static RoomEdge Outward(bool alongX, bool atLowEnd) => alongX
        ? atLowEnd ? RoomEdge.NegX : RoomEdge.PosX
        : atLowEnd ? RoomEdge.NegZ : RoomEdge.PosZ;

    /// <summary>The stair at one end of a run, turned outward and hung upside down — the corner of an arched
    /// head, and of the upper course of a lattice.</summary>
    public static int CornerStair(bool alongX, bool atLowEnd, bool upsideDown = true) =>
        Stair(Outward(alongX, atLowEnd), upsideDown);
}
