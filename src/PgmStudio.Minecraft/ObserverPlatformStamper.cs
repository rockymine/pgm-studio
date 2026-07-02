namespace PgmStudio.Minecraft;

/// <summary>
/// Stamps the observer/default-spawn platform (docs/contracts/sketch-world-export.md §2b): a solid 6×6
/// bedrock platform with four identical inward-facing "info boards" at the edge centres. Each board is a
/// 1-tall × 2-wide bedrock wall with a 2-sign pair on its inner face; viewed from the centre, the left sign
/// is the map name (<c>=== / [CTW] / {name} (bold) / ===</c>) and the right sign the authors
/// (<c>made by (italic) / {authors}</c>). Anchored on the integer corner <c>(anchorX, anchorZ)</c>; the
/// bedrock floor sits at <paramref name="floorY"/> (the observer stands on top), 6 wide → world
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

    public static void Stamp(VoxelWorld world, int anchorX, int anchorZ, int floorY, string mapName, IReadOnlyList<string> authors)
    {
        var x0 = anchorX - 3;
        var z0 = anchorZ - 3;

        // Solid 6×6 bedrock floor.
        for (var lx = 0; lx < Size; lx++)
        for (var lz = 0; lz < Size; lz++)
            world.SetBlock(x0 + lx, floorY, z0 + lz, Blocks.Bedrock);

        var nameLines = MapNameSign(mapName);
        var authorLines = AuthorsSign(authors);

        foreach (var b in Boards)
        {
            world.SetBlock(x0 + b.BedA.Lx, floorY + 1, z0 + b.BedA.Lz, Blocks.Bedrock);
            world.SetBlock(x0 + b.BedB.Lx, floorY + 1, z0 + b.BedB.Lz, Blocks.Bedrock);
            SignBuilder.PlaceWallSign(world, x0 + b.MapName.Lx, floorY + 1, z0 + b.MapName.Lz, b.Facing, nameLines);
            SignBuilder.PlaceWallSign(world, x0 + b.Authors.Lx, floorY + 1, z0 + b.Authors.Lz, b.Facing, authorLines);
        }
    }

    private static IReadOnlyList<SignLine> MapNameSign(string mapName) =>
    [
        new SignLine("==="),
        new SignLine("[CTW]"),
        new SignLine(mapName, Bold: true),
        new SignLine("==="),
    ];

    private static IReadOnlyList<SignLine> AuthorsSign(IReadOnlyList<string> authors)
    {
        var lines = new List<SignLine> { new("made by", Italic: true) };
        foreach (var a in authors.Take(3)) lines.Add(new SignLine(a));
        return lines;
    }
}
