using PgmStudio.Geom;
using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// Whether a board is actually the shape its symmetry says it is, read off the blocks.
///
/// <para><b>Nothing else here can answer that.</b> Every other stage image draws what is standing and leaves
/// the comparison to the eye, and an eye cannot hold two halves of a board against each other a block at a
/// time — a stamp one block from its own image looks exactly like a stamp on it. Worse, the pictures that
/// carry a structure's extent draw it from the provenance record, so a claim that is wrong about where its
/// blocks are draws a building where no building stands: a board can look asymmetric and be fine, or look
/// fine and be a block out. This reads neither the record nor the intent. It takes each column's own solid
/// spans, takes the column its orbit lands on, and asks whether the two are the same column.</para>
///
/// <para><b>The transform is the build's own.</b> Images come from <see cref="Symmetry.Cell"/> about the map's
/// stated centre, so what is measured is the same turn the stampers took — a picture computing its own
/// mirror could only ever agree with itself. A mode of order four asks all three images, so a column counts
/// as paired only when the whole orbit closes on it.</para>
///
/// <para><b>Shape and material are two questions and both are asked.</b> The first is the one a player can be
/// disadvantaged by: a column is solid where its image is solid, at the same heights. The second is the rest
/// of what a mirrored board claims — that the two halves are made of the same thing — and it is asked only of
/// the columns that already pair by shape, since a column standing somewhere its image does not has no
/// material to compare. A block's <b>data is skipped where it carries a team's colour</b>
/// (<see cref="BlockRoles.IsTeamColoured"/>): a red wool and a blue wool are the same block on the two sides
/// of a board, which is what a team tint is for. Everything else is compared as id and data both, because a
/// pattern resolves at the cell folded into the primary image (<c>terrain-painting.md</c> TP21) and therefore
/// answers alike on both sides.</para>
/// </summary>
public static class MirrorReport
{
    /// <summary>What the read found: the picture, and the count behind it. <see cref="Unpaired"/> is the
    /// number a caller gates or reports on — zero is a board that mirrors exactly.</summary>
    /// <param name="Unpaired">Columns whose solid spans differ from their orbit's.</param>
    /// <param name="Repainted">Columns that pair by shape and differ in what they are made of. Counted apart
    /// because they are a different fault: the ground is the same and its finish is not.</param>
    public sealed record Result(byte[] Pixels, int BlocksWide, int BlocksHigh, int Columns, int Unpaired,
                                int Repainted);

    private const int Void = 0x0E0E12, PairedLow = 0x23252B, PairedHigh = 0x8E9199;
    private const int Unpaired = 0xE0483C, Repainted = 0xE8A33D, Axis = 0x4FC3F7;

    /// <summary>Read a built world still in memory and write the picture.</summary>
    /// <summary>The finished symmetry picture as bytes, for a caller that wants the image rather than a
    /// file. Null where the world decodes to no column.</summary>
    public static byte[]? Png(VoxelWorld world, int scale, string? mode,
                              double centerX = 0, double centerZ = 0)
        => Emit(AnvilRegion.FromWorld(world), null, scale, mode, centerX, centerZ);

    public static int Run(VoxelWorld world, string outPng, int scale, string? mode,
                          double centerX = 0, double centerZ = 0)
        => Emit(AnvilRegion.FromWorld(world), outPng, scale, mode, centerX, centerZ) is null ? 1 : 0;

    /// <summary>Read a built region directory from disk and write the picture.</summary>
    public static int Run(string regionDir, string outPng, int scale, string? mode,
                          double centerX = 0, double centerZ = 0)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }
        return Emit(mcas.SelectMany(AnvilRegion.ReadChunks), outPng, scale, mode, centerX, centerZ) is null ? 1 : 0;
    }

    private static byte[]? Emit(IEnumerable<AnvilRegion.Chunk> chunks, string? outPng, int scale, string? mode,
                            double centerX, double centerZ)
    {
        var result = Render(chunks, mode, centerX, centerZ);
        if (result is null) { if (outPng is not null) Console.Error.WriteLine("no columns decoded"); return null; }

        var scaled = Raster.Upscale(result.Pixels, result.BlocksWide, result.BlocksHigh, scale);
        List<Legend.Entry> entries =
        [
            new("MIRRORED (SHADED BY HEIGHT)", PairedHigh),
            new("NOT MIRRORED", Unpaired),
            new("MIRRORED, PAINTED DIFFERENTLY", Repainted),
            new("SYMMETRY CENTRE", Axis),
            new("VOID", Void),
        ];
        var verdict = (result.Unpaired, result.Repainted) switch
        {
            (0, 0) => $"{result.Columns} COLUMNS, ALL MIRRORED",
            (0, var painted) => $"{result.Columns} COLUMNS MIRRORED, {painted} PAINTED DIFFERENTLY",
            (var shape, 0) => $"{shape} OF {result.Columns} COLUMNS NOT MIRRORED",
            var (shape, painted) => $"{shape} OF {result.Columns} COLUMNS NOT MIRRORED, {painted} PAINTED DIFFERENTLY",
        };
        var withLegend = Legend.AppendBelow(scaled, result.BlocksWide * scale, result.BlocksHigh * scale, entries,
            out var legendHeight,
            scaleLabel: $"SCALE: 1 BLOCK = {scale} PX - {result.BlocksWide} X {result.BlocksHigh} BLOCKS" +
                        $"  -  {(mode ?? "NONE").ToUpperInvariant()} ABOUT ({centerX:0.#}, {centerZ:0.#})  -  {verdict}");
        var png = PngWriter.Encode(result.BlocksWide * scale, legendHeight, withLegend);
        if (outPng is null) return png;
        File.WriteAllBytes(outPng, png);
        Console.WriteLine($"  wrote {outPng} ({result.BlocksWide * scale}x{legendHeight} px, {scale} px/block), "
                        + $"{verdict.ToLowerInvariant()}");
        return png;
    }

    /// <summary>The pure read: chunks in, picture and counts out. No file or console I/O. A mode with no
    /// orbit beyond the identity — <c>none</c>, empty, unknown — pairs every column with itself, so an
    /// unmirrored map draws as wholly mirrored rather than wholly broken, which is what it is.</summary>
    public static Result? Render(IEnumerable<AnvilRegion.Chunk> chunks, string? mode,
                                 double centerX = 0, double centerZ = 0)
    {
        var spans = new Dictionary<(int X, int Z), ulong[]>();
        var top = new Dictionary<(int X, int Z), int>();
        var made = new Dictionary<(int X, int Z), ulong>();
        foreach (var chunk in chunks)
        {
            int baseX = chunk.ChunkX * 16, baseZ = chunk.ChunkZ * 16;
            foreach (var (sectionY, ids, data) in AnvilRegion.Sections(chunk))
                for (var index = 0; index < 4096; index++)
                {
                    if (ids[index] == 0) continue;
                    var cell = (baseX + (index & 15), baseZ + ((index >> 4) & 15));
                    var y = sectionY * 16 + (index >> 8);
                    if (!spans.TryGetValue(cell, out var bits)) spans[cell] = bits = new ulong[4];
                    bits[y >> 6] |= 1UL << (y & 63);
                    if (!top.TryGetValue(cell, out var have) || y > have) top[cell] = y;
                    made[cell] = made.GetValueOrDefault(cell) ^ Fingerprint(y, ids[index], data[index]);
                }
        }
        if (spans.Count == 0) return null;

        // Both empty is a match and not a skip: a column with nothing in it is correctly mirrored by a column
        // with nothing in it, and calling that unpaired would paint every margin as a fault.
        bool SameShape((int X, int Z) a, (int X, int Z) b)
        {
            var hasA = spans.TryGetValue(a, out var left);
            var hasB = spans.TryGetValue(b, out var right);
            if (!hasA || !hasB) return hasA == hasB;
            return left![0] == right![0] && left[1] == right[1] && left[2] == right[2] && left[3] == right[3];
        }

        // A column with nothing in it is made of nothing, and so is its image — the same reading SameShape
        // gives an empty pair, for the same reason.
        bool SameMaterial((int X, int Z) a, (int X, int Z) b) =>
            made.GetValueOrDefault(a) == made.GetValueOrDefault(b);

        var order = Symmetry.Order(mode);
        bool Pairs(Func<(int X, int Z), (int X, int Z), bool> same, (int X, int Z) cell)
        {
            for (var k = 1; k < order; k++)
                if (!same(cell, Symmetry.Cell(cell.X, cell.Z, mode, centerX, centerZ, k))) return false;
            return true;
        }

        int minX = spans.Keys.Min(cell => cell.X), maxX = spans.Keys.Max(cell => cell.X);
        int minZ = spans.Keys.Min(cell => cell.Z), maxZ = spans.Keys.Max(cell => cell.Z);
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;

        var heights = top.Values.ToList();
        int lowest = heights.Min(), highest = heights.Max();
        var span = Math.Max(1, highest - lowest);

        var pixels = new byte[blocksWide * blocksHigh * 3];
        int unpaired = 0, repainted = 0;
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                var cell = (minX + col, minZ + row);
                if (!top.TryGetValue(cell, out var height)) { Raster.Set(pixels, blocksWide, col, row, Void); continue; }

                if (!Pairs(SameShape, cell))
                {
                    unpaired++;
                    Raster.Set(pixels, blocksWide, col, row, Unpaired);
                    continue;
                }
                // Material is asked only of a column that stands where its image stands: the finish of a
                // column with no partner is not a second fault, it is the same one seen again.
                if (!Pairs(SameMaterial, cell))
                {
                    repainted++;
                    Raster.Set(pixels, blocksWide, col, row, Repainted);
                    continue;
                }
                Raster.Set(pixels, blocksWide, col, row,
                           Raster.Lerp(PairedLow, PairedHigh, (height - lowest) / (double)span));
            }

        DrawCentre(pixels, blocksWide, blocksHigh, minX, minZ, mode, centerX, centerZ);
        return new Result(pixels, blocksWide, blocksHigh, spans.Count, unpaired, repainted);
    }

    /// <summary>What one block contributes to its column's material fingerprint: its height and what it is
    /// made of, mixed so that two columns holding the same blocks at the same heights agree and nothing else
    /// does. A block whose data is a team's colour contributes its id alone
    /// (<see cref="BlockRoles.IsTeamColoured"/>), which is what lets two sides keep their own colour under one
    /// comparison. Combined by XOR, so the sections may arrive in any order — every block carries its own
    /// height, so no two contributions of a column can cancel.</summary>
    private static ulong Fingerprint(int y, int blockId, int blockData)
    {
        var material = BlockRoles.IsTeamColoured(blockId) ? blockId : (blockId << 4) | (blockData & 15);
        var mixed = ((ulong)(uint)y << 32) ^ (uint)material;
        mixed = (mixed ^ (mixed >> 33)) * 0xFF51AFD7ED558CCDUL;
        mixed = (mixed ^ (mixed >> 33)) * 0xC4CEB9FE1A85EC53UL;
        return mixed ^ (mixed >> 33);
    }

    /// <summary>Mark where the turn happens, because "one block off" is only readable against the thing it is
    /// off from. A mirror mode has an axis and gets the line; a rotation has only a point and gets a
    /// crosshair — drawing a line for it would name an axis the board does not turn about.</summary>
    private static void DrawCentre(byte[] pixels, int blocksWide, int blocksHigh, int minX, int minZ,
                                   string? mode, double centerX, double centerZ)
    {
        void Mark(int col, int row)
        {
            if (col >= 0 && col < blocksWide && row >= 0 && row < blocksHigh)
                Raster.Over(pixels, blocksWide, col, row, Axis, 0.75);
        }

        int centreCol = (int)Math.Floor(centerX) - minX, centreRow = (int)Math.Floor(centerZ) - minZ;
        switch (mode)
        {
            case "mirror_x":
                for (var row = 0; row < blocksHigh; row++) Mark(centreCol, row);
                break;
            case "mirror_z":
                for (var col = 0; col < blocksWide; col++) Mark(col, centreRow);
                break;
            case "mirror_d1":
            case "mirror_d2":
                var down = mode == "mirror_d1" ? 1 : -1;
                for (var step = -blocksWide; step < blocksWide; step++) Mark(centreCol + step, centreRow + step * down);
                break;
            default:                                    // rot_90 / rot_180 / none — a point, not an axis
                for (var step = -4; step <= 4; step++) { Mark(centreCol + step, centreRow); Mark(centreCol, centreRow + step); }
                break;
        }
    }
}
