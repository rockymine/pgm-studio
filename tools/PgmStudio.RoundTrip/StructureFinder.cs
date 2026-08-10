using PgmStudio.Minecraft;

namespace PgmStudio.RoundTrip;

/// <summary>
/// The built structures standing on a world, found and drawn over the terrain they sit on.
///
/// <para><b>A building is recognised by its material, not by its height.</b> Elevation alone cannot separate
/// a hut from the boulder beside it — both are flat-topped lumps standing over their surroundings — and a
/// long hall on level ground has no elevation signature at all. What every building does have is a surface a
/// world does not generate: planks, stairs, slabs, brick, glass, wool. Each column is classified by the block
/// on top of it once vegetation is stripped, the built ones are joined into connected components, and each
/// component is one candidate structure.</para>
///
/// <para><b>Height is then measured against the ground the structure hides.</b> The natural surface under a
/// building is not visible, so the comparison is with a ring of natural columns just outside the component's
/// own footprint; a structure's height is its roof over that ring's median. This is what separates a building
/// from a path or a plaza, which are built surfaces with no height at all, and it is reported rather than
/// used as a filter — a paved road is a real find, it is simply not a building.</para>
///
/// <para>The render puts the findings over a desaturated height profile, because a structure's placement only
/// means something against the terrain it was placed on.</para>
/// </summary>
internal static class StructureFinder
{
    /// <summary>Blocks a world does not put on its own surface. Deliberately excludes stone, cobblestone,
    /// andesite, gravel, sand and clay: all four generate naturally in the open, so including them would
    /// classify every outcrop and river bank as architecture.</summary>
    private static readonly HashSet<int> Built =
    [
        5, 20, 24, 35, 43, 44, 45, 47, 53, 54, 57, 58, 61, 62, 64, 65, 67, 71, 80, 82, 84, 87, 89, 91, 95,
        96, 98, 101, 102, 108, 109, 112, 114, 116, 117, 118, 120, 125, 126, 128, 133, 134, 135, 136, 138,
        145, 146, 152, 155, 158, 159, 160, 163, 164, 165, 168, 169, 170, 171, 172, 173, 179, 180, 181, 182,
        188, 189, 190, 191, 192, 193, 194, 195, 196, 197, 198, 199, 201, 202, 206, 208, 214, 215, 216,
    ];

    /// <summary>Stripped before the top block is read: plants, snow and the small furniture that would
    /// otherwise decide a column's class.</summary>
    private static readonly HashSet<int> Skin =
    [
        0, 6, 18, 30, 31, 32, 37, 38, 39, 40, 50, 51, 55, 59, 63, 66, 68, 69, 70, 72, 75, 76, 77, 78, 83,
        104, 105, 106, 111, 115, 131, 132, 141, 142, 143, 157, 161, 175,
    ];

    /// <summary>Natural ground: everything the terrain itself is made of, so a built column's own blocks and
    /// the liquids over them are stepped past to reach it.</summary>
    private static bool IsNaturalGround(int id) => id != 0 && !Skin.Contains(id) && !Built.Contains(id)
                                                   && id is not (8 or 9 or 10 or 11 or 17 or 162);

    private sealed record Structure(int MinX, int MaxX, int MinZ, int MaxZ, int Area, int RoofLow, int RoofHigh,
                                    int GroundAround, string Materials);

    public static int Run(string regionDir, string outPng, int scale, int minimumArea)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }

        var topId = new Dictionary<(int X, int Z), int>();
        var topY = new Dictionary<(int X, int Z), int>();
        var naturalY = new Dictionary<(int X, int Z), int>();
        foreach (var mca in mcas)
            foreach (var chunk in AnvilRegion.ReadChunks(mca))
                Scan(chunk, topId, topY, naturalY);
        if (topY.Count == 0) { Console.Error.WriteLine("no columns decoded"); return 1; }

        var builtCells = new HashSet<(int X, int Z)>(topId.Where(entry => Built.Contains(entry.Value)).Select(entry => entry.Key));
        var structures = new List<Structure>();
        var claimed = new Dictionary<(int X, int Z), int>();

        var pending = new HashSet<(int X, int Z)>(builtCells);
        while (pending.Count > 0)
        {
            var seed = pending.First();
            var component = Flood(seed, pending);
            if (component.Count < minimumArea) continue;

            var index = structures.Count;
            foreach (var cell in component) claimed[cell] = index;

            var roofs = component.Select(cell => topY[cell]).OrderBy(y => y).ToList();
            var ring = Ring(component, builtCells).Where(naturalY.ContainsKey).Select(cell => naturalY[cell]).OrderBy(y => y).ToList();
            var materials = component.GroupBy(cell => BlockPalette.Name(topId[cell], 0))
                .OrderByDescending(group => group.Count()).Take(3)
                .Select(group => $"{group.Key} {group.Count() * 100 / component.Count}%");

            structures.Add(new Structure(
                component.Min(cell => cell.X), component.Max(cell => cell.X),
                component.Min(cell => cell.Z), component.Max(cell => cell.Z),
                component.Count, roofs[0], roofs[^1],
                ring.Count > 0 ? ring[ring.Count / 2] : roofs[0], string.Join(", ", materials)));
        }

        Report(structures);
        Draw(outPng, scale, topY, naturalY, claimed, structures);
        return 0;
    }

    private static void Scan(AnvilRegion.Chunk chunk, Dictionary<(int X, int Z), int> topId,
                             Dictionary<(int X, int Z), int> topY, Dictionary<(int X, int Z), int> naturalY)
    {
        var ids = new ushort[256 * 256];
        foreach (var section in AnvilRegion.Sections(chunk))
        {
            var yStart = section.SectionY * 16;
            if (yStart is < 0 or >= 256) continue;
            Array.Copy(section.Ids, 0, ids, yStart * 256, 4096);
        }

        for (var lz = 0; lz < 16; lz++)
            for (var lx = 0; lx < 16; lx++)
            {
                var col = (lz << 4) | lx;
                var cell = (chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz);
                var haveTop = false;
                for (var y = 255; y >= 0; y--)
                {
                    var id = ids[(y << 8) | col];
                    if (Skin.Contains(id)) continue;
                    if (!haveTop) { topId[cell] = id; topY[cell] = y; haveTop = true; }
                    if (!IsNaturalGround(id)) continue;
                    naturalY[cell] = y;
                    break;
                }
            }
    }

    /// <summary>One connected component of built columns, 8-neighbour so a diagonal corner still joins.</summary>
    private static List<(int X, int Z)> Flood((int X, int Z) seed, HashSet<(int X, int Z)> pending)
    {
        var component = new List<(int X, int Z)>();
        var queue = new Queue<(int X, int Z)>([seed]);
        pending.Remove(seed);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            component.Add(cell);
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = (cell.X + dx, cell.Z + dz);
                    if ((dx != 0 || dz != 0) && pending.Remove(next)) queue.Enqueue(next);
                }
        }
        return component;
    }

    /// <summary>Natural columns just outside a component — the ground it stands on, which its own roof hides.</summary>
    private static HashSet<(int X, int Z)> Ring(List<(int X, int Z)> component, HashSet<(int X, int Z)> built)
    {
        var inside = new HashSet<(int X, int Z)>(component);
        var ring = new HashSet<(int X, int Z)>();
        foreach (var cell in component)
            for (var dz = -3; dz <= 3; dz++)
                for (var dx = -3; dx <= 3; dx++)
                {
                    var next = (cell.X + dx, cell.Z + dz);
                    if (!inside.Contains(next) && !built.Contains(next)) ring.Add(next);
                }
        return ring;
    }

    private static void Report(List<Structure> structures)
    {
        Console.WriteLine($"{structures.Count} built structure(s) of the minimum size\n");
        Console.WriteLine($"{"x range",-14} {"z range",-14} {"area",6} {"roof y",10} {"ground",7} {"tall",5}  materials");
        foreach (var structure in structures.OrderByDescending(structure => structure.Area))
            Console.WriteLine($"{$"{structure.MinX}..{structure.MaxX}",-14} {$"{structure.MinZ}..{structure.MaxZ}",-14} " +
                $"{structure.Area,6} {$"{structure.RoofLow}..{structure.RoofHigh}",10} {structure.GroundAround,7} " +
                $"{structure.RoofHigh - structure.GroundAround,5}  {structure.Materials}");

        var tall = structures.Count(structure => structure.RoofHigh - structure.GroundAround >= 3);
        Console.WriteLine($"\n{tall} stand 3+ blocks over the ground around them; " +
            $"{structures.Count - tall} are flat — paths, floors, plazas.");
    }

    /// <summary>Findings over a desaturated height profile: a structure's placement only means something
    /// against the terrain it was placed on.</summary>
    private static void Draw(string outPng, int scale, Dictionary<(int X, int Z), int> topY,
                             Dictionary<(int X, int Z), int> naturalY, Dictionary<(int X, int Z), int> claimed,
                             List<Structure> structures)
    {
        int minX = topY.Keys.Min(cell => cell.X), maxX = topY.Keys.Max(cell => cell.X);
        int minZ = topY.Keys.Min(cell => cell.Z), maxZ = topY.Keys.Max(cell => cell.Z);
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;

        var terrain = naturalY.Values.ToList();
        int lowest = terrain.Min(), highest = terrain.Max();
        var span = Math.Max(1, highest - lowest);

        // Structures far enough apart never share a colour, and the cycle is short enough to stay readable.
        int[] accents = [0xFF7A1F, 0x35D6C4, 0xFFD400, 0xFF4FA3, 0x7CFF4F, 0x9B7BFF];

        var pixels = new byte[blocksWide * blocksHigh * 3];
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                var cell = (minX + col, minZ + row);
                if (!naturalY.TryGetValue(cell, out var ground)) { Raster.Set(pixels, blocksWide, col, row, 0x0E0E12); continue; }
                var shade = Raster.Lerp(0x23252B, 0x8E9199, (ground - lowest) / (double)span);
                Raster.Set(pixels, blocksWide, col, row, shade);

                if (!claimed.TryGetValue(cell, out var index)) continue;
                var onEdge = Enumerable.Range(0, 4)
                    .Select(side => (cell.Item1 + (side == 0 ? 1 : side == 1 ? -1 : 0), cell.Item2 + (side == 2 ? 1 : side == 3 ? -1 : 0)))
                    .Any(neighbour => !claimed.TryGetValue(neighbour, out var other) || other != index);
                Raster.Over(pixels, blocksWide, col, row, accents[index % accents.Length], onEdge ? 1.0 : 0.62);
            }

        var scaled = Raster.Upscale(pixels, blocksWide, blocksHigh, scale);
        PngWriter.Write(outPng, blocksWide * scale, blocksHigh * scale, scaled);
        Console.WriteLine($"  wrote {outPng} ({blocksWide * scale}x{blocksHigh * scale} px, {scale} px/block), " +
            $"{structures.Count} structure(s) over the terrain");
    }
}
