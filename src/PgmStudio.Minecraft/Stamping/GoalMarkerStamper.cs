using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Stamping;

/// <summary>The two cheap sky-marker shapes a goal can carry. Kept to a small enum rather than a single
/// fixed shape so a caller can key the shape to what kind of goal it is marking without inventing a new
/// stamper for it.</summary>
public enum GoalMarkerShape
{
    /// <summary>A solid <see cref="GoalMarkerStamper.Size"/>³ cube.</summary>
    Cube,

    /// <summary>A 3-D asterisk: one arm along each axis through the centre block, so it reads as a mark
    /// rather than a block even at a distance where a small cube would just look like a stray voxel.</summary>
    Cross,
}

/// <summary>
/// Stamps a small coloured marker high above a goal — a wool room, a destroyable, or a core — so a player
/// crossing open ground can tell where the goal is without a map. It floats <see cref="Clearance"/> blocks
/// above the map's authored build-height cap, never below it: a marker inside build range is a marker a
/// player could bury under a tower, and the whole point is that nobody can reach or grief it.
/// <para>Coordinates are absolute world blocks; every caller stamps one marker per already-fanned goal
/// entry (one wool room, one destroyable, one core, per symmetry-orbit image), which is what keeps a
/// mirrored board's markers matching without this stamper doing any orbit math of its own.</para>
/// </summary>
public static class GoalMarkerStamper
{
    /// <summary>Cube edge / cross arm span, in blocks.</summary>
    public const int Size = 3;

    /// <summary>Open air kept between the build-height cap and the marker's lowest block.</summary>
    public const int Clearance = 4;

    /// <summary>Stamp the marker centred on (<paramref name="centerX"/>, <paramref name="centerZ"/>), its
    /// lowest block at <paramref name="floorY"/>, in <paramref name="woolDamage"/> wool. <paramref
    /// name="floorY"/> is the caller's to place — normally the build-height cap plus <see cref="Clearance"/>,
    /// clamped so the shape still fits under the world ceiling.</summary>
    public static void Stamp(
        VoxelWorld world, int centerX, int centerZ, int floorY, int woolDamage,
        GoalMarkerShape shape = GoalMarkerShape.Cube)
    {
        if (floorY < 0 || floorY + Size > VoxelWorld.MaxHeight) return;
        foreach (var (localX, localY, localZ) in Cells(shape))
            world.SetBlock(centerX + localX, floorY + localY, centerZ + localZ, Blocks.Wool, woolDamage);
    }

    private static IEnumerable<(int X, int Y, int Z)> Cells(GoalMarkerShape shape)
    {
        const int half = Size / 2;
        if (shape == GoalMarkerShape.Cross)
        {
            for (var offset = -half; offset <= half; offset++)
            {
                yield return (offset, half, 0);
                yield return (0, half + offset, 0);
                yield return (0, half, offset);
            }
            yield break;
        }

        for (var localX = -half; localX <= half; localX++)
        for (var localY = -half; localY <= half; localY++)
        for (var localZ = -half; localZ <= half; localZ++)
            yield return (localX, half + localY, localZ);
    }
}
