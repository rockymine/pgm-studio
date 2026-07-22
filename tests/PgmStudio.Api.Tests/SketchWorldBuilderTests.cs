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
        Meta = new MetaIntent { Name = "Test", Authors = [new AuthorIntent { Name = "alice" }] },
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
        var mon = resolved.Wools![0].Monuments[0];
        await Assert.That(mon.Team).IsEqualTo("blue");
        await Assert.That(Math.Abs(mon.Location.X - 20) <= 4).IsTrue();

        var (mx, my, mz) = ((int)mon.Location.X, (int)mon.Location.Y, (int)mon.Location.Z);
        await Assert.That(built.World.GetBlock(mx, my, mz)).IsEqualTo((Blocks.Air, 0));           // placement cell
        await Assert.That(built.World.GetBlock(mx, my - 1, mz)).IsEqualTo((Blocks.Bedrock, 0));   // pedestal
        await Assert.That(built.World.GetBlock(mx, my + 1, mz).Id).IsEqualTo(Blocks.StainedGlass); // cap
    }

    [Test]
    public async Task A_capturer_without_a_spawn_gets_no_monument_rather_than_one_at_the_origin()
    {
        // green has no spawn cube, so it has no placement cell; the auto-wired wool must not emit a
        // green monument at (0,0,0).
        var intent = new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" },
                     new TeamDef { Id = "green", Color = "green" }],
            Spawns =
            [
                new SpawnIntent { Team = "red", Point = new Pt(-20, 1, 0), Yaw = 0 },
                new SpawnIntent { Team = "blue", Point = new Pt(20, 1, 0), Yaw = 180 },
            ],
            Wools = [new WoolIntent { Owner = "red", Color = "red", Spawn = new Pt(-10, 1, 10), Monuments = [] }],
            Observer = new ObserverIntent { Point = new Pt(0, 20, 0), Yaw = 0 },
            Meta = new MetaIntent { Name = "Test", Authors = [new AuthorIntent { Name = "alice" }] },
        };

        var mons = SketchWorldBuilder.Build(Layout, intent).ResolvedIntent.Wools![0].Monuments;

        await Assert.That(mons.Any(m => m.Team == "blue")).IsTrue();           // blue has a spawn → kept
        await Assert.That(mons.Any(m => m.Team == "green")).IsFalse();         // green has no spawn → skipped
        await Assert.That(mons.Any(m => m.Location == new Pt(0, 0, 0))).IsFalse();  // no phantom at origin
    }

    [Test]
    public async Task A_gold_team_gets_an_orange_cube_not_white()
    {
        // "gold" is a chat colour with no wool of its own; it must coerce to orange (damage 1), not the
        // damage-0 white fallback. Its spawn (yaw 0 → door on the +Z wall) leaves the -Z wall strip intact.
        var intent = new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "gold", Color = "gold" }],
            Spawns =
            [
                new SpawnIntent { Team = "red", Point = new Pt(-20, 1, 0), Yaw = 180 },
                new SpawnIntent { Team = "gold", Point = new Pt(20, 1, 0), Yaw = 0 },
            ],
            Wools = [new WoolIntent { Owner = "red", Color = "red", Spawn = new Pt(-10, 1, 10), Monuments = [new MonumentIntent { Team = "gold" }] }],
            Observer = new ObserverIntent { Point = new Pt(0, 20, 0), Yaw = 0 },
            Meta = new MetaIntent { Name = "Test", Authors = [new AuthorIntent { Name = "alice" }] },
        };

        var built = SketchWorldBuilder.Build(Layout, intent);
        // Gold cube anchored at (20,0), floor y=1: the -Z wall strip (layer 4 → y=5) is orange stained clay.
        await Assert.That(built.World.GetBlock(19, 5, -4)).IsEqualTo((Blocks.StainedClay, 1));
    }

    [Test]
    public async Task An_author_elevated_island_caps_the_cube_floor_instead_of_throwing()
    {
        // A very tall island pushes the surface to the world ceiling; the cube floor must clamp so its roof
        // stays under y=256 rather than throwing ArgumentOutOfRangeException mid-export.
        const string tall =
            """
            {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":300}],"islands":[]}}
            """;
        var intent = new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }],
            Spawns = [new SpawnIntent { Team = "red", Point = new Pt(10, 1, 0), Yaw = 0 }],
            Wools = [],
            Observer = new ObserverIntent { Point = new Pt(0, 20, 0), Yaw = 0 },
            Meta = new MetaIntent { Name = "Tall", Authors = [new AuthorIntent { Name = "alice" }] },
        };

        var built = SketchWorldBuilder.Build(tall, intent);   // must not throw
        // Floor clamped to MaxHeight - RoofLayer - 1 = 247; the 2×2 wool marker sits there.
        await Assert.That(built.World.GetBlock(10, 247, 0).Id).IsEqualTo(Blocks.Wool);
    }

    [Test]
    public async Task No_observer_stands_the_world_spawn_on_real_terrain_not_the_void()
    {
        // An off-origin footprint with no observer: the world spawn must land on an actual terrain column,
        // not float at (0, y, 0) over the void.
        const string offset =
            """
            {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":10,"min_z":10,"max_x":35,"max_z":35,"base_height":5}],"islands":[]}}
            """;
        var intent = new MapIntent
        {
            Teams = [new TeamDef { Id = "red", Color = "red" }],
            Spawns = [new SpawnIntent { Team = "red", Point = new Pt(20, 1, 20), Yaw = 0 }],
            Wools = [],
            Observer = null,
            Meta = new MetaIntent { Name = "NoObs", Authors = [new AuthorIntent { Name = "alice" }] },
        };

        var built = SketchWorldBuilder.Build(offset, intent);

        await Assert.That(built.World.GetBlock(0, 0, 0).Id).IsEqualTo(Blocks.Air);                 // origin is off-island
        await Assert.That((built.SpawnX, built.SpawnZ)).IsNotEqualTo((0, 0));                       // not the naive origin
        await Assert.That(built.World.GetBlock(built.SpawnX, 0, built.SpawnZ).Id).IsEqualTo(Blocks.Bedrock);  // real column
    }
}
