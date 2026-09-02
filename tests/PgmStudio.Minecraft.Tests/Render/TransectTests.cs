using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>A polyline walked block by block over a synthetic surface — the read for a claim about a shape,
/// which a single column cannot answer.</summary>
public sealed class TransectTests
{
    private static Dictionary<(int X, int Z), int> FlatSurface(int from, int to, int z, int height)
    {
        var surface = new Dictionary<(int X, int Z), int>();
        for (var x = from; x <= to; x++) surface[(x, z)] = height;
        return surface;
    }

    [Test]
    public async Task A_flat_floor_is_walked_end_to_end_with_zero_events()
    {
        var surface = FlatSurface(0, 9, 0, 5);
        var walked = Transect.Walk(new VoxelWorld(), new WorldProvenance(), surface, columns: null,
            [(0, 0), (9, 0)], every: 1, beside: 0);

        await Assert.That(walked.Stations.Count).IsEqualTo(10);
        await Assert.That(walked.Events.Count).IsEqualTo(0);
        await Assert.That(walked.Barriers).IsEqualTo(0);
        await Assert.That(walked.Drops).IsEqualTo(0);

        var text = Transect.Render(walked, [(0, 0), (9, 0)], every: 1, beside: 0);
        await Assert.That(text).Contains("walked end to end");
    }

    [Test]
    public async Task A_step_of_eight_yields_one_barrier_event_with_its_coordinates()
    {
        var surface = FlatSurface(0, 4, 0, 5);
        foreach (var x in Enumerable.Range(5, 5)) surface[(x, 0)] = 13;   // +8 at x=5

        var walked = Transect.Walk(new VoxelWorld(), new WorldProvenance(), surface, columns: null,
            [(0, 0), (9, 0)], every: 1, beside: 0);

        await Assert.That(walked.Barriers).IsEqualTo(1);
        await Assert.That(walked.WorstStep).IsEqualTo(8);
        await Assert.That(walked.Events).IsEquivalentTo(["BARRIER +8 at (5, 0)"]);
    }

    [Test]
    public async Task A_step_of_minus_five_yields_a_drop()
    {
        var surface = FlatSurface(0, 4, 0, 5);
        foreach (var x in Enumerable.Range(5, 5)) surface[(x, 0)] = 0;   // -5 at x=5

        var walked = Transect.Walk(new VoxelWorld(), new WorldProvenance(), surface, columns: null,
            [(0, 0), (9, 0)], every: 1, beside: 0);

        await Assert.That(walked.Drops).IsEqualTo(1);
        await Assert.That(walked.Events).IsEquivalentTo(["DROP -5 at (5, 0)"]);
    }

    [Test]
    public async Task Every_two_halves_the_stations()
    {
        var surface = FlatSurface(0, 9, 0, 5);
        var every1 = Transect.Walk(new VoxelWorld(), new WorldProvenance(), surface, null, [(0, 0), (9, 0)], 1, 0);
        var every2 = Transect.Walk(new VoxelWorld(), new WorldProvenance(), surface, null, [(0, 0), (9, 0)], 2, 0);

        await Assert.That(every1.Stations.Count).IsEqualTo(10);
        await Assert.That(every2.Stations.Count).IsEqualTo(every1.Stations.Count / 2);
    }

    [Test]
    public async Task Beside_lists_an_owner_within_reach_and_not_one_further_off()
    {
        var surface = FlatSurface(0, 5, 0, 5);
        var provenance = new WorldProvenance();
        provenance.Claim(2, 2, ProvenancePass.Structure, new StampId("house", "near", 0));   // 2 cells off (2,0)
        provenance.Claim(2, 3, ProvenancePass.Structure, new StampId("house", "far", 0));    // 3 cells off

        var walked = Transect.Walk(new VoxelWorld(), provenance, surface, null, [(0, 0), (5, 0)],
            every: 1, beside: 2);

        await Assert.That(walked.Beside.Select(n => n.Unit)).Contains("near");
        await Assert.That(walked.Beside.Select(n => n.Unit)).DoesNotContain("far");
    }

    [Test]
    public async Task Beside_lists_nothing_when_nobody_asked()
    {
        var surface = FlatSurface(0, 5, 0, 5);
        var provenance = new WorldProvenance();
        provenance.Claim(2, 0, ProvenancePass.Structure, new StampId("house", "onthenose", 0));

        var walked = Transect.Walk(new VoxelWorld(), provenance, surface, null, [(0, 0), (5, 0)],
            every: 1, beside: 0);

        await Assert.That(walked.Beside.Count).IsEqualTo(0);
    }
}
