using PgmStudio.Geom.Relief;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Dressing;

/// <summary>
/// Cuts a board's carves into its built world after the paint, so a cave's walls are the theme's fill, and dresses
/// what it cut the way pgmvox's <c>dress_cave</c> does: floors of gravel, andesite, clay and cobble, a mushroom here
/// and there, stalactites from the roof and ore in the faces. A carve never cuts water, bedrock or a column the
/// world's own passes claimed for a structure. Every draw is hashed at the folded cell, so the halves of a mirrored
/// board are dressed alike.
/// </summary>
public static class CaveCarver
{
    private const int CobblestoneWall = 139, BrownMushroom = 39, RedMushroom = 40, CoalOre = 16, IronOre = 15;

    private static readonly (int Id, int Data)[] Floors =
        [(Blocks.Gravel, 0), (Blocks.Gravel, 0), (Blocks.Stone, 5), (Blocks.Clay, 0), (Blocks.Cobblestone, 0)];

    /// <summary>Cuts every carve in order and dresses those that ask, answering how many blocks were emptied.
    /// <paramref name="groundTop"/> is the first course of air over the terrain at a column;
    /// <paramref name="isStructure"/> says a column holds something a pass stamped; <paramref name="fold"/> takes a
    /// cell to the member of its orbit the board's draws are made at.</summary>
    public static int Carve(VoxelWorld world, IReadOnlyList<CarveSpec> carves, Func<int, int, int?> groundTop,
                            Func<int, int, bool> isStructure, Func<int, int, (int X, int Z)> fold)
    {
        var emptied = 0;
        foreach (var carve in carves)
        {
            var cut = Carves.Cut(carve, groundTop);
            var opened = new List<(int X, int Y, int Z)>();
            foreach (var (x, y, z) in cut.Air)
            {
                if (y <= 0 || isStructure(x, z)) continue;
                var (id, _) = world.GetBlock(x, y, z);
                if (id is Blocks.Air or Blocks.Bedrock or Blocks.Water or Blocks.StationaryWater
                       or Blocks.Lava or Blocks.StationaryLava) continue;
                world.SetBlock(x, y, z, Blocks.Air);
                opened.Add((x, y, z));
            }
            emptied += opened.Count;
            if (carve.Dress) Dress(world, opened, carve.Seed, fold);
            foreach (var (x, y, z, data) in cut.Ladders)
                if (!isStructure(x, z) && world.GetBlock(x, y, z).Id == Blocks.Air && world.GetBlock(x, y, z + 1).Id != Blocks.Air)
                    world.SetBlock(x, y, z, Blocks.Ladder, data);
        }
        return emptied;
    }

    private static void Dress(VoxelWorld world, List<(int X, int Y, int Z)> opened, int seed, Func<int, int, (int X, int Z)> fold)
    {
        double Draw(int x, int y, int z, int row)
        {
            var (foldX, foldZ) = fold(x, z);
            return ValueNoise.Hash(foldX * 31 + y, foldZ * 17 + row, seed);
        }
        bool Solid(int x, int y, int z) => world.GetBlock(x, y, z).Id is not (Blocks.Air or Blocks.Water or Blocks.StationaryWater);

        foreach (var (x, y, z) in opened)
        {
            if (world.GetBlock(x, y, z).Id != Blocks.Air) continue;
            var below = world.GetBlock(x, y - 1, z).Id;
            if (below is not (Blocks.Air or Blocks.Bedrock or Blocks.Water or Blocks.StationaryWater or Blocks.Ladder))
            {
                var floor = Floors[(int)(Draw(x, y, z, 0) * Floors.Length)];
                world.SetBlock(x, y - 1, z, floor.Id, floor.Data);
                if (Draw(x, y, z, 1) < 0.015) world.SetBlock(x, y, z, Draw(x, y, z, 2) < 0.6 ? BrownMushroom : RedMushroom);
                continue;
            }
            if (Solid(x, y + 1, z) && below == Blocks.Air && Draw(x, y, z, 3) < 0.05)
            {
                var drip = 1 + (int)(Draw(x, y, z, 4) * 2.5);
                for (var k = 0; k < drip && world.GetBlock(x, y - k, z).Id == Blocks.Air && world.GetBlock(x, y - k - 1, z).Id == Blocks.Air; k++)
                    world.SetBlock(x, y - k, z, k < drip - 1 ? Blocks.Stone : CobblestoneWall);
            }
        }

        foreach (var (x, y, z) in opened)
            foreach (var (dx, dy, dz) in Faces)
            {
                var (wallX, wallY, wallZ) = (x + dx, y + dy, z + dz);
                if (world.GetBlock(wallX, wallY, wallZ) != (Blocks.Stone, 0) || Draw(wallX, wallY, wallZ, 5) >= 0.02) continue;
                world.SetBlock(wallX, wallY, wallZ, Draw(wallX, wallY, wallZ, 6) < 0.6 ? CoalOre : IronOre);
            }
    }

    private static readonly (int Dx, int Dy, int Dz)[] Faces = [(1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1), (0, 1, 0)];
}
