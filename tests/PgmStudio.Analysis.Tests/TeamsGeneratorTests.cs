using PgmStudio.Analysis;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Teams-slice generator (declarative authoring). Asserts the emitted structure and the
/// <b>mirror property</b>: what the generator produces, the categorizer reads back as the same intent
/// (spawn/point + spawn/protection). See docs/contracts/new-map-authoring.md §8.
/// </summary>
public sealed class TeamsGeneratorTests
{
    private static Dict Map() => new()
    {
        ["teams"] = new List<object?>
        {
            new Dict { ["id"] = "red-team", ["color"] = "red", ["name"] = "Red", ["max_players"] = 20 },
            new Dict { ["id"] = "blue-team", ["color"] = "blue", ["name"] = "Blue", ["max_players"] = 20 },
        },
        ["regions"] = new Dict(),
        ["filters"] = new Dict(),
        ["spawns"] = new List<object?>(),
        ["apply_rules"] = new List<object?>(),
    };

    private static MapIntent Intent() => new()
    {
        Spawns =
        [
            new SpawnIntent { Team = "red-team",  Point = new(10, 12, 10),   Protection = new(0, 0, 20, 20) },
            new SpawnIntent { Team = "blue-team", Point = new(-10, 12, -10), Protection = new(-20, -20, 0, 0) },
        ],
    };

    private static Dict Regions(Dict d) => (Dict)d["regions"]!;
    private static Dict Filters(Dict d) => (Dict)d["filters"]!;
    private static List<object?> Spawns(Dict d) => (List<object?>)d["spawns"]!;
    private static List<object?> Rules(Dict d) => (List<object?>)d["apply_rules"]!;

    [Test]
    public async Task Generates_point_protection_filter_rule_and_link_per_team()
    {
        var doc = Map();
        TeamsGenerator.Apply(doc, Intent());

        await Assert.That(Regions(doc).ContainsKey("red-spawn-point")).IsTrue();
        await Assert.That(Regions(doc).ContainsKey("red-spawn")).IsTrue();
        await Assert.That(Filters(doc).ContainsKey("only-red")).IsTrue();
        await Assert.That(((Dict)Filters(doc)["only-red"]!)["team"]).IsEqualTo("red-team");

        var redSpawn = Spawns(doc).OfType<Dict>().FirstOrDefault(s => s.GetValueOrDefault("region") as string == "red-spawn-point");
        await Assert.That(redSpawn).IsNotNull();
        await Assert.That(redSpawn!["team"]).IsEqualTo("red-team");

        var redRule = Rules(doc).OfType<Dict>().FirstOrDefault(r => r.GetValueOrDefault("region") as string == "red-spawn");
        await Assert.That(redRule).IsNotNull();
        await Assert.That(redRule!["enter"]).IsEqualTo("only-red");

        await Assert.That(Spawns(doc).Count).IsEqualTo(2);
    }

    [Test]
    public async Task Protection_zone_gets_both_enter_and_block_protection()
    {
        var doc = Map();
        TeamsGenerator.Apply(doc, new MapIntent { Spawns = [new SpawnIntent { Team = "red-team", Point = new(0, 8, 0), Protection = new(0, 0, 10, 10) }] });

        var onRed = Rules(doc).OfType<Dict>().Where(r => r.GetValueOrDefault("region") as string == "red-spawn").ToList();
        await Assert.That(onRed.Count).IsEqualTo(2);
        await Assert.That(onRed.Any(r => r.GetValueOrDefault("enter") as string == "only-red")).IsTrue();
        await Assert.That(onRed.Any(r => r.GetValueOrDefault("block") as string == "never")).IsTrue();
    }

    [Test]
    public async Task Generated_structure_reads_back_as_spawn_point_and_protection()   // the mirror property
    {
        var doc = Map();
        TeamsGenerator.Apply(doc, Intent());
        var facets = RegionCategorizer.DeriveFacets(doc);

        await Assert.That(facets["red-spawn-point"].Category).IsEqualTo("spawn");
        await Assert.That(facets["red-spawn-point"].Subtype).IsEqualTo("point");
        await Assert.That(facets["red-spawn"].Category).IsEqualTo("spawn");
        await Assert.That(facets["red-spawn"].Subtype).IsEqualTo("protection");
        await Assert.That(facets["blue-spawn"].Subtype).IsEqualTo("protection");
    }

    [Test]
    public async Task Reapplying_is_idempotent()
    {
        var doc = Map();
        TeamsGenerator.Apply(doc, Intent());
        TeamsGenerator.Apply(doc, Intent());

        await Assert.That(Spawns(doc).Count).IsEqualTo(2);
        await Assert.That(Regions(doc).Keys.Count(k => k.EndsWith("-spawn-point"))).IsEqualTo(2);
        await Assert.That(Rules(doc).Count).IsEqualTo(4);   // enter + block per protected team
        await Assert.That(Filters(doc).Count).IsEqualTo(2);
    }

    [Test]
    public async Task Spawn_without_protection_emits_point_only()
    {
        var doc = Map();
        TeamsGenerator.Apply(doc, new MapIntent { Spawns = [new SpawnIntent { Team = "red-team", Point = new(1, 2, 3) }] });

        await Assert.That(Regions(doc).ContainsKey("red-spawn-point")).IsTrue();
        await Assert.That(Regions(doc).ContainsKey("red-spawn")).IsFalse();
        await Assert.That(Filters(doc).Count).IsEqualTo(0);
        await Assert.That(Rules(doc).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Emits_the_standard_spawn_kit()   // blocking gap #1
    {
        var doc = new Dict { ["regions"] = new Dict(), ["spawns"] = new List<object?>(), ["kits"] = new List<object?>() };
        TeamsGenerator.Apply(doc, new MapIntent { Spawns = [new SpawnIntent { Team = "red-team", Point = new(0, 8, 0) }] });

        var kits = (List<object?>)doc["kits"]!;
        var kit = kits.OfType<Dict>().FirstOrDefault(k => k.GetValueOrDefault("id") as string == "spawn-kit");
        await Assert.That(kit).IsNotNull();
        await Assert.That(kits.Count).IsEqualTo(1);

        var items = ((List<object?>)kit!["items"]!).OfType<Dict>().ToList();
        var armor = ((List<object?>)kit["armor"]!).OfType<Dict>().ToList();
        string Mat(IEnumerable<Dict> xs, string m) => xs.First(x => x["material"] as string == m)["material"] as string ?? "";

        // Standard preset: infinity bow + a single arrow, the two staple items, chainmail leggings, 4 armour pieces
        var bow = items.First(i => i["material"] as string == "bow");
        await Assert.That(bow["enchantments"]).IsEqualTo("infinity:1");
        var arrow = items.First(i => i["material"] as string == "arrow");
        await Assert.That(arrow.ContainsKey("amount")).IsFalse();   // amount 1 (omitted) — infinity makes it endless
        await Assert.That(Mat(items, "golden apple")).IsEqualTo("golden apple");
        await Assert.That(Mat(items, "water bucket")).IsEqualTo("water bucket");
        await Assert.That(armor.Count).IsEqualTo(4);
        await Assert.That(Mat(armor, "chainmail leggings")).IsEqualTo("chainmail leggings");
    }

    [Test]
    public async Task Spawns_link_to_the_fixed_kit_id()
    {
        var doc = Map();
        TeamsGenerator.Apply(doc, new MapIntent { Spawns = [new SpawnIntent { Team = "red-team", Point = new(0, 8, 0) }] });
        var spawn = Spawns(doc).OfType<Dict>().First();
        await Assert.That(spawn["kit"]).IsEqualTo("spawn-kit");
    }

    [Test]
    public async Task Generates_the_observer_default_spawn()   // blocking gap #2
    {
        var doc = Map();
        TeamsGenerator.Apply(doc, new MapIntent
        {
            Spawns = [new SpawnIntent { Team = "red-team", Point = new(0, 8, 0) }],
            Observer = new ObserverIntent { Point = new(0, 8, 0), Yaw = 180 },
        });

        await Assert.That(Regions(doc).ContainsKey("observer-spawn")).IsTrue();
        var obs = doc["observer_spawn"] as Dict;
        await Assert.That(obs).IsNotNull();
        await Assert.That(obs!["region"]).IsEqualTo("observer-spawn");
        await Assert.That(RegionCategorizer.DeriveFacets(doc)["observer-spawn"].Category).IsEqualTo("observer_spawn");
    }

    [Test]
    public async Task Always_emits_a_default_spawn_even_without_observer_intent()   // PGM requires one
    {
        var doc = Map();
        TeamsGenerator.Apply(doc, new MapIntent { Spawns = [new SpawnIntent { Team = "red-team", Point = new(40, 10, -20) }] });

        var obs = doc["observer_spawn"] as Dict;
        await Assert.That(obs).IsNotNull();
        await Assert.That(obs!["region"]).IsEqualTo("observer-spawn");
        await Assert.That(Regions(doc).ContainsKey("observer-spawn")).IsTrue();
        await Assert.That(RegionCategorizer.DeriveFacets(doc)["observer-spawn"].Category).IsEqualTo("observer_spawn");
    }

    [Test]
    public async Task Generates_teams_from_intent()   // blocking gap #3
    {
        var doc = new Dict { ["regions"] = new Dict(), ["spawns"] = new List<object?>(), ["kits"] = new List<object?>() };
        TeamsGenerator.Apply(doc, new MapIntent
        {
            Teams = [new TeamDef { Id = "red-team", Name = "Red", Color = "red" }, new TeamDef { Id = "blue-team", Name = "Blue", Color = "blue" }],
            MaxPlayers = 12,
            Spawns = [new SpawnIntent { Team = "red-team", Point = new(0, 8, 0) }],
        });

        var teams = (List<object?>)doc["teams"]!;
        await Assert.That(teams.Count).IsEqualTo(2);
        var red = teams.OfType<Dict>().First(t => t.GetValueOrDefault("id") as string == "red-team");
        await Assert.That(red["max_players"]).IsEqualTo(12);
        await Assert.That(red["color"]).IsEqualTo("red");
    }

    [Test]
    public async Task Names_by_team_id_slug_not_raw_colour()   // slug fix
    {
        // a team whose colour is multi-word ("dark red") must still name regions/filters "red-*", not "dark red-*"
        var doc = new Dict
        {
            ["teams"] = new List<object?> { new Dict { ["id"] = "red-team", ["color"] = "dark red", ["name"] = "Red" } },
            ["regions"] = new Dict(), ["filters"] = new Dict(), ["spawns"] = new List<object?>(), ["apply_rules"] = new List<object?>(),
        };
        TeamsGenerator.Apply(doc, new MapIntent { Spawns = [new SpawnIntent { Team = "red-team", Point = new(0, 8, 0), Protection = new(0, 0, 10, 10) }] });

        await Assert.That(Regions(doc).ContainsKey("red-spawn-point")).IsTrue();
        await Assert.That(Filters(doc).ContainsKey("only-red")).IsTrue();
        await Assert.That(Regions(doc).Keys.Any(k => k.Contains("dark"))).IsFalse();
        // the filter still targets the real team id
        await Assert.That(((Dict)Filters(doc)["only-red"]!)["team"]).IsEqualTo("red-team");
    }
}
