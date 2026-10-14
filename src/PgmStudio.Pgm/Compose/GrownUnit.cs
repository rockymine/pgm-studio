using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Compose;

/// <summary>A composed unit piece: a rect in cell coordinates (<see cref="Plan.PlanPiece.Rect"/> convention),
/// its map-level role, and — when it came out of a box fill — its <see cref="Part"/> in its compound
/// (<see cref="CompoundParts"/>) and its <see cref="Box"/> ownership. Part + box are the full label
/// (<c>wool-a/entry</c>) the compose-side rules bind to. Labels are compose-internal: every compose move
/// preserves them, and they drop only at <see cref="Composer"/> assembly (the written plan is label-free by
/// design).</summary>
public sealed record GrownPiece(string Id, CellRect Rect, string Role = PlanRoles.Piece, string? Part = null, BoxRef? Box = null);

/// <summary>A point inside a piece: the <see cref="Piece"/> by id, and the point's offset from the piece's
/// minimum corner, in cells. A spawn or a wool stands at one.</summary>
public sealed record Marker(string Piece, double X, double Z);

/// <summary>The team-0 unit the allocate→fill pipeline produces: its pieces, the marker its spawn stands at and the
/// way the spawn faces (SP3: toward the enemy by default), and the markers its wools stand at — in plan cell
/// coordinates, ready for <see cref="Composer"/> to assemble into a <see cref="Plan.PlanModel"/>.</summary>
public sealed record GrownUnit(
    IReadOnlyList<GrownPiece> Pieces, Marker Spawn, string SpawnFacing, IReadOnlyList<Marker> Wools);

/// <summary>Lays a compound filled into a box onto the plan: its pieces and the marker its room holds.</summary>
public static class GrownPieces
{
    /// <summary>The plan role a piece with <paramref name="part"/> takes in a box of <paramref name="kind"/>: a
    /// room is the spawn in a spawn box and a wool room anywhere else; every other part is plain ground.</summary>
    public static string RoleOf(BoxKind kind, string? part) =>
        part != CompoundParts.Room ? PlanRoles.Piece : kind == BoxKind.Spawn ? PlanRoles.Spawn : PlanRoles.WoolRoom;

    /// <summary>The <paramref name="compound"/>'s pieces moved from box-local cells to the box's corner
    /// (<paramref name="x"/>, <paramref name="z"/>): each named <c>{idPrefix}-t{n}</c> in order, the room named
    /// <paramref name="roomId"/> and last, every piece carrying its part, its role in a box of
    /// <paramref name="kind"/>, and <paramref name="box"/> where one owns it. The marker is the compound's, made
    /// relative to the room; null for a compound with no room.</summary>
    public static (List<GrownPiece> Pieces, Marker? Marker) Lay(
        Compound compound, int x, int z, string idPrefix, string roomId, BoxKind kind, BoxRef? box)
    {
        var pieces = new List<GrownPiece>(compound.Pieces.Count);
        var n = 1;
        foreach (var (r, part) in compound.Pieces.Where(piece => piece.Part != CompoundParts.Room))
            pieces.Add(new GrownPiece($"{idPrefix}-t{n++}", new(x + r.X, z + r.Z, r.Width, r.Height),
                RoleOf(kind, part), part, box));
        if (compound.Room is not { } room) return (pieces, null);

        var rr = room.Rect;
        pieces.Add(new GrownPiece(roomId, new(x + rr.X, z + rr.Z, rr.Width, rr.Height),
            RoleOf(kind, CompoundParts.Room), CompoundParts.Room, box));
        var marker = compound.Marker ?? throw new ArgumentException("a compound that ends in a room carries its marker.");
        return (pieces, new Marker(roomId, marker.X - rr.X, marker.Z - rr.Z));
    }
}
