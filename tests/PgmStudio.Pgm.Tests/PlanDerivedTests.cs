using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// Derived structure: how two pieces meet (land / sliver / corner / overlap), connected components over land
/// interfaces, gap links across build zones, and the computed frontline. Edge cases centre on the corridor
/// minimum: an exact-10 border connects, a 9-block border slivers, a bare corner touch is neither.
/// </summary>
public sealed class PlanDerivedTests
{
    private static DerivedPiece P(string id, int minX, int minZ, int maxX, int maxZ, int surface = 9) =>
        new(id, "lane", new BlockRect(minX, minZ, maxX, maxZ), surface, true);

    [Test]
    public async Task Exact_ten_block_shared_border_is_a_land_interface()
    {
        var c = PlanDerived.Classify(P("a", 0, 0, 10, 10), P("b", 10, 0, 20, 10));
        await Assert.That(c.Kind).IsEqualTo(ContactKind.Land);
        await Assert.That(c.BorderLength).IsEqualTo(10);
    }

    [Test]
    public async Task A_nine_block_border_is_a_sliver()
    {
        var c = PlanDerived.Classify(P("a", 0, 0, 10, 9), P("b", 10, 0, 20, 10));
        await Assert.That(c.Kind).IsEqualTo(ContactKind.Sliver);
        await Assert.That(c.BorderLength).IsEqualTo(9);
    }

    [Test]
    public async Task A_bare_corner_touch_is_a_corner_contact()
    {
        var c = PlanDerived.Classify(P("a", 0, 0, 10, 10), P("b", 10, 10, 20, 20));
        await Assert.That(c.Kind).IsEqualTo(ContactKind.Corner);
    }

    [Test]
    public async Task Separated_pieces_do_not_contact()
    {
        var c = PlanDerived.Classify(P("a", 0, 0, 10, 10), P("b", 20, 0, 30, 10));
        await Assert.That(c.Kind).IsEqualTo(ContactKind.None);
    }

    [Test]
    public async Task Area_overlap_carries_the_surface_delta()
    {
        var c = PlanDerived.Classify(P("a", 0, 0, 10, 10, 9), P("b", 5, 5, 15, 15, 13));
        await Assert.That(c.Kind).IsEqualTo(ContactKind.Overlap);
        await Assert.That(c.SurfaceDelta).IsEqualTo(4);
    }

    [Test]
    public async Task The_h_bars_form_one_component_the_square_another()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("base-2island.plan.json"))!;
        var d = PlanDerived.Build(plan);
        await Assert.That(d.Components.Count).IsEqualTo(2);              // H (three bars) + square
        var big = d.Components.OrderByDescending(c => c.Count).First();
        await Assert.That(big.Count).IsEqualTo(3);
        await Assert.That(big).Contains("bar-e");
        await Assert.That(big).Contains("cross");
        await Assert.That(big).Contains("bar-w");
    }

    [Test]
    public async Task A_build_zone_gap_links_the_pieces_it_touches()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("base-2wool.plan.json"))!;
        var d = PlanDerived.Build(plan);
        // the bridge zone links the east bar to the wool room across a 10-block void
        var bridge = d.GapLinks.Where(g => g.Zone == "bridge-e").ToList();
        await Assert.That(bridge.Any(g =>
            (g.A == "bar-e" && g.B == "wl2-a") || (g.A == "wl2-a" && g.B == "bar-e"))).IsTrue();
        var hop = bridge.First(g => g.A is "bar-e" or "wl2-a" && g.B is "bar-e" or "wl2-a").Hop;
        await Assert.That(hop).IsEqualTo(10);
    }

    [Test]
    public async Task Frontline_is_the_pieces_abutting_a_zone()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("base-2island.plan.json"))!;
        var d = PlanDerived.Build(plan);
        await Assert.That(d.Frontline).Contains("bar-e");              // spawn lane abuts the mid band
        await Assert.That(d.Frontline).Contains("bar-w");              // wool lane overlaps the mid band
    }

    // ── overlay geometry (block-space segments) ─────────────────────────────────────────────────────────

    [Test]
    public async Task Border_segment_of_an_x_abutting_pair_runs_along_the_shared_edge()
    {
        // a: [0,0]-[10,10], b: [10,0]-[20,10] touch on the vertical line x=10 over z∈[0,10].
        var seg = PlanDerived.BorderSegment(new BlockRect(0, 0, 10, 10), new BlockRect(10, 0, 20, 10));
        await Assert.That(seg).IsEqualTo((10, 0, 10, 10));
    }

    [Test]
    public async Task Border_segment_of_a_corner_touch_is_the_single_point()
    {
        var seg = PlanDerived.BorderSegment(new BlockRect(0, 0, 10, 10), new BlockRect(10, 10, 20, 20));
        await Assert.That(seg).IsEqualTo((10, 10, 10, 10));
    }

    [Test]
    public async Task Interface_segments_expose_land_and_sliver_contacts()
    {
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("base-2island.plan.json"))!;
        var d = PlanDerived.Build(plan);
        // the H-bars connect via land interfaces → at least one land segment carrying the border length
        var land = d.InterfaceSegments.Where(s => s.Kind == ContactKind.Land).ToList();
        await Assert.That(land).IsNotEmpty();
        await Assert.That(land.All(s => s.Length >= PlanDerived.CorridorMin)).IsTrue();
    }

    [Test]
    public async Task Nearest_segment_connects_the_confronting_edges_across_a_gap()
    {
        // a right edge at x=10, b left edge at x=20; they share z∈[0,10] → mid z = 5 on both ends.
        var seg = PlanDerived.NearestSegment(new BlockRect(0, 0, 10, 10), new BlockRect(20, 0, 30, 10));
        await Assert.That(seg).IsEqualTo((10, 5, 20, 5));
    }

    [Test]
    public async Task Frontline_edges_face_the_zone_they_abut()
    {
        // lane sits above a zone, abutting on z=10; its facing edge is the bottom edge z=10.
        var plan = PlanModel.Parse("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"lane","role":"lane","rect":[0,0,20,10]} ],
          "zones":[ {"id":"z","rect":[0,10,20,10]} ] }
        """)!;
        var d = PlanDerived.Build(plan);
        var edges = d.FrontlineEdges.Where(e => e.Piece == "lane").ToList();
        await Assert.That(edges).IsNotEmpty();
        await Assert.That(edges.Any(e => e.Z1 == 10 && e.Z2 == 10)).IsTrue();
    }
}
