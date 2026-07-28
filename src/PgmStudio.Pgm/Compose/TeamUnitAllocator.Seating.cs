using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>Turning demands into positions — one integer per neighbour, the seat, under whichever of the three
/// dock rules its style selects.</summary>
public static partial class TeamUnitAllocator
{
    /// <summary>Seat every demand on <paramref name="form"/>'s real free-edge intervals, seated on the hub
    /// <paramref name="hubRect"/>. Builds the body once (<see cref="HubBoxEmitter"/>) — the same body the filler
    /// re-emits, so both read the same runs — and reads its per-edge free runs off the emitted offers (the
    /// offerable surface, §1.13). Returns the hub box (carrying <paramref name="form"/> for the filler) plus the
    /// seated neighbour boxes and their hub joints, or <c>null</c> when the box is too small for the form or a
    /// demand finds no free run to dock (the directed signal the caller answers by falling back / resampling).</summary>
    private static (List<Box> Boxes, List<BoxJoint> Joints)? Seat(
        CompoundRead form, CellRect hubRect, Frame frame, int laneWidthCells, IReadOnlyList<Demand> demands, ComposeRng rng,
        bool noFront, RingWalls? walls = null, IReadOnlyList<(int Start, int Width)>? arms = null)
    {
        int boxW = hubRect.Width, boxH = hubRect.Height;
        var frontEdge = SideEdge(frame, UnitSide.Front);
        // orient the form so its open feet face the unused front (SP: the frontline's side) and its solid edges
        // cover the demanded back/laterals — a vertical flip when the front is the box's top edge (every z-frame);
        // symmetric forms (Rectangle, Ring) are unaffected, so this is safe to apply uniformly
        var flipV = frontEdge == BoxEdge.Top;
        var hubBox = new Box("hub", BoxKind.Hub, hubRect, boxW * boxH, form, flipV,
            HubWalls: walls, HubArms: arms);
        if (HubBoxEmitter.Fill(hubBox, form, FillProfiles.HubWallCells, flipV: flipV, ringWalls: walls,
                armLayout: arms) is not { } hub)
            return null;   // too small

        // the offerable surface: the contiguous free runs on each hub edge (box-local along-coords), read off the
        // emitted body's per-edge offers — one offer per free run, so a bay simply yields no run over its stretch
        var runsByEdge = hub.Offers.GroupBy(o => o.Edge).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<(int Start, int Len)>)g.Select(o => (o.Interval.Start, o.Interval.LengthCells)).ToList());

        var boxes = new List<Box> { hubBox };
        var joints = new List<BoxJoint>();
        // seats left flush with the front — handed to the FrontGuard post-pass once every neighbour is seated
        var flushSeats = new List<FrontGuard.FlushSeat>();

        // the seat-step separation law: no spawn/wool neighbour may seat within the gap (the map lane width — w2 =
        // 10 blocks, w3 = 15 on wide boards) of another. Each already-seated spawn/wool projects onto the edge
        // being seated as a forbidden along-interval, so SeatInRuns samples a legal position directly — one pass
        // covering same-edge abut and adjacent-edge corner meetings alike. The frontline keeps no such gap (its
        // wool clearance is a build-zone rule, not this one).
        List<(int Start, int Len)> Blocked(BoxEdge edge, int depth) => boxes
            .Where(b => b.Kind is BoxKind.Spawn or BoxKind.Wool)
            .Select(b => ProjectOntoEdge(edge, hubRect, depth, b.Rect, laneWidthCells))
            .Where(iv => iv is not null).Select(iv => iv!.Value).ToList();

        // record a seated neighbour: its box, and the joint granting it its corridor width. One place, so the
        // three dock styles differ only in how they FOUND the rect — never in what they record.
        void Seated(Demand nb, CellRect rect, BoxAbutment abutment, int grantedWidthCells)
        {
            boxes.Add(new Box(nb.Id, nb.Kind, rect, nb.Along * nb.Depth, Wool: nb.Wool));
            joints.Add(HubJointFrom("hub", nb.Id, abutment, grantedWidthCells));
        }

        foreach (var demand in demands)
        {
            var d = demand;
            var edge = SideEdge(frame, d.Side);
            var edgeLen = edge is BoxEdge.Top or BoxEdge.Bottom ? boxW : boxH;
            if (!runsByEdge.TryGetValue(edge, out var runs)) return null;      // the form leaves this edge empty
            var grantedWidthCells = d.Kind == BoxKind.Wool ? WoolLaneCells : laneWidthCells;           // the wool lane is w2; spawn/frontline read the map lane width
            var style = StyleOf(d);

            if (style is DockStyle.Overhang && d.Wool is { } rich)
            {
                // no frontline ⇒ prefer the overhang placement furthest behind the front face (bent back / flipped),
                // not spiking across the empty no-man's-land in front of the hub
                var guardFront = noFront ? frontEdge : (BoxEdge?)null;
                if (SeatOverhang(runs, edgeLen, d, rich, edge, hubRect, boxes, grantedWidthCells, laneWidthCells, guardFront, rng) is { } placed)
                {
                    Seated(d with { Wool = rich with { Flip = placed.Flip } }, placed.Box, placed.Abutment, grantedWidthCells);
                    continue;
                }
                // no clear overhang placement on this hub (crowded / narrow): demote to the compact I and
                // re-dispatch as a full mouth. The demotion IS the fallback ladder — stated, not fallen through.
                d = Compact(d, grantedWidthCells);
                style = DockStyle.FullMouth;
            }

            if (style is DockStyle.ContactPatch)
            {
                if (SeatFront(runs, edgeLen, d, edge, hubRect, boxes, laneWidthCells, rng) is not { } placed) return null;
                Seated(d, placed.Box, placed.Abutment, grantedWidthCells);
                continue;
            }

            if (SeatFullMouth(runs, edgeLen, d, edge, hubRect, Blocked, laneWidthCells, grantedWidthCells, noFront, frontEdge, rng)
                is not { } dock)
            {
                // a wool that no longer fits with the seat gap (the third wool doubling onto the spawn's own edge
                // cannot clear the gap on a small hub — it only ever fit by touching) is dropped rather than
                // failing the whole unit, so long as a wool already seated: the unit keeps its objectives, one
                // fewer. The spawn and frontline are not droppable — a demand they cannot seat is a real too-small
                // signal the caller answers by falling back / resampling.
                if (d.Kind == BoxKind.Wool && boxes.Any(b => b.Kind == BoxKind.Wool)) continue;
                return null;
            }
            if (dock.Flush is { } flush) flushSeats.Add(flush);
            Seated(dock.Demand, dock.Box, dock.Abutment, grantedWidthCells);   // dock.Demand — a full mouth may have demoted it
        }

        // FrontGuard.Resolve — the post-pass over the seating: the seats the immediate slide could not bring
        // off the front are shifted / relocated / dropped there, deterministically (no draws). A residue on a
        // non-rectangle form is the directed "cannot host" signal — the caller's rectangle fallback re-seats on
        // four full edges, which usually hold a lawful off-front seat the form's runs could not; only the
        // rectangle itself keeps the flush seat, the flagged residue of a truly saturated hub.
        if (flushSeats.Count > 0)
        {
            var (rBoxes, rJoints, residue) = FrontGuard.Resolve(boxes, joints, flushSeats, hubRect, frontEdge, laneWidthCells, runsByEdge);
            if (residue > 0 && form.Form != Compound.Rectangle) return null;
            (boxes, joints) = (rBoxes, rJoints);
        }
        return (boxes, joints);
    }

    /// <summary>What a full-mouth dock produced: the placed <see cref="Box"/> rect and the
    /// <see cref="Abutment"/> it abuts the hub over, the <see cref="Demand"/> <b>as it ended up</b> (a wool whose
    /// full mouth found no run is demoted to the compact <c>I</c>, so this may differ from the one passed in),
    /// and the <see cref="Flush"/> seat to hand <see cref="FrontGuard.Resolve"/> when the immediate slide found
    /// no backward position. <c>null</c> from <see cref="SeatFullMouth"/> means no legal seat at all.</summary>
    private sealed record FullMouthDock(
        CellRect Box, BoxAbutment Abutment, Demand Demand, FrontGuard.FlushSeat? Flush);

    /// <summary>
    /// Seat a neighbour by <b>full mouth</b>: its whole along-extent must lie inside one of the hub's free
    /// <paramref name="runs"/>. The shape-agnostic rule — it assumes nothing about where the shape's entries
    /// are, so it serves the spawn, the plain <c>I</c> wools and the dual-entry staples alike (an overhang
    /// would strand a staple's second entry off the hub).
    ///
    /// <para>A wool whose mouth no run holds is demoted once to the compact <c>I</c> and retried; the demand
    /// that comes back on <see cref="FullMouthDock.Demand"/> is the one the caller must build the box from.
    /// The no-frontline front guard then slides a lateral seat backward off the hub's front face
    /// (deterministic, no draw); a seat no backward position can hold is returned as a
    /// <see cref="FrontGuard.FlushSeat"/> for the post-pass rather than failing here.</para>
    ///
    /// <para><paramref name="blocked"/> is the caller's projection of the already-seated spawn/wool boxes onto
    /// an edge — passed as a delegate because it closes over the boxes seated so far, which grows as the loop
    /// runs.</para>
    /// </summary>
    private static FullMouthDock? SeatFullMouth(
        IReadOnlyList<(int Start, int Len)> runs, int edgeLen, Demand demand, BoxEdge edge, CellRect hubRect,
        Func<BoxEdge, int, List<(int Start, int Len)>> blocked, int laneWidthCells, int grantedWidthCells,
        bool noFront, BoxEdge frontEdge, ComposeRng rng)
    {
        var d = demand;
        var seatGap = d.Kind is BoxKind.Spawn or BoxKind.Wool ? laneWidthCells : 0;
        List<(int Start, int Len)> blk = seatGap > 0 ? blocked(edge, d.Depth) : [];
        var seat = SeatInRuns(runs, blk, edgeLen, d.Along, CornerClearanceCells, seatGap, rng);
        if (seat is null && d.Kind == BoxKind.Wool)   // a staple's full mouth found no run — the compact I will
        {
            d = Compact(d, grantedWidthCells);
            blk = blocked(edge, d.Depth);
            seat = SeatInRuns(runs, blk, edgeLen, d.Along, CornerClearanceCells, seatGap, rng);
        }
        if (seat is not { } s) return null;

        // no-frontline front guard: a lateral seat flush with the hub front face slides back to the nearest
        // clear off-front position (deterministic — no draw, so a seat already off the front re-seats
        // bit-identically); a seat no backward position can hold yet (the separation gap blocks the whole edge)
        // is handed to the FrontGuard.Resolve post-pass instead.
        FrontGuard.FlushSeat? flush = null;
        if (noFront && d.Kind is BoxKind.Spawn or BoxKind.Wool
            && edge != frontEdge && edge != Opposite(frontEdge))
        {
            if (FrontGuard.ShiftOffFront(runs, blk, edgeLen, d.Along, seatGap, s,
                    frontAtLow: frontEdge is BoxEdge.Top or BoxEdge.Left) is { } offFront)
                s = offFront;
            else flush = new FrontGuard.FlushSeat(d.Id, d.Kind, d.Depth, d.Along, edge, edgeLen, runs, seatGap);
        }
        return new FullMouthDock(
            NeighbourRect(edge, s, d.Depth, d.Along, hubRect), new BoxAbutment(edge, s, d.Along), d, flush);
    }

    /// <summary>A free box-local along-position for an <paramref name="along"/>-wide dock among the edge's
    /// <paramref name="runs"/> (its offerable surface), avoiding the <paramref name="occupied"/> intervals (each
    /// inflated by <paramref name="separationCells"/> — the inter-seat separation law, so two neighbours on one
    /// edge never abut) and an <paramref name="inset"/>-cell clearance at each <b>box corner</b> — a run end
    /// coinciding with along-coord 0 or <paramref name="edgeLen"/>, so no neighbour seats at a hub corner and
    /// corner-touches a neighbour on the adjacent side; an internal run end (a bay boundary) is no box corner and
    /// needs no inset. Sampled within a randomly chosen fitting gap, or null when no gap holds it.
    /// <paramref name="separationCells"/> is a neighbour↔neighbour clearance only — distinct from the corner law
    /// (corners keep <paramref name="inset"/> 0; the mass-level pinch gate owns the hub's own corners).</summary>
    private static int? SeatInRuns(
        IReadOnlyList<(int Start, int Len)> runs, List<(int Start, int Len)> occupied,
        int edgeLen, int along, int inset, int separationCells, ComposeRng rng)
    {
        var gaps = new List<(int Lo, int Hi)>();
        foreach (var (rs, rl) in runs)
        {
            int lo = rs, hi = rs + rl;
            if (lo == 0) lo += inset;                                 // a box corner at the low end
            if (hi == edgeLen) hi -= inset;                          // a box corner at the high end
            var cursor = lo;
            foreach (var (os, ol) in occupied
                .Where(o => o.Start - separationCells < hi && o.Start + o.Len + separationCells > lo).OrderBy(o => o.Start))
            {
                if (os - separationCells - cursor >= along) gaps.Add((cursor, os - separationCells));   // keep the separation clear of the seat
                cursor = Math.Max(cursor, os + ol + separationCells);
            }
            if (hi - cursor >= along) gaps.Add((cursor, hi));
        }
        if (gaps.Count == 0) return null;
        var (glo, ghi) = gaps[rng.NextInt(0, gaps.Count)];
        return glo + rng.NextInt(0, ghi - glo - along + 1);
    }

    /// <summary>
    /// Seat the frontline on the hub's front edge by its <b>contact patch</b> (G123). Every other neighbour docks
    /// by fitting wholly inside a free run; the frontline does not have to, because its face is what the mid
    /// meets rather than a corridor the hub must hold. So a position is legal when the face abuts the hub over at
    /// least <paramref name="cw"/> contiguous cells of one free run — which admits a face narrower than the edge
    /// (seated anywhere along it) and one wider than the edge (overhanging either end).
    ///
    /// <para>Returns the plan-cell box and the <b>real</b> hub↔frontline interface, clipped to the abutment and
    /// so narrower than the box whenever it overhangs — the filler reads the offer off this, not off the face
    /// width. A face that overhangs must keep the neighbour separation gap from every seated spawn/wool: past
    /// the hub's corner there is no hub cell bridging the meeting, so a frontline corner and a wool corner would
    /// meet as a bare diagonal pinch, which the corner law forbids. (A full-width face meets those neighbours at
    /// the hub's own corner, which the hub fills — that is why the pinned face never needed this.) <c>null</c>
    /// when no position gives a patch — the directed signal the caller answers by falling back.</para>
    /// </summary>
    private static (CellRect Box, BoxAbutment Abutment)? SeatFront(
        IReadOnlyList<(int Start, int Len)> runs, int edgeLen, Demand d, BoxEdge edge, CellRect hubRect,
        IReadOnlyList<Box> seated, int laneWidthCells, ComposeRng rng)
    {
        var placements = new List<(int Seat, CellRect Box, BoxAbutment Abutment)>();
        for (var seat = -(d.Along - laneWidthCells); seat <= edgeLen - laneWidthCells; seat++)
        {
            int lo = seat, hi = seat + d.Along;
            if (!Docks(runs, lo, hi, laneWidthCells)) continue;
            if (PinchesAtEnd(runs, seat, seat + d.Along)) continue;
            var box = NeighbourRect(edge, seat, d.Depth, d.Along, hubRect);
            var overhangs = seat < 0 || seat + d.Along > edgeLen;
            if (seated.Any(b => b.Kind is BoxKind.Spawn or BoxKind.Wool
                    && (overhangs ? TooClose(b.Rect, box, laneWidthCells) : Overlap(b.Rect, box)))) continue;
            if (BoxPartition.SharedEdge(hubRect, box) is { } abutment) placements.Add((seat, box, abutment));
        }
        if (placements.Count == 0) return null;

        // (see PinchesAtEnd for the end-alignment law the loop above applies)

        // Centred by default. Sliding the face along the edge is the funnel, and it costs the mid band slack
        // (Composer.FrontHullSlackCells) — so it is a sampled exception, not what every seat does. Without this
        // even a full-width face would land off-centre, since every overhanging position is legal too.
        if (!rng.NextBool(ShiftedFaceChance))
        {
            var centre = (edgeLen - d.Along) / 2.0;
            var best = placements.OrderBy(p => Math.Abs(p.Seat - centre)).ThenBy(p => p.Seat).First();
            return (best.Box, best.Abutment);
        }
        var pick = placements[rng.NextInt(0, placements.Count)];
        return (pick.Box, pick.Abutment);
    }

    /// <summary>
    /// The <b>spanning dock</b> (G123): whether a face covering edge-local <c>[lo, hi)</c> holds the hub properly.
    /// Its contact patches are where the face meets the edge's free <paramref name="runs"/>; it docks when there
    /// is at least one and <b>every</b> patch is at least <paramref name="laneWidthCells"/> wide.
    ///
    /// <para>"Every", not "any", is the whole law. A face wide enough to reach across a bay-fronted hub's bay
    /// (a G, U or L) rests on a <b>shoulder each side of the hole</b>, and a shoulder thinner than a corridor is
    /// a sliver — the face is cantilevered over the bay, held by one side. Requiring the width per patch is what
    /// turns "the face happens to touch the far run" into "the face is anchored on both shoulders", which is what
    /// seals the bay into a declared hole rather than leaving a lip hanging over it.</para>
    ///
    /// <para>On a solid front there is one patch and this reduces to the single-patch rule.</para>
    /// </summary>
    private static bool Docks(IReadOnlyList<(int Start, int Len)> runs, int lo, int hi, int laneWidthCells)
    {
        var patches = runs
            .Select(r => Math.Min(hi, r.Start + r.Len) - Math.Max(lo, r.Start))
            .Where(len => len > 0).ToList();
        return patches.Count > 0 && patches.All(len => len >= laneWidthCells);
    }

    /// <summary>
    /// Whether a frontline spanning edge-local <c>[lo, hi)</c> would meet the hub's own edge terrain as a bare
    /// <b>diagonal pinch</b> at either of its ends — the corner law, applied where the face stops.
    ///
    /// <para>The frontline's spine is solid across its span, so along the shared edge the two masses meet cell by
    /// cell. The bad alignment is an end that lands where the hub's edge goes from <em>filled</em> just outside
    /// the face to <em>empty</em> just inside it (a hub bay starting exactly at the face's end): the face's end
    /// cell and the hub's last filled cell then touch only at a corner, with both orthogonal neighbours empty.
    /// An end inside a run is fine (the hub is filled under both sides), and so is an end at a run's start (both
    /// sides empty) or clear of the hub entirely (an overhang).</para>
    ///
    /// <para>This is why the pinned full-width face never needed the check: it ended at the hub's own corners,
    /// where there is no edge terrain beyond it to meet.</para>
    /// </summary>
    private static bool PinchesAtEnd(IReadOnlyList<(int Start, int Len)> runs, int lo, int hi)
    {
        bool Filled(int cell) => runs.Any(r => r.Start <= cell && cell < r.Start + r.Len);
        return (Filled(lo - 1) && !Filled(lo)) || (Filled(hi) && !Filled(hi - 1));
    }

    /// <summary>Seat a <b>rich</b> wool by the seat-and-shift: probe the family's narrow <b>entry</b> on its mouth,
    /// place the box so that entry lands on a hub <paramref name="runs"/> interval while the wider body <b>overhangs</b>
    /// the edge, and reject any placement whose box overlaps a seated box. Both handednesses are tried (the body
    /// overhanging either way), so a crowded side does not sink the dock. Returns the plan-cell box, the actual
    /// hub↔box interface (the abutment — narrower than the box when it overhangs), and the chosen flip; or
    /// <c>null</c> when no clear placement exists (a directed signal the caller falls back on).</summary>
    private static (CellRect Box, BoxAbutment Abutment, bool Flip)? SeatOverhang(
        IReadOnlyList<(int Start, int Len)> runs, int edgeLen, Demand d, WoolFill fill, BoxEdge edge,
        CellRect hubRect, IReadOnlyList<Box> seated, int grantedWidthCells, int separationCells, BoxEdge? guardFront, ComposeRng rng)
    {
        var mouth = Opposite(edge);
        var probeRect = edge is BoxEdge.Top or BoxEdge.Bottom ? new CellRect(0, 0, d.Along, d.Depth) : new CellRect(0, 0, d.Depth, d.Along);
        var placements = new List<(CellRect Box, bool Flip)>();
        foreach (var flip in new[] { false, true })
        {
            if (BoxFiller.EntryOn(new Box("probe", BoxKind.Wool, probeRect, 0), mouth, grantedWidthCells, fill.Family, flip,
                    fill.Placement, fill.WoolAtEnd, fill.AttachmentWidth) is not { } e)
                continue;
            // the box's along-start (seat) values for which the entry [seat+e0, +eLen] lands within a run; the box
            // must abut the hub, never overlap a seated box, and keep the seat gap from any seated spawn/wool
            foreach (var (rs, rl) in runs)
                for (var seat = rs - e.Start; seat <= rs + rl - e.Start - e.Len; seat++)
                {
                    var box = NeighbourRect(edge, seat, d.Depth, d.Along, hubRect);
                    if (BoxPartition.SharedEdge(hubRect, box) is not null
                        && !seated.Any(b => Overlap(b.Rect, box))
                        && !seated.Any(b => b.Kind is BoxKind.Spawn or BoxKind.Wool && TooClose(b.Rect, box, separationCells)))
                        placements.Add((box, flip));
                }
        }
        if (placements.Count == 0) return null;

        // no-frontline front guard, overhang side: only placements buffered behind the hub front face
        // (≥ FrontGuard.BufferCells back) are kept, so the overhang bends back instead of spiking toward — or
        // sitting flush with — the front, where it would extend the face into one long flat frontier. When no
        // buffered placement exists (a tight hub) the dock falls to the compact I, which the full-mouth guard
        // seats off the front; sampling within the surviving placements keeps variety.
        if (guardFront is { } gf)
        {
            var tier = placements.Where(p => FrontGuard.Backness(p.Box, gf, hubRect) >= FrontGuard.BufferCells).ToList();
            if (tier.Count == 0) return null;
            placements = tier;
        }

        var (chosen, chosenFlip) = placements[rng.NextInt(0, placements.Count)];
        return (chosen, BoxPartition.SharedEdge(hubRect, chosen)!, chosenFlip);
    }
}
