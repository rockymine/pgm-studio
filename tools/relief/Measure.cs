using PgmStudio.Geom.Relief;
using Footprint = PgmStudio.Geom.Relief.Footprint;
using PgmStudio.Minecraft;

namespace PgmStudio.Relief;

/// <summary>
/// Reads the ground surface out of a built world and hands it to the same analysis the solved fields go
/// through. It is what turns the readback from a self-consistent story into a calibrated one: the tiers, the
/// places-and-ledges split and the detour factor mean nothing until they have been run over maps people
/// actually built and played.
///
/// <para><b>Ground is not the topmost block.</b> A tree is seven blocks of trunk standing on flat grass, so
/// the topmost-block answer draws a forest as a plateau of spikes. Vegetation, trunks and the furniture
/// standing on terrain are stepped past, and so are liquids — a river read at its waterline flattens the
/// channel that gives the valley its shape, so the bed is the height and the water is recorded separately.
/// The rule is <see cref="BlockRoles"/>'s, so what counts as ground is decided once for the whole suite.</para>
/// </summary>
internal static class Measure
{
    /// <summary>
    /// Which surface a reading is of, and it is the difference between measuring a map and measuring its
    /// architecture. <see cref="Built"/> takes the top of everything a player stands on, walls and roofs
    /// included — the right surface for a question about play. <see cref="Natural"/> steps past a building's
    /// own courses to the terrain underneath, which is the only one a terrain solver can be calibrated
    /// against: a rampart is a three-block step and a landform is not, and a reading that cannot tell them
    /// apart reports a town as a mountain range.
    /// </summary>
    internal enum Surface { Built, Natural }

    private static bool NotGround(int id, Surface surface) => surface == Surface.Natural
        ? !BlockRoles.IsNaturalGround(id)
        : id == 0 || BlockRoles.IsLiquid(id) || BlockRoles.StandsOnGround(id);

    /// <summary>The ground surface of a world folder as a height field, plus the columns standing under
    /// liquid and the waterline they stand under.</summary>
    public static (HeightField Field, Dictionary<(int X, int Z), int> Flooded)? Read(
        string regionDir, Surface surface = Surface.Built, int margin = 0)
    {
        if (!Directory.Exists(regionDir)) return null;
        var files = Directory.GetFiles(regionDir, "*.mca");
        if (files.Length == 0) return null;

        var ground = new Dictionary<(int X, int Z), int>();
        var flooded = new Dictionary<(int X, int Z), int>();
        foreach (var mca in files)
            foreach (var chunk in AnvilRegion.ReadChunks(mca))
                Scan(chunk, ground, flooded, surface);
        if (ground.Count == 0) return null;

        int minX = ground.Keys.Min(cell => cell.X) - margin, maxX = ground.Keys.Max(cell => cell.X) + margin;
        int minZ = ground.Keys.Min(cell => cell.Z) - margin, maxZ = ground.Keys.Max(cell => cell.Z) + margin;
        var footprint = new Footprint(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
        footprint.Add(ground.Keys);

        var continuous = new double[footprint.Width * footprint.Depth];
        var blocks = new int[continuous.Length];
        foreach (var (cell, height) in ground)
        {
            var index = footprint.Index(cell.X, cell.Z);
            continuous[index] = height; blocks[index] = height;
        }
        return (new HeightField(footprint, continuous, blocks), flooded);
    }

    private static void Scan(AnvilRegion.Chunk chunk, Dictionary<(int X, int Z), int> ground,
                             Dictionary<(int X, int Z), int> flooded, Surface surface)
    {
        var ids = new ushort[256 * 256];
        foreach (var section in AnvilRegion.Sections(chunk))
        {
            var start = section.SectionY * 16;
            if (start is < 0 or >= 256) continue;
            Array.Copy(section.Ids, 0, ids, start * 256, 4096);
        }

        for (var lz = 0; lz < 16; lz++)
            for (var lx = 0; lx < 16; lx++)
            {
                var column = (lz << 4) | lx;
                var waterline = int.MinValue;
                for (var y = 255; y >= 0; y--)
                {
                    var id = ids[(y << 8) | column];
                    if (waterline == int.MinValue && BlockRoles.IsLiquid(id)) waterline = y;
                    if (NotGround(id, surface)) continue;
                    var cell = (chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz);
                    ground[cell] = y;
                    if (waterline != int.MinValue) flooded[cell] = waterline;
                    break;
                }
            }
    }

    /// <summary>The body of a map's relief rather than its extremes: the narrowest height band holding the
    /// given share of the columns. A single watchtower or a hole in the ground puts thirty blocks between the
    /// lowest and highest column of a board whose ground all sits within twelve, so the range is the wrong
    /// statistic and this is the right one.</summary>
    public static (int Low, int High) Body(HeightField field, double share = 0.95)
    {
        var heights = field.Footprint.Land().Select(cell => field.At(cell.X, cell.Z)).Order().ToList();
        var want = (int)Math.Ceiling(heights.Count * share);
        int bestLow = heights[0], bestHigh = heights[^1];
        for (var start = 0; start + want <= heights.Count; start++)
        {
            int low = heights[start], high = heights[start + want - 1];
            if (high - low < bestHigh - bestLow) { bestLow = low; bestHigh = high; }
        }
        return (bestLow, bestHigh);
    }

    /// <summary>One map's line in the corpus table.</summary>
    internal readonly record struct Reading(
        string Name, int Cells, int Wide, int Deep, int Span, int Body, double Walk, double Scramble,
        double Barrier, int Places, double Largest, int Ledges, int Cliffs, int WidestCliff, double Flooded);

    public static Reading? Of(string name, string regionDir, Surface surface = Surface.Built)
    {
        var read = Read(regionDir, surface);
        if (read is not var (field, flooded)) return null;

        var cells = field.Footprint.Land().ToList();
        if (cells.Count < 400) return null;

        var steps = cells.GroupBy(cell => Terrain.MaxStep(field, cell.X, cell.Z))
                         .ToDictionary(group => group.Key, group => group.Count());
        var (low, high) = Body(field);
        var onFoot = Terrain.Reachable(field, Terrain.Passage.Walk);
        var places = onFoot.Count(region => region.Count * 100 >= cells.Count);
        var scarps = Terrain.Scarps(field);
        var cliffs = scarps.Where(scarp => scarp.IsCliff).ToList();

        return new Reading(
            name, cells.Count,
            cells.Max(cell => cell.X) - cells.Min(cell => cell.X) + 1,
            cells.Max(cell => cell.Z) - cells.Min(cell => cell.Z) + 1,
            field.Max - field.Min, high - low,
            steps.Where(entry => entry.Key <= 1).Sum(entry => entry.Value) * 100.0 / cells.Count,
            steps.GetValueOrDefault(2) * 100.0 / cells.Count,
            steps.Where(entry => entry.Key >= 3).Sum(entry => entry.Value) * 100.0 / cells.Count,
            places, onFoot[0].Count * 100.0 / cells.Count, onFoot.Count - places,
            cliffs.Count, cliffs.Count > 0 ? cliffs.Max(cliff => cliff.Width) : 0,
            flooded.Count * 100.0 / cells.Count);
    }

    /// <summary>Runs the readback over every world under a corpus directory and reports the distribution.
    /// What comes out is the answer the pressure budget needs and design cannot supply: not what a surface
    /// should cost, but what the surfaces people ship actually do.</summary>
    public static void Corpus(string root, Action<string> say, Surface surface = Surface.Built, int limit = 500)
    {
        var maps = Directory.GetDirectories(root).OrderBy(path => path).Take(limit).ToList();
        var readings = new List<Reading>();
        foreach (var map in maps)
        {
            var region = Path.Combine(map, "region");
            if (!Directory.Exists(region)) continue;
            try
            {
                if (Of(Path.GetFileName(map), region, surface) is { } reading) readings.Add(reading);
            }
            catch (Exception error) { say($"  {Path.GetFileName(map)}: unreadable ({error.GetType().Name})"); }
        }
        if (readings.Count == 0) { say("  no readable worlds"); return; }

        say($"  read {readings.Count} maps ({(surface == Surface.Natural ? "natural ground, stepping past what was built" : "the built surface, walls and roofs included")})");
        void Spread(string label, Func<Reading, double> pick, string unit = "")
        {
            var values = readings.Select(pick).Order().ToList();
            double At(double q) => values[Math.Clamp((int)(values.Count * q), 0, values.Count - 1)];
            say($"  {label,-28} min {At(0):0.#}{unit}  p25 {At(.25):0.#}{unit}  median {At(.5):0.#}{unit}  " +
                $"p75 {At(.75):0.#}{unit}  max {At(1):0.#}{unit}");
        }
        Spread("plan width", reading => reading.Wide);
        Spread("plan depth", reading => reading.Deep);
        Spread("height range", reading => reading.Span);
        Spread("body (95% of columns)", reading => reading.Body);
        Spread("walk 0-1", reading => reading.Walk, "%");
        Spread("scramble 2", reading => reading.Scramble, "%");
        Spread("barrier 3+", reading => reading.Barrier, "%");
        Spread("largest place", reading => reading.Largest, "%");
        Spread("places of 1% or more", reading => reading.Places);
        Spread("cliffs (EL6)", reading => reading.Cliffs);
        Spread("under liquid", reading => reading.Flooded, "%");
    }
}
