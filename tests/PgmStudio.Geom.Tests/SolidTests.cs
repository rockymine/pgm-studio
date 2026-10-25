using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// What a column of blocks meets of a solid: one run for each convex form at any turn and tilt, two for a torus
/// on its edge, and the same footprint with the width and depth swapped by a quarter turn.
/// </summary>
public sealed class SolidTests
{
    private static List<(int Floor, int Top)> RunsAt(Solid solid, int x, int z)
        => [.. solid.Columns().Where(run => run.X == x && run.Z == z).Select(run => (run.Floor, run.Top))];

    private static HashSet<(int X, int Z)> Cells(Solid solid)
        => [.. solid.Columns().Select(run => (run.X, run.Z))];

    [Test]
    public async Task A_sphere_is_its_diameter_at_the_middle_and_thins_to_the_rim()
    {
        var sphere = new Solid(SolidForm.Sphere, 10.5, 10.5, 6, 6, 6);

        await Assert.That(RunsAt(sphere, 10, 10)).IsEquivalentTo([(0, 6)]);
        await Assert.That(RunsAt(sphere, 12, 10)).IsEquivalentTo([(1, 5)]).Because("two blocks out, the chord is four");
        await Assert.That(RunsAt(sphere, 12, 12)).IsEquivalentTo([(2, 4)]).Because("at the rim a column grazes the middle courses");
        await Assert.That(RunsAt(sphere, 13, 10)).IsEmpty().Because("three out the chord is a point between two block centres");
    }

    [Test]
    [Arguments(2)] [Arguments(3)] [Arguments(5)] [Arguments(8)]
    public async Task A_sphere_is_its_diameter_at_the_middle_whatever_its_radius(int radius)
    {
        var sphere = new Solid(SolidForm.Sphere, 20.5, 20.5, radius * 2, radius * 2, radius * 2, Floor: 4);

        await Assert.That(RunsAt(sphere, 20, 20)).IsEquivalentTo([(4, 4 + radius * 2)]);
    }

    [Test]
    public async Task A_box_tilted_forty_five_degrees_rises_a_block_a_column_along_its_tilt()
    {
        var ramp = new Solid(SolidForm.Box, 10, 10, 4, 2, 10, Tilt: 45);

        var floors = Enumerable.Range(7, 6).Select(z => RunsAt(ramp, 10, z)).ToList();
        await Assert.That(floors.All(runs => runs.Count == 1)).IsTrue().Because("a box is convex, so a column meets it once");
        for (var at = 1; at < floors.Count; at++)
        {
            await Assert.That(floors[at][0].Floor - floors[at - 1][0].Floor).IsEqualTo(1);
            await Assert.That(floors[at][0].Top - floors[at - 1][0].Top).IsEqualTo(1);
        }
        await Assert.That(ramp.Lowest).IsEqualTo(0.0).Because("the lowest point stands on the floor");
    }

    [Test]
    public async Task A_wedge_falls_from_its_tall_end()
    {
        var wedge = new Solid(SolidForm.Wedge, 10, 10, 4, 6, 8);

        var tops = Enumerable.Range(6, 7).Select(z => RunsAt(wedge, 10, z)[0].Top).ToList();
        await Assert.That(tops).IsEquivalentTo([6, 5, 4, 3, 3, 2, 1]);
        await Assert.That(RunsAt(wedge, 10, 13)).IsEmpty().Because("the far end comes to an edge");
        await Assert.That(RunsAt(wedge, 10, 6)[0].Floor).IsEqualTo(0);
    }

    [Test]
    public async Task A_cylinders_run_is_its_height_in_every_column_of_its_footprint()
    {
        var cylinder = new Solid(SolidForm.Cylinder, 10, 10, 6, 5, 6, Floor: 2);

        var runs = cylinder.Columns().ToList();
        await Assert.That(runs.Count).IsBetween(24, 36);
        await Assert.That(runs.All(run => run.Floor == 2 && run.Top == 7)).IsTrue();
        await Assert.That(RunsAt(cylinder, 13, 13)).IsEmpty();
    }

    [Test]
    public async Task A_torus_lying_flat_has_no_run_over_its_hole()
    {
        var ring = new Solid(SolidForm.Torus, 10, 10, 12, 4, 12, Tube: 2);

        await Assert.That(RunsAt(ring, 10, 10)).IsEmpty();
        await Assert.That(RunsAt(ring, 9, 9)).IsEmpty();
        await Assert.That(RunsAt(ring, 14, 10)).IsEquivalentTo([(0, 4)]).Because("over the ring's centreline it is the tube's full diameter");
        await Assert.That(ring.Columns().GroupBy(run => (run.X, run.Z)).All(column => column.Count() == 1)).IsTrue();
    }

    [Test]
    public async Task A_torus_on_its_edge_is_met_twice_by_a_column_through_its_hole()
    {
        var ring = new Solid(SolidForm.Torus, 10.5, 10.5, 12, 4, 12, Tilt: 90, Tube: 2);

        var runs = RunsAt(ring, 10, 10);
        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0].Top).IsLessThan(runs[1].Floor).Because("the hole is between the two arcs of the ring");
        await Assert.That(runs[0].Floor).IsEqualTo(0);
        await Assert.That(runs[1].Top).IsEqualTo(12);
    }

    [Test]
    public async Task A_quarter_turn_swaps_width_and_depth()
    {
        var along = new Solid(SolidForm.Box, 10, 10, 8, 3, 2);
        var across = along with { Turn = 90 };

        await Assert.That(Cells(along)).IsEquivalentTo(
            Enumerable.Range(6, 8).SelectMany(x => new[] { 9, 10 }.Select(z => (x, z))));
        await Assert.That(Cells(across)).IsEquivalentTo(
            new[] { 9, 10 }.SelectMany(x => Enumerable.Range(6, 8).Select(z => (x, z))));
    }

    [Test]
    public async Task A_solid_is_sunk_below_its_floor()
    {
        var bedded = new Solid(SolidForm.Box, 10, 10, 4, 6, 4, Floor: 5, Sunk: 3);

        await Assert.That(RunsAt(bedded, 10, 10)).IsEquivalentTo([(2, 8)]);
        await Assert.That(bedded.Lowest).IsEqualTo(2.0);
    }

    [Test]
    public async Task A_solid_with_no_volume_has_no_run()
    {
        await Assert.That(new Solid(SolidForm.Box, 0, 0, 4, 0, 4).Columns()).IsEmpty();
        await Assert.That(new Solid(SolidForm.Sphere, 0, 0, 0, 0, 0).Columns()).IsEmpty();
        await Assert.That(new Solid(SolidForm.Torus, 0, 0, 8, 0, 8, Tube: 0).Columns()).IsEmpty();
    }
}
