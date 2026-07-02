using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The plan wire model: parse round-trips, defaults (<c>mirrors</c> true, absent optional fields), and that
/// the checked-in seed plans deserialise into the expected shape.
/// </summary>
public sealed class PlanModelTests
{
    [Test]
    public async Task Parses_a_plan_and_round_trips_through_json()
    {
        const string json = """
        {
          "plan": 1,
          "meta": { "name": "T" },
          "globals": { "cell": 5, "symmetry": "rot_180", "maxPlayers": 12, "surface": 9, "headroom": 11 },
          "pieces": [ { "id": "a", "role": "lane", "rect": [1, 5, 2, 6] },
                      { "id": "b", "role": "mid", "rect": [1, 1, 2, 2], "surface": 13, "mirrors": false } ],
          "zones": [ { "id": "z", "rect": [-3, -5, 6, 10], "holes": [ [-1, -1, 2, 2] ] } ],
          "placements": {
            "spawns": [ { "piece": "a", "at": [1, 5], "facing": "front" } ],
            "wools":  [ { "piece": "a", "at": [1, 8], "color": "orange" } ],
            "iron":   [ { "piece": "a", "at": [0, 4] } ]
          },
          "cliffs": [ { "a": "a", "b": "b" } ]
        }
        """;
        var plan = PlanModel.Parse(json)!;
        await Assert.That(plan.Globals.Cell).IsEqualTo(5);
        await Assert.That(plan.Pieces.Count).IsEqualTo(2);
        await Assert.That(plan.Pieces[0].MirrorsOrDefault).IsTrue();     // absent → default true
        await Assert.That(plan.Pieces[1].MirrorsOrDefault).IsFalse();    // explicit false
        await Assert.That(plan.Pieces[1].Surface).IsEqualTo(13);
        await Assert.That(plan.Zones[0].Holes.Count).IsEqualTo(1);
        await Assert.That(plan.Placements.Wools[0].Color).IsEqualTo("orange");
        await Assert.That(plan.Placements.Iron.Count).IsEqualTo(1);
        await Assert.That(plan.Cliffs[0].A).IsEqualTo("a");

        var reparsed = PlanModel.Parse(plan.ToJson())!;
        await Assert.That(reparsed.Pieces.Count).IsEqualTo(2);
        await Assert.That(reparsed.Placements.Spawns[0].Facing).IsEqualTo("front");
    }

    [Test]
    public async Task Absent_optional_color_is_null_and_defaults_apply()
    {
        var plan = PlanModel.Parse("""{ "plan": 1, "placements": { "wools": [ { "piece": "a", "at": [0, 0] } ] } }""")!;
        await Assert.That(plan.Placements.Wools[0].Color).IsNull();
        await Assert.That(plan.Globals.Cell).IsEqualTo(5);              // default cell
        await Assert.That(plan.Globals.Symmetry).IsEqualTo("rot_180");
    }

    [Test]
    public async Task Each_seed_plan_parses()
    {
        foreach (var name in new[] { "base-2island", "base-2wool", "base-4team" })
        {
            var plan = PlanModel.Parse(PlanTestSupport.ReadSeed($"{name}.plan.json"))!;
            await Assert.That(plan.Pieces.Count).IsGreaterThan(0);
            await Assert.That(plan.Placements.Spawns.Count).IsGreaterThan(0);
        }
    }
}
