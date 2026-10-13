using fNbt;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Domain;

namespace PgmStudio.Minecraft.Stamping;

/// <summary>
/// Stands a flag's banner at a post. PGM loads a flag only where a banner stands at its starting post, and
/// copies that banner's colour, patterns and facing into the flag players carry (<c>docs/pgm/flags.md</c> §2),
/// so a post with no banner is a map that does not load. The banner stands on the ground the world build solved
/// under the post's column, the course a player walks on.
/// </summary>
public static class FlagStamper
{
    /// <summary>The block a post's banner stands in: the post's own column, in the first air over its ground.</summary>
    public static BlockBox BannerBox(IReadOnlyDictionary<(int X, int Z), int> surfaceTop, double x, double z)
    {
        var (cellX, cellZ) = ((int)Math.Floor(x), (int)Math.Floor(z));
        var y = PositionSnap.SurfaceYOver(surfaceTop, cellX, cellZ, cellX, cellZ, 1);
        return new BlockBox(cellX, y, cellZ, cellX, y, cellZ);
    }

    /// <summary>Stand a banner of <paramref name="color"/> in <paramref name="box"/>, turned to
    /// <paramref name="yaw"/> (south where none is stated).</summary>
    public static void Stamp(VoxelWorld world, BlockBox box, string color, double? yaw)
    {
        world.SetBlock(box.MinX, box.MinY, box.MinZ, Blocks.StandingBanner, Rotation(yaw));
        world.AddTileEntity(box.MinX, box.MinZ, Banner(box.MinX, box.MinY, box.MinZ, color));
    }

    /// <summary>A standing banner's data value for a yaw: sixteenths of a turn from south, clockwise.</summary>
    public static int Rotation(double? yaw) => (int)Math.Round(((yaw ?? 0) % 360 + 360) % 360 / 22.5) % 16;

    /// <summary>A plain <c>Banner</c> tile entity of <paramref name="color"/>, its base written as a dye damage
    /// (<see cref="BlockColors.DyeDamage"/>); a name that is no colour is white.</summary>
    public static NbtCompound Banner(int x, int y, int z, string color) =>
        new()
        {
            new NbtString("id", "Banner"),
            new NbtInt("x", x),
            new NbtInt("y", y),
            new NbtInt("z", z),
            new NbtInt("Base", BlockColors.DyeDamage(color)),
            new NbtList("Patterns", NbtTagType.Compound),
        };
}
