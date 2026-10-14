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
public sealed record UnitPiece(string Id, CellRect Rect, string Role = PlanRoles.Piece, string? Part = null, BoxRef? Box = null);

/// <summary>A point inside a piece: the <see cref="Piece"/> by id, and the point's offset from the piece's
/// minimum corner, in cells. A spawn or a wool stands at one.</summary>
public sealed record Marker(string Piece, double X, double Z);

/// <summary>One team's half of the board, the team-0 unit the allocate→fill pipeline produces: its pieces, the marker its spawn stands at and the
/// way the spawn faces (SP3: toward the enemy by default), and the markers its wools stand at — in plan cell
/// coordinates, ready for <see cref="Composer"/> to assemble into a <see cref="Plan.PlanModel"/>.</summary>
public sealed record TeamUnit(
    IReadOnlyList<UnitPiece> Pieces, Marker Spawn, string SpawnFacing, IReadOnlyList<Marker> Wools);

/// <summary>Lays a compound filled into a box onto the board: its pieces and the marker its room holds.</summary>
public static class UnitPieces
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
    public static (List<UnitPiece> Pieces, Marker? Marker) Lay(
        Compound compound, int x, int z, string idPrefix, string roomId, BoxKind kind, BoxRef? box)
    {
        var pieces = new List<UnitPiece>(compound.Pieces.Count);
        var n = 1;
        foreach (var (r, part) in compound.Pieces.Where(piece => piece.Part != CompoundParts.Room))
            pieces.Add(new UnitPiece($"{idPrefix}-t{n++}", new(x + r.X, z + r.Z, r.Width, r.Height),
                RoleOf(kind, part), part, box));
        if (compound.Room is not { } room) return (pieces, null);

        var rr = room.Rect;
        pieces.Add(new UnitPiece(roomId, new(x + rr.X, z + rr.Z, rr.Width, rr.Height),
            RoleOf(kind, CompoundParts.Room), CompoundParts.Room, box));
        var marker = compound.Marker ?? throw new ArgumentException("a compound that ends in a room carries its marker.");
        return (pieces, new Marker(roomId, marker.X - rr.X, marker.Z - rr.Z));
    }

    /// <summary>The <paramref name="compound"/> filling <paramref name="box"/>, laid at the box's corner: its pieces
    /// named after the box and labelled with it, the room named <c>{box id}-room</c>.</summary>
    public static (List<UnitPiece> Pieces, Marker? Marker) Lay(Compound compound, Box box) =>
        Lay(compound, box.Rect.X, box.Rect.Z, box.Id, $"{box.Id}-room", box.Kind, box.Ref);

    /// <summary>One <paramref name="compound"/> as a standalone <c>symmetry:none</c> plan, laid at the origin —
    /// the form the classifier and the shape pages read. A compound with a room carries its wool marker.</summary>
    public static PlanModel AsPlan(Compound compound, string name, string idPrefix = "wa")
    {
        var plan = new PlanModel
        {
            Meta = new PlanMeta { Name = name },
            Globals = new PlanGlobals { Cell = 5, Symmetry = "none", MaxPlayers = 12, Surface = 9 },
        };
        var (pieces, marker) = Lay(compound, 0, 0, idPrefix, $"{idPrefix}-room", BoxKind.Wool, null);
        foreach (var piece in pieces)
            plan.Pieces.Add(new PlanPiece { Id = piece.Id, Role = piece.Role, Rect = piece.Rect });
        if (marker is not null)
            plan.Placements.Wools.Add(new WoolMarker { Piece = marker.Piece, At = Composer.MarkerOffset(marker, plan.Globals.Cell) });
        return plan;
    }
}
