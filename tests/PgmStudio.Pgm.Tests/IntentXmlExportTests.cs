using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

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
        Meta = new MetaIntent { Name = "Test Map" },
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
        // the observer (<default>) spawn is emitted with its yaw (team spawns here have yaw 0 → omitted)
        await Assert.That(xml).Contains("<default");
        await Assert.That(xml).Contains("yaw=\"180\"");

        // spawn protection: infinite damage-resistance in spawn + a force reset kit applied outside it
        await Assert.That(xml).Contains("<effect duration=\"oo\" amplifier=\"100\">damage resistance</effect>");
        await Assert.That(xml).Contains("<kit id=\"reset-resistance-kit\" force=\"true\">");
        await Assert.That(xml).Contains("<apply kit=\"reset-resistance-kit\" region=\"not-spawns\"/>");

        // re-parse the generated XML — proves it's well-formed and PGM-parseable
        var reparsed = Serializer.ToDict(MapParser.ParseXmlString(xml));
        await Assert.That(((List<object?>)reparsed["teams"]!).Count).IsEqualTo(2);
        await Assert.That(((List<object?>)reparsed["spawns"]!).Count).IsEqualTo(2);
        await Assert.That(((List<object?>)reparsed["wools"]!).Count).IsEqualTo(2);
        await Assert.That(((List<object?>)reparsed["spawners"]!).Count).IsEqualTo(2);
        await Assert.That(reparsed["objective"]).IsEqualTo("Capture the enemies' wools!");
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
}
