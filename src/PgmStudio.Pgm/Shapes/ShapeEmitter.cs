using PgmStudio.Geom;

namespace PgmStudio.Pgm.Shapes;

/// <summary>One edge of an axis-aligned box, in box-local coordinates (Top = z min, Left = x min).</summary>
public enum BoxEdge { Top, Bottom, Left, Right }

/// <summary>How a <see cref="BoxTurn"/> moves an edge, read off the edge's outward direction.</summary>
public static class BoxEdges
{
    /// <summary>The edge <paramref name="edge"/> becomes under <paramref name="turn"/>.</summary>
    public static BoxEdge Turned(this BoxEdge edge, BoxTurn turn)
    {
        var (dx, dz) = turn.Direction(edge.Outward().Dx, edge.Outward().Dz);
        return (dx, dz) switch
        {
            (0, < 0) => BoxEdge.Top,
            (0, > 0) => BoxEdge.Bottom,
            (< 0, 0) => BoxEdge.Left,
            _ => BoxEdge.Right,
        };
    }

    /// <summary>The turn that lays a shape's top edge onto <paramref name="edge"/>: the identity, a flip, or a
    /// quarter turn.</summary>
    public static BoxTurn TopOnto(this BoxEdge edge) => edge switch
    {
        BoxEdge.Top => BoxTurn.None,
        BoxEdge.Bottom => BoxTurn.FlipZ,
        BoxEdge.Right => BoxTurn.Clockwise,
        BoxEdge.Left => BoxTurn.Anticlockwise,
        _ => throw new ArgumentException($"unknown box edge {edge}."),
    };

    private static (int Dx, int Dz) Outward(this BoxEdge edge) => edge switch
    {
        BoxEdge.Top => (0, -1),
        BoxEdge.Bottom => (0, 1),
        BoxEdge.Left => (-1, 0),
        _ => (1, 0),
    };
}

/// <summary>Negative space a shape leaves inside its box, published at emit time — exact by construction,
/// no derive pass finds it. <see cref="Kind"/>: <c>bay</c> (open toward one edge — claimable by a later
/// box), <c>notch</c> (a corner remainder, open on two edges), <c>hole</c> (enclosed by the shape).
/// <see cref="Mouth"/> is the open edge of a bay (null for notches and holes); <see cref="Walls"/> are the
/// template parts of the pieces bounding it.</summary>
public sealed record ShapeVacancy(string Kind, CellRect Rect, BoxEdge? Mouth, IReadOnlyList<string> Walls);

/// <summary>
/// The one shape emitter — fills a W×H box with a base family at a lane width, in the canonical box-local
/// frame, as a <see cref="Compound"/> that ends in a room: part-named pieces (<see cref="CompoundParts"/>), the
/// room piece last, its marker and the vacancies the pieces leave. Pure cell geometry: no plan types, roles or
/// ids — the wool and spawn boxes stamp those. <see cref="OrientMouthTop"/> / <see cref="MouthEdge"/> place a
/// family's mouth on the edge a caller docks.
/// </summary>
public static class ShapeEmitter
{
    /// <summary>The dead-end room's depth along the final corridor, in cells — a two-cell (~10-block)
    /// plateau that clears the export stamp.</summary>
    public const int RoomDepthCells = 2;

    /// <summary>The room-capped forms the emitter builds; the rest of <see cref="CompoundForm"/> is the
    /// room-free hub and front-line forms <see cref="CompoundEmitter"/> builds.</summary>
    public static readonly IReadOnlyList<CompoundForm> Families =
    [
        CompoundForm.I, CompoundForm.L, CompoundForm.Z, CompoundForm.Scythe,
        CompoundForm.Clamp, CompoundForm.U, CompoundForm.H, CompoundForm.Donut,
    ];

    /// <summary>The canonical-frame mouth edge of <paramref name="family"/>: where its entry part(s) dock the
    /// host. A flip mirrors the shape along this edge, so it never moves the mouth.</summary>
    public static BoxEdge MouthEdge(CompoundForm family) => family switch
    {
        CompoundForm.U or CompoundForm.H or CompoundForm.Clamp => BoxEdge.Bottom,
        CompoundForm.Donut => BoxEdge.Left,
        _ => BoxEdge.Top,
    };

    /// <summary>The minimal box (W, H) <paramref name="family"/> needs at <paramref name="laneWidth"/> with the
    /// given options — the emit-side counterpart of every <c>Emit</c> size guard, so a caller can size a box
    /// (or signal a directed too-small) without exception control flow.</summary>
    public static (int W, int H) MinBox(
        CompoundForm family, int laneWidth, RoomPlacement roomPlacement = RoomPlacement.Inline,
        int attachments = 1, bool woolExtend = false, int attachmentWidth = 0, bool woolAtEnd = false)
    {
        var rd = RoomDepthCells;
        var aw = attachmentWidth > 0 ? attachmentWidth : laneWidth;
        return family switch
        {
            CompoundForm.I when roomPlacement == RoomPlacement.SideTuck => (laneWidth + rd, 2 * laneWidth),
            CompoundForm.I => (laneWidth, rd + 1),
            CompoundForm.L => (laneWidth + rd + 1, 2 * laneWidth),
            CompoundForm.Z when roomPlacement == RoomPlacement.SideTuck => (2 * laneWidth + rd, 3 * laneWidth + 1),
            CompoundForm.Z => (2 * laneWidth, 3 * laneWidth + rd),
            CompoundForm.Scythe when roomPlacement == RoomPlacement.SideTuck => (4 * laneWidth + rd, 2 * laneWidth + rd),
            CompoundForm.Scythe => (4 * laneWidth, 2 * laneWidth + rd),
            // the clamp's legs only have to reach the mouth past the wool — it has no crossbar to clear, so it
            // does NOT inherit the U's 2·laneWidth leg run (that depth became a 4-cell void under every clamp)
            CompoundForm.Clamp => (3 * laneWidth, laneWidth + rd),
            CompoundForm.U => (3 * laneWidth, 2 * laneWidth + rd),
            CompoundForm.H => (3 * laneWidth, 2 * laneWidth + 2 * rd),
            // the trailing `rd` is the room hanging off the ring's bottom-right; the corner-integrated wool
            // (woolAtEnd) sits INSIDE the ring's span instead, so it needs none of it
            CompoundForm.Donut => (4 * laneWidth + (woolExtend ? laneWidth : 0) + (woolAtEnd ? 0 : rd),
                Math.Max(2 * laneWidth + 1, attachments >= 2 ? 2 * aw + 1 : aw + laneWidth)),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "the emitter fills room-capped families"),
        };
    }

    /// <summary>Emit <paramref name="family"/> into a W×H box at <paramref name="laneWidth"/> (cells) as a
    /// compound ending in a room, with the room's marker. <paramref name="flip"/> mirrors the shape
    /// along its mouth (the turn goes left instead of right) so both handednesses are reachable. <paramref name="attachments"/> (donut) is the number of hub-side stubs, 1 or 2.
    /// <paramref name="woolAtEnd"/> (U / H / donut) puts the room on an end of the crossbar / integrates it
    /// at the ring corner. <paramref name="woolExtend"/> (donut) holds the room a short I out from the
    /// shape. <paramref name="attachmentWidth"/> (donut, scythe) is the hub-interface width of the attachment in
    /// cells, measured ALONG the edge it docks (0 = one lane width; the width grammar's w2/w4/w6 =
    /// <c>laneWidth</c>/<c>2·laneWidth</c>/<c>3·laneWidth</c>). <paramref name="entryShift"/> / <paramref name="woolShift"/> (scythe)
    /// slide the two independently-offsettable endpoints down the docking edge: the entry shift propagates
    /// inward — the spine it docks shrinks from the top with it — and the wool shift shortens the return leg the
    /// same way, so only the shifted endpoint still reaches the edge. <paramref name="attachmentOffset"/>
    /// (donut) slides the hub attachment down the ring's edge — only the attachment moves, the ring is
    /// unchanged. <paramref name="ringWalls"/> (donut) gives the ring's four wall widths independently, so one
    /// leg or bar can be wider than the rest; <c>null</c> builds every wall at <paramref name="laneWidth"/>.
    /// Widening consumes the box's slack — the hole is what the walls leave — so a vector too fat for the box
    /// is refused rather than squashing the ring.</summary>
    public static Compound Emit(
        CompoundForm family, int boxW, int boxH, int laneWidth, bool flip = false,
        RoomPlacement roomPlacement = RoomPlacement.Inline, int attachments = 1, bool woolAtEnd = false,
        bool woolExtend = false, int attachmentWidth = 0, int entryShift = 0, int woolShift = 0,
        int attachmentOffset = 0, RingWalls? ringWalls = null)
    =>
        Compose(family, boxW, boxH, laneWidth, flip, roomPlacement, attachments, woolAtEnd, woolExtend,
            attachmentWidth, entryShift, woolShift, attachmentOffset, ringWalls);

    /// <summary>Build the family geometry into the canonical box, apply <paramref name="flip"/>, and return the
    /// compound: the pieces, the room last, its marker and the vacancies. See <see cref="Emit"/> for the knobs.
    /// Every segment abuts its neighbour along a full corridor-width edge; the shape stays inside the box; a box
    /// too small for the family's turns throws.
    ///
    /// <para><b>The mouth</b> — the box edge the entry docks a host through — is family-specific in the
    /// canonical frame (<see cref="MouthEdge"/>): I/L/Z/scythe enter at the top, U/H at the bottom (their legs
    /// run down to the host), clamp/donut at the left (bars/stub open leftward). Callers reorient with
    /// <see cref="OrientMouthTop"/> to put any family's mouth on the edge they dock.</para></summary>
    private static Compound Compose(
        CompoundForm family, int boxW, int boxH, int laneWidth, bool flip,
        RoomPlacement roomPlacement, int attachments, bool woolAtEnd,
        bool woolExtend, int attachmentWidth, int entryShift, int woolShift,
        int attachmentOffset, RingWalls? ringWalls)
    {
        var (W, H) = (boxW, boxH);
        if (!Families.Contains(family))
            throw new ArgumentException($"the emitter fills room-capped forms; {family} is a room-free form.");
        if (laneWidth < 2) throw new ArgumentException($"lane width {laneWidth} < 2 (a lane is at least one 10-block cell pair).");
        if (laneWidth > W) throw new ArgumentException($"lane width {laneWidth} exceeds box width {W}.");
        if (roomPlacement == RoomPlacement.SideTuck && family is not (CompoundForm.I or CompoundForm.Z or CompoundForm.Scythe))
            throw new ArgumentException($"side-tuck room is supported for I, Z and scythe (requested {family}).");
        if ((entryShift != 0 || woolShift != 0) && family != CompoundForm.Scythe)
            throw new ArgumentException($"entry/wool shifts are scythe endpoint knobs (requested {family}).");
        if (attachmentOffset != 0 && family != CompoundForm.Donut)
            throw new ArgumentException($"the attachment offset is a donut knob (requested {family}).");

        var t = new List<Piece>();
        var vac = new List<ShapeVacancy>();
        CellRect room;
        double[]? at = null;                                 // marker offset within the room (defaults to centre)
        var rd = RoomDepthCells;
        switch (family)
        {
            case CompoundForm.I when roomPlacement == RoomPlacement.SideTuck:
            {
                // straight lane; the room is a stub off the SIDE of the lane at the room — the catalog's
                // side-tuck (tttt/vvvw): the lane runs straight to its end and the wool ducks off to one side.
                // The room is BESIDE the lane (shares a vertical corridor-width edge), never a wide cap extending
                // the lane's end. It reads I: the lane is straight and the room is excluded from the bend count.
                Need(W >= laneWidth + rd && H >= 2 * laneWidth, family, W, H);
                t.Add(new(new(0, 0, laneWidth, H), CompoundParts.Entry));   // straight vertical lane, full depth (left)
                room = new(laneWidth, H - laneWidth, rd, laneWidth);                   // room off the lane's right side, at the room
                at = [rd / 2.0, laneWidth / 2.0];
                if (H - laneWidth > 0)
                    vac.Add(new ShapeVacancy("notch", new(laneWidth, 0, W - laneWidth, H - laneWidth), null, [CompoundParts.Entry]));
                break;
            }
            case CompoundForm.I:
            {
                Need(H >= rd + 1, family, W, H);
                int lx = (W - laneWidth) / 2, laneH = H - rd;
                t.Add(new(new(lx, 0, laneWidth, laneH), CompoundParts.Entry));
                room = new(lx, laneH, laneWidth, rd);
                if (lx > 0) vac.Add(new ShapeVacancy("notch", new(0, 0, lx, H), null, [CompoundParts.Entry]));
                if (W - lx - laneWidth > 0)
                    vac.Add(new ShapeVacancy("notch", new(lx + laneWidth, 0, W - lx - laneWidth, H), null, [CompoundParts.Entry]));
                break;
            }
            case CompoundForm.L:
            {
                // vertical arm down one side, a horizontal band across the bottom, room at its far end. The
                // +1 guarantees ≥1 cell of horizontal arm beyond the vertical, so the bend is real (without it
                // the band sits in the vertical's own column and the shape collapses to a straight I).
                Need(W >= laneWidth + rd + 1 && H >= 2 * laneWidth, family, W, H);
                int vLx = 0, bandZ = H - laneWidth;
                t.Add(new(new(vLx, 0, laneWidth, bandZ), CompoundParts.Entry));       // vertical arm (enters at the mouth)
                t.Add(new(new(0, bandZ, W - rd, laneWidth), CompoundParts.Run));      // horizontal band up to the room
                room = new(W - rd, bandZ, rd, laneWidth);                          // dead-end at the far side of the band
                vac.Add(new ShapeVacancy("notch", new(laneWidth, 0, W - laneWidth, H - laneWidth), null,
                    [CompoundParts.Entry, CompoundParts.Run]));
                break;
            }
            case CompoundForm.Z when roomPlacement == RoomPlacement.SideTuck:
            {
                // the run reaches the box bottom and the room docks its SIDE at the end, perpendicular —
                // the run is shortened in the sense that it no longer extends past the lane to hold the
                // room. Same I-family side-tuck grammar, and still a Z: the room is excluded from the bend
                // read, so the staircase stays a staircase.
                Need(W >= 2 * laneWidth + rd && H >= 3 * laneWidth + 1, family, W, H);
                int z1 = (H - laneWidth) / 2;
                int botZ = z1 + laneWidth;
                t.Add(new(new(0, 0, laneWidth, z1), CompoundParts.Entry));            // top arm (left) — the mouth
                t.Add(new(new(0, z1, W, laneWidth), CompoundParts.Bar));              // crossing band
                t.Add(new(new(W - laneWidth, botZ, laneWidth, H - botZ), CompoundParts.RoomRun)); // bottom arm to the box bottom
                room = new(W - laneWidth - rd, H - laneWidth, rd, laneWidth);                    // room off the run's interior side
                vac.Add(new ShapeVacancy("notch", new(laneWidth, 0, W - laneWidth, z1), null,
                    [CompoundParts.Entry, CompoundParts.Bar]));
                break;
            }
            case CompoundForm.Z:
            {
                // top arm on the left, a full-width band, bottom arm on the right ending in the room.
                Need(W >= 2 * laneWidth && H >= 3 * laneWidth + rd, family, W, H);
                int z1 = (H - rd - laneWidth) / 2;                              // top-arm length (balanced with the bottom arm)
                int botZ = z1 + laneWidth, botLen = H - rd - botZ;
                t.Add(new(new(0, 0, laneWidth, z1), CompoundParts.Entry));            // top arm (left) — the mouth
                t.Add(new(new(0, z1, W, laneWidth), CompoundParts.Bar));              // crossing band
                t.Add(new(new(W - laneWidth, botZ, laneWidth, botLen), CompoundParts.RoomRun)); // bottom arm (right) up to the room
                room = new(W - laneWidth, H - rd, laneWidth, rd);
                vac.Add(new ShapeVacancy("notch", new(laneWidth, 0, W - laneWidth, z1), null,
                    [CompoundParts.Entry, CompoundParts.Bar]));
                vac.Add(new ShapeVacancy("notch", new(0, botZ, W - laneWidth, H - botZ), null,
                    [CompoundParts.Bar, CompoundParts.RoomRun]));
                break;
            }
            case CompoundForm.Scythe:
            {
                // the S-hook (ttvw/vtvt/vttt): enter at the top-left tail, drop the spine, run the bottom,
                // climb the return leg to the wool at top-right — three bends with a tight bay between the
                // spine and the return leg (not a symmetric U). Both endpoints may slide down the docking
                // edge: a shifted tail takes the spine's top with it (the docked piece resizes with the
                // shift — a full-height spine over a dropped tail is a different, wrong shape) and a
                // shifted wool takes the return leg's top the same way.
                int aw = attachmentWidth > 0 ? attachmentWidth : laneWidth;     // tail width ALONG the spine it docks
                Need(W >= 4 * laneWidth && H >= 2 * laneWidth + rd, family, W, H);
                int botZ = H - laneWidth;
                if (entryShift < 0 || entryShift + aw > botZ - 1)
                    throw new ArgumentException(
                        $"entry shift {entryShift} with attachment width {aw} leaves no spine above the bar (box {W}x{H}).");
                if (woolShift < 0 || woolShift + rd > botZ - laneWidth)
                    throw new ArgumentException(
                        $"wool shift {woolShift} leaves no return leg above the bar (box {W}x{H}).");
                t.Add(new(new(0, entryShift, laneWidth, aw), CompoundParts.Entry));   // tail — the mouth, slid down the edge
                t.Add(new(new(laneWidth, entryShift, laneWidth, botZ - entryShift), CompoundParts.EntryRun)); // spine, shrunk with it
                t.Add(new(new(laneWidth, botZ, 3 * laneWidth, laneWidth), CompoundParts.Bar));      // bottom bar (spine → return leg)
                if (roomPlacement == RoomPlacement.SideTuck)
                {
                    // the wool docks the return leg's SIDE at its top end — the leg is shortened to the
                    // room's line instead of running out to hold it, and the tail stays lane-width (a
                    // thickened tail branches, independent of the docking)
                    Need(W >= 4 * laneWidth + rd, family, W, H);
                    if (woolShift + laneWidth > botZ - 1)
                        throw new ArgumentException(
                            $"wool shift {woolShift} leaves no return leg above the bar (box {W}x{H}).");
                    t.Add(new(new(3 * laneWidth, woolShift, laneWidth, botZ - woolShift), CompoundParts.RoomRun));
                    room = new(4 * laneWidth, woolShift, rd, laneWidth);                  // perpendicular, off the outer side
                }
                else
                {
                    t.Add(new(new(3 * laneWidth, woolShift + rd, laneWidth, botZ - woolShift - rd), CompoundParts.RoomRun)); // return leg
                    room = new(3 * laneWidth, woolShift, laneWidth, rd);                  // wool caps the return leg
                }
                vac.Add(new ShapeVacancy("bay", new(2 * laneWidth, 0, laneWidth, botZ), BoxEdge.Top,
                    [CompoundParts.EntryRun, CompoundParts.Bar, CompoundParts.RoomRun]));
                break;
            }
            case CompoundForm.H:
            {
                // the branch with a room-run STUB: two legs run down to the hub and merge at a crossbar; the wool
                // caps a short stub rising from the crossbar's opposite side — its middle, or an end (woolAtEnd).
                // TWO attachment feet + the wool on its own stub (multi-access), not a 4-armed +.
                Need(W >= 3 * laneWidth && H >= 2 * laneWidth + 2 * rd, family, W, H);
                int barZ = 2 * rd;                                       // wool + stub above the bar
                int wx = woolAtEnd ? 0 : (W - laneWidth) / 2;
                t.Add(new(new(0, barZ, W, laneWidth), CompoundParts.Bar));                        // crossbar (full width)
                t.Add(new(new(0, barZ + laneWidth, laneWidth, H - barZ - laneWidth), CompoundParts.Entry));     // left leg (down to the hub)
                t.Add(new(new(W - laneWidth, barZ + laneWidth, laneWidth, H - barZ - laneWidth), CompoundParts.Entry)); // right leg (down to the hub)
                t.Add(new(new(wx, rd, laneWidth, rd), CompoundParts.RoomRun));        // room-run stub from the crossbar up to the wool
                room = new(wx, 0, laneWidth, rd);                                  // wool caps the stub (middle or an end)
                vac.Add(new ShapeVacancy("bay", new(laneWidth, barZ + laneWidth, W - 2 * laneWidth, H - barZ - laneWidth), BoxEdge.Bottom,
                    [CompoundParts.Entry, CompoundParts.Bar, CompoundParts.Entry]));
                break;
            }
            case CompoundForm.U:
            {
                // the branch with the wool FLUSH on the crossbar (no stub): two legs down to the hub, the
                // crossbar, and the wool docked directly on the crossbar's opposite side — middle, or an end
                // (woolAtEnd). The crossbar reaches past the wool toward the legs, so the wool sits on a bar
                // wider than itself — what separates U from H.
                Need(W >= 3 * laneWidth && H >= 2 * laneWidth + rd, family, W, H);
                int barZ = rd;                                           // wool sits directly above the bar
                int wx = woolAtEnd ? 0 : (W - laneWidth) / 2;
                t.Add(new(new(0, barZ, W, laneWidth), CompoundParts.Bar));                        // crossbar (full width)
                t.Add(new(new(0, barZ + laneWidth, laneWidth, H - barZ - laneWidth), CompoundParts.Entry));     // left leg (down to the hub)
                t.Add(new(new(W - laneWidth, barZ + laneWidth, laneWidth, H - barZ - laneWidth), CompoundParts.Entry)); // right leg (down to the hub)
                room = new(wx, 0, laneWidth, rd);                                  // wool flush on the crossbar
                vac.Add(new ShapeVacancy("bay", new(laneWidth, barZ + laneWidth, W - 2 * laneWidth, H - barZ - laneWidth), BoxEdge.Bottom,
                    [CompoundParts.Entry, CompoundParts.Bar, CompoundParts.Entry]));
                break;
            }
            case CompoundForm.Clamp:
            {
                // the wool clamped INSIDE the shape as a cut cell: two legs run down to the hub (one U-style
                // mouth, MouthEdge Bottom) and the wool is their ONLY bridge — remove it and the terrain falls
                // into two pieces. woolAtEnd is the adjacent/corner variant (L+I): the wool sits in a corner
                // gripped on two ADJACENT faces; else the centered variant (I+I): the wool bridges the two legs.
                // The legs only run from the wool down to the mouth — no crossbar to clear, so laneWidth + rd does it.
                Need(W >= 3 * laneWidth && H >= laneWidth + rd, family, W, H);
                int gap = W - 2 * laneWidth;                                    // the space between the legs
                if (woolAtEnd)
                {
                    // adjacent (corner, L+I): the wool sits in the top-LEFT corner, gripped on its RIGHT (a
                    // connector to the right leg) and its BOTTOM (the left leg) — two adjacent faces. The
                    // connector runs a row above the left leg, so the left leg reaches the mouth only through
                    // the wool: the wool is the cut cell. Both legs still meet the host on the bottom mouth.
                    t.Add(new(new(W - laneWidth, 0, laneWidth, H), CompoundParts.Entry));                   // right leg (full height, I)
                    t.Add(new(new(laneWidth, 0, gap, rd), CompoundParts.Bar));                       // connector: wool → right leg
                    t.Add(new(new(0, rd, laneWidth, H - rd), CompoundParts.Entry));                  // left leg (below the wool)
                    room = new(0, 0, laneWidth, rd);                                              // wool in the corner
                    vac.Add(new ShapeVacancy("bay", new(laneWidth, rd, gap, H - rd), BoxEdge.Bottom,
                        [CompoundParts.Entry, CompoundParts.Room, CompoundParts.Entry]));
                }
                else
                {
                    // centered (I+I): two straight legs, the wool bridging them across the top.
                    t.Add(new(new(0, 0, laneWidth, H), CompoundParts.Entry));                        // left leg (full height)
                    t.Add(new(new(W - laneWidth, 0, laneWidth, H), CompoundParts.Entry));                   // right leg (full height)
                    room = new(laneWidth, 0, gap, rd);                                            // wool bridges the two legs
                    vac.Add(new ShapeVacancy("bay", new(laneWidth, rd, gap, H - rd), BoxEdge.Bottom,
                        [CompoundParts.Entry, CompoundParts.Room, CompoundParts.Entry]));
                }
                break;
            }
            case CompoundForm.Donut:
            {
                // a rectangular ring around an enclosed hole (multi-access), built from NON-overlapping rects.
                // Hub-side attachment stub(s) extend the bars leftward (single = top only, double = top+bottom).
                // The wool sits off the ring's bottom-right — a stub (optionally a short I out), or integrated
                // AT the bottom-right corner (woolAtEnd), replacing that corner cell (tttt/vtvt/vttw).
                int extend = woolExtend ? laneWidth : 0, aw = attachmentWidth > 0 ? attachmentWidth : laneWidth;
                // a single attachment only has to clear the bottom bar (aw + laneWidth); two need to stack without
                // overlapping (2·aw + 1). Don't force the two-stub height on a one-stub ring.
                int needH = Math.Max(2 * laneWidth + 1, attachments >= 2 ? 2 * aw + 1 : aw + laneWidth);
                // the trailing room only exists on the non-woolAtEnd variant (see below) — the corner-integrated
                // wool replaces the ring's own bottom-right cell, so it costs no width past the ring
                Need(W >= 4 * laneWidth + extend + (woolAtEnd ? 0 : rd) && H >= needH, family, W, H);
                // the ring absorbs the whole box: its span derives from W (the min box gives the classic 3·laneWidth),
                // so an allocator-grown box widens the ring — and with it the enclosed hole — instead of leaving
                // dead width; the height already rides H the same way
                int ax = laneWidth, ringH = H, span = W - laneWidth - extend - (woolAtEnd ? 0 : rd);
                if (attachmentOffset < 0 || attachmentOffset + aw > (attachments >= 2 ? ringH - aw : ringH))
                    throw new ArgumentException(
                        $"attachment offset {attachmentOffset} slides the stub off the ring's edge (box {W}x{H}).");
                // the ring's four walls come from the shared geometry (CompoundEmitter.RingWallRects) — the same
                // walls the Ring, P, G and Double-hole are built from — but the donut lays them down in its own
                // order and names them entry-bar / room-bar, because the top bar is the hub side and the bottom
                // carries the wool
                var walls = ringWalls ?? RingWalls.Uniform(laneWidth);
                // widening consumes the box's slack: the walls must still leave a hole inside the span the box
                // affords. A vector too fat for this box is a directed refusal, not a silently squashed ring.
                if (walls.Min < 2 || span < walls.Left + walls.Right + 1 || ringH < walls.Top + walls.Bottom + 1)
                    throw new ArgumentException(
                        $"ring walls {walls.Top}/{walls.Right}/{walls.Bottom}/{walls.Left} do not fit the donut's " +
                        $"{span}x{ringH} ring in a {W}x{H} box.");
                var ring = CompoundEmitter.RingWallRects(walls, ax, 0, span, ringH);
                t.Add(new(ring.Top, CompoundParts.EntryBar));                   // top bar
                t.Add(new(ring.Left, CompoundParts.Leg));                       // left leg (middle only — no corner overlap)
                t.Add(new(ring.Right, CompoundParts.Leg));                      // right leg (middle only)
                t.Add(new(new(0, attachmentOffset, laneWidth, aw), CompoundParts.Entry)); // hub attachment, slid down the ring edge
                if (attachments >= 2) t.Add(new(new(0, ringH - aw, laneWidth, aw), CompoundParts.Entry)); // second attachment (bottom-left)
                if (woolAtEnd)
                {
                    // the bottom wall, stopped short of the corner the wool takes
                    t.Add(new(new(ring.Bottom.X, ring.Bottom.Z, span - walls.Right, ring.Bottom.Height), CompoundParts.RoomBar));
                    room = new(ax + span - walls.Right, ringH - walls.Bottom, walls.Right, walls.Bottom);  // wool AT the bottom-right corner (integrated)
                }
                else
                {
                    t.Add(new(ring.Bottom, CompoundParts.RoomBar));    // full bottom bar
                    int wxr = ax + span;                            // right of the ring's right leg
                    if (woolExtend) { t.Add(new(new(wxr, ringH - walls.Bottom, laneWidth, walls.Bottom), CompoundParts.Run)); wxr += laneWidth; }  // short I holding the wool
                    room = new(wxr, ringH - walls.Bottom, rd, walls.Bottom);   // wool off the bottom-right
                }
                vac.Add(new ShapeVacancy("hole",
                    new(ax + walls.Left, walls.Top, span - walls.Left - walls.Right, ringH - walls.Top - walls.Bottom), null,
                    [CompoundParts.EntryBar, CompoundParts.Leg, CompoundParts.RoomBar, CompoundParts.Leg]));
                break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(family), family, "unsupported family");
        }

        at ??= [room.Width / 2.0, room.Height / 2.0];
        t.Add(new(room, CompoundParts.Room));
        var compound = new Compound(t, vac, (room.X + at[0], room.Z + at[1]));
        if (!flip) return compound;

        // mirror along the mouth: across the vertical centre for a top/bottom mouth, the horizontal one for a side
        // mouth
        return compound.Turned(MouthEdge(family) is BoxEdge.Left or BoxEdge.Right ? BoxTurn.FlipZ : BoxTurn.FlipX, W, H);
    }

    /// <summary>Normalize an emission so its mouth lands on the TOP edge: maps every rect, the room, the
    /// marker and the vacancies from the canonical frame into the normalized box (whose dims swap when the
    /// map transposes). The three non-identity maps — vertical mirror, transpose, transpose + mirror — are
    /// pure reflections/rotations, so the family read is unchanged. Callers dock any edge by placing the
    /// normalized, mouth-up box themselves (a vertical flip at placement docks the bottom).</summary>
    public static (Compound Shape, int W, int H) OrientMouthTop(
        Compound s, CompoundForm family, int boxW, int boxH)
    {
        // The turn taking the family's mouth to the top: a flip for the bottom, a transpose for the left, and for
        // the right a transpose and a flip.
        var turn = MouthEdge(family) switch
        {
            BoxEdge.Top => BoxTurn.None,
            BoxEdge.Bottom => BoxTurn.FlipZ,
            BoxEdge.Left => BoxTurn.Transpose,
            _ => BoxTurn.Anticlockwise,
        };
        var (w, h) = turn.Extent(boxW, boxH);
        return (s.Turned(turn, boxW, boxH), w, h);
    }

    private static void Need(bool ok, CompoundForm family, int w, int h)
    {
        if (!ok) throw new ArgumentException($"box {w}x{h} is too small for family {family}.");
    }
}
