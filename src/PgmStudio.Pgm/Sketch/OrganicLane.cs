using PgmStudio.Geom;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// Organic lane growth — the engine behind the <see cref="LaneArchetype.Organic"/> board. A team's island is
/// grown outward from a spawn <b>hub</b> into a few <b>wool lanes</b>: a noise grid spreads the wool tips
/// across the far edge (farthest-point sampling), each lane is bent by noise-offset waypoints, and the lane
/// body is a variable-width jittered polygon <b>ribbon</b> (an interesting hull, not a clean rectangle) that
/// may carry a <b>diamond hole</b>. A lane is one hub→tip branch ending in a dead-end wool tip; the spawn
/// sits at the hub where the lanes meet. Pure + seeded (same <see cref="LaneLayoutOptions.Seed"/> → same
/// island). Documented in docs/contracts/organic-lane-generation.md.
/// </summary>
public static class OrganicLane
{
    public sealed record Unit(List<SketchShape> Shapes, (double X, double Z) Hub, List<(double X, double Z)> Tips);

    public static Unit Grow(LaneLayoutOptions o)
    {
        var rng = new Rng(o.Seed);
        double W = o.Width, H = o.Height, m = o.Margin, lw = o.LaneWidth, midZ = H / 2;
        var noise = new NoiseField(o.Seed, Math.Max(6.0, lw));

        // spawn hub: near the mid line, facing the foe — lanes grow up/out to the far edge from here
        var hub = (W / 2 + rng.Range(-W * 0.06, W * 0.06), midZ - lw * 2.0);
        var tips = FarthestTips(o, rng, noise, hub, Math.Max(1, o.Wools));

        var shapes = new List<SketchShape>();
        var id = 0;

        // a short trunk from the hub toward the mid line so the island reaches the mid (short bridges)
        var trunkTip = (hub.Item1 + rng.Range(-lw * 0.5, lw * 0.5), midZ - lw * 0.6);
        shapes.Add(Poly($"trunk{id++}", GrowLane(rng, noise, hub, trunkTip, lw, o, allowHole: false).Ribbon, "add"));

        // one lane per wool tip: hub → tip, bent + varied, optional diamond hole inside
        foreach (var tip in tips)
        {
            var lane = GrowLane(rng, noise, hub, tip, lw, o, allowHole: true);
            shapes.Add(Poly($"lane{id}", lane.Ribbon, "add"));
            if (lane.Hole is { } hole) shapes.Add(Poly($"hole{id}", hole, "subtract"));
            id++;
        }
        return new Unit(shapes, hub, tips);
    }

    // ── far-spread wool tips from the noise grid (toward the far edge) ────────────────────────────
    private static List<(double X, double Z)> FarthestTips(LaneLayoutOptions o, Rng rng, NoiseField noise, (double X, double Z) hub, int count)
    {
        double W = o.Width, H = o.Height, m = o.Margin, lw = o.LaneWidth;
        var step = Math.Max(4.0, lw * 0.5);
        var cands = new List<(double X, double Z, double N)>();
        var threshold = 0.42;
        while (cands.Count < count + 2 && threshold > -0.01)
        {
            cands.Clear();
            for (var x = m + lw; x <= W - m - lw; x += step)
                for (var z = m; z <= H * 0.24; z += step)
                {
                    var nz = noise.At(x, z);
                    if (nz > threshold) cands.Add((x + rng.Range(-step * 0.3, step * 0.3), z + rng.Range(-step * 0.3, step * 0.3), nz));
                }
            threshold -= 0.1;
        }
        if (cands.Count == 0)
            for (var i = 0; i < count; i++) cands.Add((m + lw + (W - 2 * (m + lw)) * (i + 0.5) / count, m + lw, 1));

        var first = cands.OrderByDescending(c => c.N).First();
        var chosen = new List<(double X, double Z)> { (first.X, first.Z) };
        while (chosen.Count < count)
        {
            (double X, double Z) best = default;
            var bestScore = -1.0;
            foreach (var c in cands)
            {
                var d = Math.Min(Dist((c.X, c.Z), hub), chosen.Min(ch => Dist((c.X, c.Z), ch)));
                var score = d * (0.5 + c.N);                 // far-spread, weighted by the noise grid
                if (score > bestScore) { bestScore = score; best = (c.X, c.Z); }
            }
            chosen.Add(best);
        }
        return chosen;
    }

    // ── one lane: bent centerline → variable-width ribbon (+ optional diamond hole) ───────────────
    private static (List<double[]> Ribbon, List<double[]>? Hole) GrowLane(
        Rng rng, NoiseField noise, (double X, double Z) hub, (double X, double Z) tip, double lw, LaneLayoutOptions o, bool allowHole)
    {
        double dx = tip.X - hub.X, dz = tip.Z - hub.Z;
        var len = Math.Sqrt(dx * dx + dz * dz);
        double px = -dz / Math.Max(1e-6, len), pz = dx / Math.Max(1e-6, len);   // perpendicular

        // centerline control points: hub, 1–2 noise-offset waypoints (the bends), tip
        var ctrl = new List<double[]> { new[] { hub.X, hub.Z } };
        var waypoints = allowHole ? rng.Int(1, 3) : 1;
        for (var k = 1; k <= waypoints; k++)
        {
            var t = k / (double)(waypoints + 1);
            double bx = hub.X + dx * t, bz = hub.Z + dz * t;
            var off = (noise.At(bx, bz) - 0.5) * 2 * lw * 0.8;                   // ± ~0.8 lane widths
            ctrl.Add([bx + px * off, bz + pz * off]);
        }
        ctrl.Add([tip.X, tip.Z]);
        var center = Lane.Smooth(ctrl, 10);
        var n = center.Count;

        // base half-width with a slight taper toward the dead-end tip
        var half = new double[n];
        for (var i = 0; i < n; i++) half[i] = lw * 0.5 * (1 - 0.25 * (i / (double)(n - 1)));

        // optional diamond hole: widen the ribbon there and cut a rotated square inside it
        List<double[]>? hole = null;
        if (allowHole && rng.Chance(o.HoleChance) && len > lw * 3)
        {
            var hi = Math.Clamp(n / 2 + rng.Int(-n / 6, n / 6 + 1), 2, n - 3);
            var window = Math.Max(2, n / 6);
            for (var i = Math.Max(0, hi - window); i <= Math.Min(n - 1, hi + window); i++)
                half[i] += lw * 0.5 * (1 - Math.Abs(i - hi) / (double)window);   // bulge around the hole
            var hr = half[hi] * 0.55;
            if (hr >= 2.5) hole = Diamond(center[hi][0], center[hi][1], hr, rng.Range(0, Math.PI / 2));
        }

        // jitter the two sides independently for an organic outline
        var left = new List<double>(n);
        var right = new List<double>(n);
        for (var i = 0; i < n; i++)
        {
            var p = center[i];
            var jl = (noise.At(p[0] + 13.7, p[1] + 5.1) - 0.5) * lw * 0.3;
            var jr = (noise.At(p[0] - 7.3, p[1] + 19.4) - 0.5) * lw * 0.3;
            left.Add(Math.Max(2, half[i] + jl));
            right.Add(Math.Max(2, half[i] + jr));
        }
        return (Lane.Ribbon(center, left, right), hole);
    }

    private static List<double[]> Diamond(double cx, double cz, double r, double rot)
    {
        var ring = new List<double[]>(4);
        for (var i = 0; i < 4; i++) { var a = rot + i * Math.PI / 2; ring.Add([cx + r * Math.Cos(a), cz + r * Math.Sin(a)]); }
        return ring;
    }

    private static double Dist((double X, double Z) a, (double X, double Z) b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    private static SketchShape Poly(string id, List<double[]> ring, string op) =>
        new() { Id = id, Type = "polygon", Operation = op, Vertices = [.. ring] };

    /// <summary>Smooth 2-D value noise from a seed — a coherent field in [0,1] sampled on a grid of cell
    /// size <c>cell</c>, used to spread the wool tips and bend/jitter the lanes.</summary>
    private sealed class NoiseField(long seed, double cell)
    {
        private double Hash(int gx, int gz)
        {
            var h = (ulong)(gx * 374761393L + gz * 668265263L) ^ ((ulong)seed * 0x9E3779B97F4A7C15UL);
            h = (h ^ (h >> 13)) * 1274126177UL;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (double)0xFFFFFF;
        }

        public double At(double x, double z)
        {
            double fx = x / cell, fz = z / cell;
            int gx = (int)Math.Floor(fx), gz = (int)Math.Floor(fz);
            double tx = Smooth(fx - gx), tz = Smooth(fz - gz);
            double a = Hash(gx, gz), b = Hash(gx + 1, gz), c = Hash(gx, gz + 1), d = Hash(gx + 1, gz + 1);
            return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), tz);
        }

        private static double Smooth(double t) => t * t * (3 - 2 * t);
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
