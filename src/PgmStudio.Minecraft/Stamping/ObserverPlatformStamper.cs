using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Stamping;

/// <summary>
/// Stamps the observer/default-spawn platform (docs/world-export/sketch-world-export.md §2b): a solid 6×6
/// bedrock platform with four identical inward-facing "info boards" at the edge centres. Each board is a
/// 1-tall × 2-wide bedrock wall with a 2-sign pair on its inner face; viewed from the centre, the left sign
/// is the map name (<c>=== / [{gamemodes}] / {name} (bold) / ===</c>) and the right sign the authors
/// (<c>made by (italic) / {authors}</c>). Anchored on the integer corner <c>(anchorX, anchorZ)</c>; the
/// bedrock floor sits at <c>floorY</c> (the observer stands on top), 6 wide → world
/// <c>anchorX-3 .. anchorX+2</c>.
/// </summary>
public static class ObserverPlatformStamper
{
    public const int Size = 6;

    private readonly record struct Board(
        (int Lx, int Lz) BedA, (int Lx, int Lz) BedB,
        (int Lx, int Lz) MapName, (int Lx, int Lz) Authors, Facing Facing);

    // The four edge boards (local 0..5): bedrock wall cells, the map-name + authors sign cells, inward facing.
    private static readonly Board[] Boards =
    [
        new((2, 0), (3, 0), MapName: (2, 1), Authors: (3, 1), Facing.PosZ),   // min-z edge
        new((2, 5), (3, 5), MapName: (3, 4), Authors: (2, 4), Facing.NegZ),   // max-z edge
        new((0, 2), (0, 3), MapName: (1, 3), Authors: (1, 2), Facing.PosX),   // min-x edge
        new((5, 2), (5, 3), MapName: (4, 2), Authors: (4, 3), Facing.NegX),   // max-x edge
    ];

    /// <summary>The lowest floor at or above <paramref name="wantedY"/> that leaves the platform's own
    /// footprint clear of everything the board already builds under it.
    ///
    /// <para>The platform is <b>stamped, not fitted</b>: its floor course is bedrock written over whatever
    /// occupies those cells. A height derived from a nominal ground level knows nothing about a bridge, a keep
    /// or a made thing standing at the board's centre, so the floor is seated above the highest block in its
    /// own six-by-six rather than taken on trust. A footprint with nothing over it keeps
    /// <paramref name="wantedY"/> exactly.</para></summary>
    public static int ClearFloorAt(VoxelWorld world, int anchorX, int anchorZ, int wantedY)
    {
        var (x0, z0) = (anchorX - 3, anchorZ - 3);
        var highest = int.MinValue;

        for (var lx = 0; lx < Size; lx++)
        for (var lz = 0; lz < Size; lz++)
            for (var y = VoxelWorld.MaxHeight - 1; y >= wantedY; y--)
                if (world.GetBlock(x0 + lx, y, z0 + lz).Id != Blocks.Air)
                {
                    highest = Math.Max(highest, y);
                    break;
                }

        return highest == int.MinValue ? wantedY : highest + 1;
    }

    /// <summary>Stamps the platform and its four boards at <paramref name="anchorX"/>/<paramref name="anchorZ"/>,
    /// floor course at <paramref name="floorY"/>, named <paramref name="mapName"/>.</summary>
    /// <param name="world">The world the platform is written into.</param>
    /// <param name="anchorX">The integer corner the 6×6 is anchored on, X.</param>
    /// <param name="anchorZ">The same corner's Z.</param>
    /// <param name="floorY">The bedrock floor course; the observer stands one above it.</param>
    /// <param name="mapName">What the boards call the map.</param>
    /// <param name="gamemodes">What the map is played as, in the order the map declares them — the same set
    /// the <c>&lt;gamemode&gt;</c> element carries, since both are read off the objectives. Two or three are
    /// written as one label (<c>[CTW/DTM]</c>), because a map that carries a wool and a monument is played as
    /// both. Empty leaves the line off: a board with nothing to win has no mode to name, and a made-up one
    /// would be the first thing every player reads.</param>
    /// <param name="authors">Who made it. Empty leaves the authors board's sign off entirely rather than
    /// standing a heading over nothing — <c>EX6</c> is where that is said out loud.</param>
    public static void Stamp(VoxelWorld world, int anchorX, int anchorZ, int floorY, string mapName,
                             IReadOnlyList<string> authors, IReadOnlyList<string> gamemodes)
    {
        var x0 = anchorX - 3;
        var z0 = anchorZ - 3;

        // Solid 6×6 bedrock floor.
        for (var lx = 0; lx < Size; lx++)
        for (var lz = 0; lz < Size; lz++)
            world.SetBlock(x0 + lx, floorY, z0 + lz, Blocks.Bedrock);

        var nameLines = MapNameSign(mapName, gamemodes);
        var authorLines = AuthorsSign(authors);

        foreach (var b in Boards)
        {
            world.SetBlock(x0 + b.BedA.Lx, floorY + 1, z0 + b.BedA.Lz, Blocks.Bedrock);
            world.SetBlock(x0 + b.BedB.Lx, floorY + 1, z0 + b.BedB.Lz, Blocks.Bedrock);
            SignBuilder.PlaceWallSign(world, x0 + b.MapName.Lx, floorY + 1, z0 + b.MapName.Lz, b.Facing, nameLines);
            if (authorLines is not null)
                SignBuilder.PlaceWallSign(world, x0 + b.Authors.Lx, floorY + 1, z0 + b.Authors.Lz, b.Facing, authorLines);
        }
    }

    private static IReadOnlyList<SignLine> MapNameSign(string mapName, IReadOnlyList<string> gamemodes)
    {
        var lines = new List<SignLine> { new("===") };
        if (gamemodes.Count > 0)
            lines.Add(new SignLine($"[{string.Join("/", gamemodes.Select(mode => mode.ToUpperInvariant()))}]"));
        lines.Add(new SignLine(mapName, Bold: true));
        lines.Add(new SignLine("==="));
        return lines;
    }

    /// <summary>The authors board, or null where the map names nobody: a heading over three blank lines is
    /// worse than no sign, and the studio has no name of its own to write there.</summary>
    private static IReadOnlyList<SignLine>? AuthorsSign(IReadOnlyList<string> authors)
    {
        if (authors.All(string.IsNullOrWhiteSpace)) return null;
        var lines = new List<SignLine> { new("made by", Italic: true) };
        foreach (var a in authors.Where(name => !string.IsNullOrWhiteSpace(name)).Take(3)) lines.Add(new SignLine(a));
        return lines;
    }
}
