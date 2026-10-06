using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// A block volume seen from outside, drawn with the game's own sprites: a building, a tree or a boulder as a
/// three-quarter view over a diorama of the ground it stands on. It is <see cref="EyeScene"/>'s picture with
/// the eye placed by the volume's size rather than by a player's position, so a card and an in-game picture
/// show one block one way.
/// </summary>
public static class StructurePicture
{
    /// <summary>Degrees the eye is turned from looking south, so a front on the −z wall is in view.</summary>
    private const double Yaw = 30;

    /// <summary>Degrees the eye looks down from the horizon.</summary>
    private const double Pitch = 28;

    /// <summary>The horizontal field of view, narrow enough that the volume keeps its proportions.</summary>
    private const double Fov = 36;

    /// <summary>How much clear sky is left round the volume, as a share of its bounding sphere.</summary>
    private const double Breathing = 1.08;

    /// <summary>The blocks of <paramref name="world"/> inside <paramref name="box"/>, drawn from the
    /// three-quarter eye that fits the whole box in a <paramref name="pixelsWide"/> ×
    /// <paramref name="pixelsHigh"/> frame, as a PNG. A box with no block in it draws as sky.</summary>
    public static byte[] Png(VoxelWorld world, BlockBox box, BlockTextureSet textures, int pixelsWide, int pixelsHigh)
    {
        var scene = EyeScene.Of(Clipped(world, box), textures);
        return scene.Draw(Camera(box, pixelsWide, pixelsHigh), pixelsWide, pixelsHigh).Png();
    }

    /// <summary>The eye that looks at the middle of <paramref name="box"/> from far enough that the box's own
    /// bounding sphere fits the frame's narrower side.</summary>
    public static EyeCamera Camera(BlockBox box, int pixelsWide, int pixelsHigh)
    {
        double middleX = (box.MinX + box.MaxX + 1) / 2.0, middleY = (box.MinY + box.MaxY + 1) / 2.0,
               middleZ = (box.MinZ + box.MaxZ + 1) / 2.0;
        var radius = 0.5 * Math.Sqrt((double)box.Width * box.Width + (double)box.Height * box.Height
                                     + (double)box.Depth * box.Depth) * Breathing;

        var halfAcross = Fov * Math.PI / 360;
        var halfDown = Math.Atan(Math.Tan(halfAcross) * pixelsHigh / pixelsWide);
        var distance = radius / Math.Sin(Math.Min(halfAcross, halfDown));

        var yaw = Yaw * Math.PI / 180;
        var pitch = Pitch * Math.PI / 180;
        return new EyeCamera(
            middleX + Math.Sin(yaw) * Math.Cos(pitch) * distance,
            middleY + Math.Sin(pitch) * distance,
            middleZ - Math.Cos(yaw) * Math.Cos(pitch) * distance,
            Yaw, Pitch, Fov);
    }

    /// <summary>A copy holding only what stands inside <paramref name="box"/>, so the scene's own extent is the
    /// box and a cut part is drawn without the rest of the building around it.</summary>
    private static VoxelWorld Clipped(VoxelWorld world, BlockBox box)
    {
        var clipped = new VoxelWorld();
        for (var x = box.MinX; x <= box.MaxX; x++)
        for (var z = box.MinZ; z <= box.MaxZ; z++)
            for (var y = Math.Max(0, box.MinY); y <= Math.Min(VoxelWorld.MaxHeight - 1, box.MaxY); y++)
            {
                var (id, data) = world.GetBlock(x, y, z);
                if (id != Blocks.Air) clipped.SetBlock(x, y, z, id, data);
            }

        // A biome is only kept for a chunk that holds a block, so the columns are tinted once every block is down.
        for (var x = box.MinX; x <= box.MaxX; x++)
        for (var z = box.MinZ; z <= box.MaxZ; z++)
            clipped.SetBiome(x, z, world.GetBiome(x, z));
        return clipped;
    }
}
