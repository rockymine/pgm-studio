using PgmStudio.Minecraft;

namespace PgmStudio.RoundTrip;

/// <summary>
/// The world under its own roof, seen from above: for every column the terrain above is discarded and what
/// is drawn is the open space below it and the structures standing in that space. A surface render answers
/// what a player walks on; this one answers what a player finds after digging, which is the only view in
/// which a cave system, a ravine or a mineshaft has a shape at all.
///
/// <para><b>The roof is per column, not a plane.</b> Cutting the world at one height would slice through a
/// hillside and report its solid interior as "underground" while missing a tunnel that runs under a valley.
/// Each column instead finds its own highest terrain block — vegetation and the mineshaft's own furniture do
/// not count as terrain, or a torch would raise the roof above the tunnel it lights — and only space strictly
/// below that is considered enclosed. Open sky is therefore never mistaken for a cavity.</para>
///
/// <para><b>Depth is the base colour, structure is painted over it.</b> An enclosed column is shaded by how
/// high its topmost void sits, so tunnels running at one level read as one tone and a shaft between levels
/// reads as a gradient, and it is brightened by how much open space the column holds, so a ravine separates
/// from a one-block crawl. Onto that go the blocks that mark human or generated structure — rails, supports,
/// cobweb, torches, chests, spawners — in saturated colours no terrain shade reaches, so the skeleton of a
/// mineshaft stands out from the caves it runs through.</para>
///
/// <para><c>--band</c> narrows the scan to a height range, which turns the render from "every cavity" into a
/// reading of one level; <c>--ores</c> adds ore bodies, off by default because coal alone would speckle the
/// whole image.</para>
/// </summary>
internal static class UndergroundRender
{
    private const int SolidRock = 0x0D0E12;
    private const int DeepVoid = 0x241A3D;
    private const int ShallowVoid = 0xA8C0DC;

    /// <summary>Blocks that do not form a roof: air, plants, and the furniture a tunnel carries. A torch or a
    /// rail sits inside the space being measured, so counting it as terrain would hide that space.</summary>
    private static readonly HashSet<int> NotTerrain =
    [
        0, 6, 18, 27, 28, 30, 31, 32, 37, 38, 39, 40, 50, 51, 55, 59, 63, 64, 65, 66, 68, 69, 70, 71, 72,
        75, 76, 77, 78, 83, 85, 104, 105, 106, 111, 115, 131, 132, 141, 142, 143, 157, 161, 175,
    ];

    /// <summary>What is worth drawing over the depth shade, most telling first — a spawner names the structure
    /// it sits in, whereas planks appear in every corridor.</summary>
    private static readonly (int[] Ids, int Rgb, string Label)[] Structures =
    [
        ([52], 0xFF36C8, "spawner"),
        ([54, 146], 0xFF8A1F, "chest"),
        ([66, 27, 28, 157], 0xFFD84A, "rail"),
        ([30], 0xEEF0F8, "cobweb"),
        ([50], 0xFFF0A0, "torch"),
        ([5, 85, 53, 134, 135, 136], 0xA2713C, "mineshaft support"),
        ([10, 11], 0xFF5A12, "lava"),
        ([8, 9], 0x2F6FD8, "water"),
    ];

    private static readonly (int[] Ids, int Rgb, string Label)[] Ores =
    [
        ([56], 0x45E6DD, "diamond"), ([129], 0x17DD62, "emerald"), ([14], 0xF6CF3C, "gold"),
        ([73, 74], 0xE83030, "redstone"), ([21], 0x3A63D8, "lapis"), ([15], 0xCF9A6D, "iron"),
        ([16], 0x545454, "coal"),
    ];

    private sealed record Column(int VoidTop, int VoidCells, int StructureRank, int StructureRgb);

    public static int Run(string regionDir, string outPng, int scale, int? bandMin, int? bandMax, bool withOres)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }

        var palette = withOres ? [.. Structures, .. Ores] : Structures;
        var lookup = new Dictionary<int, (int Rank, int Rgb)>();
        for (var rank = 0; rank < palette.Length; rank++)
            foreach (var id in palette[rank].Ids) lookup.TryAdd(id, (rank, palette[rank].Rgb));

        var columns = new Dictionary<(int X, int Z), Column>();
        var found = new Dictionary<string, int>();
        foreach (var mca in mcas)
            foreach (var chunk in AnvilRegion.ReadChunks(mca))
                Scan(chunk, bandMin ?? 1, bandMax ?? 255, lookup, palette, columns, found);

        if (columns.Count == 0) { Console.Error.WriteLine("no columns decoded"); return 1; }

        int minX = columns.Keys.Min(cell => cell.X), maxX = columns.Keys.Max(cell => cell.X);
        int minZ = columns.Keys.Min(cell => cell.Z), maxZ = columns.Keys.Max(cell => cell.Z);
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;

        var open = columns.Values.Where(column => column.VoidCells > 0).ToList();
        int shallowest = open.Count > 0 ? open.Max(column => column.VoidTop) : 0;
        int deepest = open.Count > 0 ? open.Min(column => column.VoidTop) : 0;
        var span = Math.Max(1, shallowest - deepest);
        var thickest = open.Count > 0 ? open.Max(column => column.VoidCells) : 1;

        var pixels = new byte[blocksWide * blocksHigh * 3];
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                if (!columns.TryGetValue((minX + col, minZ + row), out var column) || (column.VoidCells == 0 && column.StructureRank < 0))
                { Raster.Set(pixels, blocksWide, col, row, SolidRock); continue; }

                var shade = SolidRock;
                if (column.VoidCells > 0)
                {
                    var depth = Raster.Lerp(DeepVoid, ShallowVoid, (column.VoidTop - deepest) / (double)span);
                    // A ravine holds far more open space than a corridor; the log keeps the corridor visible
                    // instead of letting the ravine own the whole brightness range.
                    var volume = Math.Log(1 + column.VoidCells) / Math.Log(1 + thickest);
                    shade = Raster.Scale(depth, 0.55 + 0.45 * volume);
                }
                Raster.Set(pixels, blocksWide, col, row, shade);
                if (column.StructureRank >= 0) Raster.Over(pixels, blocksWide, col, row, column.StructureRgb, 0.92);
            }

        var scaled = Raster.Upscale(pixels, blocksWide, blocksHigh, scale);
        PngWriter.Write(outPng, blocksWide * scale, blocksHigh * scale, scaled);

        var name = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(regionDir)) ?? regionDir);
        var band = bandMin is null && bandMax is null ? "roof-down" : $"y {bandMin?.ToString() ?? "1"}..{bandMax?.ToString() ?? "255"}";
        Console.WriteLine($"underground {name} ({band}): {open.Count} of {columns.Count} columns hold enclosed space, " +
            $"void tops y {deepest}..{shallowest}, thickest column {thickest} cells");
        foreach (var (label, count) in found.OrderByDescending(entry => entry.Value))
            Console.WriteLine($"  {label,-20} {count,7} columns");
        Console.WriteLine($"  wrote {outPng} ({blocksWide * scale}x{blocksHigh * scale} px, {scale} px/block)");
        return 0;
    }

    /// <summary>One chunk's columns: the roof, the enclosed space under it, and the most telling structure
    /// block standing in that space.</summary>
    private static void Scan(AnvilRegion.Chunk chunk, int bandMin, int bandMax,
                             Dictionary<int, (int Rank, int Rgb)> lookup, (int[] Ids, int Rgb, string Label)[] palette,
                             Dictionary<(int X, int Z), Column> columns, Dictionary<string, int> found)
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

                var roof = -1;
                for (var y = 255; y >= 0; y--)
                    if (!NotTerrain.Contains(ids[(y << 8) | col])) { roof = y; break; }
                if (roof < 0) continue;

                int voidTop = -1, voidCells = 0, structureRank = -1, structureRgb = 0;
                string? structureLabel = null;
                var top = Math.Min(bandMax, roof - 1);
                for (var y = top; y >= Math.Max(1, bandMin); y--)
                {
                    var id = ids[(y << 8) | col];
                    if (id == 0) { voidCells++; if (voidTop < 0) voidTop = y; continue; }
                    if (!lookup.TryGetValue(id, out var structure) || (structureRank >= 0 && structure.Rank >= structureRank)) continue;
                    structureRank = structure.Rank;
                    structureRgb = structure.Rgb;
                    structureLabel = palette[structure.Rank].Label;
                }

                if (voidCells == 0 && structureRank < 0) continue;
                columns[(chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz)] =
                    new Column(voidTop < 0 ? Math.Max(1, bandMin) : voidTop, voidCells, structureRank, structureRgb);
                if (structureLabel is not null) found[structureLabel] = found.GetValueOrDefault(structureLabel) + 1;
            }
    }
}
