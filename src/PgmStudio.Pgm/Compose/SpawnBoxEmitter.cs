using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The filled spawn box: its terrain pieces (slot- and box-labeled), the <see cref="PlanRoles.Spawn"/>
/// room the lane dead-ends into, the spawn marker's offset within that room, and the length of the entry run
/// (from the hub-side edge to the first turn/room) a wool box may dock along.</summary>
public sealed record EmittedSpawn(IReadOnlyList<GrownPiece> Pieces, GrownPiece Room, double[] MarkerAt, int EntryLen);

/// <summary>
/// The spawn binding over <see cref="ShapeEmitter"/> — the <b>second box kind</b> after the wool box. A spawn
/// is a terminal-capped approach like a wool arm, so the same emitter, classifier and mirror apply; it just
/// admits a smaller <b>shape profile as data</b> (<see cref="Families"/> = {I, L} only, small boxes per SP)
/// and its terminal is a <see cref="PlanRoles.Spawn"/> room carrying the spawn marker rather than a wool room.
/// Making the machinery explicit is the point: the profile (families + <see cref="Box"/>) is plain data the
/// per-box shape menu and footprint budget read directly — this and the wool box are the first two rows of
/// the per-kind profile table.
///
/// <para><see cref="Fill"/> fills a plan-cell <see cref="Box"/> the same way the wool box does — emit mouth-up,
/// orient onto the docking edge via <see cref="MouthOrient"/> — and stamps the spawn specifics: every piece
/// carries its slot and the spawn <see cref="BoxRef"/>, the room takes the <see cref="PlanRoles.Spawn"/> role,
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
    /// terminal a <see cref="PlanRoles.Spawn"/> room. Mirrors the wool box's plan-cell fill — emit mouth-up, then
    /// orient onto the mouth via <see cref="MouthOrient"/> — but stamps the spawn role and reports the
    /// <b>entry-run length</b> (outward from the mouth) a wool box may dock along. Pieces carry the
    /// <paramref name="box"/> label + their slot; the room takes <paramref name="roomId"/>. <paramref name="flip"/>
    /// puts an L's hook on the other side. Null when the footprint is too small (a directed signal, not a throw).</summary>
    public static EmittedSpawn? Fill(Box box, BoxEdge mouth, ShapeFamily family, int cw, bool flip, string roomId)
    {
        if (!Families.Contains(family))
            throw new ComposeException($"the spawn profile admits only I and L (requested {family}).");

        // the mouth's along × depth frame (I/L never transpose, so the box dims map straight through)
        var lateral = mouth is BoxEdge.Left or BoxEdge.Right;
        var (alongLen, depth) = lateral ? (box.Rect[3], box.Rect[2]) : (box.Rect[2], box.Rect[3]);
        var (minW, minH) = ShapeEmitter.MinBox(family, cw);
        if (alongLen < minW || depth < minH) return null;

        var raw = ShapeEmitter.Emit(family, alongLen, depth, cw, flip);
        var (mouthTop, w, h) = ShapeEmitter.OrientMouthTop(raw, family, flip, alongLen, depth);
        var shape = MouthOrient.To(mouthTop, mouth, w, h);

        var boxRef = new BoxRef(box.Id, BoxKind.Spawn);
        var pieces = new List<GrownPiece>(shape.Terrain.Count + 1);
        var n = 1;
        foreach (var (r, slot) in shape.Terrain)
            pieces.Add(new GrownPiece($"{box.Id}-t{n++}", [box.Rect[0] + r[0], box.Rect[1] + r[1], r[2], r[3]],
                PlanRoles.Piece, slot, boxRef));
        var rr = shape.Room;
        var room = new GrownPiece(roomId, [box.Rect[0] + rr[0], box.Rect[1] + rr[1], rr[2], rr[3]],
            PlanRoles.Spawn, ApproachSlots.Room, boxRef);
        pieces.Add(room);

        // the entry run's outward length (perpendicular to the mouth) — the interval a wool box docks along
        var entry = shape.Terrain.First(t => t.Slot == ApproachSlots.Entry).Rect;
        var entryLen = mouth is BoxEdge.Top or BoxEdge.Bottom ? entry[3] : entry[2];
        return new EmittedSpawn(pieces, room, shape.At, entryLen);
    }
}
