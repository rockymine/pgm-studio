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

    /// <summary>
    /// Ground materials grouped by the tone they read as. A map is not laid out one block at a time — an
    /// author reaches for "the green", "the earth", "the grey stone" — so a per-block histogram splits one
    /// decision across four rows and reports the variation inside a field as though it were the field. Grass,
    /// green clay and lime clay are one surface with a texture; counted apart they look like three.
    ///
    /// <para>Unlike a path or roof palette this table is a property of the blocks rather than of a map:
    /// granite reads warm on every world, podzol reads dark. It is therefore a default rather than an
    /// argument, and a material it does not name is reported as unnamed instead of being forced into a
    /// family.</para>
    /// </summary>
    private static readonly (string Tone, int Rgb, (int Id, int Data)[] Blocks)[] Tones =
    [
        ("verdant",     0x5BA83C, [(2, -1), (159, 13), (159, 5), (35, 13), (35, 5), (31, -1)]),
        ("loam",        0x6B4A2E, [(3, -1), (5, 1), (5, 5), (159, 12), (35, 12), (60, -1)]),
        ("brick",       0xA85C3C, [(45, -1), (1, 1), (1, 2), (159, 14), (172, -1), (159, 1)]),
        ("grey stone",  0x9A9A96, [(1, 0), (1, 5), (1, 6), (13, -1), (98, -1), (14, -1), (15, -1),
                                   (16, -1), (21, -1), (56, -1), (73, -1), (129, -1), (42, -1), (159, 8)]),
        ("cobble",      0x5F6672, [(4, -1), (48, -1), (139, -1), (67, -1)]),
        ("pale stone",  0xD8D8D0, [(1, 3), (1, 4)]),
        ("sand",        0xE0D2A0, [(12, 0), (24, -1), (5, 2), (17, 2), (121, -1), (128, -1)]),
        ("rust",        0xB2542A, [(12, 1), (179, -1), (5, 4), (17, 4), (159, 1), (180, -1)]),
        ("dark",        0x3A3A44, [(159, 9), (35, 7), (7, -1), (159, 15), (49, -1), (159, 7), (35, 15)]),
        ("bright",      0xF2F6FA, [(155, -1), (80, -1), (78, -1), (159, 0), (35, 0), (43, 7), (44, 7)]),
    ];

    private static readonly Dictionary<(int Id, int Data), string> ToneOf = BuildTones();
    private static readonly Dictionary<string, int> ToneRgb =
        Tones.ToDictionary(entry => entry.Tone, entry => entry.Rgb);

    private static Dictionary<(int Id, int Data), string> BuildTones()
    {
        var table = new Dictionary<(int Id, int Data), string>();
        foreach (var (tone, _, blocks) in Tones)
            foreach (var block in blocks)
                table[block] = tone;
        return table;
    }

    /// <summary>The tone a block reads as, or "unnamed". An entry with data -1 claims every sub-type, so a
    /// specific one is checked first — dirt in all its forms is loam, but sand and red sand part company.</summary>
    private static string ToneName(int id, int data) =>
        ToneOf.TryGetValue((id, data), out var exact) ? exact
        : ToneOf.TryGetValue((id, -1), out var any) ? any : "unnamed";

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

    private readonly record struct Cell(int Ground, int GroundData, int GroundY, int Decor, int DecorData,
                                        int Depth, int Bed, int BedData, bool Built, bool Shaded);

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
        ReportTones(ground);
        ReportDecoration(columns);
        ReportBeds(columns);
        ReportPatchiness(ground, topMaterials);
        ReportTonePatchiness(ground);
        ReportRelief(columns);
        Draw(outPng, scale, columns, ground);
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
                        new Cell(id, data[(y << 8) | col], y, decor, decorData, depth, bed, bedData, built, shaded);
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

    private static void ReportTones(Dictionary<(int X, int Z), (int Id, int Data)> ground)
    {
        Console.WriteLine($"\n=== the same ground by tone ===");
        foreach (var group in ground.Values.GroupBy(entry => ToneName(entry.Id, entry.Data))
                     .OrderByDescending(group => group.Count()))
        {
            var members = group.GroupBy(entry => entry).OrderByDescending(inner => inner.Count()).Take(4)
                .Select(inner => $"{BlockPalette.Name(inner.Key.Id, inner.Key.Data)} {inner.Count() * 100 / group.Count()}%");
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
                             Dictionary<(int X, int Z), (int Id, int Data)> ground)
    {
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
                    ToneRgb.TryGetValue(ToneName(column.Ground, column.GroundData), out var rgb) ? rgb : 0xC020C0);
            }

        var scaled = Raster.Upscale(pixels, blocksWide, blocksHigh, scale);
        PngWriter.Write(outPng, blocksWide * scale, blocksHigh * scale, scaled);
        Console.WriteLine($"\n  wrote {outPng} ({blocksWide * scale}x{blocksHigh * scale} px, {scale} px/block); " +
            $"structure charcoal, water blue, unnamed materials magenta");
    }
}
