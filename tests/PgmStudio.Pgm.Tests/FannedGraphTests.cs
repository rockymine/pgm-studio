using PgmStudio.Domain;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The fanned reachability board. Its gap connectivity runs over buildable REGIONS (zones merged by overlap or
/// shared edge), not single zones, so a crossing may chain through a row of adjacent zones without landing on
/// terrain. A tiled void connects its two ends; a broken chain (a missing link) does not.
///
/// <para>Its <b>land</b> connectivity is the rect layer's, asked of fanned rects — so the overlap cases below
/// assert the agreement rather than a second rule, and a plan cannot answer one thing to a component read and
/// another to a reachability one.</para>
/// </summary>
public sealed class FannedGraphTests
{
    // A single-team query still works on the fanned board: rot_180 doubles every node, but a team-0 → team-0
    // reachability question is answered by the team-0 pieces + the region they share.
    private static bool Reaches(string json, string fromPiece, string toPiece)
    {
        var d = ContactGraph.Build(PlanModel.Parse(json)!);
        var graph = FannedGraph.Build(d);
        return graph.Reachable(new[] { (0, fromPiece) }, (0, toPiece));
    }

    [Test]
    public async Task Three_zones_in_a_row_span_a_wide_void_so_the_ends_are_reachable()
    {
        // L and R sit 30 blocks apart with no land between; three edge-adjacent zones tile the void into one
        // buildable region touching both, so a builder chains across it — L reaches R.
        const string plan = """
        { "plan":2, "globals":{"cell":1,"symmetry":"rot_180"},
          "pieces":[ {"id":"L","role":"lane","rect":[0,0,10,10]}, {"id":"R","role":"lane","rect":[40,0,10,10]} ],
          "zones":[ {"id":"z1","rect":[10,0,10,10]}, {"id":"z2","rect":[20,0,10,10]}, {"id":"z3","rect":[30,0,10,10]} ] }
        """;
        await Assert.That(Reaches(plan, "L", "R")).IsTrue();
    }

    [Test]
    public async Task A_broken_chain_leaves_the_far_end_unreachable()
    {
        // the middle zone is gone: the void is not tiled, the two zones form separate regions each touching one
        // piece, and there is no land bridge → L cannot reach R.
        const string plan = """
        { "plan":2, "globals":{"cell":1,"symmetry":"rot_180"},
          "pieces":[ {"id":"L","role":"lane","rect":[0,0,10,10]}, {"id":"R","role":"lane","rect":[40,0,10,10]} ],
          "zones":[ {"id":"z1","rect":[10,0,10,10]}, {"id":"z3","rect":[30,0,10,10]} ] }
        """;
        await Assert.That(Reaches(plan, "L", "R")).IsFalse();
    }

    [Test]
    public async Task The_pinwheel_seed_centre_chains_every_cross_team_wool_reachable()
    {
        // four-team-towers-big: the rot_90 pinwheel's centre zones from adjacent teams overlap/adjoin, forming
        // shared buildable regions that bridge one team's frontline to the next. With region-union connectivity
        // every cross-team wool is reachable (the per-zone model wrongly reported all 24 unreachable).
        var plan = PlanModel.Parse(PlanTestSupport.ReadSeed("four-team-towers-big.plan.json"))!;
        var reachErrors = PlanValidator.Check(plan)
            .Count(f => f.Severity == Severity.Refusal && f.Message.Contains("unreachable"));
        await Assert.That(reachErrors).IsEqualTo(0);
    }

    // ── land edges: the rule the rect layer states ──────────────────────────────────────────────────────
    // Two pieces overlapping by two blocks in x, at the surfaces given. `none` symmetry holds the fan to one
    // image, so what is asserted is the land edge itself and not an orbit copy of it.
    private static string Overlapping(int surfaceA, int surfaceB) => $$"""
    { "plan":2, "globals":{"cell":1,"symmetry":"none","surface":0},
      "pieces":[ {"id":"a","role":"lane","rect":[0,0,10,10],"surface":{{surfaceA}}},
                 {"id":"b","role":"lane","rect":[8,0,10,10],"surface":{{surfaceB}}} ] }
    """;

    /// <summary>Two pieces claiming the same ground at the same height are one landmass: the shared cells have
    /// a surface both agree on, so a player walks across them.</summary>
    [Test]
    public async Task An_overlap_at_one_height_is_one_landmass()
    {
        await Assert.That(Reaches(Overlapping(0, 0), "a", "b")).IsTrue();
    }

    /// <summary><b>And at two heights it joins nothing.</b> The shared cells have no coherent surface — a plan
    /// stating one is refused <c>PL4</c> — so there is no land edge to walk and reachability says so.</summary>
    [Test]
    public async Task An_overlap_at_two_heights_joins_nothing()
    {
        await Assert.That(Reaches(Overlapping(0, 4), "a", "b")).IsFalse();
    }

    /// <summary>And the rect layer answers that same pair the same way, which is the invariant worth holding:
    /// one question, one answer, whichever graph a caller happens to be holding.</summary>
    [Test]
    [Arguments(0, 0, 1)]
    [Arguments(0, 4, 2)]
    public async Task The_two_graphs_answer_one_pair_alike(int surfaceA, int surfaceB, int landmasses)
    {
        var plan = Overlapping(surfaceA, surfaceB);
        var rects = ContactGraph.Build(PlanModel.Parse(plan)!);

        await Assert.That(rects.Components.Count).IsEqualTo(landmasses);
        await Assert.That(Reaches(plan, "a", "b")).IsEqualTo(landmasses == 1);
    }
}
