using PgmStudio.Domain;
using PgmStudio.Export;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

/// <summary>
/// The wings a room's house is stamped from: the resolved frame first, then the joined wings, max-inclusive. A
/// joined wing off the room's region, or one overlapping the room, leaves the room standing alone with a
/// complaint naming the room.
/// </summary>
public sealed class RoomHousesTests
{
    private static readonly RoomFrame Frame =
        RoomFrames.Resolve(new BlockRect(0, 0, 14, 10), new BlockRect(1, 1, 7, 9), shellBound: true, 4, 4, [new AskedDoor(RoomEdge.NegZ)], spawn: true, out _)!;

    private static RoomHouse House(params Rect[] wings) =>
        new() { Wings = [.. wings.SelectMany(wing => RoomHouse.Over(wing).Wings)] };

    [Test]
    public async Task A_joined_wing_follows_the_frame()
    {
        var (wings, refused) = RoomHouses.Of(House(new Rect(1, 1, 7, 9), new Rect(7, 3, 13, 7)), Frame, [new Rect(0, 0, 14, 10)], "spawn-red");

        await Assert.That(refused).IsEmpty();
        await Assert.That(wings.Select(wing => (wing.MinX, wing.MinZ, wing.MaxX, wing.MaxZ)))
            .IsEquivalentTo(new[] { (Frame.MinX, Frame.MinZ, Frame.MaxX - 1, Frame.MaxZ - 1), (7, 3, 12, 6) });
    }

    [Test]
    public async Task A_house_of_one_wing_stamps_the_frame_alone()
    {
        var (wings, refused) = RoomHouses.Of(House(new Rect(1, 1, 7, 9)), Frame, [new Rect(0, 0, 14, 10)], "spawn-red");

        await Assert.That(wings).IsEmpty();
        await Assert.That(refused).IsEmpty();
    }

    [Test]
    public async Task A_wing_off_the_region_leaves_the_room_alone_and_says_so()
    {
        var (wings, refused) = RoomHouses.Of(House(new Rect(1, 1, 7, 9), new Rect(7, 3, 15, 7)), Frame, [new Rect(0, 0, 14, 10)], "spawn-red");

        await Assert.That(wings).IsEmpty();
        await Assert.That(refused.Single().Rule).IsEqualTo(RoomFrameRules.FootprintOffPiece);
        await Assert.That(refused.Single().Subjects!).IsEquivalentTo(new[] { "spawn-red" });
    }

    [Test]
    public async Task A_wing_overlapping_the_room_is_no_joint_and_the_room_stands_alone()
    {
        var (wings, refused) = RoomHouses.Of(House(new Rect(1, 1, 7, 9), new Rect(5, 3, 13, 7)), Frame, [new Rect(0, 0, 14, 10)], "spawn-red");

        await Assert.That(wings).IsEmpty();
        await Assert.That(refused).IsNotEmpty();
    }
}
