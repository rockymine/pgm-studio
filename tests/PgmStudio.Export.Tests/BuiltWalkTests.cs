using PgmStudio.Analysis.Playability;
using PgmStudio.Analysis.Scan;
using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The ground a built board is walked over is the world the export writes: what stands in it is what a player
/// meets, and what a player may bridge is what the map document grants.
/// </summary>
public sealed class BuiltWalkTests
{
    /// <summary>One plate, x −60..60 and z −20..20, cut across at x 0 by a channel of lava wider than a jump
    /// and running past both of its edges.</summary>
    private const string LavaAcross =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
          {"id":"a","type":"rectangle","operation":"add","min_x":-60,"min_z":-20,"max_x":60,"max_z":20,"base_height":8}],
         "groups":[]} }],
         "dressing":{"styles":{},"props":[
          {"kind":"fluid","id":"moat","fluid":"lava","points":[[0,-30],[0,30]],"radius":3,"depth":2}]}}
        """;

    private static MapIntent Intent() => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns = [new SpawnIntent { Team = "red", Point = new Pt(-40, 9, 0), Yaw = 0 }],
        Wools = [new WoolIntent { Owner = "blue", Color = "blue", Spawn = new Pt(40, 9, 0) }],
    };

    private static Dict Doc() => new() { ["name"] = "m", ["version"] = "1.0.0" };

    private static Dict Xz(double x, double z) => new() { ["x"] = x, ["z"] = z };

    /// <summary>A rule over the channel that forbids placing a block in it — a moat the author means to hold.</summary>
    private static void NoPlacingInTheMoat(Dict doc)
    {
        var regions = doc.GetValueOrDefault("regions") as Dict ?? [];
        regions["moat"] = new Dict
        {
            ["type"] = "rectangle", ["min"] = Xz(-4, -20), ["max"] = Xz(5, 21),
            ["bounds_2d"] = new Dict { ["min"] = Xz(-4, -20), ["max"] = Xz(5, 21) },
        };
        doc["regions"] = regions;
        var rules = doc.GetValueOrDefault("apply_rules") as List<object?> ?? [];
        rules.Insert(0, new Dict { ["region"] = "moat", ["block_place"] = "never" });
        doc["apply_rules"] = rules;
    }

    /// <summary>The plate's terrain runs unbroken under the channel, so a walk over the rasterized columns
    /// crosses it dry. The world holds lava there, and where nobody may place a block in it nobody crosses.</summary>
    [Test]
    public async Task A_channel_of_lava_nobody_may_build_in_cuts_the_walk_the_terrain_alone_would_cross()
    {
        var intent = Intent();
        var built = BuiltWorlds.Of(LavaAcross, intent);
        var doc = Doc();
        IntentGenerator.Apply(doc, built.ResolvedIntent);
        NoPlacingInTheMoat(doc);

        var terrain = new SegmentIndex(built.Columns!.Select(column => (column.X, column.Z, column.YFloor, column.YTop)));
        await Assert.That(Traversability.Check(doc, WorldWalk.Ground(doc, terrain)).Connected).IsTrue()
            .Because("the terrain alone runs dry under the channel");

        var walked = Traversability.Check(doc, BuiltWalk.Ground(built, doc, LavaAcross));
        await Assert.That(walked.HaveLayers).IsTrue();
        await Assert.That(walked.Connected).IsFalse();

        var result = MapExportComposer.BuildAndCompose(Doc(), LavaAcross, intent, decorate: NoPlacingInTheMoat);
        await Assert.That(result.Refusal?.Error).IsEqualTo("not traversable");
    }

    /// <summary>A block placed into lava replaces it, and on ground at y=0 nothing forbids placing one, so the
    /// same channel on a board with no rule over it is crossed a block a column.</summary>
    [Test]
    public async Task Lava_over_ground_a_player_may_build_on_is_bridged()
    {
        var intent = Intent();
        var built = BuiltWorlds.Of(LavaAcross, intent);
        var doc = Doc();
        IntentGenerator.Apply(doc, built.ResolvedIntent);

        var ground = BuiltWalk.Ground(built, doc, LavaAcross);
        await Assert.That(Traversability.Check(doc, ground).Connected).IsTrue();
        await Assert.That(ground.Ground.Any(place => place.X == 0 && place.Z == 0)).IsFalse()
            .Because("nobody stands in lava");
        await Assert.That(ground.Bridgeable.Any(place => place.X == 0 && place.Z == 0)).IsTrue();
    }
}
