using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Pgm.Plan;

/// <summary>What a freshly drawn role piece carries: the marker the room is built around and the footprint
/// the building stands on, both as piece-relative block offsets ready to store.</summary>
public readonly record struct DrawnRoom(double[] At, double[] Footprint);

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
            [footprint.MinX - piece.MinX, footprint.MinZ - piece.MinZ, footprint.Width, footprint.Depth]);
    }

    private static double Centre(int lo, int hi) => (lo + hi) / 2.0;
}
