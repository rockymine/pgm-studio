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

    private const string ClayBed = "\"bed_material\": { \"kind\": \"solid\", \"id\": 82, \"data\": 0 },";

    [Test]
    public async Task A_river_lies_on_the_bed_it_states_and_one_that_states_none_on_the_default_bank()
    {
        var stated = BuiltWorlds.Of(Board(River.Replace("\"op\": \"river\",", "\"op\": \"river\"," + ClayBed)), Intent());
        var plain = BuiltWorlds.Of(Board(River), Intent());

        foreach (var x in new[] { -20, 0, 20 })
        {
            await Assert.That(stated.World.GetBlock(x, 7, 25).Id).IsEqualTo(Blocks.Clay);
            await Assert.That(new[] { Blocks.Gravel, Blocks.Dirt, Blocks.Sand }).Contains(plain.World.GetBlock(x, 7, 25).Id);
        }
    }

    [Test]
    public async Task Two_waters_each_lie_on_their_own_bed()
    {
        const string sandLake =
            """
            { "id": "lake", "op": "lake", "level": 9, "depth": 3, "shore": 3,
              "bed_material": { "kind": "solid", "id": 12, "data": 0 },
              "mask": { "areas": [ { "kind": "ellipse", "at": [-20.5, -20.5], "rx": 8, "rz": 8 } ], "mirror": false } }
            """;
        var built = BuiltWorlds.Of(Board(River.Replace("\"op\": \"river\",", "\"op\": \"river\"," + ClayBed) + "," + sandLake), Intent());

        await Assert.That(built.World.GetBlock(0, 7, 25).Id).IsEqualTo(Blocks.Clay);
        await Assert.That(built.World.GetBlock(-21, 5, -21).Id).IsEqualTo(Blocks.Sand);
    }

    [Test]
    public async Task A_river_run_to_the_edge_of_the_ground_falls_off_it()
    {
        var built = BuiltWorlds.Of(Board(River), Intent());

        foreach (var past in new[] { -41, 40 })
        {
            for (var y = 0; y <= 8; y++)
                await Assert.That(built.World.GetBlock(past, y, 25)).IsEqualTo((Blocks.Water, 8)).Because($"y{y} past the end of the river");
            await Assert.That(built.World.GetBlock(past, 9, 25).Id).IsEqualTo(Blocks.Air);
        }
    }

    [Test]
    public async Task A_river_that_stops_short_of_the_edge_pours_nothing()
    {
        var short_ = River.Replace("[[-39.5, 25.5], [39.5, 25.5]]", "[[-30.5, 25.5], [30.5, 25.5]]");
        var built = BuiltWorlds.Of(Board(short_), Intent());

        var flowing = 0;
        for (var x = -45; x <= 45; x++)
        for (var z = 15; z <= 35; z++)
        for (var y = 0; y <= 12; y++)
            if (built.World.GetBlock(x, y, z).Id == Blocks.Water) flowing++;
        await Assert.That(flowing).IsEqualTo(0);
        await Assert.That(Water(built, 0, 8, 25)).IsEqualTo(1);
    }

    /// <summary>The highest block that is neither air nor water in a column, from the top of the plate down.</summary>
    private static int TopSolid(BuiltWorld built, int x, int z)
    {
        for (var y = 30; y >= 0; y--)
        {
            var id = built.World.GetBlock(x, y, z).Id;
            if (id != Blocks.Air && id != Blocks.StationaryWater) return id;
        }
        return Blocks.Air;
    }

    [Test]
    public async Task A_paint_rule_near_a_river_lays_its_bank_and_leaves_the_rest_of_the_board_to_its_theme()
    {
        var layout = Board(River).TrimEnd().TrimEnd('}') + """
            , "paint": [ { "id": "bank", "material": { "kind": "solid", "id": 12 }, "near": { "modifier": "river", "width": 6 } } ] }
            """;
        var built = BuiltWorlds.Of(layout, Intent());
        var plain = BuiltWorlds.Of(Board(River), Intent());

        // The water's own beach is laid over the first blocks of the bank after the paint; past it the rule shows.
        foreach (var z in new[] { 31, 32, 33 })
            await Assert.That((z, TopSolid(built, 0, z))).IsEqualTo((z, Blocks.Sand)).Because("the bank lies within six of the band");
        await Assert.That(TopSolid(built, 0, 36)).IsNotEqualTo(Blocks.Sand).Because("past the width the theme paints");
        await Assert.That(TopSolid(built, 0, 0)).IsEqualTo(TopSolid(plain, 0, 0)).Because("far from the river the theme paints");
        await Assert.That(TopSolid(plain, 0, 32)).IsNotEqualTo(Blocks.Sand);
    }
}
