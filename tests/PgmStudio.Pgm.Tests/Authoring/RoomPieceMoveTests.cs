using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Authoring;

/// <summary>
/// Moving a room piece back from the sketch. The canvas draws a spawn, a wool room and the building
/// inside one out of the intent, so a drag has to arrive here or the picture and the world build disagree
/// about where a room is.
///
/// <para>Two rules carry the weight. <b>The region is the ground</b>, so moving it carries what is seated on
/// it — the marker, the building, the iron, the entries — and leaving any of them behind puts it off the
/// ground it belongs to. And <b>a piece moves and does not resize</b>: a room's marker is a fraction of its
/// own rectangle, so a changed span shifts the marker inside it and is refused rather than accommodated.</para>
/// </summary>
public sealed class RoomPieceMoveTests
{
    private static MapIntent Board() => new()
    {
        Spawns =
        [
            new SpawnIntent
            {
                Team = "red",
                Point = new Pt(5, 12, 5),
                Protection = [new Rect(0, 0, 10, 10)],
                Footprint = new Rect(2, 2, 8, 8),
                Iron = [new Pt(1, 12, 1)],
            },
        ],
        Wools =
        [
            new WoolIntent
            {
                Owner = "red", Color = "blue",
                Spawn = new Pt(25, 12, 5),
                Protection = [new Rect(20, 0, 30, 10)],
                Footprint = new Rect(22, 2, 28, 8),
                Entries = [new Rect(20, 4, 20, 6)],
            },
        ],
    };

    private static SpawnIntent Spawn(MapIntent intent) => intent.Spawns[0];
    private static WoolIntent Wool(MapIntent intent) => intent.Wools![0];

    [Test]
    public async Task Moving_a_spawns_region_carries_the_room_seated_on_it()
    {
        var (moved, refused) = RoomPieceMove.Apply(
            Board(), "red", StructuralRoles.Spawn, new Rect(100, 50, 110, 60));

        await Assert.That(refused.Count).IsEqualTo(0);
        await Assert.That(moved).IsNotNull();

        var spawn = Spawn(moved!);
        await Assert.That(spawn.Protection.Single()).IsEqualTo(new Rect(100, 50, 110, 60));
        // Everything on that ground travelled the same hundred and fifty blocks.
        await Assert.That(spawn.Point.X).IsEqualTo(105);
        await Assert.That(spawn.Point.Z).IsEqualTo(55);
        await Assert.That(spawn.Footprint).IsEqualTo(new Rect(102, 52, 108, 58));
        await Assert.That(spawn.Iron.Single().X).IsEqualTo(101);
        await Assert.That(spawn.Iron.Single().Z).IsEqualTo(51);
    }

    /// <summary>The marker's Y is the nominal surface the room was stated at, and what it actually stands on
    /// is measured by the world build against the terrain it laid. Carrying a Y here would be a second answer
    /// to a question already settled.</summary>
    [Test]
    public async Task A_moved_marker_keeps_the_height_it_was_stated_at()
    {
        var (moved, _) = RoomPieceMove.Apply(
            Board(), "red", StructuralRoles.Spawn, new Rect(100, 50, 110, 60));

        await Assert.That(Spawn(moved!).Point.Y).IsEqualTo(12);
    }

    [Test]
    public async Task Moving_a_wools_region_carries_its_marker_footprint_and_entries()
    {
        var (moved, refused) = RoomPieceMove.Apply(
            Board(), "red:blue", StructuralRoles.WoolRoom, new Rect(20, 40, 30, 50));

        await Assert.That(refused.Count).IsEqualTo(0);
        var wool = Wool(moved!);
        await Assert.That(wool.Protection.Single()).IsEqualTo(new Rect(20, 40, 30, 50));
        await Assert.That(wool.Spawn.Z).IsEqualTo(45);
        await Assert.That(wool.Footprint).IsEqualTo(new Rect(22, 42, 28, 48));
        await Assert.That(wool.Entries.Single()).IsEqualTo(new Rect(20, 44, 20, 46));
    }

    /// <summary>The building is the house on the ground rather than the ground, so moving it moves it alone.</summary>
    [Test]
    public async Task Moving_a_building_moves_the_footprint_and_nothing_else()
    {
        var (moved, refused) = RoomPieceMove.Apply(
            Board(), "red", StructuralRoles.Building, new Rect(1, 1, 7, 7));

        await Assert.That(refused.Count).IsEqualTo(0);
        var spawn = Spawn(moved!);
        await Assert.That(spawn.Footprint).IsEqualTo(new Rect(1, 1, 7, 7));
        await Assert.That(spawn.Protection.Single()).IsEqualTo(new Rect(0, 0, 10, 10));   // the ground stayed
        await Assert.That(spawn.Point.X).IsEqualTo(5);
    }

    [Test]
    public async Task A_building_carried_off_its_region_is_refused()
    {
        var (moved, refused) = RoomPieceMove.Apply(
            Board(), "red", StructuralRoles.Building, new Rect(6, 6, 12, 12));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RoomFrameRules.FootprintOffPiece);
    }

    /// <summary>A resize is a different question — the marker is a fraction of the rectangle it sits in — so
    /// a placed span that differs from the standing one is refused rather than written.</summary>
    [Test]
    [Arguments(StructuralRoles.Spawn, 0, 0, 12, 10)]     // wider
    [Arguments(StructuralRoles.Spawn, 0, 0, 10, 8)]      // shallower
    [Arguments(StructuralRoles.Building, 2, 2, 9, 8)]    // the building too
    public async Task A_move_that_resizes_is_refused(string part, int minX, int minZ, int maxX, int maxZ)
    {
        var (moved, refused) = RoomPieceMove.Apply(
            Board(), "red", part, new Rect(minX, minZ, maxX, maxZ));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RequestRules.Unreadable);
        await Assert.That(refused.Single().Message).Contains("does not resize");
    }

    /// <summary>A region an author drew as a union has no single rectangle a drag could mean, and moving the
    /// first alone would tear the union apart. The sketch never draws such a region — it projects one
    /// rectangle — so this is the case where Configure has since said something the canvas cannot.</summary>
    [Test]
    public async Task A_region_of_several_rectangles_has_no_rectangle_to_move()
    {
        var board = Board();
        var spawn = board.Spawns[0] with { Protection = [new Rect(0, 0, 10, 10), new Rect(10, 0, 20, 10)] };
        var (moved, refused) = RoomPieceMove.Apply(
            board with { Spawns = [spawn] }, "red", StructuralRoles.Spawn, new Rect(100, 50, 110, 60));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RequestRules.Unreadable);
        await Assert.That(refused.Single().Field).IsEqualTo("protection");
    }

    [Test]
    [Arguments("green", StructuralRoles.Spawn)]
    [Arguments("red:green", StructuralRoles.WoolRoom)]
    public async Task A_reference_naming_no_room_is_a_missing_subject(string reference, string part)
    {
        var (moved, refused) = RoomPieceMove.Apply(Board(), reference, part, new Rect(0, 0, 10, 10));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RequestRules.NoSuchSubject);
        await Assert.That(refused.Single().SubjectIds.Single()).IsEqualTo(reference);
    }

    [Test]
    public async Task A_part_that_is_not_a_rooms_rectangle_is_refused()
    {
        var (moved, refused) = RoomPieceMove.Apply(Board(), "red", "lane", new Rect(0, 0, 10, 10));

        await Assert.That(moved).IsNull();
        await Assert.That(refused.Single().Rule).IsEqualTo(RequestRules.Unreadable);
        await Assert.That(refused.Single().Field).IsEqualTo("part");
    }

    /// <summary>Every other room on the board is untouched. Each image of a mirrored board is its own entry
    /// with its own team, so moving red's spawn is a statement about red's spawn.</summary>
    [Test]
    public async Task Moving_one_room_leaves_every_other_where_it_was()
    {
        var board = Board();
        var (moved, _) = RoomPieceMove.Apply(board, "red", StructuralRoles.Spawn, new Rect(100, 50, 110, 60));

        await Assert.That(Wool(moved!)).IsEqualTo(Wool(board));
    }
}
