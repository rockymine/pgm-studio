using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

// Turning requests into positions — one integer per neighbour, the seat, under whichever of the three dock
// rules its style selects.

/// <summary>What a full-mouth dock produced: the placed <see cref="Box"/> rect and the
/// <see cref="Abutment"/> it abuts the hub over, and the <see cref="Request"/> <b>as it ended up</b> (a wool whose
/// full mouth found no run is demoted to the compact <c>I</c>, so this may differ from the one passed in).
/// <c>null</c> from <c>SeatFullMouth</c> means no legal seat at all.</summary>
internal sealed record FullMouthDock(CellRect Box, BoxAbutment Abutment, NeighbourRequest Request);

public static class UnitSeating
{
    /// <summary>Build the hub's body once and read what it offers: the box the filler re-emits, its per-edge
    /// <b>free runs</b> in box-local along-coords, and which box edge faces the axis. One offer per free run, so
    /// a bay in the body simply yields no run over its stretch — which is how a caller sizing a neighbour learns
    /// there is one. Emitting is a pure function of the form, the walls and the arms, so the three readers of
    /// this body (the request sizing, the seating, the filler) all see the same one without a draw between them.
    /// <c>Holes</c> is the body's enclosed void, the cells inside its box no piece covers and the outside cannot
    /// reach. A null box means the form does not fit the rect at all.</summary>
    internal static (Box? Box, IReadOnlyDictionary<BoxEdge, IReadOnlyList<(int Start, int Len)>> Runs, BoxEdge Front,
        IReadOnlySet<(int X, int Z)> Holes)
        Emit(CompoundRead form, CellRect hubRect, Frame frame, int laneWidthCells,
             RingWalls? walls, IReadOnlyList<(int Start, int Width)>? arms)
    {
        var frontEdge = SeatGeometry.SideEdge(frame, UnitSide.Front);
        var empty = (IReadOnlyDictionary<BoxEdge, IReadOnlyList<(int Start, int Len)>>)
            new Dictionary<BoxEdge, IReadOnlyList<(int Start, int Len)>>();
        // orient the form so its open feet face the unused front (SP: the frontline's side) and its solid edges
        // cover the demanded back/laterals — a vertical flip when the front is the box's top edge (every z-frame);
        // symmetric forms (Rectangle, Ring) are unaffected, so this is safe to apply uniformly
        var flipV = frontEdge == BoxEdge.Top;
        var hubBox = new Box("hub", BoxKind.Hub, hubRect, hubRect.Width * hubRect.Height, form, flipV,
            HubWalls: walls, HubArms: arms, HubCorridorCells: laneWidthCells);
        if (HubBoxEmitter.Fill(hubBox, form, hubBox.HubCorridor, flipV: flipV, ringWalls: walls,
                armLayout: arms) is not { } hub)
            return (null, empty, frontEdge, new HashSet<(int X, int Z)>());

        var runs = hub.Offers.GroupBy(o => o.Edge).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<(int Start, int Len)>)g.Select(o => (o.Interval.Start, o.Interval.LengthCells)).ToList());
        return (hubBox, runs, frontEdge, Enclosed(hubRect, hub.Pieces));
    }

    /// <summary>The cells inside <paramref name="box"/> that none of <paramref name="pieces"/> covers and that no
    /// empty path reaches from the box's border — a ring's hole, where a bay open to an edge is not one.</summary>
    private static HashSet<(int X, int Z)> Enclosed(CellRect box, IReadOnlyList<GrownPiece> pieces)
    {
        var land = new HashSet<(int X, int Z)>();
        foreach (var piece in pieces)
            for (var x = piece.Rect.X; x < piece.Rect.X + piece.Rect.Width; x++)
                for (var z = piece.Rect.Z; z < piece.Rect.Z + piece.Rect.Height; z++)
                    land.Add((x, z));
        var empty = new HashSet<(int X, int Z)>();
        for (var x = box.X; x < box.X + box.Width; x++)
            for (var z = box.Z; z < box.Z + box.Height; z++)
                if (!land.Contains((x, z))) empty.Add((x, z));
        var reached = new HashSet<(int X, int Z)>();
        var queue = new Queue<(int X, int Z)>(empty.Where(c => c.X == box.X || c.Z == box.Z
            || c.X == box.X + box.Width - 1 || c.Z == box.Z + box.Height - 1));
        foreach (var c in queue) reached.Add(c);
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            foreach (var n in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
                if (empty.Contains(n) && reached.Add(n)) queue.Enqueue(n);
        }
        empty.ExceptWith(reached);
        return empty;
    }

    /// <summary>The free runs on the edge a neighbour docking at the <b>front</b> would meet, which is what the
    /// frontline's face has to span. Empty when the form does not fit — the caller then sizes as if the edge were
    /// solid, which is what the rectangle it falls back to offers anyway.</summary>
    internal static IReadOnlyList<(int Start, int Len)> FrontRuns(
        CompoundRead form, CellRect hubRect, Frame frame, int laneWidthCells,
        RingWalls? walls, IReadOnlyList<(int Start, int Width)>? arms)
    {
        var (box, runs, front, _) = Emit(form, hubRect, frame, laneWidthCells, walls, arms);
        return box is not null && runs.TryGetValue(front, out var onFront) ? onFront : [];
    }

    /// <summary>Seat every request on <paramref name="form"/>'s real free-edge intervals, seated on the hub
    /// <paramref name="hubRect"/>. Builds the body once (<see cref="HubBoxEmitter"/>) — the same body the filler
    /// re-emits, so both read the same runs — and reads its per-edge free runs off the emitted offers (the
    /// offerable surface, §4). Returns the hub box (carrying <paramref name="form"/> for the filler) plus the
    /// seated neighbour boxes and their hub joints, or <c>null</c> when the box is too small for the form or a
    /// request finds no free run to dock (the directed signal the caller answers by falling back / resampling).</summary>
    internal static (List<Box> Boxes, List<BoxJoint> Joints)? Seat(
        CompoundRead form, CellRect hubRect, Frame frame, int laneWidthCells, int woolLaneCells, int seatGapCells,
        int cell, IReadOnlyList<NeighbourRequest> requests, ComposeRng rng,
        RingWalls? walls = null, IReadOnlyList<(int Start, int Width)>? arms = null)
    {
        var (emitted, runsByEdge, frontEdge, holes) = Emit(form, hubRect, frame, laneWidthCells, walls, arms);
        if (emitted is not { } hubBox) return null;   // too small
        int boxW = hubRect.Width, boxH = hubRect.Height;

        var boxes = new List<Box> { hubBox };
        var joints = new List<BoxJoint>();

        // the seat-step separation law: no spawn/wool neighbour may seat within the gap (the map lane width — w2 =
        // 10 blocks, w3 = 15 on wide boards) of another. Each already-seated spawn/wool projects onto the edge
        // being seated as a forbidden along-interval, so SeatInRuns samples a legal position directly — one pass
        // covering same-edge abut and adjacent-edge corner meetings alike. The frontline keeps no such gap (its
        // wool clearance is a build-zone rule, not this one).
        List<(int Start, int Len)> Blocked(BoxEdge edge, int depth) => boxes
            .Where(b => b.Kind is BoxKind.Spawn or BoxKind.Wool)
            .Select(b => SeatGeometry.ProjectOntoEdge(edge, hubRect, depth, b.Rect, seatGapCells))
            .Where(iv => iv is not null).Select(iv => iv!.Value).ToList();

        // record a seated neighbour: its box, and the joint granting it its corridor width. One place, so the
        // three dock styles differ only in how they FOUND the rect — never in what they record.
        void Seated(NeighbourRequest nb, CellRect rect, BoxAbutment abutment, int grantedWidthCells)
        {
            boxes.Add(new Box(nb.Id, nb.Kind, rect, nb.Along * nb.Depth, Wool: nb.Wool));
            joints.Add(SeatGeometry.HubJointFrom("hub", nb.Id, abutment, grantedWidthCells));
        }

        foreach (var initial in requests)
        {
            var request = initial;
            var edge = SeatGeometry.SideEdge(frame, request.Side);
            var edgeLen = edge is BoxEdge.Top or BoxEdge.Bottom ? boxW : boxH;
            if (!runsByEdge.TryGetValue(edge, out var runs)) return null;      // the form leaves this edge empty
            var grantedWidthCells = request.Kind == BoxKind.Wool ? woolLaneCells : laneWidthCells;   // each reads its own band corridor
            var style = UnitRequests.StyleOf(request);

            if (style is DockStyle.Overhang && request.Wool is { } rich)
            {
                if (SeatOverhang(runs, edgeLen, request, rich, edge, hubRect, boxes, grantedWidthCells, seatGapCells, rng) is { } placed)
                {
                    Seated(request with { Wool = rich with { Flip = placed.Flip } }, placed.Box, placed.Abutment, grantedWidthCells);
                    continue;
                }
                // no clear overhang placement on this hub (crowded / narrow): demote to the compact I and
                // re-dispatch as a full mouth. The demotion IS the fallback ladder — stated, not fallen through.
                request = UnitRequests.Compact(request, grantedWidthCells, cell);
                style = DockStyle.FullMouth;
            }

            if (style is DockStyle.ContactPatch)
            {
                if (SeatFront(runs, edgeLen, request, edge, hubRect, boxes, laneWidthCells, seatGapCells, rng) is not { } placed) return null;
                Seated(request, placed.Box, placed.Abutment, grantedWidthCells);
                continue;
            }

            var inLine = request.Kind == BoxKind.Spawn ? SpawnCentre(request, edge, edgeLen, frame, hubRect, frontEdge, holes) : null;
            if (SeatFullMouth(runs, edgeLen, request, edge, hubRect, Blocked, seatGapCells, grantedWidthCells, cell, inLine, rng)
                is not { } dock)
            {
                // nothing is dropped: a unit keeps every objective it planned, and a request it cannot seat is a
                // too-small signal the caller answers by falling back to the rectangle or redrawing the attempt
                return null;
            }
            Seated(dock.Request, dock.Box, dock.Abutment, grantedWidthCells);   // dock.Request — a full mouth may have demoted it
        }
        return (boxes, joints);
    }

    /// <summary>
    /// Seat a neighbour by <b>full mouth</b>: its whole along-extent must lie inside one of the hub's free
    /// <paramref name="runs"/>. The shape-agnostic rule — it assumes nothing about where the shape's entries
    /// are, so it serves the spawn, the plain <c>I</c> wools and the dual-entry staples alike (an overhang
    /// would strand a staple's second entry off the hub).
    ///
    /// <para>A wool whose mouth no run holds is demoted once to the compact <c>I</c> and retried; the request
    /// that comes back on <see cref="FullMouthDock.Request"/> is the one the caller must build the box from.
    /// A spawn with an <paramref name="inLine"/> centre seats within a cell of it (<see cref="InLine"/>).</para>
    ///
    /// <para><paramref name="blocked"/> is the caller's projection of the already-seated spawn/wool boxes onto
    /// an edge — passed as a delegate because it closes over the boxes seated so far, which grows as the loop
    /// runs.</para>
    /// </summary>
    internal static FullMouthDock? SeatFullMouth(
        IReadOnlyList<(int Start, int Len)> runs, int edgeLen, NeighbourRequest requested, BoxEdge edge, CellRect hubRect,
        Func<BoxEdge, int, List<(int Start, int Len)>> blocked, int seatGapCells, int grantedWidthCells,
        int cell, double? inLine, ComposeRng rng)
    {
        var request = requested;
        var seatGap = request.Kind is BoxKind.Spawn or BoxKind.Wool ? seatGapCells : 0;
        List<(int Start, int Len)> blk = seatGap > 0 ? blocked(edge, request.Depth) : [];
        int? seat;
        if (request.Toward is not null && inLine is { } end)
        {
            // as near the end as the seated neighbours allow, and never past the edge's middle
            seat = null;
            for (var slack = 1; seat is null && slack <= Math.Max(1, (edgeLen - request.Along) / 2); slack++)
                seat = SeatInRuns(InLine(runs, request.Along, end, slack), blk, edgeLen, request.Along, UnitTuning.CornerClearanceCells, seatGap, rng);
        }
        else
        {
            if (inLine is { } centre) runs = InLine(runs, request.Along, centre);
            seat = SeatInRuns(runs, blk, edgeLen, request.Along, UnitTuning.CornerClearanceCells, seatGap, rng);
        }
        if (seat is null && request.Kind == BoxKind.Wool)   // a staple's full mouth found no run — the compact I will
        {
            request = UnitRequests.Compact(request, grantedWidthCells, cell);
            blk = blocked(edge, request.Depth);
            seat = SeatInRuns(runs, blk, edgeLen, request.Along, UnitTuning.CornerClearanceCells, seatGap, rng);
        }
        if (seat is not { } s) return null;
        return new FullMouthDock(
            SeatGeometry.NeighbourRect(edge, s, request.Depth, request.Along, hubRect), new BoxAbutment(edge, s, request.Along), request);
    }

    /// <summary>An edge's free <paramref name="runs"/> cut to the seats whose centre stands within
    /// <paramref name="slack"/> cells of <paramref name="centre"/>, for a dock <paramref name="along"/> wide. A spawn seated in line with the hub's
    /// hole faces it squarely and walks about as far to a wool on either side of it; one behind the hole stands
    /// nearer the wool at the back, and one ahead of it walks straight out onto the frontline.</summary>
    public static IReadOnlyList<(int Start, int Len)> InLine(
        IReadOnlyList<(int Start, int Len)> runs, int along, double centre, int slack = 1)
    {
        var lo = (int)Math.Ceiling(centre - along / 2.0 - slack);
        var hi = (int)Math.Floor(centre - along / 2.0 + slack) + along;
        return runs
            .Select(r => (Start: Math.Max(r.Start, lo), End: Math.Min(r.Start + r.Len, hi)))
            .Where(r => r.End > r.Start)
            .Select(r => (r.Start, r.End - r.Start))
            .ToList();
    }

    /// <summary>Where along its <paramref name="edge"/> a spawn centres, or null where it is free to seat anywhere.
    /// A spawn on a lateral edge stands in line with the hub's hole, or the edge's middle without one; a back spawn
    /// with a <see cref="NeighbourRequest.Toward"/> side stands at the end of the back edge that side meets.</summary>
    private static double? SpawnCentre(
        NeighbourRequest spawn, BoxEdge edge, int edgeLen, Frame frame, CellRect hub, BoxEdge frontEdge,
        IReadOnlySet<(int X, int Z)> holes)
    {
        if (spawn.Toward is { } toward)
            return SeatGeometry.SideEdge(frame, toward) is BoxEdge.Top or BoxEdge.Left
                ? spawn.Along / 2.0
                : edgeLen - spawn.Along / 2.0;
        var lateral = edge != frontEdge && edge != SeatGeometry.Opposite(frontEdge);
        return lateral ? HoleCentre(edge, hub, holes) ?? edgeLen / 2.0 : null;
    }

    /// <summary>The centre of the hub's enclosed hole along <paramref name="edge"/>, in that edge's own
    /// coordinates, or null for a body without one.</summary>
    private static double? HoleCentre(BoxEdge edge, CellRect hub, IReadOnlySet<(int X, int Z)> holes)
    {
        if (holes.Count == 0) return null;
        var alongX = edge is BoxEdge.Top or BoxEdge.Bottom;
        var span = holes.Select(c => alongX ? c.X - hub.X : c.Z - hub.Z).ToList();
        return (span.Min() + span.Max() + 1) / 2.0;
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
    internal static int? SeatInRuns(
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
    /// least <c>cw</c> contiguous cells of one free run — which admits a face narrower than the edge
    /// (seated anywhere along it) and one wider than the edge (overhanging either end) by at most
    /// <see cref="UnitTuning.FaceOverhangMaxCells"/> across both ends together.
    ///
    /// <para>Returns the plan-cell box and the <b>real</b> hub↔frontline interface, clipped to the abutment and
    /// so narrower than the box whenever it overhangs — the filler reads the offer off this, not off the face
    /// width. A face that overhangs must keep the neighbour separation gap from every seated spawn/wool: past
    /// the hub's corner there is no hub cell bridging the meeting, so a frontline corner and a wool corner would
    /// meet as a bare diagonal pinch, which the corner law forbids. (A full-width face meets those neighbours at
    /// the hub's own corner, which the hub fills — that is why the pinned face never needed this.) <c>null</c>
    /// when no position gives a patch — the directed signal the caller answers by falling back.</para>
    /// </summary>
    internal static (CellRect Box, BoxAbutment Abutment)? SeatFront(
        IReadOnlyList<(int Start, int Len)> runs, int edgeLen, NeighbourRequest request, BoxEdge edge, CellRect hubRect,
        IReadOnlyList<Box> seated, int laneWidthCells, int seatGapCells, ComposeRng rng)
    {
        var placements = new List<(int Seat, CellRect Box, BoxAbutment Abutment)>();
        // the face may reach past the hub's corners, but only by what UnitTuning states: a seat is bounded by
        // the overhang budget rather than by how much of the face still touches. Without the bound every seat
        // that keeps one lane of contact is legal, a shifted face picks uniformly among them, and the far ones
        // outnumber the near — which is a face hanging off the hub's end rather than a face slid along it.
        var overhangBudget = UnitTuning.FaceOverhangMaxCells;
        for (var seat = -overhangBudget; seat <= edgeLen - request.Along + overhangBudget; seat++)
        {
            int lo = seat, hi = seat + request.Along;
            if (Math.Max(0, -lo) + Math.Max(0, hi - edgeLen) > overhangBudget) continue;
            if (!Docks(runs, lo, hi, laneWidthCells)) continue;
            if (PinchesAtEnd(runs, seat, seat + request.Along)) continue;
            var box = SeatGeometry.NeighbourRect(edge, seat, request.Depth, request.Along, hubRect);
            var overhangs = seat < 0 || seat + request.Along > edgeLen;
            if (seated.Any(b => b.Kind is BoxKind.Spawn or BoxKind.Wool
                    && (overhangs ? SeatGeometry.TooClose(b.Rect, box, seatGapCells) : SeatGeometry.Overlap(b.Rect, box)))) continue;
            if (BoxPartition.SharedEdge(hubRect, box) is { } abutment) placements.Add((seat, box, abutment));
        }
        if (placements.Count == 0) return null;

        // (see PinchesAtEnd for the end-alignment law the loop above applies)

        // A bay-fronted hub — a G, U or L, whose body leaves a gap in its own front edge — is meant to be
        // CLOSED by the frontline: a face spanning the bay rests on a shoulder each side and turns the bay
        // into a declared hole, which is the rotation device CT8 names. A face seated to one side of it leaves
        // the bay open as a notch and puts the whole crossing off the hub's flank. So where the edge has a bay
        // and any placement spans it, those are the placements; the sample is over them.
        var sealing = placements.Where(p => Seals(runs, p.Seat, p.Seat + request.Along)).ToList();
        if (sealing.Count > 0) placements = sealing;

        // Centred by default. Sliding the face along the edge is the funnel, and it costs the mid band slack
        // (Composer.FrontHullSlackCells) — so it is a sampled exception, not what every seat does. Without this
        // even a full-width face would land off-centre, since every overhanging position is legal too.
        if (!rng.NextBool(UnitTuning.ShiftedFaceChance))
        {
            var centre = (edgeLen - request.Along) / 2.0;
            var best = placements.OrderBy(p => Math.Abs(p.Seat - centre)).ThenBy(p => p.Seat).First();
            return (best.Box, best.Abutment);
        }
        var pick = placements[rng.NextInt(0, placements.Count)];
        return (pick.Box, pick.Abutment);
    }

    /// <summary>Whether a face covering edge-local <c>[lo, hi)</c> closes every bay in the hub's own front
    /// edge — each gap between two of its free <paramref name="runs"/>, which is edge the hub's body leaves
    /// without terrain. True for an edge with no bay at all, so a solid front never prefers one seat over
    /// another on this account.</summary>
    internal static bool Seals(IReadOnlyList<(int Start, int Len)> runs, int lo, int hi)
    {
        var ordered = runs.OrderBy(r => r.Start).ToList();
        for (var index = 1; index < ordered.Count; index++)
            if (ordered[index].Start > ordered[index - 1].Start + ordered[index - 1].Len
                && (lo > ordered[index - 1].Start + ordered[index - 1].Len || hi < ordered[index].Start))
                return false;
        return true;
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
    internal static bool Docks(IReadOnlyList<(int Start, int Len)> runs, int lo, int hi, int laneWidthCells)
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
    internal static bool PinchesAtEnd(IReadOnlyList<(int Start, int Len)> runs, int lo, int hi)
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
    internal static (CellRect Box, BoxAbutment Abutment, bool Flip)? SeatOverhang(
        IReadOnlyList<(int Start, int Len)> runs, int edgeLen, NeighbourRequest request, WoolFill fill, BoxEdge edge,
        CellRect hubRect, IReadOnlyList<Box> seated, int grantedWidthCells, int separationCells, ComposeRng rng)
    {
        var mouth = SeatGeometry.Opposite(edge);
        var probeRect = edge is BoxEdge.Top or BoxEdge.Bottom ? new CellRect(0, 0, request.Along, request.Depth) : new CellRect(0, 0, request.Depth, request.Along);
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
                    var box = SeatGeometry.NeighbourRect(edge, seat, request.Depth, request.Along, hubRect);
                    if (BoxPartition.SharedEdge(hubRect, box) is not null
                        && !seated.Any(b => SeatGeometry.Overlap(b.Rect, box))
                        && !seated.Any(b => b.Kind is BoxKind.Spawn or BoxKind.Wool && SeatGeometry.TooClose(b.Rect, box, separationCells)))
                        placements.Add((box, flip));
                }
        }
        if (placements.Count == 0) return null;

        var (chosen, chosenFlip) = placements[rng.NextInt(0, placements.Count)];
        return (chosen, BoxPartition.SharedEdge(hubRect, chosen)!, chosenFlip);
    }
}
