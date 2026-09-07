using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// Which shape owns a column's paint. Among the shapes covering it, only those reaching its visible top may
/// own it — a shape running <b>under</b> another forms no surface there, and the theme a column wears is the
/// theme of what it shows — and among those the smallest area wins, so two shapes at one height stay a theme
/// scoped to a patch.
///
/// <para>A shape stating a <c>height_mode</c> is outside the height test both ways: it stands in the terrain
/// rather than being it, and the top it settles at is read against ground the relief has not made yet. A
/// role-tagged shape is outside it for the opposite reason — it places no terrain at all, and the ground it
/// annotates is already there — which is what lets a room's own ground be stated.</para>
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

    /// <summary>A structural annotation as the compile writes one: the plan's own piece, locked, placing no
    /// terrain of its own. It states a theme the same way any shape does.</summary>
    private static string Role(string id, string role, string? theme, int minX, int minZ, int maxX, int maxZ,
                               int height = 0) =>
        $@"{{""id"":""{id}"",""type"":""rectangle"",""operation"":""add"",""role"":""{role}"","
        + (theme is null ? "" : $@"""theme"":""{theme}"",")
        + $@"""min_x"":{minX},""min_z"":{minZ},""max_x"":{maxX},""max_z"":{maxZ},"
        + $@"""floor"":0,""base_height"":{height}}}";

    /// <summary>The same board with every terrain shape in one named group — what a compiled layout carries,
    /// and what the group read is asked about.</summary>
    private static string Grouped(string groupId, params string[] shapes) =>
        @"{""setup"":{""mirror_mode"":""none"",""center"":{""cx"":0,""cz"":0}},""layers"":[{""id"":""ground"","
        + $@"""base_y"":0,""layout"":{{""shapes"":[{string.Join(",", shapes)}],""groups"":[{{""id"":""{groupId}"","
        + @"""mirrors"":false,""shapeIds"":[""island""]}]}}]}";

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

    /// <summary>A role-tagged shape — the spawn, the wool room, the building footprint inside one — is the
    /// plan's own piece drawn over terrain the island already holds. It places nothing, so it is read the way
    /// an erected shape is: a candidate for the paint on its cells at any height, never the surface another
    /// shape is measured against. That is what makes a room's ground statable.</summary>
    [Test]
    public async Task A_role_tagged_shape_owns_the_paint_on_the_ground_it_annotates()
    {
        var board = Board(Rect("island", "quartz", -20, -20, 20, 20, height: 20),
                          Role("wool-red", "woolRoom", "sandstone", -6, -6, 6, 6));

        await Assert.That(OwnerAt(board, 0, 0)).IsEqualTo("wool-red");   // the room states its own ground
        await Assert.That(OwnerAt(board, 15, 15)).IsEqualTo("island");   // and nothing outside it moves
    }

    /// <summary>Its own drawn height is never read. A room annotation carries whatever height the compile
    /// wrote onto it — the plan's flat surface, or nothing at all — and that number is not a claim about the
    /// terrain, so it must not decide whether the room's ground is the room's to paint.</summary>
    [Test]
    public async Task A_role_tagged_shape_keeps_its_scope_whatever_height_it_carries()
    {
        foreach (var height in new[] { 0, 4, 40 })
        {
            var board = Board(Rect("island", "quartz", -20, -20, 20, 20, height: 20),
                              Role("spawn-red", "spawn", "sandstone", -6, -6, 6, 6, height));
            await Assert.That(OwnerAt(board, 0, 0)).IsEqualTo("spawn-red");
        }
    }

    /// <summary>An annotation stating no paint states nothing. It is a rectangle the plan drew to keep a piece
    /// visible, and a room that says nothing about its ground leaves it the island's.</summary>
    [Test]
    public async Task A_role_tagged_shape_with_no_theme_owns_nothing()
    {
        var board = Board(Rect("island", "quartz", -20, -20, 20, 20, height: 20),
                          Role("wool-red", "woolRoom", theme: null, -6, -6, 6, 6));

        await Assert.That(OwnerAt(board, 0, 0)).IsEqualTo("island");
    }

    /// <summary>And the group read keeps them out entirely. An annotation belongs to no island's
    /// <c>ShapeIds</c>, so a role shape owning a cell there would drop that cell's group rather than report
    /// it — and the relief a gate speaks about would go silent under every room on the board.</summary>
    [Test]
    public async Task The_group_a_cell_is_solved_under_is_the_islands_under_a_room_too()
    {
        var board = Grouped("isle",
            Rect("island", "quartz", -20, -20, 20, 20, height: 20),
            Role("wool-red", "woolRoom", "sandstone", -6, -6, 6, 6));

        var groups = SketchRasterizer.GroupOwners(board);
        await Assert.That(groups[(0, 0)]).IsEqualTo("isle");       // inside the room
        await Assert.That(groups[(15, 15)]).IsEqualTo("isle");     // and outside it
    }
}
