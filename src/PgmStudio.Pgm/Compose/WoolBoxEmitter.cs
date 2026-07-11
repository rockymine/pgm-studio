using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Compose;

/// <summary>The base wool-approach shape a wool box is filled with — the terrain topology the categorizer
/// reads back (see the base wool-approach catalog in <c>docs/contracts/layout-generation.md</c> §2). This
/// first pass covers the thin-corridor families the bend count already names; the branched (H), looped
/// (donut) and solid (plug) families come with the promoted skeleton classifier.</summary>
public enum ApproachFamily { I, L, Z, Scythe, U, H, Donut, Plug }

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
    /// vertical centre (the turn goes left instead of right) so both handednesses are reachable.
    /// <paramref name="attachments"/> (donut) is the number of hub-side stubs, 1 or 2. <paramref name="woolAtEnd"/>
    /// (H) puts the wool on an end of the crossbar instead of its middle. <paramref name="woolExtend"/>
    /// (H / donut) holds the wool a short I out from the shape rather than tucked against it.
    /// <paramref name="attachmentWidth"/> is the hub-side attachment/entry width in cells (0 = one corridor
    /// width; the width grammar's w2/w4/w6 = <c>cw</c>/<c>2·cw</c>/<c>3·cw</c>). It widens the donut's hub stub(s)
    /// or the scythe's entry tail (the nub grows away from the bay, so the mouth presents a wide interface without
    /// closing the loop — the three bends and the open bay are preserved, so it stays a scythe).</summary>
    public static EmittedApproach Emit(ApproachFamily family, WoolBox box, int corridorWidth, bool flip = false, RoomPlacement roomPlacement = RoomPlacement.Inline, int attachments = 1, bool woolAtEnd = false, bool woolExtend = false, int attachmentWidth = 0, string idPrefix = "wa")
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
                // straight lane; the room is a stub off the SIDE of the lane at the terminal — the catalog's
                // side-tuck (tttt/vvvw): the lane runs straight to its end and the wool ducks off to one side.
                // The room is BESIDE the lane (shares a vertical corridor-width edge), never a wide cap extending
                // the lane's end. It reads I: the lane is straight and the room is excluded from the bend count.
                Need(box.W >= cw + RoomDepthCells && box.H >= 2 * cw, family, box);
                t.Add([0, 0, cw, box.H]);                    // straight vertical lane, full depth (left)
                room = [cw, box.H - cw, RoomDepthCells, cw]; // room off the lane's right side, at the terminal
                at = [RoomDepthCells / 2.0, cw / 2.0];
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
                // top arm on the left, a full-width band, bottom arm on the right ending in the room. The entry
                // (top arm) is an attachment: it may widen (attachmentWidth) as a nub sticking LEFT into free
                // space, necking down to the cw top arm before the band — so the wide mouth never touches the
                // band-side (a wide arm ON the band would read as a T / branch) and never encloses a pocket. Two
                // bends stay, no bay forms, so it stays a Z (never a scythe).
                int aw = attachmentWidth > 0 ? attachmentWidth : cw; // entry (nub) width; nub extends left of the arm
                Need(box.W >= aw + cw && box.H >= 3 * cw + RoomDepthCells, family, box);
                int z1 = (box.H - RoomDepthCells - cw) / 2;          // top vertical extent (nub + arm)
                int botZ = z1 + cw, botLen = box.H - RoomDepthCells - botZ;
                int ax = aw - cw;                                    // top-arm x (nub sticks out to its left)
                if (aw > cw) t.Add([0, 0, aw, cw]);                  // wide entry nub (top bar, sticks left)
                t.Add([ax, aw > cw ? cw : 0, cw, aw > cw ? z1 - cw : z1]);  // top arm / neck down to the band
                t.Add([ax, z1, box.W - ax, cw]);                     // crossing band (top arm → bottom arm)
                t.Add([box.W - cw, botZ, cw, botLen]);               // bottom arm (right)
                room = [box.W - cw, box.H - RoomDepthCells, cw, RoomDepthCells];
                break;
            }
            case ApproachFamily.Scythe:
            {
                // the S-hook (ttvw/vtvt/vttt): enter at the top-left tail, drop the spine, run the bottom,
                // climb the return leg to the wool at top-right — three bends with a tight bay (the open void
                // notch between the spine and the return leg; not a symmetric U). The entry tail is an
                // attachment: it may widen (attachmentWidth) to the LEFT, away from the bay — the spine drops
                // from the nub's right end and everything downstream shifts right, so the wide mouth never roofs
                // the bay (which would close it into a donut). All three bends stay, so it stays a scythe.
                int aw = attachmentWidth > 0 ? attachmentWidth : cw;  // entry-tail (nub) width
                Need(box.W >= aw + 3 * cw && box.H >= 2 * cw + RoomDepthCells, family, box);
                int botZ = box.H - cw;
                t.Add([0, 0, aw, cw]);                               // entry tail / nub — the mouth (aw wide)
                t.Add([aw, 0, cw, botZ]);                            // spine (down from the nub's right end)
                t.Add([aw, botZ, 3 * cw, cw]);                       // bottom bar (spine → return leg)
                t.Add([aw + 2 * cw, RoomDepthCells, cw, botZ - RoomDepthCells]);  // return leg (up), one bay over
                room = [aw + 2 * cw, 0, cw, RoomDepthCells];         // wool caps the return leg (top-right)
                break;
            }
            case ApproachFamily.H:
            {
                // the branch (vwv/ttt/tvt): two legs run down to the hub and merge at a crossbar; the wool
                // sits on the crossbar — its middle, or an end (woolAtEnd) — optionally held a short I above it
                // (woolExtend). TWO attachment feet + the wool (multi-access), not a 4-armed +.
                Need(box.W >= 3 * cw && box.H >= 2 * cw + RoomDepthCells + (woolExtend ? RoomDepthCells : 0), family, box);
                int barZ = RoomDepthCells + (woolExtend ? RoomDepthCells : 0);   // room above the bar for the wool (+ short I)
                int wx = woolAtEnd ? 0 : (box.W - cw) / 2;
                t.Add([0, barZ, box.W, cw]);                         // crossbar (full width)
                t.Add([0, barZ + cw, cw, box.H - barZ - cw]);        // left leg (down to the hub)
                t.Add([box.W - cw, barZ + cw, cw, box.H - barZ - cw]);  // right leg (down to the hub)
                if (woolExtend) t.Add([wx, RoomDepthCells, cw, RoomDepthCells]);  // short I from the crossbar up to the wool
                room = [wx, 0, cw, RoomDepthCells];                  // wool on top (middle or an end)
                break;
            }
            case ApproachFamily.U:
            {
                // a terrain cup — two parallel bars with the wool integrated as the closing wall between them,
                // approached from the open side (tt/vw/tt). The wool sits with terrain on two opposite sides.
                Need(box.W >= 2 * cw && box.H >= 2 * cw + 1, family, box);
                int barLen = 2 * cw;
                t.Add([0, 0, barLen, cw]);                           // top bar
                t.Add([0, box.H - cw, barLen, cw]);                  // bottom bar
                room = [barLen - cw, cw, cw, box.H - 2 * cw];        // wool = the closing wall (connects the bars)
                break;
            }
            case ApproachFamily.Donut:
            {
                // a rectangular ring around an enclosed hole (multi-access), built from NON-overlapping rects.
                // Hub-side attachment stub(s) extend the bars leftward (single = top only, double = top+bottom).
                // The wool sits off the ring's bottom-right — a stub (optionally a short I out), or integrated
                // AT the bottom-right corner (woolAtEnd), replacing that corner cell (tttt/vtvt/vttw).
                int extend = woolExtend ? cw : 0, aw = attachmentWidth > 0 ? attachmentWidth : cw;  // attachment interface width
                Need(box.W >= 4 * cw + extend + RoomDepthCells && box.H >= 2 * cw + 1 && box.H >= 2 * aw + 1, family, box);
                int ax = cw, ringH = box.H, span = 3 * cw;           // ring x in [ax, ax+3cw); hub stubs sit in [0, cw)
                t.Add([ax, 0, span, cw]);                            // top bar
                t.Add([ax, cw, cw, ringH - 2 * cw]);                 // left leg (middle only — no corner overlap)
                t.Add([ax + 2 * cw, cw, cw, ringH - 2 * cw]);        // right leg (middle only)
                t.Add([0, 0, cw, aw]);                               // hub attachment (top-left), aw cells wide
                if (attachments >= 2) t.Add([0, ringH - aw, cw, aw]);// second attachment (bottom-left)
                if (woolAtEnd)
                {
                    t.Add([ax, ringH - cw, 2 * cw, cw]);            // bottom bar stops before the corner
                    room = [ax + 2 * cw, ringH - cw, cw, cw];       // wool AT the bottom-right corner (integrated)
                }
                else
                {
                    t.Add([ax, ringH - cw, span, cw]);              // full bottom bar
                    int wxr = ax + span;                            // right of the ring's right leg
                    if (woolExtend) { t.Add([wxr, ringH - cw, cw, cw]); wxr += cw; }  // short I holding the wool
                    room = [wxr, ringH - cw, RoomDepthCells, cw];   // wool off the bottom-right
                }
                break;
            }
            case ApproachFamily.Plug:
            {
                // a solid body — the wool caps it on a tab (the plaza pole).
                Need(box.W >= cw + 1 && box.H >= cw + 1 + RoomDepthCells, family, box);
                t.Add([0, 0, box.W, box.H - RoomDepthCells]);        // solid body
                room = [(box.W - cw) / 2, box.H - RoomDepthCells, cw, RoomDepthCells];  // tab
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
