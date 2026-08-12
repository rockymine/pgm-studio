using PgmStudio.Api.Services;
using PgmStudio.Geom.Relief;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The head start a relief preview resumes from. Everything here is about what the cache must <b>refuse</b>
/// to hand back: a surface it can offer is only ever a saving, and a surface it should not offer is a slower
/// solve at best. It can never change an answer — the solver discards a resume that fails to settle — so the
/// properties worth holding are the ones that keep it useful and bounded.
/// </summary>
public sealed class ReliefPreviewCacheTests
{
    private static HeightField Field(int minX, int minZ, int width, int depth, double height = 7)
    {
        var footprint = new Footprint(minX, minZ, width, depth);
        footprint.Add(Enumerable.Range(minX, width).SelectMany(x => Enumerable.Range(minZ, depth).Select(z => (x, z))));
        var continuous = new double[footprint.Cells];
        Array.Fill(continuous, height);
        return new HeightField(footprint, continuous, new int[footprint.Cells]);
    }

    [Test]
    public async Task A_remembered_surface_comes_back_for_the_same_island()
    {
        var cache = new ReliefPreviewCache();
        var field = Field(0, 0, 20, 20, 9);
        cache.Remember(1, "i1", field);

        await Assert.That(cache.WarmStart(1, "i1", field.Footprint)).IsEqualTo(field.Continuous);
    }

    [Test]
    public async Task Nothing_comes_back_for_an_island_or_a_map_that_never_solved()
    {
        var cache = new ReliefPreviewCache();
        var field = Field(0, 0, 20, 20);
        cache.Remember(1, "i1", field);

        await Assert.That(cache.WarmStart(1, "i2", field.Footprint)).IsNull();
        await Assert.That(cache.WarmStart(2, "i1", field.Footprint)).IsNull();
    }

    [Test]
    public async Task A_surface_from_a_different_grid_is_refused_rather_than_reused()
    {
        // A field is an array indexed by the grid it was solved on. One from a different grid is not a worse
        // starting point but a different picture: the cell it would seed each position with is unrelated to
        // that position, so the solve starts further from the answer than flat ground would.
        var cache = new ReliefPreviewCache();
        cache.Remember(1, "i1", Field(0, 0, 20, 20));

        await Assert.That(cache.WarmStart(1, "i1", Field(0, 0, 21, 20).Footprint)).IsNull();   // grew
        await Assert.That(cache.WarmStart(1, "i1", Field(4, 0, 20, 20).Footprint)).IsNull();   // moved
        await Assert.That(cache.WarmStart(1, "i1", Field(0, 0, 20, 19).Footprint)).IsNull();   // shrank
    }

    [Test]
    public async Task Remembering_the_same_island_again_replaces_what_it_held()
    {
        var cache = new ReliefPreviewCache();
        var footprint = Field(0, 0, 12, 12).Footprint;
        cache.Remember(1, "i1", Field(0, 0, 12, 12, 5));
        cache.Remember(1, "i1", Field(0, 0, 12, 12, 14));

        await Assert.That(cache.WarmStart(1, "i1", footprint)![0]).IsEqualTo(14);
    }

    [Test]
    public async Task The_least_recently_used_surfaces_are_the_ones_dropped()
    {
        // Bounded, so a long session cannot grow it without limit — and bounded by USE, so the island an
        // author is working on is the last thing evicted rather than an arbitrary one.
        var cache = new ReliefPreviewCache();
        var footprint = Field(0, 0, 8, 8).Footprint;
        for (var map = 0; map < 70; map++) cache.Remember(map, "i1", Field(0, 0, 8, 8, map));

        // The first entries are gone; the most recent survive.
        await Assert.That(cache.WarmStart(0, "i1", footprint)).IsNull();
        await Assert.That(cache.WarmStart(69, "i1", footprint)).IsNotNull();
    }

    [Test]
    public async Task Reading_a_surface_keeps_it_alive()
    {
        // The cheapest way to get eviction wrong is to age entries by when they were written: an island being
        // edited is written once and read on every preview after, which is exactly the entry worth keeping.
        var cache = new ReliefPreviewCache();
        var footprint = Field(0, 0, 8, 8).Footprint;
        cache.Remember(1, "kept", Field(0, 0, 8, 8, 3));

        for (var map = 100; map < 160; map++)
        {
            cache.Remember(map, "i1", Field(0, 0, 8, 8, map));
            cache.WarmStart(1, "kept", footprint);          // still in use
        }

        await Assert.That(cache.WarmStart(1, "kept", footprint)).IsNotNull();
    }
}
