using PgmStudio.Pgm;
using PgmStudio.Pgm.Editing;

namespace PgmStudio.Analysis.Tests;

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
            new SpawnIntent { Team = "red-team", Point = new(100, 12, 50), Protection = new(90, 40, 110, 60) },
            new SpawnIntent { Team = "blue-team", Point = new(-100, 12, -50), Protection = new(-110, -60, -90, -40) },
        ],
        Observer = new ObserverIntent { Point = new(0, 60, 0), Yaw = 180 },
        Build = new BuildIntent { MaxHeight = 30, Areas = [new Rect(0, 0, 50, 50), new Rect(-50, -50, 0, 0)] },
        Wools =
        [
            new WoolIntent { Owner = "red-team", Room = new(95, 45, 105, 55), Spawn = new(100.5, 13, 50.5),
                Monuments = [new MonumentIntent { Team = "blue-team", Location = new(-100, 13, -50) }] },
            new WoolIntent { Owner = "blue-team", Room = new(-105, -55, -95, -45), Spawn = new(-100.5, 13, -50.5),
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

        // re-parse the generated XML — proves it's well-formed and PGM-parseable
        var reparsed = Serializer.ToDict(MapParser.ParseXmlString(xml));
        await Assert.That(((List<object?>)reparsed["teams"]!).Count).IsEqualTo(2);
        await Assert.That(((List<object?>)reparsed["spawns"]!).Count).IsEqualTo(2);
        await Assert.That(((List<object?>)reparsed["wools"]!).Count).IsEqualTo(2);
        await Assert.That(((List<object?>)reparsed["spawners"]!).Count).IsEqualTo(2);
        await Assert.That(reparsed["objective"]).IsEqualTo("Capture the enemies' wools!");
        // build void enforcement survived
        await Assert.That(((Dict)reparsed["regions"]!).ContainsKey("not-build-area")).IsTrue();
    }
}
