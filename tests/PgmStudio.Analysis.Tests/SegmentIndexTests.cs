using PgmStudio.Analysis.Layer;
using PgmStudio.Geom;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// Where a player stands in a column, which is the set every walk and the export's own traversability gate
/// run over. The cases here are the three shapes a board actually builds — open terrain, a tree, a house —
/// plus the two a column can take that offer nowhere to stand at all.
/// </summary>
public sealed class SegmentIndexTests
{
    private static SegmentIndex Of(params (int x, int z, int ys, int ye)[] segments) => new(segments);

    private static int? Standing(SegmentIndex index, int x, int z)
        => index.StandingTops().Where(row => row.x == x && row.z == z)
            .Select(row => (int?)row.top).FirstOrDefault();

    [Test]
    public async Task Open_terrain_stands_on_its_own_surface()
    {
        var index = Of((0, 0, 0, 18));
        await Assert.That(Standing(index, 0, 0)).IsEqualTo(19);
    }

    [Test]
    public async Task A_wooded_column_stands_on_the_terrain_and_not_on_its_canopy()
    {
        // Terrain to y18, air, leaves at y24-27. Reading the highest surface would put the player on the
        // canopy and make every tree on a board a climb worth going round.
        var index = Of((0, 0, 0, 18), (0, 0, 24, 27));
        await Assert.That(Standing(index, 0, 0)).IsEqualTo(19);
    }

    [Test]
    public async Task A_walled_column_stands_on_the_wall_and_not_on_the_floor_it_buries()
    {
        // The byre's shape, read off elderwold-10 at (26, 37): terrain to y18, a wall standing y19-25 on
        // top of it, an eave at y27 and a roof course at y28. The lowest surface is the terrain's and it
        // carries no headroom because the wall is on it; the wall's own top is boxed in by the eave. So the
        // column stands at the roof, and a walk over it pays the climb rather than crossing as if the
        // building were not there.
        var index = Of((0, 0, 0, 18), (0, 0, 19, 25), (0, 0, 27, 27), (0, 0, 28, 28));
        await Assert.That(Standing(index, 0, 0)).IsEqualTo(29);
    }

    [Test]
    public async Task A_wall_top_with_room_over_it_is_somewhere_to_stand()
    {
        // The same wall with no eave boxing it in: a player can stand on it, and the walk should say so
        // rather than sending them to the ridge.
        var index = Of((0, 0, 0, 18), (0, 0, 19, 25), (0, 0, 28, 28));
        await Assert.That(Standing(index, 0, 0)).IsEqualTo(26);
    }

    [Test]
    public async Task A_room_under_a_roof_stands_on_its_floor()
    {
        // The same building one cell further in: floor course at y19, roof at y28, eight clear between them.
        // Standing inside a house is a thing a player does, and the walls are what decide whether they can
        // get there.
        var index = Of((0, 0, 0, 19), (0, 0, 28, 28));
        await Assert.That(Standing(index, 0, 0)).IsEqualTo(20);
    }

    [Test]
    public async Task A_crawl_space_is_not_somewhere_to_stand()
    {
        var index = Of((0, 0, 0, 10), (0, 0, 12, 20));
        await Assert.That(Standing(index, 0, 0)).IsEqualTo(21);
    }

    [Test]
    public async Task A_column_solid_to_the_sky_is_not_a_standing_column()
    {
        var index = Of((0, 0, 0, 255));
        await Assert.That(Standing(index, 0, 0)).IsNull();
        await Assert.That(index.StandingColumns()).DoesNotContain((0, 0));
    }

    [Test]
    public async Task Standing_columns_are_exactly_the_columns_with_a_standing_surface()
    {
        var index = Of((0, 0, 0, 18), (1, 0, 0, 255), (2, 0, 0, 4), (2, 0, 6, 255));
        await Assert.That(index.StandingColumns()).IsEquivalentTo(new HashSet<(int, int)> { (0, 0) });
    }

    [Test]
    public async Task The_headroom_a_surface_needs_is_the_one_the_walk_states()
    {
        // A gap of exactly Headroom is standable; one block less is not — asserted against the constant so
        // the three masks that read it cannot drift apart silently.
        var enough = Of((0, 0, 0, 10), (0, 0, 11 + Walk.Headroom, 20));
        var tooLittle = Of((0, 0, 0, 10), (0, 0, 10 + Walk.Headroom, 20));

        await Assert.That(Standing(enough, 0, 0)).IsEqualTo(11);
        await Assert.That(Standing(tooLittle, 0, 0)).IsEqualTo(21);
    }
}
