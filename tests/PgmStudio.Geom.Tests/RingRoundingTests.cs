using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// Bézier rounding of a simplified ring: gentle bends get Catmull–Rom tangent handles, sharp corners
/// (rectangles, tight regular polygons) stay hard, and the handles sit symmetrically about the vertex along
/// the neighbour chord.
/// </summary>
public sealed class RingRoundingTests
{
    private static List<double[]> Regular(double r, int n)
    {
        var ring = new List<double[]>(n);
        for (var i = 0; i < n; i++)
        {
            var a = 2 * Math.PI * i / n;
            ring.Add([r * Math.Cos(a), r * Math.Sin(a)]);
        }
        return ring;
    }

    [Test]
    public async Task A_rectangle_keeps_every_corner_sharp()
    {
        List<double[]> rect = [[0, 0], [10, 0], [10, 6], [0, 6]];
        await Assert.That(RingRounding.Tangents(rect).Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_regular_octagon_stays_sharp_below_its_turn_and_rounds_above()
    {
        var oct = Regular(20, 8);   // 45° turns
        await Assert.That(RingRounding.Tangents(oct, cornerAngleDeg: 40).Count).IsEqualTo(0);
        await Assert.That(RingRounding.Tangents(oct, cornerAngleDeg: 50).Count).IsEqualTo(8);
    }

    [Test]
    public async Task Gentle_bends_all_round()
    {
        var dodecagon = Regular(20, 12);   // 30° turns
        await Assert.That(RingRounding.Tangents(dodecagon, cornerAngleDeg: 40).Count).IsEqualTo(12);
    }

    [Test]
    public async Task Handles_follow_the_neighbour_chord_scaled_by_the_local_edge()
    {
        // a shallow bend at vertex 1 (≈11°), closed into a ring
        List<double[]> ring = [[0, 0], [10, 1], [20, 0], [10, -8]];
        var t = RingRounding.Tangents(ring, cornerAngleDeg: 80, tension: 1.0 / 3);

        await Assert.That(t.ContainsKey(1)).IsTrue();
        var h = t[1];
        double[] cur = ring[1], prev = ring[0], next = ring[2];
        double dx = next[0] - prev[0], dz = next[1] - prev[1];
        var dl = Math.Sqrt(dx * dx + dz * dz);
        double ux = dx / dl, uz = dz / dl;
        var outLen = Math.Sqrt(Math.Pow(next[0] - cur[0], 2) + Math.Pow(next[1] - cur[1], 2)) / 3;
        var inLen = Math.Sqrt(Math.Pow(cur[0] - prev[0], 2) + Math.Pow(cur[1] - prev[1], 2)) / 3;

        await Assert.That(Math.Abs(h.Out[0] - (cur[0] + ux * outLen))).IsLessThan(1e-9);
        await Assert.That(Math.Abs(h.Out[1] - (cur[1] + uz * outLen))).IsLessThan(1e-9);
        await Assert.That(Math.Abs(h.In[0] - (cur[0] - ux * inLen))).IsLessThan(1e-9);
        await Assert.That(Math.Abs(h.In[1] - (cur[1] - uz * inLen))).IsLessThan(1e-9);
    }

    [Test]
    public async Task Smooth_rounds_a_convex_curve_and_skips_a_rectangle()
    {
        await Assert.That(RingRounding.Smooth(Regular(20, 12)).Count).IsEqualTo(12);   // convex → simple
        await Assert.That(RingRounding.Smooth([[0, 0], [10, 0], [10, 6], [0, 6]]).Count).IsEqualTo(0);
    }

    [Test]
    public async Task TurnDeg_reads_straight_and_right_angles()
    {
        await Assert.That(RingRounding.TurnDeg([0, 0], [1, 0], [2, 0])).IsLessThan(1e-9);
        await Assert.That(Math.Abs(RingRounding.TurnDeg([0, 0], [1, 0], [1, 1]) - 90)).IsLessThan(1e-9);
    }
}
