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

    /// <summary>A core's casing is square in plan, so its footprint is its size both ways.</summary>
    public static (int Width, int Depth) Core(int size) => (size, size);

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
