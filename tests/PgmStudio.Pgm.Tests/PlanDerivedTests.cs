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
    public async Task A_zone_never_gap_links_pieces_of_the_same_land_component()
    {
        // a and b abut (land, border 10) and BOTH touch the zone below them; walkably-connected pieces
        // need no void crossing, so the zone yields no a–b gap link. c is a separate island the same zone
        // also touches → the cross-component links to it remain.
        var plan = PlanModel.Parse("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]},
                     {"id":"b","role":"lane","rect":[10,0,10,10]},
                     {"id":"c","role":"mid","rect":[0,25,10,10]} ],
          "zones":[ {"id":"z","rect":[0,10,20,15]} ] }
        """)!;
        var d = PlanDerived.Build(plan);
        await Assert.That(d.GapLinks.Any(g => g.A is "a" or "b" && g.B is "a" or "b")).IsFalse();
        await Assert.That(d.GapLinks.Any(g => (g.A, g.B) is ("a", "c") or ("c", "a"))).IsTrue();
        await Assert.That(d.GapLinks.Any(g => (g.A, g.B) is ("b", "c") or ("c", "b"))).IsTrue();
    }

    [Test]
    public async Task A_zone_that_only_abuts_piece_fronts_bridges_nothing()
    {
        // a and b are disjoint (a 10-block void between them); the zone sits BELOW, abutting the bottom edge of
        // each. Their nearest connecting span runs above the zone (outside it), so the zone links neither.
        var plan = PlanModel.Parse("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]},
                     {"id":"b","role":"lane","rect":[20,0,10,10]} ],
          "zones":[ {"id":"z","rect":[0,10,30,10]} ] }
        """)!;
        var d = PlanDerived.Build(plan);
        await Assert.That(d.GapLinks).IsEmpty();
    }

    [Test]
    public async Task A_zone_spanning_the_void_between_two_pieces_still_links_them()
    {
        // the zone fills the 10-block void between a and b; their nearest span runs THROUGH it → one 10-hop link.
        var plan = PlanModel.Parse("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]},
                     {"id":"b","role":"lane","rect":[20,0,10,10]} ],
          "zones":[ {"id":"z","rect":[10,0,10,10]} ] }
        """)!;
        var d = PlanDerived.Build(plan);
        await Assert.That(d.GapLinks.Count).IsEqualTo(1);
        await Assert.That(d.GapLinks[0].Hop).IsEqualTo(10);
    }

    [Test]
    public async Task A_gap_link_whose_span_crosses_a_third_pieces_interior_is_suppressed()
    {
        // a and c face each other across a wide zone, but b sits in the middle of that void — the a–c span runs
        // over b's interior, so a and c are not linked (a–b and b–c, each spanning their own clear void, are).
        var plan = PlanModel.Parse("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10]},
                     {"id":"b","role":"mid","rect":[20,0,10,10]},
                     {"id":"c","role":"lane","rect":[40,0,10,10]} ],
          "zones":[ {"id":"z","rect":[10,0,30,10]} ] }
        """)!;
        var d = PlanDerived.Build(plan);
        await Assert.That(d.GapLinks.Any(g => (g.A, g.B) is ("a", "c") or ("c", "a"))).IsFalse();
        await Assert.That(d.GapLinks.Any(g => (g.A, g.B) is ("a", "b") or ("b", "a"))).IsTrue();
        await Assert.That(d.GapLinks.Any(g => (g.A, g.B) is ("b", "c") or ("c", "b"))).IsTrue();
    }

    [Test]
    public async Task Isolated_spawn_seed_keeps_only_the_two_bridge_gap_links()
    {
        // the author's case: a spawn island abutting several lane fronts. Only the two zones that truly span the
        // void from the central island out to lane-4 / lane-5 (10 blocks each) survive; the abutting mid band's
        // out-of-zone spans (the two 15s) and the cross-map 40 are dropped.
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("isolated-spawn.plan.json"))!;
        var d = PlanDerived.Build(plan);
        await Assert.That(d.GapLinks.Count).IsEqualTo(2);
        await Assert.That(d.GapLinks.All(g => g.Hop == 10)).IsTrue();
        var partners = d.GapLinks.SelectMany(g => new[] { g.A, g.B }).ToHashSet();
        await Assert.That(partners).Contains("lane-4");
        await Assert.That(partners).Contains("lane-5");
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
    public async Task A_wall_mark_resolves_to_its_land_interface_and_flags_the_segment()
    {
        // a and b abut over a 10-block land border; a wall mark on the pair → one wall interface + a flagged segment.
        var plan = PlanModel.Parse("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]}, {"id":"b","role":"piece","rect":[10,0,10,10]} ],
          "walls":[ {"a":"b","b":"a"} ] }
        """)!;
        var d = PlanDerived.Build(plan);
        await Assert.That(d.WallInterfaces.Count).IsEqualTo(1);
        var seg = d.InterfaceSegments.Single(s => s.Kind == ContactKind.Land);
        await Assert.That(seg.Wall).IsTrue();
        await Assert.That(seg.WoolRoom).IsFalse();
    }

    [Test]
    public async Task A_terrain_to_wool_room_land_seam_is_flagged_wool_room()
    {
        // a (piece) abuts b (wool-room) → the seam flags woolRoom; a wool-room↔wool-room seam does not.
        var plan = PlanModel.Parse("""
        { "plan":1, "globals":{"cell":1},
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,10,10]},
                     {"id":"b","role":"wool-room","rect":[10,0,10,10]},
                     {"id":"c","role":"wool-room","rect":[20,0,10,10]} ] }
        """)!;
        var d = PlanDerived.Build(plan);
        var ab = d.InterfaceSegments.Single(s => (s.A, s.B) is ("a", "b") or ("b", "a"));
        var bc = d.InterfaceSegments.Single(s => (s.A, s.B) is ("b", "c") or ("c", "b"));
        await Assert.That(ab.WoolRoom).IsTrue();
        await Assert.That(bc.WoolRoom).IsFalse();   // both sides are rooms → not a terrain seam
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
