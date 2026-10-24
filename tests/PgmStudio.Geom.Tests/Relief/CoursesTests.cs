using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// What a river, a lake and a road guarantee of the ground they run over: a river's bed lies under its banks and
/// holds water that never flows uphill, a lake's surface is its level over the bed it cut with its shore held at
/// the water, and a road's grade never exceeds the grade it states along its run.
/// </summary>
public sealed class CoursesTests
{
    private const int Width = 60;
    private const int Depth = 40;

    /// <summary>A line along x through the middle of the field, centred on row 20.</summary>
    private static MaskArea Run(double width) => MaskArea.Line([(2.5, 20.5), (57.5, 20.5)], width);

    private static ReliefField Flat(double height) => ReliefField.Flat(Width, Depth, height);

    private static ModifierSpec River(MaskArea line) =>
        new() { Op = ModifierOp.River, Areas = [line], Depth = 2, Water = 1, Bank = 4 };

    [Test]
    public async Task A_rivers_bed_lies_under_its_banks_and_holds_water()
    {
        var field = Flat(50);
        field.Run(River(Run(6)));

        // Rows 17..23 are within three blocks of the line; the bed is the ground two down.
        for (var z = 17; z <= 23; z++)
        {
            await Assert.That(field.At(30, z)).IsEqualTo(48);
            await Assert.That(field.WaterAt(30, z)).IsEqualTo(49);
            await Assert.That(field.WaterAt(30, z)).IsGreaterThan(field.At(30, z));
        }
        await Assert.That(field.At(30, 10)).IsEqualTo(50);
        await Assert.That(field.At(30, 10)).IsGreaterThan(field.At(30, 20));
        await Assert.That(double.IsNaN(field.WaterAt(30, 10))).IsTrue();
    }

    [Test]
    public async Task A_river_wets_exactly_its_channel_and_nothing_beside_it()
    {
        var field = Flat(50);
        var line = Run(6);
        field.Run(River(line));

        for (var index = 0; index < field.Heights.Length; index++)
        {
            var inChannel = line.E(field.Xs[index], field.Zs[index]) <= 1;
            await Assert.That(double.IsNaN(field.Waters[index])).IsEqualTo(!inChannel);
        }
    }

    [Test]
    public async Task A_rivers_banks_never_stand_under_the_water_beside_them()
    {
        var field = new ReliefField(Width, Depth, (_, z) => z is 16 or 24 or 14 ? 44 : 50);
        field.Run(River(Run(6)));

        // Rows 16 and 24 border the channel and stand low; row 14 is a block further out.
        await Assert.That(field.At(30, 16)).IsGreaterThanOrEqualTo(49);
        await Assert.That(field.At(30, 24)).IsGreaterThanOrEqualTo(49);
        await Assert.That(field.At(30, 14)).IsLessThan(49).Because("a low column the water does not touch is left");
    }

    [Test]
    public async Task A_rivers_banks_ease_back_to_the_ground_over_the_bank_width()
    {
        var field = Flat(50);
        field.Run(River(Run(6)) with { Bank = 4 });

        await Assert.That(field.At(30, 15)).IsGreaterThan(field.At(30, 16))
            .Because("row 15 is further from the water");
        await Assert.That(field.At(30, 16)).IsGreaterThanOrEqualTo(49);
        await Assert.That(field.At(30, 13)).IsEqualTo(50).Because("seven blocks out is the end of the bank");
    }

    [Test]
    public async Task A_rivers_bed_never_climbs_on_ground_that_falls_along_it()
    {
        var field = new ReliefField(Width, Depth, (x, _) => 70 - 0.4 * x);
        field.Run(River(Run(4)));

        var bed = Enumerable.Range(3, 54).Select(x => field.At(x, 20)).ToList();
        for (var at = 1; at < bed.Count; at++)
            await Assert.That(bed[at]).IsLessThanOrEqualTo(bed[at - 1]);
        await Assert.That(bed[^1]).IsLessThan(bed[0]);
        for (var x = 3; x < 57; x++)
            await Assert.That(field.At(x, 20)).IsLessThanOrEqualTo(field.Was(x, 20));
    }

    [Test]
    public async Task A_rivers_bed_holds_level_where_the_ground_rises_along_it()
    {
        var field = new ReliefField(Width, Depth, (x, _) => 50 + 0.2 * x);
        field.Run(River(Run(4)));

        var bed = Enumerable.Range(3, 54).Select(x => field.At(x, 20)).Distinct().ToList();
        await Assert.That(bed.Count).IsEqualTo(1).Because("the water cannot be carried uphill, so the bed cuts deeper instead");
        await Assert.That(field.WaterAt(5, 20)).IsEqualTo(field.WaterAt(55, 20));
    }

    [Test]
    public async Task A_river_drops_in_falls_only_after_a_reach_and_only_by_two_or_more()
    {
        var field = new ReliefField(Width, Depth, (x, _) => 70 - 0.4 * x);
        field.Run(River(Run(4)));

        var surfaces = Enumerable.Range(3, 54).Select(x => field.WaterAt(x, 20)).ToList();
        var drops = surfaces.Zip(surfaces.Skip(1), (before, after) => before - after).Where(drop => drop != 0).ToList();
        await Assert.That(drops.Count).IsGreaterThan(0);
        await Assert.That(drops.All(drop => drop >= 2)).IsTrue();
        var runs = surfaces.GroupBy(surface => surface).Select(group => group.Count()).ToList();
        await Assert.That(runs.Take(runs.Count - 1).All(length => length >= 10)).IsTrue();
    }

    [Test]
    public async Task A_second_rivers_water_replaces_the_first_where_they_cross()
    {
        var field = Flat(50);
        field.Run(River(Run(6)));
        field.Run(River(Run(6)) with { Depth = 5, Water = 2 });

        await Assert.That(field.WaterAt(30, 20)).IsEqualTo(45)
            .Because("the second bed is cut five under the ground the first left at 48, with two blocks of water in it");
    }

    private static ModifierSpec Lake(MaskArea area, double? level) =>
        new() { Op = ModifierOp.Lake, Areas = [area], Level = level, Depth = 3, Shore = 3 };

    private static readonly MaskArea Pond = MaskArea.Ellipse(20.5, 20.5, 8, 8);

    [Test]
    public async Task A_lakes_surface_is_its_level_over_the_bed_it_cut()
    {
        var field = Flat(56);
        field.Run(Lake(Pond, 55));

        foreach (var index in field.Inside(Pond, 1))
        {
            await Assert.That(field.Waters[index]).IsEqualTo(55);
            await Assert.That(field.Heights[index]).IsLessThan(55);
        }
        await Assert.That(field.At(20, 20)).IsEqualTo(52).Because("a bed three under the surface in the middle, two of them water");
        await Assert.That(field.Inside(Pond, 1).Min(index => field.Heights[index])).IsEqualTo(52);
        foreach (var index in field.Outside(Pond, 1.0001))
            await Assert.That(double.IsNaN(field.Waters[index])).IsTrue();
    }

    [Test]
    public async Task A_lakes_bed_rises_to_a_block_under_the_surface_at_its_rim()
    {
        var field = Flat(60);
        field.Run(Lake(Pond, 55));

        await Assert.That(field.At(20, 12)).IsEqualTo(54).Because("the rim column is one block of water deep");
        await Assert.That(field.At(20, 14)).IsLessThan(field.At(20, 12));
        await Assert.That(field.At(20, 20)).IsLessThan(field.At(20, 14));
    }

    [Test]
    public async Task A_lakes_shore_is_held_at_the_water_and_eases_back_to_the_ground()
    {
        var field = Flat(50);
        field.Run(Lake(Pond, 55));

        // Row 11 is one block outside the rim, row 10 two, row 8 four.
        await Assert.That(field.At(20, 11)).IsGreaterThanOrEqualTo(55);
        await Assert.That(field.At(20, 10)).IsGreaterThan(50);
        await Assert.That(field.At(20, 10)).IsLessThan(55);
        await Assert.That(field.At(20, 8)).IsEqualTo(50).Because("past the shore the ground is left");
    }

    [Test]
    public async Task A_lake_with_no_level_stands_one_under_the_ground_a_tenth_of_its_rim_stands_at_or_below()
    {
        var field = new ReliefField(Width, Depth, (x, _) => 70 - 0.5 * x);
        field.Run(Lake(Pond, null));

        var rim = field.Outside(Pond, 1.0001).Where(index => Pond.E(field.Xs[index], field.Zs[index]) <= 1 + 3.0 / 8)
                       .Select(index => field.Original[index]).Order().ToArray();
        var surface = Math.Floor(rim[(int)Math.Floor(0.1 * (rim.Length - 1))]) - 1;
        foreach (var index in field.Inside(Pond, 1))
        {
            await Assert.That(field.Waters[index]).IsEqualTo(surface);
            await Assert.That(field.Heights[index]).IsLessThan(surface);
        }
    }

    [Test]
    public async Task A_lake_given_a_line_does_nothing()
    {
        var field = Flat(50);
        field.Run(Lake(Run(8), 55));

        await Assert.That(field.Heights.SequenceEqual(field.Original)).IsTrue();
        await Assert.That(field.Waters.All(double.IsNaN)).IsTrue();
    }

    /// <summary>Level ground with a bump twelve high and a few blocks wide across the middle of the run.</summary>
    private static double Bump(int x) => 50 + 12 * Math.Exp(-Math.Pow((x - 30) / 3.0, 2));

    private static ModifierSpec Road(MaskArea line) =>
        new() { Op = ModifierOp.Road, Areas = [line], Grade = 0.25, Shoulder = 4 };

    [Test]
    public async Task A_roads_grade_never_exceeds_the_grade_it_states_along_its_run()
    {
        var field = new ReliefField(Width, Depth, (x, _) => Bump(x));
        field.Run(Road(Run(4)));

        var centre = Enumerable.Range(2, 56).Select(x => (X: x, Height: field.At(x, 20))).ToList();
        foreach (var from in centre)
            foreach (var to in centre.Where(other => other.X > from.X))
                await Assert.That(Math.Abs(to.Height - from.Height)).IsLessThanOrEqualTo(0.25 * (to.X - from.X) + 1);
        await Assert.That(field.Was(30, 20) - field.At(30, 20)).IsGreaterThan(3)
            .Because("the road is cut through a bump far steeper than its grade");
    }

    [Test]
    public async Task A_roads_ends_hold_the_ground_they_stand_on()
    {
        var field = new ReliefField(Width, Depth, (x, _) => Bump(x));
        field.Run(Road(Run(4)));

        await Assert.That(Math.Abs(field.At(2, 20) - 50)).IsLessThanOrEqualTo(1);
        await Assert.That(Math.Abs(field.At(57, 20) - 50)).IsLessThanOrEqualTo(1);
    }

    [Test]
    public async Task A_road_is_level_across_its_width_and_leaves_the_ground_past_its_shoulders()
    {
        var field = new ReliefField(Width, Depth, (x, _) => Bump(x));
        field.Run(Road(Run(4)));

        await Assert.That(field.At(30, 19)).IsEqualTo(field.At(30, 20));
        await Assert.That(field.At(30, 21)).IsEqualTo(field.At(30, 20));
        await Assert.That(field.At(30, 10)).IsEqualTo(field.Was(30, 10));
    }

    [Test]
    public async Task A_road_over_ground_it_may_climb_changes_nothing()
    {
        var field = Flat(50);
        field.Run(Road(Run(4)));

        await Assert.That(field.Heights.SequenceEqual(field.Original)).IsTrue();
    }

    [Test]
    public async Task A_coast_floods_its_area_to_the_sea_and_eases_the_land_down_to_a_beach()
    {
        var field = new ReliefField(40, 40, (x, _) => 50 + 0.5 * x);
        var sea = MaskArea.Polygon([(0, 0), (12, 0), (12, 40), (0, 40)]);
        field.Run(new ModifierSpec { Op = ModifierOp.Coast, Areas = [sea], Level = 52, Shelf = 8, Depth = 6, Slope = 0.5 });

        for (var x = 0; x < 12; x++)
        {
            await Assert.That(field.At(x, 20)).IsLessThan(52).Because("nothing inside the sea stands at its surface");
            await Assert.That(field.WaterAt(x, 20)).IsEqualTo(52);
        }
        await Assert.That(field.At(11, 20)).IsGreaterThan(field.At(5, 20)).Because("the floor falls away from the shore");
        await Assert.That(field.At(1, 20)).IsGreaterThanOrEqualTo(46).Because("it falls no deeper than its depth");
        await Assert.That(field.At(12, 20)).IsEqualTo(53).Within(0.1).Because("the column on the shore is a beach one over the sea");
        await Assert.That(field.At(16, 20)).IsLessThan(field.Was(16, 20)).And.IsGreaterThanOrEqualTo(53);
        await Assert.That(field.At(24, 20)).IsEqualTo(field.Was(24, 20)).Because("past the shelf the land is left");
        await Assert.That(double.IsNaN(field.WaterAt(12, 20))).IsTrue();
    }

    [Test]
    public async Task A_coast_with_no_level_stands_one_under_the_ground_a_tenth_of_its_rim_stands_at_or_below()
    {
        var field = new ReliefField(40, 40, (x, _) => 50 + 0.5 * x);
        var sea = MaskArea.Polygon([(0, 0), (12, 0), (12, 40), (0, 40)]);
        field.Run(new ModifierSpec { Op = ModifierOp.Coast, Areas = [sea] });

        await Assert.That(field.WaterAt(4, 20)).IsEqualTo(55).Because("the lowest ground beside the rim is column 12 at 56");
    }

    [Test]
    [Arguments(ModifierOp.Lake)]
    [Arguments(ModifierOp.Coast)]
    public async Task One_cleft_cut_into_the_rim_does_not_drain_the_water(ModifierOp op)
    {
        // Flat ground at 50 with one column on the rim cut down to 2, as a Shatter's cleft cuts it.
        var field = new ReliefField(40, 40, (x, z) => x == 29 && z == 20 ? 2 : 50);
        field.Run(new ModifierSpec { Op = op, Areas = [Pond] });

        await Assert.That(field.WaterAt(20, 20)).IsEqualTo(49).Because("the rim stands at 50 but for one column");
    }
}
