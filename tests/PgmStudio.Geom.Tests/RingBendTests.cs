using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// The two rules a bend is safe under: the ring's own vertices never move, and no point ever moves outward.
/// Both are asserted over a ring wound each way, because the inward side is decided by asking the polygon
/// rather than by reading a winding — which is exactly the reasoning a shoelace sign gets wrong.
/// </summary>
public sealed class RingBendTests
{
    /// <summary>A 100 × 100 square, counter-clockwise in the plan's own axes.</summary>
    private static double[][] Square() =>
        [[0, 0], [100, 0], [100, 100], [0, 100]];

    private static double[][] Reversed() => [.. Square().Reverse()];

    private static double Area(IReadOnlyList<double[]> ring)
    {
        double twice = 0;
        for (var i = 0; i < ring.Count; i++)
        {
            var next = ring[(i + 1) % ring.Count];
            twice += ring[i][0] * next[1] - next[0] * ring[i][1];
        }
        return Math.Abs(twice) / 2;
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task A_bend_only_ever_takes_land_away(bool reversed)
    {
        var ring = reversed ? Reversed() : Square();
        var coast = RingBend.Draw(ring, wander: 3, step: 10, seed: 5);

        await Assert.That(coast).IsNotNull();
        await Assert.That(coast!.Value.Ring.Count).IsGreaterThan(ring.Length);
        await Assert.That(Area(coast.Value.Ring)).IsLessThan(Area(ring));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Every_vertex_of_the_ring_is_still_where_it_was(bool reversed)
    {
        var ring = reversed ? Reversed() : Square();
        var coast = RingBend.Draw(ring, wander: 3, step: 10, seed: 5)!.Value;

        foreach (var vertex in ring)
            await Assert.That(coast.Ring.Any(drawn => drawn[0] == vertex[0] && drawn[1] == vertex[1]))
                .IsTrue().Because($"({vertex[0]}, {vertex[1]}) is the plan's own and may not move");
    }

    /// <summary>No drawn point lies outside the ring it was cut from — the invariant the area check only
    /// measures in aggregate. A point the wander left on its edge is on the boundary and not outside it,
    /// which is what the closed reading allows for.</summary>
    [Test]
    public async Task No_drawn_point_lies_outside_the_ring_it_was_cut_from()
    {
        var ring = Square();
        var coast = RingBend.Draw(ring, wander: 4, step: 8, seed: 11)!.Value;

        foreach (var drawn in coast.Ring)
            await Assert.That(Polygon.PointInRing(drawn[0], drawn[1], ring) || OnEdge(drawn, ring)).IsTrue()
                .Because($"({drawn[0]}, {drawn[1]}) is outside the ring it was cut from");
    }

    /// <summary>Whether a point lies on one of the ring's own edges, within a tenth of a block — the
    /// precision the drawn ring is rounded to.</summary>
    private static bool OnEdge(double[] point, IReadOnlyList<double[]> ring)
    {
        for (var i = 0; i < ring.Count; i++)
        {
            double[] a = ring[i], b = ring[(i + 1) % ring.Count];
            double dx = b[0] - a[0], dz = b[1] - a[1];
            var length = Math.Sqrt(dx * dx + dz * dz);
            if (length < 1e-9) continue;
            var across = Math.Abs((point[0] - a[0]) * dz - (point[1] - a[1]) * dx) / length;
            var along = ((point[0] - a[0]) * dx + (point[1] - a[1]) * dz) / (length * length);
            if (across <= 0.1 && along >= -0.001 && along <= 1.001) return true;
        }
        return false;
    }

    /// <summary>The same seed draws the same coast, which is what lets a spec be re-driven.</summary>
    [Test]
    public async Task The_same_seed_draws_the_same_coast()
    {
        var once = RingBend.Draw(Square(), 3, 10, 5)!.Value.Ring;
        var twice = RingBend.Draw(Square(), 3, 10, 5)!.Value.Ring;
        var other = RingBend.Draw(Square(), 3, 10, 6)!.Value.Ring;

        await Assert.That(once.Select(p => (p[0], p[1]))).IsEquivalentTo(twice.Select(p => (p[0], p[1])));
        await Assert.That(once.Select(p => (p[0], p[1])).SequenceEqual(other.Select(p => (p[0], p[1]))))
            .IsFalse();
    }

    /// <summary>An edge with room for fewer than two cuts keeps its shape, so a neck and a short face come
    /// out of a bend exactly as the plan drew them.</summary>
    [Test]
    public async Task A_short_edge_is_left_straight()
    {
        double[][] neck = [[0, 0], [100, 0], [100, 6], [0, 6]];
        var coast = RingBend.Draw(neck, wander: 2, step: 10, seed: 3)!.Value;

        await Assert.That(coast.Ring.Any(point => point[0] == 100 && point[1] > 0 && point[1] < 6)).IsFalse();
    }

    /// <summary>A wander wide enough to fold the outline over its far side is refused rather than clamped.
    /// It takes a <b>peninsula</b> to do it: a notch's two walls move apart and stay simple, and only an arm
    /// of land narrower than twice the wander has two walls moving toward each other, each into ground that
    /// really is inside the ring.</summary>
    [Test]
    public async Task A_wander_that_folds_a_peninsula_over_itself_draws_nothing()
    {
        double[][] tower = [[0, 0], [200, 0], [200, 60], [110, 60], [110, 200], [90, 200], [90, 60], [0, 60]];
        await Assert.That(RingBend.Draw(tower, wander: 12, step: 10, seed: 4)).IsNull();
    }

    /// <summary>The same arm at a wander it fits inside is drawn, so the refusal above is the fold and not
    /// the shape.</summary>
    [Test]
    public async Task The_same_peninsula_at_a_wander_it_fits_is_drawn()
    {
        double[][] tower = [[0, 0], [200, 0], [200, 60], [110, 60], [110, 200], [90, 200], [90, 60], [0, 60]];
        await Assert.That(RingBend.Draw(tower, wander: 3, step: 10, seed: 4)).IsNotNull();
    }

    /// <summary>Where a point has land on neither side it stays on its edge and is counted. A wander far
    /// past the ground's own width is the case: nothing can move, so nothing folds either, and the count is
    /// the whole of what says the coast is not the one that was asked for.</summary>
    [Test]
    public async Task A_point_with_no_room_is_held_and_counted()
    {
        double[][] thin = [[0, 0], [200, 0], [200, 3], [0, 3]];
        var coast = RingBend.Draw(thin, wander: 12, step: 10, seed: 4)!.Value;

        await Assert.That(coast.Held).IsGreaterThan(0);
        await Assert.That(coast.Held).IsLessThan(coast.Inserted);
        await Assert.That(Area(coast.Ring)).IsLessThanOrEqualTo(Area(thin));
    }
}
