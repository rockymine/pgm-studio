using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The <b>no-frontline front guard</b> — the law that on a frontline-less unit no spawn/wool may end flush
/// with (or past) the hub's front face: a flush neighbour extends the face into one long flat frontier (hub
/// front + neighbour front reading as a single straight edge), which map design forbids. With a frontline the
/// guard does not apply — the front is occupied and juts forward, so no continuous line can form.
///
/// <para>This is a <b>post-pass</b> over the allocator's seating, deliberately separate from the sampling: it
/// draws <b>nothing</b> from the RNG, so a seat already off the front — and every untouched unit — re-seats
/// bit-identically. <see cref="TeamUnitAllocator"/> applies it in three layers: the overhang tier filter reads
/// <see cref="BufferCells"/>/<see cref="Backness"/> directly; each full-mouth lateral seat gets the immediate
/// <see cref="ShiftOffFront"/> slide; and the seats no slide could save go through <see cref="Resolve"/>, the
/// deterministic search that shifts, relocates, or drops them once every neighbour is known.</para>
/// </summary>
public static class FrontGuard
{
    /// <summary>The buffer kept behind the hub's front face (in cells, 5 blocks each) by every lateral
    /// spawn/wool on a no-frontline unit. An overhanging wool only takes placements at least this far back
    /// (else it falls to the compact <c>I</c>), and a full-mouth seat that lands closer is slid back.</summary>
    public const int BufferCells = 1;

    /// <summary>A full-mouth seat the immediate slide could not bring off the front — the input to
    /// <see cref="Resolve"/>: the box's <see cref="Id"/>/<see cref="Kind"/> and demanded footprint
    /// (<see cref="Depth"/> outward × <see cref="Along"/> along-edge), the hub <see cref="Edge"/> it docks
    /// (length <see cref="EdgeLen"/>, free runs <see cref="Runs"/>), and the separation <see cref="Gap"/> it
    /// seated with.</summary>
    public sealed record FlushSeat(string Id, BoxKind Kind, int Depth, int Along,
        BoxEdge Edge, int EdgeLen, IReadOnlyList<(int Start, int Len)> Runs, int Gap);

    /// <summary>How far the <paramref name="box"/>'s front-most extent sits <b>behind</b> the hub's front face
    /// (<paramref name="front"/> the axis-facing edge), in cells: positive is behind (good — bent away from the
    /// axis), zero flush with the face, negative overreaching toward the axis. The overhang tier filter keeps
    /// only placements at <see cref="BufferCells"/> or more.</summary>
    public static int Backness(int[] box, BoxEdge front, int[] hub) => front switch
    {
        BoxEdge.Top => box[1] - hub[1],
        BoxEdge.Bottom => hub[1] + hub[3] - (box[1] + box[3]),
        BoxEdge.Left => box[0] - hub[0],
        _ => hub[0] + hub[2] - (box[0] + box[2]),                      // Right
    };

    /// <summary>Slide a full-mouth lateral <paramref name="seat"/> <b>off the hub's front face</b>: when its
    /// front-side offset (toward the box-corner the front face meets — along-coord 0 when
    /// <paramref name="frontAtLow"/>, else the <paramref name="edgeLen"/> end) is under
    /// <see cref="BufferCells"/>, walk backward along the edge to the nearest position that is buffered,
    /// lies wholly within a free run, and keeps the <paramref name="gap"/> from every <paramref name="blocked"/>
    /// interval. Returns the original seat when it is already buffered, the nearest buffered position when it is
    /// not, or <c>null</c> when no clear backward position exists (the caller records a <see cref="FlushSeat"/>
    /// for <see cref="Resolve"/>).</summary>
    public static int? ShiftOffFront(
        IReadOnlyList<(int Start, int Len)> runs, List<(int Start, int Len)> blocked,
        int edgeLen, int along, int gap, int seat, bool frontAtLow)
    {
        int Offset(int s) => frontAtLow ? s : edgeLen - (s + along);
        if (Offset(seat) >= BufferCells) return seat;
        var dir = frontAtLow ? 1 : -1;
        for (var cand = seat + dir; cand >= 0 && cand + along <= edgeLen; cand += dir)
        {
            if (Offset(cand) < BufferCells) continue;
            if (!runs.Any(r => r.Start <= cand && cand + along <= r.Start + r.Len)) continue;
            if (blocked.Any(o => o.Start - gap < cand + along && o.Start + o.Len + gap > cand)) continue;
            return cand;
        }
        return null;
    }

    /// <summary>Resolve the seats still flush with the front — a small deterministic search, no draws. Per
    /// seat, in order: retry the slide (the full neighbour set is now known, and an earlier drop may have been
    /// the very blocker), relocate to the mirror lateral or the back edge (the backmost lawful seat there),
    /// retry both at the reduced wool-lane gap, and only then drop the wool so long as another remains (the
    /// same law that drops a wool over seating inside the separation gap) — the unit keeps its objectives, one
    /// fewer, rather than a flat front. Which seat resolves first changes what the rest can do, so every
    /// processing order is tried (the seats are at most four), as is every lawful <b>spawn back-edge slide</b>
    /// (a spawn mid-back on a narrow hub can gap-block both lateral corners at once; a corner spawn frees a
    /// lateral — the unchanged position comes first, so a unit the in-place search resolves keeps that
    /// outcome). The outcome with the fewest flush residues, then the most wools, wins. The returned
    /// <c>Residue</c> counts the seats still flush — the caller's directed "cannot host" signal (a
    /// non-rectangle form falls back to the rectangle; only the rectangle itself keeps the flush seat, the
    /// flagged residue of a truly saturated hub).</summary>
    public static (List<Box> Boxes, List<BoxJoint> Joints, int Residue) Resolve(
        List<Box> boxes, List<BoxJoint> joints, IReadOnlyList<FlushSeat> flushSeats,
        int[] hubRect, BoxEdge frontEdge, int w,
        IReadOnlyDictionary<BoxEdge, IReadOnlyList<(int Start, int Len)>> runsByEdge)
    {
        int boxW = hubRect[2], boxH = hubRect[3];
        var frontAtLow = frontEdge is BoxEdge.Top or BoxEdge.Left;

        List<(int Start, int Len)> BlockedFor(List<Box> bs, string selfId, BoxEdge e, int depth) => bs
            .Where(b => b.Kind is BoxKind.Spawn or BoxKind.Wool && b.Id != selfId)
            .Select(b => TeamUnitAllocator.ProjectOntoEdge(e, hubRect, depth, b.Rect, w))
            .Where(iv => iv is not null).Select(iv => iv!.Value).ToList();

        void MoveTo(List<Box> bs, List<BoxJoint> js, string id, BoxKind kind, int depth, int along, BoxEdge e, int seat)
        {
            var i = bs.FindIndex(b => b.Id == id);
            bs[i] = bs[i] with { Rect = TeamUnitAllocator.NeighbourRect(e, seat, depth, along, hubRect) };
            var ji = js.FindIndex(j => j.BoxB == id);
            js[ji] = TeamUnitAllocator.HubJoint("hub", id, e, seat, along,
                kind == BoxKind.Wool ? TeamUnitAllocator.WoolLaneCells : w);
        }

        (List<Box> B, List<BoxJoint> J, int Residue) ResolveOrder(
            IReadOnlyList<FlushSeat> order, List<Box> baseB, List<BoxJoint> baseJ)
        {
            var bs = new List<Box>(baseB);
            var js = new List<BoxJoint>(baseJ);
            var residue = 0;
            foreach (var f in order)
            {
                var self = bs.First(b => b.Id == f.Id);
                var cur = f.Edge is BoxEdge.Top or BoxEdge.Bottom ? self.Rect[0] - hubRect[0] : self.Rect[1] - hubRect[1];
                // gap tiers: the full separation gap first; a wool may fall to the wool-lane gap (2 cells,
                // 10 blocks — the very gap the narrower boards seat with, still no-touch) as the last tier
                // before a flush residue, trading separation ideal for the flush law
                var gaps = f.Kind == BoxKind.Wool && f.Gap > TeamUnitAllocator.WoolLaneCells
                    ? new[] { f.Gap, TeamUnitAllocator.WoolLaneCells } : new[] { f.Gap };
                var resolved = false;
                foreach (var gap in gaps)
                {
                    if (ShiftOffFront(f.Runs, BlockedFor(bs, f.Id, f.Edge, f.Depth), f.EdgeLen, f.Along, gap,
                            cur, frontAtLow) is { } moved)
                    {
                        MoveTo(bs, js, f.Id, f.Kind, f.Depth, f.Along, f.Edge, moved);
                        resolved = true;
                        break;
                    }
                    foreach (var target in new[] { TeamUnitAllocator.Opposite(f.Edge), TeamUnitAllocator.Opposite(frontEdge) })
                    {
                        if (target == f.Edge || target == frontEdge || !runsByEdge.TryGetValue(target, out var truns)) continue;
                        var tlen = target is BoxEdge.Top or BoxEdge.Bottom ? boxW : boxH;
                        var guard = target == TeamUnitAllocator.Opposite(frontEdge) ? 0 : BufferCells;   // the back edge is off-front by construction
                        if (BackmostSeat(truns, BlockedFor(bs, f.Id, target, f.Depth), tlen, f.Along, gap,
                                guard, frontAtLow) is { } c)
                        {
                            MoveTo(bs, js, f.Id, f.Kind, f.Depth, f.Along, target, c);
                            resolved = true;
                            break;
                        }
                    }
                    if (resolved) break;
                }
                if (resolved) continue;
                if (f.Kind == BoxKind.Wool && bs.Count(b => b.Kind == BoxKind.Wool) > 1)
                {
                    bs.RemoveAll(b => b.Id == f.Id);
                    js.RemoveAll(j => j.BoxA == f.Id || j.BoxB == f.Id);
                }
                else residue++;
            }
            return (bs, js, residue);
        }

        // the spawn-slide variants: nothing to slide unless every flush seat is a wool and the spawn docks the
        // back edge (a lateral spawn is itself front-guarded; the back edge is the one with slack)
        var spawnSlides = new List<int?> { null };
        var spawnBox = boxes.FirstOrDefault(b => b.Kind == BoxKind.Spawn);
        var backEdge = TeamUnitAllocator.Opposite(frontEdge);
        var (spAlong, spDepth) = (0, 0);
        if (spawnBox is not null
            && joints.FirstOrDefault(j => j.BoxB == spawnBox.Id)?.Interface.Edge == backEdge
            && flushSeats.All(f => f.Kind == BoxKind.Wool)
            && runsByEdge.TryGetValue(backEdge, out var backRuns))
        {
            var horizontal = backEdge is BoxEdge.Top or BoxEdge.Bottom;
            (spAlong, spDepth) = horizontal
                ? (spawnBox.Rect[2], spawnBox.Rect[3]) : (spawnBox.Rect[3], spawnBox.Rect[2]);
            var spSeat = horizontal ? spawnBox.Rect[0] - hubRect[0] : spawnBox.Rect[1] - hubRect[1];
            var backLen = horizontal ? boxW : boxH;
            var spBlocked = BlockedFor(boxes, spawnBox.Id, backEdge, spDepth);
            for (var cand = 0; cand + spAlong <= backLen; cand++)
            {
                if (cand == spSeat) continue;
                if (!backRuns.Any(r => r.Start <= cand && cand + spAlong <= r.Start + r.Len)) continue;
                if (spBlocked.Any(o => o.Start - w < cand + spAlong && o.Start + o.Len + w > cand)) continue;
                spawnSlides.Add(cand);
            }
        }

        var best = default((List<Box> B, List<BoxJoint> J, int Residue)?);
        var maxWools = boxes.Count(x => x.Kind == BoxKind.Wool);
        foreach (var slide in spawnSlides)
        {
            var baseB = new List<Box>(boxes);
            var baseJ = new List<BoxJoint>(joints);
            if (slide is { } sSeat) MoveTo(baseB, baseJ, spawnBox!.Id, BoxKind.Spawn, spDepth, spAlong, backEdge, sSeat);
            foreach (var order in Permutations(flushSeats))
            {
                var trial = ResolveOrder(order, baseB, baseJ);
                if (best is not { } b0
                    || trial.Residue < b0.Residue
                    || (trial.Residue == b0.Residue
                        && trial.B.Count(x => x.Kind == BoxKind.Wool) > b0.B.Count(x => x.Kind == BoxKind.Wool)))
                    best = trial;
                if (best is { Residue: 0 } b1 && b1.B.Count(x => x.Kind == BoxKind.Wool) == maxWools) break;
            }
            if (best is { Residue: 0 } b2 && b2.B.Count(x => x.Kind == BoxKind.Wool) == maxWools) break;
        }
        return best!.Value;
    }

    /// <summary>The lawful dock position on an edge <b>farthest from the front</b>, or <c>null</c>: the seat must
    /// lie wholly within a free run, keep the <paramref name="gap"/> from every <paramref name="blocked"/>
    /// interval, and keep <paramref name="guard"/> cells from the front-side end (<paramref name="frontAtLow"/>
    /// naming which end that is; 0 for the back edge, where no end faces the front). The relocation half of the
    /// guard reads it — a wool moving to the mirror lateral or the back edge lands as far off the front as that
    /// edge allows.</summary>
    private static int? BackmostSeat(
        IReadOnlyList<(int Start, int Len)> runs, List<(int Start, int Len)> blocked,
        int edgeLen, int along, int gap, int guard, bool frontAtLow)
    {
        var lo = frontAtLow ? guard : 0;
        var hi = edgeLen - along - (frontAtLow ? 0 : guard);
        for (var cand = frontAtLow ? hi : lo; cand >= lo && cand <= hi; cand += frontAtLow ? -1 : 1)
        {
            if (!runs.Any(r => r.Start <= cand && cand + along <= r.Start + r.Len)) continue;
            if (blocked.Any(o => o.Start - gap < cand + along && o.Start + o.Len + gap > cand)) continue;
            return cand;
        }
        return null;
    }

    /// <summary>Every processing order of <paramref name="items"/> — the flush-resolution search space (at most
    /// a handful of seats, so full enumeration stays tiny). The identity order comes first, keeping the outcome
    /// deterministic when several tie.</summary>
    private static IEnumerable<IReadOnlyList<T>> Permutations<T>(IReadOnlyList<T> items)
    {
        if (items.Count <= 1) { yield return items; yield break; }
        for (var i = 0; i < items.Count; i++)
        {
            var head = items[i];
            var rest = items.Where((_, j) => j != i).ToList();
            foreach (var p in Permutations(rest)) yield return new List<T> { head }.Concat(p).ToList();
        }
    }
}
