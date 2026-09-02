using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// A claim about a shape — a bank, a wall, a stair, a basin — read as a walked line rather than a single
/// column. A column answers what is at a coordinate; a transect answers whether a player can walk between two
/// of them, because the step between neighbouring stations is where a barrier or a drop actually shows: two
/// adjacent columns read <c>ground 38</c> and <c>ground 22</c> say nothing on their own, and a sixteen-course
/// drop between them says everything.
///
/// <para><see cref="Walk"/> is the pure computation — a world, its provenance, its terrain surface and its
/// rasterized spans, in; a station per point on the line, out. <see cref="Render"/> is the text twin, kept
/// beside it because the two answer the same walk.</para>
/// </summary>
public static class Transect
{
    /// <summary>One station along the walked line. <see cref="Ground"/> is the terrain's own recorded height,
    /// null over void. <see cref="Surface"/> is the top of the highest rasterized span at the cell, whatever
    /// layer drew it — the storey a walker actually stands on, equal to <see cref="Ground"/> on a flat board.
    /// <see cref="Water"/> is the highest liquid course in the world's column, or null where it holds none.
    /// <see cref="Top"/> is the highest block of any kind in the column — what stands there reaches this high.
    /// <see cref="Standing"/> names the claim a walker stands on as <c>"&lt;kind&gt; &lt;unit&gt;"</c>, or
    /// <c>"storey"</c> where the board stacks above the terrain with no claim of its own, else null.
    /// <see cref="Step"/> is the rise from the previous station's <see cref="Surface"/> — null at the first
    /// station and wherever either side is void; <see cref="Word"/> is <see cref="PgmStudio.Geom.Walk.StepWord"/>'s
    /// reading of it, <c>"walk"</c> where there is ground but no step to read, and <c>"void"</c> where there
    /// is no ground at all.</summary>
    public readonly record struct Station(int X, int Z, int? Ground, int? Surface, int? Water, int? Top,
        string? Standing, int? Step, string Word);

    /// <summary>One claim found within reach of the line — the first cell it was met at.</summary>
    public readonly record struct Neighbour(string Kind, string Unit, int Image, int X, int Z);

    /// <summary>The walked line and its totals. <see cref="Rises"/> and <see cref="Falls"/> count every
    /// station whose step climbed or fell, whatever word it earned; <see cref="Barriers"/>,
    /// <see cref="Scrambles"/> and <see cref="Drops"/> count the words themselves, and <see cref="WorstStep"/>
    /// is the largest rise or fall met, in blocks. <see cref="Events"/> is every non-walk step as a sentence,
    /// in the order the line meets them. <see cref="Beside"/> is the claims found within reach — empty both
    /// when none stood near the line and when nobody asked.</summary>
    public sealed record Walked(IReadOnlyList<Station> Stations, int Rises, int Falls, int WorstStep,
        int Barriers, int Scrambles, int Drops, IReadOnlyList<string> Events, IReadOnlyList<Neighbour> Beside);

    /// <summary>
    /// Walks <paramref name="points"/> block by block — Bresenham between each consecutive pair — thinned to
    /// one station every <paramref name="every"/> blocks, and answers what stands at each. <paramref
    /// name="beside"/> above zero also lists every distinct claim within that many cells of any station.
    /// </summary>
    public static Walked Walk(VoxelWorld world, WorldProvenance provenance,
        IReadOnlyDictionary<(int X, int Z), int> surface, IReadOnlyList<ColumnSegment>? columns,
        IReadOnlyList<(int X, int Z)> points, int every, int beside)
    {
        var storeys = (columns ?? []).GroupBy(segment => segment.Cell)
            .ToDictionary(group => group.Key, group => group.Max(segment => segment.YTop - 1));
        var cells = Sampled(points, every);

        var stations = new List<Station>(cells.Count);
        int? previousSurface = null;
        foreach (var cell in cells)
        {
            var ground = surface.TryGetValue(cell, out var groundTop) ? groundTop : (int?)null;
            var stationSurface = storeys.TryGetValue(cell, out var storeyTop) ? storeyTop : ground;
            var (top, water) = TopAndWater(world, cell.X, cell.Z);
            var owner = provenance.OwnerAt(cell.X, cell.Z);
            var standing = owner is { } id ? $"{id.Kind} {id.Unit}"
                : stationSurface is { } stationTop && ground is { } groundValue && stationTop > groundValue
                    ? "storey" : null;

            int? step = previousSurface is { } prior && stationSurface is { } here ? here - prior : null;
            var word = ground is null ? "void" : step is { } delta ? PgmStudio.Geom.Walk.StepWord(delta) : "walk";

            stations.Add(new Station(cell.X, cell.Z, ground, stationSurface, water, top, standing, step, word));
            previousSurface = stationSurface;
        }

        var rises = stations.Count(station => station.Step is > 0);
        var falls = stations.Count(station => station.Step is < 0);
        var worst = stations.Where(station => station.Step is not null)
            .Select(station => Math.Abs(station.Step!.Value)).DefaultIfEmpty(0).Max();
        var barriers = stations.Count(station => station.Word == "barrier");
        var scrambles = stations.Count(station => station.Word == "scramble");
        var drops = stations.Count(station => station.Word == "drop");
        var events = stations.Where(station => station.Word is "barrier" or "scramble" or "drop")
            .Select(station =>
                $"{EventLabel(station.Word)} {Signed(station.Step!.Value)} at ({station.X}, {station.Z})")
            .ToList();

        var near = Beside(provenance, cells, beside);

        return new Walked(stations, rises, falls, worst, barriers, scrambles, drops, events, near);
    }

    /// <summary>
    /// The walk as characters: a title line naming the line and the station count, a row per station, a
    /// summary line of the totals, and — where <paramref name="beside"/> was asked for — the claims found near
    /// it.
    /// </summary>
    public static string Render(Walked walked, IReadOnlyList<(int X, int Z)> points, int every, int beside)
    {
        var written = new System.Text.StringBuilder();
        var via = points.Count > 2
            ? " via " + string.Join(", ", points.Skip(1).Take(points.Count - 2).Select(p => $"({p.X}, {p.Z})"))
            : "";
        written.AppendLine($"TRANSECT {walked.Stations.Count} stations from ({points[0].X}, {points[0].Z}) to "
            + $"({points[^1].X}, {points[^1].Z}){via}, every {every}");
        written.AppendLine("  station        ground  surface  water  top  standing            step");

        foreach (var station in walked.Stations)
            written.AppendLine($"  ({station.X,5}, {station.Z,5})  {Cell(station.Ground),6}  "
                + $"{Cell(station.Surface),7}  {Cell(station.Water),5}  {Cell(station.Top),4}  "
                + $"{station.Standing ?? "",-18}  {StepDisplay(station)}");

        var summary = $"rises {walked.Rises}, falls {walked.Falls}, worst step {walked.WorstStep}: "
            + $"{walked.Barriers} barrier, {walked.Scrambles} scramble, {walked.Drops} drop";
        if (walked.Events.Count > 0) summary += " — " + string.Join("; ", walked.Events);
        written.AppendLine(summary + " | walked end to end");

        if (beside > 0)
            written.AppendLine(walked.Beside.Count == 0 ? "beside: (none)"
                : "beside: " + string.Join(", ", walked.Beside.Select(n => $"{n.Kind} {n.Unit} at ({n.X}, {n.Z})")));

        return written.ToString();
    }

    private static string Cell(int? value) => value?.ToString() ?? "";

    private static string StepDisplay(Station station) => station.Word switch
    {
        "void" => "void",
        "barrier" or "drop" or "scramble" => $"{EventLabel(station.Word)} {Signed(station.Step!.Value)}",
        _ => station.Step is null or 0 ? "" : Signed(station.Step.Value),
    };

    private static string EventLabel(string word) => word is "barrier" or "drop" ? word.ToUpperInvariant() : word;

    private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();

    /// <summary>Every distinct claim within <paramref name="radius"/> cells of any station, the first cell it
    /// was met at scanning the stations in order. Empty, not merely unsearched, where <paramref name="radius"/>
    /// is zero — nobody asked.</summary>
    private static List<Neighbour> Beside(
        WorldProvenance provenance, IReadOnlyList<(int X, int Z)> stations, int radius)
    {
        var near = new List<Neighbour>();
        if (radius <= 0) return near;

        var seen = new HashSet<StampId>();
        foreach (var station in stations)
            for (var dz = -radius; dz <= radius; dz++)
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var owner = provenance.OwnerAt(station.X + dx, station.Z + dz);
                    if (owner is not { } id || !seen.Add(id)) continue;
                    near.Add(new Neighbour(id.Kind, id.Unit, id.Image, station.X + dx, station.Z + dz));
                }
        return near;
    }

    /// <summary>The highest block of any kind in the column, and the highest liquid course in it — one scan
    /// down from the world's own ceiling, since a bridge deck over open water needs both answers from the
    /// same column.</summary>
    private static (int? Top, int? Water) TopAndWater(VoxelWorld world, int x, int z)
    {
        int? top = null, water = null;
        for (var y = VoxelWorld.MaxHeight - 1; y >= 0; y--)
        {
            var (id, _) = world.GetBlock(x, y, z);
            if (id == 0) continue;
            top ??= y;
            if (water is null && BlockRoles.IsLiquid(id)) water = y;
            if (top is not null && water is not null) break;
        }
        return (top, water);
    }

    /// <summary>Every point of the polyline walked block by block and thinned to one every
    /// <paramref name="every"/> blocks — the stations a caller actually sees.</summary>
    private static List<(int X, int Z)> Sampled(IReadOnlyList<(int X, int Z)> points, int every)
    {
        if (every < 1) every = 1;
        var full = new List<(int X, int Z)>();
        for (var i = 0; i < points.Count - 1; i++)
        {
            var segment = Bresenham(points[i], points[i + 1]).ToList();
            if (i > 0) segment.RemoveAt(0);
            full.AddRange(segment);
        }
        return [.. full.Where((_, index) => index % every == 0)];
    }

    /// <summary>Every cell a straight walk from <paramref name="from"/> to <paramref name="to"/> passes
    /// through, both ends included — the standard integer line, so the line a caller asked for is the line
    /// actually walked rather than an interpolation that can skip a cell.</summary>
    private static IEnumerable<(int X, int Z)> Bresenham((int X, int Z) from, (int X, int Z) to)
    {
        int x = from.X, z = from.Z;
        int dx = Math.Abs(to.X - from.X), sx = from.X < to.X ? 1 : -1;
        int dz = -Math.Abs(to.Z - from.Z), sz = from.Z < to.Z ? 1 : -1;
        var err = dx + dz;
        while (true)
        {
            yield return (x, z);
            if (x == to.X && z == to.Z) yield break;
            var doubled = 2 * err;
            if (doubled >= dz) { err += dz; x += sx; }
            if (doubled <= dx) { err += dx; z += sz; }
        }
    }
}
