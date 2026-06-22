using NetTopologySuite.Geometries;
using PgmStudio.Analysis.Footprint;

namespace PgmStudio.Analysis.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>Anchor-based gameplay role classification: spawn → Team, wool-only → Objective, anchorless in
/// build → Neutral, anchorless outside build → Decorative. Monuments and economy spawners are not anchors.</summary>
public sealed class IslandRoleClassifierTests
{
    private static readonly GeometryFactory Gf = new();
    private static Geometry Box(double x0, double z0, double x1, double z1)
        => (Polygon)Gf.ToGeometry(new Envelope(x0, x1, z0, z1));
    private static Geometry Pt(double x, double z) => Gf.CreatePoint(new Coordinate(x, z));

    [Test]
    public async Task Classify_assignsRolesByAnchorAndBuildRegion()
    {
        var teamIsland = Box(0, 0, 10, 10);
        var woolIsland = Box(100, 0, 110, 10);
        var midIsland = Box(50, 0, 56, 6);
        var decorIsland = Box(50, 200, 56, 206);
        var islands = new List<Geometry> { teamIsland, woolIsland, midIsland, decorIsland };

        var anchors = new List<IslandRoleClassifier.Anchor>
        {
            new(IsSpawn: true, Pt(5, 5)),     // spawn inside teamIsland
            new(IsSpawn: false, Pt(105, 5)),  // wool inside woolIsland
        };
        var buildRegion = Box(40, -5, 70, 60);   // covers midIsland, not decorIsland

        var roles = IslandRoleClassifier.Classify(islands, anchors, buildRegion);

        await Assert.That(roles[0]).IsEqualTo(IslandGameplayRole.Team);
        await Assert.That(roles[1]).IsEqualTo(IslandGameplayRole.Objective);
        await Assert.That(roles[2]).IsEqualTo(IslandGameplayRole.Neutral);
        await Assert.That(roles[3]).IsEqualTo(IslandGameplayRole.Decorative);
    }

    [Test]
    public async Task Assess_reportsRoleAndTheAnchorsEachIslandCarries()
    {
        var teamIsland = Box(0, 0, 10, 10);     // holds a spawn AND a wool (a home island with its own wool)
        var woolIsland = Box(100, 0, 110, 10);  // holds a wool only
        var midIsland = Box(50, 0, 56, 6);      // anchorless, in the build region
        var islands = new List<Geometry> { teamIsland, woolIsland, midIsland };

        var anchors = new List<IslandRoleClassifier.Anchor>
        {
            new(IsSpawn: true, Pt(5, 5)),       // spawn on teamIsland
            new(IsSpawn: false, Pt(8, 8)),      // wool on teamIsland
            new(IsSpawn: false, Pt(105, 5)),    // wool on woolIsland
        };
        var assessed = IslandRoleClassifier.Assess(islands, anchors, buildRegion: Box(40, -5, 70, 60));

        // roles agree with Classify
        await Assert.That(assessed[0].Role).IsEqualTo(IslandGameplayRole.Team);
        await Assert.That(assessed[1].Role).IsEqualTo(IslandGameplayRole.Objective);
        await Assert.That(assessed[2].Role).IsEqualTo(IslandGameplayRole.Neutral);

        // a team island carries every anchor on it (spawn + its wool); the wool island just its wool; mid none
        await Assert.That(assessed[0].Anchors.Count).IsEqualTo(2);
        await Assert.That(assessed[0].Anchors.Count(a => a.IsSpawn)).IsEqualTo(1);
        await Assert.That(assessed[1].Anchors.Count).IsEqualTo(1);
        await Assert.That(assessed[1].Anchors[0].IsSpawn).IsFalse();
        await Assert.That(assessed[2].Anchors).IsEmpty();
    }

    [Test]
    public async Task Classify_anchorlessIsNeutralWhenNoBuildRegionKnown()
    {
        var roles = IslandRoleClassifier.Classify([Box(0, 0, 5, 5)], [], buildRegion: null);
        await Assert.That(roles[0]).IsEqualTo(IslandGameplayRole.Neutral);
    }

    [Test]
    public async Task ExtractAnchors_takesSpawnAndWoolSignals_notMonuments()
    {
        var doc = new Dict
        {
            ["regions"] = new Dict
            {
                ["red-spawn-point"] = Rect(0, 0, 2, 2),
                ["red-spawn"] = Rect(-2, -2, 4, 4),         // spawn protection
                ["red-room"] = Rect(20, 0, 24, 4),          // wool room
                ["wool-disp"] = Rect(20, 0, 21, 1),         // wool spawner region
            },
            ["spawns"] = new List<object?> { new Dict { ["team"] = "red", ["region"] = "red-spawn-point" } },
            ["apply_rules"] = new List<object?>
            {
                new Dict { ["enter"] = "only-red", ["region"] = "red-spawn" },     // spawn protection
                new Dict { ["enter"] = "not-red", ["region"] = "red-room" },       // own-room rule (NOT a spawn anchor)
            },
            ["wools"] = new List<object?>
            {
                new Dict
                {
                    ["location"] = new Dict { ["x"] = 22.5, ["z"] = 2.5 },
                    ["wool_room_region"] = "red-room",
                    ["monuments"] = new List<object?> { new Dict { ["location"] = new Dict { ["x"] = 500, ["z"] = 500 } } },
                },
            },
            ["spawners"] = new List<object?>
            {
                new Dict { ["spawn_region"] = "wool-disp", ["items"] = new List<object?> { new Dict { ["material"] = "wool" } } },
                new Dict { ["spawn_region"] = "wool-disp", ["items"] = new List<object?> { new Dict { ["material"] = "gold nugget" } } },
            },
        };

        var anchors = IslandRoleClassifier.ExtractAnchors(doc, (-50, -50, 50, 50));

        // spawn-type: spawn point + spawn protection (not the not-red own-room rule)
        await Assert.That(anchors.Count(a => a.IsSpawn)).IsEqualTo(2);
        // wool-type: location + wool room + the wool-dispensing spawner (NOT the gold-nugget spawner)
        await Assert.That(anchors.Count(a => !a.IsSpawn)).IsEqualTo(3);
        // the monument at (500,500) is far outside any anchor footprint
        await Assert.That(anchors.All(a => !a.Geom.Intersects(Pt(500, 500)))).IsTrue();
    }

    [Test]
    public async Task DispensesWool_distinguishesWoolFromEconomySpawners()
    {
        await Assert.That(IslandRoleClassifier.DispensesWool(
            new Dict { ["items"] = new List<object?> { new Dict { ["material"] = "white wool" } } })).IsTrue();
        await Assert.That(IslandRoleClassifier.DispensesWool(
            new Dict { ["items"] = new List<object?> { new Dict { ["material"] = "gold nugget" } } })).IsFalse();
    }

    private static Dict Rect(double x0, double z0, double x1, double z1) => new()
    {
        ["type"] = "rectangle",
        ["min"] = new Dict { ["x"] = x0, ["z"] = z0 },
        ["max"] = new Dict { ["x"] = x1, ["z"] = z1 },
    };
}
