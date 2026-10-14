using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// A zone states rules over a region, and each thing it says is one apply rule over it — or, for harm, one deny in
/// the damage module — written before the build rules, so what a zone forbids is forbidden inside a build region
/// too. What it leaves unsaid it leaves alone.
/// </summary>
public sealed class ZoneGeneratorTests
{
    private static readonly TeamDef[] Teams =
    [
        new() { Id = "red-team", Name = "Red", Color = "red" },
        new() { Id = "blue-team", Name = "Blue", Color = "blue" },
    ];

    private static MapXml Written(params ZoneIntent[] zones) => Written(new MapIntent { Teams = [.. Teams], Zones = [.. zones] });

    private static MapXml Written(MapIntent intent)
    {
        var doc = new Dict();
        IntentGenerator.Apply(doc, intent);
        return MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));
    }

    private static ZoneIntent Lobby => new() { Id = "lobby", Area = [new Rect(-4, -4, 4, 4)] };

    private static List<ApplyRule> RulesOver(MapXml map, string region) => [.. map.ApplyRules.Where(rule => rule.RegionId == region)];

    [Test]
    public async Task A_zone_nobody_may_enter_turns_everyone_away()
    {
        var rule = RulesOver(Written(Lobby with { Enter = ZoneEntries.Nobody }), "zone-lobby").Single();

        await Assert.That(rule.EnterFilter).IsEqualTo("never");
        await Assert.That(rule.Message).IsNotEmpty();
    }

    [Test]
    public async Task A_team_zone_lets_its_team_in_and_fans_to_the_other_team()
    {
        var map = Written(new MapIntent
        {
            Teams = [.. Teams],
            Symmetry = new SymmetryIntent { Mode = "rot_180" },
            Zones = [Lobby with { Team = "red-team", Enter = ZoneEntries.Team, Area = [new Rect(20, -4, 28, 4)] }],
        });

        await Assert.That(RulesOver(map, "zone-lobby-red").Single().EnterFilter).IsEqualTo("red-team");
        await Assert.That(RulesOver(map, "zone-lobby-blue").Single().EnterFilter).IsEqualTo("blue-team");
        var blue = map.Regions["zone-lobby-blue"];
        await Assert.That((blue.Bounds2d!.MinX, blue.Bounds2d.MaxX)).IsEqualTo((-28.0, -20.0));
    }

    [Test]
    public async Task A_zone_refusing_edits_decides_before_the_build_rules()
    {
        var map = Written(new MapIntent
        {
            Teams = [.. Teams],
            Zones = [Lobby with { Place = false, Break = false }],
            Build = new BuildIntent { Mode = BuildModes.Void, Areas = [new Rect(-20, -20, 20, 20)] },
        });

        var zone = RulesOver(map, "zone-lobby").Single();
        await Assert.That((zone.BlockPlaceFilter, zone.BlockBreakFilter)).IsEqualTo(("never", "never"));
        await Assert.That(map.ApplyRules.IndexOf(zone)).IsLessThan(map.ApplyRules.FindIndex(rule => rule.RegionId == "not-build-area"));
    }

    [Test]
    public async Task A_harmless_zone_denies_every_hit_inside_it()
    {
        var map = Written(Lobby with { Harm = false });

        var deny = map.Filters[map.Damage!.FilterIds.Single()];
        await Assert.That(deny.Type).IsEqualTo("deny");
        var inside = map.Filters[deny.Child!];
        await Assert.That((inside.Type, inside.RegionRef)).IsEqualTo(("region", (string?)"zone-lobby"));
    }

    [Test]
    public async Task A_zone_gives_its_kit_to_whoever_is_inside()
    {
        await Assert.That(RulesOver(Written(Lobby with { Kit = "spawn-kit" }), "zone-lobby").Single().Kit).IsEqualTo("spawn-kit");
    }

    [Test]
    public async Task A_zone_that_says_nothing_writes_no_rule()
    {
        var map = Written(Lobby);

        await Assert.That(map.Regions.ContainsKey("zone-lobby")).IsTrue();
        await Assert.That(RulesOver(map, "zone-lobby")).IsEmpty();
        await Assert.That(map.Damage).IsNull();
    }

    [Test]
    public async Task Applying_twice_writes_each_zone_once()
    {
        var doc = new Dict();
        var intent = new MapIntent { Teams = [.. Teams], Zones = [Lobby with { Enter = ZoneEntries.Nobody, Harm = false }] };
        IntentGenerator.Apply(doc, intent);
        IntentGenerator.Apply(doc, intent);

        var rules = ((List<object?>)doc["apply_rules"]!).OfType<Dict>().Count(rule => rule["region"] as string == "zone-lobby");
        await Assert.That(rules).IsEqualTo(1);
        await Assert.That(((List<object?>)((Dict)doc["damage"]!)["filters"]!).Count).IsEqualTo(1);
    }
}
