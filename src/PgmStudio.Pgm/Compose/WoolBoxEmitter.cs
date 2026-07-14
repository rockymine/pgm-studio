using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The wool-approach <b>slot roles</b> — the shape-internal taxonomy every emitted piece carries
/// (<see cref="GrownPiece.Slot"/>), naming its position in the family template so the composition rules read
/// as properties of a slot rather than raw geometry. <see cref="Entry"/> is the universal hub-attach (a lane's
/// mouth, either leg of a U/H, either bar of a clamp, the donut's hub stub) — the target of entry shift and
/// entry width; <see cref="Room"/> is the wool room — the target of the extend-vs-side-dock rule;
/// <see cref="Run"/>/<see cref="Bar"/> are corridor/crossing segments, qualified <see cref="EntryRun"/>/
/// <see cref="RoomRun"/> and <see cref="EntryBar"/>/<see cref="RoomBar"/> where a family has two; <see cref="Leg"/>
/// is a donut ring arm. A role is a <b>template slot, not a property of the rectangle</b> — a scythe's
/// <c>entry-run</c> and a donut's <c>leg</c> can be the same rectangle in different slots. See the piece
/// vocabulary in <c>docs/contracts/map-generation.md</c> §5.</summary>
public static class ApproachSlots
{
    public const string Entry = "entry";
    public const string Run = "run";
    public const string Bar = "bar";
    public const string Leg = "leg";
    public const string Room = "room";
    public const string EntryRun = "entry-run";
    public const string RoomRun = "room-run";
    public const string EntryBar = "entry-bar";
    public const string RoomBar = "room-bar";

    /// <summary>The canonical ordered slot template of <paramref name="family"/> — terrain slots in emit order,
    /// the <see cref="Room"/> last — the §5.3 piece-vocabulary table as data. This is the base configuration
    /// (single donut attachment, no wool-extend, inline room); the optional donut knobs add pieces
    /// (a second attachment is another <see cref="Entry"/>, a wool-extend a <see cref="Run"/>).</summary>
    public static IReadOnlyList<string> Template(ShapeFamily family) => family switch
    {
        ShapeFamily.I     => [Entry, Room],
        ShapeFamily.L     => [Entry, Run, Room],
        ShapeFamily.Z     => [Entry, Bar, RoomRun, Room],
        ShapeFamily.Scythe => [Entry, EntryRun, Bar, RoomRun, Room],
        ShapeFamily.Clamp => [Entry, Entry, Room],
        ShapeFamily.U     => [Bar, Entry, Entry, Room],
        ShapeFamily.H     => [Bar, Entry, Entry, RoomRun, Room],
        ShapeFamily.Donut => [EntryBar, Leg, Leg, Entry, RoomBar, Room],
        _ => throw new ComposeException($"no slot template for family {family}."),
    };
}

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
/// read. Given the box, a target <see cref="ShapeFamily"/> and a corridor width, it emits rectilinear
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
    /// (U / H) puts the wool on an end of the crossbar instead of its middle. <paramref name="woolExtend"/>
    /// (donut) holds the wool a short I out from the shape rather than tucked against it.
    /// <paramref name="attachmentWidth"/> (donut) is the hub-interface width of each attachment in cells (0 =
    /// one corridor width; the width grammar's w2/w4/w6 = <c>cw</c>/<c>2·cw</c>/<c>3·cw</c>).</summary>
    public static EmittedApproach Emit(ShapeFamily family, WoolBox box, int corridorWidth, bool flip = false, RoomPlacement roomPlacement = RoomPlacement.Inline, int attachments = 1, bool woolAtEnd = false, bool woolExtend = false, int attachmentWidth = 0, string idPrefix = "wa")
    {
        var cw = corridorWidth;
        if (family == ShapeFamily.Isolated) throw new ComposeException("the emitter fills terminal-capped families; Isolated is a derive-only reading.");
        if (cw < 2) throw new ComposeException($"corridor width {cw} < 2 (a lane is at least one 10-block cell pair).");
        if (cw > box.W) throw new ComposeException($"corridor width {cw} exceeds box width {box.W}.");
        if (roomPlacement == RoomPlacement.SideTuck && family != ShapeFamily.I)
            throw new ComposeException($"side-tuck room is only supported for the I family in this pass (requested {family}).");

        var t = new List<(int[] Rect, string Slot)>();
        int[] room;
        double[]? at = null;                                 // wool marker offset within the room (defaults to centre)
        switch (family)
        {
            case ShapeFamily.I when roomPlacement == RoomPlacement.SideTuck:
            {
                // straight lane; the room is a stub off the SIDE of the lane at the terminal — the catalog's
                // side-tuck (tttt/vvvw): the lane runs straight to its end and the wool ducks off to one side.
                // The room is BESIDE the lane (shares a vertical corridor-width edge), never a wide cap extending
                // the lane's end. It reads I: the lane is straight and the room is excluded from the bend count.
                Need(box.W >= cw + RoomDepthCells && box.H >= 2 * cw, family, box);
                t.Add(([0, 0, cw, box.H], ApproachSlots.Entry));   // straight vertical lane, full depth (left)
                room = [cw, box.H - cw, RoomDepthCells, cw]; // room off the lane's right side, at the terminal
                at = [RoomDepthCells / 2.0, cw / 2.0];
                break;
            }
            case ShapeFamily.I:
            {
                Need(box.H >= RoomDepthCells + 1, family, box);
                int lx = (box.W - cw) / 2, laneH = box.H - RoomDepthCells;
                t.Add(([lx, 0, cw, laneH], ApproachSlots.Entry));
                room = [lx, laneH, cw, RoomDepthCells];
                break;
            }
            case ShapeFamily.L:
            {
                // vertical arm down one side, a horizontal band across the bottom, room at its far end. The
                // +1 guarantees ≥1 cell of horizontal arm beyond the vertical, so the bend is real (without it
                // the band sits in the vertical's own column and the shape collapses to a straight I).
                Need(box.W >= cw + RoomDepthCells + 1 && box.H >= 2 * cw, family, box);
                int vLx = 0, bandZ = box.H - cw, roomLen = RoomDepthCells;
                t.Add(([vLx, 0, cw, bandZ], ApproachSlots.Entry));           // vertical arm (enters at the mouth)
                t.Add(([0, bandZ, box.W - roomLen, cw], ApproachSlots.Run)); // horizontal band up to the room
                room = [box.W - roomLen, bandZ, roomLen, cw];        // dead-end at the far side of the band
                break;
            }
            case ShapeFamily.Z:
            {
                // top arm on the left, a full-width band, bottom arm on the right ending in the room.
                Need(box.W >= 2 * cw && box.H >= 3 * cw + RoomDepthCells, family, box);
                int z1 = (box.H - RoomDepthCells - cw) / 2;          // top-arm length (balanced with the bottom arm)
                int botZ = z1 + cw, botLen = box.H - RoomDepthCells - botZ;
                t.Add(([0, 0, cw, z1], ApproachSlots.Entry));            // top arm (left) — the mouth
                t.Add(([0, z1, box.W, cw], ApproachSlots.Bar));          // crossing band
                t.Add(([box.W - cw, botZ, cw, botLen], ApproachSlots.RoomRun)); // bottom arm (right) up to the room
                room = [box.W - cw, box.H - RoomDepthCells, cw, RoomDepthCells];
                break;
            }
            case ShapeFamily.Scythe:
            {
                // the S-hook (ttvw/vtvt/vttt): enter at the top-left tail, drop the spine, run the bottom,
                // climb the return leg to the wool at top-right — three bends with a tight bay between the
                // spine and the return leg (not a symmetric U).
                Need(box.W >= 4 * cw && box.H >= 2 * cw + RoomDepthCells, family, box);
                int botZ = box.H - cw;
                t.Add(([0, 0, cw, cw], ApproachSlots.Entry));            // top-left tail — the mouth
                t.Add(([cw, 0, cw, botZ], ApproachSlots.EntryRun));      // spine (down from the tail)
                t.Add(([cw, botZ, 3 * cw, cw], ApproachSlots.Bar));      // bottom bar (spine → return leg)
                t.Add(([3 * cw, RoomDepthCells, cw, botZ - RoomDepthCells], ApproachSlots.RoomRun)); // return leg (up), one bay over
                room = [3 * cw, 0, cw, RoomDepthCells];              // wool caps the return leg (top-right)
                break;
            }
            case ShapeFamily.H:
            {
                // the branch with a room-run STUB: two legs run down to the hub and merge at a crossbar; the wool
                // caps a short stub rising from the crossbar's opposite side — its middle, or an end (woolAtEnd).
                // TWO attachment feet + the wool on its own stub (multi-access), not a 4-armed +.
                Need(box.W >= 3 * cw && box.H >= 2 * cw + 2 * RoomDepthCells, family, box);
                int barZ = 2 * RoomDepthCells;                       // wool + stub above the bar
                int wx = woolAtEnd ? 0 : (box.W - cw) / 2;
                t.Add(([0, barZ, box.W, cw], ApproachSlots.Bar));                        // crossbar (full width)
                t.Add(([0, barZ + cw, cw, box.H - barZ - cw], ApproachSlots.Entry));     // left leg (down to the hub)
                t.Add(([box.W - cw, barZ + cw, cw, box.H - barZ - cw], ApproachSlots.Entry)); // right leg (down to the hub)
                t.Add(([wx, RoomDepthCells, cw, RoomDepthCells], ApproachSlots.RoomRun)); // room-run stub from the crossbar up to the wool
                room = [wx, 0, cw, RoomDepthCells];                  // wool caps the stub (middle or an end)
                break;
            }
            case ShapeFamily.U:
            {
                // the branch with the wool FLUSH on the crossbar (no stub): two legs down to the hub, the
                // crossbar, and the wool docked directly on the crossbar's opposite side — middle, or an end
                // (woolAtEnd). The crossbar reaches past the wool toward the legs, so the wool sits on a bar
                // wider than itself — what separates U from H.
                Need(box.W >= 3 * cw && box.H >= 2 * cw + RoomDepthCells, family, box);
                int barZ = RoomDepthCells;                           // wool sits directly above the bar
                int wx = woolAtEnd ? 0 : (box.W - cw) / 2;
                t.Add(([0, barZ, box.W, cw], ApproachSlots.Bar));                        // crossbar (full width)
                t.Add(([0, barZ + cw, cw, box.H - barZ - cw], ApproachSlots.Entry));     // left leg (down to the hub)
                t.Add(([box.W - cw, barZ + cw, cw, box.H - barZ - cw], ApproachSlots.Entry)); // right leg (down to the hub)
                room = [wx, 0, cw, RoomDepthCells];                  // wool flush on the crossbar
                break;
            }
            case ShapeFamily.Clamp:
            {
                // the wool clamped between two parallel bars, approached from the open side (tt/vw/tt) — the wool
                // is the closing wall connecting them (terrain on two opposite sides, and it bridges them).
                Need(box.W >= 2 * cw && box.H >= 2 * cw + 1, family, box);
                int barLen = 2 * cw;
                t.Add(([0, 0, barLen, cw], ApproachSlots.Entry));           // top bar
                t.Add(([0, box.H - cw, barLen, cw], ApproachSlots.Entry));  // bottom bar
                room = [barLen - cw, cw, cw, box.H - 2 * cw];        // wool = the closing wall (connects the bars)
                break;
            }
            case ShapeFamily.Donut:
            {
                // a rectangular ring around an enclosed hole (multi-access), built from NON-overlapping rects.
                // Hub-side attachment stub(s) extend the bars leftward (single = top only, double = top+bottom).
                // The wool sits off the ring's bottom-right — a stub (optionally a short I out), or integrated
                // AT the bottom-right corner (woolAtEnd), replacing that corner cell (tttt/vtvt/vttw).
                int extend = woolExtend ? cw : 0, aw = attachmentWidth > 0 ? attachmentWidth : cw;  // attachment interface width
                // a single attachment only has to clear the bottom bar (aw + cw); two need to stack without
                // overlapping (2·aw + 1). Don't force the two-stub height on a one-stub ring.
                int needH = Math.Max(2 * cw + 1, attachments >= 2 ? 2 * aw + 1 : aw + cw);
                Need(box.W >= 4 * cw + extend + RoomDepthCells && box.H >= needH, family, box);
                int ax = cw, ringH = box.H, span = 3 * cw;           // ring x in [ax, ax+3cw); hub stubs sit in [0, cw)
                t.Add(([ax, 0, span, cw], ApproachSlots.EntryBar));          // top bar
                t.Add(([ax, cw, cw, ringH - 2 * cw], ApproachSlots.Leg));    // left leg (middle only — no corner overlap)
                t.Add(([ax + 2 * cw, cw, cw, ringH - 2 * cw], ApproachSlots.Leg)); // right leg (middle only)
                t.Add(([0, 0, cw, aw], ApproachSlots.Entry));               // hub attachment (top-left), aw cells wide
                if (attachments >= 2) t.Add(([0, ringH - aw, cw, aw], ApproachSlots.Entry)); // second attachment (bottom-left)
                if (woolAtEnd)
                {
                    t.Add(([ax, ringH - cw, 2 * cw, cw], ApproachSlots.RoomBar)); // bottom bar stops before the corner
                    room = [ax + 2 * cw, ringH - cw, cw, cw];       // wool AT the bottom-right corner (integrated)
                }
                else
                {
                    t.Add(([ax, ringH - cw, span, cw], ApproachSlots.RoomBar));  // full bottom bar
                    int wxr = ax + span;                            // right of the ring's right leg
                    if (woolExtend) { t.Add(([wxr, ringH - cw, cw, cw], ApproachSlots.Run)); wxr += cw; }  // short I holding the wool
                    room = [wxr, ringH - cw, RoomDepthCells, cw];   // wool off the bottom-right
                }
                break;
            }
            default: throw new ComposeException($"unsupported family {family}.");
        }

        at ??= [room[2] / 2.0, room[3] / 2.0];
        if (flip)
        {
            foreach (var (rect, _) in t) rect[0] = box.W - rect[0] - rect[2];   // slot survives the mirror
            room[0] = box.W - room[0] - room[2];
            at = [room[2] - at[0], at[1]];                   // mirror the marker within the flipped room
        }

        // translate box-local -> absolute and wrap as pieces, each carrying its template slot role
        var terrain = new List<GrownPiece>(t.Count);
        for (var i = 0; i < t.Count; i++)
            terrain.Add(new GrownPiece($"{idPrefix}-t{i + 1}", [box.X + t[i].Rect[0], box.Z + t[i].Rect[1], t[i].Rect[2], t[i].Rect[3]], PlanRoles.Piece, t[i].Slot));
        var woolRoom = new GrownPiece($"{idPrefix}-wool", [box.X + room[0], box.Z + room[1], room[2], room[3]], PlanRoles.WoolRoom, ApproachSlots.Room);
        return new EmittedApproach(terrain, woolRoom, at);
    }

    private static void Need(bool ok, ShapeFamily family, WoolBox box)
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
