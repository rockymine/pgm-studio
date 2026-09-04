using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// What a drawn role piece is seeded with (WX1): the marker inside the room the piece affords, the footprint
/// the resolver would otherwise have defaulted to, and nothing at all where no room fits.
/// </summary>
public sealed class PieceRoomTests
{
    private static readonly (int W, int D)[] Sizes =
    [
        (8, 8), (9, 9), (10, 10), (11, 11), (12, 12), (14, 14), (16, 16), (20, 20), (24, 24), (30, 30),
        (20, 11), (20, 13), (21, 12), (20, 30), (30, 20), (20, 10), (10, 20), (8, 30),
    ];

    [Test]
    public async Task A_spawn_piece_is_seeded_with_the_room_the_default_would_have_given_it()
    {
        // The seeded rectangle and the resolver's fallback are one number: a 20×20 spawn facing −z opens as
        // an 18×14 room, five blocks of ground in front of its door and one on the other three sides.
        var piece = new BlockRect(0, 0, 20, 20);
        var seed = PieceRoom.ForPiece(piece, PlanRoles.Spawn, Doors("front"), "front")!.Value;
        await Assert.That(seed.Footprint).IsEquivalentTo(new double[] { 1, RoomFrames.DefaultDoorGap, 18, 14 });

        var stated = PlanMarkers.Footprint(piece, seed.Footprint)!.Value;
        var fallback = RoomFrames.DefaultFootprint(
            piece, [RoomEdge.NegZ], piece.MinX + seed.At[0], piece.MinZ + seed.At[1], walled: true);
        await Assert.That((stated.MinX, stated.MinZ, stated.MaxX, stated.MaxZ))
            .IsEqualTo((fallback.MinX, fallback.MinZ, fallback.MaxX, fallback.MaxZ));
    }

    [Test]
    public async Task A_wool_room_keeps_the_plain_inset_because_it_names_no_door()
    {
        // Its entries come from whichever sides abut it, so no one side is opened wider than the rest.
        var seed = PieceRoom.ForPiece(new BlockRect(0, 0, 20, 20), PlanRoles.WoolRoom, Doors("front"), "front")!.Value;
        await Assert.That(seed.Footprint).IsEquivalentTo(new double[] { 1, 1, 18, 18 });
        await Assert.That(seed.At).IsEquivalentTo(new double[] { 10, 10 });
    }

    [Test]
    [MethodDataSource(nameof(Roles))]
    public async Task A_seeded_marker_never_lands_on_mixed_parity(string role)
    {
        // A room odd across one axis only centres a marker whose two axes disagree, which WX3 refuses. The
        // seed nudges it half a block, so every piece a room fits on carries a marker that resolves.
        foreach (var (w, d) in Sizes)
        {
            var piece = new BlockRect(0, 0, w, d);
            if (PieceRoom.ForPiece(piece, role, Doors("front"), "front") is not { } seed) continue;
            await Assert.That(RoomFrames.MixedParity(seed.At[0], seed.At[1])).IsFalse();
        }
    }

    [Test]
    [MethodDataSource(nameof(Roles))]
    public async Task A_seeded_room_resolves_with_its_pad_on_its_own_marker(string role)
    {
        // The whole point of seeding: what the document states is what the export builds, with no refusal
        // and no WX4 clamp moving the spawn point off the marker the author was handed.
        foreach (var (w, d) in Sizes)
        {
            var piece = new BlockRect(0, 0, w, d);
            if (PieceRoom.ForPiece(piece, role, Doors("front"), "front") is not { } seed) continue;
            IReadOnlyList<RoomEdge> doors = role == PlanRoles.Spawn ? [RoomEdge.NegZ] : [];
            List<(double MinX, double MinZ, double MaxX, double MaxZ)> entries = doors.Count == 0
                ? [(0, 0, w, 0), (0, d, w, d), (0, 0, 0, d), (w, 0, w, d)]
                : [];
            foreach (var bound in new[] { false, true })
            {
                var room = RoomFrames.Resolve(piece, PlanMarkers.Footprint(piece, seed.Footprint), bound,
                    piece.MinX + seed.At[0], piece.MinZ + seed.At[1], entries, doors, out var refusal);
                await Assert.That(refusal).IsNull();
                await Assert.That(room!.Pad.Shifted).IsFalse();
            }
        }
    }

    [Test]
    public async Task A_spawn_is_seeded_with_one_cube_beside_its_door_and_a_wool_room_with_none()
    {
        // One cube is the seed; adding more is the author's. It stands in the nearest row outside the door
        // wall WX8 allows, which on a default footprint is the piece's outer edge, and clear of the door
        // corridor, so nobody walks out into it.
        var piece = new BlockRect(0, 0, 20, 20);
        var spawn = PieceRoom.ForPiece(piece, PlanRoles.Spawn, Doors("front"), "front")!.Value;
        await Assert.That(spawn.Iron).IsNotNull();
        await Assert.That(spawn.Iron![1]).IsEqualTo(RoomFrames.IronSpan / 2.0);

        var room = RoomFrames.ResolveRoom(piece, PlanMarkers.Footprint(piece, spawn.Footprint), shellBound: true,
            piece.MinX + spawn.At[0], piece.MinZ + spawn.At[1], [], [RoomEdge.NegZ],
            [(piece.MinX + spawn.Iron[0], piece.MinZ + spawn.Iron[1])], out _)!;
        var cube = room.Iron[0];
        await Assert.That(cube.Placeable).IsTrue();
        await Assert.That(cube.Size).IsEqualTo(RoomFrames.IronSpan);
        // Clear of the door's own opening projected out to that row.
        await Assert.That(cube.MinX).IsGreaterThanOrEqualTo(room.Frame.Doors[0].Lo + room.Frame.Doors[0].Width);

        // A wool room names no door, so there is no yard in front of it and no iron.
        await Assert.That(PieceRoom.ForPiece(piece, PlanRoles.WoolRoom, Doors("front"), "front")!.Value.Iron).IsNull();
    }

    [Test]
    [Arguments("front", 1, 0)]
    [Arguments("back", -1, 0)]
    [Arguments("left", 0, -1)]
    [Arguments("right", 0, 1)]
    public async Task The_cube_stands_on_the_hand_a_player_leaves_by(string facing, int rightX, int rightZ)
    {
        // Walking out of the door, the cube is on the player's right: leaving −z their right is +x, leaving
        // +z it is −x, and the two side doors turn with them. The seed is where an author reaches first, so
        // which hand it lands on is the whole of what it says.
        var piece = new BlockRect(0, 0, 20, 20);
        var door = RoomEdges.Nearest(SpawnFacings.Direction(facing), RoomEdges.All)!.Value;
        var seed = PieceRoom.ForPiece(piece, PlanRoles.Spawn, Doors(facing), facing)!.Value;
        var room = RoomFrames.ResolveRoom(piece, PlanMarkers.Footprint(piece, seed.Footprint), shellBound: true,
            piece.MinX + seed.At[0], piece.MinZ + seed.At[1], [], [door],
            [(piece.MinX + seed.Iron![0], piece.MinZ + seed.Iron[1])], out _)!;
        var (cube, opening) = (room.Iron[0], room.Frame.Doors[0]);
        await Assert.That(cube.Placeable).IsTrue();

        // The cube's own side of the corridor along the wall's axis, signed the way the player's right points.
        var (cubeLo, cubeHi) = door.AlongX() ? (cube.MinX, cube.MinX + cube.Size) : (cube.MinZ, cube.MinZ + cube.Size);
        var toRight = door.AlongX() ? rightX : rightZ;
        await Assert.That(toRight > 0 ? cubeLo >= opening.Lo + opening.Width : cubeHi <= opening.Lo).IsTrue();
    }

    [Test]
    public async Task A_stated_building_is_the_room_and_the_cube_stands_beside_its_door()
    {
        // A wide protection region holding a small hall: the footprint comes back verbatim, the marker centres
        // in the hall rather than in the piece, and the cube seats against the hall's own door wall — WX8's
        // standing room out from it, not out at the region's rim.
        var piece = new BlockRect(0, 0, 20, 20);
        var hall = new BlockRect(1, 1, 13, 13);
        var seed = PieceRoom.ForPiece(piece, PlanRoles.Spawn, [RoomEdge.PosZ], "back", hall)!.Value;
        await Assert.That(seed.Footprint).IsEquivalentTo(new double[] { 1, 1, 12, 12 });
        await Assert.That(seed.At).IsEquivalentTo(new double[] { 7, 7 });

        var room = RoomFrames.ResolveRoom(piece, hall, shellBound: true,
            piece.MinX + seed.At[0], piece.MinZ + seed.At[1], [], [RoomEdge.PosZ],
            [(piece.MinX + seed.Iron![0], piece.MinZ + seed.Iron[1])], out var refusal)!;
        await Assert.That(refusal).IsNull();
        await Assert.That(room.Iron[0].Placeable).IsTrue();
        await Assert.That(room.Iron[0].MinZ).IsEqualTo(hall.MaxZ + RoomFrames.IronGap);
        // Leaving a +z door the player's right is −x, so the cube is below the corridor.
        await Assert.That(room.Iron[0].MinX + room.Iron[0].Size).IsLessThanOrEqualTo(room.Frame.Doors[0].Lo);
    }

    [Test]
    public async Task A_stated_building_that_leaves_the_piece_is_not_seeded()
    {
        // The answer is a rectangle the export would build, so one reaching outside the ground it stands on is
        // no answer at all — the same WX2 refusal a room too small to hold a pad gets.
        var piece = new BlockRect(0, 0, 20, 20);
        await Assert.That(PieceRoom.ForPiece(piece, PlanRoles.Spawn, Doors("front"), "front", new BlockRect(14, 1, 26, 13)))
            .IsNull();
        await Assert.That(PieceRoom.ForPiece(piece, PlanRoles.Spawn, Doors("front"), "front", new BlockRect(1, 1, 4, 4)))
            .IsNull();
    }

    [Test]
    public async Task A_yard_too_shallow_for_a_cube_seeds_no_iron()
    {
        // The gap only reaches the cube's own span plus its clear air on a deep enough piece; below that the
        // spawn is seeded with its room and nothing else.
        await Assert.That(PieceRoom.ForPiece(new BlockRect(0, 0, 20, 12), PlanRoles.Spawn, Doors("front"), "front")!.Value.Iron)
            .IsNull();
        await Assert.That(PieceRoom.ForPiece(new BlockRect(0, 0, 20, 20), PlanRoles.Spawn, Doors("front"), "front")!.Value.Iron)
            .IsNotNull();
    }

    [Test]
    public async Task A_piece_too_small_for_a_building_is_still_seeded_with_the_room_it_can_have()
    {
        // 7×7 insets to 5×5, which holds a pad and its chest corners but not walls. The room is seeded; the
        // building simply is not there.
        var seed = PieceRoom.ForPiece(new BlockRect(0, 0, 7, 7), PlanRoles.WoolRoom, Doors("front"), "front");
        await Assert.That(seed).IsNotNull();
        await Assert.That(seed!.Value.Footprint).IsEquivalentTo(new double[] { 1, 1, 5, 5 });

        // A piece that cannot hold a room at all is left bare: 5×5 insets to 3×3, under WX2's own minimum.
        await Assert.That(PieceRoom.ForPiece(new BlockRect(0, 0, 5, 5), PlanRoles.Spawn, Doors("front"), "front")).IsNull();
        await Assert.That(PieceRoom.ForPiece(new BlockRect(0, 0, 5, 5), PlanRoles.WoolRoom, Doors("front"), "front")).IsNull();
    }

    [Test]
    public async Task A_piece_with_no_room_role_is_not_seeded()
    {
        await Assert.That(PieceRoom.ForPiece(new BlockRect(0, 0, 20, 20), PlanRoles.Piece, Doors("front"), "front")).IsNull();
        await Assert.That(PieceRoom.ForPiece(new BlockRect(0, 0, 20, 20), PlanRoles.Buffer, Doors("front"), "front")).IsNull();
    }

    [Test]
    public async Task The_door_the_spawn_faces_is_the_side_the_ground_is_kept_on()
    {
        // Facing turns the gap with it: the same piece opens on whichever side the player arrives through.
        var piece = new BlockRect(0, 0, 20, 20);
        var gap = (double)RoomFrames.DefaultDoorGap;
        await Assert.That(PieceRoom.ForPiece(piece, PlanRoles.Spawn, Doors("front"), "front")!.Value.Footprint)
            .IsEquivalentTo(new[] { 1d, gap, 18d, 14d });
        await Assert.That(PieceRoom.ForPiece(piece, PlanRoles.Spawn, Doors("back"), "back")!.Value.Footprint)
            .IsEquivalentTo(new[] { 1d, 1d, 18d, 14d });
        await Assert.That(PieceRoom.ForPiece(piece, PlanRoles.Spawn, Doors("left"), "left")!.Value.Footprint)
            .IsEquivalentTo(new[] { gap, 1d, 14d, 18d });
        await Assert.That(PieceRoom.ForPiece(piece, PlanRoles.Spawn, Doors("right"), "right")!.Value.Footprint)
            .IsEquivalentTo(new[] { 1d, 1d, 14d, 18d });
    }

    // The doors the compiler would derive for a piece that abuts nothing yet: the one wall the facing leans
    // into. A drawn piece has no neighbours, so this is what seeding a fresh board answers with.
    private static IReadOnlyList<RoomEdge> Doors(string facing) =>
        [RoomEdges.Nearest(SpawnFacings.Direction(facing), RoomEdges.All)!.Value];

    public static IEnumerable<Func<string>> Roles() => [() => PlanRoles.Spawn, () => PlanRoles.WoolRoom];
}
