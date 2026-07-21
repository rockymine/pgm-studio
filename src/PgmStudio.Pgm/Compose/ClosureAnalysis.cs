using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// Closure-hole measurement (CT8): the fanned closure (terrain pieces ∪ build zones, all orbit images)
/// rasterized on the proxy cell grid, with the void flood-filled 4-connected from outside — every enclosed
/// void component is a <b>hole</b>, the corpus's rotation device (a loop around a hole gives routes between
/// lanes that don't retreat through a chokepoint). Also classifies a hole's <b>ring</b> (the solids bordering
/// it): a hole ringed by a wool plateau is the wool-two-approaches motif (WL8), which the composer does not
/// author. Pure analysis over a <see cref="PlanModel"/>.
///
/// <para>This is a deliberate <b>fast-path twin</b> of the enclosed-void classification in
/// <c>BoardDeriver</c> (<c>BoardStructure.Voids</c>): a narrow dense-grid flood kept separate because it runs
/// inside the composer's 60-attempt hunt loop, where re-deriving the whole board per attempt is wasteful. It
/// computes the same §1.7 "hole" concept over the same fanned closure — the two must stay in agreement; when
/// the board deriver's hole rules change, this twin changes with them (the same discipline the JS symmetry
/// twin follows). Fold it into a query over <c>BoardStructure</c> only if profiling shows the hunt loop can
/// absorb it.</para>
/// </summary>
public static class ClosureAnalysis
{
    /// <summary>The sizes (cells) of every enclosed void pocket in the plan's fanned closure, descending.</summary>
    public static IReadOnlyList<int> HoleSizes(PlanModel plan) => Analyze(plan, null).Sizes;

    /// <summary>True when any closure hole borders a fanned image of one of the given wool-room pieces <b>and
    /// is not that wool's own sealed courtyard</b> — the wool-two-approaches motif (WL8). A staple-class or
    /// donut wool deliberately encloses a bay/hole of its own: two legs (or a ring) plus the room, sealed by
    /// the one host edge it docks. Such a hole's ring reads as <b>at least two pieces of the wool's own box</b>
    /// (shared id prefix, the room id minus <c>-room</c>) with <b>at most one foreign piece</b> (the sealing
    /// host) — sanctioned. Any other wool-bordered hole — two or more foreign ring pieces, or a lone wool piece
    /// on the ring — is genuine terrain wrapping the wool, the second approach WL8 bans.</summary>
    public static bool AnyHoleRingedBy(PlanModel plan, IReadOnlySet<string> pieceIds) =>
        Analyze(plan, pieceIds).AnyRinged;

    private static (IReadOnlyList<int> Sizes, bool AnyRinged) Analyze(PlanModel plan, IReadOnlySet<string>? ringIds)
    {
        var order = Symmetry.Order(plan.Globals.Symmetry);
        var axes = Symmetry.OrbitAxes(plan.Globals.Symmetry);

        // fan every solid (pieces + zones) in CELL coordinates — cell rects fan exactly (integer corners)
        var solids = new List<(int X1, int Z1, int X2, int Z2, string? Id)>();
        void Add(int[] rect, string? id)
        {
            for (var k = 0; k < order; k++)
            {
                var (x1, z1, x2, z2) = ComposeGeometry.FanImage(
                    rect[0], rect[1], rect[0] + rect[2], rect[1] + rect[3], axes, k);
                solids.Add(((int)Math.Round(x1), (int)Math.Round(z1), (int)Math.Round(x2), (int)Math.Round(z2), id));
            }
        }
        // A buffer marks EMPTY space — it must not rasterize as solid, or it would fill in (and erase) the very
        // rotation hole it documents, dropping the hole from the measurement.
        foreach (var p in plan.Pieces)
        {
            if (PlanRoles.IsAnnotation(p.Role)) continue;
            Add(p.Rect, p.Id);
        }
        foreach (var z in plan.Zones) Add(z.Rect, null);
        if (solids.Count == 0) return ([], false);

        // rasterize with a one-cell void margin so the outside flood reaches around everything; each solid cell
        // remembers its piece id (zones are anonymous) so a hole's ring can be read per piece
        int minX = solids.Min(s => s.X1) - 1, minZ = solids.Min(s => s.Z1) - 1;
        int maxX = solids.Max(s => s.X2) + 1, maxZ = solids.Max(s => s.Z2) + 1;
        int w = maxX - minX, h = maxZ - minZ;
        var solid = new bool[w, h];
        var cellId = new string?[w, h];
        foreach (var s in solids)
            for (var x = s.X1; x < s.X2; x++)
                for (var z = s.Z1; z < s.Z2; z++)
                {
                    solid[x - minX, z - minZ] = true;
                    cellId[x - minX, z - minZ] ??= s.Id;
                }

        // flood the outside void from the margin
        var outside = new bool[w, h];
        var queue = new Queue<(int X, int Z)>();
        void Seed(int x, int z) { if (!solid[x, z] && !outside[x, z]) { outside[x, z] = true; queue.Enqueue((x, z)); } }
        for (var x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
        for (var z = 0; z < h; z++) { Seed(0, z); Seed(w - 1, z); }
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            foreach (var (nx, nz) in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
                if (nx >= 0 && nx < w && nz >= 0 && nz < h && !solid[nx, nz] && !outside[nx, nz])
                {
                    outside[nx, nz] = true;
                    queue.Enqueue((nx, nz));
                }
        }

        // remaining void cells are enclosed — group into 4-connected components, collecting each hole's ring
        // (the piece ids bordering it; an anonymous zone counts as one foreign entity — buildable terrain on
        // the ring is itself a route in)
        var seen = new bool[w, h];
        var sizes = new List<int>();
        var anyRinged = false;
        for (var x = 0; x < w; x++)
            for (var z = 0; z < h; z++)
            {
                if (solid[x, z] || outside[x, z] || seen[x, z]) continue;
                var size = 0;
                var ring = new HashSet<string>();
                var q = new Queue<(int X, int Z)>();
                seen[x, z] = true;
                q.Enqueue((x, z));
                while (q.Count > 0)
                {
                    var (cx, cz) = q.Dequeue();
                    size++;
                    foreach (var (nx, nz) in new[] { (cx + 1, cz), (cx - 1, cz), (cx, cz + 1), (cx, cz - 1) })
                    {
                        if (nx < 0 || nx >= w || nz < 0 || nz >= h) continue;
                        if (solid[nx, nz])
                        {
                            ring.Add(cellId[nx, nz] ?? "(zone)");
                            continue;
                        }
                        if (outside[nx, nz] || seen[nx, nz]) continue;
                        seen[nx, nz] = true;
                        q.Enqueue((nx, nz));
                    }
                }
                sizes.Add(size);

                // WL8, courtyard-aware: a hole bordering a wool room is that wool's own sealed bay only when
                // the ring reads as the shape's courtyard — at least two pieces of the wool's own box (its
                // legs/ring plus the room, shared id prefix) with at most ONE foreign piece (the sealing host
                // edge it docks). Anything else — two or more foreign pieces, or a lone wool piece on the
                // ring — is genuine terrain wrapping the wool, the second approach WL8 bans.
                if (ringIds is not null && !anyRinged)
                    foreach (var roomId in ring.Where(ringIds.Contains))
                    {
                        var box = roomId.EndsWith("-room", StringComparison.Ordinal) ? roomId[..^5] : roomId;
                        var own = ring.Count(id => id.StartsWith(box, StringComparison.Ordinal));
                        if (ring.Count - own >= 2 || own < 2)
                        {
                            anyRinged = true;
                            break;
                        }
                    }
            }
        return (sizes.OrderByDescending(s => s).ToList(), anyRinged);
    }
}
