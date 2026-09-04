using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Plan;

/// <summary>What a freshly drawn role piece carries: the marker the room is built around, the footprint the
/// building stands on, and — on a spawn whose yard has room for one — the iron marker. All piece-relative
/// block offsets, ready to store.</summary>
public readonly record struct DrawnRoom(double[] At, double[] Footprint, double[]? Iron);

/// <summary>
/// The contents a <c>spawn</c> or <c>wool-room</c> piece is drawn with. A piece that arrives empty leaves its
/// room to a default nobody can see; seeding states the same numbers in the document, where the author reads
/// and edits them. The footprint is <see cref="RoomFrames.DefaultFootprint"/>'s own answer, so the seeded
/// rectangle and the resolver's fallback are one number rather than two that can disagree.
/// </summary>
public static class PieceRoom
{
    /// <summary>The marker, footprint and iron for a piece of this role, or null where the role carries no
    /// room, the piece is too small to hold one at all (<c>WX2</c>), or a stated building does not lie on the
    /// piece. <paramref name="doorEdges"/> are the walls the room opens through — <see cref="PieceDoors"/>'s
    /// answer for the piece, ignored on a wool cage, whose doors are cut from its entry segments instead.
    /// <paramref name="facing"/> is which way the player looks, which picks the door the cube stands beside.
    /// <paramref name="stated"/> is the building the placement already names; absent, the room is the one the
    /// piece affords.</summary>
    /// <remarks>The default footprint is <b>sized for a shell</b>: a plan states no room style
    /// (docs/world-export/structures.md §9), and a room that can carry a building is the one worth handing an
    /// author who has not said yet. Where the piece is too small for that, the room it can have is seeded
    /// anyway — a building that does not fit simply is not there, and the pad, chests and monuments a room is
    /// for need the same floor either way.</remarks>
    public static DrawnRoom? ForPiece(
        BlockRect piece, string role, IReadOnlyList<RoomEdge> doorEdges, string facing,
        BlockRect? stated = null)
    {
        if (role != PlanRoles.Spawn && role != PlanRoles.WoolRoom) return null;
        IReadOnlyList<RoomEdge> doors = role == PlanRoles.Spawn ? doorEdges : [];

        // The marker sits at the centre of the room the piece affords, not of the piece: the door's gap moves
        // the room off the piece's own middle, and a player arrives inside the building. Probing with the
        // piece centre is what names that room before there is a marker to name it with. A stated building is
        // already the room, so it is its own probe and its own answer.
        var probe = stated ?? RoomFrames.DefaultFootprint(piece, doors, Centre(piece.MinX, piece.MaxX),
            Centre(piece.MinZ, piece.MaxZ), walled: true);
        var (markerX, markerZ) = RoomFrames.SameParity(
            Centre(probe.MinX, probe.MaxX) - piece.MinX, Centre(probe.MinZ, probe.MaxZ) - piece.MinZ);

        // The footprint the marker actually seats in. The parity nudge can move the marker half a block off
        // the room's middle, which the gap yields to, so the answer is resolved against the seeded marker
        // rather than against the probe it came from.
        var footprint = stated ?? RoomFrames.DefaultFootprint(
            piece, doors, piece.MinX + markerX, piece.MinZ + markerZ, walled: true);
        if (RoomFrames.FootprintTooSmall(footprint.Width, footprint.Depth, walled: false)) return null;
        if (footprint.MinX < piece.MinX || footprint.MinZ < piece.MinZ
            || footprint.MaxX > piece.MaxX || footprint.MaxZ > piece.MaxZ) return null;

        return new DrawnRoom(
            [markerX, markerZ],
            [footprint.MinX - piece.MinX, footprint.MinZ - piece.MinZ, footprint.Width, footprint.Depth],
            doors.Count > 0 ? Iron(piece, footprint, doors, facing, markerX, markerZ) : null);
    }

    /// <summary>The spawn's one iron marker, or null where the yard has no room for a cube. One is the seed;
    /// adding more is the author's. It stands beside <b>the door the player walks out of</b> — the one their
    /// facing leans into most, which on a hall opening on two walls is the near one rather than the one behind
    /// them — in the nearest row outside that wall that <c>WX8</c> allows: the building's own edge plus
    /// <see cref="RoomFrames.IronGap"/>, which on a default footprint is the piece's outer edge exactly. It
    /// keeps clear of the <b>door corridor</b>, that opening projected out to the row, so nobody walks out
    /// into it, and stands on the player's <b>right</b> as they leave, falling to their left where the piece
    /// has no ground for a cube there.</summary>
    private static double[]? Iron(
        BlockRect piece, BlockRect footprint, IReadOnlyList<RoomEdge> doors, string facing,
        double markerX, double markerZ)
    {
        var frame = RoomFrames.Resolve(piece, footprint, shellBound: true,
            piece.MinX + markerX, piece.MinZ + markerZ, [], doors, out _);
        if (frame is null || RoomEdges.Nearest(SpawnFacings.Direction(facing), doors) is not { } door) return null;
        if (frame.Doors.FirstOrDefault(cut => cut.Edge == door) is not { Width: > 0 } opening) return null;

        var alongX = door.AlongX();
        var outward = door.Positive()
            ? (alongX ? footprint.MaxZ : footprint.MaxX) + RoomFrames.IronGap
            : (alongX ? footprint.MinZ : footprint.MinX) - RoomFrames.IronGap - RoomFrames.IronSpan;

        // The corridor's two flanks, the player's right hand first. Walking out through a +z or a −x door puts
        // their right on the low side of the wall's own axis, and through a −z or a +x door on the high side.
        var right = opening.Lo + (door is RoomEdge.PosZ or RoomEdge.NegX
            ? -RoomFrames.IronSpan
            : opening.Width);
        var left = opening.Lo + (door is RoomEdge.PosZ or RoomEdge.NegX
            ? opening.Width
            : -RoomFrames.IronSpan);

        foreach (var aside in (int[])[right, left])
        {
            var (cubeMinX, cubeMinZ) = alongX ? (aside, outward) : (outward, aside);
            double ironX = cubeMinX + RoomFrames.IronSpan / 2.0, ironZ = cubeMinZ + RoomFrames.IronSpan / 2.0;
            var seated = RoomFrames.ResolveRoom(piece, footprint, shellBound: true,
                piece.MinX + markerX, piece.MinZ + markerZ, [], doors, [(ironX, ironZ)], out _);
            if (seated?.Iron is [{ Placeable: true }, ..]) return [ironX - piece.MinX, ironZ - piece.MinZ];
        }
        return null;
    }

    private static double Centre(int lo, int hi) => (lo + hi) / 2.0;
}
