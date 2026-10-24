using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// What a layer's <c>underside</c> does to the columns the rasterizer hands every reader: the floor of each
/// column drops from the flat slab to a rock that thickens inward from the rim, and rests on the terrain below
/// where less than four blocks would stand clear of it. A layer that states none builds exactly as before.
/// </summary>
public sealed class SketchUndersideTests
{
    private static string Slab(string id, int minX, int minZ, int maxX, int maxZ, int height, string extra = "") =>
        $@"{{""id"":""{id}"",""type"":""rectangle"",""operation"":""add"",""min_x"":{minX},""min_z"":{minZ},"
        + $@"""max_x"":{maxX},""max_z"":{maxZ},""base_height"":{height}{extra}}}";

    private static string Layer(string id, int baseY, string shapes, string? underside = null) =>
        $@"{{""id"":""{id}"",""base_y"":{baseY},"
        + (underside is null ? "" : $@"""underside"":{underside},")
        + $@"""layout"":{{""shapes"":[{shapes}],""groups"":[]}}}}";

    private static List<Geom.ColumnSegment> Build(string mirror, params string[] layers) =>
        SketchRasterizer.RasterizeColumns(SketchLayout.Parse(
            $@"{{""setup"":{{""mirror_mode"":""{mirror}"",""center"":{{""cx"":0,""cz"":0}}}},""layers"":[{string.Join(",", layers)}]}}"));

    private static Geom.ColumnSegment At(List<Geom.ColumnSegment> columns, string layer, int x, int z) =>
        columns.Single(segment => segment.Layer == layer && segment.X == x && segment.Z == z);

    [Test]
    public async Task A_layer_over_void_hangs_four_blocks_at_the_rim_and_deeper_inward()
    {
        var columns = Build("none", Layer("island", 40, Slab("a", 0, 0, 21, 21, 1), @"{""taper"":1}"));

        await Assert.That(At(columns, "island", 0, 10).YTop).IsEqualTo(41);
        await Assert.That(At(columns, "island", 0, 10).YFloor).IsEqualTo(37);
        await Assert.That(At(columns, "island", 3, 10).YFloor).IsEqualTo(34);
        await Assert.That(At(columns, "island", 10, 10).YFloor).IsEqualTo(41 - 14);
    }

    [Test]
    public async Task A_layer_that_states_no_underside_is_the_flat_slab_it_always_was()
    {
        var columns = Build("none", Layer("island", 40, Slab("a", 0, 0, 21, 21, 1)));

        await Assert.That(columns.All(segment => segment.YFloor == 40 && segment.YTop == 41)).IsTrue();
    }

    [Test]
    public async Task A_slab_thicker_than_its_underside_keeps_its_own_floor()
    {
        var columns = Build("none", Layer("island", 40, Slab("a", 0, 0, 21, 21, 8), "{}"));

        await Assert.That(At(columns, "island", 0, 10).YFloor).IsEqualTo(40);
        await Assert.That(At(columns, "island", 10, 10).YFloor).IsLessThan(40);
    }

    [Test]
    public async Task An_underside_that_comes_near_the_terrain_below_rests_on_it_and_one_that_does_not_hangs_free()
    {
        var columns = Build("none",
            Layer("ground", 0, Slab("g", 0, 0, 21, 21, 30)),
            Layer("island", 40, Slab("a", 0, 0, 21, 21, 1), @"{""taper"":1}"));

        await Assert.That(At(columns, "island", 0, 10).YFloor).IsEqualTo(37);
        await Assert.That(At(columns, "island", 10, 10).YFloor).IsEqualTo(30);
        await Assert.That(At(columns, "ground", 10, 10).YTop).IsEqualTo(30);
    }

    [Test]
    public async Task A_made_thing_below_is_not_ground_to_rest_on()
    {
        var made = Layer("statue", 0, Slab("g", 0, 0, 21, 21, 30)).Replace(@"""base_y"":0,", @"""base_y"":0,""kind"":""made"",");
        var columns = Build("none", made, Layer("island", 40, Slab("a", 0, 0, 21, 21, 1), @"{""taper"":1}"));

        await Assert.That(At(columns, "island", 10, 10).YFloor).IsEqualTo(27);
    }

    [Test]
    public async Task A_mirrored_rough_underside_hangs_the_same_on_both_images()
    {
        var columns = Build("rot_180", Layer("island", 40, Slab("a", 5, 5, 30, 30, 1), @"{""rough"":2.5,""seed"":3}"));

        var mismatched = 0;
        foreach (var segment in columns)
            if (At(columns, "island", -1 - segment.X, -1 - segment.Z).YFloor != segment.YFloor) mismatched++;
        await Assert.That(mismatched).IsEqualTo(0);
        await Assert.That(columns.Select(segment => segment.YFloor).Distinct().Count()).IsGreaterThan(3);
    }

    [Test]
    public async Task A_named_shape_makes_the_air_beside_it_a_sheer_side()
    {
        var shapes = Slab("rift", 0, 0, 3, 61, 1) + "," + Slab("body", 3, 0, 61, 61, 1);
        var plain = Build("none", Layer("island", 80, shapes, "{}"));
        var sheer = Build("none", Layer("island", 80, shapes, @"{""sheer"":[""rift""]}"));

        await Assert.That(At(plain, "island", 0, 30).YFloor).IsEqualTo(81 - 4);
        await Assert.That(At(sheer, "island", 0, 30).YFloor).IsEqualTo(81 - 26);
        await Assert.That(At(sheer, "island", 60, 30).YFloor).IsEqualTo(At(plain, "island", 60, 30).YFloor);
    }
}
