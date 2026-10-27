using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>What hangs under a floating layer: the rock thickens inward from the rim at a stated rate, a sheer
/// side drops far before it narrows, the noise is the same on every image of a mirrored layer, and the
/// underside stays between the course under the top and the first course over the bedrock.</summary>
public sealed class UndersideTests
{
    private static readonly IReadOnlySet<(int X, int Z)> NoSheer = new HashSet<(int X, int Z)>();

    /// <summary>A square footprint of <paramref name="side"/> cells from (0, 0), every column topped at
    /// <paramref name="top"/>.</summary>
    private static Dictionary<(int X, int Z), int> Square(int side, int top)
    {
        var tops = new Dictionary<(int X, int Z), int>();
        for (var x = 0; x < side; x++)
            for (var z = 0; z < side; z++) tops[(x, z)] = top;
        return tops;
    }

    [Test]
    public async Task A_rim_column_hangs_the_edge_depth_and_each_block_inward_adds_the_taper()
    {
        var floors = Underside.Floors(Square(21, 100), NoSheer, new UndersideSpec { Edge = 4, Taper = 1 }, null, 0, 0);

        await Assert.That(floors[(0, 10)]).IsEqualTo(96);
        await Assert.That(floors[(1, 10)]).IsEqualTo(95);
        await Assert.That(floors[(5, 10)]).IsEqualTo(91);
        await Assert.That(floors[(10, 10)]).IsEqualTo(86);
    }

    [Test]
    public async Task The_taper_deepens_the_centre_of_a_wide_island_past_its_shoulders()
    {
        var floors = Underside.Floors(Square(41, 100), NoSheer, new UndersideSpec(), null, 0, 0);

        await Assert.That(floors[(20, 20)]).IsLessThan(floors[(8, 20)]);
        await Assert.That(floors[(8, 20)]).IsLessThan(floors[(0, 20)]);
    }

    [Test]
    public async Task The_underside_stops_at_course_one_and_never_rises_above_the_course_under_the_top()
    {
        var low = Underside.Floors(Square(41, 12), NoSheer, new UndersideSpec(), null, 0, 0);
        await Assert.That(low.Values.Min()).IsEqualTo(1);

        var none = Underside.Floors(Square(5, 30), NoSheer, new UndersideSpec { Edge = 0, Taper = 0 }, null, 0, 0);
        await Assert.That(none.Values.All(floor => floor == 29)).IsTrue();
    }

    [Test]
    public async Task Each_column_hangs_from_its_own_top()
    {
        var tops = Square(21, 100);
        tops[(10, 10)] = 120;
        var floors = Underside.Floors(tops, NoSheer, new UndersideSpec { Edge = 4, Taper = 1 }, null, 0, 0);

        await Assert.That(floors[(10, 10)]).IsEqualTo(120 - 14);
    }

    /// <summary>The air to the west is the rift: a column beside it reads the sheer depth, while the other
    /// rims keep the ordinary one.</summary>
    [Test]
    public async Task A_sheer_side_drops_far_before_it_narrows()
    {
        var tops = Square(61, 100);
        var rift = Enumerable.Range(0, 61).Select(z => (-1, z)).ToHashSet();
        var spec = new UndersideSpec();
        var floors = Underside.Floors(tops, rift, spec, null, 0, 0);
        var plain = Underside.Floors(tops, NoSheer, spec, null, 0, 0);

        await Assert.That(floors[(0, 30)]).IsEqualTo(100 - 26);
        await Assert.That(floors[(2, 30)]).IsEqualTo(100 - (int)Math.Round(26 + 1.2 * 2));
        await Assert.That(plain[(0, 30)]).IsEqualTo(100 - 4);
        await Assert.That(floors[(60, 30)]).IsEqualTo(plain[(60, 30)]);
    }

    [Test]
    public async Task A_rough_underside_is_the_same_on_every_image_of_a_mirrored_layer()
    {
        var tops = Square(30, 100);
        var spec = new UndersideSpec { Rough = 2, Seed = 5 };
        var floors = Underside.Floors(tops, NoSheer, spec, "rot_180", 15, 15);

        var differs = 0;
        foreach (var ((x, z), floor) in floors)
        {
            await Assert.That(floors[(29 - x, 29 - z)]).IsEqualTo(floor);
            if (floor != Underside.Floors(tops, NoSheer, spec with { Rough = 0 }, "rot_180", 15, 15)[(x, z)]) differs++;
        }
        await Assert.That(differs).IsGreaterThan(0);
    }

    [Test]
    public async Task The_noise_is_not_mirrored_where_the_layer_is_not()
    {
        var tops = Square(30, 100);
        var floors = Underside.Floors(tops, NoSheer, new UndersideSpec { Rough = 3, Seed = 5 }, null, 15, 15);

        await Assert.That(floors.Any(entry => floors[(29 - entry.Key.X, 29 - entry.Key.Z)] != entry.Value)).IsTrue();
    }

    [Test]
    [Arguments(100, 100, true)]
    [Arguments(100, 97, true)]
    [Arguments(100, 96, false)]
    [Arguments(100, 40, false)]
    public async Task An_underside_rests_where_fewer_than_four_blocks_would_stand_clear(int floor, int groundTop, bool rests)
    {
        await Assert.That(Underside.Rests(floor, groundTop)).IsEqualTo(rests);
    }
}
