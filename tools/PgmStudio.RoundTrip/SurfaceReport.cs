using PgmStudio.Minecraft;

namespace PgmStudio.RoundTrip;

/// <summary>
/// What a map's ground is made of, once the things standing on it are set aside.
///
/// <para><b>Decoration is read as a layer, not as the surface.</b> A fern is not what the ground is made of,
/// but which ground an author puts a fern on is a decision worth recovering, so a column reports its ground
/// material and, separately, whatever is growing on it. The same column shape answers the riverbed: water is
/// stepped through to the first solid block, and the depth standing over it is kept, so a bed reads as its own
/// material rather than as a sheet of blue.</para>
///
/// <para><b>Patchiness is measured, not assumed.</b> Whether a material was scattered or laid in fields cannot
/// be seen in a histogram — two maps with identical proportions can look nothing alike. The test is how often
/// a cell of one material neighbours its own kind against how often it would by chance: a material covering a
/// tenth of the ground and finding its own kind in a tenth of its neighbours is scattered, and one finding its
/// own kind in nine neighbours out of ten is laid in fields. Component counts and their median size then give
/// the scale of the field, which is what tells a broad wash from a fine speckle at the same clustering.</para>
/// </summary>
internal static class SurfaceReport
{
    /// <summary>Grows on the ground rather than being it. Kept, but reported as a layer over its own soil.</summary>
    private static readonly HashSet<int> Decoration =
    [
        6, 31, 32, 37, 38, 39, 40, 59, 83, 104, 105, 106, 111, 115, 141, 142, 175,
    ];

    private static readonly HashSet<int> Liquid = [8, 9, 10, 11];

    /// <summary>A tree standing over the ground. Stepped past like decoration and <b>not</b> counted as
    /// structure: the ground beneath a canopy is ground, and excluding it would drop a quarter of the map from
    /// the sample and cut every material into fragments wherever a wood stands over it — which would then be
    /// read as speckle rather than as the shade it is.</summary>
    private static bool IsCanopy(int id) => id is 17 or 18 or 161 or 162;

    /// <summary>Neither ground nor decoration: the wood, glass and stone a structure is built from. Stepped
    /// past so a roof does not report as terrain, and counted so the share it covers stays visible.</summary>
    private static bool IsBuilt(int id) => id is 5 or 20 or 26 or 35 or 43 or 44 or 47 or 50 or 53
        or 54 or 58 or 61 or 62 or 64 or 65 or 66 or 85 or 95 or 96 or 98 or 101 or 102 or 107 or 108 or 109
        or 114 or 125 or 126 or 128 or 134 or 135 or 136 or 139 or 156 or 160 or 163 or 164
        or 171 or 180 or 186 or 188 or 189 or 190 or 191 or 192 or 193 or 194 or 195 or 196 or 197;

    private readonly record struct Cell(int Ground, int GroundData, int Decor, int DecorData, int Depth, int Bed,
                                        int BedData, bool Built, bool Shaded);

    public static int Run(string regionDir, string outPng, int scale, int topMaterials)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }

        var columns = new Dictionary<(int X, int Z), Cell>();
        foreach (var mca in mcas)
            foreach (var chunk in AnvilRegion.ReadChunks(mca))
                Scan(chunk, columns);
        if (columns.Count == 0) { Console.Error.WriteLine("no columns decoded"); return 1; }

        var ground = columns.Where(entry => !entry.Value.Built && entry.Value.Depth == 0)
            .ToDictionary(entry => entry.Key, entry => (entry.Value.Ground, entry.Value.GroundData));

        ReportMaterials(columns, ground, topMaterials);
        ReportDecoration(columns);
        ReportBeds(columns);
        ReportPatchiness(ground, topMaterials);
        Draw(outPng, scale, columns, ground, topMaterials);
        return 0;
    }

    private static void Scan(AnvilRegion.Chunk chunk, Dictionary<(int X, int Z), Cell> columns)
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

        for (var lz = 0; lz < 16; lz++)
            for (var lx = 0; lx < 16; lx++)
            {
                var col = (lz << 4) | lx;
                int decor = 0, decorData = 0, depth = 0, bed = 0, bedData = 0;
                bool built = false, shaded = false;

                for (var y = 255; y >= 0; y--)
                {
                    var id = ids[(y << 8) | col];
                    if (id == 0) continue;
                    if (IsCanopy(id)) { shaded = true; continue; }
                    if (Decoration.Contains(id)) { if (decor == 0) { decor = id; decorData = data[(y << 8) | col]; } continue; }
                    if (Liquid.Contains(id)) { depth++; continue; }
                    if (IsBuilt(id)) { built = true; continue; }

                    if (depth > 0) { bed = id; bedData = data[(y << 8) | col]; }
                    columns[(chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz)] =
                        new Cell(id, data[(y << 8) | col], decor, decorData, depth, bed, bedData, built, shaded);
                    break;
                }
            }
    }

    private static void ReportMaterials(Dictionary<(int X, int Z), Cell> columns,
                                        Dictionary<(int X, int Z), (int Id, int Data)> ground, int top)
    {
        Console.WriteLine($"=== ground material ({ground.Count} open columns of {columns.Count}; " +
            $"{columns.Count(entry => entry.Value.Built)} carry structure, " +
            $"{columns.Count(entry => entry.Value.Shaded && !entry.Value.Built)} stand under a canopy and are counted, " +
            $"{columns.Count(entry => entry.Value.Depth > 0)} lie under liquid) ===");
        foreach (var group in ground.Values.GroupBy(entry => entry).OrderByDescending(group => group.Count()).Take(top))
            Console.WriteLine($"  {BlockPalette.Name(group.Key.Id, group.Key.Data),-26} {group.Count(),7} " +
                $"{group.Count() * 100.0 / ground.Count,5:0.0}%");
    }

    private static void ReportDecoration(Dictionary<(int X, int Z), Cell> columns)
    {
        var decorated = columns.Values.Where(cell => cell.Decor != 0 && cell.Depth == 0).ToList();
        if (decorated.Count == 0) return;
        Console.WriteLine($"\n=== decoration ({decorated.Count} columns, " +
            $"{decorated.Count * 100.0 / columns.Count:0.0}% of the map) and the soil under each ===");
        foreach (var group in decorated.GroupBy(cell => (cell.Decor, cell.DecorData))
                     .OrderByDescending(group => group.Count()).Take(8))
        {
            var soils = group.GroupBy(cell => (cell.Ground, cell.GroundData))
                .OrderByDescending(inner => inner.Count()).Take(3)
                .Select(inner => $"{BlockPalette.Name(inner.Key.Ground, inner.Key.GroundData)} {inner.Count() * 100 / group.Count()}%");
            Console.WriteLine($"  {BlockPalette.Name(group.Key.Decor, group.Key.DecorData),-24} {group.Count(),6}  on {string.Join(", ", soils)}");
        }
    }

    private static void ReportBeds(Dictionary<(int X, int Z), Cell> columns)
    {
        var flooded = columns.Values.Where(cell => cell.Depth > 0).ToList();
        if (flooded.Count == 0) return;
        var depths = flooded.Select(cell => cell.Depth).OrderBy(depth => depth).ToList();
        Console.WriteLine($"\n=== what lies under the water ({flooded.Count} columns, depth " +
            $"{depths[0]}..{depths[^1]}, {depths[depths.Count / 2]} typical) ===");
        foreach (var group in flooded.GroupBy(cell => (cell.Bed, cell.BedData))
                     .OrderByDescending(group => group.Count()).Take(8))
            Console.WriteLine($"  {BlockPalette.Name(group.Key.Bed, group.Key.BedData),-26} {group.Count(),6} " +
                $"{group.Count() * 100.0 / flooded.Count,5:0.0}%   depth " +
                $"{group.Select(cell => cell.Depth).OrderBy(depth => depth).ElementAt(group.Count() / 2)} typical");
    }

    /// <summary>How often each material neighbours its own kind, against how often it would by chance.</summary>
    private static void ReportPatchiness(Dictionary<(int X, int Z), (int Id, int Data)> ground, int top)
    {
        Console.WriteLine($"\n=== is it scattered or laid in fields? ===");
        Console.WriteLine($"  {"material",-26} {"share",6} {"own neighbours",15} {"vs chance",10} {"patches",8} {"median",7}");

        foreach (var group in ground.GroupBy(entry => entry.Value).OrderByDescending(group => group.Count()).Take(top))
        {
            var cells = new HashSet<(int X, int Z)>(group.Select(entry => entry.Key));
            var share = cells.Count / (double)ground.Count;

            var same = 0;
            foreach (var cell in cells)
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (cells.Contains((cell.X + dx, cell.Z + dz))) same++;
            var neighbourly = same / (4.0 * cells.Count);

            var sizes = Components(cells).OrderBy(size => size).ToList();
            Console.WriteLine($"  {BlockPalette.Name(group.Key.Id, group.Key.Data),-26} {share * 100,5:0.0}% " +
                $"{neighbourly * 100,14:0.0}% {neighbourly / Math.Max(1e-9, share),9:0.0}x {sizes.Count,8} " +
                $"{sizes[sizes.Count / 2],7}");
        }
        Console.WriteLine("  (1x is what random scattering gives; higher means the material is laid in fields)");
    }

    private static List<int> Components(HashSet<(int X, int Z)> cells)
    {
        var pending = new HashSet<(int X, int Z)>(cells);
        var sizes = new List<int>();
        while (pending.Count > 0)
        {
            var seed = pending.First();
            pending.Remove(seed);
            var queue = new Queue<(int X, int Z)>([seed]);
            var size = 0;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                size++;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (pending.Remove((cell.X + dx, cell.Z + dz))) queue.Enqueue((cell.X + dx, cell.Z + dz));
            }
            sizes.Add(size);
        }
        return sizes;
    }

    /// <summary>The ground by material identity, in colours chosen to be told apart rather than to be
    /// realistic — the question this render answers is where one material stops and the next begins, and a
    /// palette of true block colours renders podzol, dirt and coarse dirt as three browns.</summary>
    private static void Draw(string outPng, int scale, Dictionary<(int X, int Z), Cell> columns,
                             Dictionary<(int X, int Z), (int Id, int Data)> ground, int top)
    {
        int[] accents =
        [
            0xE6444A, 0x3FA9F5, 0x4CD964, 0xFFD400, 0xB86BFF, 0xFF8A1F, 0x00C2B2, 0xFF5FA2,
            0x9BE564, 0x7A5CFF, 0xD4A017, 0x5AC8FA,
        ];
        var order = ground.Values.GroupBy(entry => entry).OrderByDescending(group => group.Count())
            .Take(top).Select((group, index) => (group.Key, index))
            .ToDictionary(entry => entry.Key, entry => entry.index);

        int minX = columns.Keys.Min(cell => cell.X), maxX = columns.Keys.Max(cell => cell.X);
        int minZ = columns.Keys.Min(cell => cell.Z), maxZ = columns.Keys.Max(cell => cell.Z);
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;

        var pixels = new byte[blocksWide * blocksHigh * 3];
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                var cell = (minX + col, minZ + row);
                if (!columns.TryGetValue(cell, out var column)) { Raster.Set(pixels, blocksWide, col, row, 0x0E0E12); continue; }
                if (column.Built) { Raster.Set(pixels, blocksWide, col, row, 0x2A2D33); continue; }
                if (column.Depth > 0) { Raster.Set(pixels, blocksWide, col, row, 0x1B3A5C); continue; }
                Raster.Set(pixels, blocksWide, col, row,
                    order.TryGetValue((column.Ground, column.GroundData), out var index) ? accents[index % accents.Length] : 0x6E7278);
            }

        var scaled = Raster.Upscale(pixels, blocksWide, blocksHigh, scale);
        PngWriter.Write(outPng, blocksWide * scale, blocksHigh * scale, scaled);
        Console.WriteLine($"\n  wrote {outPng} ({blocksWide * scale}x{blocksHigh * scale} px, {scale} px/block); " +
            $"structure dark grey, water dark blue, other materials mid grey");
    }
}
