using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Render;

/// <summary>Which coordinate the cut runs along: a range of <c>x</c> at one fixed <c>z</c>, or a range of
/// <c>z</c> at one fixed <c>x</c>. A section is always axis-aligned — a diagonal cut is not a shape this
/// world format needs, since every piece an author places is drawn on the grid.</summary>
public enum SectionAxis { AlongX, AlongZ }

/// <summary>
/// A vertical slice through a built world — the image half of the section read-back (<see cref="ColumnReport"/>
/// is the textual half). Every other renderer in this suite looks straight down; none of them can show a riser,
/// a ramp's step heights, a wall's courses, a building's storeys or a goal's clearance above the ground, because
/// all of those are differences <em>in Y</em> and a plan view has already thrown Y away by the time it draws a
/// pixel. This is the one renderer that keeps it.
///
/// <para><b>Material carries the reading, the same way it does in every other block render here.</b> A section
/// exists to check a <c>layered</c> material — the whole reason that material kind exists is to vary a colour
/// down a riser — so the diagnostic signal is which block sits at which height, not a category standing in for
/// it; <see cref="BlockPalette"/> already gives every material its own distinguishable colour, which is what
/// every plan-view render in the studio already leans on. What this render adds on top, in the spirit a stage
/// image should keep (a diagram, not a photograph): a background that never wears a real block's colour, so
/// "nothing recorded" is never mistaken for a block, and a horizontal scale ruled onto the image so a reader
/// can count blocks instead of guessing them from the picture's height.</para>
///
/// <para><b>Two modes, one picture.</b> At <c>depth</c> 0 the render samples a single one-block-thick slice,
/// which is right for checking a material and useless for looking at a map: a cut through solid ground is a
/// solid slab, because a solid slab is genuinely what sits on that plane. Above 0 it projects — each column
/// takes the nearest block at or behind the plane, up to <see cref="MaxDepth"/>, so a cut that misses a
/// house's walls still shows them. <b>Hue stays the material and depth is carried in the value</b>: the block
/// at the plane reads exactly as the slice draws it and everything behind reads as the same material further
/// away, which is what makes a building behind the cut read as a building rather than as a lighter smudge.
/// Hue could as easily have carried a category and does not, for the reason above — a depth mode that swapped
/// the material out would answer a different question than the mode it is a mode of.</para>
///
/// <para><b>Two different kinds of "nothing" are drawn two different ways.</b> A column inside a loaded chunk
/// with no block at some height is ordinary air, drawn as a pale neutral; a column whose chunk was never
/// written at all — outside the map, or a genuine hole nothing covers — is void, drawn as the same near-black
/// every other renderer here already uses for "no data". The distinction is what makes the Ashen Quarry mouth's
/// 102-column gap and an author's empty attic read as two different findings instead of one.</para>
/// </summary>
public static class SectionRender
{
    /// <summary>Loaded chunk, nothing recorded at this height — ordinary air.</summary>
    private const int AirRgb = 0xE7ECF3;

    /// <summary>No chunk here at all — the same "no data" near-black every plan-view renderer uses.</summary>
    private const int VoidRgb = 0x0E0E12;

    private const int MinorTickRgb = 0xFFFFFF;
    private const double MinorTickWeight = 0.16;
    private const int MajorTickRgb = 0xFFD400;
    private const double MajorTickWeight = 0.36;

    /// <summary>How deep the projection looks behind the cut before it gives up and calls the column empty.
    /// Sixteen blocks is a chunk and is more than any room this studio builds is deep, so a wall behind a
    /// wall is found and a building on the far side of a court is not dragged into a picture of this
    /// one.</summary>
    public const int MaxDepth = 16;

    public sealed record Result(byte[] Pixels, int Width, int Height, int RangeMin, int RangeMax, int FixedCoord,
        SectionAxis Axis, int LowestY, int HighestY, int Columns, int VoidColumns, int TickInterval, int Depth);

    /// <summary>Reads a built region directory from disk — the <c>RoundTrip</c> CLI's entry point.</summary>
    public static int Run(string regionDir, string outPng, SectionAxis axis, int rangeMin, int rangeMax,
        int fixedCoord, int scale, int? yMin, int? yMax, int tickInterval = 8, int depth = 0)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }
        return Emit(mcas.SelectMany(AnvilRegion.ReadChunks), outPng, axis, rangeMin, rangeMax, fixedCoord,
            scale, yMin, yMax, tickInterval, depth) is null ? 1 : 0;
    }

    /// <summary>Renders a world still held in memory, via <see cref="AnvilRegion.FromWorld"/>.</summary>
    /// <summary>The finished cut as bytes, for a caller that wants the image rather than a file. Null where
    /// nothing stands along the plane asked for.</summary>
    public static byte[]? Png(VoxelWorld world, SectionAxis axis, int rangeMin, int rangeMax,
        int fixedCoord, int scale, int? yMin, int? yMax, int tickInterval = 8, int depth = 0)
        => Emit(AnvilRegion.FromWorld(world), null, axis, rangeMin, rangeMax, fixedCoord, scale, yMin, yMax,
            tickInterval, depth);

    public static int Run(VoxelWorld world, string outPng, SectionAxis axis, int rangeMin, int rangeMax,
        int fixedCoord, int scale, int? yMin, int? yMax, int tickInterval = 8, int depth = 0)
        => Emit(AnvilRegion.FromWorld(world), outPng, axis, rangeMin, rangeMax, fixedCoord, scale, yMin, yMax, tickInterval, depth) is null ? 1 : 0;

    private static byte[]? Emit(IEnumerable<AnvilRegion.Chunk> chunks, string? outPng, SectionAxis axis, int rangeMin,
        int rangeMax, int fixedCoord, int scale, int? yMin, int? yMax, int tickInterval, int depth)
    {
        var result = Render(chunks, axis, rangeMin, rangeMax, fixedCoord, yMin, yMax, tickInterval, depth);
        if (result is null) { if (outPng is not null) Console.Error.WriteLine("no blocks found along that cut"); return null; }

        var scaled = Raster.Upscale(result.Pixels, result.Width, result.Height, scale);
        var alongLabel = axis == SectionAxis.AlongX ? "x" : "z";
        var fixedLabel = axis == SectionAxis.AlongX ? "z" : "x";

        // Every other colour in the picture is a real block's, drawn from the palette and named by the block
        // it is; what the key has to carry is the four the render itself invents, since a reader who cannot
        // tell air from unwritten chunk cannot tell an author's empty attic from a hole in the world.
        List<Legend.Entry> entries =
        [
            new("AIR (CHUNK LOADED)", AirRgb),
            new("VOID (NO CHUNK)", VoidRgb),
            new($"GRIDLINE EVERY {result.TickInterval}", MajorTickRgb),
            new($"HEAVY EVERY {result.TickInterval * 5}", MinorTickRgb),
        ];
        if (result.Depth > 0)
            entries.Add(new Legend.Entry($"DIMMED = UP TO {result.Depth} BLOCKS BEHIND THE CUT",
                Raster.Scale(0xFFFFFF, Recess)));
        var withLegend = Legend.AppendBelow(scaled, result.Width * scale, result.Height * scale, entries,
            out var legendHeight,
            scaleLabel: $"SCALE: 1 BLOCK = {scale} PX - {alongLabel.ToUpperInvariant()} "
                + $"{result.RangeMin}..{result.RangeMax} AT {fixedLabel.ToUpperInvariant()}={result.FixedCoord}"
                + $" - ROW 0 = Y{result.HighestY}, LAST = Y{result.LowestY}"
                + (result.Depth > 0 ? $" - PROJECTED {result.Depth} DEEP" : ""));
        var png = PngWriter.Encode(result.Width * scale, legendHeight, withLegend);
        if (outPng is null) return png;
        File.WriteAllBytes(outPng, png);

        Console.WriteLine($"section {alongLabel}={result.RangeMin}..{result.RangeMax} at {fixedLabel}={result.FixedCoord}" +
            (result.Depth > 0 ? $" projected {result.Depth} deep" : "") + ": " +
            $"{result.Columns} column(s) with a block, y {result.LowestY}..{result.HighestY}" +
            (result.VoidColumns > 0 ? $", {result.VoidColumns} void column(s)" : ""));
        Console.WriteLine($"  gridlines every {result.TickInterval} block(s) (heavy every {result.TickInterval * 5}), " +
            $"row 0 = y{result.HighestY}, last row = y{result.LowestY}");
        Console.WriteLine($"  wrote {outPng} ({result.Width * scale}x{legendHeight} px, {scale} px/block)");
        return png;
    }

    /// <summary>The pure render: chunks in, an RGB pixel buffer out. No file or console I/O.</summary>
    public static Result? Render(IEnumerable<AnvilRegion.Chunk> chunks, SectionAxis axis, int rangeMin, int rangeMax,
        int fixedCoord, int? yMin, int? yMax, int tickInterval = 8, int depth = 0)
    {
        if (rangeMin > rangeMax) (rangeMin, rangeMax) = (rangeMax, rangeMin);
        if (tickInterval <= 0) tickInterval = 8;

        var chunkList = chunks as ICollection<AnvilRegion.Chunk> ?? chunks.ToList();
        var loadedChunks = new HashSet<(int Cx, int Cz)>(chunkList.Select(chunk => (chunk.ChunkX, chunk.ChunkZ)));

        // One dictionary per cut coordinate, Y -> the nearest block at or behind the plane and how far behind
        // it stands, populated only for the line asked for. At depth 0 that is the plane itself and every
        // block is at distance 0, which is the single-slice read.
        depth = Math.Clamp(depth, 0, MaxDepth);
        var columns = new Dictionary<int, Dictionary<int, (int Id, int Data, int Behind)>>();
        foreach (var chunk in chunkList)
            foreach (var block in AnvilRegion.Blocks(chunk))
            {
                var cut = axis == SectionAxis.AlongX ? block.X : block.Z;
                var behind = (axis == SectionAxis.AlongX ? block.Z : block.X) - fixedCoord;
                if (behind < 0 || behind > depth) continue;
                if (cut < rangeMin || cut > rangeMax) continue;
                if (!columns.TryGetValue(cut, out var stack)) columns[cut] = stack = [];
                // Nearest wins: the projection answers what a viewer at the plane sees, so a block at the cut
                // is never hidden by one behind it, whichever order the chunks arrive in.
                if (!stack.TryGetValue(block.Y, out var standing) || behind < standing.Behind)
                    stack[block.Y] = (block.Id, block.Data, behind);
            }
        if (columns.Count == 0) return null;

        var lowestFound = columns.Values.SelectMany(stack => stack.Keys).Min();
        var highestFound = columns.Values.SelectMany(stack => stack.Keys).Max();
        var lowest = yMin ?? Math.Max(0, lowestFound - 1);
        var highest = yMax ?? Math.Min(255, highestFound + 1);
        if (highest < lowest) (lowest, highest) = (highest, lowest);

        var width = rangeMax - rangeMin + 1;
        var height = highest - lowest + 1;
        var pixels = new byte[width * height * 3];
        var voidColumns = 0;

        for (var col = 0; col < width; col++)
        {
            var cut = rangeMin + col;
            var loaded = loadedChunks.Contains(ChunkOf(axis, cut, fixedCoord));
            var hasAnyBlock = columns.TryGetValue(cut, out var stack);
            if (!hasAnyBlock) voidColumns++;   // nothing recorded here at all — unloaded, or a hole nothing covers

            for (var row = 0; row < height; row++)
            {
                var y = highest - row;
                var packed = hasAnyBlock && stack!.TryGetValue(y, out var block)
                    ? Recede(BlockPalette.PackedRgb(block.Id, block.Data), block.Behind, depth)
                    : loaded ? AirRgb : VoidRgb;
                Raster.Set(pixels, width, col, row, packed);
            }
        }

        DrawScale(pixels, width, height, lowest, highest, tickInterval);

        return new Result(pixels, width, height, rangeMin, rangeMax, fixedCoord, axis, lowest, highest,
            columns.Count, voidColumns, tickInterval, depth);
    }

    /// <summary>A block's own colour dimmed by how far behind the cut it stands — <b>hue is the material and
    /// value is the distance</b>, so the block at the plane reads exactly as the single slice draws it and
    /// everything behind reads as the same material further away.
    ///
    /// <para>Hue could as easily have carried a category, and does not, because the reason this render exists
    /// is to check <c>layered</c> materials — which block sits at which height, not a category standing in
    /// for it. A depth mode that swapped the material out would answer a different question than the mode it
    /// is a mode of.</para>
    ///
    /// <para>The far end keeps <see cref="Recess"/> of its brightness rather than fading to the background:
    /// a wall eight blocks back must still read as a wall, and a ramp that reaches the ground colour makes
    /// "deep" and "nothing there" the same pixel.</para></summary>
    private static int Recede(int packedRgb, int behind, int depth) =>
        depth <= 0 || behind <= 0 ? packedRgb
            : Raster.Scale(packedRgb, 1.0 - (1.0 - Recess) * behind / depth);

    /// <summary>What share of its brightness the furthest visible block keeps.</summary>
    private const double Recess = 0.45;

    /// <summary>Which chunk a cut coordinate falls in, on the fixed side of the line.</summary>
    private static (int Cx, int Cz) ChunkOf(SectionAxis axis, int cut, int fixedCoord) =>
        axis == SectionAxis.AlongX ? (FloorDiv16(cut), FloorDiv16(fixedCoord)) : (FloorDiv16(fixedCoord), FloorDiv16(cut));

    private static int FloorDiv16(int value) => value >= 0 ? value / 16 : (value - 15) / 16;

    /// <summary>Horizontal gridlines every <paramref name="tickInterval"/> blocks of Y, blended over the
    /// terrain rather than replacing it — the same discipline <see cref="HeightProfileRender"/> already uses
    /// for contours, so a section and a heightmap put a scale on the image the same way.</summary>
    private static void DrawScale(byte[] pixels, int width, int height, int lowest, int highest, int tickInterval)
    {
        for (var row = 0; row < height; row++)
        {
            var y = highest - row;
            if (y % tickInterval != 0) continue;
            var major = y % (tickInterval * 5) == 0;
            for (var col = 0; col < width; col++)
                Raster.Over(pixels, width, col, row, major ? MajorTickRgb : MinorTickRgb, major ? MajorTickWeight : MinorTickWeight);
        }
    }
}
