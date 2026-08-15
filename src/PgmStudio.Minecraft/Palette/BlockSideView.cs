using PgmStudio.Minecraft.Anvil;
namespace PgmStudio.Minecraft.Palette;

/// <summary>Which block is nearest the camera at one (column, course) of a side view, and how far back it
/// stands — 0 at the front of the projected box, 1 at its back.</summary>
public readonly record struct SideViewBlock(int Id, int Data, double Depth);

/// <summary>
/// A world seen from the side: for every column and course, the nearest solid block along the view axis, with
/// the distance it stands at.
///
/// <para>This is the same model the build-height side view draws a map with (<c>Analysis.SideView</c>, the
/// <c>/segments</c> endpoint): project along one axis, nearest hit wins, and turn the distance into shade so a
/// flat picture still reads as a volume. The difference is what is being projected. That one walks vertical
/// <em>segments</em>, which carry no block identity, so its picture is a stone-grey ramp; this one walks a
/// real <see cref="VoxelWorld"/> and keeps the block, so the picture stays the export's own colours and only
/// the shading comes from depth.</para>
///
/// <para>Why a projection rather than a cut. A single row through a tree meets its crown wherever that row
/// happens to fall — through the gaps between leaf clusters as often as through the clusters — so the
/// silhouette comes out speckled and missing pieces that are plainly there. Projecting every row answers
/// "what would you see", which is the question a side view is asked.</para>
/// </summary>
public static class BlockSideView
{
    /// <summary>Project a box of <paramref name="world"/> along +Z, looking from <paramref name="fromZ"/>: for
    /// every column and course of the box, the block nearest the camera wins. Bounds are inclusive and in world
    /// coordinates, which is what <see cref="SideViewProjection.At"/> is then addressed in.</summary>
    public static SideViewProjection Project(
        VoxelWorld world, int fromX, int toX, int fromZ, int toZ, int fromY, int toY)
    {
        var columns = Math.Max(0, toX - fromX + 1);
        var courses = Math.Max(0, toY - fromY + 1);
        var cells = new SideViewBlock?[columns * courses];

        // Depth is measured against the box, so a block's shade says where that block is and nothing else.
        // Stretching the scale over the depths the view happened to reach would make one block moving back
        // re-shade every other block in the picture, and two cards of the same set incomparable.
        var deepest = Math.Max(1, toZ - fromZ);

        for (var y = fromY; y <= toY; y++)
        for (var x = fromX; x <= toX; x++)
        {
            for (var z = fromZ; z <= toZ; z++)
            {
                var (id, data) = world.GetBlock(x, y, z);
                if (id == Blocks.Air) continue;
                cells[(y - fromY) * columns + (x - fromX)] =
                    new SideViewBlock(id, data, (double)(z - fromZ) / deepest);
                break;                                    // nearest hit wins; nothing behind it is visible
            }
        }

        return new SideViewProjection(cells, fromX, columns, fromY, courses);
    }
}

/// <summary>What <see cref="BlockSideView.Project"/> produced, addressed in the world coordinates it was cut
/// from.</summary>
public sealed class SideViewProjection(SideViewBlock?[] cells, int fromX, int columns, int fromY, int courses)
{
    /// <summary>The leftmost column the view carries, and how many — what a raster walks.</summary>
    public int FromX => fromX;

    public int Columns => columns;

    /// <summary>The block visible at a column and course, or null where the view sees through.</summary>
    public SideViewBlock? At(int x, int y)
    {
        var (column, row) = (x - fromX, y - fromY);
        return row < 0 || row >= courses || column < 0 || column >= columns ? null : cells[row * columns + column];
    }

    /// <summary>The highest course anything is visible at, or null when the view is empty — what a caller
    /// crops to so a picture is the thing rather than the sky above it.</summary>
    public int? Highest()
    {
        for (var row = courses - 1; row >= 0; row--)
            for (var column = 0; column < columns; column++)
                if (cells[row * columns + column] is not null) return fromY + row;
        return null;
    }
}
