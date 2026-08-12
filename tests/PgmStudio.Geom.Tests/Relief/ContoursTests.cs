using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// Tracing a solved surface into lines of constant height. What the tests hold is what makes the lines
/// readable as topography: a contour separates the ground above it from the ground below it, a run arrives in
/// walking order rather than as loose pieces, and a level that closes on itself says so.
/// </summary>
public sealed class ContoursTests
{
    // A field built by hand, so what the contour should do is known independently of the solver.
    private static HeightField Field(int width, int depth, Func<int, int, double> height)
    {
        var footprint = new Footprint(0, 0, width, depth);
        footprint.Add(Enumerable.Range(0, width).SelectMany(x => Enumerable.Range(0, depth).Select(z => (x, z))));
        var continuous = new double[footprint.Cells];
        var blocks = new int[footprint.Cells];
        foreach (var (x, z) in footprint.Land())
        {
            var index = footprint.Index(x, z);
            continuous[index] = height(x, z);
            blocks[index] = (int)Math.Round(continuous[index]);
        }
        return new HeightField(footprint, continuous, blocks);
    }

    [Test]
    public async Task A_flat_field_has_no_contours()
    {
        // Nothing to say about a plain, and one arbitrary line across it would say there was a slope.
        await Assert.That(Contours.Of(Field(20, 20, (_, _) => 7)).Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_ramp_gives_one_straight_line_per_level()
    {
        // Height rises with x alone, so every contour is a line of constant x, and there is one for each
        // whole level the ramp passes through.
        var lines = Contours.Of(Field(21, 8, (x, _) => x * 0.5));

        await Assert.That(lines.Count).IsEqualTo(10);           // levels 1..10 over a 0..10 ramp
        foreach (var line in lines)
        {
            var spread = line.Points.Max(point => point.X) - line.Points.Min(point => point.X);
            await Assert.That(spread).IsLessThan(0.01);
            await Assert.That(line.Closed).IsFalse();           // it runs off both edges of the land
        }
    }

    [Test]
    public async Task A_contour_sits_where_the_field_crosses_its_level()
    {
        // Cell 10 stands at 5, and a cell's height sits at its centre, so the 5 line runs down x = 10.5. The
        // position is interpolated between samples rather than snapped to the nearer of them.
        var line = Contours.Of(Field(21, 8, (x, _) => x * 0.5)).Single(candidate => Math.Abs(candidate.Level - 5) < 0.5);
        await Assert.That(line.Points[0].X).IsBetween(10.4, 10.6);
    }

    [Test]
    public async Task A_cone_closes_its_rings()
    {
        // A hill's contours are loops, and a loop that reports itself as open is a line an editor would let
        // an author drag apart.
        var lines = Contours.Of(Field(41, 41, (x, z) =>
            Math.Max(0, 12 - Math.Sqrt((x - 20) * (x - 20) + (z - 20) * (z - 20)))));

        var rings = lines.Where(line => line.Level >= 1).ToList();
        await Assert.That(rings.Count).IsGreaterThan(5);
        await Assert.That(rings.All(ring => ring.Closed)).IsTrue();

        // Every ring encircles the summit, and a higher one is tighter — which is the whole reading.
        double Radius(ContourLine ring) => ring.Points.Average(point =>
            Math.Sqrt((point.X - 20.5) * (point.X - 20.5) + (point.Z - 20.5) * (point.Z - 20.5)));
        var byLevel = rings.OrderBy(ring => ring.Level).ToList();
        await Assert.That(Radius(byLevel[0])).IsGreaterThan(Radius(byLevel[^1]));
    }

    [Test]
    public async Task A_run_arrives_in_walking_order()
    {
        // Marching squares emits a level as unordered pieces; a stroke needs consecutive points to be
        // neighbours, or the line drawn is a scribble across the map.
        var lines = Contours.Of(Field(41, 41, (x, z) =>
            Math.Max(0, 12 - Math.Sqrt((x - 20) * (x - 20) + (z - 20) * (z - 20)))));

        foreach (var line in lines)
            for (var i = 1; i < line.Points.Count; i++)
            {
                var step = Math.Sqrt(Math.Pow(line.Points[i].X - line.Points[i - 1].X, 2) +
                                     Math.Pow(line.Points[i].Z - line.Points[i - 1].Z, 2));
                await Assert.That(step).IsLessThan(2.0);
            }
    }

    [Test]
    public async Task A_contour_stops_at_the_edge_of_the_land()
    {
        // The right half is void. A contour crossing it would be a line over water.
        var footprint = new Footprint(0, 0, 40, 20);
        footprint.Add(Enumerable.Range(0, 20).SelectMany(x => Enumerable.Range(0, 20).Select(z => (x, z))));
        var continuous = new double[footprint.Cells];
        foreach (var (x, z) in footprint.Land()) continuous[footprint.Index(x, z)] = z * 0.5;
        var field = new HeightField(footprint, continuous, new int[footprint.Cells]);

        var lines = Contours.Of(field);
        await Assert.That(lines.Count).IsGreaterThan(0);
        await Assert.That(lines.SelectMany(line => line.Points).Max(point => point.X)).IsLessThan(20);
    }

    [Test]
    public async Task The_interval_decides_how_many_lines_there_are()
    {
        var field = Field(41, 8, (x, _) => x * 0.5);
        await Assert.That(Contours.Of(field, 1).Count).IsEqualTo(20);
        await Assert.That(Contours.Of(field, 4).Count).IsEqualTo(5);
    }

    [Test]
    public async Task A_saddle_between_two_summits_stays_connected()
    {
        // Two hills sharing a low pass. Read wrongly, the ambiguous square splits the pair into four lobes
        // and the pass reads as a gap the terrain does not have.
        var field = Field(61, 31, (x, z) =>
        {
            double Hill(int cx) => Math.Max(0, 10 - Math.Sqrt((x - cx) * (x - cx) + (z - 15) * (z - 15)) * 0.8);
            return Hill(20) + Hill(40);
        });

        // Low enough to run round both summits at once: one ring, not two.
        var low = Contours.Of(field, 1).Where(line => Math.Abs(line.Level - 2) < 0.5).ToList();
        await Assert.That(low.Count).IsEqualTo(1);
        await Assert.That(low[0].Closed).IsTrue();
    }

    [Test]
    public async Task A_solved_relief_contours_without_stray_pieces()
    {
        // The case the overlay actually draws: the solver's own output, grain and all. Every line has to be
        // long enough to be a line — a level fragmenting into two-point stubs is the failure that makes an
        // overlay unreadable.
        var footprint = Footprint.Of([([[0, 0], [80, 0], [80, 60], [0, 60]], false)]);
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 6,
            Grain = 1.2,
            Marks =
            [
                new RimMark(5),
                new PointMark(20, 20, 18, 4),
                new PointMark(62, 44, 14, 5),
            ],
        });

        var lines = Contours.Of(field);
        await Assert.That(lines.Count).IsGreaterThan(0);
        await Assert.That(lines.Average(line => line.Points.Count)).IsGreaterThan(8);
    }
}
