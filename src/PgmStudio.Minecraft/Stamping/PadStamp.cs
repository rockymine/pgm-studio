using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Minecraft.Stamping;

/// <summary>Where something entered the world: the pad's centre and the course above it, which is what the
/// exported element names (<c>WX5</c>). What the pad <em>is</em> — a spawn, a wool, a generator's ground — is
/// the caller's, and the caller already knows it.</summary>
public sealed record PlacedPad(double CenterX, int Y, double CenterZ);

/// <summary>
/// Laying a <see cref="SpawnPad"/>: one square of a stated block at the floor course, and the point that
/// square resolves to.
///
/// <para><b>Into the floor rather than onto it.</b> A pad replaces the course a player stands on, so a wool
/// is broken out of the ground rather than picked up off it and a generator's stack lands on the marked block
/// rather than on a step.</para>
/// </summary>
public static class PadStamp
{
    public static PlacedPad Lay(VoxelWorld world, SpawnPad pad, int floorY, int blockId, int data)
    {
        for (var x = pad.MinX; x < pad.MinX + pad.Size; x++)
            for (var z = pad.MinZ; z < pad.MinZ + pad.Size; z++)
                world.SetBlock(x, floorY, z, blockId, data);
        return new PlacedPad(pad.CenterX, floorY + 1, pad.CenterZ);
    }
}
