namespace PgmStudio.Pgm.Shapes;

/// <summary>One edge of an axis-aligned box, in box-local coordinates (Top = z min, Left = x min).</summary>
public enum BoxEdge { Top, Bottom, Left, Right }

/// <summary>Negative space a shape leaves inside its box, published at emit time — exact by construction,
/// no derive pass finds it. <see cref="Kind"/>: <c>bay</c> (open toward one edge — claimable by a later
/// box), <c>notch</c> (a corner remainder, open on two edges), <c>hole</c> (enclosed by the shape).
/// <see cref="Mouth"/> is the open edge of a bay (null for notches and holes); <see cref="Walls"/> are the
/// template slots of the pieces bounding it.</summary>
public sealed record ShapeVacancy(string Kind, int[] Rect, BoxEdge? Mouth, IReadOnlyList<string> Walls);

/// <summary>A pure emission: slot-typed terrain rects, the terminal rect, the marker offset within the
/// terminal (box-local half-cell coordinates), and the shape's vacancies. All rects are box-local.</summary>
public sealed record EmittedShape(
    IReadOnlyList<(int[] Rect, string Slot)> Terrain, int[] Room, double[] At, IReadOnlyList<ShapeVacancy> Vacancies);

/// <summary>
/// The one shape emitter — fills a W×H box with a base family at a corridor width, in the canonical
/// box-local frame, returning slot-typed rects (<see cref="ApproachSlots"/>), the terminal ("room") rect,
/// the marker offset and the emit-side vacancies. Pure cell geometry: no plan types, no roles, no ids —
/// bindings (the wool box, later the spawn box) stamp those. Every segment abuts its neighbour along a full
/// corridor-width edge; the shape stays inside the box; a box too small for the family's turns throws.
///
/// <para><b>The mouth</b> — the box edge the entry docks a host through — is family-specific in the
/// canonical frame (<see cref="MouthEdge"/>): I/L/Z/scythe enter at the top, U/H at the bottom (their legs
/// run down to the host), clamp/donut at the left (bars/stub open leftward). Callers reorient with
/// <see cref="Orient"/> to put any family's mouth on the edge they dock.</para>
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
        ShapeFamily.U or ShapeFamily.H => BoxEdge.Bottom,
        ShapeFamily.Clamp or ShapeFamily.Donut => flip ? BoxEdge.Right : BoxEdge.Left,
        _ => BoxEdge.Top,
    };

    /// <summary>The minimal box (W, H) <paramref name="family"/> needs at <paramref name="cw"/> with the
    /// given options — the emit-side counterpart of every <c>Emit</c> size guard, so a caller can size a box
    /// (or signal a directed too-small) without exception control flow.</summary>
    public static (int W, int H) MinBox(
        ShapeFamily family, int cw, RoomPlacement roomPlacement = RoomPlacement.Inline,
        int attachments = 1, bool woolExtend = false, int attachmentWidth = 0)
    {
        var rd = RoomDepthCells;
        var aw = attachmentWidth > 0 ? attachmentWidth : cw;
        return family switch
        {
            ShapeFamily.I when roomPlacement == RoomPlacement.SideTuck => (cw + rd, 2 * cw),
            ShapeFamily.I => (cw, rd + 1),
            ShapeFamily.L => (cw + rd + 1, 2 * cw),
            ShapeFamily.Z => (2 * cw, 3 * cw + rd),
            ShapeFamily.Scythe => (4 * cw, 2 * cw + rd),
            ShapeFamily.Clamp => (2 * cw, 2 * cw + 1),
            ShapeFamily.U => (3 * cw, 2 * cw + rd),
            ShapeFamily.H => (3 * cw, 2 * cw + 2 * rd),
            ShapeFamily.Donut => (4 * cw + (woolExtend ? cw : 0) + rd,
                Math.Max(2 * cw + 1, attachments >= 2 ? 2 * aw + 1 : aw + cw)),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "the emitter fills terminal-capped families"),
        };
    }

    /// <summary>Emit <paramref name="family"/> into a W×H box at <paramref name="cw"/> (cells).
    /// <paramref name="flip"/> mirrors the shape across the box's vertical centre (the turn goes left instead
    /// of right) so both handednesses are reachable. <paramref name="attachments"/> (donut) is the number of
    /// hub-side stubs, 1 or 2. <paramref name="woolAtEnd"/> (U / H / donut) puts the terminal on an end of the
    /// crossbar / integrates it at the ring corner. <paramref name="woolExtend"/> (donut) holds the terminal a
    /// short I out from the shape. <paramref name="attachmentWidth"/> (donut) is the hub-interface width of
    /// each attachment in cells (0 = one corridor width).</summary>
    public static EmittedShape Emit(
        ShapeFamily family, int boxW, int boxH, int cw, bool flip = false,
        RoomPlacement roomPlacement = RoomPlacement.Inline, int attachments = 1, bool woolAtEnd = false,
        bool woolExtend = false, int attachmentWidth = 0)
    {
        var (W, H) = (boxW, boxH);
        if (family == ShapeFamily.Isolated)
            throw new ArgumentException("the emitter fills terminal-capped families; Isolated is a derive-only reading.");
        if (cw < 2) throw new ArgumentException($"corridor width {cw} < 2 (a lane is at least one 10-block cell pair).");
        if (cw > W) throw new ArgumentException($"corridor width {cw} exceeds box width {W}.");
        if (roomPlacement == RoomPlacement.SideTuck && family != ShapeFamily.I)
            throw new ArgumentException($"side-tuck room is only supported for the I family in this pass (requested {family}).");

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
                // spine and the return leg (not a symmetric U).
                Need(W >= 4 * cw && H >= 2 * cw + rd, family, W, H);
                int botZ = H - cw;
                t.Add(([0, 0, cw, cw], ApproachSlots.Entry));            // top-left tail — the mouth
                t.Add(([cw, 0, cw, botZ], ApproachSlots.EntryRun));      // spine (down from the tail)
                t.Add(([cw, botZ, 3 * cw, cw], ApproachSlots.Bar));      // bottom bar (spine → return leg)
                t.Add(([3 * cw, rd, cw, botZ - rd], ApproachSlots.RoomRun)); // return leg (up), one bay over
                room = [3 * cw, 0, cw, rd];                              // wool caps the return leg (top-right)
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
                // the wool clamped between two parallel bars, approached from the open side (tt/vw/tt) — the wool
                // is the closing wall connecting them (terrain on two opposite sides, and it bridges them).
                Need(W >= 2 * cw && H >= 2 * cw + 1, family, W, H);
                int barLen = 2 * cw;
                t.Add(([0, 0, barLen, cw], ApproachSlots.Entry));           // top bar
                t.Add(([0, H - cw, barLen, cw], ApproachSlots.Entry));      // bottom bar
                room = [barLen - cw, cw, cw, H - 2 * cw];            // wool = the closing wall (connects the bars)
                vac.Add(new ShapeVacancy("bay", [0, cw, barLen - cw, H - 2 * cw], BoxEdge.Left,
                    [ApproachSlots.Entry, ApproachSlots.Room, ApproachSlots.Entry]));
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
                Need(W >= 4 * cw + extend + rd && H >= needH, family, W, H);
                int ax = cw, ringH = H, span = 3 * cw;               // ring x in [ax, ax+3cw); hub stubs sit in [0, cw)
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

        return new EmittedShape(t, room, at, vac);
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
