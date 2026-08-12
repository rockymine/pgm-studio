using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// The relief solver's load-bearing properties. Each of these is a claim the design rests on rather than a
/// number a particular scene happened to produce, so they are asserted as properties: the surface's extremes
/// can only sit where a mark put one, the outline is a no-flow boundary, a band stops where its line stops,
/// and a mirrored map is mirrored exactly.
/// </summary>
public sealed class ReliefSolverTests
{
    private static double[][] Rect(double minX, double minZ, double maxX, double maxZ) =>
        [[minX, minZ], [maxX, minZ], [maxX, maxZ], [minX, maxZ]];

    private static Footprint Board(int width = 40, int depth = 40) =>
        Footprint.Of([(Rect(0, 0, width, depth), false)]);

    // Two arms joined across the top, separated by a slot: the shape whose notch an interpolator either
    // respects or reaches through.
    private static Footprint Horseshoe() => Footprint.Of(
    [
        (new[] { new[] { 0.0, 0.0 }, [40, 0], [40, 34], [24, 34], [24, 10], [16, 10], [16, 34], [0, 34] }, false),
    ]);

    [Test]
    public async Task One_mark_and_no_rim_decides_the_whole_surface()
    {
        // Without a rim, marks alone decide everything — so a shape carrying a single high mark rises to that
        // height everywhere and simply runs off its own edge.
        var footprint = Board();
        var field = ReliefSolver.Solve(footprint, new ReliefSpec { Base = 4, Marks = [new PointMark(20, 20, 11, 3)] });

        await Assert.That(field.Min).IsEqualTo(11);
        await Assert.That(field.Max).IsEqualTo(11);
    }

    [Test]
    public async Task The_surface_never_leaves_the_range_its_marks_stated()
    {
        // The property the relaxation is chosen for: no bump appears that nobody asked for, and no ridge sags
        // between two marks that were both high.
        var footprint = Board(60, 60);
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 5,
            Marks = [new RimMark(5), new PointMark(15, 15, 14, 4), new PointMark(45, 45, 16, 4)],
        });

        await Assert.That(field.Min).IsEqualTo(5);
        await Assert.That(field.Max).IsEqualTo(16);
    }

    [Test]
    public async Task The_outline_is_a_no_flow_boundary_so_a_notch_is_not_reached_through()
    {
        // One arm's tip high, the other's low. The two banks are eight blocks apart in a straight line and
        // roughly fifty apart along the land; a fill that respects the land keeps the full statement between
        // them, and one that does not averages it away.
        var footprint = Horseshoe();
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 4,
            Marks = [new PointMark(8, 30, 14, 3), new PointMark(32, 30, 4, 3)],
        });

        var acrossTheSlot = Math.Abs(field.At(15, 30) - field.At(25, 30));
        await Assert.That(acrossTheSlot).IsGreaterThanOrEqualTo(9);
    }

    [Test]
    public async Task A_mark_placed_past_the_edge_contributes_only_its_overlap()
    {
        // How a hill at the corner of a map is authored: the ground rises into the corner and stops, with no
        // back slope behind it.
        var footprint = Board(30, 30);
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 4,
            Marks = [new RimMark(4), new PointMark(-6, -6, 18, 12)],
        });

        await Assert.That(field.At(1, 1)).IsGreaterThan(field.At(28, 28));
        await Assert.That(field.Max).IsLessThanOrEqualTo(18);
    }

    [Test]
    public async Task A_mark_entirely_off_the_footprint_pins_nothing()
    {
        var footprint = Board(20, 20);
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 6,
            Marks = [new PointMark(200, 200, 30, 4)],
        });

        await Assert.That(field.Min).IsEqualTo(6);
        await Assert.That(field.Max).IsEqualTo(6);
    }

    [Test]
    public async Task The_last_mark_wins_a_contested_cell()
    {
        // What lets a bench drawn over a slope flatten it.
        var footprint = Board(30, 30);
        var bench = Rect(10, 10, 20, 20);
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 4,
            Marks = [new AreaMark(bench, 12), new AreaMark(bench, 7)],
        });

        await Assert.That(field.At(15, 15)).IsEqualTo(7);
    }

    [Test]
    public async Task A_scarp_band_stops_where_its_line_stops()
    {
        // Perpendicular distance alone wraps a half-disc around each end, which closes the gap beside the
        // face — the gap being the entire reason the line was drawn to end there.
        var footprint = Board(40, 60);
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 6,
            Marks = [new RimMark(6), new ScarpMark([[20, 4], [20, 26]], High: 18, Low: 6, FaceWidth: 2, BandWidth: 6)],
        });

        // Beside the face, the two sides differ by most of the drop.
        await Assert.That(field.At(16, 15) - field.At(24, 15)).IsGreaterThanOrEqualTo(8);
        // Well past its end, they do not.
        await Assert.That(Math.Abs(field.At(16, 50) - field.At(24, 50))).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task A_scarps_grade_is_the_drop_over_the_face()
    {
        var scarp = new ScarpMark([[0, 0], [0, 10]], High: 16, Low: 6, FaceWidth: 5);
        await Assert.That(scarp.Grade).IsEqualTo(2.0);
    }

    [Test]
    public async Task A_mirrored_map_is_mirrored_exactly()
    {
        // Mirroring the marks is not enough on its own: the relaxation agrees with its own image only to
        // within its tolerance, and a tolerance is what rounding turns into a whole block.
        const double CentreX = 30, CentreZ = 24;
        var footprint = Board(60, 48);
        List<Mark> marks =
        [
            new RimMark(5),
            new PointMark(12, 10, 15, 4), new PointMark(2 * CentreX - 12, 2 * CentreZ - 10, 15, 4),
        ];
        var spec = new ReliefSpec
        {
            Base = 5, Marks = marks, Grain = 1.2, GrainScale = 9, Seed = 3,
            FoldMode = "rot_180", FoldCentreX = CentreX, FoldCentreZ = CentreZ,
        };

        var folded = ReliefSolver.Solve(footprint, spec);
        var loose = ReliefSolver.Solve(footprint, spec with { FoldMode = null });

        await Assert.That(WorstMirroredDifference(folded, CentreX, CentreZ)).IsEqualTo(0);
        await Assert.That(WorstMirroredDifference(loose, CentreX, CentreZ)).IsGreaterThan(0);
    }

    [Test]
    public async Task The_cascade_agrees_with_a_single_grid()
    {
        var footprint = Board(70, 70);
        var spec = new ReliefSpec
        {
            Base = 5,
            Marks = [new RimMark(5), new PointMark(20, 20, 15, 5), new PointMark(50, 50, 18, 5)],
        };

        var cascaded = ReliefSolver.Solve(footprint, spec);
        var oneGrid = ReliefSolver.Solve(footprint, spec, cascade: false);

        var worst = footprint.Land().Max(cell => Math.Abs(cascaded.At(cell.X, cell.Z) - oneGrid.At(cell.X, cell.Z)));
        await Assert.That(worst).IsLessThanOrEqualTo(1);
    }

    [Test]
    public async Task A_warm_start_lands_on_the_settled_answer()
    {
        // What makes a drag affordable: moving one mark perturbs the field locally, so the relaxation has
        // only that perturbation left to carry.
        var footprint = Board(50, 50);
        var before = new ReliefSpec
        {
            Base = 5, Marks = [new RimMark(5), new PointMark(18, 18, 14, 4)],
        };
        var after = before with { Marks = [new RimMark(5), new PointMark(22, 21, 14, 4)] };

        var previous = ReliefSolver.Solve(footprint, before).Continuous;
        var resumed = ReliefSolver.Solve(footprint, after, previous, sweeps: 40);
        var settled = ReliefSolver.Solve(footprint, after);

        var worst = footprint.Land().Max(cell => Math.Abs(resumed.At(cell.X, cell.Z) - settled.At(cell.X, cell.Z)));
        await Assert.That(worst).IsLessThanOrEqualTo(1);
    }

    private static int WorstMirroredDifference(HeightField field, double centreX, double centreZ)
    {
        var worst = 0;
        foreach (var (x, z) in field.Footprint.Land())
        {
            int mx = (int)Math.Floor(2 * centreX - (x + 0.5)), mz = (int)Math.Floor(2 * centreZ - (z + 0.5));
            if (!field.Has(mx, mz)) continue;
            worst = Math.Max(worst, Math.Abs(field.At(x, z) - field.At(mx, mz)));
        }
        return worst;
    }
}
