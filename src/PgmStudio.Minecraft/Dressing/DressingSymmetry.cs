using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Minecraft.Dressing;

/// <summary> How a map is mirrored, and the two things the dressing pass does about it. <para>Fanning a
/// <em>site</em> across the orbit is not enough, and the difference is the whole reason this type exists. Place
/// the same boulder at a cell and at its mirror and the two rocks occupy mirrored <em>positions</em> but the same
/// <em>shape</em> — so one team's rock bulges east and the other's bulges east too, and the cover each gives
/// differs cell by cell. What has to be fanned is the prop: generated once on the authored unit, then placed at
/// each image <b>turned by that image's transform</b>, exactly as the layout's own geometry is. That is <see
/// cref="TurnOffset"/>, and it is why a prop is built in its own local frame first.</para>
/// <para><b>Mode</b> — The map's symmetry mode (<c>rot_180</c>, <c>mirror_x</c>, …), or null for
/// none.</para></summary>
public sealed record DressingSymmetry(string? Mode = null, double CenterX = 0, double CenterZ = 0)
{
    /// <summary>Nothing is mirrored — every prop is placed once, where it was generated.</summary>
    public static DressingSymmetry None { get; } = new();

    /// <summary>How many images an orbit has, this one included.</summary>
    public int Order => Symmetry.Order(Mode);

    /// <summary>The cell whose scatter decides for a whole orbit. Generating only on representatives is what
    /// makes the fan a copy rather than a second, independent roll.</summary>
    public (int X, int Z) Canonical(int x, int z) => OrbitScatter.Canonical(x, z, Mode, CenterX, CenterZ);

    /// <summary>Whether a cell is the representative of its own orbit — the test a generator loops on.</summary>
    public bool IsCanonical(int x, int z) => Canonical(x, z) == (x, z);

    /// <summary>The <paramref name="k"/>-th image of a cell, as a cell.</summary>
    public (int X, int Z) ImageCell(int x, int z, int k) => Symmetry.Cell(x, z, Mode, CenterX, CenterZ, k);

    /// <summary>The <paramref name="k"/>-th image of an inclusive rectangle of cells — a wing's own rectangle,
    /// and the transform a footprint takes rather than the one an outline takes.</summary>
    public (int MinX, int MinZ, int MaxX, int MaxZ) ImageCellRect(int minX, int minZ, int maxX, int maxZ, int k)
        => Symmetry.CellRect(minX, minZ, maxX, maxZ, Mode, CenterX, CenterZ, k);

    /// <summary>The <paramref name="k"/>-th image of an offset from a prop's own anchor: the same turn, taken
    /// about the origin rather than the map's centre, because an offset is a direction and a direction has no
    /// position to mirror. This is what turns the prop instead of merely moving it.</summary>
    public (double X, double Z) TurnOffset(double dx, double dz, int k)
        => k == 0 ? (dx, dz) : Symmetry.Point(dx, dz, Mode, 0, 0, k);

    /// <summary>The <paramref name="k"/>-th image of a drawn outline, point by point. An area is mirrored as
    /// the shape it is rather than cell by cell: mirroring its cells one at a time rounds each of them
    /// independently, which frays an edge that the author drew as a clean line.</summary>
    public List<double[]> ImageRing(IReadOnlyList<double[]> ring, int k)
    {
        if (k == 0) return [.. ring.Select(point => new[] { point[0], point[1] })];
        return [.. ring.Select(point =>
        {
            var (x, z) = Symmetry.Point(point[0], point[1], Mode, CenterX, CenterZ, k);
            return new[] { x, z };
        })];
    }

    /// <summary>The <paramref name="k"/>-th image of a wall's outward direction.
    /// <para>A building's door is the case this exists for. Fanning a house's <em>rectangle</em> puts a copy on
    /// the far side of the map, but the copy's door stays on whichever compass side the original's was — so a
    /// mirrored pair face the same way and one team walks out toward the other's half. An edge is a direction,
    /// so it turns the way an offset does: take its outward normal round the orbit and read back whichever wall
    /// that lands on.</para></summary>
    public RoomEdge TurnEdge(RoomEdge edge, int k)
    {
        if (k == 0) return edge;
        var (outX, outZ) = edge.Outward();
        var (tx, tz) = TurnOffset(outX, outZ, k);
        return Math.Abs(tx) >= Math.Abs(tz)
            ? tx < 0 ? RoomEdge.NegX : RoomEdge.PosX
            : tz < 0 ? RoomEdge.NegZ : RoomEdge.PosZ;
    }

    /// <summary>The <paramref name="k"/>-th image of a prop-local block offset.
    /// <para>Deliberately <b>not</b> corrected by half a cell the way <see cref="ImageCell"/> is. The anchor
    /// already carries that correction, and a prop's world cell is anchor + offset — so correcting the offset
    /// too shifts every image of every prop one block past its mirror. An offset is a delta between cells, and
    /// the delta between two mirrored cells is just the mirrored delta.</para></summary>
    public (int X, int Z) TurnCell(int dx, int dz, int k)
    {
        if (k == 0) return (dx, dz);
        var (px, pz) = TurnOffset(dx, dz, k);
        return ((int)Math.Round(px), (int)Math.Round(pz));
    }
}
