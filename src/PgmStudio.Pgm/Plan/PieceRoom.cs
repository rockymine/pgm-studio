using PgmStudio.Domain;
using PgmStudio.Geom;

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
    /// <summary>The marker and footprint for a piece of this role, or null where the role carries no room or
    /// the piece is too small to raise a shell on (<c>WX2</c>) — a piece that cannot be built on is left bare
    /// rather than seeded with a rectangle its own export would refuse.</summary>
    /// <remarks>Sized for a <b>walled</b> room throughout. A plan states no room style
    /// (docs/world-export/structures.md §9), so the seed cannot know whether a shell will stand here, and a
    /// footprint that holds walls holds an open pad too — the safe direction to be wrong in.</remarks>
    public static DrawnRoom? ForPiece(BlockRect piece, string role, string facing)
    {
        if (role != PlanRoles.Spawn && role != PlanRoles.WoolRoom) return null;
        RoomEdge? doorEdge = role == PlanRoles.Spawn ? RoomEdges.OfFacing(facing) : null;

        // The marker sits at the centre of the room the piece affords, not of the piece: the door's gap moves
        // the room off the piece's own middle, and a player arrives inside the building. Probing with the
        // piece centre is what names that room before there is a marker to name it with.
        var probe = RoomFrames.DefaultFootprint(piece, doorEdge, Centre(piece.MinX, piece.MaxX),
            Centre(piece.MinZ, piece.MaxZ), walled: true);
        var (markerX, markerZ) = RoomFrames.SameParity(
            Centre(probe.MinX, probe.MaxX) - piece.MinX, Centre(probe.MinZ, probe.MaxZ) - piece.MinZ);

        // The footprint the marker actually seats in. The parity nudge can move the marker half a block off
        // the room's middle, which the gap yields to, so the answer is resolved against the seeded marker
        // rather than against the probe it came from.
        var footprint = RoomFrames.DefaultFootprint(
            piece, doorEdge, piece.MinX + markerX, piece.MinZ + markerZ, walled: true);
        if (RoomFrames.FootprintTooSmall(footprint.Width, footprint.Depth, walled: true)) return null;

        return new DrawnRoom(
            [markerX, markerZ],
            [footprint.MinX - piece.MinX, footprint.MinZ - piece.MinZ, footprint.Width, footprint.Depth],
            doorEdge is { } door ? Iron(piece, footprint, door, markerX, markerZ) : null);
    }

    /// <summary>The spawn's one iron marker, or null where the yard has no room for a cube. One is the seed;
    /// adding more is the author's. It stands hard against the piece's outer edge — the door gap is the cube
    /// plus its clear air exactly, so that is the only row along the door axis a cube fits in — and beside the
    /// <b>door corridor</b>, the door's own opening projected out to that edge, so nobody walks out into it.
    /// The low side of the corridor every time, which the author slides along.</summary>
    private static double[]? Iron(
        BlockRect piece, BlockRect footprint, RoomEdge door, double markerX, double markerZ)
    {
        var frame = RoomFrames.Resolve(piece, footprint, walled: true,
            piece.MinX + markerX, piece.MinZ + markerZ, [], door, out _);
        if (frame?.Doors is not [{ } opening, ..]) return null;

        var alongX = door is RoomEdge.NegZ or RoomEdge.PosZ;
        var outward = door is RoomEdge.NegZ or RoomEdge.NegX
            ? (alongX ? piece.MinZ : piece.MinX)
            : (alongX ? piece.MaxZ : piece.MaxX) - RoomFrames.IronSpan;
        var aside = opening.Lo - RoomFrames.IronSpan;
        var (cubeMinX, cubeMinZ) = alongX ? (aside, outward) : (outward, aside);

        double ironX = cubeMinX + RoomFrames.IronSpan / 2.0, ironZ = cubeMinZ + RoomFrames.IronSpan / 2.0;
        var seated = RoomFrames.ResolveRoom(piece, footprint, walled: true,
            piece.MinX + markerX, piece.MinZ + markerZ, [], door, [(ironX, ironZ)], out _);
        return seated?.Iron is [{ Placeable: true }, ..]
            ? [ironX - piece.MinX, ironZ - piece.MinZ]
            : null;
    }

    private static double Centre(int lo, int hi) => (lo + hi) / 2.0;
}
