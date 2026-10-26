using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>
/// Setting one of a play piece's settings by its id. <b>What a unit shares, every image takes</b> — a goal's
/// structure is the same goal for every team — and <b>a name and a colour belong to the image they name</b>.
/// </summary>
public sealed class PlayPieceSettingsTests
{
    private static readonly SymmetryIntent MirrorX = new() { Mode = "mirror_x", CenterX = 0, CenterZ = 0 };

    private static MapIntent Board() => new()
    {
        Spawns =
        [
            new SpawnIntent { Id = "spawn-red", Team = "red", Stamp = new StampId("spawn", "0", 0), Yaw = 270,
                              Doors = [new StatedDoor("+x")], Protection = [new Rect(-40, -6, -30, 6)] },
            new SpawnIntent { Id = "spawn-blue", Team = "blue", Stamp = new StampId("spawn", "0", 1), Yaw = 90,
                              Doors = [new StatedDoor("-x")], Protection = [new Rect(30, -6, 40, 6)] },
        ],
        Wools = [new WoolIntent { Id = "wool-red", Owner = "red", Stamp = new StampId("wool", "0", 0) },
                 new WoolIntent { Id = "wool-blue", Owner = "blue", Stamp = new StampId("wool", "0", 1) }],
        Destroyables =
        [
            new DestroyableIntent { Id = "destroyable-red", Owner = "red", Name = "Red Monument", Stamp = new StampId("destroyable", "0", 0) },
            new DestroyableIntent { Id = "destroyable-blue", Owner = "blue", Name = "Blue Monument", Stamp = new StampId("destroyable", "0", 1) },
        ],
        Cores = [new CoreIntent { Id = "core-red", Owner = "red", Stamp = new StampId("core", "0", 0) },
                 new CoreIntent { Id = "core-blue", Owner = "blue", Stamp = new StampId("core", "0", 1) }],
    };

    [Test]
    public async Task A_goals_structure_is_set_on_every_image_and_its_name_on_the_one_named()
    {
        var (styled, _) = PlayPieceSettings.Apply(Board(), "destroyable-red", PieceSettings.Materials, new PieceValue(Text: "gold block"));
        var (named, _) = PlayPieceSettings.Apply(Board(), "destroyable-red", PieceSettings.Name, new PieceValue(Text: "Keep"));

        await Assert.That(styled!.Destroyables!.Select(goal => goal.Materials)).IsEquivalentTo(new[] { "gold block", "gold block" });
        await Assert.That(named!.Destroyables!.Select(goal => goal.Name)).IsEquivalentTo(new[] { "Keep", "Blue Monument" });
    }

    [Test]
    public async Task Alone_sets_a_shared_setting_on_the_named_image_only()
    {
        var (set, _) = PlayPieceSettings.Apply(Board(), "core-red", PieceSettings.Lava, new PieceValue(Number: 4), alone: true);

        await Assert.That(set!.Cores!.Select(core => core.Lava)).IsEquivalentTo(new[] { 4, ObjectiveDefaults.CoreLava });
    }

    [Test]
    public async Task Null_puts_a_setting_back_to_its_default()
    {
        var (raised, _) = PlayPieceSettings.Apply(Board(), "core-red", PieceSettings.Float, new PieceValue(Number: 9));
        var (reset, _) = PlayPieceSettings.Apply(raised!, "core-red", PieceSettings.Float, default);

        await Assert.That(reset!.Cores!.All(core => core.Float == ObjectiveDefaults.CoreFloat)).IsTrue();
    }

    [Test]
    public async Task A_wool_colour_is_its_own()
    {
        var (set, _) = PlayPieceSettings.Apply(Board(), "wool-red", PieceSettings.Color, new PieceValue(Text: "lime"));

        await Assert.That(set!.Wools!.Select(wool => wool.Color)).IsEquivalentTo(new[] { "lime", "" });
    }

    [Test]
    public async Task A_spawns_facing_turns_its_mirror_the_other_way_and_leaves_its_region()
    {
        var (faced, _) = PlayPieceSettings.Apply(Board(), "spawn-red", PieceSettings.Facing, new PieceValue(Number: 1), board: MirrorX);

        await Assert.That(faced!.Spawns.Select(spawn => spawn.Yaw)).IsEquivalentTo(new[] { 0.0, 0.0 })
            .Because("red turns from east to south and its mirror from west to south");
        await Assert.That(faced.Spawns[0].Doors.Select(door => door.Edge)).IsEquivalentTo(new[] { "+z" });
        await Assert.That(faced.Spawns[0].Protection.Single()).IsEqualTo(new Rect(-40, -6, -30, 6));
    }

    [Test]
    public async Task A_setting_the_kind_lacks_or_a_value_out_of_range_is_refused()
    {
        var (wrong, why) = PlayPieceSettings.Apply(Board(), "wool-red", PieceSettings.Lava, new PieceValue(Number: 3));
        var (range, which) = PlayPieceSettings.Apply(Board(), "core-red", PieceSettings.Lava, new PieceValue(Number: 40));
        var (missing, none) = PlayPieceSettings.Apply(Board(), "nothing", PieceSettings.Name, new PieceValue(Text: "x"));

        await Assert.That(wrong).IsNull();
        await Assert.That(why.Single().Field).IsEqualTo("key");
        await Assert.That(range).IsNull();
        await Assert.That(which.Single().Field).IsEqualTo("value");
        await Assert.That(missing).IsNull();
        await Assert.That(none.Single().Rule).IsEqualTo(RequestRules.NoSuchSubject);
    }

    [Test]
    public async Task A_stamp_switched_off_is_off_on_every_image_and_back_on_leaves_nothing_listed()
    {
        var (off, refused) = PlayPieceSettings.Apply(Board(), "destroyable-red", StampSwitches.Marker, new PieceValue(Flag: false));
        var (on, _) = PlayPieceSettings.Apply(off!, "destroyable-red", StampSwitches.Marker, new PieceValue(Flag: true));

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(off!.Destroyables!.All(goal => goal.StampsOff!.SequenceEqual([StampSwitches.Marker]))).IsTrue();
        await Assert.That(on!.Destroyables!.All(goal => goal.StampsOff is null)).IsTrue();
    }

    [Test]
    public async Task A_stamp_a_piece_does_not_have_is_refused()
    {
        var (looted, why) = PlayPieceSettings.Apply(Board(), "destroyable-red", StampSwitches.Loot, new PieceValue(Flag: false));
        var (worded, unread) = PlayPieceSettings.Apply(Board(), "wool-red", StampSwitches.Loot, new PieceValue(Text: "off"));

        await Assert.That(looted).IsNull();
        await Assert.That(why.Single().Field).IsEqualTo("key");
        await Assert.That(worded).IsNull();
        await Assert.That(unread.Single().Field).IsEqualTo("value");
    }
}
