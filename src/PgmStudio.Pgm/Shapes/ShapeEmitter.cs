namespace PgmStudio.Pgm.Shapes;

/// <summary>One edge of an axis-aligned box, in box-local coordinates (Top = z min, Left = x min).</summary>
public enum BoxEdge { Top, Bottom, Left, Right }

/// <summary>Negative space a shape leaves inside its box, published at emit time — exact by construction,
/// no derive pass finds it. <see cref="Kind"/>: <c>bay</c> (open toward one edge — claimable by a later
/// box), <c>notch</c> (a corner remainder, open on two edges), <c>hole</c> (enclosed by the shape).
/// <see cref="Mouth"/> is the open edge of a bay (null for notches and holes); <see cref="Walls"/> are the
/// template slots of the pieces bounding it.</summary>
public sealed record ShapeVacancy(string Kind, int[] Rect, BoxEdge? Mouth, IReadOnlyList<string> Walls);

/// <summary>An approach emission: a terminal-free <see cref="ShapeBody"/> finished by the approach designation —
/// the terminal <see cref="Room"/> rect and the marker offset <see cref="At"/> within it (box-local half-cell
/// coordinates). <see cref="Terrain"/> and <see cref="Vacancies"/> read through to the body; all rects are
/// box-local. The <see cref="ShapeBody"/> is what the hub/frontline designations reuse without a room.</summary>
public sealed record EmittedShape(ShapeBody Body, int[] Room, double[] At)
{
    /// <summary>The body's structural-slotted rects — the walkable terrain of the approach reading.</summary>
    public IReadOnlyList<(int[] Rect, string Slot)> Terrain => Body.Pieces;

    /// <summary>The body's published vacancies.</summary>
    public IReadOnlyList<ShapeVacancy> Vacancies => Body.Vacancies;

    /// <summary>Assemble an approach emission from loose terrain + vacancies (wrapping them as a
    /// <see cref="ShapeBody"/>) plus the terminal room and marker.</summary>
    public EmittedShape(
        IReadOnlyList<(int[] Rect, string Slot)> terrain, int[] room, double[] at,
        IReadOnlyList<ShapeVacancy> vacancies)
        : this(new ShapeBody(terrain, vacancies), room, at) { }
}

/// <summary>
/// The one shape emitter — fills a W×H box with a base family at a corridor width, in the canonical box-local
/// frame. Pure cell geometry: no plan types, no roles, no ids — bindings (the wool box, the spawn box) stamp
/// those. It emits in <b>two stages</b>: <see cref="Body"/> builds the terminal-free <see cref="ShapeBody"/>
/// (slot-typed rects (<see cref="ApproachSlots"/>) + vacancies, shared by every box kind), and a
/// <b>designation</b> finishes it — <see cref="Emit"/> applies the approach designation (<see cref="Approach"/>),
/// stamping the terminal ("room") rect and marker; the hub/frontline designations read the same body without a
/// room. <see cref="OrientMouthTop"/> / <see cref="MouthEdge"/> place a family's mouth on the edge a caller docks.
/// </summary>
public static class ShapeEmitter
{
    /// <summary>The dead-end terminal's depth along the final corridor, in cells — a two-cell (~10-block)
    /// plateau that clears the export stamp.</summary>
    public const int RoomDepthCells = 2;

    /// <summary>The canonical-frame mouth edge of <paramref name="family"/> before any flip: where its entry
    /// slot(s) dock the host. <paramref name="flip"/> mirrors across the box's vertical centre, swapping a
    /// left/right mouth.</summary>
    public static BoxEdge MouthEdge(ShapeFamily family, bool flip = false) => family switch
    {
        ShapeFamily.U or ShapeFamily.H or ShapeFamily.Clamp => BoxEdge.Bottom,
        ShapeFamily.Donut => flip ? BoxEdge.Right : BoxEdge.Left,
        _ => BoxEdge.Top,
    };

    /// <summary>The minimal box (W, H) <paramref name="family"/> needs at <paramref name="cw"/> with the
    /// given options — the emit-side counterpart of every <c>Emit</c> size guard, so a caller can size a box
    /// (or signal a directed too-small) without exception control flow.</summary>
    public static (int W, int H) MinBox(
        ShapeFamily family, int cw, RoomPlacement roomPlacement = RoomPlacement.Inline,
        int attachments = 1, bool woolExtend = false, int attachmentWidth = 0, bool woolAtEnd = false)
    {
        var rd = RoomDepthCells;
        var aw = attachmentWidth > 0 ? attachmentWidth : cw;
        return family switch
        {
            ShapeFamily.I when roomPlacement == RoomPlacement.SideTuck => (cw + rd, 2 * cw),
            ShapeFamily.I => (cw, rd + 1),
            ShapeFamily.L => (cw + rd + 1, 2 * cw),
            ShapeFamily.Z when roomPlacement == RoomPlacement.SideTuck => (2 * cw + rd, 3 * cw + 1),
            ShapeFamily.Z => (2 * cw, 3 * cw + rd),
            ShapeFamily.Scythe when roomPlacement == RoomPlacement.SideTuck => (4 * cw + rd, 2 * cw + rd),
            ShapeFamily.Scythe => (4 * cw, 2 * cw + rd),
            // the clamp's legs only have to reach the mouth past the wool — it has no crossbar to clear, so it
            // does NOT inherit the U's 2·cw leg run (that depth became a 4-cell void under every clamp)
            ShapeFamily.Clamp => (3 * cw, cw + rd),
            ShapeFamily.U => (3 * cw, 2 * cw + rd),
            ShapeFamily.H => (3 * cw, 2 * cw + 2 * rd),
            // the trailing `rd` is the room hanging off the ring's bottom-right; the corner-integrated wool
            // (woolAtEnd) sits INSIDE the ring's span instead, so it needs none of it
            ShapeFamily.Donut => (4 * cw + (woolExtend ? cw : 0) + (woolAtEnd ? 0 : rd),
                Math.Max(2 * cw + 1, attachments >= 2 ? 2 * aw + 1 : aw + cw)),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "the emitter fills terminal-capped families"),
        };
    }

    /// <summary>Emit <paramref name="family"/> into a W×H box at <paramref name="cw"/> (cells) as an
    /// <b>approach</b>: build the terminal-free <see cref="Body"/> and stamp the approach designation onto it
    /// (<see cref="Approach"/>) — the terminal room and its marker. <paramref name="flip"/> mirrors the shape
    /// across the box's vertical centre (the turn goes left instead of right) so both handednesses are
    /// reachable. <paramref name="attachments"/> (donut) is the number of hub-side stubs, 1 or 2.
    /// <paramref name="woolAtEnd"/> (U / H / donut) puts the terminal on an end of the crossbar / integrates it
    /// at the ring corner. <paramref name="woolExtend"/> (donut) holds the terminal a short I out from the
    /// shape. <paramref name="attachmentWidth"/> (donut, scythe) is the hub-interface width of the attachment in
    /// cells, measured ALONG the edge it docks (0 = one corridor width; the width grammar's w2/w4/w6 =
    /// <c>cw</c>/<c>2·cw</c>/<c>3·cw</c>). <paramref name="entryShift"/> / <paramref name="woolShift"/> (scythe)
    /// slide the two independently-offsettable endpoints down the docking edge: the entry shift propagates
    /// inward — the spine it docks shrinks from the top with it — and the wool shift shortens the return leg the
    /// same way, so only the shifted endpoint still reaches the edge. <paramref name="attachmentOffset"/>
    /// (donut) slides the hub attachment down the ring's edge — only the attachment moves, the ring is
    /// unchanged.</summary>
    public static EmittedShape Emit(
        ShapeFamily family, int boxW, int boxH, int cw, bool flip = false,
        RoomPlacement roomPlacement = RoomPlacement.Inline, int attachments = 1, bool woolAtEnd = false,
        bool woolExtend = false, int attachmentWidth = 0, int entryShift = 0, int woolShift = 0,
        int attachmentOffset = 0)
    {
        var (terrain, room, at, vac) = Compose(
            family, boxW, boxH, cw, flip, roomPlacement, attachments, woolAtEnd, woolExtend,
            attachmentWidth, entryShift, woolShift, attachmentOffset);
        return Approach(new ShapeBody(terrain, vac), room, at);
    }

    /// <summary>The terminal-free <see cref="ShapeBody"/> of <paramref name="family"/> in a W×H box at
    /// <paramref name="cw"/> — the structural pieces and vacancies with <b>no terminal, marker, or id</b>, the
    /// stage every designation builds on. Same geometry and knobs as <see cref="Emit"/>; it just withholds the
    /// terminal (which the approach designation stamps). Hub/frontline designations read this same body.</summary>
    public static ShapeBody Body(
        ShapeFamily family, int boxW, int boxH, int cw, bool flip = false,
        RoomPlacement roomPlacement = RoomPlacement.Inline, int attachments = 1, bool woolAtEnd = false,
        bool woolExtend = false, int attachmentWidth = 0, int entryShift = 0, int woolShift = 0,
        int attachmentOffset = 0)
    {
        var (terrain, _, _, vac) = Compose(
            family, boxW, boxH, cw, flip, roomPlacement, attachments, woolAtEnd, woolExtend,
            attachmentWidth, entryShift, woolShift, attachmentOffset);
        return new ShapeBody(terrain, vac);
    }

    /// <summary>The <b>approach</b> designation over a terminal-free <paramref name="body"/>: finish it as a
    /// wool/spawn approach by stamping the terminal <paramref name="room"/> and its marker
    /// <paramref name="markerAt"/> (box-local, within the room). The sibling of the hub's per-edge-interface
    /// designation and the frontline's face designation — each takes the same <see cref="ShapeBody"/> and
    /// finishes it its own way; here the finish is a dead-end room.</summary>
    public static EmittedShape Approach(ShapeBody body, int[] room, double[] markerAt) =>
        new(body, room, markerAt);

    /// <summary>Build the family geometry into the canonical box, apply <paramref name="flip"/>, and return the
    /// loose parts — the terminal-free terrain + vacancies plus the reserved terminal (room + marker). The one
    /// per-family switch <see cref="Emit"/> and <see cref="Body"/> share; see <see cref="Emit"/> for the knobs.
    /// Every segment abuts its neighbour along a full corridor-width edge; the shape stays inside the box; a box
    /// too small for the family's turns throws.
    ///
    /// <para><b>The mouth</b> — the box edge the entry docks a host through — is family-specific in the
    /// canonical frame (<see cref="MouthEdge"/>): I/L/Z/scythe enter at the top, U/H at the bottom (their legs
    /// run down to the host), clamp/donut at the left (bars/stub open leftward). Callers reorient with
    /// <see cref="OrientMouthTop"/> to put any family's mouth on the edge they dock.</para></summary>
    private static (List<(int[] Rect, string Slot)> Terrain, int[] Room, double[] At, List<ShapeVacancy> Vacancies) Compose(
        ShapeFamily family, int boxW, int boxH, int cw, bool flip,
        RoomPlacement roomPlacement, int attachments, bool woolAtEnd,
        bool woolExtend, int attachmentWidth, int entryShift, int woolShift,
        int attachmentOffset)
    {
        var (W, H) = (boxW, boxH);
        if (family == ShapeFamily.Isolated)
            throw new ArgumentException("the emitter fills terminal-capped families; Isolated is a derive-only reading.");
        if (cw < 2) throw new ArgumentException($"corridor width {cw} < 2 (a lane is at least one 10-block cell pair).");
        if (cw > W) throw new ArgumentException($"corridor width {cw} exceeds box width {W}.");
        if (roomPlacement == RoomPlacement.SideTuck && family is not (ShapeFamily.I or ShapeFamily.Z or ShapeFamily.Scythe))
            throw new ArgumentException($"side-tuck room is supported for I, Z and scythe (requested {family}).");
        if ((entryShift != 0 || woolShift != 0) && family != ShapeFamily.Scythe)
            throw new ArgumentException($"entry/wool shifts are scythe endpoint knobs (requested {family}).");
        if (attachmentOffset != 0 && family != ShapeFamily.Donut)
            throw new ArgumentException($"the attachment offset is a donut knob (requested {family}).");

        var t = new List<(int[] Rect, string Slot)>();
        var vac = new List<ShapeVacancy>();
        int[] room;
        double[]? at = null;                                 // marker offset within the room (defaults to centre)
        var rd = RoomDepthCells;
        switch (family)
        {
            case ShapeFamily.I when roomPlacement == RoomPlacement.SideTuck:
            {
                // straight lane; the room is a stub off the SIDE of the lane at the terminal — the catalog's
                // side-tuck (tttt/vvvw): the lane runs straight to its end and the wool ducks off to one side.
                // The room is BESIDE the lane (shares a vertical corridor-width edge), never a wide cap extending
                // the lane's end. It reads I: the lane is straight and the room is excluded from the bend count.
                Need(W >= cw + rd && H >= 2 * cw, family, W, H);
                t.Add(([0, 0, cw, H], ApproachSlots.Entry));   // straight vertical lane, full depth (left)
                room = [cw, H - cw, rd, cw];                   // room off the lane's right side, at the terminal
                at = [rd / 2.0, cw / 2.0];
                if (H - cw > 0)
                    vac.Add(new ShapeVacancy("notch", [cw, 0, W - cw, H - cw], null, [ApproachSlots.Entry]));
                break;
            }
            case ShapeFamily.I:
            {
                Need(H >= rd + 1, family, W, H);
                int lx = (W - cw) / 2, laneH = H - rd;
                t.Add(([lx, 0, cw, laneH], ApproachSlots.Entry));
                room = [lx, laneH, cw, rd];
                if (lx > 0) vac.Add(new ShapeVacancy("notch", [0, 0, lx, H], null, [ApproachSlots.Entry]));
                if (W - lx - cw > 0)
                    vac.Add(new ShapeVacancy("notch", [lx + cw, 0, W - lx - cw, H], null, [ApproachSlots.Entry]));
                break;
            }
            case ShapeFamily.L:
            {
                // vertical arm down one side, a horizontal band across the bottom, room at its far end. The
                // +1 guarantees ≥1 cell of horizontal arm beyond the vertical, so the bend is real (without it
                // the band sits in the vertical's own column and the shape collapses to a straight I).
                Need(W >= cw + rd + 1 && H >= 2 * cw, family, W, H);
                int vLx = 0, bandZ = H - cw;
                t.Add(([vLx, 0, cw, bandZ], ApproachSlots.Entry));       // vertical arm (enters at the mouth)
                t.Add(([0, bandZ, W - rd, cw], ApproachSlots.Run));      // horizontal band up to the room
                room = [W - rd, bandZ, rd, cw];                          // dead-end at the far side of the band
                vac.Add(new ShapeVacancy("notch", [cw, 0, W - cw, H - cw], null,
                    [ApproachSlots.Entry, ApproachSlots.Run]));
                break;
            }
            case ShapeFamily.Z when roomPlacement == RoomPlacement.SideTuck:
            {
                // the run reaches the box bottom and the room docks its SIDE at the end, perpendicular —
                // the run is shortened in the sense that it no longer extends past the lane to hold the
                // room. Same I-family side-tuck grammar, and still a Z: the room is excluded from the bend
                // read, so the staircase stays a staircase.
                Need(W >= 2 * cw + rd && H >= 3 * cw + 1, family, W, H);
                int z1 = (H - cw) / 2;
                int botZ = z1 + cw;
                t.Add(([0, 0, cw, z1], ApproachSlots.Entry));            // top arm (left) — the mouth
                t.Add(([0, z1, W, cw], ApproachSlots.Bar));              // crossing band
                t.Add(([W - cw, botZ, cw, H - botZ], ApproachSlots.RoomRun)); // bottom arm to the box bottom
                room = [W - cw - rd, H - cw, rd, cw];                    // room off the run's interior side
                vac.Add(new ShapeVacancy("notch", [cw, 0, W - cw, z1], null,
                    [ApproachSlots.Entry, ApproachSlots.Bar]));
                break;
            }
            case ShapeFamily.Z:
            {
                // top arm on the left, a full-width band, bottom arm on the right ending in the room.
                Need(W >= 2 * cw && H >= 3 * cw + rd, family, W, H);
                int z1 = (H - rd - cw) / 2;                              // top-arm length (balanced with the bottom arm)
                int botZ = z1 + cw, botLen = H - rd - botZ;
                t.Add(([0, 0, cw, z1], ApproachSlots.Entry));            // top arm (left) — the mouth
                t.Add(([0, z1, W, cw], ApproachSlots.Bar));              // crossing band
                t.Add(([W - cw, botZ, cw, botLen], ApproachSlots.RoomRun)); // bottom arm (right) up to the room
                room = [W - cw, H - rd, cw, rd];
                vac.Add(new ShapeVacancy("notch", [cw, 0, W - cw, z1], null,
                    [ApproachSlots.Entry, ApproachSlots.Bar]));
                vac.Add(new ShapeVacancy("notch", [0, botZ, W - cw, H - botZ], null,
                    [ApproachSlots.Bar, ApproachSlots.RoomRun]));
                break;
            }
            case ShapeFamily.Scythe:
            {
                // the S-hook (ttvw/vtvt/vttt): enter at the top-left tail, drop the spine, run the bottom,
                // climb the return leg to the wool at top-right — three bends with a tight bay between the
                // spine and the return leg (not a symmetric U). Both endpoints may slide down the docking
                // edge: a shifted tail takes the spine's top with it (the docked piece resizes with the
                // shift — a full-height spine over a dropped tail is a different, wrong shape) and a
                // shifted wool takes the return leg's top the same way.
                int aw = attachmentWidth > 0 ? attachmentWidth : cw;     // tail width ALONG the spine it docks
                Need(W >= 4 * cw && H >= 2 * cw + rd, family, W, H);
                int botZ = H - cw;
                if (entryShift < 0 || entryShift + aw > botZ - 1)
                    throw new ArgumentException(
                        $"entry shift {entryShift} with attachment width {aw} leaves no spine above the bar (box {W}x{H}).");
                if (woolShift < 0 || woolShift + rd > botZ - cw)
                    throw new ArgumentException(
                        $"wool shift {woolShift} leaves no return leg above the bar (box {W}x{H}).");
                t.Add(([0, entryShift, cw, aw], ApproachSlots.Entry));   // tail — the mouth, slid down the edge
                t.Add(([cw, entryShift, cw, botZ - entryShift], ApproachSlots.EntryRun)); // spine, shrunk with it
                t.Add(([cw, botZ, 3 * cw, cw], ApproachSlots.Bar));      // bottom bar (spine → return leg)
                if (roomPlacement == RoomPlacement.SideTuck)
                {
                    // the wool docks the return leg's SIDE at its top end — the leg is shortened to the
                    // room's line instead of running out to hold it, and the tail stays lane-width (a
                    // thickened tail branches, independent of the docking)
                    Need(W >= 4 * cw + rd, family, W, H);
                    if (woolShift + cw > botZ - 1)
                        throw new ArgumentException(
                            $"wool shift {woolShift} leaves no return leg above the bar (box {W}x{H}).");
                    t.Add(([3 * cw, woolShift, cw, botZ - woolShift], ApproachSlots.RoomRun));
                    room = [4 * cw, woolShift, rd, cw];                  // perpendicular, off the outer side
                }
                else
                {
                    t.Add(([3 * cw, woolShift + rd, cw, botZ - woolShift - rd], ApproachSlots.RoomRun)); // return leg
                    room = [3 * cw, woolShift, cw, rd];                  // wool caps the return leg
                }
                vac.Add(new ShapeVacancy("bay", [2 * cw, 0, cw, botZ], BoxEdge.Top,
                    [ApproachSlots.EntryRun, ApproachSlots.Bar, ApproachSlots.RoomRun]));
                break;
            }
            case ShapeFamily.H:
            {
                // the branch with a room-run STUB: two legs run down to the hub and merge at a crossbar; the wool
                // caps a short stub rising from the crossbar's opposite side — its middle, or an end (woolAtEnd).
                // TWO attachment feet + the wool on its own stub (multi-access), not a 4-armed +.
                Need(W >= 3 * cw && H >= 2 * cw + 2 * rd, family, W, H);
                int barZ = 2 * rd;                                       // wool + stub above the bar
                int wx = woolAtEnd ? 0 : (W - cw) / 2;
                t.Add(([0, barZ, W, cw], ApproachSlots.Bar));                        // crossbar (full width)
                t.Add(([0, barZ + cw, cw, H - barZ - cw], ApproachSlots.Entry));     // left leg (down to the hub)
                t.Add(([W - cw, barZ + cw, cw, H - barZ - cw], ApproachSlots.Entry)); // right leg (down to the hub)
                t.Add(([wx, rd, cw, rd], ApproachSlots.RoomRun));        // room-run stub from the crossbar up to the wool
                room = [wx, 0, cw, rd];                                  // wool caps the stub (middle or an end)
                vac.Add(new ShapeVacancy("bay", [cw, barZ + cw, W - 2 * cw, H - barZ - cw], BoxEdge.Bottom,
                    [ApproachSlots.Entry, ApproachSlots.Bar, ApproachSlots.Entry]));
                break;
            }
            case ShapeFamily.U:
            {
                // the branch with the wool FLUSH on the crossbar (no stub): two legs down to the hub, the
                // crossbar, and the wool docked directly on the crossbar's opposite side — middle, or an end
                // (woolAtEnd). The crossbar reaches past the wool toward the legs, so the wool sits on a bar
                // wider than itself — what separates U from H.
                Need(W >= 3 * cw && H >= 2 * cw + rd, family, W, H);
                int barZ = rd;                                           // wool sits directly above the bar
                int wx = woolAtEnd ? 0 : (W - cw) / 2;
                t.Add(([0, barZ, W, cw], ApproachSlots.Bar));                        // crossbar (full width)
                t.Add(([0, barZ + cw, cw, H - barZ - cw], ApproachSlots.Entry));     // left leg (down to the hub)
                t.Add(([W - cw, barZ + cw, cw, H - barZ - cw], ApproachSlots.Entry)); // right leg (down to the hub)
                room = [wx, 0, cw, rd];                                  // wool flush on the crossbar
                vac.Add(new ShapeVacancy("bay", [cw, barZ + cw, W - 2 * cw, H - barZ - cw], BoxEdge.Bottom,
                    [ApproachSlots.Entry, ApproachSlots.Bar, ApproachSlots.Entry]));
                break;
            }
            case ShapeFamily.Clamp:
            {
                // the wool clamped INSIDE the shape as a cut cell: two legs run down to the hub (one U-style
                // mouth, MouthEdge Bottom) and the wool is their ONLY bridge — remove it and the terrain falls
                // into two pieces. woolAtEnd is the adjacent/corner variant (L+I): the wool sits in a corner
                // gripped on two ADJACENT faces; else the centered variant (I+I): the wool bridges the two legs.
                // The legs only run from the wool down to the mouth — no crossbar to clear, so cw + rd does it.
                Need(W >= 3 * cw && H >= cw + rd, family, W, H);
                int gap = W - 2 * cw;                                    // the space between the legs
                if (woolAtEnd)
                {
                    // adjacent (corner, L+I): the wool sits in the top-LEFT corner, gripped on its RIGHT (a
                    // connector to the right leg) and its BOTTOM (the left leg) — two adjacent faces. The
                    // connector runs a row above the left leg, so the left leg reaches the mouth only through
                    // the wool: the wool is the cut cell. Both legs still meet the host on the bottom mouth.
                    t.Add(([W - cw, 0, cw, H], ApproachSlots.Entry));                   // right leg (full height, I)
                    t.Add(([cw, 0, gap, rd], ApproachSlots.Bar));                       // connector: wool → right leg
                    t.Add(([0, rd, cw, H - rd], ApproachSlots.Entry));                  // left leg (below the wool)
                    room = [0, 0, cw, rd];                                              // wool in the corner
                    vac.Add(new ShapeVacancy("bay", [cw, rd, gap, H - rd], BoxEdge.Bottom,
                        [ApproachSlots.Entry, ApproachSlots.Room, ApproachSlots.Entry]));
                }
                else
                {
                    // centered (I+I): two straight legs, the wool bridging them across the top.
                    t.Add(([0, 0, cw, H], ApproachSlots.Entry));                        // left leg (full height)
                    t.Add(([W - cw, 0, cw, H], ApproachSlots.Entry));                   // right leg (full height)
                    room = [cw, 0, gap, rd];                                            // wool bridges the two legs
                    vac.Add(new ShapeVacancy("bay", [cw, rd, gap, H - rd], BoxEdge.Bottom,
                        [ApproachSlots.Entry, ApproachSlots.Room, ApproachSlots.Entry]));
                }
                break;
            }
            case ShapeFamily.Donut:
            {
                // a rectangular ring around an enclosed hole (multi-access), built from NON-overlapping rects.
                // Hub-side attachment stub(s) extend the bars leftward (single = top only, double = top+bottom).
                // The wool sits off the ring's bottom-right — a stub (optionally a short I out), or integrated
                // AT the bottom-right corner (woolAtEnd), replacing that corner cell (tttt/vtvt/vttw).
                int extend = woolExtend ? cw : 0, aw = attachmentWidth > 0 ? attachmentWidth : cw;
                // a single attachment only has to clear the bottom bar (aw + cw); two need to stack without
                // overlapping (2·aw + 1). Don't force the two-stub height on a one-stub ring.
                int needH = Math.Max(2 * cw + 1, attachments >= 2 ? 2 * aw + 1 : aw + cw);
                // the trailing room only exists on the non-woolAtEnd variant (see below) — the corner-integrated
                // wool replaces the ring's own bottom-right cell, so it costs no width past the ring
                Need(W >= 4 * cw + extend + (woolAtEnd ? 0 : rd) && H >= needH, family, W, H);
                int ax = cw, ringH = H, span = 3 * cw;               // ring x in [ax, ax+3cw); hub stubs sit in [0, cw)
                if (attachmentOffset < 0 || attachmentOffset + aw > (attachments >= 2 ? ringH - aw : ringH))
                    throw new ArgumentException(
                        $"attachment offset {attachmentOffset} slides the stub off the ring's edge (box {W}x{H}).");
                t.Add(([ax, 0, span, cw], ApproachSlots.EntryBar));          // top bar
                t.Add(([ax, cw, cw, ringH - 2 * cw], ApproachSlots.Leg));    // left leg (middle only — no corner overlap)
                t.Add(([ax + 2 * cw, cw, cw, ringH - 2 * cw], ApproachSlots.Leg)); // right leg (middle only)
                t.Add(([0, attachmentOffset, cw, aw], ApproachSlots.Entry)); // hub attachment, slid down the ring edge
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
                    room = [wxr, ringH - cw, rd, cw];               // wool off the bottom-right
                }
                vac.Add(new ShapeVacancy("hole", [ax + cw, cw, cw, ringH - 2 * cw], null,
                    [ApproachSlots.EntryBar, ApproachSlots.Leg, ApproachSlots.RoomBar, ApproachSlots.Leg]));
                break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(family), family, "unsupported family");
        }

        at ??= [room[2] / 2.0, room[3] / 2.0];
        if (flip)
        {
            foreach (var (rect, _) in t) rect[0] = W - rect[0] - rect[2];   // slot survives the mirror
            room[0] = W - room[0] - room[2];
            at = [room[2] - at[0], at[1]];                   // mirror the marker within the flipped room
            for (var i = 0; i < vac.Count; i++)
            {
                var v = vac[i];
                var m = v.Mouth switch
                {
                    BoxEdge.Left => BoxEdge.Right, BoxEdge.Right => BoxEdge.Left,
                    _ => v.Mouth,
                };
                vac[i] = v with { Rect = [W - v.Rect[0] - v.Rect[2], v.Rect[1], v.Rect[2], v.Rect[3]], Mouth = m };
            }
        }

        return (t, room, at, vac);
    }

    /// <summary>Normalize an emission so its mouth lands on the TOP edge: maps every rect, the room, the
    /// marker and the vacancies from the canonical frame into the normalized box (whose dims swap when the
    /// map transposes). The three non-identity maps — vertical mirror, transpose, transpose + mirror — are
    /// pure reflections/rotations, so the family read is unchanged. Callers dock any edge by placing the
    /// normalized, mouth-up box themselves (a vertical flip at placement docks the bottom).</summary>
    public static (EmittedShape Shape, int W, int H) OrientMouthTop(
        EmittedShape s, ShapeFamily family, bool flip, int boxW, int boxH)
    {
        var source = MouthEdge(family, flip);
        if (source == BoxEdge.Top) return (s, boxW, boxH);

        Func<int[], int[]> map = source switch
        {
            BoxEdge.Bottom => r => [r[0], boxH - r[1] - r[3], r[2], r[3]],       // vertical mirror
            BoxEdge.Left => r => [r[1], r[0], r[3], r[2]],                       // transpose
            _ => r => [r[1], boxW - r[0] - r[2], r[3], r[2]],                    // transpose + vertical mirror
        };
        var dims = source == BoxEdge.Bottom ? (W: boxW, H: boxH) : (W: boxH, H: boxW);

        BoxEdge? MouthMap(BoxEdge? e) => e is null ? null : source switch
        {
            BoxEdge.Bottom => e switch
            {
                BoxEdge.Top => BoxEdge.Bottom, BoxEdge.Bottom => BoxEdge.Top, _ => e,
            },
            BoxEdge.Left => e switch
            {
                BoxEdge.Left => BoxEdge.Top, BoxEdge.Top => BoxEdge.Left,
                BoxEdge.Right => BoxEdge.Bottom, _ => BoxEdge.Right,
            },
            _ => e switch
            {
                BoxEdge.Right => BoxEdge.Top, BoxEdge.Top => BoxEdge.Left,
                BoxEdge.Left => BoxEdge.Bottom, _ => BoxEdge.Right,
            },
        };

        var terrain = s.Terrain.Select(p => (map(p.Rect), p.Slot)).ToList();
        var room = map(s.Room);
        double ax = s.At[0], az = s.At[1];
        double[] at = source switch
        {
            BoxEdge.Bottom => [ax, s.Room[3] - az],
            BoxEdge.Left => [az, ax],
            _ => [az, s.Room[2] - ax],
        };
        var vac = s.Vacancies.Select(v => v with { Rect = map(v.Rect), Mouth = MouthMap(v.Mouth) }).ToList();
        return (new EmittedShape(terrain, room, at, vac), dims.W, dims.H);
    }

    private static void Need(bool ok, ShapeFamily family, int w, int h)
    {
        if (!ok) throw new ArgumentException($"box {w}x{h} is too small for family {family}.");
    }
}
