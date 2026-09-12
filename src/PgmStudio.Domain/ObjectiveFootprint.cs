using PgmStudio.Geom;

namespace PgmStudio.Domain;

/// <summary>
/// The ground a destroyable or a core covers — its XZ extent and where that extent lands around the marker
/// it is anchored on.
///
/// <para>It lives below both the plan layer and the world stamper because the two must agree exactly. The
/// stamper builds the structure; the plan validator has to decide, before anything is built, whether that
/// structure would overhang the void or land inside a spawn or a wool room. A validator computing the
/// footprint its own way would pass a plan the stamper then places one block wider, which is a goal that
/// cannot be broken and no message anywhere.</para>
///
/// <para>Height is deliberately absent. It follows the terrain the structure floats over, which the plan
/// does not know and does not need to: every rule that reads a footprint reads it in plan view.</para>
/// </summary>
public static class ObjectiveFootprint
{
    /// <summary>A destroyable's block extent for its style — the same table the stamper fills.</summary>
    public static (int Width, int Height, int Depth) Destroyable(DestroyableStyle style, int columnHeight = 3) => style switch
    {
        DestroyableStyle.Pillar1 => (1, 1, 1),
        DestroyableStyle.Pillar2 => (1, 2, 1),
        DestroyableStyle.Pillar3 => (1, 3, 1),
        DestroyableStyle.Cube3 => (3, 3, 3),
        DestroyableStyle.Cube4 => (4, 4, 4),
        DestroyableStyle.ColumnPlus => (3, columnHeight, 3),
        _ => (1, 1, 1),
    };

    /// <summary>How many blocks a destroyable of this style is made of — the number PGM counts its health in,
    /// and what decides which materials it may be built from. A plus-section column is five blocks a layer
    /// rather than its footprint filled, which is the whole difference between it and a cube.</summary>
    public static int BlockCount(DestroyableStyle style, int columnHeight = 3)
    {
        if (style == DestroyableStyle.ColumnPlus) return 5 * Math.Max(0, columnHeight);
        var (width, height, depth) = Destroyable(style, columnHeight);
        return width * height * depth;
    }

    /// <summary>A core's casing is square in plan, so its footprint is its size both ways.</summary>
    public static (int Width, int Depth) Core(int size) => (size, size);

    /// <summary>A control point's pad is square too, by the author's ruling — the ground the capture volume
    /// stands on, and the same rect the capture region scopes.</summary>
    public static (int Width, int Depth) ControlPoint(int size) => (size, size);

    /// <summary>
    /// The cell a goal's anchor stands in. An anchor is authored as a position — a piece-relative half-cell
    /// offset resolves to whole or half values — and every rule that reads a goal reads the block it lands
    /// on, so the two are one conversion and it lives here with the footprint it feeds.
    ///
    /// <para>It is also the only honest place to fan one from. A cell's orbit image is
    /// <see cref="Symmetry.Cell"/>'s, not <see cref="Symmetry.Point"/>'s; converting first and fanning the
    /// cell is what keeps a goal and its mirror the same distance from the centre, where fanning the position
    /// and converting afterwards rounds the two images toward opposite sides and lands them a block
    /// apart.</para>
    /// </summary>
    public static (int X, int Z) AnchorCell(double anchorX, double anchorZ)
        => ((int)Math.Round(anchorX, MidpointRounding.AwayFromZero),
            (int)Math.Round(anchorZ, MidpointRounding.AwayFromZero));

    /// <summary>
    /// The <paramref name="k"/>-th orbit image of a goal's anchor, as the anchor of that image — taken by
    /// mirroring the <b>footprint</b> and reading the anchor back off it, not by mirroring the anchor.
    ///
    /// <para>The two differ for an even-sided structure and the difference is a whole block.
    /// <see cref="Centred"/> leans such a structure one further along +X/+Z than −X/−Z, matching the
    /// stamper; an image that reverses an axis has to reverse that lean, and an anchor fanned on its own
    /// carries it through unchanged. A <c>cube-4</c> mirrored that way lands a block off its own reflection
    /// on both axes at once, which is what a hand-built cage around one shows immediately and what a corpus
    /// of odd-sided goals never does — <c>(n−1)/2</c> is symmetric at every odd size.</para>
    ///
    /// <para>The image's own extent is measured from the mirrored corners rather than assumed, so a mode
    /// that turns the plan carries the structure's depth into its width without a special case.</para>
    /// </summary>
    public static (int X, int Z) ImageAnchor(double anchorX, double anchorZ, string? mode, double cx, double cz,
                                             int k, int width, int depth)
    {
        var (cellX, cellZ) = AnchorCell(anchorX, anchorZ);
        var (minX, minZ, maxX, maxZ) = Centred(cellX, cellZ, width, depth);
        var near = Symmetry.Cell(minX, minZ, mode, cx, cz, k);
        var far = Symmetry.Cell(maxX, maxZ, mode, cx, cz, k);
        int imageMinX = Math.Min(near.X, far.X), imageMinZ = Math.Min(near.Z, far.Z);
        int imageWidth = Math.Abs(far.X - near.X) + 1, imageDepth = Math.Abs(far.Z - near.Z) + 1;
        // Read the anchor back out of the image box: Centred puts the minimum at anchor − (n−1)/2.
        return (imageMinX + (imageWidth - 1) / 2, imageMinZ + (imageDepth - 1) / 2);
    }

    /// <summary>
    /// The block rect (inclusive) a structure of <paramref name="width"/>×<paramref name="depth"/> occupies
    /// when centred on a marker. The <c>(n−1)/2</c> offset is what the stamper uses, so an even-sided
    /// structure leans the same way in both — a cube-4 sits one block further along +X/+Z than it does −X/−Z,
    /// and a rule that rounded the other way would disagree with the world by exactly that block.
    /// </summary>
    public static (int MinX, int MinZ, int MaxX, int MaxZ) Centred(double anchorX, double anchorZ, int width, int depth)
    {
        int cx = (int)Math.Floor(anchorX), cz = (int)Math.Floor(anchorZ);
        int minX = cx - (width - 1) / 2, minZ = cz - (depth - 1) / 2;
        return (minX, minZ, minX + width - 1, minZ + depth - 1);
    }
}
