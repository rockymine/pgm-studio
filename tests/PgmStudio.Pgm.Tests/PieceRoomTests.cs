using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

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
        // an 18×12 room, seven blocks of ground in front of its door and one on the other three sides.
        var piece = new BlockRect(0, 0, 20, 20);
        var seed = PieceRoom.ForPiece(piece, PlanRoles.Spawn, "front")!.Value;
        await Assert.That(seed.Footprint).IsEquivalentTo(new double[] { 1, RoomFrames.DefaultDoorGap, 18, 12 });

        var stated = PlanMarkers.Footprint(piece, seed.Footprint)!.Value;
        var fallback = RoomFrames.DefaultFootprint(
            piece, RoomEdge.NegZ, piece.MinX + seed.At[0], piece.MinZ + seed.At[1], walled: true);
        await Assert.That((stated.MinX, stated.MinZ, stated.MaxX, stated.MaxZ))
            .IsEqualTo((fallback.MinX, fallback.MinZ, fallback.MaxX, fallback.MaxZ));
    }

    [Test]
    public async Task A_wool_room_keeps_the_plain_inset_because_it_names_no_door()
    {
        // Its entries come from whichever sides abut it, so no one side is opened wider than the rest.
        var seed = PieceRoom.ForPiece(new BlockRect(0, 0, 20, 20), PlanRoles.WoolRoom, "front")!.Value;
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
            if (PieceRoom.ForPiece(piece, role, "front") is not { } seed) continue;
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
            if (PieceRoom.ForPiece(piece, role, "front") is not { } seed) continue;
            RoomEdge? door = role == PlanRoles.Spawn ? RoomEdge.NegZ : null;
            List<(double MinX, double MinZ, double MaxX, double MaxZ)> entries = door is null
                ? [(0, 0, w, 0), (0, d, w, d), (0, 0, 0, d), (w, 0, w, d)]
                : [];
            foreach (var walled in new[] { false, true })
            {
                var room = RoomFrames.Resolve(piece, PlanMarkers.Footprint(piece, seed.Footprint), walled,
                    piece.MinX + seed.At[0], piece.MinZ + seed.At[1], entries, door, out var refusal);
                await Assert.That(refusal).IsNull();
                await Assert.That(room!.Pad.Shifted).IsFalse();
            }
        }
    }

    [Test]
    public async Task A_piece_too_small_to_raise_a_shell_on_is_left_bare()
    {
        // 7×7 insets to 5×5, under WX2's 6×6 — seeding it would hand the author a rectangle its own export
        // refuses, so nothing is seeded and the piece says so by being empty.
        await Assert.That(PieceRoom.ForPiece(new BlockRect(0, 0, 7, 7), PlanRoles.Spawn, "front")).IsNull();
        await Assert.That(PieceRoom.ForPiece(new BlockRect(0, 0, 7, 7), PlanRoles.WoolRoom, "front")).IsNull();
    }

    [Test]
    public async Task A_piece_with_no_room_role_is_not_seeded()
    {
        await Assert.That(PieceRoom.ForPiece(new BlockRect(0, 0, 20, 20), PlanRoles.Piece, "front")).IsNull();
        await Assert.That(PieceRoom.ForPiece(new BlockRect(0, 0, 20, 20), PlanRoles.Buffer, "front")).IsNull();
    }

    [Test]
    public async Task The_door_the_spawn_faces_is_the_side_the_ground_is_kept_on()
    {
        // Facing turns the gap with it: the same piece opens on whichever side the player arrives through.
        var piece = new BlockRect(0, 0, 20, 20);
        var gap = (double)RoomFrames.DefaultDoorGap;
        await Assert.That(PieceRoom.ForPiece(piece, PlanRoles.Spawn, "front")!.Value.Footprint)
            .IsEquivalentTo(new[] { 1d, gap, 18d, 12d });
        await Assert.That(PieceRoom.ForPiece(piece, PlanRoles.Spawn, "back")!.Value.Footprint)
            .IsEquivalentTo(new[] { 1d, 1d, 18d, 12d });
        await Assert.That(PieceRoom.ForPiece(piece, PlanRoles.Spawn, "left")!.Value.Footprint)
            .IsEquivalentTo(new[] { gap, 1d, 12d, 18d });
        await Assert.That(PieceRoom.ForPiece(piece, PlanRoles.Spawn, "right")!.Value.Footprint)
            .IsEquivalentTo(new[] { 1d, 1d, 12d, 18d });
    }

    public static IEnumerable<Func<string>> Roles() => [() => PlanRoles.Spawn, () => PlanRoles.WoolRoom];
}
