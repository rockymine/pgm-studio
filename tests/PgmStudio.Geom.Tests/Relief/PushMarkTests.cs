using PgmStudio.Geom;
using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// The sculpting half: a push keeps the plan of the ring that was drawn, and its top is a field rather than a
/// number. The properties here are the ones a radial falloff and a scalar amount cannot produce — which is
/// the whole reason the push exists beside the marks.
/// </summary>
public sealed class PushMarkTests
{
    private static Footprint Board(int width = 60, int depth = 60) =>
        Footprint.Of([(new[] { new[] { 0.0, 0.0 }, [width, 0], [width, depth], [0, depth] }, false)]);

    private static double[][] Ring(params (double X, double Z)[] points) =>
        points.Select(point => new[] { point.X, point.Z }).ToArray();

    // A long thin outline and a round one of comparable area — the pair that separates a shape-keeping
    // falloff from a radial one.
    private static double[][] Spur() => Ring((26, 8), (34, 8), (34, 52), (26, 52));

    private static double[][] Blob() => Ring((22, 22), (38, 22), (38, 38), (22, 38));

    [Test]
    public async Task A_long_push_stays_long_as_it_fades()
    {
        // A radial falloff rounds a thin shape off within a few blocks of its own outline. Measured on the
        // lifted footprint, the push must still be markedly longer than it is wide.
        var footprint = Board();
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 5,
            Marks = [new RimMark(5)],
            Pushes = [new PushMark(Spur(), Amount: 10, Falloff: 8)],
        });

        var lifted = footprint.Land().Where(cell => field.At(cell.X, cell.Z) > 6).ToList();
        var wide = lifted.Max(cell => cell.X) - lifted.Min(cell => cell.X) + 1;
        var deep = lifted.Max(cell => cell.Z) - lifted.Min(cell => cell.Z) + 1;

        await Assert.That(deep).IsGreaterThan(wide * 2);
    }

    [Test]
    public async Task A_push_lifts_a_plain_mark_and_steps_over_a_rigid_one()
    {
        // Which side of the vocabulary a push is on: it composes over ground, so an area mark stating a
        // height is sculpted along with everything around it. A rigid mark is not ground — it is a floor,
        // and a floor lifted on one side is a floor over a hole — so the lift steps over it, exactly as the
        // grain already does. The two runs differ only in the flag.
        var footprint = Board(40, 40);
        var pad = Ring((16, 16), (24, 16), (24, 24), (16, 24));
        // The push's ring stops halfway across the pad, so its skirt crosses the rest — the shape of the
        // hollowmarch case, where a ring ending short of a wool room reached it through the falloff.
        ReliefSpec Spec(bool rigid) => new()
        {
            Base = 5,
            Marks = [new RimMark(5), new AreaMark(pad, 9) { Rigid = rigid }],
            Pushes = [new PushMark(Ring((12, 4), (28, 4), (28, 20), (12, 20)), Amount: 6, Falloff: 6)],
        };

        var sculpted = ReliefSolver.Solve(footprint, Spec(rigid: false));
        var floor = ReliefSolver.Solve(footprint, Spec(rigid: true));
        var pinned = footprint.Land()
                              .Where(cell => Polygon.PointInRing(cell.X + 0.5, cell.Z + 0.5, pad)).ToList();

        await Assert.That(sculpted.At(20, 18)).IsGreaterThan(9);        // carried up by the push
        await Assert.That(pinned.Select(cell => sculpted.At(cell.X, cell.Z)).Distinct().Count())
            .IsGreaterThan(1);                                          // and tilted: the pad is not a floor
        // Rigid, the same push leaves it exactly as it was stated — one height over every cell of it.
        await Assert.That(pinned.All(cell => floor.At(cell.X, cell.Z) == 9)).IsTrue();
    }

    [Test]
    public async Task Pushes_over_the_same_ground_add()
    {
        // What separates a push from a constraint: two constraints over one cell argue, two pushes compose.
        var footprint = Board(40, 40);
        var ring = Ring((14, 14), (26, 14), (26, 26), (14, 26));
        ReliefSpec Spec(params PushMark[] pushes) => new()
        {
            Base = 5, Marks = [new RimMark(5)], Pushes = pushes,
        };

        var once = ReliefSolver.Solve(footprint, Spec(new PushMark(ring, 6, 6)));
        var twice = ReliefSolver.Solve(footprint, Spec(new PushMark(ring, 6, 6), new PushMark(ring, 6, 6)));

        await Assert.That(twice.At(20, 20) - 5).IsEqualTo((once.At(20, 20) - 5) * 2);
    }

    [Test]
    public async Task A_crown_domes_a_round_push_toward_a_point()
    {
        var footprint = Board();
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 5, Marks = [new RimMark(5)],
            Pushes = [new PushMark(Blob(), Amount: 8, Falloff: 8, Crown: 8)],
        });

        // The summit is a small patch near the middle rather than the whole interior.
        var summit = footprint.Land().Where(cell => field.At(cell.X, cell.Z) == field.Max).ToList();
        var interior = footprint.Land().Count(cell => Polygon.PointInRing(cell.X + 0.5, cell.Z + 0.5, Blob()));

        await Assert.That(summit.Count * 4).IsLessThan(interior);
        await Assert.That(field.At(30, 30)).IsGreaterThan(field.At(23, 23));
    }

    [Test]
    public async Task The_same_crown_ridges_a_long_push_along_a_line()
    {
        // Identical setting, different proportions: the medial axis of a long shape is a line, so the crest
        // runs its length instead of gathering at a point. Nothing about a centre is authored.
        var footprint = Board();
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 5, Marks = [new RimMark(5)],
            Pushes = [new PushMark(Spur(), Amount: 8, Falloff: 8, Crown: 8)],
        });

        var crest = footprint.Land().Where(cell => field.At(cell.X, cell.Z) >= field.Max - 1).ToList();
        var crestDeep = crest.Max(cell => cell.Z) - crest.Min(cell => cell.Z) + 1;
        var crestWide = crest.Max(cell => cell.X) - crest.Min(cell => cell.X) + 1;

        await Assert.That(crestDeep).IsGreaterThan(crestWide * 3);
    }

    [Test]
    public async Task A_negative_crown_dishes_the_interior_below_its_rim()
    {
        var footprint = Board();
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 5, Marks = [new RimMark(5)],
            Pushes = [new PushMark(Blob(), Amount: 8, Falloff: 8, Crown: -6)],
        });

        await Assert.That(field.At(30, 30)).IsLessThan(field.At(23, 30));
    }

    [Test]
    public async Task A_lift_per_vertex_makes_the_crest_fall_along_its_length()
    {
        var footprint = Board();
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 5, Marks = [new RimMark(5)],
            // The ring runs (26,8) (34,8) (34,52) (26,52): high at the near end, low at the far one.
            Pushes = [new PushMark(Spur(), Amount: 8, Falloff: 8, Amounts: [14, 14, 4, 4])],
        });

        await Assert.That(field.At(30, 12)).IsGreaterThan(field.At(30, 46) + 5);
    }

    [Test]
    public async Task A_ring_of_amounts_wraps_at_its_seam()
    {
        var push = new PushMark(Blob(), 0, Amounts: [10, 0, 0, 0]);
        // Just before the wrap the value is climbing back toward the first vertex's, not falling off a cliff.
        await Assert.That(push.AmountAt(0.99)).IsGreaterThan(push.AmountAt(0.80));
        await Assert.That(push.AmountAt(0)).IsEqualTo(10);
    }
}
