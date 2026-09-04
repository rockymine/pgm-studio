using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Plan;

/// <summary>
/// Which walls a role piece's room opens through (<c>WX6</c>). A room's doors are cut where its piece meets
/// more board — a land seam with a neighbour, or a build-zone frontage — so a door always opens onto ground
/// somebody can stand on, and a wall with nothing behind it stays shut.
///
/// <para>Both roles read the same abutment; what differs is how much of it becomes a door. A <b>wool cage</b>
/// takes one per entry segment, because every side an attacker can reach it from is a side they may come
/// through. A <b>spawn</b> takes at most <see cref="SpawnMax"/> — a hall with a door on every wall is a
/// crossroads rather than a room, and the two widest are where the team actually leaves.</para>
/// </summary>
public static class PieceDoors
{
    /// <summary>The most doors a spawn hall is cut with: one where the piece meets the board on a single
    /// side, two where it meets it on more. A corner spawn earns both; nothing earns three.</summary>
    public const int SpawnMax = 2;

    /// <summary>The walls a spawn hall on this piece opens through, in the order they are cut. The piece's
    /// open sides, widest opening first, at most <see cref="SpawnMax"/> of them; where two open equally wide
    /// the one the player looks more nearly at is cut first, so the door they walk out of is
    /// <c>Doors[0]</c>. A piece that meets nothing — an island with no interface at all — opens on the wall
    /// its facing leans into, which is the one door it can still be given.</summary>
    public static List<RoomEdge> ForSpawn(ContactGraph d, string pieceId, string facing)
    {
        var direction = SpawnFacings.Direction(facing);
        if (d.Piece(pieceId) is not { } piece) return Fallback(direction);

        var open = OpenSides(d, piece);
        if (open.Count == 0) return Fallback(direction);

        return [.. open
            .OrderByDescending(side => side.Value)
            .ThenByDescending(side => side.Key.Lean(direction))
            .ThenBy(side => Array.IndexOf(RoomEdges.All, side.Key))
            .Take(SpawnMax)
            .Select(side => side.Key)];

        static List<RoomEdge> Fallback((int Dx, int Dz) direction) =>
            RoomEdges.Nearest(direction, RoomEdges.All) is { } edge ? [edge] : [];
    }

    /// <summary>Which sides of a wool cage its entries stand on, one door per segment: the land seams and
    /// frontline edges the room abuts, each a degenerate rect on one of its four boundary lines, so the side
    /// is which line it lies on.</summary>
    public static List<RoomEdge> ForWoolRoom(ContactGraph d, string pieceId, BlockRect room)
    {
        var edges = new List<RoomEdge>();
        foreach (var seg in PlanCompiler.WoolEntrySegments(d, pieceId))
        {
            if (seg.MinX == seg.MaxX && seg.MinX == room.MinX) edges.Add(RoomEdge.NegX);
            else if (seg.MinX == seg.MaxX && seg.MinX == room.MaxX) edges.Add(RoomEdge.PosX);
            else if (seg.MinZ == seg.MaxZ && seg.MinZ == room.MinZ) edges.Add(RoomEdge.NegZ);
            else if (seg.MinZ == seg.MaxZ && seg.MinZ == room.MaxZ) edges.Add(RoomEdge.PosZ);
        }
        return edges;
    }

    /// <summary>The piece's sides that open onto more board rather than the void, each with the widest
    /// opening found on it: a land or narrow interface with another piece, or a build-zone frontage. A side
    /// earns its direction from where the opening's segment actually lies on the piece's own rect, not from
    /// which piece is "bigger", so an opening on a piece's north edge marks north open regardless of what is
    /// across it.</summary>
    public static Dictionary<RoomEdge, int> OpenSides(ContactGraph d, DerivedPiece piece)
    {
        var open = new Dictionary<RoomEdge, int>();
        void Mark(int x1, int z1, int x2, int z2)
        {
            RoomEdge? side =
                x1 == piece.Rect.MinX && x2 == piece.Rect.MinX ? RoomEdge.NegX
                : x1 == piece.Rect.MaxX && x2 == piece.Rect.MaxX ? RoomEdge.PosX
                : z1 == piece.Rect.MinZ && z2 == piece.Rect.MinZ ? RoomEdge.NegZ
                : z1 == piece.Rect.MaxZ && z2 == piece.Rect.MaxZ ? RoomEdge.PosZ
                : null;
            if (side is not { } edge) return;
            var width = Math.Max(Math.Abs(x2 - x1), Math.Abs(z2 - z1));
            if (width > open.GetValueOrDefault(edge)) open[edge] = width;
        }
        foreach (var c in d.LandInterfaces.Where(c => c.A == piece.Id || c.B == piece.Id))
        {
            var other = d.Piece(c.A == piece.Id ? c.B : c.A);
            if (other is null) continue;
            var (x1, z1, x2, z2) = ContactGraph.BorderSegment(piece.Rect, other.Value.Rect);
            Mark(x1, z1, x2, z2);
        }
        foreach (var edge in d.FrontlineEdges.Where(e => e.Piece == piece.Id))
            Mark(edge.X1, edge.Z1, edge.X2, edge.Z2);
        return open;
    }

}
