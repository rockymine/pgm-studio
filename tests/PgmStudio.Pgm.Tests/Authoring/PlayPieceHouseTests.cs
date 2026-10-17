using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>
/// Setting a room's house whole: the named image takes it as posted and every other image of its unit takes it
/// carried through the symmetry, and a house the room cannot stand is refused naming the wing.
/// </summary>
public sealed class PlayPieceHouseTests
{
    /// <summary>Red's spawn and its mirror across <c>x = 50</c>, both images of the unit <c>spawn:home</c>.</summary>
    private static MapIntent Mirrored() => new()
    {
        Symmetry = new SymmetryIntent { Mode = "mirror_x", CenterX = 50, CenterZ = 50 },
        Spawns =
        [
            new SpawnIntent
            {
                Id = "spawn-red", Team = "red", Stamp = new StampId("spawn", "home", 0),
                Point = new Pt(5, 12, 5), Protection = [new Rect(0, 0, 20, 10)], House = RoomHouse.Over(new Rect(2, 2, 8, 8)),
            },
            new SpawnIntent
            {
                Id = "spawn-blue", Team = "blue", Stamp = new StampId("spawn", "home", 1),
                Point = new Pt(95, 12, 5), Protection = [new Rect(80, 0, 100, 10)], House = RoomHouse.Over(new Rect(92, 2, 98, 8)),
            },
        ],
    };

    private static RoomHouse Two(Rect room, Rect wing) =>
        new() { Wings = [.. RoomHouse.Over(room).Wings, .. RoomHouse.Over(wing).Wings] };

    [Test]
    public async Task A_house_set_on_one_image_is_carried_to_its_mirror()
    {
        var (set, refused) = PlayPieceHouse.Apply(Mirrored(), "spawn-red", Two(new Rect(2, 2, 8, 8), new Rect(8, 2, 13, 8)));

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(set!.Spawns[0].House!.Rects()).IsEquivalentTo(new[] { new Rect(2, 2, 8, 8), new Rect(8, 2, 13, 8) });
        await Assert.That(set.Spawns[1].House!.Rects()).IsEquivalentTo(new[] { new Rect(92, 2, 98, 8), new Rect(87, 2, 92, 8) })
            .Because("the mirror across x = 50 carries both wings");
    }

    [Test]
    public async Task Alone_sets_the_named_image_only()
    {
        var (set, _) = PlayPieceHouse.Apply(Mirrored(), "spawn-red", Two(new Rect(2, 2, 8, 8), new Rect(8, 2, 13, 8)), alone: true);

        await Assert.That(set!.Spawns[1].House!.Rects()).IsEquivalentTo(new[] { new Rect(92, 2, 98, 8) });
    }

    [Test]
    public async Task A_ridge_turns_with_a_quarter_turn()
    {
        var board = Mirrored() with { Symmetry = new SymmetryIntent { Mode = "rot_90", CenterX = 50, CenterZ = 50 } };
        board = board with { Spawns = [board.Spawns[0]] };
        var house = RoomHouse.Over(new Rect(2, 2, 8, 8));
        house = house with { Wings = [house.Wings[0] with { Spec = new WingSpec(Ridge: RidgeAxis.AlongX) }] };
        var turned = board.Spawns[0] with { Id = "spawn-blue", Stamp = new StampId("spawn", "home", 1) };

        var (set, _) = PlayPieceHouse.Apply(board with { Spawns = [board.Spawns[0], turned] }, "spawn-red", house);

        await Assert.That(set!.Spawns[1].House!.Wings[0].Spec.Ridge).IsEqualTo(RidgeAxis.AlongZ);
    }

    [Test]
    [Arguments(2, 2, 8, 8, 8, 2, 11, 8, "WX2")]
    [Arguments(2, 2, 8, 8, 8, 2, 24, 8, "WX12")]
    [Arguments(6, 2, 12, 8, 1, 2, 6, 8, "RQ1")]
    public async Task A_house_the_room_cannot_stand_is_refused(
        int ax, int az, int bx, int bz, int cx, int cz, int dx, int dz, string rule)
    {
        var (set, refused) = PlayPieceHouse.Apply(Mirrored(), "spawn-red", Two(new Rect(ax, az, bx, bz), new Rect(cx, cz, dx, dz)));

        await Assert.That(set).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(rule);
    }

    [Test]
    public async Task A_room_nobody_states_is_no_such_subject()
    {
        var (_, refused) = PlayPieceHouse.Apply(Mirrored(), "spawn-green", RoomHouse.Over(new Rect(2, 2, 8, 8)));

        await Assert.That(refused.Single().Rule).IsEqualTo(RequestRules.NoSuchSubject);
    }
}
