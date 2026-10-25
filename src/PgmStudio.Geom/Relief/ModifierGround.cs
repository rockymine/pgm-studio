namespace PgmStudio.Geom.Relief;

/// <summary>One mask outline measured against every column a modifier runs over: <see cref="E"/> for all of them,
/// and for a line also the arc length of the nearest point on it and which hand each column lies on.</summary>
internal sealed class AreaView
{
    public MaskArea Area { get; }
    public double[] E { get; }
    public double[]? Along { get; }
    public int[]? Side { get; }

    private readonly double[] _xs, _zs;

    public AreaView(MaskArea area, double[] xs, double[] zs)
    {
        Area = area;
        (_xs, _zs) = (xs, zs);
        E = new double[xs.Length];
        if (area.Shape == MaskShape.Line)
        {
            Along = new double[xs.Length];
            Side = new int[xs.Length];
            for (var i = 0; i < xs.Length; i++)
            {
                var fix = area.Fix(xs[i], zs[i]);
                (E[i], Along[i], Side[i]) = (fix.Distance / area.Scale, fix.Along, fix.Side);
            }
            return;
        }
        for (var i = 0; i < xs.Length; i++) E[i] = area.E(xs[i], zs[i]);
    }

    /// <summary>How many blocks column <paramref name="i"/> lies from the middle or the line, in the mask's own
    /// measure.</summary>
    public double Blocks(int i) => E[i] * Area.Scale;

    /// <summary>Where column <paramref name="i"/> stands in the frame of the outline as drawn
    /// (<see cref="MaskArea.ToDrawn"/>): what noise and scatter are read at, so each image of an area is the same.</summary>
    public (double X, double Z, string? Axis) Drawn(int i) => Area.ToDrawn(_xs[i], _zs[i]);

    public bool Is(params MaskShape[] shapes) => Array.IndexOf(shapes, Area.Shape) >= 0;
}

/// <summary>The ground as it stood when a modifier began: a frozen copy of the heights, and the columns found by
/// the cell they stand in, so an operation can read a neighbour, sample the ground along a path or resample it at
/// a shifted position without seeing its own change.</summary>
internal sealed class Ground
{
    private Dictionary<(int X, int Z), int>? _index;

    public double[] Heights { get; }
    public double[] Xs { get; }
    public double[] Zs { get; }

    /// <summary>The surface of the water earlier modifiers left standing on each column, NaN where there is none.</summary>
    public double[]? Standing { get; }

    public Ground(double[] heights, double[] xs, double[] zs, double[]? standing = null) =>
        (Heights, Xs, Zs, Standing) = ((double[])heights.Clone(), xs, zs, standing);

    /// <summary>The index of the column standing in cell (<paramref name="cellX"/>, <paramref name="cellZ"/>), or -1.</summary>
    public int IndexAt(int cellX, int cellZ)
    {
        if (_index is null)
        {
            _index = new Dictionary<(int X, int Z), int>(Heights.Length);
            for (var i = 0; i < Heights.Length; i++) _index[((int)Math.Floor(Xs[i]), (int)Math.Floor(Zs[i]))] = i;
        }
        return _index.TryGetValue((cellX, cellZ), out var found) ? found : -1;
    }

    /// <summary>The height of the column the point falls in, or null over no ground.</summary>
    public double? HeightAt(double x, double z)
    {
        var at = IndexAt((int)Math.Floor(x), (int)Math.Floor(z));
        return at < 0 ? null : Heights[at];
    }

    /// <summary>The ground along a path, one sample about every <paramref name="step"/> blocks: the arc length
    /// of each and the height of the column it falls in. A sample over no ground takes its nearest neighbour's
    /// height; a path over none at all answers null.</summary>
    public (double[] Arc, double[] Height)? Along(IReadOnlyList<(double X, double Z)> path, double step)
    {
        var arc = new List<double>();
        var height = new List<double?>();
        var run = 0.0;
        for (var i = 1; i < path.Count; i++)
        {
            var (ax, az) = path[i - 1];
            var (bx, bz) = path[i];
            var length = double.Hypot(bx - ax, bz - az);
            var samples = Math.Max(1, (int)(length / step));
            for (var k = 0; k < samples; k++)
            {
                var along = (double)k / samples;
                arc.Add(run + along * length);
                height.Add(HeightAt(ax + (bx - ax) * along, az + (bz - az) * along));
            }
            run += length;
        }
        arc.Add(run);
        height.Add(HeightAt(path[^1].X, path[^1].Z));

        var last = height.FindIndex(sample => sample is not null);
        if (last < 0) return null;
        var filled = new double[height.Count];
        double held = height[last]!.Value;
        for (var i = 0; i < filled.Length; i++)
        {
            held = height[i] ?? held;
            filled[i] = held;
        }
        for (var i = last - 1; i >= 0; i--) filled[i] = filled[last];
        return ([.. arc], filled);
    }

    /// <summary>The ground bilinearly interpolated between column centres at a point; a corner over no ground
    /// reads as <paramref name="fallback"/>.</summary>
    public double Sample(double x, double z, double fallback)
    {
        double gridX = x - 0.5, gridZ = z - 0.5;
        int cellX = (int)Math.Floor(gridX), cellZ = (int)Math.Floor(gridZ);
        double blendX = gridX - cellX, blendZ = gridZ - cellZ;
        double Corner(int dx, int dz)
        {
            var at = IndexAt(cellX + dx, cellZ + dz);
            return at < 0 ? fallback : Heights[at];
        }
        var near = Corner(0, 0) * (1 - blendX) + Corner(1, 0) * blendX;
        var far = Corner(0, 1) * (1 - blendX) + Corner(1, 1) * blendX;
        return near * (1 - blendZ) + far * blendZ;
    }

    /// <summary>The value of a profile given at <paramref name="arc"/> stations, read at one arc length and held
    /// at its ends.</summary>
    public static double Interpolate(double[] arc, double[] values, double at)
    {
        if (at <= arc[0]) return values[0];
        if (at >= arc[^1]) return values[^1];
        var upper = Array.BinarySearch(arc, at);
        if (upper >= 0) return values[upper];
        upper = ~upper;
        var span = arc[upper] - arc[upper - 1];
        return span > 0 ? values[upper - 1] + (values[upper] - values[upper - 1]) * (at - arc[upper - 1]) / span
                        : values[upper];
    }
}
