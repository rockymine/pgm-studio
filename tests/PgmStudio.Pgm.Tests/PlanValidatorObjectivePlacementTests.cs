using PgmStudio.Domain;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// OB17 — the three places a destroyable or a core may not stand, and the reason the rule reads a
/// <em>footprint</em> rather than a marker: a marker legally inside its piece can still put the structure
/// over the void or inside a room, and every one of those is a map that cannot be won with nothing anywhere
/// saying so.
///
/// <para>Cell size is 1 throughout, so a piece rect is its block rect and the numbers in the fixtures are
/// the numbers the rule compares. Both max conventions meet here — <c>BlockRect</c> counts its extent
/// exclusively while a footprint is an inclusive block box — so the edge cases are the point of the tests
/// that pass, not only of the ones that fire.</para>
/// </summary>
public sealed class PlanValidatorObjectivePlacementTests
{
    private static PlanModel Plan(string json) => PlanModel.Parse(json)!;

    private static bool Err(PlanModel plan, string needle) =>
        PlanValidator.Check(plan).Any(f => f.Severity == Severity.Refusal && f.Message.Contains(needle));

    /// <summary>A 20×20 island with a two-team symmetry, so the objective tools are legal (OB14).</summary>
    private static string Land(string placements) => $$"""
    { "plan":1, "globals":{"cell":1,"symmetry":"rot_180"},
      "pieces":[ {"id":"land","role":"piece","rect":[0,0,20,20]} ],
      "placements":{ {{placements}} } }
    """;

    // ── the void ────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_core_whose_casing_hangs_off_the_land_is_refused()
    {
        // Marker at the piece's last block, so CheckInside is happy — but a 5×5 casing centred there puts
        // two columns past the edge, where block_place=deny(void) makes the goal unbreakable.
        var plan = Plan(Land("""  "cores":[ {"piece":"land","at":[19,10],"size":5} ] """));
        await Assert.That(Err(plan, "overhangs the void")).IsTrue();
    }

    [Test]
    public async Task The_same_core_one_footprint_in_from_the_edge_is_fine()
    {
        // 5×5 centred at x=17 spans 15..19, the last block of a 0..19 island — inclusive on both sides.
        var plan = Plan(Land("""  "cores":[ {"piece":"land","at":[17,10],"size":5} ] """));
        await Assert.That(Err(plan, "overhangs the void")).IsFalse();
    }

    [Test]
    public async Task A_one_block_destroyable_at_the_very_edge_stands()
    {
        // pillar-3 is 1×1 in plan, so the edge block is legal ground for it — the rule must not refuse a
        // structure that fits just because a larger one would not.
        var plan = Plan(Land("""  "destroyables":[ {"piece":"land","at":[19,19],"style":"pillar-3"} ] """));
        await Assert.That(Err(plan, "overhangs the void")).IsFalse();
    }

    [Test]
    public async Task A_wide_destroyable_at_the_same_block_is_refused()
    {
        var plan = Plan(Land("""  "destroyables":[ {"piece":"land","at":[19,19],"style":"cube-4"} ] """));
        await Assert.That(Err(plan, "overhangs the void")).IsTrue();
    }

    [Test]
    public async Task Land_assembled_from_abutting_pieces_counts_as_one_surface()
    {
        // The footprint straddles two pieces that meet: every block still stands on something, so nothing
        // fires. A rule testing the marker's own piece alone would refuse this.
        var plan = Plan("""
        { "plan":1, "globals":{"cell":1,"symmetry":"rot_180"},
          "pieces":[ {"id":"west","role":"piece","rect":[0,0,10,20]},
                     {"id":"east","role":"piece","rect":[10,0,10,20]} ],
          "placements":{ "cores":[ {"piece":"west","at":[9,10],"size":5} ] } }
        """);
        await Assert.That(Err(plan, "overhangs the void")).IsFalse();
    }

    // ── spawn and wool rooms ────────────────────────────────────────────────────────────────────────────

    private const string RoomBoard = """
    { "plan":1, "globals":{"cell":1,"symmetry":"rot_180"},
      "pieces":[ {"id":"home","role":"spawn","rect":[0,0,20,20]},
                 {"id":"mid","role":"piece","rect":[20,0,30,20]} ],
      "placements":{ "spawns":[ {"piece":"home","at":[10,10]} ], CORE } }
    """;

    [Test]
    public async Task A_core_reaching_into_the_spawn_room_is_refused()
    {
        // The room is stamped around the spawn marker at the middle of 'home'; a core on its own piece but
        // pushed into that frame inherits block="never", which denies the attacking team too.
        var plan = Plan(RoomBoard.Replace("CORE", """ "cores":[ {"piece":"home","at":[10,10],"size":5} ] """));
        await Assert.That(Err(plan, "reaches into the spawn")).IsTrue();
    }

    [Test]
    public async Task A_core_on_the_far_piece_is_clear_of_the_room()
    {
        // The refusal is against the ROOM, not the piece holding it — a goal well away from the frame is
        // legal even though a spawn lives on the board.
        var plan = Plan(RoomBoard.Replace("CORE", """ "cores":[ {"piece":"mid","at":[25,10],"size":5} ] """));
        await Assert.That(Err(plan, "reaches into the spawn")).IsFalse();
    }

    [Test]
    public async Task A_destroyable_reaching_into_a_wool_room_is_refused()
    {
        // The vault abuts a lane so its room resolves at all — an unreachable wool room refuses at WX6
        // before it has a frame, and a rule about frames has nothing to say about a room that has none.
        var plan = Plan("""
        { "plan":1, "globals":{"cell":1,"symmetry":"rot_180"},
          "pieces":[ {"id":"vault","role":"wool-room","rect":[0,0,20,20]},
                     {"id":"lane","role":"piece","rect":[20,0,20,20]},
                     {"id":"home","role":"spawn","rect":[40,0,20,20]} ],
          "placements":{ "spawns":[ {"piece":"home","at":[50,10]} ],
                         "wools":[ {"piece":"vault","at":[10,10]} ],
                         "destroyables":[ {"piece":"vault","at":[10,10],"style":"cube-4"} ] } }
        """);
        await Assert.That(Err(plan, "reaches into the wool room")).IsTrue();
    }

    // ── the agent surface (B21) ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Every_refusal_is_an_error_so_the_compile_gate_returns_it()
    {
        // An agent driving the endpoint must be refused for each case rather than shipping an unwinnable
        // map: the compile gate answers 422 on errors alone, so a lint here would let all three through.
        var overVoid = Plan(Land("""  "cores":[ {"piece":"land","at":[19,10],"size":5} ] """));
        var inSpawn = Plan(RoomBoard.Replace("CORE", """ "cores":[ {"piece":"home","at":[10,10],"size":5} ] """));

        foreach (var plan in new[] { overVoid, inSpawn })
            await Assert.That(PlanValidator.HasErrors(plan)).IsTrue();
    }

    [Test]
    public async Task The_refusal_names_the_pieces_so_the_editor_can_point_at_them()
    {
        var plan = Plan(RoomBoard.Replace("CORE", """ "cores":[ {"piece":"home","at":[10,10],"size":5} ] """));
        var finding = PlanValidator.Check(plan)
            .First(f => f.Severity == Severity.Refusal && f.Message.Contains("reaches into the spawn"));
        await Assert.That(finding.SubjectIds).Contains("home");
    }
}
