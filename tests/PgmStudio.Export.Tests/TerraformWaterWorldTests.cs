using PgmStudio.Analysis.Playability;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The water a river or a lake of the modifier stack makes reaches the built world: it stands in the blocks over
/// the bed the stack cut, up to the surface the modifier states, and every read of the world and the walk over it
/// see it.
/// </summary>
public sealed class TerraformWaterWorldTests
{
    private static string Board(string modifiers) =>
        $$$"""
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},"layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
          {"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":10}],
         "groups":[]} }],
         "modifiers": [ {{{modifiers}}} ]}
        """;

    /// <summary>A river across the plate at z 25, four wide, its bed two under the ground with a block of water in it.</summary>
    private const string River =
        """
        { "id": "river", "op": "river", "depth": 2, "water": 1, "bank": 3,
          "mask": { "areas": [ { "kind": "line", "points": [[-39.5, 25.5], [39.5, 25.5]], "width": 4 } ], "mirror": false } }
        """;

    private const string Lake =
        """
        { "id": "lake", "op": "lake", "level": 9, "depth": 3, "shore": 3,
          "mask": { "areas": [ { "kind": "ellipse", "at": [-20.5, -20.5], "rx": 8, "rz": 8 } ], "mirror": false } }
        """;

    private static MapIntent Intent() => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns = [new SpawnIntent { Team = "red", Point = new Pt(0, 11, 0), Yaw = 0 }],
        Wools = [new WoolIntent { Owner = "blue", Color = "blue", Spawn = new Pt(20, 11, 0) }],
    };

    private static int Water(BuiltWorld built, int x, int y, int z) =>
        built.World.GetBlock(x, y, z).Id == Blocks.StationaryWater ? 1 : 0;

    [Test]
    public async Task A_river_fills_the_bed_the_stack_cut_to_its_surface_and_no_higher()
    {
        var built = BuiltWorlds.Of(Board(River), Intent());

        // Ground ten thick, so its top block is y9; the bed is cut two down and holds a block of water.
        await Assert.That(built.World.GetBlock(0, 7, 25).Id).IsNotEqualTo(Blocks.StationaryWater);
        await Assert.That(built.World.GetBlock(0, 7, 25).Id).IsNotEqualTo(Blocks.Air).Because("the bed is solid");
        await Assert.That(Water(built, 0, 8, 25)).IsEqualTo(1);
        await Assert.That(built.World.GetBlock(0, 9, 25).Id).IsEqualTo(Blocks.Air).Because("the water stops at the surface");
        await Assert.That(Water(built, 0, 8, 23)).IsEqualTo(1).Because("the channel is two blocks either side of the line");
        await Assert.That(Water(built, 0, 8, 28)).IsEqualTo(0).Because("a bank stands beside it");
    }

    [Test]
    public async Task A_river_run_with_the_stack_switched_off_leaves_the_world_dry()
    {
        var off = Board(River.Replace("\"op\": \"river\",", "\"op\": \"river\", \"off\": true,"));
        var built = BuiltWorlds.Of(off, Intent());

        await Assert.That(Water(built, 0, 8, 25)).IsEqualTo(0);
        var columns = PgmStudio.Minecraft.Anvil.WorldColumns.Of(built.World);
        await Assert.That(columns.Any(column => column.Runs.Any(run => run.BlockId == Blocks.StationaryWater))).IsFalse();
    }

    [Test]
    public async Task A_lake_stands_at_its_level_over_the_bowl_the_stack_cut()
    {
        var built = BuiltWorlds.Of(Board(Lake), Intent());

        // The surface 9 is one under the ground's top; the bowl is cut three under it in the middle.
        await Assert.That(Water(built, -21, 8, -21)).IsEqualTo(1);
        await Assert.That(Water(built, -21, 7, -21)).IsEqualTo(1);
        await Assert.That(Water(built, -21, 9, -21)).IsEqualTo(0).Because("the surface is the level the lake states");
        await Assert.That(Water(built, 20, 8, 20)).IsEqualTo(0);
    }

    [Test]
    public async Task Ground_a_later_modifier_raises_over_the_surface_holds_no_water()
    {
        const string plateau = """{ "id": "plateau", "op": "level", "top": 14, "mask": { "areas": [ { "kind": "ellipse", "at": [0.5, 25.5], "rx": 6, "rz": 6 } ], "mirror": false } }""";
        var built = BuiltWorlds.Of(Board(River + "," + plateau), Intent());

        await Assert.That(Water(built, 0, 8, 25)).IsEqualTo(0);
        await Assert.That(Water(built, 0, 13, 25)).IsEqualTo(0);
        await Assert.That(Water(built, 30, 8, 25)).IsEqualTo(1).Because("the river runs on past the plateau");
    }

    [Test]
    public async Task The_water_a_walk_swims_includes_the_water_the_stack_made()
    {
        var built = BuiltWorlds.Of(Board(River), Intent());
        var water = BuiltWalk.Ground(built, new Dictionary<string, object?> { ["name"] = "m", ["version"] = "1.0.0" }).Water!;

        await Assert.That(water.Contains((0, 25))).IsTrue();
        await Assert.That(water.Contains((0, 5))).IsFalse();
    }
}
