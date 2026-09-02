namespace PgmStudio.Geom;

/// <summary>
/// Rectilinear cell-set substrate — the shared 4-connected raster primitives (neighbour iteration, flood fill,
/// connected components, enclosed-void detection, reflex-corner counting, fold detection, bounding box, min run
/// width) that the shape classifier, the lane read, and the board deriver all read cell topology through.
/// Pure integer-grid geometry over <c>(x, z)</c> cells; references nothing above <c>Geom</c>.
///
/// <para>Topology, not travel. How far a player walks and what route they take is <see cref="Walk"/>'s, which
/// prices a diagonal, a climb and water; what is asked here is whether cells connect and what shape they
/// make.</para>
/// </summary>
public static class Cells
{
    /// <summary>Every cell a straight walk from <paramref name="from"/> to <paramref name="to"/> passes
    /// through, both ends included — the standard integer line, so a line a caller asked for is the line
    /// actually walked rather than an interpolation free to skip a cell. Every read that follows a line over
    /// the ground takes this one: a transect's polyline, a route's own centreline.</summary>
    public static IEnumerable<(int X, int Z)> Line((int X, int Z) from, (int X, int Z) to)
    {
        int x = from.X, z = from.Z;
        int dx = Math.Abs(to.X - from.X), sx = from.X < to.X ? 1 : -1;
        int dz = -Math.Abs(to.Z - from.Z), sz = from.Z < to.Z ? 1 : -1;
        var err = dx + dz;
        while (true)
        {
            yield return (x, z);
            if (x == to.X && z == to.Z) yield break;
            var doubled = 2 * err;
            if (doubled >= dz) { err += dz; x += sx; }
            if (doubled <= dx) { err += dx; z += sz; }
        }
    }

    /// <summary>The four orthogonal neighbours of <paramref name="c"/> (4-connectivity).</summary>
    public static IEnumerable<(int, int)> N4((int, int) c)
    {
        yield return (c.Item1 + 1, c.Item2); yield return (c.Item1 - 1, c.Item2);
        yield return (c.Item1, c.Item2 + 1); yield return (c.Item1, c.Item2 - 1);
    }

    /// <summary>The extent of a non-empty cell set. Both bounding corners are occupied cells, so the
    /// returned rect's <see cref="CellRect.Width"/>/<see cref="CellRect.Height"/> are the cell counts the set
    /// spans and its <see cref="CellRect.MaxX"/>/<see cref="CellRect.MaxZ"/> are one past the far cells.</summary>
    public static CellRect BoundingBox(IReadOnlyCollection<(int, int)> cells)
    {
        int mnx = int.MaxValue, mnz = int.MaxValue, mxx = int.MinValue, mxz = int.MinValue;
        foreach (var (x, z) in cells)
        {
            if (x < mnx) mnx = x; if (x > mxx) mxx = x;
            if (z < mnz) mnz = z; if (z > mxz) mxz = z;
        }
        return CellRect.FromInclusive(mnx, mnz, mxx, mxz);
    }

    /// <summary>The 4-connected component of <paramref name="within"/> reachable from <paramref name="seeds"/>
    /// (seeds not in <paramref name="within"/> are ignored). Returns the component's cells.</summary>
    public static HashSet<(int, int)> Flood(IEnumerable<(int, int)> seeds, IReadOnlySet<(int, int)> within)
    {
        var comp = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>();
        foreach (var s in seeds) if (within.Contains(s) && comp.Add(s)) q.Enqueue(s);
        while (q.Count > 0) { var c = q.Dequeue(); foreach (var n in N4(c)) if (within.Contains(n) && comp.Add(n)) q.Enqueue(n); }
        return comp;
    }

    /// <summary>Whether a route from <paramref name="from"/> to <paramref name="to"/> may pass on
    /// <b>either</b> side of <paramref name="hole"/>, or must commit to one — the topological test, not a cut
    /// count. A ray is cast from the hole to the set's boundary on one side and the ends are asked whether they
    /// still connect; anything reaching past that ray has to cross it, so surviving the cut means a route exists
    /// on the <em>other</em> side. Repeated with the ray on the opposite side. Returns how many of the two
    /// survive: <b>2</b> is a genuine choice, <b>1</b> is one committed way, <b>0</b> is unreachable.
    ///
    /// <para>Counting the connected components of a minimum cut is <b>not</b> this test and answers wrong in
    /// both directions — an uncuttable cell inside a single barrier splits it into fragments with no second
    /// route, and a real second way is missed whenever the cheapest cut lies elsewhere.</para>
    ///
    /// <para>The ray runs along <paramref name="hole"/>'s median row (its median column when
    /// <paramref name="horizontal"/> is false), so a hole wider than it is tall is cut the short way. An
    /// endpoint standing on a ray is kept, since removing it would answer "unreachable" about the cut rather
    /// than about the board.</para></summary>
    public static int WaysRound((int X, int Z) from, (int X, int Z) to,
        IReadOnlyCollection<(int X, int Z)> hole, IReadOnlySet<(int X, int Z)> within, bool horizontal = true)
    {
        if (hole.Count == 0 || !within.Contains(from) || !within.Contains(to)) return 0;
        var bounds = BoundingBox([.. within, .. hole]);
        var ways = 0;
        foreach (var forward in (bool[])[true, false])
        {
            var cut = RayCut(hole, bounds, horizontal, forward);
            cut.Remove(from);
            cut.Remove(to);
            var open = new HashSet<(int X, int Z)>(within);
            open.ExceptWith(cut);
            if (Flood([from], open).Contains(to)) ways++;
        }
        return ways;
    }

    /// <summary>The cells of the ray <see cref="WaysRound"/> cuts with — from the hole's far edge along its
    /// median line out to <paramref name="bounds"/>. Exposed so a render can draw the cut it was asked about
    /// rather than a reader having to trust it.</summary>
    public static HashSet<(int X, int Z)> RayCut(
        IReadOnlyCollection<(int X, int Z)> hole, CellRect bounds, bool horizontal, bool forward)
    {
        var cut = new HashSet<(int X, int Z)>();
        if (hole.Count == 0) return cut;

        if (horizontal)
        {
            var row = Median([.. hole.Select(c => c.Z)]);
            var onRow = hole.Where(c => c.Z == row).Select(c => c.X).ToList();
            if (onRow.Count == 0) return cut;
            if (forward) for (var x = onRow.Max() + 1; x < bounds.MaxX; x++) cut.Add((x, row));
            else for (var x = onRow.Min() - 1; x >= bounds.X; x--) cut.Add((x, row));
        }
        else
        {
            var column = Median([.. hole.Select(c => c.X)]);
            var onColumn = hole.Where(c => c.X == column).Select(c => c.Z).ToList();
            if (onColumn.Count == 0) return cut;
            if (forward) for (var z = onColumn.Max() + 1; z < bounds.MaxZ; z++) cut.Add((column, z));
            else for (var z = onColumn.Min() - 1; z >= bounds.Z; z--) cut.Add((column, z));
        }
        return cut;

        static int Median(List<int> values) { values.Sort(); return values[values.Count / 2]; }
    }

    /// <summary>How deep inside <paramref name="region"/> each of its cells lies — the 4-connected distance
    /// to the nearest cell that is <b>not</b> in the region, counting anything outside
    /// <paramref name="bounds"/> as not in it. A cell on the border answers 1, one step in answers 2.
    ///
    /// <para>This is the measure of how much room a route has either side of it. A walk that only minimises
    /// length hugs every border it passes, because the border is the short way round — and on a board whose
    /// borders are void, the short way is the exposed one. Charging for missing clearance is what pulls a
    /// route onto the middle of the ground it is crossing.</para></summary>
    public static Dictionary<(int X, int Z), int> Clearance(IReadOnlySet<(int X, int Z)> region, CellRect bounds)
    {
        var depth = new Dictionary<(int X, int Z), int>();
        var queue = new Queue<(int X, int Z)>();

        // Seed with every region cell that touches a non-region cell (or the bound), at depth 1.
        foreach (var cell in region)
        {
            var edge = false;
            foreach (var n in N4(cell))
                if (!region.Contains(n)) { edge = true; break; }
            if (!edge)
                edge = cell.X <= bounds.X || cell.X >= bounds.MaxX - 1 || cell.Z <= bounds.Z || cell.Z >= bounds.MaxZ - 1;
            if (edge) { depth[cell] = 1; queue.Enqueue(cell); }
        }

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            var next = depth[cell] + 1;
            foreach (var n in N4(cell))
                if (region.Contains(n) && depth.TryAdd(n, next)) queue.Enqueue(n);
        }
        return depth;
    }

    /// <summary>Snaps <paramref name="cell"/> onto <paramref name="within"/>: the cell itself when it is
    /// already a member, otherwise the nearest member found by searching outward ring by ring, up to
    /// <paramref name="radius"/> rings, using the <b>square</b> (Chebyshev) ring — every cell at
    /// <c>max(|dx|, |dz|) == ring</c> — rather than the diamond (Manhattan) ring, so a reachable diagonal
    /// neighbour is found at the same ring a cardinal one is. Ties within a ring break deterministically by
    /// increasing Z then increasing X. Returns null when nothing in <paramref name="within"/> falls within
    /// <paramref name="radius"/> rings.</summary>
    public static (int X, int Z)? SnapToWalkable((int X, int Z) cell, IReadOnlySet<(int X, int Z)> within, int radius)
    {
        if (within.Contains(cell)) return cell;
        for (var ring = 1; ring <= radius; ring++)
            for (var dz = -ring; dz <= ring; dz++)
                for (var dx = -ring; dx <= ring; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                    var candidate = (cell.X + dx, cell.Z + dz);
                    if (within.Contains(candidate)) return candidate;
                }
        return null;
    }

    /// <summary>Number of 4-connected components of a cell set.</summary>
    public static int Components(IReadOnlySet<(int, int)> cells)
    {
        var seen = new HashSet<(int, int)>();
        int n = 0;
        foreach (var c in cells)
        {
            if (!seen.Add(c)) continue;
            n++;
            var q = new Queue<(int, int)>(); q.Enqueue(c);
            while (q.Count > 0) { var d = q.Dequeue(); foreach (var m in N4(d)) if (cells.Contains(m) && seen.Add(m)) q.Enqueue(m); }
        }
        return n;
    }

    /// <summary>True when the footprint encloses a background cell unreachable from outside its bounding box
    /// (a hole). A one-cell margin around the bounding box seeds the outside flood.</summary>
    public static bool HasEnclosedVoid(IReadOnlySet<(int, int)> fill)
    {
        int mnx = fill.Min(c => c.Item1) - 1, mxx = fill.Max(c => c.Item1) + 1, mnz = fill.Min(c => c.Item2) - 1, mxz = fill.Max(c => c.Item2) + 1;
        var outside = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>(); q.Enqueue((mnx, mnz)); outside.Add((mnx, mnz));
        while (q.Count > 0) { var c = q.Dequeue(); foreach (var n in N4(c)) if (n.Item1 >= mnx && n.Item1 <= mxx && n.Item2 >= mnz && n.Item2 <= mxz && !fill.Contains(n) && outside.Add(n)) q.Enqueue(n); }
        for (var x = mnx; x <= mxx; x++) for (var z = mnz; z <= mxz; z++) if (!fill.Contains((x, z)) && !outside.Contains((x, z))) return true;
        return false;
    }

    /// <summary>The enclosed void of a footprint: the background cells trapped inside it — unreachable from
    /// outside the bounding box without crossing a filled cell. Empty when the footprint has no hole. The
    /// cell-level companion to <see cref="HasEnclosedVoid"/> (which is <c>EnclosedVoid(fill).Count &gt; 0</c>).</summary>
    public static HashSet<(int, int)> EnclosedVoid(IReadOnlySet<(int, int)> fill)
    {
        var trapped = new HashSet<(int, int)>();
        if (fill.Count == 0) return trapped;
        int mnx = fill.Min(c => c.Item1) - 1, mxx = fill.Max(c => c.Item1) + 1;
        int mnz = fill.Min(c => c.Item2) - 1, mxz = fill.Max(c => c.Item2) + 1;
        var outside = new HashSet<(int, int)>();
        var q = new Queue<(int, int)>(); q.Enqueue((mnx, mnz)); outside.Add((mnx, mnz));
        while (q.Count > 0) { var c = q.Dequeue(); foreach (var n in N4(c)) if (n.Item1 >= mnx && n.Item1 <= mxx && n.Item2 >= mnz && n.Item2 <= mxz && !fill.Contains(n) && outside.Add(n)) q.Enqueue(n); }
        for (var x = mnx; x <= mxx; x++) for (var z = mnz; z <= mxz; z++) if (!fill.Contains((x, z)) && !outside.Contains((x, z))) trapped.Add((x, z));
        return trapped;
    }

    /// <summary>The number of reflex (concave) corners of a cell set's outline — the bend count, read
    /// width-independently (a lane and the same lane widened uniformly turn the same number of times).</summary>
    public static int ReflexCorners(IReadOnlySet<(int, int)> cells)
    {
        int mnx = cells.Min(c => c.Item1), mxx = cells.Max(c => c.Item1) + 1, mnz = cells.Min(c => c.Item2), mxz = cells.Max(c => c.Item2) + 1, r = 0;
        for (var x = mnx; x <= mxx; x++)
            for (var z = mnz; z <= mxz; z++)
            {
                int cnt = 0;
                if (cells.Contains((x, z))) cnt++; if (cells.Contains((x - 1, z))) cnt++;
                if (cells.Contains((x, z - 1))) cnt++; if (cells.Contains((x - 1, z - 1))) cnt++;
                if (cnt == 3) r++;
            }
        return r;
    }

    /// <summary>True when the cell set doubles back on itself: some grid row or column meets it in two or more
    /// runs (the set is not orthogonally convex). A fold wrapping a concavity always puts two separate runs on
    /// the lines crossing that concavity; a staircase never does. A property of the cells alone, width-
    /// independent — unlike a bounding-box-edge test, the verdict cannot flip merely because added neighbour
    /// cells extend the bounding box.</summary>
    public static bool HasFold(IReadOnlySet<(int, int)> cells)
    {
        if (cells.Count == 0) return false;
        var b = BoundingBox(cells);
        for (var z = b.Z; z < b.MaxZ; z++)
        {
            int runs = 0; var inRun = false;
            for (var x = b.X; x < b.MaxX; x++)
            {
                if (cells.Contains((x, z))) { if (!inRun) { runs++; inRun = true; } }
                else inRun = false;
            }
            if (runs >= 2) return true;
        }
        for (var x = b.X; x < b.MaxX; x++)
        {
            int runs = 0; var inRun = false;
            for (var z = b.Z; z < b.MaxZ; z++)
            {
                if (cells.Contains((x, z))) { if (!inRun) { runs++; inRun = true; } }
                else inRun = false;
            }
            if (runs >= 2) return true;
        }
        return false;
    }

    /// <summary>True when the cell set has a <b>diagonal pinch</b>: some 2×2 window holds exactly the two
    /// diagonal cells filled and the other two empty, so the filled pair meets only at a point — there is no
    /// 4-connected step between them and the two opposite diagonals are void. A ¾-solid inside corner (three
    /// of the four filled) is <b>not</b> a pinch: a third cell bridges the diagonal into one walkable mass.
    /// The mass-level corner law — read on the composed mask, not on a pairwise bounding-box touch, so a bare
    /// point contact that a third piece bridges reads clean. Width-independent.</summary>
    public static bool HasDiagonalPinch(IReadOnlySet<(int, int)> cells)
    {
        if (cells.Count == 0) return false;
        var bb = BoundingBox(cells);
        for (var x = bb.X; x < bb.MaxX - 1; x++)
            for (var z = bb.Z; z < bb.MaxZ - 1; z++)
            {
                bool a = cells.Contains((x, z)), b = cells.Contains((x + 1, z));
                bool c = cells.Contains((x, z + 1)), d = cells.Contains((x + 1, z + 1));
                if ((a && d && !b && !c) || (b && c && !a && !d)) return true;
            }
        return false;
    }

    /// <summary>The minimum corridor cross-section (cells) at <paramref name="seeds"/>, measured as the smaller
    /// of each seed's horizontal and vertical run within <paramref name="cells"/>, clamped to [2, 6] (the lane
    /// width convention). Runs never fall below 1, so the clamp's lower bound is the effective floor — a
    /// caller that must see a <b>sub-minimum</b> corridor (one narrower than any lane the vocabulary has) reads
    /// <see cref="MinRunWidthRaw"/> instead.</summary>
    public static int MinRunWidth(IReadOnlySet<(int, int)> cells, IEnumerable<(int, int)> seeds) =>
        Math.Clamp(MinRunWidthRaw(cells, seeds), 2, 6);

    /// <summary>The same measurement <b>unclamped</b> — the true minimum cross-section, which may be 1. The
    /// width convention's floor is a reading convention, not a fact of the geometry: a 1-cell corridor exists
    /// and something checking whether the emitters could have produced it has to be able to see it.</summary>
    public static int MinRunWidthRaw(IReadOnlySet<(int, int)> cells, IEnumerable<(int, int)> seeds)
    {
        int Hrun((int, int) c) { int n = 1; for (var x = c.Item1 - 1; cells.Contains((x, c.Item2)); x--) n++; for (var x = c.Item1 + 1; cells.Contains((x, c.Item2)); x++) n++; return n; }
        int Vrun((int, int) c) { int n = 1; for (var z = c.Item2 - 1; cells.Contains((c.Item1, z)); z--) n++; for (var z = c.Item2 + 1; cells.Contains((c.Item1, z)); z++) n++; return n; }
        return seeds.Min(c => Math.Min(Hrun(c), Vrun(c)));
    }
}
