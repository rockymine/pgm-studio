using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Stamping;

/// <summary>Where a team comes into the world: the pad's centre, which is what the exported
/// <c>&lt;spawn&gt;</c> region names.</summary>
public sealed record PlacedPlayerSpawn(int TeamColor, double CenterX, int Y, double CenterZ);

/// <summary>Where a wool comes from: the pad's centre, which is what the exported <c>&lt;wool&gt;</c>
/// location names.</summary>
public sealed record PlacedWoolSpawn(string WoolSlug, double CenterX, int Y, double CenterZ);

/// <summary>
/// The square a team spawns on — a <see cref="RoomPad"/> of wool in the team's colour, laid into the floor
/// rather than standing on it.
///
/// <para>Separate from the wool pad it is built exactly like, because the two mean different things to the
/// map: this one becomes a spawn region, the other a wool location, and a caller that has to remember which
/// colour it passed to a shared "pad" is a caller that will eventually pass the wrong one. Separate from any
/// shell for the same reason the monument is: a spawn point is not a fact about cubes, and a map may want one
/// on open ground.</para>
/// </summary>
public static class PlayerSpawnStamper
{
    public static PlacedPlayerSpawn Place(VoxelWorld world, RoomPad pad, int floorY, int teamColor)
    {
        PadStamp.Lay(world, pad, floorY, teamColor);
        return new PlacedPlayerSpawn(teamColor, pad.CenterX, floorY + 1, pad.CenterZ);
    }
}

/// <summary>
/// The square a wool is taken from — a <see cref="RoomPad"/> of wool in the objective's colour, laid into the
/// floor so it is broken out of the ground rather than picked up off it.
/// </summary>
public static class WoolSpawnStamper
{
    public static PlacedWoolSpawn Place(VoxelWorld world, RoomPad pad, int floorY, string woolSlug)
    {
        PadStamp.Lay(world, pad, floorY, BlockColors.BlockDamage(woolSlug));
        return new PlacedWoolSpawn(woolSlug, pad.CenterX, floorY + 1, pad.CenterZ);
    }
}

/// <summary>The blocks both pads are: one square of coloured wool at the floor course. Shared because the
/// geometry really is the same; what differs is only what the map calls it.</summary>
public static class PadStamp
{
    public static void Lay(VoxelWorld world, RoomPad pad, int floorY, int color)
    {
        for (var x = pad.MinX; x < pad.MinX + pad.Size; x++)
            for (var z = pad.MinZ; z < pad.MinZ + pad.Size; z++)
                world.SetBlock(x, floorY, z, Blocks.Wool, color);
    }
}
