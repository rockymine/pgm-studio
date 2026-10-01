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

    /// <summary>The same plate with water on it: a pool over x and z −10..10, and a hollow dug to a floor at y4
    /// over x 26..40 and z −6..6 with a basin's ring drawn loose round it, over x 20..46, filled to y6.</summary>
    private const string Waters =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
          {"id":"a","type":"rectangle","operation":"add","min_x":-60,"min_z":-20,"max_x":60,"max_z":20,"base_height":8},
          {"id":"dig","type":"rectangle","operation":"subtract","min_x":26,"min_z":-6,"max_x":40,"max_z":6},
          {"id":"floor","type":"rectangle","operation":"add","override":true,"min_x":26,"min_z":-6,"max_x":40,"max_z":6,"base_height":4}],
         "groups":[{"id":"g","name":"Plate","mirrors":false,"shapeIds":["a","dig","floor"]}]} }],
         "dressing":{"styles":{},"props":[
          {"kind":"fluid","id":"mere","shape":"pool","points":[[-10,-10],[10,-10],[10,10],[-10,10]],"radius":3,"depth":2,"shore":0},
          {"kind":"fluid","id":"wash","shape":"basin","level":6,"points":[[20,-10],[46,-10],[46,10],[20,10]],"shore":0}]}}
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

        var walked = Traversability.Check(doc, BuiltWalk.Ground(built, doc));
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

        var ground = BuiltWalk.Ground(built, doc);
        await Assert.That(Traversability.Check(doc, ground).Connected).IsTrue();
        await Assert.That(ground.Ground.Any(place => place.X == 0 && place.Z == 0)).IsFalse()
            .Because("nobody stands in lava");
        await Assert.That(ground.Bridgeable.Any(place => place.X == 0 && place.Z == 0)).IsTrue();
    }

    /// <summary>What a player swims is the water the world holds: a pool across the whole of its inside, and a
    /// basin wherever its ground stood lower than its line and nowhere else inside its ring.</summary>
    [Test]
    public async Task The_water_a_walk_swims_is_the_water_the_world_holds()
    {
        var built = BuiltWorlds.Of(Waters, Intent());
        var water = BuiltWalk.Ground(built, Doc()).Water!;

        await Assert.That(water.Contains((0, 0))).IsTrue().Because("the middle of the pool is water");
        await Assert.That(water.Contains((33, 0))).IsTrue().Because("the hollow is under the basin's line");
        await Assert.That(water.Contains((22, 0))).IsFalse()
            .Because("the plate inside the basin's ring stands above its line, and a basin cuts nothing");
        await Assert.That(water.Contains((-20, 0))).IsFalse().Because("the plate between them is dry");
    }
}
