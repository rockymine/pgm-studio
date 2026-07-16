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
/// <para><see cref="Fill"/> emits the family in the canonical (mouth-top) frame, then maps it into the growth
/// frame the spawn docks the hub's back edge and runs outward through: box-local <c>bz</c> is outward distance
/// <c>u</c>, box-local <c>bx</c> is the cross coordinate <c>v</c>, and an L's turn goes to whichever side
/// <paramref name="dir"/> selects — the entry run stays pinned on the hub edge either way. Every piece carries
/// its slot and the spawn <see cref="BoxRef"/>; the room takes the <see cref="PlanRoles.Spawn"/> role.</para>
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

    /// <summary>Fill a spawn box docking the hub edge at <paramref name="spawnU0"/> (outward <c>u</c>), its
    /// entry run pinned at cross coordinate <paramref name="vBase"/>, turning to side <paramref name="dir"/>
    /// (+1/-1, L only). Pieces map through <paramref name="frame"/> into real cell coordinates and carry the
    /// <paramref name="boxId"/> box label + their slot; the room takes <paramref name="roomId"/> and the
    /// <see cref="PlanRoles.Spawn"/> role.</summary>
    internal static EmittedSpawn Fill(
        ShapeFamily family, int cw, int runCells, int turnCells, int dir,
        Frame frame, int spawnU0, int vBase, string boxId, string roomId)
    {
        var (w, h) = Box(family, cw, runCells, turnCells);
        EmittedShape shape;
        try { shape = ShapeEmitter.Emit(family, w, h, cw); }
        catch (ArgumentException e) { throw new ComposeException(e.Message); }

        // box-local (bx -> v, bz -> u); dir < 0 reflects the cross axis about the entry column so the turn
        // flips side while the entry stays pinned on [vBase, vBase+cw].
        int VMin(int bx, int bw) => dir >= 0 ? vBase + bx : vBase + cw - (bx + bw);
        double VPoint(double bx) => dir >= 0 ? vBase + bx : vBase + cw - bx;
        int[] Map(int[] r) => frame.ToRect(spawnU0 + r[1], r[3], VMin(r[0], r[2]), r[2]);

        var box = new BoxRef(boxId, BoxKind.Spawn);
        var pieces = new List<GrownPiece>(shape.Terrain.Count + 1);
        var n = 1;
        foreach (var (r, slot) in shape.Terrain)
            pieces.Add(new GrownPiece($"{boxId}-t{n++}", Map(r), PlanRoles.Piece, slot, box));

        var rr = shape.Room;
        var room = new GrownPiece(roomId, Map(rr), PlanRoles.Spawn, ApproachSlots.Room, box);
        pieces.Add(room);

        // the marker within the room, mapped through the cross reflection + the frame
        var markerU = spawnU0 + rr[1] + shape.At[1];
        var markerV = VPoint(rr[0] + shape.At[0]);
        var at = frame.LocalAt(spawnU0 + rr[1], rr[3], VMin(rr[0], rr[2]), rr[2], markerU, markerV);

        // the entry run's outward length (hub edge -> first turn/room) = the entry piece's bz extent
        var entry = shape.Terrain[0].Rect;                 // the entry is always emitted first (slot template)
        var entryLen = entry[3];
        return new EmittedSpawn(pieces, room, at, entryLen);
    }
}
