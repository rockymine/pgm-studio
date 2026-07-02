using PgmStudio.Api.Services;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The sketch → world assembly (no DB): a rectangular layout + a 2-team intent yields terrain, wool cages,
/// spawn cubes with auto-derived monuments, and an observer platform — plus a resolved intent whose spawns
/// are integer-snapped and whose monument locations point at the world air cells.
/// </summary>
public sealed class SketchWorldBuilderTests
{
    private const string Layout =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":1}],"islands":[]}}
        """;

    private static MapIntent SampleIntent() => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
        Spawns =
        [
            new SpawnIntent { Team = "red", Point = new Pt(-20, 1, 0), Yaw = 0 },
            new SpawnIntent { Team = "blue", Point = new Pt(20, 1, 0), Yaw = 180 },
        ],
        Wools =
        [
            new WoolIntent { Owner = "red", Color = "red", Spawn = new Pt(-10, 1, 10), Monuments = [new MonumentIntent { Team = "blue" }] },
            new WoolIntent { Owner = "blue", Color = "blue", Spawn = new Pt(10, 1, 10), Monuments = [new MonumentIntent { Team = "red" }] },
        ],
        Observer = new ObserverIntent { Point = new Pt(0, 20, 0), Yaw = 0 },
        Meta = new MetaIntent { Name = "Test", Authors = ["alice"] },
    };

    [Test]
    public async Task Builds_terrain_cages_and_a_floating_observer_spawn()
    {
        var built = SketchWorldBuilder.Build(Layout, SampleIntent());

        // Terrain: bedrock floor at y=0 under the footprint.
        await Assert.That(built.World.GetBlock(0, 0, 0)).IsEqualTo((Blocks.Bedrock, 0));

        // Observer platform floats at the authored Y=20; world spawn stands on it (21).
        await Assert.That(built.SpawnY).IsEqualTo(21);
        await Assert.That(built.World.GetBlock(0, 20, 0)).IsEqualTo((Blocks.Bedrock, 0));   // platform floor

        // Wool cage at the (snapped) red wool spawn: 2×2 wool marker on the floor (surface top y=1).
        await Assert.That(built.World.GetBlock(-10, 1, 10)).IsEqualTo((Blocks.Wool, 14));   // red = 14
    }

    [Test]
    public async Task Resolves_snapped_spawns_and_derived_monument_locations()
    {
        var built = SketchWorldBuilder.Build(Layout, SampleIntent());
        var resolved = built.ResolvedIntent;

        // Spawns snapped to whole integers, Y = floor + 1 (standing on the cube floor).
        await Assert.That(resolved.Spawns[0].Point).IsEqualTo(new Pt(-20, 2, 0));

        // The red wool (captured by blue) has its monument located inside blue's spawn cube (near x=20),
        // and the world has a bedrock pedestal below + glass cap above that air cell.
        var mon = resolved.Wools[0].Monuments[0];
        await Assert.That(mon.Team).IsEqualTo("blue");
        await Assert.That(Math.Abs(mon.Location.X - 20) <= 4).IsTrue();

        var (mx, my, mz) = ((int)mon.Location.X, (int)mon.Location.Y, (int)mon.Location.Z);
        await Assert.That(built.World.GetBlock(mx, my, mz)).IsEqualTo((Blocks.Air, 0));           // placement cell
        await Assert.That(built.World.GetBlock(mx, my - 1, mz)).IsEqualTo((Blocks.Bedrock, 0));   // pedestal
        await Assert.That(built.World.GetBlock(mx, my + 1, mz).Id).IsEqualTo(Blocks.StainedGlass); // cap
    }
}
