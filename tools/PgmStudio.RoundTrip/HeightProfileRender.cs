using PgmStudio.Minecraft;

namespace PgmStudio.RoundTrip;

/// <summary>
/// The terrain's shape alone: every column reduced to one ground height, drawn with no reference to what the
/// ground is made of. A material render answers what a place looks like and hides how it slopes; this answers
/// the opposite, which is the view a layout is actually judged in — where the high ground is, how a valley
/// runs, whether a route climbs.
///
/// <para><b>Ground is not the topmost block.</b> A tree is seven blocks of trunk and canopy standing on flat
/// grass, so the surface extractor's answer would draw a forest as a plateau of spikes. Vegetation, tree
/// trunks and the furniture standing on terrain are skipped, and so are liquids — a river read at its
/// waterline flattens the channel that gives the valley its shape, so the bed is the height and the river
/// appears as the cut it really is.</para>
///
/// <para><b>Three layers carry the reading.</b> A hypsometric ramp maps height to colour so altitude is
/// legible at a glance. Hillshade from a north-west sun multiplies over it, computed from a smoothed copy of
/// the grid — raw block heights step by whole blocks, so an unsmoothed gradient reports every one-block lip as
/// a cliff and the image turns to noise, while the colour and the contours stay on the exact heights.
/// Contour lines close the reading, because shading shows that a slope exists and only a contour says how much
/// it climbs.</para>
/// </summary>
internal static class HeightProfileRender
{
    /// <summary>Blocks that stand on the ground rather than being it: plants, tree trunks and canopy, surface
    /// furniture, and the liquids that would hide a channel's true depth.</summary>
    /// <summary>Not the ground: nothing, water, and everything standing on the ground rather than being it.
    /// The roles are <see cref="BlockRoles"/>, so a block this render must step past is decided once for the
    /// whole suite instead of here.</summary>
    private static bool NotGround(int id) =>
        id == 0 || BlockRoles.IsLiquid(id) || BlockRoles.StandsOnGround(id);

    /// <summary>Hypsometric stops, low to high — the convention a topographic map reads by.</summary>
    private static readonly (double At, int Rgb)[] Ramp =
    [
        (0.00, 0x27455E), (0.12, 0x336B57), (0.30, 0x5C9450), (0.48, 0x9DB258),
        (0.66, 0xC8AE64), (0.82, 0xC08A63), (0.93, 0xAE7A72), (1.00, 0xF0EAE4),
    ];

    private const double SunAzimuthDegrees = 315;
    private const double SunAltitudeDegrees = 45;

    public static int Run(string regionDir, string outPng, int scale, int contourInterval, bool greyscale, bool markWater)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }

        var ground = new Dictionary<(int X, int Z), int>();
        var flooded = new Dictionary<(int X, int Z), int>();
        foreach (var mca in mcas)
            foreach (var chunk in AnvilRegion.ReadChunks(mca))
                Scan(chunk, ground, flooded);
        if (ground.Count == 0) { Console.Error.WriteLine("no ground columns"); return 1; }

        int minX = ground.Keys.Min(cell => cell.X), maxX = ground.Keys.Max(cell => cell.X);
        int minZ = ground.Keys.Min(cell => cell.Z), maxZ = ground.Keys.Max(cell => cell.Z);
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;
        int lowest = ground.Values.Min(), highest = ground.Values.Max();
        var span = Math.Max(1, highest - lowest);

        if (contourInterval <= 0) contourInterval = Math.Max(1, (int)Math.Round(span / 12.0));

        // A dense grid, so the gradient and contour reads are array lookups rather than dictionary probes.
        var heights = new int[blocksWide * blocksHigh];
        for (var index = 0; index < heights.Length; index++) heights[index] = int.MinValue;
        foreach (var (cell, height) in ground) heights[(cell.Z - minZ) * blocksWide + (cell.X - minX)] = height;

        var smoothed = Smooth(heights, blocksWide, blocksHigh);
        var pixels = new byte[blocksWide * blocksHigh * 3];

        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                var height = heights[row * blocksWide + col];
                if (height == int.MinValue) { Raster.Set(pixels, blocksWide, col, row, 0x0E0E12); continue; }

                var position = (height - lowest) / (double)span;
                var tint = greyscale ? Raster.Lerp(0x1C1C20, 0xF2F2F0, position) : Hypsometric(position);
                var lit = Raster.Scale(tint, Hillshade(smoothed, blocksWide, blocksHigh, col, row));

                // A contour marks the cell whose band differs from the cell east or south of it, so a line
                // lands on one side of the boundary only and never doubles.
                if (CrossesBand(heights, blocksWide, blocksHigh, col, row, height, contourInterval))
                    lit = Raster.Scale(lit, height % (contourInterval * 5) == 0 ? 0.42 : 0.66);

                Raster.Set(pixels, blocksWide, col, row, lit);
                if (markWater && flooded.ContainsKey((minX + col, minZ + row)))
                    Raster.Over(pixels, blocksWide, col, row, 0x3C7FE0, 0.30);
            }

        var scaled = Raster.Upscale(pixels, blocksWide, blocksHigh, scale);
        PngWriter.Write(outPng, blocksWide * scale, blocksHigh * scale, scaled);

        var name = Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(regionDir)) ?? regionDir);
        Console.WriteLine($"height profile {name}: {ground.Count} columns over {blocksWide}x{blocksHigh} blocks, " +
            $"ground y {lowest}..{highest} (span {span}), contours every {contourInterval} " +
            $"(every {contourInterval * 5} emphasised)");
        Report(ground, flooded, lowest, highest);
        Console.WriteLine($"  wrote {outPng} ({blocksWide * scale}x{blocksHigh * scale} px, {scale} px/block)");
        return 0;
    }

    /// <summary>Highest ground block per column, plus the waterline where a column stands under liquid.</summary>
    private static void Scan(AnvilRegion.Chunk chunk, Dictionary<(int X, int Z), int> ground,
                             Dictionary<(int X, int Z), int> flooded)
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
                var waterline = int.MinValue;
                for (var y = 255; y >= 0; y--)
                {
                    var id = ids[(y << 8) | col];
                    if (waterline == int.MinValue && BlockRoles.IsLiquid(id)) waterline = y;
                    if (NotGround(id)) continue;
                    var cell = (chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz);
                    ground[cell] = y;
                    if (waterline != int.MinValue) flooded[cell] = waterline;
                    break;
                }
            }
    }

    /// <summary>A 3×3 mean of the height grid, used only for shading. Block heights change in whole-block
    /// steps, so a gradient taken on them reports every lip as a cliff; smoothing recovers the slope the
    /// terrain actually has without touching the heights the colour and contours read.</summary>
    private static double[] Smooth(int[] heights, int blocksWide, int blocksHigh)
    {
        var smoothed = new double[heights.Length];
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                double total = 0;
                var counted = 0;
                for (var dz = -1; dz <= 1; dz++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int nx = col + dx, nz = row + dz;
                        if (nx < 0 || nz < 0 || nx >= blocksWide || nz >= blocksHigh) continue;
                        var height = heights[nz * blocksWide + nx];
                        if (height == int.MinValue) continue;
                        total += height; counted++;
                    }
                smoothed[row * blocksWide + col] = counted == 0 ? 0 : total / counted;
            }
        return smoothed;
    }

    /// <summary>Lambertian relief from a fixed north-west sun, via the standard 3×3 slope/aspect gradient.
    /// Clamped away from black so a shadowed face keeps its elevation colour readable.</summary>
    private static double Hillshade(double[] heights, int blocksWide, int blocksHigh, int col, int row)
    {
        double At(int dx, int dz)
        {
            int nx = Math.Clamp(col + dx, 0, blocksWide - 1), nz = Math.Clamp(row + dz, 0, blocksHigh - 1);
            return heights[nz * blocksWide + nx];
        }

        var dzdx = ((At(1, -1) + 2 * At(1, 0) + At(1, 1)) - (At(-1, -1) + 2 * At(-1, 0) + At(-1, 1))) / 8.0;
        var dzdz = ((At(-1, 1) + 2 * At(0, 1) + At(1, 1)) - (At(-1, -1) + 2 * At(0, -1) + At(1, -1))) / 8.0;

        var slope = Math.Atan(Math.Sqrt(dzdx * dzdx + dzdz * dzdz));
        var aspect = Math.Atan2(dzdz, -dzdx);
        var zenith = (90 - SunAltitudeDegrees) * Math.PI / 180;
        var azimuth = (360 - SunAzimuthDegrees + 90) * Math.PI / 180;

        var shade = Math.Cos(zenith) * Math.Cos(slope) + Math.Sin(zenith) * Math.Sin(slope) * Math.Cos(azimuth - aspect);
        return Math.Clamp(0.55 + 0.75 * shade, 0.45, 1.45);
    }

    /// <summary>Whether a contour line belongs on this cell — its band differs from the cell east or south,
    /// so the line falls on one side of the boundary and is never drawn twice.</summary>
    private static bool CrossesBand(int[] heights, int blocksWide, int blocksHigh, int col, int row,
                                    int height, int interval)
    {
        var band = (int)Math.Floor(height / (double)interval);
        foreach (var (dx, dz) in new[] { (1, 0), (0, 1) })
        {
            int nx = col + dx, nz = row + dz;
            if (nx >= blocksWide || nz >= blocksHigh) continue;
            var other = heights[nz * blocksWide + nx];
            if (other != int.MinValue && (int)Math.Floor(other / (double)interval) != band) return true;
        }
        return false;
    }

    private static int Hypsometric(double position)
    {
        var at = Math.Clamp(position, 0, 1);
        for (var stop = 1; stop < Ramp.Length; stop++)
            if (at <= Ramp[stop].At)
                return Raster.Lerp(Ramp[stop - 1].Rgb, Ramp[stop].Rgb,
                    (at - Ramp[stop - 1].At) / (Ramp[stop].At - Ramp[stop - 1].At));
        return Ramp[^1].Rgb;
    }

    /// <summary>The distribution the picture cannot state: how much of the map sits at each height, and how
    /// much of it is under water.</summary>
    private static void Report(Dictionary<(int X, int Z), int> ground, Dictionary<(int X, int Z), int> flooded,
                              int lowest, int highest)
    {
        var bands = Math.Min(12, Math.Max(1, highest - lowest));
        var width = (highest - lowest + 1) / (double)bands;
        Console.WriteLine($"  {"ground y",-12} {"columns",8}  share");
        for (var band = bands - 1; band >= 0; band--)
        {
            int from = lowest + (int)(band * width), to = lowest + (int)((band + 1) * width) - 1;
            if (band == bands - 1) to = highest;
            var count = ground.Values.Count(height => height >= from && height <= to);
            if (count == 0) continue;
            var share = count * 100.0 / ground.Count;
            Console.WriteLine($"  {from,4}..{to,-6} {count,8}  {share,5:0.0}% {new string('#', (int)Math.Round(share / 2))}");
        }
        if (flooded.Count > 0)
            Console.WriteLine($"  under liquid {flooded.Count} columns ({flooded.Count * 100.0 / ground.Count:0.0}%), " +
                $"waterline y {flooded.Values.Min()}..{flooded.Values.Max()}");
    }
}
