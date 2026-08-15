using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Stamping;

/// <summary>A stamped monument: the wool it captures + the world air-cell the wool goes in, so the exporter
/// can wire the matching XML monument location.</summary>
public sealed record PlacedMonument(string WoolSlug, int X, int Y, int Z);

/// <summary>
/// The monument a wool is placed on: a bedrock pedestal standing on the floor, the air cell above it the wool
/// goes in, a cap of stained glass in the wool's own colour, and a sign beside it saying which wool it wants.
///
/// <para>Here rather than inside the spawn cube because a monument is not a thing about spawn cubes. The same
/// four blocks are what a wool room, a house or a plain plinth on open ground needs, and a second copy of them
/// is how a map comes to have two monuments that do not look alike.</para>
/// </summary>
public static class MonumentStamper
{
    /// <summary>Stamp one monument whose pedestal stands at <paramref name="x"/>/<paramref name="z"/> on
    /// <paramref name="floorY"/> — the floor block, so the pedestal is the first cell above it.
    /// <paramref name="wall"/> is the side the monument backs onto; the sign hangs one cell out from it and
    /// turns to face the room. Returns where the wool goes, which is what the XML has to name.</summary>
    public static PlacedMonument Place(VoxelWorld world, int x, int floorY, int z, string woolSlug, Facing wall)
    {
        world.SetBlock(x, floorY + 1, z, Blocks.Bedrock);                              // pedestal, on the floor
        // floorY + 2 is the placement cell and is left air — that is where the wool goes.
        world.SetBlock(x, floorY + 3, z, Blocks.StainedGlass, BlockColors.BlockDamage(woolSlug));

        // The sign stands one cell off the wall the monument backs onto and looks away from it, into the room.
        var (signDx, signDz) = wall.Inward();
        SignBuilder.PlaceWallSign(world, x + signDx, floorY + 1, z + signDz, wall.Opposite(), Label(woolSlug));

        return new PlacedMonument(woolSlug, x, floorY + 2, z);
    }

    /// <summary>The monument label: <c>Place the / {Colour} (bold) / Wool / here!</c>, coloured per wool.</summary>
    private static IReadOnlyList<SignLine> Label(string slug)
    {
        var chat = SignBuilder.ChatColor(slug);
        return
        [
            new SignLine("Place the"),
            new SignLine(DisplayName(slug), chat, Bold: true),
            new SignLine("Wool", chat),
            new SignLine("here!"),
        ];
    }

    private static string DisplayName(string slug)
        => string.Join(' ', slug.Split('_').Select(word => word.Length == 0 ? word
            : char.ToUpperInvariant(word[0]) + word[1..]));
}
