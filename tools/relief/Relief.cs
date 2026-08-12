namespace PgmStudio.Relief;

/// <summary>The footprint a relief is solved over: the block cells a shape group rasterizes into, held as a
/// dense grid over their bounding box so the solver's neighbour reads are array indexing. Membership follows
/// the sketch rasterizer's rule exactly — a cell is inside when its centre falls in the ring.</summary>
internal sealed class Footprint
{
    public readonly int MinX, MinZ, Width, Depth;
    private readonly bool[] _inside;

    public Footprint(int minX, int minZ, int width, int depth)
    {
        MinX = minX; MinZ = minZ; Width = width; Depth = depth;
        _inside = new bool[width * depth];
    }

    public int Count => _inside.Count(cell => cell);
    public int Index(int x, int z) => (z - MinZ) * Width + (x - MinX);
    public bool InBounds(int x, int z) => x >= MinX && z >= MinZ && x < MinX + Width && z < MinZ + Depth;
    public bool Inside(int x, int z) => InBounds(x, z) && _inside[Index(x, z)];
    public bool InsideAt(int index) => _inside[index];
    public (int X, int Z) CellAt(int index) => (MinX + index % Width, MinZ + index / Width);

    public IEnumerable<(int X, int Z)> Cells()
    {
        for (var index = 0; index < _inside.Length; index++)
            if (_inside[index]) yield return CellAt(index);
    }

    public void Add(IEnumerable<(int X, int Z)> cells)
    {
        foreach (var (x, z) in cells) if (InBounds(x, z)) _inside[Index(x, z)] = true;
    }

    public void Remove(IEnumerable<(int X, int Z)> cells)
    {
        foreach (var (x, z) in cells) if (InBounds(x, z)) _inside[Index(x, z)] = false;
    }

    /// <summary>The footprint a set of add/subtract rings covers, sized to their combined bounds plus a margin.</summary>
    public static Footprint Of(IEnumerable<(double[][] Ring, bool Subtract)> parts, int margin = 2)
    {
        var all = parts.SelectMany(part => part.Ring).ToList();
        int minX = (int)Math.Floor(all.Min(p => p[0])) - margin, maxX = (int)Math.Ceiling(all.Max(p => p[0])) + margin;
        int minZ = (int)Math.Floor(all.Min(p => p[1])) - margin, maxZ = (int)Math.Ceiling(all.Max(p => p[1])) + margin;
        var footprint = new Footprint(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
        foreach (var (ring, subtract) in parts)
        {
            var cells = Rasterize(ring, minX, minZ, maxX, maxZ);
            if (subtract) footprint.Remove(cells); else footprint.Add(cells);
        }
        return footprint;
    }

    public static List<(int X, int Z)> Rasterize(double[][] ring, int minX, int minZ, int maxX, int maxZ)
    {
        var cells = new List<(int, int)>();
        for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++)
                if (PointInRing(x + 0.5, z + 0.5, ring)) cells.Add((x, z));
        return cells;
    }

    public static bool PointInRing(double x, double z, double[][] ring)
    {
        var inside = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            double xi = ring[i][0], zi = ring[i][1], xj = ring[j][0], zj = ring[j][1];
            if (zi > z != zj > z && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
        }
        return inside;
    }
}

/// <summary>What an author states about height: a placed mark holding a patch of the footprint at a chosen
/// elevation. Every kind reduces to the same thing for the solver — a set of cells pinned to a value — which
/// is what lets one field carry a peak, a ridgeline, a contour ring and a flat bench at once.</summary>
internal abstract record Mark(double Height)
{
    /// <summary>The cells this mark pins, clipped to the footprint by the caller.</summary>
    public abstract IEnumerable<((int X, int Z) Cell, double Height)> Pins(Footprint footprint);
}

/// <summary>A high point or a hollow: one position, one height, and the radius over which it is held flat.
/// A radius of zero pins a single cell, which reads as a spike; anything from two up reads as a summit.</summary>
internal sealed record PointMark(double X, double Z, double MarkHeight, double Radius = 2)
    : Mark(MarkHeight)
{
    public override IEnumerable<((int X, int Z) Cell, double Height)> Pins(Footprint footprint)
    {
        for (var x = (int)(X - Radius) - 1; x <= X + Radius + 1; x++)
            for (var z = (int)(Z - Radius) - 1; z <= Z + Radius + 1; z++)
            {
                double dx = x + 0.5 - X, dz = z + 0.5 - Z;
                if (dx * dx + dz * dz <= Math.Max(0.5, Radius) * Math.Max(0.5, Radius) && footprint.Inside(x, z))
                    yield return ((x, z), MarkHeight);
            }
    }
}

/// <summary>A ridgeline, a valley floor or any drawn height line: a polyline held at a height, optionally
/// varying along its length when per-vertex heights are given. The width is the band around the centerline
/// that is held, so a ridge can be a knife edge or a broad shoulder.</summary>
internal sealed record LineMark(double[][] Points, double[] Heights, double Width = 1.5)
    : Mark(Heights.Length > 0 ? Heights[0] : 0)
{
    public override IEnumerable<((int X, int Z) Cell, double Height)> Pins(Footprint footprint)
    {
        var seen = new HashSet<(int, int)>();
        foreach (var (x, z) in footprint.Cells())
        {
            var (distance, along) = NearestOnPolyline(x + 0.5, z + 0.5, Points);
            if (distance > Width) continue;
            if (!seen.Add((x, z))) continue;
            yield return ((x, z), HeightAt(along));
        }
    }

    /// <summary>The stated height at a fractional position along the line — one height holds the whole run,
    /// several interpolate between the vertices so a ridge can fall as it goes.</summary>
    private double HeightAt(double along)
    {
        if (Heights.Length <= 1) return Heights.Length == 1 ? Heights[0] : 0;
        var scaled = Math.Clamp(along, 0, 1) * (Heights.Length - 1);
        var lower = Math.Min(Heights.Length - 2, (int)scaled);
        return Heights[lower] + (Heights[lower + 1] - Heights[lower]) * (scaled - lower);
    }

    /// <summary>Distance to the polyline and the fractional arc position of the nearest point.</summary>
    public static (double Distance, double Along) NearestOnPolyline(double x, double z, double[][] points)
    {
        double best = double.MaxValue, bestArc = 0, arc = 0;
        var total = 0.0;
        for (var i = 0; i + 1 < points.Length; i++)
            total += Math.Sqrt(Sq(points[i + 1][0] - points[i][0]) + Sq(points[i + 1][1] - points[i][1]));
        if (total <= 0) total = 1;

        for (var i = 0; i + 1 < points.Length; i++)
        {
            double ax = points[i][0], az = points[i][1], bx = points[i + 1][0], bz = points[i + 1][1];
            double dx = bx - ax, dz = bz - az, length2 = dx * dx + dz * dz;
            var t = length2 <= 0 ? 0 : Math.Clamp(((x - ax) * dx + (z - az) * dz) / length2, 0, 1);
            double px = ax + t * dx, pz = az + t * dz;
            var distance = Math.Sqrt(Sq(x - px) + Sq(z - pz));
            if (distance < best) { best = distance; bestArc = (arc + t * Math.Sqrt(length2)) / total; }
            arc += Math.Sqrt(length2);
        }
        return (best, bestArc);
    }

    private static double Sq(double value) => value * value;
}

/// <summary>A bench, a mesa top or a sunken floor: every cell inside a ring is held at one height, so the
/// field arrives at a genuinely flat surface rather than the rounded top an interpolation would give.</summary>
internal sealed record AreaMark(double[][] Ring, double MarkHeight) : Mark(MarkHeight)
{
    public override IEnumerable<((int X, int Z) Cell, double Height)> Pins(Footprint footprint)
    {
        foreach (var (x, z) in footprint.Cells())
            if (Footprint.PointInRing(x + 0.5, z + 0.5, Ring)) yield return ((x, z), MarkHeight);
    }
}

/// <summary>The whole outer boundary of the footprint held at one height — the statement that the land meets
/// the void at a known level, which is what stops a hill running off the edge of an island.</summary>
internal sealed record RimMark(double MarkHeight, int Depth = 1) : Mark(MarkHeight)
{
    public override IEnumerable<((int X, int Z) Cell, double Height)> Pins(Footprint footprint)
    {
        // A boundary cell is one step from the void, so the outermost ring answers 1 — the depth counts rings
        // inward from there rather than from zero, which nothing outside the footprint could occupy.
        foreach (var (x, z) in footprint.Cells())
            if (RingDistance(footprint, x, z) <= Depth) yield return ((x, z), MarkHeight);
    }

    private static int RingDistance(Footprint footprint, int x, int z)
    {
        for (var radius = 0; radius < 8; radius++)
            for (var dx = -radius; dx <= radius; dx++)
                for (var dz = -radius; dz <= radius; dz++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) == radius && !footprint.Inside(x + dx, z + dz))
                        return radius;
        return 99;
    }
}

/// <summary>How the field between the marks is decided. The three are genuinely different surfaces, not
/// tunings of one: <see cref="Idw"/> weights every mark by straight-line distance and so reaches through a
/// wall; <see cref="GeodesicIdw"/> measures the same weights along paths that stay on the land;
/// <see cref="Diffusion"/> solves for the smoothest surface that satisfies the marks, which is the only one
/// of the three whose extremes can only sit where an author put one.</summary>
internal enum Interpolator { Idw, GeodesicIdw, Diffusion }

/// <summary>The relief of one footprint: a base level, the marks stated on it, how the space between them is
/// filled, and the finishing that turns a continuous surface into blocks.</summary>
internal sealed record ReliefSpec
{
    public double Base { get; init; } = 4;
    public List<Mark> Marks { get; init; } = [];
    public Interpolator Interpolator { get; init; } = Interpolator.Diffusion;

    /// <summary>How far a mark's influence travels before the field falls back to <see cref="Base"/>, in
    /// blocks. Zero is unlimited reach — the surface is decided entirely by the marks, which is what a small
    /// shape wants. A finite reach makes each mark a local landform, which is what a large one wants.</summary>
    public double Reach { get; init; } = 0;

    /// <summary>Amplitude of the grain added after interpolation, in blocks — the wobble that stops a solved
    /// surface reading as machined. Its scale is the field's feature size.</summary>
    public double Grain { get; init; } = 0;
    public int GrainScale { get; init; } = 9;
    public uint Seed { get; init; } = 1;

    /// <summary>The block quantum the finished surface snaps to. One follows the field cell by cell; two is
    /// the step unit the hand-built corpus uses, and reads as deliberate terracing.</summary>
    public int Step { get; init; } = 1;

    /// <summary>The symmetry the grain is sampled through, when the relief is solved over a whole mirrored
    /// map. Without it the noise is drawn from map coordinates and so differs between a cell and its own
    /// mirror image — a difference small enough to be invisible and large enough to decide a fight.</summary>
    public string? FoldMode { get; init; }
    public double FoldCentreX { get; init; }
    public double FoldCentreZ { get; init; }
}

/// <summary>A solved height field over a footprint: the continuous surface and the block surface it snaps to.</summary>
internal sealed class HeightField
{
    public readonly Footprint Footprint;
    public readonly double[] Continuous;
    public readonly int[] Blocks;

    public HeightField(Footprint footprint, double[] continuous, int[] blocks)
    { Footprint = footprint; Continuous = continuous; Blocks = blocks; }

    public int At(int x, int z) => Footprint.Inside(x, z) ? Blocks[Footprint.Index(x, z)] : int.MinValue;
    public bool Has(int x, int z) => Footprint.Inside(x, z);
    public int Min => Footprint.Cells().Select(c => At(c.X, c.Z)).DefaultIfEmpty(0).Min();
    public int Max => Footprint.Cells().Select(c => At(c.X, c.Z)).DefaultIfEmpty(0).Max();

    /// <summary>A copy with the block surface replaced — how a composited shape or a carved bed is layered on
    /// without losing the field it was solved from.</summary>
    public HeightField WithBlocks(int[] blocks) => new(Footprint, Continuous, blocks);
}

internal static class ReliefSolver
{
    /// <summary>Solves a relief into a height field. The marks are collected once into a pinned-cell table —
    /// later marks win a contested cell, which is what makes a bench drawn over a slope flatten it — and the
    /// chosen interpolator fills the rest.</summary>
    /// <summary>Solves a relief into a height field. Passing the previous solve's continuous surface as
    /// <paramref name="warmStart"/> resumes from it instead of from flat, which is what makes a drag
    /// affordable: moving one mark perturbs the field locally, so the relaxation has only that perturbation
    /// left to carry rather than the whole surface to build.</summary>
    public static HeightField Solve(Footprint footprint, ReliefSpec spec, double[]? warmStart = null, int sweeps = 1200)
    {
        var pinned = new double[footprint.Width * footprint.Depth];
        var isPinned = new bool[pinned.Length];
        foreach (var mark in spec.Marks)
            foreach (var (cell, height) in mark.Pins(footprint))
            {
                if (!footprint.Inside(cell.X, cell.Z)) continue;
                var index = footprint.Index(cell.X, cell.Z);
                pinned[index] = height; isPinned[index] = true;
            }

        var field = spec.Interpolator switch
        {
            Interpolator.Idw => Weighted(footprint, pinned, isPinned, spec, geodesic: false),
            Interpolator.GeodesicIdw => Weighted(footprint, pinned, isPinned, spec, geodesic: true),
            _ => Diffuse(footprint, pinned, isPinned, spec, warmStart, sweeps),
        };

        if (spec.Grain > 0)
            foreach (var (x, z) in footprint.Cells())
            {
                var index = footprint.Index(x, z);
                if (isPinned[index]) continue;                       // grain never overrides a statement
                var (sx, sz) = Fold(x, z, spec);
                field[index] += (Noise.Field(sx, sz, spec.Seed, spec.GrainScale, 3) - 0.5) * 2 * spec.Grain;
            }

        // Symmetry is imposed on the surface, not merely on the statements that made it. Mirrored marks give
        // a field that agrees with its own image to within the solver's tolerance, and a tolerance is exactly
        // what rounding turns into a whole block: two cells at 8.499 and 8.501 become 8 and 9, and one team
        // owns a step the other does not. Copying the canonical half's value across the pair before
        // quantizing costs nothing and makes the guarantee exact rather than close.
        if (spec.FoldMode is not null)
        {
            var symmetric = (double[])field.Clone();
            foreach (var (x, z) in footprint.Cells())
            {
                var (sx, sz) = Fold(x, z, spec);
                if (footprint.Inside(sx, sz)) symmetric[footprint.Index(x, z)] = field[footprint.Index(sx, sz)];
            }
            field = symmetric;
        }

        var blocks = new int[field.Length];
        var step = Math.Max(1, spec.Step);
        foreach (var (x, z) in footprint.Cells())
        {
            var index = footprint.Index(x, z);
            blocks[index] = (int)Math.Round((field[index] - spec.Base) / step) * step + (int)Math.Round(spec.Base);
        }
        return new HeightField(footprint, field, blocks);
    }

    /// <summary>The coordinate a cell's grain is drawn from: itself, or — once a fold is declared — whichever
    /// of the cell and its mirror image lies in the canonical half. Both members of a mirrored pair then read
    /// the same lattice point, so the grain fans with the map instead of being redrawn per side.</summary>
    private static (int X, int Z) Fold(int x, int z, ReliefSpec spec)
    {
        if (spec.FoldMode is null) return (x, z);
        // The reflection is of the cell's centre, not its corner — the same convention the rasterizer's own
        // mirroring uses. Taking the corner instead pairs each cell with its image's neighbour, which is a
        // one-cell shear that looks like symmetry and measures as a whole block of unfairness.
        double centreX = x + 0.5, centreZ = z + 0.5;
        int mirroredX = (int)Math.Floor(2 * spec.FoldCentreX - centreX);
        int mirroredZ = (int)Math.Floor(2 * spec.FoldCentreZ - centreZ);
        return spec.FoldMode switch
        {
            "mirror_x" => centreX <= spec.FoldCentreX ? (x, z) : (mirroredX, z),
            "mirror_z" => centreZ <= spec.FoldCentreZ ? (x, z) : (x, mirroredZ),
            // A half-turn has no single axis, so the canonical half is one side of a line through the centre,
            // broken along it by the other coordinate so the two images never both claim to be canonical.
            _ => centreZ < spec.FoldCentreZ || (centreZ == spec.FoldCentreZ && centreX <= spec.FoldCentreX)
                ? (x, z) : (mirroredX, mirroredZ),
        };
    }

    /// <summary>Inverse-distance weighting over the marks. With <paramref name="geodesic"/> the distance is
    /// measured by a chamfer sweep that only ever steps onto land, so a mark on one arm of an L no longer
    /// reaches the other arm through the notch.</summary>
    private static double[] Weighted(Footprint footprint, double[] pinned, bool[] isPinned, ReliefSpec spec, bool geodesic)
    {
        var sources = Enumerable.Range(0, pinned.Length).Where(index => isPinned[index]).ToList();
        var field = new double[pinned.Length];
        if (sources.Count == 0) { Array.Fill(field, spec.Base); return field; }

        // Marks are grouped into connected patches so a broad bench weighs as one statement rather than as
        // several hundred coincident ones, which would drown every other mark near it.
        var patches = Patches(footprint, sources, isPinned);
        var distances = patches.Select(patch => geodesic
            ? GeodesicDistance(footprint, patch)
            : EuclideanDistance(footprint, patch)).ToList();

        foreach (var (x, z) in footprint.Cells())
        {
            var index = footprint.Index(x, z);
            if (isPinned[index]) { field[index] = pinned[index]; continue; }
            double weightSum = 0, valueSum = 0;
            for (var patch = 0; patch < patches.Count; patch++)
            {
                var distance = distances[patch][index];
                if (double.IsPositiveInfinity(distance)) continue;
                var weight = 1.0 / Math.Pow(Math.Max(0.5, distance), 2);
                weightSum += weight;
                valueSum += weight * pinned[patches[patch][0]];
            }
            field[index] = weightSum > 0 ? valueSum / weightSum : spec.Base;
        }
        return field;
    }

    private static List<List<int>> Patches(Footprint footprint, List<int> sources, bool[] isPinned)
    {
        var seen = new HashSet<int>();
        var patches = new List<List<int>>();
        foreach (var start in sources)
        {
            if (!seen.Add(start)) continue;
            var patch = new List<int> { start };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var (x, z) = footprint.CellAt(queue.Dequeue());
                foreach (var (dx, dz) in Neighbours4)
                {
                    if (!footprint.Inside(x + dx, z + dz)) continue;
                    var next = footprint.Index(x + dx, z + dz);
                    if (!isPinned[next] || !seen.Add(next)) continue;
                    patch.Add(next); queue.Enqueue(next);
                }
            }
            patches.Add(patch);
        }
        return patches;
    }

    private static readonly (int X, int Z)[] Neighbours4 = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int X, int Z)[] Neighbours8 =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    private static double[] EuclideanDistance(Footprint footprint, List<int> patch)
    {
        var distance = new double[footprint.Width * footprint.Depth];
        Array.Fill(distance, double.PositiveInfinity);
        var seeds = patch.Select(footprint.CellAt).ToList();
        foreach (var (x, z) in footprint.Cells())
        {
            var best = double.PositiveInfinity;
            foreach (var (sx, sz) in seeds)
            {
                var d2 = (double)(x - sx) * (x - sx) + (double)(z - sz) * (z - sz);
                if (d2 < best) best = d2;
            }
            distance[footprint.Index(x, z)] = Math.Sqrt(best);
        }
        return distance;
    }

    /// <summary>Distance measured only across cells that are land — a two-pass chamfer sweep with 3/4 weights,
    /// repeated until it stops improving, which is enough for the doubling-back an L or a ring forces.</summary>
    private static double[] GeodesicDistance(Footprint footprint, List<int> patch)
    {
        var distance = new double[footprint.Width * footprint.Depth];
        Array.Fill(distance, double.PositiveInfinity);
        foreach (var index in patch) distance[index] = 0;

        for (var sweep = 0; sweep < 8; sweep++)
        {
            var changed = false;
            for (var pass = 0; pass < 2; pass++)
            {
                var forward = pass == 0;
                for (var i = 0; i < distance.Length; i++)
                {
                    var index = forward ? i : distance.Length - 1 - i;
                    if (!footprint.InsideAt(index)) continue;
                    var (x, z) = footprint.CellAt(index);
                    var best = distance[index];
                    foreach (var (dx, dz) in Neighbours8)
                    {
                        if (!footprint.Inside(x + dx, z + dz)) continue;
                        var step = dx != 0 && dz != 0 ? 1.41421356 : 1.0;
                        var candidate = distance[footprint.Index(x + dx, z + dz)] + step;
                        if (candidate < best) best = candidate;
                    }
                    if (best < distance[index]) { distance[index] = best; changed = true; }
                }
            }
            if (!changed) break;
        }
        return distance;
    }

    /// <summary>The smoothest surface that satisfies the marks: a screened-Poisson relaxation over the
    /// footprint, run with successive over-relaxation and started from a coarse solve so a sixty-by-ninety
    /// field settles in a few hundred sweeps rather than tens of thousands.
    ///
    /// <para>Cells off the footprint are simply not read, which makes the land's outline a no-flow boundary:
    /// the surface neither climbs nor falls as it meets the void unless a mark says so, and it can only bend
    /// around a notch rather than through it. The screening term pulls the field toward the base at a rate
    /// set by <see cref="ReliefSpec.Reach"/>; at zero it is absent and the marks decide everything.</para></summary>
    private static double[] Diffuse(Footprint footprint, double[] pinned, bool[] isPinned, ReliefSpec spec,
                                    double[]? warmStart, int sweeps)
    {
        var anyPin = isPinned.Any(pin => pin);
        if (!anyPin)
        {
            var flat = new double[pinned.Length];
            Array.Fill(flat, spec.Base);
            return flat;
        }

        double[] field;
        if (warmStart is { } previous && previous.Length == pinned.Length) field = (double[])previous.Clone();
        else
        {
            field = new double[pinned.Length];
            Array.Fill(field, Enumerable.Range(0, pinned.Length).Where(i => isPinned[i]).Average(i => pinned[i]));
        }
        for (var index = 0; index < pinned.Length; index++) if (isPinned[index]) field[index] = pinned[index];

        // The screening strength that gives a mark the requested reach: the field decays toward the base over
        // a characteristic length of one over the root of this, so reach is stated in blocks.
        var screen = spec.Reach > 0 ? 1.0 / (spec.Reach * spec.Reach) : 0.0;
        Relax(footprint, field, pinned, isPinned, screen, spec.Base, sweeps);
        return field;
    }

    private static void Relax(Footprint footprint, double[] field, double[] pinned, bool[] isPinned,
                              double screen, double baseHeight, int sweeps)
    {
        const double OverRelaxation = 1.85;
        for (var sweep = 0; sweep < sweeps; sweep++)
        {
            var movement = 0.0;
            // Red-black ordering, so the sweep is order-independent and converges evenly from both directions.
            for (var parity = 0; parity < 2; parity++)
                for (var index = 0; index < field.Length; index++)
                {
                    if (!footprint.InsideAt(index) || isPinned[index]) continue;
                    var (x, z) = footprint.CellAt(index);
                    if (((x + z) & 1) != parity) continue;

                    double sum = 0; var count = 0;
                    foreach (var (dx, dz) in Neighbours4)
                        if (footprint.Inside(x + dx, z + dz)) { sum += field[footprint.Index(x + dx, z + dz)]; count++; }
                    if (count == 0) continue;

                    var target = (sum + screen * baseHeight) / (count + screen);
                    var moved = OverRelaxation * (target - field[index]);
                    field[index] += moved;
                    movement = Math.Max(movement, Math.Abs(moved));
                }
            if (movement < 1e-4) break;
        }
        // Pinned cells are re-imposed so nothing above can have drifted them.
        for (var index = 0; index < field.Length; index++) if (isPinned[index]) field[index] = pinned[index];
    }
}

/// <summary>Deterministic value noise — the same hash-from-cell discipline the terrain patterns use, so a
/// relief re-solves identically on any machine and a seed is a real handle rather than a wish.</summary>
internal static class Noise
{
    public static uint Hash(int x, int z, uint seed)
    {
        unchecked
        {
            uint h = seed + 0x9E3779B9u;
            h ^= (uint)x * 0x85EBCA77u; h = (h << 13) | (h >> 19); h *= 0xC2B2AE3Du;
            h ^= (uint)z * 0x27D4EB2Fu; h = (h << 15) | (h >> 17); h *= 0x165667B1u;
            h ^= h >> 16; return h;
        }
    }

    public static double Unit(int x, int z, uint seed) => (Hash(x, z, seed) & 0xFFFFFF) / (double)0x1000000;

    public static double Value(double x, double z, uint seed, int scale)
    {
        double fx = x / scale, fz = z / scale;
        int x0 = (int)Math.Floor(fx), z0 = (int)Math.Floor(fz);
        double tx = fx - x0, tz = fz - z0;
        double sx = tx * tx * (3 - 2 * tx), sz = tz * tz * (3 - 2 * tz);
        double a = Lerp(Unit(x0, z0, seed), Unit(x0 + 1, z0, seed), sx);
        double b = Lerp(Unit(x0, z0 + 1, seed), Unit(x0 + 1, z0 + 1, seed), sx);
        return Lerp(a, b, sz);
    }

    public static double Field(double x, double z, uint seed, int scale, int octaves)
    {
        double sum = 0, amplitude = 1, norm = 0;
        var currentScale = Math.Max(1, scale);
        for (var octave = 0; octave < Math.Max(1, octaves); octave++)
        {
            sum += amplitude * Value(x, z, seed + (uint)octave * 7919u, currentScale);
            norm += amplitude; amplitude *= 0.5; currentScale = Math.Max(1, currentScale / 2);
        }
        return norm > 0 ? sum / norm : 0;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
