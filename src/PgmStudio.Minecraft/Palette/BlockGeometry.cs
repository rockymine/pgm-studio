using PgmStudio.Domain;

namespace PgmStudio.Minecraft.Palette;

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
    /// climbs <em>toward</em>. A stair counts them from its own corner (east 0, west 1, south 2, north 3) and
    /// shares them with nothing else, which is why <see cref="Fronting"/> is a separate four numbers.</summary>
    public static int StairFacing(RoomEdge toward) => toward switch
    {
        RoomEdge.NegX => Blocks.StairWest,
        RoomEdge.PosX => Blocks.StairEast,
        RoomEdge.NegZ => Blocks.StairNorth,
        _ => Blocks.StairSouth,
    };

    /// <summary>The nibble a block mounted with a front takes so that front looks <paramref name="toward"/> —
    /// north 2, south 3, west 4, east 5. A wall sign, a ladder, a chest and a furnace all read the same four
    /// numbers, so this is one table rather than one per block; a stair reads
    /// <see cref="StairFacing"/> instead.
    ///
    /// <para>What a block hangs <em>on</em> is the opposite of what it looks toward — the block behind a
    /// ladder is what holds it up — so a caller that has the wall rather than the view passes
    /// <see cref="RoomEdges.Opposite"/>.</para></summary>
    public static int Fronting(RoomEdge toward) => toward switch
    {
        RoomEdge.NegZ => 2,
        RoomEdge.PosZ => 3,
        RoomEdge.NegX => 4,
        _ => 5,
    };

    /// <summary>A stair turned to climb toward <paramref name="toward"/>, upright or hung upside down.
    ///
    /// <para>A stair's whole data value is geometry — two bits of facing and the upside-down flag — so it
    /// carries no variant nibble of its own, and which wood or stone it is, is which block id it is.</para></summary>
    public static int Stair(RoomEdge toward, bool upsideDown = false) =>
        StairFacing(toward) | (upsideDown ? Blocks.StairUpsideDown : 0);

    /// <summary>A block's data with its direction turned by <paramref name="turn"/>, which maps a horizontal
    /// offset to its image the way a prop's own cells are turned round the symmetry.
    ///
    /// <para>Two blocks a copied prop carries have a direction in their data. A log's two orientation bits name
    /// the axis it lies along — upright, along x, along z, or bark on every face — and a stair's two low bits
    /// name the side it climbs toward. Both are turned by taking the direction as an offset, turning that, and
    /// reading the bits back off the result; a mirror sends an axis to itself and a facing to its opposite, a
    /// quarter turn swaps the axes, and a half turn leaves an axis alone while reversing a facing. Every other
    /// block keeps its data: a slab, a leaf and a wool block face no way in particular.</para></summary>
    public static int Turned(int id, int data, Func<int, int, (int X, int Z)> turn)
    {
        if (id is Blocks.Log or Blocks.Log2)
        {
            var axis = data & 12;
            if (axis is 0 or 12) return data;
            var (tx, tz) = turn(axis == 4 ? 1 : 0, axis == 8 ? 1 : 0);
            return (data & 3) | (Math.Abs(tx) >= Math.Abs(tz) ? 4 : 8);
        }
        if (BlockFamilies.IsStair(id))
        {
            var (dx, dz) = (data & 3) switch
            {
                Blocks.StairEast => (1, 0),
                Blocks.StairWest => (-1, 0),
                Blocks.StairSouth => (0, 1),
                _ => (0, -1),
            };
            var (tx, tz) = turn(dx, dz);
            var facing = Math.Abs(tx) >= Math.Abs(tz)
                ? tx < 0 ? Blocks.StairWest : Blocks.StairEast
                : tz < 0 ? Blocks.StairNorth : Blocks.StairSouth;
            return (data & ~3) | facing;
        }
        return data;
    }

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
