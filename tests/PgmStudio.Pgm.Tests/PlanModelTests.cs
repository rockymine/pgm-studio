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
    public async Task Legacy_piece_roles_map_to_piece_and_intent_roles_survive()
    {
        var plan = PlanModel.Parse("""
        { "plan":1,
          "pieces":[ {"id":"a","role":"lane","rect":[0,0,2,2]},
                     {"id":"b","role":"hub","rect":[2,0,2,2]},
                     {"id":"c","role":"mid","rect":[4,0,2,2]},
                     {"id":"d","role":"wool-room","rect":[6,0,2,2]},
                     {"id":"e","role":"spawn","rect":[8,0,2,2]},
                     {"id":"f","rect":[10,0,2,2]} ] }
        """)!;
        await Assert.That(plan.Pieces[0].Role).IsEqualTo(PlanRoles.Piece);   // lane
        await Assert.That(plan.Pieces[1].Role).IsEqualTo(PlanRoles.Piece);   // hub
        await Assert.That(plan.Pieces[2].Role).IsEqualTo(PlanRoles.Piece);   // mid
        await Assert.That(plan.Pieces[3].Role).IsEqualTo(PlanRoles.WoolRoom);
        await Assert.That(plan.Pieces[4].Role).IsEqualTo(PlanRoles.Spawn);
        await Assert.That(plan.Pieces[5].Role).IsEqualTo(PlanRoles.Piece);   // absent → default
    }

    [Test]
    public async Task Buffer_role_round_trips_and_is_an_annotation()
    {
        // Canonical preserves the buffer annotation role (Normalize must not fold it to piece); an unknown role
        // still folds to piece.
        var plan = PlanModel.Parse("""
        { "plan":1,
          "pieces":[ {"id":"buffer","role":"buffer","rect":[0,0,2,2]},
                     {"id":"x","role":"nonsense","rect":[2,0,2,2]} ] }
        """)!;
        await Assert.That(plan.Pieces[0].Role).IsEqualTo(PlanRoles.Buffer);   // survives Normalize
        await Assert.That(plan.Pieces[1].Role).IsEqualTo(PlanRoles.Piece);    // unknown → piece

        var reparsed = PlanModel.Parse(plan.ToJson())!;
        await Assert.That(reparsed.Pieces[0].Role).IsEqualTo(PlanRoles.Buffer);

        await Assert.That(PlanRoles.Canonical("buffer")).IsEqualTo(PlanRoles.Buffer);
        await Assert.That(PlanRoles.IsAnnotation(PlanRoles.Buffer)).IsTrue();
        await Assert.That(PlanRoles.IsGenerating(PlanRoles.Buffer)).IsFalse();
        await Assert.That(PlanRoles.IsAnnotation(PlanRoles.Piece)).IsFalse();
        await Assert.That(PlanRoles.IsGenerating(PlanRoles.Piece)).IsTrue();
    }

    [Test]
    public async Task Walls_parse_and_round_trip()
    {
        var plan = PlanModel.Parse("""
        { "plan":1,
          "pieces":[ {"id":"a","role":"piece","rect":[0,0,2,2]}, {"id":"b","role":"piece","rect":[2,0,2,2]} ],
          "walls":[ {"a":"a","b":"b"} ] }
        """)!;
        await Assert.That(plan.Walls.Count).IsEqualTo(1);
        await Assert.That(plan.Walls[0].A).IsEqualTo("a");
        await Assert.That(plan.Walls[0].B).IsEqualTo("b");

        var reparsed = PlanModel.Parse(plan.ToJson())!;
        await Assert.That(reparsed.Walls.Count).IsEqualTo(1);
        await Assert.That(reparsed.Walls[0].B).IsEqualTo("b");
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
