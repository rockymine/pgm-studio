using PgmStudio.Domain;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// Marker identity. A piece and a zone each carry an id; a marker carried only its index in a list, which is
/// enough to draw one and not enough to <em>refer</em> to one — a finding could name only the piece a marker
/// stood on, and an agent holding "the second core" lost its reference the moment another core was deleted.
///
/// <para>Ids are minted on parse, so a plan written before markers had identity gains it on load, and the
/// server mints by the same rule the editor does (<c>plan-doc.js mintMarkerIds</c>).</para>
/// </summary>
public sealed class PlanMarkerIdTests
{
    private static PlanModel Plan(string json) => PlanModel.Parse(json)!;

    [Test]
    public async Task A_plan_written_before_markers_had_ids_gains_them_on_load()
    {
        var plan = Plan("""
        { "plan":1, "globals":{"cell":1},
          "placements":{ "spawns":[{"piece":"home","at":[1,1]}],
                         "cores":[{"piece":"mid","at":[0,0]},{"piece":"mid","at":[2,2]}] } }
        """);
        await Assert.That(plan.Placements.Spawns[0].Id).IsEqualTo("spawn-1");
        await Assert.That(plan.Placements.Cores.Select(core => core.Id)).IsEquivalentTo(new[] { "core-1", "core-2" });
    }

    [Test]
    public async Task An_authored_id_survives_and_the_mint_routes_around_it()
    {
        // Identity is the author's to set: an id that means something to them must survive a load, and the
        // mint has to step around it rather than issue the same name twice.
        var plan = Plan("""
        { "plan":1, "globals":{"cell":1},
          "placements":{ "cores":[{"id":"core-2","piece":"mid","at":[0,0]},{"piece":"mid","at":[2,2]}] } }
        """);
        await Assert.That(plan.Placements.Cores.Select(core => core.Id)).IsEquivalentTo(new[] { "core-2", "core-1" });
    }

    [Test]
    public async Task A_duplicate_id_is_not_an_id_and_the_later_marker_is_re_minted()
    {
        // Two markers answering to one name is worse than no name: a finding or an agent naming it would
        // resolve to whichever came first, silently.
        var plan = Plan("""
        { "plan":1, "globals":{"cell":1},
          "placements":{ "cores":[{"id":"heart","piece":"mid","at":[0,0]},{"id":"heart","piece":"mid","at":[2,2]}] } }
        """);
        await Assert.That(plan.Placements.Cores[0].Id).IsEqualTo("heart");
        await Assert.That(plan.Placements.Cores[1].Id).IsEqualTo("core-1");
    }

    [Test]
    public async Task Ids_are_unique_across_kinds_not_merely_within_one()
    {
        // A finding names one id; if a wool and a core could both be "core-1" it would resolve to either.
        var plan = Plan("""
        { "plan":1, "globals":{"cell":1},
          "placements":{ "wools":[{"id":"core-1","piece":"vault","at":[0,0]}],
                         "cores":[{"piece":"mid","at":[0,0]}] } }
        """);
        await Assert.That(plan.Placements.Cores[0].Id).IsEqualTo("core-2");
    }

    [Test]
    public async Task Ids_round_trip_through_the_wire_format()
    {
        var plan = Plan("""
        { "plan":1, "globals":{"cell":1},
          "placements":{ "cores":[{"id":"heart","piece":"mid","at":[0,0]}] } }
        """);
        await Assert.That(Plan(plan.ToJson()).Placements.Cores[0].Id).IsEqualTo("heart");
    }

    [Test]
    public async Task Every_marker_kind_is_minted()
    {
        // The mint walks Placements.All(); a kind missing from that pass would silently keep an empty id and
        // be unnameable, which is the failure this whole change is about.
        var plan = Plan("""
        { "plan":1, "globals":{"cell":1},
          "placements":{ "spawns":[{"piece":"p","at":[0,0]}], "wools":[{"piece":"p","at":[0,0]}],
                         "iron":[{"piece":"p","at":[0,0]}], "destroyables":[{"piece":"p","at":[0,0]}],
                         "cores":[{"piece":"p","at":[0,0]}] } }
        """);
        foreach (var (_, marker) in plan.Placements.All())
            await Assert.That(marker.Id).IsNotEmpty();
    }

    [Test]
    public async Task A_refused_objective_is_named_by_its_marker_not_only_by_its_piece()
    {
        // The point of the ids: an OB17 refusal is about one marker, and pulsing the whole piece under it
        // says the wrong thing about which one is wrong.
        var plan = Plan("""
        { "plan":1, "globals":{"cell":1,"symmetry":"rot_180"},
          "pieces":[ {"id":"land","role":"piece","rect":[0,0,20,20]} ],
          "placements":{ "cores":[ {"id":"heart","piece":"land","at":[19,10],"size":5} ] } }
        """);
        var finding = PlanValidator.Check(plan)
            .First(f => f.Severity == Severity.Refusal && f.Message.Contains("overhangs the void"));
        await Assert.That(finding.SubjectIds).Contains("heart");
        await Assert.That(finding.Message).Contains("'heart'");
    }
}
