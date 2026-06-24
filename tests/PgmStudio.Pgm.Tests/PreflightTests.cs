using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The Review-phase pre-flight checks (<see cref="Preflight"/>): a generated map round-trips through the
/// export codec with no field lost, and the categorizer recovers the intent's classification (generator
/// and categorizer are inverses). Buildability + traversability are world-data checks composed by the
/// endpoint, not here.
/// </summary>
public sealed class PreflightTests
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
        Spawns =
        [
            new SpawnIntent { Team = "red-team", Point = new(100, 12, 50), Protection = [new(90, 40, 110, 60)] },
            new SpawnIntent { Team = "blue-team", Point = new(-100, 12, -50), Protection = [new(-110, -60, -90, -40)] },
        ],
        Observer = new ObserverIntent { Point = new(0, 60, 0) },
        Build = new BuildIntent { MaxHeight = 30, Areas = [new Rect(0, 0, 50, 50), new Rect(-50, -50, 0, 0)] },
        Wools =
        [
            new WoolIntent { Owner = "red-team", Room = [new(95, 45, 105, 55)], Spawn = new(100.5, 13, 50.5),
                Monuments = [new MonumentIntent { Team = "blue-team", Location = new(-100, 13, -50) }] },
            new WoolIntent { Owner = "blue-team", Room = [new(-105, -55, -95, -45)], Spawn = new(-100.5, 13, -50.5),
                Monuments = [new MonumentIntent { Team = "red-team", Location = new(100, 13, 50) }] },
        ],
    };

    private static Dict Generated(MapIntent intent)
    {
        var doc = BaseDoc();
        IntentGenerator.Apply(doc, intent);
        return doc;
    }

    [Test]
    public async Task RoundTrip_passes_for_a_generated_map()
    {
        var check = Preflight.RoundTrip(Generated(FullIntent()));
        await Assert.That(check.Status).IsEqualTo("pass");
        await Assert.That(check.Key).IsEqualTo("round-trip");
    }

    [Test]
    public async Task RoundTrip_fails_when_the_codec_rejects_the_document()
    {
        // A rectangle region missing its required bounds_2d — the export codec throws, so the check fails.
        var doc = BaseDoc();
        ((Dict)doc["regions"]!)["bad"] = new Dict { ["id"] = "bad", ["type"] = "rectangle" };
        var check = Preflight.RoundTrip(doc);
        await Assert.That(check.Status).IsEqualTo("fail");
    }

    [Test]
    public async Task Mirror_recovers_every_declared_classification()
    {
        var intent = FullIntent();
        var check = Preflight.Mirror(Generated(intent), intent);
        await Assert.That(check.Status).IsEqualTo("pass");
        // the four classifications the categorizer must read back
        await Assert.That(check.Detail).Contains("spawn/protection");
        await Assert.That(check.Detail).Contains("wool/room");
        await Assert.That(check.Detail).Contains("build");
        await Assert.That(check.Detail).Contains("wool/monument");
    }

    [Test]
    public async Task Mirror_does_not_demand_classifications_the_intent_omits()
    {
        // No protection, no rooms, no build, no monuments → nothing to recover, so the check still passes.
        var intent = new MapIntent
        {
            Meta = new MetaIntent { Name = "Bare" },
            Teams = [new TeamDef { Id = "red-team", Name = "Red", Color = "red" }, new TeamDef { Id = "blue-team", Name = "Blue", Color = "blue" }],
            Spawns = [new SpawnIntent { Team = "red-team", Point = new(10, 12, 10) }, new SpawnIntent { Team = "blue-team", Point = new(-10, 12, -10) }],
        };
        var check = Preflight.Mirror(Generated(intent), intent);
        await Assert.That(check.Status).IsEqualTo("pass");
    }

    [Test]
    public async Task Mirror_fails_when_a_declared_classification_is_missing()
    {
        // The intent declares spawn protection, but the document has no regions → the categorizer can't
        // recover spawn/protection, so the inverse property is violated.
        var intent = FullIntent();
        var check = Preflight.Mirror(BaseDoc(), intent);
        await Assert.That(check.Status).IsEqualTo("fail");
        await Assert.That(check.Detail).Contains("spawn/protection");
    }
}
