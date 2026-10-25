using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The carves of the modifier stack reach the built world: a tunnel runs through a hill on its level floor under
/// the ground that stays over it, its floor dressed, and a shaft opens to the sky with a ladder up its wall.
/// </summary>
public sealed class CarveWorldTests
{
    /// <summary>A plate ten thick with a hill raised twenty over x −10 to 10, and the carves given.</summary>
    private static string Board(string carves) =>
        $$$"""
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},"layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
          {"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":10}],
         "groups":[]} }],
         "modifiers": [
           { "id": "hill", "op": "level", "top": 30, "inner": 1, "outer": 1,
             "mask": { "areas": [ { "kind": "polygon", "points": [[-10, -20], [10, -20], [10, 20], [-10, 20]] } ], "mirror": false } },
           {{{carves}}} ]}
        """;

    private const string Tunnel =
        """
        { "id": "tunnel", "op": "tunnel", "seed": 3,
          "mask": { "areas": [ { "kind": "line", "points": [[-20.5, 0.5], [20.5, 0.5]], "width": 5 } ], "mirror": false } }
        """;

    private static MapIntent Intent() => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns = [new SpawnIntent { Team = "red", Point = new Pt(-30, 11, -30), Yaw = 0 }],
        Wools = [new WoolIntent { Owner = "blue", Color = "blue", Spawn = new Pt(30, 11, 30) }],
    };

    [Test]
    public async Task A_tunnel_runs_through_the_hill_under_the_ground_that_stays_over_it()
    {
        var built = BuiltWorlds.Of(Board(Tunnel), Intent());
        var plain = BuiltWorlds.Of(Board(Tunnel.Replace("\"op\": \"tunnel\",", "\"op\": \"tunnel\", \"off\": true,")), Intent());

        await Assert.That(built.World.GetBlock(0, 11, 0).Id).IsEqualTo(Blocks.Air).Because("the tube runs through the middle of the hill");
        await Assert.That(plain.World.GetBlock(0, 11, 0).Id).IsNotEqualTo(Blocks.Air);
        await Assert.That(built.World.GetBlock(0, 29, 0).Id).IsNotEqualTo(Blocks.Air).Because("the hill's top stays over it");
        await Assert.That(built.World.GetBlock(0, 26, 0).Id).IsNotEqualTo(Blocks.Air);
        await Assert.That(built.World.GetBlock(0, 9, 0).Id).IsNotEqualTo(Blocks.Air).Because("the floor is solid, and dressed");
        await Assert.That(built.World.GetBlock(-9, 11, 0).Id).IsEqualTo(Blocks.Air).Because("it is open a block inside the hill's foot");
    }

    [Test]
    public async Task A_shaft_opens_to_the_sky_with_a_ladder_up_its_wall()
    {
        var built = BuiltWorlds.Of(Board("""
            { "id": "shaft", "op": "shaft", "floor": 14,
              "mask": { "areas": [ { "kind": "ellipse", "at": [0.5, 0.5], "rx": 2, "rz": 2 } ], "mirror": false } }
            """), Intent());

        await Assert.That(built.World.GetBlock(0, 29, 0).Id).IsEqualTo(Blocks.Air);
        await Assert.That(built.World.GetBlock(0, 14, 0).Id).IsEqualTo(Blocks.Air);
        var ladders = Enumerable.Range(14, 16).Count(y => Enumerable.Range(-3, 7).Any(z => built.World.GetBlock(0, y, z).Id == Blocks.Ladder));
        await Assert.That(ladders).IsGreaterThan(12).Because("a ladder climbs from its floor to the sky");
    }
}
