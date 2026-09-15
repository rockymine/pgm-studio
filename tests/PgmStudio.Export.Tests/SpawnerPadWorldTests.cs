using System.Text.Json;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The block a generator's stack lands on, in the world. A spawner is a place before it is a clock, and the
/// pad is what says so on the ground — the same square every other marked place lays, in whatever block the
/// board named rather than in wool.
///
/// <para>What is asserted is the blocks: the drop's own square carries the stated material and the ground
/// beside it does not, so a pad that was written at the wrong size or the wrong parity shows up here rather
/// than in a coordinate nobody reads.</para>
/// </summary>
public sealed class SpawnerPadWorldTests
{
    private const int Floor = 4, PadY = Floor - 1;

    private static string Ground() => new SketchLayout
    {
        Setup = new SketchSetup { MirrorMode = "rot_180", Center = new SketchCenter { Cx = 0, Cz = 0 } },
        Layers = [SketchLayer.Ground([new SketchShape
        {
            Id = "a", Type = "rectangle", Operation = "add",
            MinX = -40, MinZ = -40, MaxX = 40, MaxZ = 40, BaseHeight = Floor,
        }], [])],
    }.ToJson();

    private static MapIntent Board(params SpawnerIntent[] spawners) => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }],
        Spawns = [new SpawnIntent { Team = "red", Point = new Pt(-30, Floor, -30), Yaw = 0 }],
        Wools = [],
        Observer = new ObserverIntent { Point = new Pt(0, 30, 0), Yaw = 0 },
        Meta = new MetaIntent { Name = "Mint", Authors = [] },
        Spawners = [.. spawners],
    };

    private static SpawnerIntent Mint(double x, double z, string pad) => new()
    {
        Id = "mid", At = new Pt(x, Floor, z), Pad = pad, Drops = [new SpawnerDrop("emerald")],
    };

    /// <summary>A marker at a block's own centre marks that one block, and only that one.</summary>
    [Test]
    public async Task A_block_centre_marker_lays_one_block()
    {
        var world = WorldBuilder.Build(Ground(), Board(Mint(10.5, 10.5, "gold block"))).World;

        await Assert.That(world.GetBlock(10, PadY, 10).Id).IsEqualTo(Blocks.GoldBlock);
        foreach (var (x, z) in new[] { (9, 10), (11, 10), (10, 9), (10, 11) })
            await Assert.That(world.GetBlock(x, PadY, z).Id).IsNotEqualTo(Blocks.GoldBlock);
    }

    /// <summary>A marker on a grid line is the corner four blocks share, so all four are the pad.</summary>
    [Test]
    public async Task A_grid_line_marker_lays_the_four_blocks_it_corners()
    {
        var world = WorldBuilder.Build(Ground(), Board(Mint(10, 10, "gold block"))).World;

        foreach (var (x, z) in new[] { (9, 9), (9, 10), (10, 9), (10, 10) })
            await Assert.That(world.GetBlock(x, PadY, z).Id).IsEqualTo(Blocks.GoldBlock);
        await Assert.That(world.GetBlock(8, PadY, 9).Id).IsNotEqualTo(Blocks.GoldBlock);
        await Assert.That(world.GetBlock(11, PadY, 10).Id).IsNotEqualTo(Blocks.GoldBlock);
    }

    /// <summary>The pad is any block the board names, with its data — a generator is not a spawn and has no
    /// reason to be wool.</summary>
    [Test]
    public async Task The_pad_is_the_block_the_board_named()
    {
        var world = WorldBuilder.Build(Ground(), Board(Mint(10.5, 10.5, "stained clay:14"))).World;

        await Assert.That(world.GetBlock(10, PadY, 10)).IsEqualTo((Blocks.StainedClay, 14));
    }

    /// <summary>A spawner naming no block lays none: a board that built the ground its generator stands on
    /// keeps whatever it built, and the terrain finish is what shows through.</summary>
    [Test]
    public async Task A_spawner_naming_no_block_lays_none()
    {
        var bare = WorldBuilder.Build(Ground(), Board(Mint(10.5, 10.5, ""))).World;
        var none = WorldBuilder.Build(Ground(), Board()).World;

        await Assert.That(bare.GetBlock(10, PadY, 10)).IsEqualTo(none.GetBlock(10, PadY, 10));
    }

    /// <summary>The pad sits one course under the drop, so the stack rests on it: the point the document
    /// names is the pad's centre at the course above.</summary>
    [Test]
    public async Task The_drop_stands_one_course_over_its_pad()
    {
        var built = WorldBuilder.Build(Ground(), Board(Mint(10, 10, "gold block")));
        var drop = SpawnerGenerator.Drop(new Pt(10, Floor, 10));

        await Assert.That(drop).IsEqualTo((10d, 10d));
        await Assert.That(built.World.GetBlock(9, PadY, 9).Id).IsEqualTo(Blocks.GoldBlock);
        await Assert.That(built.World.GetBlock(9, Floor, 9).Id).IsNotEqualTo(Blocks.GoldBlock);
    }
}
