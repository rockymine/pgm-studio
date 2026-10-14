using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Compose;

/// <summary>The filled spawn box: its pieces (part- and box-labeled), the <see cref="PlanRoles.Spawn"/> room the lane
/// dead-ends into last, the <see cref="Marker"/> the spawn stands at, and the length of the entry run (from the
/// hub-side edge to the first turn/room) a wool box may dock along.</summary>
public sealed record PlacedSpawn(IReadOnlyList<GrownPiece> Pieces, Marker Marker, int EntryLen)
{
    /// <summary>The spawn room: the piece the marker stands on.</summary>
    public GrownPiece Room => Pieces.Single(piece => piece.Id == Marker.Piece);
}

/// <summary>
/// The spawn binding over <see cref="ShapeEmitter"/> — the <b>second box kind</b> after the wool box. A spawn
/// is a compound ending in a room like a wool's, so the same emitter, classifier and mirror apply; it just
/// admits a smaller <b>shape profile as data</b> (<see cref="Families"/> = {I, L} only, small boxes per SP)
/// and its room is a <see cref="PlanRoles.Spawn"/> room carrying the spawn marker rather than a wool room.
/// Making the machinery explicit is the point: the profile (families + <see cref="Box"/>) is plain data the
/// per-box shape menu and footprint budget read directly — this and the wool box are the first two rows of
/// the per-kind profile table.
///
/// <para><c>Fill</c> fills a plan-cell <see cref="Box"/> the same way the wool box does — emit mouth-up,
/// turn onto the docking edge (<see cref="Compound.Turned"/>) — and stamps the spawn specifics: every piece
/// carries its part and the spawn <see cref="BoxRef"/>, the room takes the <see cref="PlanRoles.Spawn"/> role,
/// and it reports the entry-run length a wool box may dock along. The grower allocates the box against the hub's
/// back edge and pins the entry run's cross band; the marker's facing (SP3, toward the axis) is stamped at
/// assembly.</para>
/// </summary>
public static class SpawnBoxEmitter
{
    /// <summary>The spawn's shape profile: only the un-escalated approaches — a straight lane (I) or one bend
    /// (L). A spawn never forks or folds, so the fill menu is exactly these two.</summary>
    public static readonly IReadOnlyList<ShapeFamily> Families = [ShapeFamily.I, ShapeFamily.L];

    /// <summary>The canonical (mouth-top) box a spawn family needs to carry a straight run of
    /// <paramref name="runCells"/> back from the hub and, for an L, a <paramref name="turnCells"/> sideways
    /// hook. Small by construction (SP): the spawn absorbs run length, not area.</summary>
    public static (int W, int H) Box(ShapeFamily family, int cw, int runCells, int turnCells)
    {
        var (minW, minH) = ShapeEmitter.MinBox(family, cw);
        var rd = ShapeEmitter.RoomDepthCells;
        return family switch
        {
            ShapeFamily.I => (cw, Math.Max(minH, runCells + rd)),
            ShapeFamily.L => (Math.Max(minW, cw + turnCells + rd), Math.Max(minH, runCells + cw)),
            _ => throw new ComposeException($"the spawn profile admits only I and L (requested {family})."),
        };
    }

    /// <summary>Fill a spawn <see cref="Box"/> (plan cells) docking <paramref name="mouth"/> with an I or L, its
    /// room a <see cref="PlanRoles.Spawn"/> room. Mirrors the wool box's plan-cell fill — emit mouth-up, then
    /// turn onto the mouth (<see cref="Compound.Turned"/>) — but stamps the spawn role and reports the
    /// <b>entry-run length</b> (outward from the mouth) a wool box may dock along. Pieces carry the
    /// <paramref name="box"/> label + their part; the room takes <paramref name="roomId"/>. <paramref name="flip"/>
    /// puts an L's hook on the other side. Null when the footprint is too small (a directed signal, not a throw).</summary>
    public static PlacedSpawn? Fill(Box box, BoxEdge mouth, ShapeFamily family, int cw, bool flip, string roomId) =>
        Fill(box, mouth, family, cw, flip, roomId, out _);

    /// <inheritdoc cref="Fill(Box, BoxEdge, ShapeFamily, int, bool, string)"/>
    /// <remarks>On a <c>null</c> return, <b>why</b> the fill was refused — the directed reason in
    /// the shared <see cref="FillRejection"/> vocabulary (the family's minimum box, in the mouth's frame).
    /// <c>null</c> when the fill succeeded.</remarks>
    public static PlacedSpawn? Fill(
        Box box, BoxEdge mouth, ShapeFamily family, int cw, bool flip, string roomId,
        out FillRejection? rejection)
    {
        rejection = null;
        if (!Families.Contains(family))
            throw new ComposeException($"the spawn profile admits only I and L (requested {family}).");

        // the mouth's along × depth frame (I/L never transpose, so the box dims map straight through)
        var lateral = mouth is BoxEdge.Left or BoxEdge.Right;
        var (alongLen, depth) = lateral ? (box.Rect.Height, box.Rect.Width) : (box.Rect.Width, box.Rect.Height);
        var (minW, minH) = ShapeEmitter.MinBox(family, cw);
        if (alongLen < minW || depth < minH)
        {
            // report the minimum in the box's own frame, so a lateral mouth's caller reads it without transposing
            rejection = lateral ? new FillRejection.TooSmall(minH, minW) : new FillRejection.TooSmall(minW, minH);
            return null;
        }

        var raw = ShapeEmitter.Emit(family, alongLen, depth, cw, flip);
        var (mouthTop, w, h) = ShapeEmitter.OrientMouthTop(raw, family, alongLen, depth);
        var shape = mouthTop.Turned(mouth.TopOnto(), w, h);

        var (pieces, marker) = GrownPieces.Lay(
            shape, box.Rect.X, box.Rect.Z, box.Id, roomId, BoxKind.Spawn, new BoxRef(box.Id, BoxKind.Spawn));

        // the entry run's outward length (perpendicular to the mouth) — the interval a wool box docks along
        var entry = shape.Pieces.First(piece => piece.Part == CompoundParts.Entry).Rect;
        var entryLen = mouth is BoxEdge.Top or BoxEdge.Bottom ? entry.Height : entry.Width;
        return new PlacedSpawn(pieces, marker!, entryLen);
    }
}
