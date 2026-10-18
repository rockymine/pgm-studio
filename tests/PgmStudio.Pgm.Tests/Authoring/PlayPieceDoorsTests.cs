using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>
/// A room's doors set whole by its id and carried onto every image of its unit, and the doors a room is built
/// with read back. The board is a red wool at x 20..30, z 0..10 fanned rot_180 onto blue's at x −30..−20,
/// z −10..0.
/// </summary>
public sealed class PlayPieceDoorsTests
{
    private static MapIntent Board() => new()
    {
        Symmetry = new SymmetryIntent { Mode = "rot_180" },
        Wools =
        [
            new WoolIntent { Id = "wool-red", Owner = "red", Stamp = new StampId("wool", "0", 0), Protection = [new Rect(20, 0, 30, 10)] },
            new WoolIntent { Id = "wool-blue", Owner = "blue", Stamp = new StampId("wool", "0", 1), Protection = [new Rect(-30, -10, -20, 0)] },
        ],
    };

    [Test]
    public async Task Doors_set_on_one_image_are_carried_onto_the_other()
    {
        var (set, refused) = PlayPieceDoors.Apply(Board(), "wool-red", [StatedDoor.At(RoomEdge.NegX, 20, 4.5)]);

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(set!.Wools![0].Doors).IsEquivalentTo(new[] { StatedDoor.At(RoomEdge.NegX, 20, 4.5) });
        await Assert.That(set.Wools[1].Doors).IsEquivalentTo(new[] { StatedDoor.At(RoomEdge.PosX, -20, -4.5) })
            .Because("a half turn carries the point through the centre and turns the wall to face the other way");
    }

    [Test]
    public async Task Doors_set_alone_leave_the_other_image_as_it_was()
    {
        var (set, _) = PlayPieceDoors.Apply(Board(), "wool-red", [StatedDoor.Centred(RoomEdge.PosZ)], alone: true);

        await Assert.That(set!.Wools![1].Doors).IsEmpty();
    }

    [Test]
    [Arguments("up", 25.0, 0.0, "names the edge 'up'")]
    [Arguments("-z", 25.0, null, "one coordinate of its point without the other")]
    [Arguments("-z", 25.0, -6.0, "stands outside the region of its room")]
    public async Task A_door_naming_no_wall_half_a_point_or_a_point_off_the_region_is_refused(
        string edge, double? x, double? z, string says)
    {
        var (set, refused) = PlayPieceDoors.Apply(Board(), "wool-red", [new StatedDoor(edge, x, z)]);

        await Assert.That(set).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RequestRules.Unreadable);
        await Assert.That(refused.Single().Message).Contains(says);
    }

    [Test]
    public async Task A_room_keeps_at_least_one_door_and_an_unknown_room_has_none()
    {
        await Assert.That(PlayPieceDoors.Apply(Board(), "wool-red", []).Refused.Single().Message).Contains("at least one door");
        await Assert.That(PlayPieceDoors.Apply(Board(), "wool-green", [StatedDoor.Centred(RoomEdge.NegZ)]).Refused.Single().Rule)
            .IsEqualTo(RequestRules.NoSuchSubject);
    }

    [Test]
    public async Task A_wool_stating_none_reads_back_the_one_it_is_built_with()
    {
        var (doors, stated, _) = PlayPieceDoors.Built(Board(), "wool-red", null);

        await Assert.That(stated).IsFalse();
        await Assert.That(doors!).IsEquivalentTo(new[] { StatedDoor.Centred(RoomEdge.NegX) })
            .Because("with no ground to read, the side facing the middle");
    }
}
