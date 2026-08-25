using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;
using PgmStudio.Geom;

/// <summary>
/// End-to-end proof for the declarative generator + XML export: a full intent → generated document →
/// <c>map.xml</c> (Deserializer.FromDict → XmlWriter.ToXml) → re-parsed. If the generated map survives
/// the XML round-trip with its teams/spawns/wools/kit intact, it's a real PGM document.
/// </summary>
public sealed class IntentXmlExportTests
{
    private static Dict BaseDoc() => new()
    {
        ["regions"] = new Dict(), ["filters"] = new Dict(),
        ["spawns"] = new List<object?>(), ["apply_rules"] = new List<object?>(),
        ["wools"] = new List<object?>(), ["spawners"] = new List<object?>(), ["kits"] = new List<object?>(),
        ["teams"] = new List<object?>(),
    };

    private static MapIntent FullIntent() => new()
    {
        Meta = new MetaIntent { Name = "Test Map", Created = "2026-08-25" },
        Teams = [new TeamDef { Id = "red-team", Name = "Red", Color = "red" }, new TeamDef { Id = "blue-team", Name = "Blue", Color = "blue" }],
        MaxPlayers = 12,
        Spawns =
        [
            new SpawnIntent { Team = "red-team", Point = new(100, 12, 50), Protection = [new(90, 40, 110, 60)] },
            new SpawnIntent { Team = "blue-team", Point = new(-100, 12, -50), Protection = [new(-110, -60, -90, -40)] },
        ],
        Observer = new ObserverIntent { Point = new(0, 60, 0), Yaw = 180 },
        Build = new BuildIntent { MaxHeight = 30, Areas = [new Rect(0, 0, 50, 50), new Rect(-50, -50, 0, 0)] },
        Wools =
        [
            new WoolIntent { Owner = "red-team", Room = [new(95, 45, 105, 55)], Spawn = new(100.5, 13, 50.5),
                Monuments = [new MonumentIntent { Team = "blue-team", Location = new(-100, 13, -50) }] },
            new WoolIntent { Owner = "blue-team", Room = [new(-105, -55, -95, -45)], Spawn = new(-100.5, 13, -50.5),
                Monuments = [new MonumentIntent { Team = "red-team", Location = new(100, 13, 50) }] },
        ],
    };

    [Test]
    public async Task Generated_map_exports_to_wellformed_pgm_xml()
    {
        var doc = BaseDoc();
        IntentGenerator.Apply(doc, FullIntent());

        var xml = XmlWriter.ToXml(Deserializer.FromDict(doc));
        await Assert.That(xml).Contains("proto=\"1.5.0\"");
        await Assert.That(xml).Contains("<kits>");
        // The phase is the studio's word for a map it authored; the date is the one the intent stated. Both
        // stand between the objective and the includes, where docs/pgm/template.xml puts them.
        await Assert.That(xml).Contains("<phase>development</phase>");
        await Assert.That(xml).Contains("<created>2026-08-25</created>");
        await Assert.That(xml.IndexOf("<created>", StringComparison.Ordinal))
            .IsGreaterThan(xml.IndexOf("<objective>", StringComparison.Ordinal));
        await Assert.That(xml.IndexOf("<phase>", StringComparison.Ordinal))
            .IsLessThan(xml.IndexOf("<teams", StringComparison.Ordinal));
        // the observer (<default>) spawn is emitted with its yaw (team spawns here have yaw 0 → omitted)
        await Assert.That(xml).Contains("<default");
        await Assert.That(xml).Contains("yaw=\"180\"");

        // spawn protection: infinite damage-resistance in spawn + a force reset kit applied outside it
        await Assert.That(xml).Contains("<effect duration=\"oo\" amplifier=\"100\">damage resistance</effect>");
        await Assert.That(xml).Contains("<kit id=\"reset-resistance-kit\" force=\"true\">");
        await Assert.That(xml).Contains("<apply kit=\"reset-resistance-kit\" region=\"not-spawns\"/>");

        // the spawn kit empties the inventory before it fills it — once, and on that kit only (a clear on the
        // force-applied reset kit would wipe the inventory every tick)
        await Assert.That(xml.Split("<clear/>").Length - 1).IsEqualTo(1);
        await Assert.That(xml.IndexOf("<clear/>", StringComparison.Ordinal))
            .IsGreaterThan(xml.IndexOf("<kit id=\"spawn-kit\">", StringComparison.Ordinal));

        // re-parse the generated XML — proves it's well-formed and PGM-parseable
        var reparsed = Serializer.ToDict(MapParser.ParseXmlString(xml));
        await Assert.That(((List<object?>)reparsed["teams"]!).Count).IsEqualTo(2);
        await Assert.That(((List<object?>)reparsed["spawns"]!).Count).IsEqualTo(2);
        await Assert.That(((List<object?>)reparsed["wools"]!).Count).IsEqualTo(2);
        await Assert.That(((List<object?>)reparsed["spawners"]!).Count).IsEqualTo(2);
        // two teams, one wool each: the objective line counts per team, so it reads singular
        await Assert.That(reparsed["objective"]).IsEqualTo("Capture the wool!");
        // build void enforcement + the spawn-leave reset complement survived
        await Assert.That(((Dict)reparsed["regions"]!).ContainsKey("not-build-area")).IsTrue();
        await Assert.That(((Dict)reparsed["regions"]!).ContainsKey("not-spawns")).IsTrue();
    }

    [Test]
    public async Task Multi_rect_protection_and_room_survive_the_xml_round_trip_as_unions()
    {
        var doc = BaseDoc();
        var intent = new MapIntent
        {
            Meta = new MetaIntent { Name = "Multi" },
            Teams = [new TeamDef { Id = "red-team", Name = "Red", Color = "red" }, new TeamDef { Id = "blue-team", Name = "Blue", Color = "blue" }],
            Spawns =
            [
                new SpawnIntent { Team = "red-team", Point = new(100, 12, 50), Protection = [new(90, 40, 110, 60), new(110, 45, 120, 55)] },
                new SpawnIntent { Team = "blue-team", Point = new(-100, 12, -50), Protection = [new(-110, -60, -90, -40), new(-120, -55, -110, -45)] },
            ],
            Wools =
            [
                new WoolIntent { Owner = "red-team", Color = "red", Room = [new(95, 45, 105, 55), new(105, 47, 112, 53)], Spawn = new(100.5, 13, 50.5),
                    Monuments = [new MonumentIntent { Team = "blue-team", Location = new(-100, 13, -50) }] },
            ],
        };
        IntentGenerator.Apply(doc, intent);

        var xml = XmlWriter.ToXml(Deserializer.FromDict(doc));
        var regions = (Dict)Serializer.ToDict(MapParser.ParseXmlString(xml))["regions"]!;

        var prot = (Dict)regions["red-spawn"]!;
        await Assert.That(prot["type"]).IsEqualTo("union");
        await Assert.That(((List<object?>)prot["children"]!).Cast<string>()).IsEquivalentTo(new[] { "red-spawn-1", "red-spawn-2" });

        var room = (Dict)regions["red-wool"]!;
        await Assert.That(room["type"]).IsEqualTo("union");
        await Assert.That(((List<object?>)room["children"]!).Cast<string>()).IsEquivalentTo(new[] { "red-wool-1", "red-wool-2" });
        await Assert.That(regions.ContainsKey("blue-spawn-2")).IsTrue();   // orbit-filled partner's second rect
    }

    [Test]
    public async Task Build_holes_survive_the_xml_round_trip_as_a_complement()
    {
        var doc = BaseDoc();
        var intent = new MapIntent
        {
            Meta = new MetaIntent { Name = "Holey" },
            Teams = [new TeamDef { Id = "red-team", Name = "Red", Color = "red" }, new TeamDef { Id = "blue-team", Name = "Blue", Color = "blue" }],
            Spawns = [new SpawnIntent { Team = "red-team", Point = new(10, 12, 10) }, new SpawnIntent { Team = "blue-team", Point = new(-10, 12, -10) }],
            Build = new BuildIntent { Areas = [new Rect(0, 0, 50, 50), new Rect(-50, -50, 0, 0)], Holes = [new Rect(10, 10, 20, 20)] },
        };
        IntentGenerator.Apply(doc, intent);

        var xml = XmlWriter.ToXml(Deserializer.FromDict(doc));
        var reparsed = Serializer.ToDict(MapParser.ParseXmlString(xml));
        var regions = (Dict)reparsed["regions"]!;

        var comp = (Dict)regions["buildable"]!;
        await Assert.That(comp["type"]).IsEqualTo("complement");
        await Assert.That(((List<object?>)comp["children"]!).Cast<string>().First()).IsEqualTo("build-area");
        await Assert.That(regions.ContainsKey("build-hole-1")).IsTrue();
        await Assert.That(((List<object?>)((Dict)regions["not-build-area"]!)["children"]!).Single()).IsEqualTo("buildable");
    }

    [Test]
    public async Task Void_enforcement_survives_the_xml_round_trip_with_no_build_area_declared()
    {
        // A worked example: an intent that states void enforcement and declares no build area at all — the
        // exact shape that used to be inexpressible, because BuildGenerator returned before it ever reached
        // the void-enforcement wiring when Areas was empty.
        var doc = BaseDoc();
        var intent = new MapIntent
        {
            Meta = new MetaIntent { Name = "Permanent Void" },
            Teams = [new TeamDef { Id = "red-team", Name = "Red", Color = "red" }, new TeamDef { Id = "blue-team", Name = "Blue", Color = "blue" }],
            Spawns = [new SpawnIntent { Team = "red-team", Point = new(10, 12, 10) }, new SpawnIntent { Team = "blue-team", Point = new(-10, 12, -10) }],
            Build = new BuildIntent { VoidEnforcement = new VoidEnforcementIntent { Exclusions = [new Rect(-2, 58, 2, 62)] } },
        };
        IntentGenerator.Apply(doc, intent);

        // no build area declared, but the enforcement rule is there
        await Assert.That(((Dict)doc["regions"]!).ContainsKey("not-build-area")).IsFalse();
        await Assert.That(((Dict)doc["regions"]!).ContainsKey("build-area")).IsFalse();

        var xml = XmlWriter.ToXml(Deserializer.FromDict(doc));
        await Assert.That(xml).Contains("block-place=\"deny(void)\"");
        await Assert.That(xml).Contains("<negative id=\"void-enforcement-area\">");
        await Assert.That(xml).DoesNotContain("not-build-area");

        // re-parse — proves it's well-formed and PGM-parseable with no build area at all
        var reparsed = Serializer.ToDict(MapParser.ParseXmlString(xml));
        var regions = (Dict)reparsed["regions"]!;
        await Assert.That(regions.ContainsKey("void-enforcement-area")).IsTrue();
        var area = (Dict)regions["void-enforcement-area"]!;
        await Assert.That(area["type"]).IsEqualTo("negative");
        var rules = (List<object?>)reparsed["apply_rules"]!;
        var rule = rules.OfType<Dict>().Single(r => r.GetValueOrDefault("region") as string == "void-enforcement-area");
        await Assert.That(rule["block_place"]).IsEqualTo("deny(void)");
    }

    [Test]
    public async Task A_team_id_says_team_while_every_other_id_keeps_the_bare_colour()
    {
        // An intent whose team ids are bare colours — what the plan compiler emits. The document's own team
        // ids take the -team suffix, and so does every reference to them (the spawn link, the team filter's
        // body, the wool's owner, its monument). Region and filter ids are named from the slug, so they are
        // untouched: `red-spawn-point`, `only-red`, `red-wool` read the same either way.
        var doc = BaseDoc();
        IntentGenerator.Apply(doc, new MapIntent
        {
            Meta = new MetaIntent { Name = "Bare" },
            Teams = [new TeamDef { Id = "red", Name = "Red", Color = "red" }, new TeamDef { Id = "blue", Name = "Blue", Color = "blue" }],
            Spawns = [new SpawnIntent { Team = "red", Point = new(10, 12, 10), Protection = [new(0, 0, 20, 20)] }],
            Wools = [new WoolIntent { Owner = "red", Room = [new(5, 5, 15, 15)], Spawn = new(10.5, 13, 10.5),
                Monuments = [new MonumentIntent { Team = "blue", Location = new(-10, 13, -10) }] }],
        });

        var xml = XmlWriter.ToXml(Deserializer.FromDict(doc));
        await Assert.That(xml).Contains("<team id=\"red-team\" color=\"red\"");
        await Assert.That(xml).Contains("<spawn team=\"red-team\"");
        await Assert.That(xml).Contains("<team id=\"only-red\">red-team</team>");
        // A <wool> is written once per capturing team, so its `team` is the monument's, not the owner's.
        await Assert.That(xml).Contains("<wool team=\"blue-team\" color=\"red\"");
        await Assert.That(xml).Contains("id=\"red-spawn-point\"");
        await Assert.That(xml).Contains("id=\"red-wool\"");

        // Re-parsing agrees: the team the spawn names is a team the map declares.
        var map = MapParser.ParseXmlString(xml);
        await Assert.That(map.Teams.Select(t => t.Id)).Contains("red-team");
        await Assert.That(map.Spawns.Select(s => s.Team)).Contains("red-team");
    }
}
