using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Wool-slice generator (declarative authoring). Asserts the objective structure (wool + monuments +
/// room + spawner + defense/edit wiring) and the mirror property: the room reads back as
/// <c>wool/room</c> and the spawn point as <c>wool/spawner</c>. See new-map-authoring.md,
/// filter-region-wiring.md templates 3 + 4.
/// </summary>
public sealed class WoolGeneratorTests
{
    private static Dict Map() => new()
    {
        ["teams"] = new List<object?>
        {
            new Dict { ["id"] = "red-team", ["color"] = "red", ["name"] = "Red" },
            new Dict { ["id"] = "blue-team", ["color"] = "blue", ["name"] = "Blue" },
        },
        ["regions"] = new Dict(), ["filters"] = new Dict(), ["apply_rules"] = new List<object?>(),
        ["wools"] = new List<object?>(), ["spawners"] = new List<object?>(),
    };

    // red owns the red wool; blue captures it (one monument).
    private static MapIntent Intent() => new()
    {
        Wools =
        [
            new WoolIntent
            {
                Owner = "red-team",
                Room = [new Rect(0, 0, 10, 10)],
                Spawn = new Pt(5.5, 10.2, 5.5),
                Monuments = [new MonumentIntent { Team = "blue-team", Location = new Pt(-50, 8, -50) }],
            },
        ],
    };

    private static Dict Regions(Dict d) => (Dict)d["regions"]!;
    private static Dict Filters(Dict d) => (Dict)d["filters"]!;
    private static List<object?> Wools(Dict d) => (List<object?>)d["wools"]!;
    private static List<object?> Spawners(Dict d) => (List<object?>)d["spawners"]!;
    private static List<object?> Rules(Dict d) => (List<object?>)d["apply_rules"]!;

    [Test]
    public async Task Builds_wool_room_spawn_monument_spawner_and_wiring()
    {
        var doc = Map();
        WoolGenerator.Apply(doc, Intent());

        await Assert.That(Regions(doc).ContainsKey("red-wool")).IsTrue();         // room
        await Assert.That(Regions(doc).ContainsKey("red-wool-spawn")).IsTrue();   // spawn point

        var wool = Wools(doc).OfType<Dict>().Single();
        await Assert.That(wool["color"]).IsEqualTo("red");
        await Assert.That(wool["wool_room_region"]).IsEqualTo("red-wool");
        // location = int-floored spawn point (5.5,10.2,5.5 -> 5,10,5)
        var loc = (Dict)wool["location"]!;
        await Assert.That(loc["x"]).IsEqualTo(5);
        await Assert.That(loc["y"]).IsEqualTo(10);
        await Assert.That(((List<object?>)wool["monuments"]!).Count).IsEqualTo(1);

        var sp = Spawners(doc).OfType<Dict>().Single();
        await Assert.That(sp["spawn_region"]).IsEqualTo("red-wool-spawn");
        await Assert.That(sp["player_region"]).IsEqualTo("red-wool");
        var item = (Dict)((List<object?>)sp["items"]!).Single()!;
        await Assert.That(item["material"]).IsEqualTo("wool");
        await Assert.That(item["damage"]).IsEqualTo(14);   // red dye id

        await Assert.That(((Dict)Filters(doc)["not-red"]!)["child"]).IsEqualTo("only-red");
        // the room is grouped into the per-team union (under the top woolrooms union); the wiring lives on it
        await Assert.That(Regions(doc).ContainsKey("reds-woolrooms")).IsTrue();
        await Assert.That(((List<object?>)((Dict)Regions(doc)["woolrooms"]!)["children"]!).Cast<string>()).Contains("reds-woolrooms");
        var onUnion = Rules(doc).OfType<Dict>().Where(r => r.GetValueOrDefault("region") as string == "reds-woolrooms").ToList();
        await Assert.That(onUnion.Any(r => r.GetValueOrDefault("enter") as string == "not-red")).IsTrue();
        await Assert.That(onUnion.Any(r => r.GetValueOrDefault("block") as string == "reds-woolrooms-filter")).IsTrue();
        // the room filter = all(not-<owner>, woolrooms-filter)
        var roomFilter = (Dict)Filters(doc)["reds-woolrooms-filter"]!;
        await Assert.That(roomFilter["type"]).IsEqualTo("all");
        await Assert.That(((List<object?>)roomFilter["children"]!).Cast<string>()).IsEquivalentTo(new[] { "not-red", "woolrooms-filter" });
    }

    [Test]
    public async Task Woolrooms_filter_whitelists_the_kit_blocks_water_and_breakable_decor()
    {
        var doc = Map();
        WoolGenerator.Apply(doc, Intent());
        var f = Filters(doc);
        await Assert.That(((Dict)f["woolrooms-filter"]!)["type"]).IsEqualTo("any");
        // the leaves: cobweb / stained glass + panes (break) and wood / clay (place)
        var mats = f.Values.OfType<Dict>().Where(x => x.GetValueOrDefault("type") as string == "material")
            .Select(x => x["material"] as string).ToList();
        foreach (var m in new[] { "web", "stained glass", "stained glass pane", "wood", "stained clay", "water", "stationary water" })
            await Assert.That(mats).Contains(m);
        // water is player-caused (an <all> of cause=player + the water materials)
        await Assert.That(f.Values.OfType<Dict>().Any(x => x.GetValueOrDefault("type") as string == "cause" && x.GetValueOrDefault("cause") as string == "player")).IsTrue();
    }

    [Test]
    public async Task Room_and_spawn_read_back_as_wool_room_and_spawner()   // mirror property
    {
        var doc = Map();
        WoolGenerator.Apply(doc, Intent());
        var facets = RegionCategorizer.DeriveFacets(doc);

        await Assert.That(facets["red-wool"].Category).IsEqualTo("wool");
        await Assert.That(facets["red-wool"].Subtype).IsEqualTo("room");
        await Assert.That(facets["red-wool-spawn"].Category).IsEqualTo("wool");
        await Assert.That(facets["red-wool-spawn"].Subtype).IsEqualTo("spawner");
    }

    [Test]
    public async Task Reuses_existing_only_filter_from_spawn_protection()
    {
        // teams slice already created only-red; the wool slice must reuse it, not duplicate/conflict.
        var doc = Map();
        Filters(doc)["only-red"] = new Dict { ["id"] = "only-red", ["type"] = "team", ["team"] = "red-team" };
        WoolGenerator.Apply(doc, Intent());

        await Assert.That(Filters(doc).Keys.Count(k => k == "only-red")).IsEqualTo(1);
        await Assert.That(Filters(doc).ContainsKey("not-red")).IsTrue();
    }

    [Test]
    public async Task Reapplying_is_idempotent()
    {
        var doc = Map();
        WoolGenerator.Apply(doc, Intent());
        WoolGenerator.Apply(doc, Intent());

        await Assert.That(Wools(doc).Count).IsEqualTo(1);
        await Assert.That(Spawners(doc).Count).IsEqualTo(1);
        await Assert.That(Rules(doc).Count).IsEqualTo(2);
        await Assert.That(Regions(doc).Keys.Count(k => k.StartsWith("red-wool"))).IsEqualTo(2);
    }

    [Test]
    public async Task Multiple_wools_per_team_share_room_filters()   // intra-team symmetry (≥2 wools/team)
    {
        // red defends two differently-coloured wools. The not-/only- room filters are per-TEAM, so they
        // must be shared — a second same-owner wool previously collided on the 'not-red' filter id.
        var doc = Map();
        WoolGenerator.Apply(doc, new MapIntent
        {
            Wools =
            [
                new WoolIntent { Owner = "red-team", Color = "red", Room = [new Rect(0, 0, 10, 10)], Spawn = new Pt(5, 10, 5),
                    Monuments = [new MonumentIntent { Team = "blue-team", Location = new Pt(-50, 8, -50) }] },
                new WoolIntent { Owner = "red-team", Color = "orange", Room = [new Rect(20, 0, 30, 10)], Spawn = new Pt(25, 10, 5),
                    Monuments = [new MonumentIntent { Team = "blue-team", Location = new Pt(-70, 8, -50) }] },
            ],
        });

        await Assert.That(Wools(doc).Count).IsEqualTo(2);
        await Assert.That(Regions(doc).ContainsKey("red-wool")).IsTrue();
        await Assert.That(Regions(doc).ContainsKey("orange-wool")).IsTrue();
        await Assert.That(Filters(doc).Keys.Count(k => k == "not-red")).IsEqualTo(1);
        await Assert.That(Filters(doc).Keys.Count(k => k == "only-red")).IsEqualTo(1);
        // both rooms grouped into one per-team union, with one enter + one block rule on it
        var union = (Dict)Regions(doc)["reds-woolrooms"]!;
        await Assert.That(((List<object?>)union["children"]!).Cast<string>()).IsEquivalentTo(new[] { "red-wool", "orange-wool" });
        var on = Rules(doc).OfType<Dict>().Where(r => r.GetValueOrDefault("region") as string == "reds-woolrooms").ToList();
        await Assert.That(on.Count(r => r.GetValueOrDefault("enter") as string == "not-red")).IsEqualTo(1);
        await Assert.That(on.Count(r => r.GetValueOrDefault("block") as string == "reds-woolrooms-filter")).IsEqualTo(1);
    }

    [Test]
    public async Task Multi_rect_room_unions_the_rects_and_the_spawner_references_the_union()
    {
        var doc = Map();
        WoolGenerator.Apply(doc, new MapIntent
        {
            Wools =
            [
                new WoolIntent { Owner = "red-team", Color = "red", Room = [new Rect(0, 0, 10, 10), new Rect(10, 0, 16, 6)], Spawn = new Pt(5, 10, 5),
                    Monuments = [new MonumentIntent { Team = "blue-team", Location = new Pt(-50, 8, -50) }] },
            ],
        });

        // the two rects union into the room region the wool / spawner / per-team union all reference
        await Assert.That(Regions(doc).ContainsKey("red-wool-1")).IsTrue();
        await Assert.That(Regions(doc).ContainsKey("red-wool-2")).IsTrue();
        var union = (Dict)Regions(doc)["red-wool"]!;
        await Assert.That(union["type"]).IsEqualTo("union");
        await Assert.That(((List<object?>)union["children"]!).Cast<string>()).IsEquivalentTo(new[] { "red-wool-1", "red-wool-2" });

        var wool = Wools(doc).OfType<Dict>().Single();
        await Assert.That(wool["wool_room_region"]).IsEqualTo("red-wool");
        await Assert.That(Spawners(doc).OfType<Dict>().Single()["player_region"]).IsEqualTo("red-wool");
        await Assert.That(((List<object?>)((Dict)Regions(doc)["reds-woolrooms"]!)["children"]!).Cast<string>()).Contains("red-wool");

        // union + rect children all read back as wool/room (the mirror property)
        var facets = RegionCategorizer.DeriveFacets(doc);
        foreach (var id in new[] { "red-wool", "red-wool-1", "red-wool-2" })
        {
            await Assert.That(facets[id].Category).IsEqualTo("wool");
            await Assert.That(facets[id].Subtype).IsEqualTo("room");
        }
    }

    [Test]
    public async Task Roomless_wool_emits_objective_without_room_or_spawner()   // partial intent (no room yet)
    {
        var doc = Map();
        WoolGenerator.Apply(doc, new MapIntent
        {
            Wools =
            [
                new WoolIntent { Owner = "red-team", Color = "red", Room = [], Spawn = new Pt(5, 10, 5),
                    Monuments = [new MonumentIntent { Team = "blue-team", Location = new Pt(-50, 8, -50) }] },
            ],
        });

        var wool = Wools(doc).OfType<Dict>().Single();          // the objective + monuments still generate
        await Assert.That(wool["color"]).IsEqualTo("red");
        await Assert.That(((List<object?>)wool["monuments"]!).Count).IsEqualTo(1);
        await Assert.That(wool.GetValueOrDefault("wool_room_region")).IsNull();
        // the source side waits for the room: no room region / spawn point / spawner / wiring
        await Assert.That(Regions(doc).ContainsKey("red-wool")).IsFalse();
        await Assert.That(Regions(doc).ContainsKey("red-wool-spawn")).IsFalse();
        await Assert.That(Spawners(doc).Count).IsEqualTo(0);
        await Assert.That(Rules(doc).Count).IsEqualTo(0);
    }
}
