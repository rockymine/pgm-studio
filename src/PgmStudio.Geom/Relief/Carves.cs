namespace PgmStudio.Geom.Relief;

/// <summary>Which carve a <see cref="CarveSpec"/> cuts.</summary>
public enum CarveOp
{
    /// <summary>A passage along a line, its floor level between its mouths.</summary>
    Tunnel,

    /// <summary>A hall under an area, domed.</summary>
    Chamber,

    /// <summary>A well from a floor up to the sky, with a ladder.</summary>
    Shaft,
}

/// <summary>
/// One carve: empty space cut into the built ground, which keeps the relief over it. A number left null takes
/// <see cref="CarveDefaults"/>.
/// </summary>
public sealed record CarveSpec
{
    public CarveOp Op { get; init; } = CarveOp.Tunnel;

    /// <summary>Where it cuts, every area already standing where it applies.</summary>
    public IReadOnlyList<MaskArea> Areas { get; init; } = [];

    /// <summary>For each area of a Tunnel, the floor stated at each of its points, null where none is: an end
    /// stating none stands at the ground there, and a point between stating none lies on the straight run between
    /// the stated floors either side of it.</summary>
    public IReadOnlyList<IReadOnlyList<double?>>? PointFloors { get; init; }

    /// <summary>A Chamber's or a Shaft's floor, as a world y.</summary>
    public double? Floor { get; init; }

    /// <summary>How high a Chamber's dome stands over its floor at its middle.</summary>
    public double? Height { get; init; }

    /// <summary>The fewest blocks of ground a Tunnel keeps as its roof, or else it is cut open to the sky there; the
    /// fewest a Chamber keeps over it.</summary>
    public double? Cover { get; init; }

    /// <summary>How far in from each end a Tunnel is cut open to the sky whatever its roof.</summary>
    public double? Mouth { get; init; }

    /// <summary>How far a Tunnel's floor dips under the straight run between its ends, at the middle.</summary>
    public double? Sag { get; init; }

    /// <summary>How much a Tunnel's width breathes along its run, as a share of it.</summary>
    public double? Wander { get; init; }

    /// <summary>How roughened a Chamber's wall is, as a share of its radius.</summary>
    public double? Jag { get; init; }

    /// <summary>Whether a Shaft carries a ladder up one wall.</summary>
    public bool Ladder { get; init; } = true;

    /// <summary>Whether the cave is dressed: floors of gravel and clay, stalactites, ore in the faces.</summary>
    public bool Dress { get; init; } = true;

    public int Seed { get; init; } = 1;
}

/// <summary>What a carve reads where its document states nothing.</summary>
public static class CarveDefaults
{
    public const double Cover = 3;
    public const double Mouth = 4;
    public const double TunnelWander = 0.2;

    /// <summary>How far over a Tunnel's floor its tube's axis runs, as a share of its radius.</summary>
    public const double TunnelLift = 0.45;
    public const double ChamberHeight = 6;
    public const double ChamberJag = 0.15;

    /// <summary>How far under the lowest ground of its area a Shaft with no floor stated reaches.</summary>
    public const double ShaftDepth = 16;
}

/// <summary>The cells one carve empties and the ladder it hangs.</summary>
/// <param name="Air">Every block it empties, as (x, y, z).</param>
/// <param name="Ladders">Every ladder block, as (x, y, z) and the block data that turns it to its wall.</param>
public sealed record CarveCut(IReadOnlyList<(int X, int Y, int Z)> Air, IReadOnlyList<(int X, int Y, int Z, int Data)> Ladders);

/// <summary>
/// Where a carve cuts, over a ground it reads the surface of. <c>groundTop(x, z)</c> answers the first course of
/// air over the ground at a column, or null where there is none; a carve never cuts a column with no ground.
/// Noise is read along a Tunnel's run and in a Chamber's drawn frame, so every image of a mask is cut alike.
/// </summary>
public static class Carves
{
    /// <summary>A ladder's data facing north, hung on the wall to its south.</summary>
    private const int LadderFacingNorth = 2;

    public static CarveCut Cut(CarveSpec spec, Func<int, int, int?> groundTop)
    {
        var air = new HashSet<(int X, int Y, int Z)>();
        var ladders = new List<(int X, int Y, int Z, int Data)>();
        for (var at = 0; at < spec.Areas.Count; at++)
        {
            var area = spec.Areas[at];
            switch (spec.Op)
            {
                case CarveOp.Tunnel when area.Shape == MaskShape.Line:
                    Tunnel(spec, area, spec.PointFloors is { } floors && at < floors.Count ? floors[at] : null, groundTop, air);
                    break;
                case CarveOp.Chamber when area.Shape is MaskShape.Ellipse or MaskShape.Polygon:
                    Chamber(spec, area, groundTop, air);
                    break;
                case CarveOp.Shaft when area.Shape is MaskShape.Ellipse or MaskShape.Polygon:
                    Shaft(spec, area, groundTop, air, ladders);
                    break;
            }
        }
        return new CarveCut([.. air.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ThenBy(cell => cell.Z)], ladders);
    }

    private static void Tunnel(CarveSpec spec, MaskArea drawn, IReadOnlyList<double?>? stated,
                               Func<int, int, int?> groundTop, HashSet<(int X, int Y, int Z)> air)
    {
        var points = drawn.Points;
        var arc = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
            arc[i] = arc[i - 1] + double.Hypot(points[i].X - points[i - 1].X, points[i].Z - points[i - 1].Z);
        var length = arc[^1];
        if (length <= 0) return;

        double? Stated(int i) => stated is not null && i < stated.Count ? stated[i] : null;
        double? AtGround(int i) => groundTop((int)Math.Floor(points[i].X), (int)Math.Floor(points[i].Z));
        var first = Stated(0) ?? AtGround(0);
        var last = Stated(points.Count - 1) ?? AtGround(points.Count - 1);
        if (first is null && last is null) return;
        var floors = new double?[points.Count];
        floors[0] = first ?? last;
        floors[^1] = last ?? first;
        for (var i = 1; i < points.Count - 1; i++) floors[i] = Stated(i);
        var known = Enumerable.Range(0, points.Count).Where(i => floors[i] is not null).ToArray();
        var floorAt = new double[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            if (floors[i] is { } floor) { floorAt[i] = floor; continue; }
            var before = known.Last(k => k < i);
            var after = known.First(k => k > i);
            var share = (arc[i] - arc[before]) / Math.Max(arc[after] - arc[before], 1e-9);
            floorAt[i] = floors[before]!.Value + (floors[after]!.Value - floors[before]!.Value) * share;
        }

        double radius = drawn.Scale, sag = spec.Sag ?? 0, mouth = spec.Mouth ?? CarveDefaults.Mouth;
        double cover = spec.Cover ?? CarveDefaults.Cover, wander = spec.Wander ?? CarveDefaults.TunnelWander;
        var path = drawn.Smoothed(1);
        var widest = radius * (1 + Math.Abs(wander));
        var (minX, minZ, maxX, maxZ) = path.Bounds!.Value;
        for (var x = (int)Math.Floor(minX - widest); x <= (int)Math.Ceiling(maxX + widest); x++)
            for (var z = (int)Math.Floor(minZ - widest); z <= (int)Math.Ceiling(maxZ + widest); z++)
            {
                if (groundTop(x, z) is not { } top) continue;
                var fix = path.Fix(x + 0.5, z + 0.5);
                if (fix.Distance > widest) continue;
                var run = fix.Along / Math.Max(path.Length, 1e-9) * length;
                var breathing = radius * (1 + wander * ValueNoise.Fractal(run, 0.5, 9, 2, spec.Seed));
                if (fix.Distance > breathing) continue;
                var floor = Math.Round(Ground.Interpolate(arc, floorAt, run) - sag * Math.Sin(Math.PI * run / length));
                var axis = floor + CarveDefaults.TunnelLift * breathing;
                var reach = Math.Sqrt(breathing * breathing - fix.Distance * fix.Distance);
                var crown = (int)Math.Ceiling(axis + reach) - 1;
                var cutting = run < mouth || run > length - mouth || crown >= top - cover;
                for (var y = (int)floor; y < top; y++)
                {
                    var rise = y + 0.5 - axis;
                    if (!cutting && rise > 0 && rise * rise + fix.Distance * fix.Distance > breathing * breathing) break;
                    air.Add((x, y, z));
                }
            }
    }

    private static void Chamber(CarveSpec spec, MaskArea area, Func<int, int, int?> groundTop,
                                HashSet<(int X, int Y, int Z)> air)
    {
        double height = Math.Max(1, spec.Height ?? CarveDefaults.ChamberHeight), cover = spec.Cover ?? CarveDefaults.Cover;
        var jag = spec.Jag ?? CarveDefaults.ChamberJag;
        var inside = Inside(area, groundTop);
        if (inside.Count == 0) return;
        var floor = spec.Floor ?? inside.Min(cell => cell.Top) - height - cover;
        foreach (var (x, z, top) in inside)
        {
            var (drawnX, drawnZ, _) = area.ToDrawn(x + 0.5, z + 0.5);
            var e = area.E(x + 0.5, z + 0.5) * (1 + jag * ValueNoise.Fractal(drawnX, drawnZ, 5, 2, spec.Seed));
            if (e >= 1) continue;
            var roof = floor + height * Math.Sqrt(1 - e * e);
            for (var y = (int)Math.Round(floor); y < roof && y < top - cover; y++) air.Add((x, y, z));
        }
    }

    private static void Shaft(CarveSpec spec, MaskArea area, Func<int, int, int?> groundTop,
                              HashSet<(int X, int Y, int Z)> air, List<(int X, int Y, int Z, int Data)> ladders)
    {
        var inside = Inside(area, groundTop);
        if (inside.Count == 0) return;
        var floor = (int)Math.Round(spec.Floor ?? inside.Min(cell => cell.Top) - CarveDefaults.ShaftDepth);
        foreach (var (x, z, top) in inside)
            for (var y = floor; y < top; y++) air.Add((x, y, z));
        if (!spec.Ladder) return;
        var (middleX, _) = area.Centre;
        var column = inside.Where(cell => cell.X == (int)Math.Floor(middleX)).DefaultIfEmpty(inside[0])
                           .OrderByDescending(cell => cell.Z).First();
        for (var y = floor; y < column.Top; y++) ladders.Add((column.X, y, column.Z, LadderFacingNorth));
    }

    private static List<(int X, int Z, int Top)> Inside(MaskArea area, Func<int, int, int?> groundTop)
    {
        var cells = new List<(int X, int Z, int Top)>();
        if (area.Bounds is not { } bounds) return cells;
        for (var x = (int)Math.Floor(bounds.MinX); x <= (int)Math.Ceiling(bounds.MaxX); x++)
            for (var z = (int)Math.Floor(bounds.MinZ); z <= (int)Math.Ceiling(bounds.MaxZ); z++)
                if (area.E(x + 0.5, z + 0.5) < 1 && groundTop(x, z) is { } top) cells.Add((x, z, top));
        return cells;
    }
}
