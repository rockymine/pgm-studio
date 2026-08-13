using PgmStudio.Minecraft;
using PgmStudio.Geom.Render;

namespace PgmStudio.RoundTrip;

/// <summary>
/// What a map gives a player to take, and what it charges for it.
///
/// <para><b>A deposit is the placement, not the block.</b> A seam of two hundred iron is one decision an author
/// made once, and counting its blocks reports the size of that decision as though it were two hundred of them.
/// Deposits are therefore 6-connected lumps, and everything below is measured per lump: how many there are, how
/// big they run, and how they are spread. Two maps with the same block count can be a hundred scattered pockets
/// or two slabs, and only the lump count tells them apart.</para>
///
/// <para><b>Cost is measured as cover, not as depth.</b> A deposit's y tells you where it sits and nothing about
/// what it takes to reach — the same y is bedrock under a mountain and open ground in a valley. What a player
/// pays is the solid blocks standing over it, counted up its own column, and a deposit is charged its
/// <em>cheapest</em> block: the shallowest way in is the way in. Cover of zero is a deposit standing at the top
/// of its own column with nothing over it at all.</para>
///
/// <para><b>Whether it is open is asked of the faces, not of the sky.</b> A deposit in a cave wall is visible to
/// anyone walking past even with a hundred blocks of mountain above it, so the measure is the share of a lump's
/// six-neighbour faces that meet air. Sky exposure is kept separately, because a deposit the surface render can
/// see is the one that shows up as terrain in the ground vocabulary and is not terrain at all.</para>
///
/// <para><b>Balance is a property of a competitive map</b>, so blocks and lumps are split across the middle of
/// the world's longer axis and reported as a share. It cannot know which half is whose, and does not need to:
/// what matters is whether the two sides were given the same, and a sixty-forty split is visible without
/// naming teams.</para>
///
/// <para>Solid mineral blocks and ores are counted as separate classes rather than pooled. Both are placed for
/// the same reason, but a block is nine ingots and an ore is one, so a map that pays in blocks and a map that
/// pays in ore are making different offers at the same block count. This also keeps the ore's other reading
/// intact: an exposed iron ore is grey stone to the ground vocabulary, which is how it looks, and a deposit
/// here, which is what it is for.</para>
/// </summary>
internal static class ResourceReport
{
    /// <summary>The solid mineral blocks — a full block of the refined material.</summary>
    private static readonly Dictionary<int, string> MineralBlocks = new()
    {
        [41] = "Gold Block", [42] = "Iron Block", [57] = "Diamond Block", [133] = "Emerald Block",
        [22] = "Lapis Block", [152] = "Redstone Block", [173] = "Coal Block",
    };

    /// <summary>The ores — the same materials as a stone with something in it. Both lit and unlit redstone ore
    /// are the one block, so they are named once.</summary>
    private static readonly Dictionary<int, string> Ores = new()
    {
        [14] = "Gold Ore", [15] = "Iron Ore", [16] = "Coal Ore", [21] = "Lapis Ore",
        [56] = "Diamond Ore", [73] = "Redstone Ore", [74] = "Redstone Ore", [129] = "Emerald Ore",
        [153] = "Quartz Ore",
    };

    private readonly record struct Cell(int X, int Y, int Z, int Id);

    /// <summary>One placement: its blocks, the cheapest cover any of them sits under, how open its faces are,
    /// and whether the sky reaches it.</summary>
    private readonly record struct Deposit(int Id, int Size, int Cover, double Open, bool Sky);

    public static int Run(string regionDir, string outPng, int scale)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }

        // Solidity as a bit per y, so "is the neighbour air" and "how much is above this" are both answerable
        // without holding the world's block ids. A column is four longs; a map is a few megabytes.
        var solid = new Dictionary<(int X, int Z), ulong[]>();
        var top = new Dictionary<(int X, int Z), int>();
        var cells = new List<Cell>();

        foreach (var mca in mcas)
            foreach (var chunk in AnvilRegion.ReadChunks(mca))
            {
                var volume = AnvilRegion.FullVolume(chunk);
                for (var lz = 0; lz < 16; lz++)
                    for (var lx = 0; lx < 16; lx++)
                    {
                        var key = (chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz);
                        var bits = new ulong[4];
                        var highest = -1;
                        for (var y = 0; y < 256; y++)
                        {
                            var id = volume[(y << 8) | (lz << 4) | lx];
                            if (id == 0) continue;
                            bits[y >> 6] |= 1UL << (y & 63);
                            highest = y;
                            if (MineralBlocks.ContainsKey(id) || Ores.ContainsKey(id))
                                cells.Add(new Cell(key.Item1, y, key.Item2, id));
                        }
                        if (highest < 0) continue;
                        solid[key] = bits;
                        top[key] = highest;
                    }
            }

        if (top.Count == 0) { Console.Error.WriteLine("no columns decoded"); return 1; }
        if (cells.Count == 0) { Console.WriteLine("no resource blocks in this world"); return 0; }

        bool Solid(int x, int y, int z) =>
            y is >= 0 and < 256 && solid.TryGetValue((x, z), out var bits) && (bits[y >> 6] & (1UL << (y & 63))) != 0;

        // What a player digs through to reach a block: the solid blocks standing over it in its own column. A
        // cave above it costs nothing, which is why this is not simply the column's height minus y.
        int Cover(int x, int y, int z)
        {
            if (!solid.TryGetValue((x, z), out var bits)) return 0;
            var count = 0;
            for (var above = y + 1; above <= top[(x, z)]; above++)
                if ((bits[above >> 6] & (1UL << (above & 63))) != 0) count++;
            return count;
        }

        var deposits = Lumps(cells, Solid, Cover, top);

        Report("mineral blocks", deposits.Where(d => MineralBlocks.ContainsKey(d.Id)).ToList(), MineralBlocks);
        Report("ores", deposits.Where(d => Ores.ContainsKey(d.Id)).ToList(), Ores);
        ReportBalance(cells, top);
        Draw(outPng, scale, top, cells);
        return 0;
    }

    /// <summary>The 6-connected lumps of one material. Connectivity is per material, not per resource: a gold
    /// seam touching an iron seam is two placements that happen to meet, and merging them would report one
    /// deposit of a material that does not exist.</summary>
    private static List<Deposit> Lumps(List<Cell> cells, Func<int, int, int, bool> solid,
                                       Func<int, int, int, int> cover, Dictionary<(int X, int Z), int> top)
    {
        var byPosition = cells.ToDictionary(cell => (cell.X, cell.Y, cell.Z), cell => cell.Id);
        var pending = new HashSet<(int X, int Y, int Z)>(byPosition.Keys);
        var steps = new (int X, int Y, int Z)[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };
        var deposits = new List<Deposit>();

        while (pending.Count > 0)
        {
            var seed = pending.First();
            var id = byPosition[seed];
            pending.Remove(seed);

            var queue = new Queue<(int X, int Y, int Z)>([seed]);
            var members = new List<(int X, int Y, int Z)>();
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                members.Add(at);
                foreach (var step in steps)
                {
                    var next = (at.X + step.X, at.Y + step.Y, at.Z + step.Z);
                    if (byPosition.TryGetValue(next, out var neighbour) && neighbour == id && pending.Remove(next))
                        queue.Enqueue(next);
                }
            }

            var faces = 0;
            var open = 0;
            var shallowest = int.MaxValue;
            var sky = false;
            foreach (var member in members)
            {
                foreach (var step in steps)
                {
                    faces++;
                    if (!solid(member.X + step.X, member.Y + step.Y, member.Z + step.Z)) open++;
                }
                var above = cover(member.X, member.Y, member.Z);
                if (above < shallowest) shallowest = above;
                if (top[(member.X, member.Z)] == member.Y) sky = true;
            }

            deposits.Add(new Deposit(id, members.Count, shallowest, open / (double)faces, sky));
        }
        return deposits;
    }

    private static void Report(string title, List<Deposit> deposits, Dictionary<int, string> names)
    {
        Console.WriteLine($"\n=== {title} ===");
        if (deposits.Count == 0) { Console.WriteLine("  none"); return; }
        Console.WriteLine($"  {"material",-16} {"blocks",7} {"deposits",9} {"median",7} {"largest",8} " +
            $"{"cover",7} {"open faces",11} {"already open",13} {"at surface",11}");

        foreach (var group in deposits.GroupBy(deposit => names[deposit.Id])
                     .OrderByDescending(group => group.Sum(deposit => deposit.Size)))
        {
            var lumps = group.OrderBy(deposit => deposit.Size).ToList();
            var covers = group.Select(deposit => deposit.Cover).Order().ToList();
            Console.WriteLine($"  {group.Key,-16} {group.Sum(d => d.Size),7} {lumps.Count,9} " +
                $"{lumps[lumps.Count / 2].Size,7} {lumps[^1].Size,8} {covers[covers.Count / 2],7} " +
                $"{group.Average(d => d.Open) * 100,10:0.0}% {group.Count(d => d.Open > 0) * 100.0 / lumps.Count,12:0.0}% " +
                $"{group.Count(d => d.Sky) * 100.0 / lumps.Count,10:0.0}%");
        }
        Console.WriteLine("  (cover is the solid blocks over a deposit's shallowest block — what it costs to reach;");
        Console.WriteLine("   already open is the share whose faces meet air somewhere, so they are reached by walking)");
    }

    /// <summary>Whether the two halves of the world were given the same. Split across the middle of the longer
    /// axis, which is the axis a two-sided map is laid out along, and counted <b>per block</b> rather than per
    /// deposit: a seam lying across the middle belongs to both halves, and charging it whole to whichever side
    /// its centre fell on invents an imbalance out of one placement.</summary>
    private static void ReportBalance(List<Cell> cells, Dictionary<(int X, int Z), int> top)
    {
        int minX = top.Keys.Min(cell => cell.X), maxX = top.Keys.Max(cell => cell.X);
        int minZ = top.Keys.Min(cell => cell.Z), maxZ = top.Keys.Max(cell => cell.Z);
        var alongX = maxX - minX >= maxZ - minZ;
        var middle = alongX ? (minX + maxX) / 2.0 : (minZ + maxZ) / 2.0;

        Console.WriteLine($"\n=== the two halves, split across {(alongX ? "x" : "z")} at {middle:0.#} ===");
        Console.WriteLine($"  {"material",-16} {"near half",10} {"far half",9} {"split",8}");
        foreach (var group in cells.GroupBy(cell => Name(cell.Id))
                     .OrderByDescending(group => group.Count()))
        {
            var near = group.Count(cell => (alongX ? cell.X : cell.Z) < middle);
            var far = group.Count() - near;
            var total = Math.Max(1, near + far);
            Console.WriteLine($"  {group.Key,-16} {near,10} {far,9} {near * 100.0 / total,6:0}/{far * 100.0 / total,-2:0}");
        }
        Console.WriteLine("  (which half belongs to which team is not knowable here; an even split is the point)");
    }

    private static string Name(int id) =>
        MineralBlocks.TryGetValue(id, out var block) ? block : Ores.TryGetValue(id, out var ore) ? ore : $"Block {id}";

    /// <summary>Every resource block over the map's relief: the terrain in grey so the shape of the world is
    /// legible, and each deposit in its own block colour. A deposit is drawn wherever it reaches a column, so a
    /// vertical seam reads as the small footprint it has rather than as its block count.</summary>
    private static void Draw(string outPng, int scale, Dictionary<(int X, int Z), int> top, List<Cell> cells)
    {
        int minX = top.Keys.Min(cell => cell.X), maxX = top.Keys.Max(cell => cell.X);
        int minZ = top.Keys.Min(cell => cell.Z), maxZ = top.Keys.Max(cell => cell.Z);
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;
        int low = top.Values.Min(), high = Math.Max(low + 1, top.Values.Max());

        var pixels = new byte[blocksWide * blocksHigh * 3];
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
                Raster.Set(pixels, blocksWide, col, row,
                    top.TryGetValue((minX + col, minZ + row), out var height)
                        ? Raster.Lerp(0x1C1C20, 0x54565C, (height - low) / (double)(high - low))
                        : 0x0E0E12);

        // The shallowest block of a column wins the pixel: what a player meets first is what the map offers.
        var shallowest = new Dictionary<(int X, int Z), Cell>();
        foreach (var cell in cells)
            if (!shallowest.TryGetValue((cell.X, cell.Z), out var held) || cell.Y > held.Y)
                shallowest[(cell.X, cell.Z)] = cell;
        foreach (var (key, cell) in shallowest)
            Raster.Set(pixels, blocksWide, key.X - minX, key.Z - minZ, BlockPalette.PackedRgb(cell.Id, 0));

        var scaled = Raster.Upscale(pixels, blocksWide, blocksHigh, scale);
        PngWriter.Write(outPng, blocksWide * scale, blocksHigh * scale, scaled);
        Console.WriteLine($"\n  wrote {outPng} ({blocksWide * scale}x{blocksHigh * scale} px, {scale} px/block); " +
            $"terrain grey by height, every resource column in its own block colour");
    }
}
