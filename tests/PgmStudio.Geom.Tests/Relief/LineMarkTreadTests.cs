using PgmStudio.Geom;
using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// A line that passes close to itself, and what its tread decides. The claim: pinning the whole band flat
/// makes the ground between two passes a wall whatever the drawing says, because every cell snaps to whichever
/// pass is nearer and two cells either side of the midline take heights a whole winding apart. Stating a tread
/// narrower than the reach lofts the rest — a straight ramp between the two treads' edges — so the same
/// drawing comes out as flat road and graded batter.
/// </summary>
public sealed class LineMarkTreadTests
{
    private static Footprint Board(int span = 60) =>
        Footprint.Of([([[0, 0], [span, 0], [span, span], [0, span]], false)]);

    /// <summary>Two straight passes of one line running down the board, <paramref name="pitch"/> apart in x
    /// and <paramref name="drop"/> blocks apart in height, joined round the far end so they are one polyline.
    /// A switchback, reduced to the two things about it that matter.</summary>
    private static LineMark Switchback(double pitch, double drop, double radius, double tread = double.NaN,
                                       double batter = 0)
    {
        var left = 30 - pitch / 2;
        var right = 30 + pitch / 2;
        double[][] points = [[left, 6], [left, 50], [right, 54], [right, 6]];
        return new LineMark(points, [20, 20, 20 - drop, 20 - drop], radius, tread, batter);
    }

    private static Dictionary<(int X, int Z), double> Pinned(Footprint footprint, Mark mark)
    {
        var pins = new Dictionary<(int X, int Z), double>();
        foreach (var (cell, height, _) in mark.Pins(footprint)) pins[cell] = height;
        return pins;
    }

    /// <summary>The worst height difference between two neighbouring pinned cells along the row that crosses
    /// both passes — the wall, measured.</summary>
    private static double WorstStep(Dictionary<(int X, int Z), double> pins, int z, int fromX, int toX)
    {
        double worst = 0;
        for (var x = fromX; x < toX; x++)
            if (pins.TryGetValue((x, z), out var here) && pins.TryGetValue((x + 1, z), out var next))
                worst = Math.Max(worst, Math.Abs(next - here));
        return worst;
    }

    [Test]
    public async Task A_line_that_laps_itself_pins_a_wall_between_its_passes()
    {
        // Bands 5 either side, passes 8 apart: they overlap, so no cell between them is left free and the
        // relaxation has nothing to grade. Every cell takes its nearer pass, and the midline is the whole drop.
        var footprint = Board();
        var pins = Pinned(footprint, Switchback(pitch: 8, drop: 6, radius: 5));

        await Assert.That(WorstStep(pins, 28, 20, 40)).IsEqualTo(6).Within(0.01);
    }

    [Test]
    public async Task A_tread_narrower_than_the_reach_lofts_the_ground_between_the_passes()
    {
        // The same drawing with two of the five cells flat: the drop now runs over the four cells between the
        // two treads, so no single step is more than a block and a half.
        var footprint = Board();
        var pins = Pinned(footprint, Switchback(pitch: 8, drop: 6, radius: 5, tread: 2));

        await Assert.That(WorstStep(pins, 28, 20, 40)).IsLessThanOrEqualTo(1.6);
    }

    [Test]
    public async Task The_tread_itself_stays_flat()
    {
        // The point of stating a tread rather than just narrowing the radius: what it keeps is a road. Every
        // cell within two of a pass sits at that pass's own height, to the block.
        var footprint = Board();
        var mark = Switchback(pitch: 8, drop: 6, radius: 5, tread: 2);
        var pins = Pinned(footprint, mark);

        // The left pass runs at x = 26 and is stated at 20 for its whole length.
        foreach (var x in new[] { 25, 26, 27 })
        {
            await Assert.That(pins.TryGetValue((x, 28), out var height)).IsTrue();
            await Assert.That((x, height)).IsEqualTo((x, 20d));
        }
    }

    /// <summary><b>A tread pins its flat outright and states its shoulder softly.</b> The weight is what the
    /// solver reads: full at the tread's edge so the road is the road, falling to nothing at the reach's so
    /// the rim is whatever the ground beside it already was. Without a tread every cell is a statement, which
    /// is what a ridgeline wants and what puts a wall round a road.</summary>
    [Test]
    public async Task A_tread_states_its_flat_outright_and_its_shoulder_softly()
    {
        var footprint = Board();
        double[][] straight = [[30, 6], [30, 54]];

        var whole = new LineMark(straight, [20], 5).Pins(footprint).ToDictionary(pin => pin.Cell);
        var trod = new LineMark(straight, [20], 5, 2).Pins(footprint).ToDictionary(pin => pin.Cell);

        // The same cells are spoken for either way — the tread changes how firmly, not how far.
        await Assert.That(trod.Keys.Order()).IsEquivalentTo(whole.Keys.Order());
        await Assert.That(whole.Values.All(pin => pin.Weight >= 1)).IsTrue();

        await Assert.That(trod[(31, 30)].Weight).IsEqualTo(1);              // inside the tread
        await Assert.That(trod[(33, 30)].Weight).IsBetween(0.01, 0.99);     // in the shoulder
        await Assert.That(trod[(34, 30)].Weight).IsLessThan(trod[(33, 30)].Weight);
    }

    /// <summary>The regression this cost a build to find, stated as what must not happen: an earlier mark's
    /// height standing unblended beside a later mark's ground. A high line drawn first, a low one drawn over
    /// it with a tread — the shoulder must arrive at the high line's height gradually across its own width
    /// rather than in one cell.</summary>
    [Test]
    public async Task A_lines_shoulder_grades_into_what_an_earlier_mark_pinned()
    {
        var footprint = Board();
        int Worst(double tread)
        {
            var field = ReliefSolver.Solve(footprint, new ReliefSpec
            {
                Base = 12, Reach = 0, Step = 1,
                Marks = [new LineMark([[30, 0], [30, 60]], [34], 18),
                         new LineMark([[30, 20], [30, 40]], [12], 6, tread)],
            });
            // Straight out from the low line, across its shoulder and on into the high line's ground.
            var worst = 0;
            for (var x = 30; x < 44; x++) worst = Math.Max(worst, Math.Abs(field.At(x + 1, 30) - field.At(x, 30)));
            return worst;
        }

        // Twenty-two blocks of difference. Stated flat to the band's edge it lands in one cell; graded over a
        // four-cell shoulder it is a quarter of that a cell — the shoulder's width is what sets the grade,
        // which is why a mark that needs a gentler one states a narrower tread or a longer reach.
        await Assert.That(Worst(double.NaN)).IsEqualTo(22);
        await Assert.That(Worst(2)).IsLessThanOrEqualTo(6);
    }

    /// <summary><b>Two marks whose bands touch meet on a ramp rather than on a step, and neither knows the
    /// other exists.</b> A high line and a low one whose bands overlap: with no tread the seam is one cell
    /// carrying the whole difference, and a tread on the later one grades it out across the shoulder's own
    /// width. The narrower the tread, the wider the grade.</summary>
    [Test]
    public async Task A_seam_between_two_marks_grades_across_the_later_ones_shoulder()
    {
        var footprint = Board();
        int Seam(double tread)
        {
            var field = ReliefSolver.Solve(footprint, new ReliefSpec
            {
                Base = 24, Reach = 0, Step = 1,
                Marks = [new LineMark([[0, 12], [60, 12]], [33], 10),
                         new LineMark([[0, 28], [60, 28]], [24], 10, tread)],
            });
            var worst = 0;
            for (var z = 14; z < 34; z++) worst = Math.Max(worst, Math.Abs(field.At(30, z + 1) - field.At(30, z)));
            return worst;
        }

        await Assert.That(Seam(double.NaN)).IsEqualTo(9);   // the whole difference, in one cell
        await Assert.That(Seam(4)).IsLessThanOrEqualTo(2);
        await Assert.That(Seam(2)).IsLessThanOrEqualTo(1);
    }

    /// <summary>A stated batter is a bench under a bank: the fall runs at the angle from the upper tread's
    /// edge and then holds at the lower pass's height, rather than spreading over the whole run.</summary>
    [Test]
    public async Task A_stated_batter_falls_at_its_angle_and_then_holds()
    {
        // Passes 14 apart with treads of 2 leave 10 blocks of run for a 6-block fall — 31 degrees left to
        // itself. Asked for 60, the fall takes about 3.5 blocks and the rest is flat at the lower level.
        var footprint = Board();
        var pins = Pinned(footprint, Switchback(pitch: 14, drop: 6, radius: 7, tread: 2, batter: 60));

        // The high pass runs at x = 23 at height 20; the low at x = 37 at height 14.
        await Assert.That(pins[(24, 28)]).IsEqualTo(20).Within(0.01);        // on the tread
        await Assert.That(pins[(30, 28)]).IsEqualTo(14).Within(0.01);        // past the toe, flat and low
        // The batter is doing the work rather than a gentle ramp: three and a half blocks out from the upper
        // tread the ground has already fallen the whole six, where the free ramp would be a fifth of the way.
        await Assert.That(pins[(28, 28)]).IsEqualTo(14).Within(0.01);
        await Assert.That(pins[(27, 28)]).IsLessThan(16.5);
    }

    /// <summary>A batter gentler than the run requires is raised to what the run needs, because the ramp has
    /// to have arrived by the time it meets the next tread — anything left over is a step.</summary>
    [Test]
    public async Task A_batter_gentler_than_the_run_requires_is_raised_to_it()
    {
        var footprint = Board();
        var asked = Pinned(footprint, Switchback(pitch: 8, drop: 6, radius: 5, tread: 2, batter: 10));
        var free = Pinned(footprint, Switchback(pitch: 8, drop: 6, radius: 5, tread: 2));

        foreach (var cell in free.Keys)
            await Assert.That((cell, asked[cell])).IsEqualTo((cell, free[cell]));
    }

    // ── the seam reading (WE33) ─────────────────────────────────────────────────────────────────────────

    private static ReliefSpec Two(double tread = double.NaN) => new()
    {
        Base = 24, Reach = 0, Step = 1,
        Marks = [new LineMark([[0, 12], [60, 12]], [33], 10) { Id = "crest" },
                 new LineMark([[0, 28], [60, 28]], [24], 10, tread) { Id = "shelf" }],
    };

    /// <summary><b>A seam is a fact about the marks and is invisible in the surface.</b> Two bands that touch
    /// put the whole difference between them in one cell, and the solved field reports that as a step, a face
    /// and a barrier cell with no mark's name on any of it. The reading names the pair, the worst cell and how
    /// far the boundary runs.</summary>
    [Test]
    public async Task A_seam_names_the_two_marks_that_built_it()
    {
        var reading = ReliefSolver.ReadMarks(Board(), Two());

        var seam = reading.Seams.Single();
        await Assert.That((seam.A, seam.B)).IsEqualTo(("crest", "shelf"));
        await Assert.That(seam.Step).IsEqualTo(9);
        await Assert.That(seam.Cells).IsGreaterThan(50);        // it runs the width of the board
        await Assert.That(Board().Inside(seam.X, seam.Z)).IsTrue();
    }

    /// <summary>And it goes away when the ground arrives, which is what makes it a fault report rather than a
    /// description of the arrangement: the two marks still overlap exactly as much, and the boundary between
    /// their territories now falls inside the graded shoulder.</summary>
    [Test]
    public async Task A_seam_that_grades_is_not_reported()
    {
        var reading = ReliefSolver.ReadMarks(Board(), Two(tread: 2));
        await Assert.That(reading.Seams.Where(seam => seam.Step > 1)).IsEmpty();
    }

    /// <summary>A mark that landed nowhere leaves nothing behind to notice, so the reading is the only thing
    /// that can say it happened.</summary>
    [Test]
    public async Task A_mark_that_pins_nothing_is_named()
    {
        var reading = ReliefSolver.ReadMarks(Board(), new ReliefSpec
        {
            Base = 20, Marks = [new PointMark(30, 30, 26, 4) { Id = "knoll" },
                                new PointMark(400, 400, 26, 4) { Id = "off-the-board" }],
        });

        await Assert.That(reading.Silent).IsEquivalentTo(new[] { "off-the-board" });
    }

    [Test]
    public async Task The_far_side_of_a_bend_is_not_a_second_pass()
    {
        // What separates a winding from a corner: distance travelled along the line, not distance across it.
        // A hairpin's two limbs are one pass where they meet, so the cells at the turn stay flat rather than
        // ramping toward a height the line reaches two blocks away.
        var footprint = Board();
        double[][] hairpin = [[28, 10], [28, 30], [32, 30], [32, 10]];
        var pins = Pinned(footprint, new LineMark(hairpin, [20, 20, 14, 14], 5, 2));

        // At the turn itself both limbs are the same height, so nothing there can ramp.
        await Assert.That(pins.TryGetValue((30, 29), out var atTurn)).IsTrue();
        await Assert.That(atTurn).IsBetween(13.9, 20.1);
    }

    // ── an area that tilts, and an edge that grades (WE103) ─────────────────────────────────────────────

    private static double[][] Pad => [[20, 22], [40, 22], [40, 38], [20, 38]];

    /// <summary>A hillside falling 40 to 20 across the board — the ground a shelf has to be cut into.</summary>
    private static ReliefSpec Hillside(params Mark[] more) => new()
    {
        Base = 20, Reach = 0, Step = 1,
        Marks = [new LineMark([[4, 30], [56, 30]], [40, 20], 30) { Id = "hill" }, .. more],
    };

    private static int[] Across(ReliefSpec spec, int z = 30)
    {
        var field = ReliefSolver.Solve(Board(60), spec);
        return [.. Enumerable.Range(0, 61).Select(x => field.At(x, z))];
    }

    /// <summary><b>One height a level pad, one per vertex a tilted one.</b> Without this every bench is dead
    /// flat whatever it was drawn as, because the mark could carry only a number.</summary>
    [Test]
    public async Task An_area_with_a_height_per_vertex_tilts()
    {
        var level = Across(Hillside(new AreaMark(Pad, [28])));
        var tilted = Across(Hillside(new AreaMark(Pad, [32, 24, 24, 32])));

        // Across the pad the level one does not move and the tilted one falls the way it was drawn.
        await Assert.That(level[24]).IsEqualTo(level[36]);
        await Assert.That(tilted[24]).IsGreaterThan(tilted[36]);
        await Assert.That(tilted[24] - tilted[36]).IsGreaterThanOrEqualTo(4);
    }

    /// <summary>And it is a <em>surface</em> rather than a ramp in one axis: four corners that do not lie in
    /// one plane come out warped, read over the ring's own triangulation.</summary>
    [Test]
    public async Task A_tilted_area_states_a_surface_and_not_a_gradient()
    {
        var spec = Hillside(new AreaMark(Pad, [32, 24, 30, 38]));
        // The same x, two z rows: a shape stating one height per corner cannot answer the same at both.
        await Assert.That(Across(spec, 25)[30]).IsNotEqualTo(Across(spec, 35)[30]);
    }

    /// <summary><b>A bevel is what stops a pad ending on a wall.</b> The seam read is the measure: a pad
    /// stated to its own outline meets the hillside on a step, and the same pad graded over its edge meets it
    /// on a ramp — the marks unchanged and overlapping exactly as much.</summary>
    [Test]
    public async Task A_bevel_grades_a_pads_edge_into_the_ground_it_is_cut_into()
    {
        int Worst(double bevel)
        {
            var reading = ReliefSolver.ReadMarks(
                Board(60), Hillside(new AreaMark(Pad, [32, 24, 24, 32], bevel) { Id = "shelf" }));
            return reading.Seams.Where(seam => seam.A == "hill" || seam.B == "hill")
                          .Select(seam => seam.Step).DefaultIfEmpty(0).Max();
        }

        await Assert.That(Worst(0)).IsGreaterThan(1);
        await Assert.That(Worst(5)).IsEqualTo(0);
    }

    /// <summary>A pad with no bevel is still stated to its outline, which is what a floor wants — the edge is
    /// the statement, and a room that graded into the ground would be a room on a slope.</summary>
    [Test]
    public async Task An_area_with_no_bevel_states_every_cell_of_itself_outright()
    {
        var pins = new AreaMark(Pad, [28]).Pins(Board(60)).ToList();
        await Assert.That(pins.Count).IsGreaterThan(0);
        await Assert.That(pins.All(pin => pin.Weight >= 1)).IsTrue();

        var graded = new AreaMark(Pad, [28], 5).Pins(Board(60)).ToList();
        await Assert.That(graded.Any(pin => pin.Weight < 1)).IsTrue();
        await Assert.That(graded.Any(pin => pin.Weight >= 1)).IsTrue();     // the middle is still flat
    }
}
