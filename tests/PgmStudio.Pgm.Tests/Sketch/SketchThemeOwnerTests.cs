using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// Which shape owns a column's paint. Among the shapes covering it, only those reaching its visible top may
/// own it — a shape running <b>under</b> another forms no surface there, and the theme a column wears is the
/// theme of what it shows — and among those the smallest area wins, so two shapes at one height stay a theme
/// scoped to a patch.
///
/// <para>A shape stating a <c>height_mode</c> is outside the height test both ways: it stands in the terrain
/// rather than being it, and the top it settles at is read against ground the relief has not made yet.</para>
/// </summary>
public sealed class SketchThemeOwnerTests
{
    private static string Rect(string id, string theme, int minX, int minZ, int maxX, int maxZ,
                               int height, string? heightMode = null) =>
        $@"{{""id"":""{id}"",""type"":""rectangle"",""operation"":""add"",""override"":true,"
        + $@"""theme"":""{theme}"",""min_x"":{minX},""min_z"":{minZ},""max_x"":{maxX},""max_z"":{maxZ},"
        + $@"""floor"":0,""base_height"":{height}"
        + (heightMode is null ? "" : $@",""height_mode"":""{heightMode}"",""skirt"":0") + "}";

    private static string Board(params string[] shapes) =>
        @"{""setup"":{""mirror_mode"":""none"",""center"":{""cx"":0,""cz"":0}},""layers"":[{""id"":""ground"","
        + $@"""base_y"":0,""layout"":{{""shapes"":[{string.Join(",", shapes)}],""groups"":[]}}}}]}}";

    private static string? OwnerAt(string board, int x, int z) =>
        SketchRasterizer.ShapeThemeOwners(board).TryGetValue(("ground", x, z), out var id) ? id : null;

    [Test]
    public async Task A_column_wears_the_theme_of_the_shape_that_forms_its_surface()
    {
        // The documented way to give a tier an organic edge is to let the tier below run under it. The lower
        // tier is the smaller shape, so by area alone it would keep its paint over ground it does not form.
        var board = Board(Rect("shelf", "quartz", -20, -20, 20, 20, height: 20),
                          Rect("under", "sandstone", -6, -6, 6, 6, height: 8));

        await Assert.That(OwnerAt(board, 0, 0)).IsEqualTo("shelf");     // inside both: the shelf forms the top
        await Assert.That(OwnerAt(board, 15, 15)).IsEqualTo("shelf");   // the shelf alone
    }

    [Test]
    public async Task Two_shapes_at_one_height_are_a_patch_and_the_smaller_is_the_scope()
    {
        // Nothing about patch scoping moves: where the two reach the same top, the more specific scope wins.
        var board = Board(Rect("shelf", "quartz", -20, -20, 20, 20, height: 20),
                          Rect("patch", "sandstone", -6, -6, 6, 6, height: 20));

        await Assert.That(OwnerAt(board, 0, 0)).IsEqualTo("patch");
        await Assert.That(OwnerAt(board, 15, 15)).IsEqualTo("shelf");
    }

    [Test]
    public async Task A_shape_standing_taller_than_the_ground_takes_the_paint_of_what_it_covers()
    {
        // The other way round: the smaller shape is also the taller, so it forms the surface and owns it.
        var board = Board(Rect("shelf", "quartz", -20, -20, 20, 20, height: 8),
                          Rect("plinth", "sandstone", -6, -6, 6, 6, height: 20));

        await Assert.That(OwnerAt(board, 0, 0)).IsEqualTo("plinth");
    }

    [Test]
    public async Task A_shape_that_states_how_its_top_is_decided_is_not_measured_here()
    {
        // A wall, a mesa, a flight: what it settles at is read against ground the relief has not made when
        // the owners are resolved, so its drawn thickness is not a height to rank it by.
        var board = Board(Rect("shelf", "quartz", -20, -20, 20, 20, height: 20),
                          Rect("wall", "brick", -6, -6, 6, 6, height: 4, heightMode: "level"));

        await Assert.That(OwnerAt(board, 0, 0)).IsEqualTo("wall");
    }
}
