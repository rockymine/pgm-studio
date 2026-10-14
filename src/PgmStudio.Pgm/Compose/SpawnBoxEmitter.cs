using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Compose;

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
/// turn onto the docking edge (<see cref="Compound.Turned"/>). Laying it gives the room the
/// <see cref="PlanRoles.Spawn"/> role (<see cref="UnitPieces.Lay(Compound, Box)"/>); the marker's facing (SP3, toward the hub)
/// is the filler's.</para>
/// </summary>
public static class SpawnBoxEmitter
{
    /// <summary>The spawn's shape profile: only the un-escalated approaches — a straight lane (I) or one bend
    /// (L). A spawn never forks or folds, so the fill menu is exactly these two.</summary>
    public static readonly IReadOnlyList<ShapeFamily> Families = [ShapeFamily.I, ShapeFamily.L];

    /// <summary>The canonical (mouth-top) box a spawn family needs to carry a straight run of
    /// <paramref name="runCells"/> back from the hub and, for an L, a <paramref name="turnCells"/> sideways
    /// hook. Small by construction (SP): the spawn absorbs run length, not area.</summary>
    public static (int W, int H) Box(ShapeFamily family, int laneWidth, int runCells, int turnCells)
    {
        var (minW, minH) = ShapeEmitter.MinBox(family, laneWidth);
        var rd = ShapeEmitter.RoomDepthCells;
        return family switch
        {
            ShapeFamily.I => (laneWidth, Math.Max(minH, runCells + rd)),
            ShapeFamily.L => (Math.Max(minW, laneWidth + turnCells + rd), Math.Max(minH, runCells + laneWidth)),
            _ => throw new ComposeException($"the spawn profile admits only I and L (requested {family})."),
        };
    }

    /// <summary>Fill a spawn <see cref="Box"/> docking <paramref name="mouth"/> with an I or L: the compound,
    /// box-local, ending in the room the spawn stands in. Mirrors the wool box's fill — emit mouth-up, then turn
    /// onto the mouth (<see cref="Compound.Turned"/>). <paramref name="flip"/> puts an L's hook on the other side.
    /// <c>null</c> when the footprint is too small (a directed signal, not a throw).</summary>
    public static Compound? Fill(Box box, BoxEdge mouth, ShapeFamily family, int laneWidth, bool flip) =>
        Fill(box, mouth, family, laneWidth, flip, out _);

    /// <inheritdoc cref="Fill(Box, BoxEdge, ShapeFamily, int, bool)"/>
    /// <remarks>On a <c>null</c> return, <b>why</b> the fill was refused — the directed reason in
    /// the shared <see cref="FillRejection"/> vocabulary (the family's minimum box, in the box's own frame).
    /// <c>null</c> when the fill succeeded.</remarks>
    public static Compound? Fill(
        Box box, BoxEdge mouth, ShapeFamily family, int laneWidth, bool flip, out FillRejection? rejection)
    {
        rejection = null;
        if (!Families.Contains(family))
            throw new ComposeException($"the spawn profile admits only I and L (requested {family}).");

        // the mouth's along × depth frame (I/L never transpose, so the box dims map straight through)
        var lateral = mouth is BoxEdge.Left or BoxEdge.Right;
        var (alongLen, depth) = lateral ? (box.Rect.Height, box.Rect.Width) : (box.Rect.Width, box.Rect.Height);
        var (minW, minH) = ShapeEmitter.MinBox(family, laneWidth);
        if (alongLen < minW || depth < minH)
        {
            // report the minimum in the box's own frame, so a lateral mouth's caller reads it without transposing
            rejection = lateral ? new FillRejection.TooSmall(minH, minW) : new FillRejection.TooSmall(minW, minH);
            return null;
        }

        var raw = ShapeEmitter.Emit(family, alongLen, depth, laneWidth, flip);
        var (mouthTop, w, h) = ShapeEmitter.OrientMouthTop(raw, family, alongLen, depth);
        return mouthTop.Turned(mouth.TopOnto(), w, h);
    }
}
