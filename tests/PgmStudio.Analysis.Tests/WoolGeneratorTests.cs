using PgmStudio.Analysis;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Analysis.Tests;

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
                Room = new Rect(0, 0, 10, 10),
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
        var onRoom = Rules(doc).OfType<Dict>().Where(r => r.GetValueOrDefault("region") as string == "red-wool").ToList();
        await Assert.That(onRoom.Any(r => r.GetValueOrDefault("enter") as string == "not-red")).IsTrue();
        await Assert.That(onRoom.Any(r => r.GetValueOrDefault("block") as string == "not-red")).IsTrue();
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
}
