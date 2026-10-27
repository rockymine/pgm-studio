using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// What each landform operation guarantees of the ground it runs over, asserted as invariants: a Butte's top is
/// flat and its rim a cliff, a Spire peaks at its top, a Scarp lifts one hand of its line and not the other, a
/// Canyon's middle lies its depth under the rim, Gullies only cut, a Warp keeps the ground's mean, and an operation
/// handed a mask of a kind it does not read changes nothing.
/// </summary>
public sealed class LandformsTests
{
    private const int Size = 40;

    private static readonly MaskArea Round = MaskArea.Ellipse(20.5, 20.5, 8, 8);

    /// <summary>A line along x through the middle of the field.</summary>
    private static MaskArea Along(double width) => MaskArea.Line([(0.5, 20.5), (39.5, 20.5)], width);

    private static ReliefField Flat(double height) => ReliefField.Flat(Size, Size, height);

    [Test]
    public async Task A_ridge_crests_at_its_rise_on_the_line_and_stays_inside_its_mask()
    {
        var field = Flat(50);
        var line = Along(10);
        field.Run(new ModifierSpec { Op = ModifierOp.Ridge, Areas = [line], Rise = 7 });

        await Assert.That(field.At(20, 20)).IsEqualTo(57).Within(1e-9);
        await Assert.That(field.Heights.Max()).IsLessThanOrEqualTo(57 + 1e-9);
        await Assert.That(field.Heights.Min()).IsGreaterThanOrEqualTo(50);
        foreach (var index in field.Outside(line, 1))
            await Assert.That(field.Heights[index]).IsEqualTo(50);
    }

    [Test]
    public async Task A_ridge_with_no_wander_falls_as_one_minus_the_square_of_the_distance_across()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec { Op = ModifierOp.Ridge, Areas = [Along(10)], Rise = 8, Wander = 0 });

        // Column (20, 22) centres 2 from the line: e = 2 / 5.
        await Assert.That(field.At(20, 22)).IsEqualTo(50 + 8 * (1 - 0.4 * 0.4)).Within(1e-9);
        await Assert.That(field.At(20, 22)).IsGreaterThan(field.At(20, 24));
    }

    [Test]
    public async Task A_ridge_is_also_the_spine_of_an_area_and_does_nothing_on_the_whole_board()
    {
        var onArea = Flat(50);
        var onBoard = Flat(50);
        onArea.Run(new ModifierSpec { Op = ModifierOp.Ridge, Areas = [Round], Rise = 6, Wander = 0 });
        onBoard.Run(new ModifierSpec { Op = ModifierOp.Ridge, Areas = [MaskArea.Board], Rise = 6 });

        await Assert.That(onArea.At(20, 20)).IsEqualTo(56).Within(1e-9);
        await Assert.That(onBoard.Heights.SequenceEqual(onBoard.Original)).IsTrue();
    }

    [Test]
    public async Task A_butte_is_flat_at_its_top_inside_and_falls_steeply_at_its_rim()
    {
        var field = Flat(50);
        // Column (20, 12) centres 8 from the middle: the rim. Column (20, 11) is a block out, (20, 10) two.
        field.Run(new ModifierSpec { Op = ModifierOp.Butte, Areas = [Round], Top = 60, Jag = 0, Cliff = 1.5, Talus = 6, Apron = 0.3 });

        foreach (var index in field.Inside(Round, 0.9))
            await Assert.That(field.Heights[index]).IsEqualTo(60);
        await Assert.That(field.At(20, 11)).IsLessThan(60);
        await Assert.That(field.At(20, 10)).IsLessThan(53.5)
            .Because("two blocks out the cliff has dropped to the foot of the cliff, three tenths of the way up");
        await Assert.That(field.At(20, 11) - field.At(20, 10)).IsGreaterThan(2);
        await Assert.That(field.At(20, 2)).IsEqualTo(50).Within(1e-9);
        await Assert.That(field.Heights.Min()).IsEqualTo(50);
    }

    [Test]
    public async Task A_butte_with_no_top_stands_twelve_over_the_median_ground_and_never_lowers_it()
    {
        var field = new ReliefField(Size, Size, (x, _) => 50 + x * 0.1);
        field.Run(new ModifierSpec { Op = ModifierOp.Butte, Areas = [Round], Jag = 0 });

        var top = field.At(20, 20);
        await Assert.That(top).IsEqualTo(Median(field, Round) + 12).Within(1e-9);
        for (var index = 0; index < field.Heights.Length; index++)
            await Assert.That(field.Heights[index]).IsGreaterThanOrEqualTo(field.Original[index]);
    }

    [Test]
    public async Task A_spire_peaks_at_its_top_and_falls_to_the_ground_at_its_rim()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec { Op = ModifierOp.Spire, Areas = [Round], Top = 72, Taper = 1.6, Jag = 0.15 });

        await Assert.That(field.At(20, 20)).IsEqualTo(72).Within(1e-9);
        await Assert.That(field.Heights.Max()).IsEqualTo(72).Within(1e-9);
        await Assert.That(field.Heights.Min()).IsEqualTo(50);
        foreach (var index in field.Outside(Round, 1.25))
            await Assert.That(field.Heights[index]).IsEqualTo(50);
    }

    [Test]
    public async Task A_lower_taper_makes_a_thinner_needle()
    {
        var needle = Flat(50);
        var full = Flat(50);
        needle.Run(new ModifierSpec { Op = ModifierOp.Spire, Areas = [Round], Top = 70, Taper = 0.6, Jag = 0 });
        full.Run(new ModifierSpec { Op = ModifierOp.Spire, Areas = [Round], Top = 70, Taper = 3, Jag = 0 });

        await Assert.That(needle.At(24, 20)).IsLessThan(full.At(24, 20));
    }

    [Test]
    public async Task A_scarp_raises_the_right_hand_by_its_height_and_leaves_the_left()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec { Op = ModifierOp.Scarp, Areas = [Along(30)], Height = 6, Side = 1, Cliff = 2, Talus = 5 });

        // Walking east along the line, south is the right hand.
        await Assert.That(field.At(20, 28)).IsEqualTo(56).Within(1e-9);
        await Assert.That(field.At(20, 12)).IsEqualTo(50).Within(1e-9);
        await Assert.That(field.At(20, 21)).IsGreaterThan(50)
            .Because("the cliff rises over its width on the line");
        await Assert.That(field.Heights.Min()).IsEqualTo(50);
    }

    [Test]
    public async Task A_scarp_with_side_minus_one_raises_the_left_hand_instead()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec { Op = ModifierOp.Scarp, Areas = [Along(30)], Height = 6, Side = -1 });

        await Assert.That(field.At(20, 12)).IsEqualTo(56).Within(1e-9);
        await Assert.That(field.At(20, 28)).IsEqualTo(50).Within(1e-9);
    }

    [Test]
    public async Task A_scarps_talus_apron_eases_the_low_hand_up_to_its_foot()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec { Op = ModifierOp.Scarp, Areas = [Along(30)], Height = 10, Side = 1, Talus = 5, Apron = 0.3 });

        // 1 block off the line on the low hand: 10 * 0.3 * (1 - 1/5).
        await Assert.That(field.At(20, 19)).IsEqualTo(50 + 10 * 0.3 * (1 - 1.0 / 5)).Within(1e-9)
            .Because("column (20, 19) centres one block from the line");
        await Assert.That(field.At(20, 19)).IsGreaterThan(field.At(20, 17));
    }

    [Test]
    public async Task A_canyons_middle_lies_its_depth_under_the_rim_and_its_walls_climb_to_it()
    {
        var field = Flat(50);
        var line = Along(14);
        field.Run(new ModifierSpec { Op = ModifierOp.Canyon, Areas = [line], Depth = 12, Bed = 0.35, Wall = 2.5 });

        await Assert.That(field.At(20, 20)).IsEqualTo(38).Within(1e-9);
        await Assert.That(field.At(20, 21)).IsEqualTo(38).Within(1e-9)
            .Because("the floor is flat out to a third of the half width");
        await Assert.That(field.Heights.Max()).IsEqualTo(50);
        for (var z = 20; z < 27; z++)
            await Assert.That(field.At(20, z + 1)).IsGreaterThanOrEqualTo(field.At(20, z));
        foreach (var index in field.Outside(line, 1.01))
            await Assert.That(field.Heights[index]).IsEqualTo(50);
    }

    [Test]
    public async Task A_canyon_with_ledges_cuts_its_walls_in_steps_of_that_height()
    {
        var field = Flat(50);
        var line = Along(30);
        field.Run(new ModifierSpec { Op = ModifierOp.Canyon, Areas = [line], Depth = 12, Ledges = 3 });

        foreach (var index in field.Inside(line, 1))
            await Assert.That((field.Heights[index] - 38) % 3).IsEqualTo(0).Within(1e-9);
    }

    [Test]
    public async Task A_canyon_follows_the_ground_its_line_crosses()
    {
        var field = new ReliefField(Size, Size, (x, _) => 40 + x);
        field.Run(new ModifierSpec { Op = ModifierOp.Canyon, Areas = [Along(8)], Depth = 6 });

        await Assert.That(field.At(10, 20)).IsEqualTo(field.Was(10, 20) - 6).Within(1.01);
        await Assert.That(field.At(30, 20)).IsEqualTo(field.Was(30, 20) - 6).Within(1.01);
    }

    [Test]
    public async Task Gullies_only_cut_and_leave_the_ground_outside_the_mask()
    {
        var field = Flat(50);
        var hill = MaskArea.Ellipse(20.5, 20.5, 16, 16);
        field.Run(new ModifierSpec { Op = ModifierOp.Gullies, Areas = [hill], Count = 8, Depth = 3, Seed = 5 });

        await Assert.That(field.Heights.Max()).IsEqualTo(50);
        await Assert.That(field.Heights.Min()).IsLessThan(50);
        await Assert.That(field.Heights.Min()).IsGreaterThanOrEqualTo(47 - 1e-9);
        foreach (var index in field.Outside(hill, 1))
            await Assert.That(field.Heights[index]).IsEqualTo(50);
    }

    [Test]
    public async Task Gullies_leave_the_middle_of_the_mask_alone()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec { Op = ModifierOp.Gullies, Areas = [MaskArea.Ellipse(20.5, 20.5, 16, 16)], Seed = 5 });

        await Assert.That(field.At(20, 20)).IsEqualTo(50);
    }

    [Test]
    public async Task A_warp_keeps_the_mean_height_of_the_ground_it_moves_within_a_block()
    {
        var field = new ReliefField(Size, Size, (x, z) => 50 + 6 * Math.Sin(x / 5.0) + 4 * Math.Cos(z / 7.0));
        field.Run(new ModifierSpec { Op = ModifierOp.Warp, Areas = [MaskArea.Board], Amount = 6, Cell = 24, Seed = 3 });

        await Assert.That(field.Heights.Average()).IsEqualTo(field.Original.Average()).Within(1);
        await Assert.That(field.Heights.Zip(field.Original, (after, before) => Math.Abs(after - before)).Max())
            .IsGreaterThan(0.5)
            .Because("the ground is carried somewhere else");
        await Assert.That(field.Heights.Max()).IsLessThanOrEqualTo(field.Original.Max() + 1e-9);
        await Assert.That(field.Heights.Min()).IsGreaterThanOrEqualTo(field.Original.Min() - 1e-9);
    }

    [Test]
    public async Task A_warp_of_flat_ground_changes_nothing()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec { Op = ModifierOp.Warp, Areas = [MaskArea.Board], Amount = 6 });

        await Assert.That(field.Heights.SequenceEqual(field.Original)).IsTrue();
    }

    [Test]
    [Arguments(ModifierOp.Butte)]
    [Arguments(ModifierOp.Spire)]
    [Arguments(ModifierOp.Gullies)]
    public async Task An_area_operation_given_a_line_does_nothing(ModifierOp op)
    {
        var field = new ReliefField(Size, Size, (x, _) => 50 + x * 0.1);
        field.Run(new ModifierSpec { Op = op, Areas = [Along(10)] });

        await Assert.That(field.Heights.SequenceEqual(field.Original)).IsTrue();
    }

    [Test]
    [Arguments(ModifierOp.Scarp)]
    [Arguments(ModifierOp.Canyon)]
    [Arguments(ModifierOp.River)]
    [Arguments(ModifierOp.Road)]
    public async Task A_line_operation_given_an_ellipse_does_nothing(ModifierOp op)
    {
        var field = new ReliefField(Size, Size, (x, _) => 50 + x * 0.1);
        field.Run(new ModifierSpec { Op = op, Areas = [Round] });

        await Assert.That(field.Heights.SequenceEqual(field.Original)).IsTrue();
        await Assert.That(field.Waters.All(double.IsNaN)).IsTrue();
    }

    [Test]
    public async Task Lift_mode_keeps_a_canyon_from_cutting()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec { Op = ModifierOp.Canyon, Areas = [Along(14)], Mode = ModifierMode.Lift });

        await Assert.That(field.Heights.SequenceEqual(field.Original)).IsTrue();
    }

    private static double Median(ReliefField field, MaskArea area)
    {
        var within = field.Inside(area, 0.999999).Select(index => field.Original[index]).Order().ToList();
        return within.Count % 2 == 1 ? within[within.Count / 2] : (within[within.Count / 2 - 1] + within[within.Count / 2]) / 2;
    }

    [Test]
    public async Task A_profile_climbs_its_steps_away_from_its_line_and_never_falls_outward()
    {
        var field = Flat(50);
        var line = Along(36);
        field.Run(new ModifierSpec
        {
            Op = ModifierOp.Profile, Areas = [line], Jag = 0,
            Steps = [new ProfileStep(4, 6, 5), new ProfileStep(10, 12, 9)],
        });

        await Assert.That(field.At(20, 20)).IsEqualTo(50).Because("the floor follows the ground on the line");
        await Assert.That(field.At(20, 28)).IsEqualTo(55).Within(1e-9).Because("eight out stands on the bench");
        await Assert.That(field.At(20, 34)).IsEqualTo(59).Within(1e-9).Because("fourteen out stands on the plateau");
        await Assert.That(field.At(20, 12)).IsEqualTo(55).Within(1e-9).Because("both hands climb alike");
        for (var z = 21; z < 37; z++)
            await Assert.That(field.At(20, z)).IsGreaterThanOrEqualTo(field.At(20, z - 1) - 1e-9);
        foreach (var index in field.Outside(line, 1))
            await Assert.That(field.Heights[index]).IsEqualTo(50);
    }

    [Test]
    public async Task A_profile_with_a_side_shapes_one_hand_of_its_line_and_a_floor_holds_flat()
    {
        var field = new ReliefField(Size, Size, (x, _) => 50 + 0.2 * x);
        field.Run(new ModifierSpec
        {
            Op = ModifierOp.Profile, Areas = [Along(30)], Jag = 0, Side = 1, Floor = 48,
            Steps = [new ProfileStep(6, 8, 6)],
        });

        await Assert.That(field.At(5, 22)).IsEqualTo(48).Because("the floor is stated, so it is flat across the run");
        await Assert.That(field.At(30, 22)).IsEqualTo(48);
        await Assert.That(field.At(30, 31)).IsEqualTo(54).Within(1e-9);
        await Assert.That(field.At(30, 12)).IsEqualTo(field.Was(30, 12)).Because("the other hand is left alone");
    }

    [Test]
    public async Task A_profile_does_nothing_over_an_area()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec { Op = ModifierOp.Profile, Areas = [Round] });

        await Assert.That(field.Heights.SequenceEqual(field.Original)).IsTrue();
    }
}
