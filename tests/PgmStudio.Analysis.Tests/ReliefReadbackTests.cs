using PgmStudio.Analysis.Playability;
using PgmStudio.Geom.Relief;

// PgmStudio.Analysis.Footprint is a NAMESPACE, and a member of the enclosing namespace binds ahead of any
// using directive — so a plain `Footprint` here is that namespace and an alias of the same name cannot win.
// The solver's type takes a distinct name instead. This is the shadowing the Geom leaf is called Geom rather
// than Geometry to avoid, arriving from the other side.
using ReliefGrid = PgmStudio.Geom.Relief.Footprint;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// What a surface charges. These hold the readings the design rests on rather than numbers a particular
/// scene produced: a flat board costs nothing at every tier, a scarp's grade decides its crossing, a drop is
/// free in the direction it falls, and a relief that adds pressure is not the same thing as one that broke
/// the map.
/// </summary>
public sealed class ReliefReadbackTests
{
    private static double[][] Rect(double minX, double minZ, double maxX, double maxZ) =>
        [[minX, minZ], [maxX, minZ], [maxX, maxZ], [minX, maxZ]];

    private static ReliefGrid Board(int width, int depth) => ReliefGrid.Of([(Rect(0, 0, width, depth), false)]);

    // A field built by hand, so what the readback should say is known independently of the solver.
    private static HeightField Field(int width, int depth, Func<int, int, int> height)
    {
        var footprint = new ReliefGrid(0, 0, width, depth);
        footprint.Add(Enumerable.Range(0, width).SelectMany(x => Enumerable.Range(0, depth).Select(z => (x, z))));
        var blocks = new int[footprint.Cells];
        var continuous = new double[footprint.Cells];
        foreach (var (x, z) in footprint.Land())
        {
            blocks[footprint.Index(x, z)] = height(x, z);
            continuous[footprint.Index(x, z)] = height(x, z);
        }
        return new HeightField(footprint, continuous, blocks);
    }

    private static ReliefReadback.Tier TierOf(ReliefReadback.Result result, string name)
        => result.Tiers.Single(tier => tier.Name == name);

    [Test]
    public async Task A_flat_board_charges_nothing_and_is_one_place()
    {
        var read = ReliefReadback.Read(Field(40, 40, (_, _) => 8));

        await Assert.That(read.Relief).IsEqualTo(0);
        await Assert.That(TierOf(read, "walk").Share).IsEqualTo(1);
        await Assert.That(TierOf(read, "walk").Places).IsEqualTo(1);
        await Assert.That(TierOf(read, "walk").Ledges).IsEqualTo(0);
        await Assert.That(read.Cliffs).IsEqualTo(0);
    }

    [Test]
    public async Task A_grade_decides_the_crossing_and_a_wall_is_not_the_only_barrier()
    {
        // The measurement the scarp exists for. Same ten-block drop over three face widths: a gentle face is
        // walked, a middling one is a SOFT WALL — refused on foot, crossed by anyone who spends a block —
        // and a sheer one is refused outright. The middle row is the most useful thing terrain can do and
        // the thing a vertical cliff cannot.
        static HeightField Ramp(int face) => Field(60, 30, (x, _) =>
            x < 20 ? 16 : x >= 20 + face ? 6 : 16 - (int)Math.Round(10.0 * (x - 20) / face));

        var gentle = ReliefReadback.Read(Ramp(10)).AcrossX;       // 1 per block
        var soft = ReliefReadback.Read(Ramp(5)).AcrossX;          // 2 per block
        var sheer = ReliefReadback.Read(Ramp(2)).AcrossX;         // 5 per block

        await Assert.That(gentle.OnFoot).IsEqualTo(gentle.Rows);
        await Assert.That(soft.OnFoot).IsEqualTo(0);
        await Assert.That(soft.WithBlock).IsEqualTo(soft.Rows);
        await Assert.That(sheer.OnFoot).IsEqualTo(0);
        await Assert.That(sheer.WithBlock).IsEqualTo(0);
    }

    [Test]
    public async Task A_drop_is_free_in_the_direction_it_falls()
    {
        // A player walks off a ledge and takes the fall, so a face that refuses a crossing one way lets it
        // through the other. Measuring one direction cannot tell a wall from a gradient of pressure.
        var read = ReliefReadback.Read(Field(60, 30, (x, _) => x < 30 ? 20 : 6));

        await Assert.That(read.AcrossX.WithBlock).IsEqualTo(0);          // never climbed
        await Assert.That(read.AcrossX.Descended).IsEqualTo(read.AcrossX.Rows);   // always fallen down
    }

    [Test]
    public async Task Pressure_is_not_a_broken_map_and_the_two_are_told_apart_by_places()
    {
        // A relief that leaves one connected place holding nearly all the ground has added pressure. One that
        // splits the ground into halves has done something else, and a walkability share alone cannot tell
        // them apart — both can read as the same percentage.
        var pressured = ReliefReadback.Read(Field(60, 40, (x, z) => 8 + ((x / 7 + z / 7) % 2 == 0 ? 2 : 0)));
        var split = ReliefReadback.Read(Field(60, 40, (x, _) => x < 30 ? 8 : 24));

        await Assert.That(TierOf(pressured, "scramble").Places).IsEqualTo(1);
        await Assert.That(TierOf(pressured, "scramble").LargestPlace).IsEqualTo(1);
        await Assert.That(TierOf(split, "scramble").Places).IsEqualTo(2);
        await Assert.That(TierOf(split, "scramble").LargestPlace).IsLessThan(0.6);
    }

    [Test]
    public async Task A_shelf_too_small_to_stand_on_is_a_ledge_and_not_a_place()
    {
        // Counting regions without this turns "one connected map with a few cliff-top shelves" into a
        // meaningless piece count.
        var read = ReliefReadback.Read(Field(60, 60, (x, z) => x >= 55 && z >= 55 ? 30 : 8));

        await Assert.That(TierOf(read, "walk").Places).IsEqualTo(1);
        await Assert.That(TierOf(read, "walk").Ledges).IsEqualTo(1);
        await Assert.That(TierOf(read, "walk").LargestPlace).IsGreaterThan(0.98);
    }

    [Test]
    public async Task A_stranded_shelf_is_named_with_the_coordinates_it_can_be_walked_to_at()
    {
        // The 5 x 5 shelf in the far corner is what a count alone reports as "ledges 1" and nothing else —
        // knowing something is stranded without knowing what it is is exactly the gap.
        var read = ReliefReadback.Read(Field(60, 60, (x, z) => x >= 55 && z >= 55 ? 30 : 8));
        var parts = TierOf(read, "walk").Parts;

        await Assert.That(parts.Count).IsEqualTo(2);
        await Assert.That(parts[0].Place).IsTrue().Because("largest first");
        var shelf = parts[1];
        await Assert.That(shelf.Place).IsFalse();
        await Assert.That(shelf.Cells).IsEqualTo(25);
        await Assert.That(shelf.MinX).IsEqualTo(55);
        await Assert.That(shelf.MinZ).IsEqualTo(55);
        await Assert.That(shelf.MaxX).IsEqualTo(59);
        await Assert.That(shelf.MaxZ).IsEqualTo(59);
        await Assert.That(shelf.CentroidX).IsEqualTo(57);
        await Assert.That(shelf.CentroidZ).IsEqualTo(57);
        await Assert.That(shelf.Share).IsLessThan(ReliefReadback.PlaceShare);
    }

    [Test]
    public async Task The_named_parts_account_for_the_places_and_the_shares_sum_to_the_island()
    {
        var read = ReliefReadback.Read(Field(60, 60, (x, z) => x >= 55 && z >= 55 ? 30 : 8));
        var tier = TierOf(read, "walk");

        await Assert.That(tier.Parts.Count(part => part.Place)).IsEqualTo(tier.Places);
        await Assert.That(tier.Parts.Sum(part => part.Cells)).IsEqualTo(read.Cells)
            .Because("nothing is left out when the pieces fit under the cap");
        await Assert.That(Math.Abs(tier.Parts.Sum(part => part.Share) - 1)).IsLessThan(0.001);
    }

    [Test]
    public async Task A_landform_qualifies_as_a_cliff_where_a_structure_does_not()
    {
        // The corpus rule discriminates on the result rather than on what the author called it: a wide face
        // at a big drop is terrain, and a narrow one at the same drop is a building.
        var mesa = ReliefReadback.Read(Field(60, 60, (x, z) => x is >= 10 and < 45 && z is >= 10 and < 45 ? 22 : 8));
        var monolith = ReliefReadback.Read(Field(60, 60, (x, z) => x is >= 20 and < 26 && z is >= 20 and < 26 ? 22 : 8));

        await Assert.That(mesa.Cliffs).IsGreaterThan(0);
        await Assert.That(mesa.Faces.Max(face => face.Width)).IsGreaterThanOrEqualTo(35);
        await Assert.That(monolith.Faces.Count).IsGreaterThan(0);   // it IS a face
        await Assert.That(monolith.Faces.Max(face => face.Width)).IsEqualTo(6);
        await Assert.That(monolith.Cliffs).IsEqualTo(0);            // just not a landform
    }

    [Test]
    public async Task A_face_is_measured_across_its_facing_not_around_its_perimeter()
    {
        // The distinction the width rule rests on. Joining the brink without regard to direction wraps a
        // block's four sides into one run, and a small block then measures its whole perimeter "wide".
        var read = ReliefReadback.Read(Field(60, 60, (x, z) => x is >= 20 and < 26 && z is >= 20 and < 26 ? 22 : 8));

        await Assert.That(read.Faces.Count).IsEqualTo(4);                       // one per side
        await Assert.That(read.Faces.All(face => face.Width == 6)).IsTrue();
        await Assert.That(read.Faces.Select(face => face.Facing).Distinct().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task An_unfair_map_is_unfair_invisibly_unless_the_symmetry_is_measured()
    {
        // Nothing else in the report changes when one half is a block higher than the other — the tiers, the
        // places and the faces all read the same. This is the only measure that catches it.
        var fair = Field(60, 40, (x, _) => 8 + Math.Min(x, 59 - x) / 6);
        var tilted = Field(60, 40, (x, _) => 8 + Math.Min(x, 59 - x) / 6 + (x < 30 ? 1 : 0));

        await Assert.That(ReliefReadback.Read(fair, "mirror_x", 30, 20).SymmetryError).IsEqualTo(0);
        await Assert.That(ReliefReadback.Read(tilted, "mirror_x", 30, 20).SymmetryError).IsGreaterThan(0);
        // With no symmetry declared there is nothing to be unfair about.
        await Assert.That(ReliefReadback.Read(tilted).SymmetryError).IsEqualTo(0);
    }

    [Test]
    public async Task The_step_histogram_accounts_for_every_boundary()
    {
        var read = ReliefReadback.Read(Field(30, 30, (x, _) => x < 15 ? 8 : 14));
        var counted = read.Steps.Values.Sum();

        // Two boundaries per cell in a full rectangle, minus the two edges that have no neighbour beyond.
        await Assert.That(counted).IsEqualTo(30 * 29 * 2);
        await Assert.That(read.Steps["barrier"]).IsEqualTo(30);   // one per row, at the face
    }

    [Test]
    public async Task A_solved_relief_reads_without_special_casing()
    {
        // The case the panel actually shows: the solver's own output, grain and all.
        var footprint = Board(80, 60);
        var field = ReliefSolver.Solve(footprint, new ReliefSpec
        {
            Base = 8,
            Grain = 1.2,
            Marks = [new RimMark(6), new PointMark(20, 20, 20, 5), new PointMark(62, 42, 14, 4)],
        });
        var read = ReliefReadback.Read(field);

        await Assert.That(read.Cells).IsEqualTo(footprint.Count);
        await Assert.That(read.Relief).IsGreaterThan(0);
        await Assert.That(TierOf(read, "walk").Share).IsBetween(0, 1);
        await Assert.That(TierOf(read, "scramble").Share).IsGreaterThanOrEqualTo(TierOf(read, "walk").Share);
    }
}
