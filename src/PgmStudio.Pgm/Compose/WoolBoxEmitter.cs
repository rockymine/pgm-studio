using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Compose;

/// <summary>The base wool-approach shape a wool box is filled with — the terrain topology the categorizer
/// reads back (see the base wool-approach catalog in <c>docs/contracts/layout-generation.md</c> §2). This
/// first pass covers the thin-corridor families the bend count already names; the branched (H), looped
/// (donut) and solid (plug) families come with the promoted skeleton classifier.</summary>
public enum ApproachFamily { I, L, Z }

/// <summary>Where the wool room sits relative to the approach's final segment. <see cref="Inline"/> continues
/// it straight (the plain dead-end). <see cref="SideTuck"/> turns the room off perpendicular at the end — the
/// catalog's <c>side-tuck</c>: still an <b>I</b> lane, because the categorizer excludes the room from the bend
/// read, so the straight approach reads as I while the wool hangs off its end.</summary>
public enum RoomPlacement { Inline, SideTuck }

/// <summary>A wool box: the axis-aligned cell region a single wool's approach is emitted into. The approach
/// enters at the <b>mouth</b> (the top edge, <c>z = Z</c> — the hub-side interface) and dead-ends at the
/// <see cref="WoolBoxEmitter"/>-placed room deep in the box.</summary>
public readonly record struct WoolBox(int X, int Z, int W, int H);

/// <summary>The terrain a wool box was filled with: the approach-lane <see cref="Terrain"/> pieces plus the
/// dead-end <see cref="WoolRoom"/>, with the wool marker at piece-relative offset <see cref="At"/>.</summary>
public sealed record EmittedApproach(IReadOnlyList<GrownPiece> Terrain, GrownPiece WoolRoom, double[] At);

/// <summary>
/// Fills a wool box with a base wool-approach shape — the composer's mirror of the categorizer's shape
/// read. Given the box, a target <see cref="ApproachFamily"/> and a corridor width, it emits rectilinear
/// terrain that realizes exactly that family: the lane enters at the mouth (top edge), turns the family's
/// number of times, and caps at a dead-end wool room. Output is composer-native <see cref="GrownPiece"/>s
/// (id prefix <paramref name="idPrefix"/>), so it drops into the grown unit; <see cref="AsPlan"/> wraps one
/// emission as a standalone plan for classification and rendering.
///
/// <para>Geometry is deliberately clean: every segment abuts its neighbour along a full corridor-width edge
/// (never a corner or a &lt;corridor seam), and the whole shape stays inside the box. A box too small for the
/// requested family's turns throws — the caller sizes the box to the family (a Z needs room to double back).</para>
/// </summary>
public static class WoolBoxEmitter
{
    /// <summary>The dead-end room's depth along the final corridor, in cells — a two-cell (~10-block) plateau
    /// that clears the export stamp, matching <see cref="SpawnWoolRooms"/>.</summary>
    public const int RoomDepthCells = 2;

    /// <summary>Emit <paramref name="family"/> into <paramref name="box"/> at the given
    /// <paramref name="corridorWidth"/> (cells). <paramref name="flip"/> mirrors the shape across the box's
    /// vertical centre (the turn goes left instead of right) so both handednesses are reachable.</summary>
    public static EmittedApproach Emit(ApproachFamily family, WoolBox box, int corridorWidth, bool flip = false, RoomPlacement roomPlacement = RoomPlacement.Inline, string idPrefix = "wa")
    {
        var cw = corridorWidth;
        if (cw < 2) throw new ComposeException($"corridor width {cw} < 2 (a lane is at least one 10-block cell pair).");
        if (cw > box.W) throw new ComposeException($"corridor width {cw} exceeds box width {box.W}.");
        if (roomPlacement == RoomPlacement.SideTuck && family != ApproachFamily.I)
            throw new ComposeException($"side-tuck room is only supported for the I family in this pass (requested {family}).");

        var t = new List<int[]>();
        int[] room;
        double[]? at = null;                                 // wool marker offset within the room (defaults to centre)
        switch (family)
        {
            case ApproachFamily.I when roomPlacement == RoomPlacement.SideTuck:
            {
                // straight approach, room turned perpendicular off the end — the room extends past the lane
                // column so the wool tucks to the side. Still reads I: the lane (room excluded) is straight.
                Need(box.W >= cw + 1 && box.H >= RoomDepthCells + cw, family, box);
                int laneLen = box.H - RoomDepthCells, roomLen = Math.Min(box.W, 2 * cw);
                t.Add([0, 0, cw, laneLen]);                  // straight vertical approach (left)
                room = [0, laneLen, roomLen, RoomDepthCells];// room turns off to the +x side at the end
                at = [roomLen - cw / 2.0, RoomDepthCells / 2.0];  // wool at the tucked (far) end
                break;
            }
            case ApproachFamily.I:
            {
                Need(box.H >= RoomDepthCells + 1, family, box);
                int lx = (box.W - cw) / 2, laneH = box.H - RoomDepthCells;
                t.Add([lx, 0, cw, laneH]);
                room = [lx, laneH, cw, RoomDepthCells];
                break;
            }
            case ApproachFamily.L:
            {
                // vertical arm down one side, a horizontal band across the bottom, room at its far end. The
                // +1 guarantees ≥1 cell of horizontal arm beyond the vertical, so the bend is real (without it
                // the band sits in the vertical's own column and the shape collapses to a straight I).
                Need(box.W >= cw + RoomDepthCells + 1 && box.H >= 2 * cw, family, box);
                int vLx = 0, bandZ = box.H - cw, roomLen = RoomDepthCells;
                t.Add([vLx, 0, cw, bandZ]);                          // vertical arm
                t.Add([0, bandZ, box.W - roomLen, cw]);              // horizontal band up to the room
                room = [box.W - roomLen, bandZ, roomLen, cw];        // dead-end at the far side of the band
                break;
            }
            case ApproachFamily.Z:
            {
                // top arm on the left, a full-width band, bottom arm on the right ending in the room.
                Need(box.W >= 2 * cw && box.H >= 3 * cw + RoomDepthCells, family, box);
                int z1 = (box.H - RoomDepthCells - cw) / 2;          // top-arm length (balanced with the bottom arm)
                int botZ = z1 + cw, botLen = box.H - RoomDepthCells - botZ;
                t.Add([0, 0, cw, z1]);                               // top arm (left)
                t.Add([0, z1, box.W, cw]);                           // crossing band
                t.Add([box.W - cw, botZ, cw, botLen]);               // bottom arm (right)
                room = [box.W - cw, box.H - RoomDepthCells, cw, RoomDepthCells];
                break;
            }
            default: throw new ComposeException($"unsupported family {family}.");
        }

        at ??= [room[2] / 2.0, room[3] / 2.0];
        if (flip)
        {
            foreach (var r in t) r[0] = box.W - r[0] - r[2];
            room[0] = box.W - room[0] - room[2];
            at = [room[2] - at[0], at[1]];                   // mirror the marker within the flipped room
        }

        // translate box-local -> absolute and wrap as pieces
        var terrain = new List<GrownPiece>(t.Count);
        for (var i = 0; i < t.Count; i++)
            terrain.Add(new GrownPiece($"{idPrefix}-t{i + 1}", [box.X + t[i][0], box.Z + t[i][1], t[i][2], t[i][3]]));
        var woolRoom = new GrownPiece($"{idPrefix}-wool", [box.X + room[0], box.Z + room[1], room[2], room[3]], PlanRoles.WoolRoom);
        return new EmittedApproach(terrain, woolRoom, at);
    }

    private static void Need(bool ok, ApproachFamily family, WoolBox box)
    {
        if (!ok) throw new ComposeException($"wool box {box.W}x{box.H} is too small for family {family}.");
    }

    /// <summary>Wrap a single emission as a standalone <c>symmetry:none</c> plan — the form the categorizer and
    /// the shape gallery consume.</summary>
    public static PlanModel AsPlan(EmittedApproach a, string name = "wool-box")
    {
        var plan = new PlanModel
        {
            Meta = new PlanMeta { Name = name },
            Globals = new PlanGlobals { Cell = 5, Symmetry = "none", MaxPlayers = 12, Surface = 9, Headroom = 11 },
        };
        foreach (var p in a.Terrain) plan.Pieces.Add(new PlanPiece { Id = p.Id, Role = p.Role, Rect = p.Rect });
        plan.Pieces.Add(new PlanPiece { Id = a.WoolRoom.Id, Role = a.WoolRoom.Role, Rect = a.WoolRoom.Rect });
        plan.Placements.Wools.Add(new WoolPlacement { Piece = a.WoolRoom.Id, At = a.At });
        return plan;
    }
}
