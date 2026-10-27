using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// What each terraform operation guarantees of the ground it runs over, asserted as invariants: a Hill raises its
/// middle by its rise and nothing outside its rim, a Hollow reaches its floor and never raises, a Level makes its
/// inner area one height, Roughness is the seed's and only the seed's, and a mode never lets a change cross it.
/// </summary>
public sealed class ModifiersTests
{
    private const int Size = 40;

    /// <summary>A square field of columns, each standing at its cell's centre.</summary>
    private sealed class Field
    {
        public double[] Heights { get; }
        public double[] Xs { get; }
        public double[] Zs { get; }
        public double[] Original { get; }

        public Field(Func<int, int, double> ground)
        {
            Heights = new double[Size * Size];
            Xs = new double[Size * Size];
            Zs = new double[Size * Size];
            for (var z = 0; z < Size; z++)
                for (var x = 0; x < Size; x++)
                {
                    Heights[z * Size + x] = ground(x, z);
                    Xs[z * Size + x] = x + 0.5;
                    Zs[z * Size + x] = z + 0.5;
                }
            Original = (double[])Heights.Clone();
        }

        public double At(int x, int z) => Heights[z * Size + x];
        public double Was(int x, int z) => Original[z * Size + x];
        public void Run(ModifierSpec spec) => Modifiers.Apply(spec, Heights, Xs, Zs);

        /// <summary>The columns whose centre lies at least <paramref name="from"/> out in the area's own measure.</summary>
        public IEnumerable<int> Outside(MaskArea area, double from) =>
            Enumerable.Range(0, Heights.Length).Where(index => area.E(Xs[index], Zs[index]) >= from);
    }

    private static Field Flat(double height) => new((_, _) => height);

    private static readonly MaskArea Middle = MaskArea.Ellipse(20.5, 20.5, 10, 10);

    private static ModifierSpec Hill(params MaskArea[] areas) =>
        new() { Op = ModifierOp.Mound, Areas = areas, Rise = 6 };

    [Test]
    public async Task A_hill_raises_its_middle_by_its_rise_and_nothing_outside_its_rim()
    {
        var field = Flat(50);
        field.Run(Hill(Middle));

        await Assert.That(field.At(20, 20)).IsEqualTo(56).Within(1e-9);
        foreach (var index in field.Outside(Middle, 1))
            await Assert.That(field.Heights[index]).IsEqualTo(50);
        await Assert.That(field.Heights.Max()).IsLessThanOrEqualTo(56 + 1e-9);
        await Assert.That(field.At(23, 20)).IsGreaterThan(50);
    }

    [Test]
    public async Task A_hill_falls_from_its_middle_to_its_rim()
    {
        var field = Flat(50);
        field.Run(Hill(Middle));

        await Assert.That(field.At(20, 20)).IsGreaterThan(field.At(24, 20));
        await Assert.That(field.At(24, 20)).IsGreaterThan(field.At(28, 20));
    }

    [Test]
    public async Task A_hill_with_no_rise_stated_takes_the_default()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec { Op = ModifierOp.Mound, Areas = [Middle] });

        await Assert.That(field.At(20, 20)).IsEqualTo(50 + ModifierDefaults.MoundRise).Within(1e-9);
    }

    [Test]
    public async Task Softness_fades_a_hill_in_from_its_rim_and_leaves_its_middle()
    {
        var sharp = Flat(50);
        var soft = Flat(50);
        sharp.Run(Hill(Middle));
        soft.Run(Hill(Middle) with { Soft = 0.5 });

        await Assert.That(soft.At(20, 20)).IsEqualTo(sharp.At(20, 20)).Within(1e-9);
        await Assert.That(soft.At(28, 20) - 50).IsLessThan(sharp.At(28, 20) - 50);
        await Assert.That(soft.At(28, 20)).IsGreaterThanOrEqualTo(50);
    }

    [Test]
    public async Task A_hill_with_a_negative_rise_in_cut_mode_sinks_a_dish_of_its_own_shape_and_raises_nothing()
    {
        var field = Flat(50);
        field.Run(Hill(Middle) with { Rise = -6, Mode = ModifierMode.Cut });

        await Assert.That(field.At(20, 20)).IsEqualTo(44).Within(1e-9);
        await Assert.That(field.Heights.Max()).IsEqualTo(50);
    }

    [Test]
    public async Task Lift_mode_never_lowers_and_cut_mode_never_raises()
    {
        var lifted = Flat(50);
        var cut = Flat(50);
        lifted.Run(Hill(Middle) with { Rise = -6, Mode = ModifierMode.Lift });
        cut.Run(Hill(Middle) with { Rise = 6, Mode = ModifierMode.Cut });

        await Assert.That(lifted.Heights.Min()).IsEqualTo(50);
        await Assert.That(cut.Heights.Max()).IsEqualTo(50);
    }

    [Test]
    public async Task A_negative_rise_with_no_mode_is_a_hollow()
    {
        var field = Flat(50);
        field.Run(Hill(Middle) with { Rise = -6 });

        await Assert.That(field.At(20, 20)).IsEqualTo(44).Within(1e-9);
    }

    [Test]
    public async Task Joining_two_overlapping_hills_fills_the_saddle_that_a_plain_maximum_leaves()
    {
        var west = MaskArea.Ellipse(15.5, 20.5, 8, 8);
        var east = MaskArea.Ellipse(25.5, 20.5, 8, 8);
        var kept = Flat(50);
        var melted = Flat(50);
        var single = Flat(50);
        kept.Run(Hill(west, east));
        melted.Run(Hill(west, east) with { Join = 2 });
        single.Run(Hill(west));

        await Assert.That(kept.At(20, 20)).IsEqualTo(single.At(20, 20)).Within(1e-9);
        await Assert.That(melted.At(20, 20)).IsGreaterThan(kept.At(20, 20) + 1);
    }

    [Test]
    public async Task Joining_hills_that_do_not_touch_changes_nothing()
    {
        var west = MaskArea.Ellipse(8.5, 20.5, 5, 5);
        var east = MaskArea.Ellipse(30.5, 20.5, 5, 5);
        var kept = Flat(50);
        var melted = Flat(50);
        kept.Run(Hill(west, east));
        melted.Run(Hill(west, east) with { Join = 3 });

        for (var index = 0; index < kept.Heights.Length; index++)
            await Assert.That(melted.Heights[index]).IsEqualTo(kept.Heights[index]).Within(1e-9);
    }

    [Test]
    public async Task A_hollow_cut_into_high_ground_reaches_its_floor_in_the_middle()
    {
        var field = Flat(60);
        field.Run(new ModifierSpec { Op = ModifierOp.Crater, Areas = [Middle], Floor = 40 });

        await Assert.That(field.At(20, 20)).IsEqualTo(40);
        await Assert.That(field.Heights.Max()).IsEqualTo(60);
    }

    [Test]
    public async Task A_hollow_holds_a_flat_floor_and_climbs_out_at_its_slope()
    {
        var field = Flat(80);
        field.Run(new ModifierSpec { Op = ModifierOp.Crater, Areas = [MaskArea.Ellipse(20.5, 20.5, 12, 12)], Floor = 40, Flat = 4, Slope = 2 });

        await Assert.That(field.At(20, 20)).IsEqualTo(40);
        await Assert.That(field.At(22, 20)).IsEqualTo(40);
        await Assert.That(field.At(26, 20)).IsEqualTo(40 + 2 * (6.0 - 4.0)).Within(1e-9);
    }

    [Test]
    public async Task A_hollow_leaves_the_ground_past_its_rim_and_never_raises_it()
    {
        var field = Flat(60);
        field.Run(new ModifierSpec { Op = ModifierOp.Crater, Areas = [Middle], Floor = 40 });

        foreach (var index in field.Outside(Middle, 1.05))
            await Assert.That(field.Heights[index]).IsEqualTo(60);
        for (var index = 0; index < field.Heights.Length; index++)
            await Assert.That(field.Heights[index]).IsLessThanOrEqualTo(field.Original[index]);
    }

    [Test]
    public async Task A_hollow_under_its_floor_leaves_the_ground_alone()
    {
        var field = Flat(30);
        field.Run(new ModifierSpec { Op = ModifierOp.Crater, Areas = [Middle], Floor = 40 });

        await Assert.That(field.Heights.SequenceEqual(field.Original)).IsTrue();
    }

    [Test]
    public async Task Melting_a_hollows_rim_rounds_it_by_at_most_a_quarter_of_the_melt()
    {
        var wide = MaskArea.Ellipse(20.5, 20.5, 30, 30);
        var sharp = Flat(60);
        var melted = Flat(60);
        sharp.Run(new ModifierSpec { Op = ModifierOp.Crater, Areas = [wide], Floor = 40 });
        melted.Run(new ModifierSpec { Op = ModifierOp.Crater, Areas = [wide], Floor = 40, Melt = 4 });

        for (var index = 0; index < sharp.Heights.Length; index++)
        {
            await Assert.That(melted.Heights[index]).IsLessThanOrEqualTo(sharp.Heights[index] + 1e-9);
            await Assert.That(melted.Heights[index]).IsGreaterThanOrEqualTo(sharp.Heights[index] - 1 - 1e-9);
        }
        await Assert.That(melted.Heights.Zip(sharp.Heights).Any(pair => pair.First < pair.Second - 0.1)).IsTrue();
    }

    [Test]
    public async Task Overlapping_hollows_join_by_depth()
    {
        var west = MaskArea.Ellipse(15.5, 20.5, 8, 8);
        var east = MaskArea.Ellipse(25.5, 20.5, 8, 8);
        var kept = Flat(60);
        var melted = Flat(60);
        kept.Run(new ModifierSpec { Op = ModifierOp.Crater, Areas = [west, east], Floor = 40 });
        melted.Run(new ModifierSpec { Op = ModifierOp.Crater, Areas = [west, east], Floor = 40, Join = 2 });

        await Assert.That(melted.At(20, 20)).IsLessThan(kept.At(20, 20));
        await Assert.That(melted.Heights.Max()).IsEqualTo(60);
    }

    [Test]
    public async Task A_level_makes_its_inner_area_equal_to_its_top_and_leaves_the_ground_past_its_skirt()
    {
        var field = new Field((x, _) => 40 + x * 0.2);
        field.Run(new ModifierSpec { Op = ModifierOp.Level, Areas = [Middle], Top = 50 });

        foreach (var index in Enumerable.Range(0, field.Heights.Length))
        {
            var distance = Middle.E(field.Xs[index], field.Zs[index]);
            if (distance < 1) await Assert.That(field.Heights[index]).IsEqualTo(50);
            else if (distance >= 1.5) await Assert.That(field.Heights[index]).IsEqualTo(field.Original[index]);
        }
    }

    [Test]
    public async Task A_levels_skirt_runs_between_its_top_and_the_ground()
    {
        var field = Flat(40);
        field.Run(new ModifierSpec { Op = ModifierOp.Level, Areas = [Middle], Top = 50 });

        var skirt = Enumerable.Range(0, field.Heights.Length)
                              .Where(index => Middle.E(field.Xs[index], field.Zs[index]) is >= 1 and < 1.5).ToList();
        await Assert.That(skirt.Count).IsGreaterThan(0);
        foreach (var index in skirt)
            await Assert.That(field.Heights[index]).IsBetween(40, 50);
    }

    [Test]
    public async Task A_level_with_no_top_stated_takes_the_median_of_the_ground_inside()
    {
        var field = new Field((x, z) => 40 + (x + z) % 7);
        field.Run(new ModifierSpec { Op = ModifierOp.Level, Areas = [Middle] });

        var inside = Enumerable.Range(0, field.Heights.Length)
                               .Where(index => Middle.E(field.Xs[index], field.Zs[index]) < 1).ToList();
        var sorted = inside.Select(index => field.Original[index]).Order().ToList();
        var median = sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
        foreach (var index in inside)
            await Assert.That(field.Heights[index]).IsEqualTo(median).Within(1e-9);
    }

    [Test]
    public async Task A_level_in_lift_mode_never_lowers_the_ground()
    {
        var field = Flat(60);
        field.Run(new ModifierSpec { Op = ModifierOp.Level, Areas = [Middle], Top = 50, Mode = ModifierMode.Lift });

        await Assert.That(field.Heights.SequenceEqual(field.Original)).IsTrue();
    }

    [Test]
    public async Task Roughness_with_one_seed_is_the_same_every_time_and_another_seed_differs()
    {
        var spec = new ModifierSpec { Op = ModifierOp.Noise, Areas = [Middle], Seed = 5 };
        var first = Flat(50);
        var again = Flat(50);
        var other = Flat(50);
        first.Run(spec);
        again.Run(spec);
        other.Run(spec with { Seed = 6 });

        await Assert.That(again.Heights.SequenceEqual(first.Heights)).IsTrue();
        await Assert.That(other.Heights.SequenceEqual(first.Heights)).IsFalse();
        await Assert.That(first.Heights.Any(height => height != 50)).IsTrue();
    }

    [Test]
    public async Task Roughness_moves_nothing_outside_its_rim_and_stays_within_its_amplitude()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec
        {
            Op = ModifierOp.Noise, Areas = [Middle], Seed = 3,
            Sizes = [new NoiseSize(12, 2, 3), new NoiseSize(4, 1, 1)],
        });

        foreach (var index in field.Outside(Middle, 1))
            await Assert.That(field.Heights[index]).IsEqualTo(50);
        await Assert.That(field.Heights.Max()).IsLessThanOrEqualTo(54);
        await Assert.That(field.Heights.Min()).IsGreaterThanOrEqualTo(46);
    }

    [Test]
    public async Task Roughness_in_lift_mode_only_raises_and_in_cut_mode_only_lowers()
    {
        var lifted = Flat(50);
        var cut = Flat(50);
        var spec = new ModifierSpec { Op = ModifierOp.Noise, Areas = [Middle], Seed = 9, Sizes = [new NoiseSize(8, 2, 4)] };
        lifted.Run(spec with { Mode = ModifierMode.Lift });
        cut.Run(spec with { Mode = ModifierMode.Cut });

        await Assert.That(lifted.Heights.Min()).IsEqualTo(50);
        await Assert.That(lifted.Heights.Max()).IsGreaterThan(50);
        await Assert.That(cut.Heights.Max()).IsEqualTo(50);
        await Assert.That(cut.Heights.Min()).IsLessThan(50);
    }

    [Test]
    public async Task A_board_area_acts_on_all_the_ground_at_full_strength()
    {
        var field = Flat(50);
        field.Run(Hill(MaskArea.Board));

        await Assert.That(field.Heights.All(height => height == 56)).IsTrue();
    }

    [Test]
    public async Task A_modifier_with_no_area_changes_nothing()
    {
        var field = Flat(50);
        field.Run(Hill());

        await Assert.That(field.Heights.SequenceEqual(field.Original)).IsTrue();
    }

    [Test]
    public async Task Fanned_areas_raise_a_pair_of_images_equally()
    {
        var field = Flat(50);
        var areas = MaskArea.Fanned([MaskArea.Ellipse(8.5, 11.5, 6, 4, angleDegrees: 30)], ["rot_180"], 20, 20);
        field.Run(Hill(areas.ToArray()));

        for (var x = 0; x < Size; x++)
            for (var z = 0; z < Size; z++)
            {
                var (imageX, imageZ) = Symmetry.ApplyCell(x, z, "rot_180", 20, 20);
                if (imageX is < 0 or >= Size || imageZ is < 0 or >= Size) continue;
                await Assert.That(field.At(imageX, imageZ)).IsEqualTo(field.At(x, z)).Within(1e-9);
            }
        await Assert.That(field.At(8, 11)).IsGreaterThan(50);
    }

    [Test]
    public async Task A_polygon_hill_peaks_at_its_deepest_point()
    {
        var field = Flat(50);
        var pond = MaskArea.Polygon([(10, 10), (30, 10), (30, 30), (10, 30)]);
        field.Run(Hill(pond));

        await Assert.That(field.At(19, 19)).IsGreaterThan(55.5);
        await Assert.That(field.At(11, 11)).IsLessThan(field.At(19, 19));
        await Assert.That(field.At(5, 5)).IsEqualTo(50);
    }

    [Test]
    public async Task Modifiers_run_over_the_ground_the_one_before_left()
    {
        var field = Flat(50);
        field.Run(Hill(Middle));
        field.Run(new ModifierSpec { Op = ModifierOp.Level, Areas = [Middle], Top = 52 });

        await Assert.That(field.At(20, 20)).IsEqualTo(52);
    }

    [Test]
    [Arguments(0.0, 1.0, 0.5, 0.5)]
    [Arguments(0.0, 1.0, -1.0, 0.0)]
    [Arguments(1.0, 0.0, 0.5, 0.5)]
    [Arguments(1.0, 1.0, 2.0, 1.0)]
    [Arguments(1.0, 1.0, 0.5, 0.0)]
    public async Task Smooth_step_is_a_ramp_that_is_a_step_when_its_edges_are_equal(
        double edge0, double edge1, double x, double expected)
    {
        await Assert.That(Modifiers.SmoothStep(edge0, edge1, x)).IsEqualTo(expected).Within(1e-12);
    }

    [Test]
    public async Task Roughness_over_a_folded_board_and_over_a_fanned_area_reads_alike_at_every_image()
    {
        static bool Symmetric(ReliefField field) => Enumerable.Range(0, 40 * 40)
            .All(index => Math.Abs(field.Heights[index] - field.Heights[40 * 40 - 1 - index]) < 1e-9);
        var board = ReliefField.Flat(40, 40, 50);
        var area = ReliefField.Flat(40, 40, 50);
        var warped = new ReliefField(40, 40, (x, z) => 50 + 0.3 * x + 0.1 * z * z % 7);
        board.Run(new ModifierSpec { Op = ModifierOp.Noise, Areas = MaskArea.Fanned([MaskArea.Board], ["rot_180"], 20, 20), Sizes = [new(9, 3, 3)] });
        area.Run(new ModifierSpec { Op = ModifierOp.Noise, Areas = MaskArea.Fanned([MaskArea.Ellipse(9.5, 11.5, 8, 6)], ["rot_180"], 20, 20), Sizes = [new(5, 2, 2)] });

        await Assert.That(board.Heights.Distinct().Count()).IsGreaterThan(100);
        await Assert.That(Symmetric(board)).IsTrue();
        await Assert.That(Symmetric(area)).IsTrue();
    }
}
