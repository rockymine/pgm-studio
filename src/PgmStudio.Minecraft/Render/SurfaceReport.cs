using PgmStudio.Geom.Render;

namespace PgmStudio.Minecraft.Render;

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
public static class SurfaceReport
{
    /// <summary>
    /// The family a block reads as. The vocabulary itself is <see cref="TerrainPalette.Families"/> — the same
    /// table the paint picker offers, so what a report measures and what an author paints cannot drift apart.
    ///
    /// <para>A block outside it is not forced into a family, and the two ways of being outside it are told
    /// apart because they mean different things. A <b>partial block</b> is not a failure of the vocabulary: a
    /// stair or a slab found on the surface is something standing on the ground, and the vocabulary names only
    /// full blocks by design. <b>Unnamed</b> is the narrower answer it leaves behind — a full block of something
    /// no family covers, which is a gap worth looking at.</para>
    /// </summary>
    private static string ToneName(int id, int data) =>
        TerrainPalette.FamilyOf(id, data) ?? (BlockRoles.IsFullCube(id) ? "unnamed" : "partial block");

    /// <summary>Standing on the ground rather than being it — what grows and what an author placed. Kept, but
    /// reported as a layer over its own soil.</summary>
    private static bool IsDecoration(int id) => BlockRoles.IsFlora(id) || BlockRoles.IsDecoration(id);

    /// <summary>A tree standing over the ground. Stepped past like decoration and <b>not</b> counted as
    /// structure: the ground beneath a canopy is ground, and excluding it would drop a quarter of the map from
    /// the sample and cut every material into fragments wherever a wood stands over it — which would then be
    /// read as speckle rather than as the shade it is.</summary>
    private static bool IsCanopy(int id) => BlockRoles.IsTree(id);

    /// <summary>Neither ground nor decoration: the wood, glass and stone a structure is built from. Stepped
    /// past so a roof does not report as terrain, and counted so the share it covers stays visible. Only full
    /// blocks are here — a fence, a door, a rail and a carpet are decoration, and are read as a layer over the
    /// ground they stand on rather than as a structure covering it.</summary>
    private static bool IsBuilt(int id) => id is 5 or 20 or 35 or 43 or 44 or 47 or 53
        or 58 or 61 or 62 or 95 or 98 or 102 or 108 or 109
        or 114 or 125 or 126 or 128 or 134 or 135 or 136 or 156 or 160 or 163 or 164
        or 180;

    private readonly record struct Cell(int Ground, int GroundData, int GroundY, int Decor, int DecorData,
                                        int Depth, int Bed, int BedData, bool Built, bool Shaded);

    public sealed record Result(byte[] Pixels, int BlocksWide, int BlocksHigh,
        Dictionary<(int X, int Z), (int Id, int Data)> Ground, int ColumnCount);

    public static int Run(string regionDir, string outPng, int scale, int topMaterials)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }
        return Emit(mcas.SelectMany(AnvilRegion.ReadChunks), outPng, scale, topMaterials, verbose: true);
    }

    /// <summary>Renders a world still held in memory, via <see cref="AnvilRegion.FromWorld"/> — the PNG only,
    /// without the CLI's console-scale material tables.</summary>
    public static int Run(VoxelWorld world, string outPng, int scale, int topMaterials = 12)
        => Emit(AnvilRegion.FromWorld(world), outPng, scale, topMaterials, verbose: false);

    /// <summary>The pure render: chunks in, the ground-only material map and an RGB pixel buffer out. No file
    /// or console I/O — what a caller wanting only the picture (an HTTP endpoint, a stage-image batch) pays
    /// for.</summary>
    public static Result? Render(IEnumerable<AnvilRegion.Chunk> chunks, int topMaterials)
    {
        var columns = new Dictionary<(int X, int Z), Cell>();
        foreach (var chunk in chunks) Scan(chunk, columns);
        return columns.Count == 0 ? null : Render(columns, topMaterials, out _);
    }

    private static int Emit(IEnumerable<AnvilRegion.Chunk> chunks, string outPng, int scale, int topMaterials, bool verbose)
    {
        var columns = new Dictionary<(int X, int Z), Cell>();
        foreach (var chunk in chunks) Scan(chunk, columns);
        if (columns.Count == 0) { Console.Error.WriteLine("no columns decoded"); return 1; }

        var result = Render(columns, topMaterials, out var ground);
        if (verbose)
        {
            ReportMaterials(columns, ground, topMaterials);
            ReportTones(ground);
            ReportDecoration(columns);
            ReportBeds(columns);
            ReportPatchiness(ground, topMaterials);
            ReportTonePatchiness(ground);
            ReportRelief(columns);
        }

        var scaled = Raster.Upscale(result.Pixels, result.BlocksWide, result.BlocksHigh, scale);
        List<Legend.Entry> entries =
        [
            new("GROUND: COLOUR = TERRAIN-PAINT FAMILY (VARIES)", 0x9DA0A0),
            new("STRUCTURE", 0x2A2D33),
            new("UNDER WATER", 0x1B3A5C),
            new("PARTIAL BLOCK (STAIR/SLAB/PANE)", 0xC08030),
            new("UNNAMED MATERIAL", 0xC020C0),
            new("VOID", 0x0E0E12),
        ];
        var withLegend = Legend.AppendBelow(scaled, result.BlocksWide * scale, result.BlocksHigh * scale, entries,
            out var legendHeight,
            scaleLabel: $"SCALE: 1 BLOCK = {scale} PX - {result.BlocksWide} X {result.BlocksHigh} BLOCKS");
        PngWriter.Write(outPng, result.BlocksWide * scale, legendHeight, withLegend);
        Console.WriteLine($"{(verbose ? "\n  " : "")}wrote {outPng} ({result.BlocksWide * scale}x{legendHeight} px, {scale} px/block); " +
            $"ground tones by terrain-paint family, structure charcoal, water blue, partial blocks amber, unnamed materials magenta");
        return 0;
    }

    /// <summary>The pure draw: a scanned column map in, the ground-only material map and an RGB pixel buffer
    /// out. No file or console I/O.</summary>
    private static Result Render(Dictionary<(int X, int Z), Cell> columns, int topMaterials,
        out Dictionary<(int X, int Z), (int Id, int Data)> ground)
    {
        ground = columns.Where(entry => !entry.Value.Built && entry.Value.Depth == 0)
            .ToDictionary(entry => entry.Key, entry => (entry.Value.Ground, entry.Value.GroundData));

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
                // A partial block gets its own colour rather than the unnamed magenta: it is not a gap in the
                // vocabulary, it is a thing standing on ground the vocabulary would have named.
                var tone = ToneName(column.Ground, column.GroundData);
                Raster.Set(pixels, blocksWide, col, row,
                    tone == "partial block" ? 0xC08030 : TerrainPalette.ColourOf(tone));
            }

        return new Result(pixels, blocksWide, blocksHigh, ground, columns.Count);
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
                    if (IsDecoration(id)) { if (decor == 0) { decor = id; decorData = data[(y << 8) | col]; } continue; }
                    if (BlockRoles.IsLiquid(id)) { depth++; continue; }
                    if (IsBuilt(id)) { built = true; continue; }

                    if (depth > 0) { bed = id; bedData = data[(y << 8) | col]; }
                    columns[(chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz)] =
                        new Cell(id, data[(y << 8) | col], y, decor, decorData, depth, bed, bedData, built, shaded);
                    break;
                }
            }
    }

    /// <summary>The identity a row is counted under, which must be the one it is printed under.
    ///
    /// <para>Block data carries a stair's facing and a wire's charge alongside a material's variant, and the
    /// palette already resolves all of them to one name. Counting the raw pair therefore splits one material
    /// across rows that read identically, divides its share between them, and — where the grouping also feeds
    /// a measurement — divides the measurement too: two stairs laid side by side in different facings are not
    /// each other's neighbours, so a staircase reports as random scatter.</para></summary>
    private static string Material(int id, int data) => BlockPalette.Name(id, data);

    private static void ReportMaterials(Dictionary<(int X, int Z), Cell> columns,
                                        Dictionary<(int X, int Z), (int Id, int Data)> ground, int top)
    {
        Console.WriteLine($"=== ground material ({ground.Count} open columns of {columns.Count}; " +
            $"{columns.Count(entry => entry.Value.Built)} carry structure, " +
            $"{columns.Count(entry => entry.Value.Shaded && !entry.Value.Built)} stand under a canopy and are counted, " +
            $"{columns.Count(entry => entry.Value.Depth > 0)} lie under liquid) ===");
        foreach (var group in ground.Values.GroupBy(entry => Material(entry.Id, entry.Data))
                     .OrderByDescending(group => group.Count()).Take(top))
            Console.WriteLine($"  {group.Key,-26} {group.Count(),7} " +
                $"{group.Count() * 100.0 / ground.Count,5:0.0}%");
    }

    private static void ReportTones(Dictionary<(int X, int Z), (int Id, int Data)> ground)
    {
        Console.WriteLine($"\n=== the same ground by tone ===");
        foreach (var group in ground.Values.GroupBy(entry => ToneName(entry.Id, entry.Data))
                     .OrderByDescending(group => group.Count()))
        {
            var members = group.GroupBy(entry => Material(entry.Id, entry.Data))
                .OrderByDescending(inner => inner.Count()).Take(4)
                .Select(inner => $"{inner.Key} {inner.Count() * 100 / group.Count()}%");
            Console.WriteLine($"  {group.Key,-12} {group.Count(),7} {group.Count() * 100.0 / ground.Count,5:0.0}%   " +
                $"{string.Join(", ", members)}");
        }
    }

    /// <summary>The same clustering question asked of tones rather than blocks. A field of one tone laid in
    /// four textures scores as four scattered materials and one solid field; only the second is the layout.</summary>
    private static void ReportTonePatchiness(Dictionary<(int X, int Z), (int Id, int Data)> ground)
    {
        Console.WriteLine($"\n=== the fields themselves, once texture within a tone stops counting as a boundary ===");
        Console.WriteLine($"  {"tone",-12} {"share",6} {"own neighbours",15} {"vs chance",10} {"fields",7} {"median",7} {"largest",8}");
        foreach (var group in ground.GroupBy(entry => ToneName(entry.Value.Id, entry.Value.Data))
                     .OrderByDescending(group => group.Count()))
        {
            var cells = new HashSet<(int X, int Z)>(group.Select(entry => entry.Key));
            var share = cells.Count / (double)ground.Count;
            var same = 0;
            foreach (var cell in cells)
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (cells.Contains((cell.X + dx, cell.Z + dz))) same++;
            var neighbourly = same / (4.0 * cells.Count);
            var sizes = Components(cells).OrderBy(size => size).ToList();
            Console.WriteLine($"  {group.Key,-12} {share * 100,5:0.0}% {neighbourly * 100,14:0.0}% " +
                $"{neighbourly / Math.Max(1e-9, share),9:0.0}x {sizes.Count,7} {sizes[sizes.Count / 2],7} {sizes[^1],8}");
        }
    }

    /// <summary>Where each tone sits in the landscape rather than on the plan. Height says which band of the
    /// terrain a tone belongs to; the steepest step to a neighbour says whether it was laid on flat ground or
    /// used to face something. A tone that marks cliffs and rocks has to show up as steep however common it
    /// is, and one that makes the landscape has to show up as flat however rare.</summary>
    private static void ReportRelief(Dictionary<(int X, int Z), Cell> columns)
    {
        var open = columns.Where(entry => !entry.Value.Built && entry.Value.Depth == 0).ToList();
        if (open.Count == 0) return;
        var height = open.ToDictionary(entry => entry.Key, entry => entry.Value.GroundY);
        var all = height.Values.OrderBy(y => y).ToList();

        Console.WriteLine($"\n=== where each tone sits: height band and the steepest step to a neighbour ===");
        Console.WriteLine($"  {"tone",-12} {"height p10..p90",16} {"median",7} {"slope median",13} {"steep (3+)",11}");
        Console.WriteLine($"  {"— whole map —",-12} {$"{all[all.Count / 10]}..{all[all.Count * 9 / 10]}",16} {all[all.Count / 2],7}");

        foreach (var group in open.GroupBy(entry => ToneName(entry.Value.Ground, entry.Value.GroundData))
                     .OrderByDescending(group => group.Count()))
        {
            var heights = group.Select(entry => entry.Value.GroundY).OrderBy(y => y).ToList();
            var slopes = new List<int>();
            foreach (var entry in group)
            {
                var steepest = 0;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (height.TryGetValue((entry.Key.X + dx, entry.Key.Z + dz), out var other))
                        steepest = Math.Max(steepest, Math.Abs(other - entry.Value.GroundY));
                slopes.Add(steepest);
            }
            slopes.Sort();
            var steep = slopes.Count(slope => slope >= 3) * 100.0 / slopes.Count;
            Console.WriteLine($"  {group.Key,-12} {$"{heights[heights.Count / 10]}..{heights[heights.Count * 9 / 10]}",16} " +
                $"{heights[heights.Count / 2],7} {slopes[slopes.Count / 2],13} {steep,10:0.0}%");
        }
    }

    private static void ReportDecoration(Dictionary<(int X, int Z), Cell> columns)
    {
        var decorated = columns.Values.Where(cell => cell.Decor != 0 && cell.Depth == 0).ToList();
        if (decorated.Count == 0) return;
        Console.WriteLine($"\n=== decoration ({decorated.Count} columns, " +
            $"{decorated.Count * 100.0 / columns.Count:0.0}% of the map) and the soil under each ===");
        foreach (var group in decorated.GroupBy(cell => Material(cell.Decor, cell.DecorData))
                     .OrderByDescending(group => group.Count()).Take(8))
        {
            var soils = group.GroupBy(cell => Material(cell.Ground, cell.GroundData))
                .OrderByDescending(inner => inner.Count()).Take(3)
                .Select(inner => $"{inner.Key} {inner.Count() * 100 / group.Count()}%");
            Console.WriteLine($"  {group.Key,-24} {group.Count(),6}  on {string.Join(", ", soils)}");
        }
    }

    private static void ReportBeds(Dictionary<(int X, int Z), Cell> columns)
    {
        var flooded = columns.Values.Where(cell => cell.Depth > 0).ToList();
        if (flooded.Count == 0) return;
        var depths = flooded.Select(cell => cell.Depth).OrderBy(depth => depth).ToList();
        Console.WriteLine($"\n=== what lies under the water ({flooded.Count} columns, depth " +
            $"{depths[0]}..{depths[^1]}, {depths[depths.Count / 2]} typical) ===");
        foreach (var group in flooded.GroupBy(cell => Material(cell.Bed, cell.BedData))
                     .OrderByDescending(group => group.Count()).Take(8))
            Console.WriteLine($"  {group.Key,-26} {group.Count(),6} " +
                $"{group.Count() * 100.0 / flooded.Count,5:0.0}%   depth " +
                $"{group.Select(cell => cell.Depth).OrderBy(depth => depth).ElementAt(group.Count() / 2)} typical");
    }

    /// <summary>How often each material neighbours its own kind, against how often it would by chance.</summary>
    private static void ReportPatchiness(Dictionary<(int X, int Z), (int Id, int Data)> ground, int top)
    {
        Console.WriteLine($"\n=== is it scattered or laid in fields? ===");
        Console.WriteLine($"  {"material",-26} {"share",6} {"own neighbours",15} {"vs chance",10} {"patches",8} {"median",7}");

        foreach (var group in ground.GroupBy(entry => Material(entry.Value.Id, entry.Value.Data))
                     .OrderByDescending(group => group.Count()).Take(top))
        {
            var cells = new HashSet<(int X, int Z)>(group.Select(entry => entry.Key));
            var share = cells.Count / (double)ground.Count;

            var same = 0;
            foreach (var cell in cells)
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (cells.Contains((cell.X + dx, cell.Z + dz))) same++;
            var neighbourly = same / (4.0 * cells.Count);

            var sizes = Components(cells).OrderBy(size => size).ToList();
            Console.WriteLine($"  {group.Key,-26} {share * 100,5:0.0}% " +
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
}
