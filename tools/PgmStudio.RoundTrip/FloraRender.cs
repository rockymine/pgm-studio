using PgmStudio.Minecraft;

namespace PgmStudio.RoundTrip;

/// <summary>
/// A world's planting and its paved routes, over the terrain both were laid on. Trees come from
/// <see cref="Flora"/> and are coloured by species; a path is whatever the caller says its materials are,
/// traced as connected surface components so the answer is a route rather than a scatter of matching blocks.
///
/// <para><b>The path palette is an argument, not a constant.</b> Every map paves with something different,
/// and the same block is terrain on one map and a road on another — granite is a road surface on a map that
/// builds its hills from andesite, and bulk stone on one that does not. Naming the materials per map is the
/// only version of this that transfers.</para>
///
/// <para>Reporting the largest component separately from the rest is the point of tracing connectivity at
/// all: a route that runs the width of a map and a hundred scattered patches are the same block count and
/// completely different claims about the map.</para>
/// </summary>
internal static class FloraRender
{
    private static readonly (string Species, int Rgb)[] SpeciesColors =
    [
        ("oak", 0x7FD13B), ("spruce", 0x1F7A5A), ("acacia", 0xE08A2E),
        ("birch", 0xBFE85F), ("dark oak", 0x6B4423), ("jungle", 0x3FBF7F),
    ];

    private const int PathRgb = 0xF2E2C0;
    private const int PathMinorRgb = 0x9A8A6E;

    public static int Run(string regionDir, string outPng, int scale, IReadOnlyList<(int Id, int Data)> pathSpec)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }

        var chunks = mcas.SelectMany(AnvilRegion.ReadChunks).ToList();
        var flora = Flora.Classify(chunks);

        // The surface as a player meets it: topmost block that is not plant cover, so a path under a flower
        // is still a path.
        var surfaceId = new Dictionary<(int X, int Z), (int Id, int Data, int Y)>();
        var ground = new Dictionary<(int X, int Z), int>();
        foreach (var chunk in chunks) ReadSurface(chunk, surfaceId, ground);
        if (ground.Count == 0) { Console.Error.WriteLine("no columns decoded"); return 1; }

        var wanted = new HashSet<(int Id, int Data)>(pathSpec);
        var anyData = new HashSet<int>(pathSpec.Where(entry => entry.Data < 0).Select(entry => entry.Id));
        var pathCells = new HashSet<(int X, int Z)>(surfaceId
            .Where(entry => anyData.Contains(entry.Value.Id) || wanted.Contains((entry.Value.Id, entry.Value.Data)))
            .Select(entry => entry.Key));

        var routes = Components(pathCells);
        ReportPath(routes, pathSpec, ground);
        ReportTrees(flora);
        Draw(outPng, scale, ground, routes, flora, pathCells);
        return 0;
    }

    private static void ReadSurface(AnvilRegion.Chunk chunk, Dictionary<(int X, int Z), (int Id, int Data, int Y)> surface,
                                    Dictionary<(int X, int Z), int> ground)
    {
        var ids = new ushort[256 * 256];
        var data = new byte[256 * 256];
        foreach (var section in AnvilRegion.Sections(chunk))
        {
            var yStart = section.SectionY * 16;
            if (yStart is < 0 or >= 256) continue;
            Array.Copy(section.Ids, 0, ids, yStart * 256, 4096);
            Array.Copy(section.Data, 0, data, yStart * 256, 4096);
        }

        // Plant cover and the tree itself are stepped past; liquids are not, since a paved ford is still
        // under water and reporting the bed as the surface would invent a path through a river.
        var cover = new HashSet<int> { 0, 6, 31, 32, 37, 38, 39, 40, 78, 106, 111, 175, 18, 161, 17, 162, 50, 55, 66 };
        for (var lz = 0; lz < 16; lz++)
            for (var lx = 0; lx < 16; lx++)
            {
                var col = (lz << 4) | lx;
                for (var y = 255; y >= 0; y--)
                {
                    var id = ids[(y << 8) | col];
                    if (cover.Contains(id)) continue;
                    var cell = (chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz);
                    surface[cell] = (id, data[(y << 8) | col], y);
                    ground[cell] = y;
                    break;
                }
            }
    }

    /// <summary>Connected runs of path surface, 8-neighbour so a diagonal step still counts as one route.</summary>
    private static List<List<(int X, int Z)>> Components(HashSet<(int X, int Z)> cells)
    {
        var pending = new HashSet<(int X, int Z)>(cells);
        var components = new List<List<(int X, int Z)>>();
        while (pending.Count > 0)
        {
            var seed = pending.First();
            pending.Remove(seed);
            var component = new List<(int X, int Z)>();
            var queue = new Queue<(int X, int Z)>([seed]);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                component.Add(cell);
                for (var dz = -1; dz <= 1; dz++)
                    for (var dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dz != 0) && pending.Remove((cell.X + dx, cell.Z + dz)))
                            queue.Enqueue((cell.X + dx, cell.Z + dz));
            }
            components.Add(component);
        }
        return [.. components.OrderByDescending(component => component.Count)];
    }

    private static void ReportPath(List<List<(int X, int Z)>> routes, IReadOnlyList<(int Id, int Data)> spec,
                                   Dictionary<(int X, int Z), int> ground)
    {
        var materials = string.Join(", ", spec.Select(entry => entry.Data < 0
            ? $"{entry.Id}:* {BlockPalette.Name(entry.Id, -1)}"
            : $"{entry.Id}:{entry.Data} {BlockPalette.Name(entry.Id, entry.Data)}"));
        Console.WriteLine($"=== paved surface ({materials}) ===");
        if (routes.Count == 0) { Console.WriteLine("  none on the surface"); return; }

        var total = routes.Sum(route => route.Count);
        int worldMinX = ground.Keys.Min(cell => cell.X), worldMaxX = ground.Keys.Max(cell => cell.X);
        int worldMinZ = ground.Keys.Min(cell => cell.Z), worldMaxZ = ground.Keys.Max(cell => cell.Z);

        Console.WriteLine($"  {total} surface cells in {routes.Count} connected component(s)");
        foreach (var route in routes.Take(5))
        {
            int minX = route.Min(cell => cell.X), maxX = route.Max(cell => cell.X);
            int minZ = route.Min(cell => cell.Z), maxZ = route.Max(cell => cell.Z);
            var spanX = (maxX - minX + 1) * 100.0 / (worldMaxX - worldMinX + 1);
            var spanZ = (maxZ - minZ + 1) * 100.0 / (worldMaxZ - worldMinZ + 1);
            var heights = route.Where(ground.ContainsKey).Select(cell => ground[cell]).OrderBy(y => y).ToList();
            Console.WriteLine($"    {route.Count,6} cells  x {minX}..{maxX} z {minZ}..{maxZ}  " +
                $"spans {spanX:0}% of the map east-west, {spanZ:0}% north-south  " +
                $"climbs y {heights[0]}..{heights[^1]}");
        }
        var largest = routes[0].Count * 100.0 / total;
        Console.WriteLine($"  the largest component holds {largest:0.0}% of the paving — " +
            $"{(largest > 60 ? "one connected route" : "the paving is not one network in this material")}");

        // A route can be continuous underfoot and still break in material where it crosses a bridge, a
        // building floor or a ford, so the gap between the big components is the thing worth knowing:
        // a few blocks means the walk connects and only the paving stopped.
        if (routes.Count < 2) return;
        foreach (var other in routes.Skip(1).Take(3))
        {
            var gap = routes[0].Min(cell => other.Min(target =>
                Math.Max(Math.Abs(cell.X - target.X), Math.Abs(cell.Z - target.Z))));
            Console.WriteLine($"    component of {other.Count} cells lies {gap} block(s) from the largest — " +
                $"{(gap <= 6 ? "close enough that the route continues over something else" : "a genuine separation")}");
        }
    }

    private static void ReportTrees(Flora.Result flora)
    {
        Console.WriteLine($"\n=== trees ({flora.Trees.Count}) ===");
        Console.WriteLine($"{"species",-10} {"count",6} {"logs",7} {"branch%",8} {"height",8}  trunk wood");
        foreach (var group in flora.Trees.GroupBy(tree => tree.Species).OrderByDescending(group => group.Count()))
        {
            var trees = group.ToList();
            var logs = trees.Sum(tree => tree.Logs.Count);
            var branches = trees.Sum(tree => tree.Branches);
            var heights = trees.Select(tree => tree.TopY - tree.BaseY + 1).OrderBy(height => height).ToList();
            var trunks = trees.GroupBy(tree => tree.Trunk).OrderByDescending(inner => inner.Count())
                .Select(inner => $"{inner.Key} {inner.Count() * 100 / trees.Count}%");
            Console.WriteLine($"{group.Key,-10} {trees.Count,6} {logs,7} {branches * 100.0 / Math.Max(1, logs),7:0.0}% " +
                $"{$"{heights[0]}..{heights[^1]}",8}  {string.Join(", ", trunks)}");
        }
        Console.WriteLine($"structural timber: {flora.StructuralLogs.Count} log blocks in components with " +
            $"no all-bark and no leaves");

        if (flora.Bare.Count == 0) return;
        Console.WriteLine($"\n=== all-bark wood carrying no canopy ({flora.Bare.Count}) — read, not counted as trees ===");
        foreach (var group in flora.Bare.GroupBy(item => item.Trunk).OrderByDescending(group => group.Count()))
        {
            var items = group.ToList();
            var heights = items.Select(item => item.TopY - item.BaseY + 1).ToList();
            var uniform = heights.Distinct().Count() == 1;
            Console.WriteLine($"  {group.Key,-10} {items.Count,4} component(s), height {heights.Min()}..{heights.Max()}" +
                $"   {(uniform ? "every one identical — a structural element, not planting" : "varied — likely stumps or saplings")}");
        }
    }

    private static void Draw(string outPng, int scale, Dictionary<(int X, int Z), int> ground,
                             List<List<(int X, int Z)>> routes, Flora.Result flora, HashSet<(int X, int Z)> pathCells)
    {
        int minX = ground.Keys.Min(cell => cell.X), maxX = ground.Keys.Max(cell => cell.X);
        int minZ = ground.Keys.Min(cell => cell.Z), maxZ = ground.Keys.Max(cell => cell.Z);
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;
        int lowest = ground.Values.Min(), highest = ground.Values.Max();
        var span = Math.Max(1, highest - lowest);

        var pixels = new byte[blocksWide * blocksHigh * 3];
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
                Raster.Set(pixels, blocksWide, col, row,
                    ground.TryGetValue((minX + col, minZ + row), out var height)
                        ? Raster.Lerp(0x212429, 0x7E828A, (height - lowest) / (double)span)
                        : 0x0E0E12);

        // The main route reads as the route; the rest of the paving is present but subdued, so a stray patch
        // cannot be mistaken for part of it.
        var mainRoute = routes.Count > 0 ? new HashSet<(int X, int Z)>(routes[0]) : [];
        foreach (var cell in pathCells)
            if (cell.X >= minX && cell.X <= maxX && cell.Z >= minZ && cell.Z <= maxZ)
                Raster.Over(pixels, blocksWide, cell.X - minX, cell.Z - minZ,
                    mainRoute.Contains(cell) ? PathRgb : PathMinorRgb, mainRoute.Contains(cell) ? 0.95 : 0.6);

        var colors = SpeciesColors.ToDictionary(entry => entry.Species, entry => entry.Rgb);
        foreach (var tree in flora.Trees)
        {
            var accent = colors.TryGetValue(tree.Species, out var rgb) ? rgb : 0xFFFFFF;
            foreach (var log in tree.Logs)
            {
                if (log.X < minX || log.X > maxX || log.Z < minZ || log.Z > maxZ) continue;
                // The trunk is opaque, the limbs are lighter, so a tree's own spread is visible.
                var isTrunk = tree.Logs.Count(other => other.X == log.X && other.Z == log.Z) > 1;
                Raster.Over(pixels, blocksWide, log.X - minX, log.Z - minZ, accent, isTrunk ? 1.0 : 0.7);
            }
        }

        var scaled = Raster.Upscale(pixels, blocksWide, blocksHigh, scale);
        PngWriter.Write(outPng, blocksWide * scale, blocksHigh * scale, scaled);
        Console.WriteLine($"\n  wrote {outPng} ({blocksWide * scale}x{blocksHigh * scale} px, {scale} px/block)");
    }
}
