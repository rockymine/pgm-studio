using PgmStudio.Api.Services;
using PgmStudio.Minecraft;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The room styles a map binds (G34c, docs/world-export/structures.md §9): read off the sketch layout and
/// carried into the export. Three things have to hold — a map that never picked one exports exactly as it did
/// before the step existed, a map that did gets the shell it picked, and both are <b>map-wide</b>, so the two
/// teams' rooms stay identical.
/// </summary>
public sealed class RoomStyleScopeTests
{
    private const string Plain =
        """
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":-40,"min_z":-40,"max_x":40,"max_z":40,"base_height":1}],"islands":[]}}
        """;

    /// <summary>The same layout with the two shells bound — the snapshot form the Rooms step writes.</summary>
    private static string Bound(RoomStyle? cage = null, RoomStyle? spawn = null)
    {
        var parts = new List<string>();
        if (cage is not null) parts.Add($"\"cage\":{RoomStyleJson.Serialize(cage)}");
        if (spawn is not null) parts.Add($"\"spawn\":{RoomStyleJson.Serialize(spawn)}");
        return Plain[..^1] + $",\"roomStyles\":{{{string.Join(',', parts)}}}}}";
    }

    private static MapIntent Intent() => new()
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
        Observer = new ObserverIntent { Point = new Pt(0, 40, 0), Yaw = 0 },
    };

    // ── what a map binds ───────────────────────────────────────────────────────────────────────────
    [Test]
    public async Task A_map_that_never_picked_one_gets_the_built_in_shells()
    {
        var (cage, spawn) = RoomStyleScope.StylesOf(Plain);

        await Assert.That(cage).IsEqualTo(RoomStyle.Cage);
        await Assert.That(spawn).IsEqualTo(RoomStyle.Spawn);
    }

    [Test]
    public async Task Each_kind_is_bound_on_its_own_and_the_other_keeps_its_default()
    {
        var tall = RoomStyle.Cage with { Wall = RoomStyle.Cage.Wall with { Extent = 11 } };
        var (cage, spawn) = RoomStyleScope.StylesOf(Bound(cage: tall));

        await Assert.That(cage.Wall.Extent).IsEqualTo(11);
        await Assert.That(spawn).IsEqualTo(RoomStyle.Spawn);
    }

    [Test]
    public async Task An_unreadable_snapshot_costs_that_map_its_shell_and_not_its_export()
    {
        var layout = Plain[..^1] + ",\"roomStyles\":{\"cage\":{\"wall\":\"not a part\"}}}";
        var (cage, spawn) = RoomStyleScope.StylesOf(layout);

        await Assert.That(cage).IsEqualTo(RoomStyle.Cage);
        await Assert.That(spawn).IsEqualTo(RoomStyle.Spawn);
    }

    // ── what the export does with them ─────────────────────────────────────────────────────────────
    [Test]
    public async Task The_bound_shell_is_the_one_the_export_stamps()
    {
        // Sandstone walls: a block no built-in shell ever places, so finding it says the binding travelled the
        // whole way rather than that a default happened to match.
        var sandstone = RoomStyle.Cage with { Wall = RoomPart.Of(new SolidMaterial(Blocks.Sandstone), 7) };
        var built = SketchWorldBuilder.Build(Bound(cage: sandstone), Intent());

        await Assert.That(Count(built.World, -10, 10, Blocks.Sandstone)).IsGreaterThan(0);
        // And only the cages: a spawn room is a different kind, so it kept its own built-in shell.
        await Assert.That(Count(built.World, -20, 0, Blocks.Sandstone)).IsEqualTo(0);
    }

    [Test]
    public async Task An_unbound_map_exports_the_shell_it_always_did()
    {
        // The stage-one promise, held at the far end of the pipeline: the two worlds are the same world.
        var plain = SketchWorldBuilder.Build(Plain, Intent());
        var bound = SketchWorldBuilder.Build(Bound(cage: RoomStyle.Cage, spawn: RoomStyle.Spawn), Intent());

        for (var y = 1; y < 30; y++)
        for (var x = -25; x <= 25; x += 1)
        for (var z = -5; z <= 15; z += 1)
            await Assert.That(bound.World.GetBlock(x, y, z)).IsEqualTo(plain.World.GetBlock(x, y, z));
    }

    [Test]
    public async Task Both_teams_get_the_same_building()
    {
        // The reason there is no per-room binding: a shell is fanned across the symmetry orbit, so both teams
        // have to face the same one. Counted as the shell's *own* material rather than every block in the room
        // — the two spawn rooms differ by design in what they hold, since each seats a monument for the other
        // team's wool.
        var built = SketchWorldBuilder.Build(
            Bound(spawn: RoomStyle.Spawn with { Wall = RoomPart.Of(new SolidMaterial(Blocks.Sandstone), 9) }),
            Intent());

        var red = Count(built.World, -20, 0, Blocks.Sandstone);
        await Assert.That(red).IsGreaterThan(0);
        await Assert.That(Count(built.World, 20, 0, Blocks.Sandstone)).IsEqualTo(red);
    }

    /// <summary>How many blocks of one kind stand in one room's own footprint — a count rather than a set, so a
    /// room that got half a shell would fail. The radius is the room's: a default shell frames 9 cells across
    /// the marker, and a window any wider reaches the neighbouring room (the red spawn and the red cage stand
    /// 10 apart here), which would read one room's material as the other's.</summary>
    private static int Count(VoxelWorld world, int x, int z, int blockId)
    {
        var found = 0;
        for (var y = 1; y < 24; y++)
        for (var dx = -5; dx <= 5; dx++)
        for (var dz = -5; dz <= 5; dz++)
            if (world.GetBlock(x + dx, y, z + dz).Id == blockId) found++;
        return found;
    }
}
