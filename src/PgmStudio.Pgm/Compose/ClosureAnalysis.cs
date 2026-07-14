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

    /// <summary>True when any closure hole borders a fanned image of one of the given pieces — used to keep
    /// wool plateaus off every hole ring (a wool-ringed hole would read as a second wool approach, WL8).</summary>
    public static bool AnyHoleRingedBy(PlanModel plan, IReadOnlySet<string> pieceIds) =>
        Analyze(plan, pieceIds).AnyRinged;

    private static (IReadOnlyList<int> Sizes, bool AnyRinged) Analyze(PlanModel plan, IReadOnlySet<string>? ringIds)
    {
        var order = Symmetry.Order(plan.Globals.Symmetry);
        var axes = Symmetry.OrbitAxes(plan.Globals.Symmetry);

        // fan every solid (pieces + zones) in CELL coordinates — cell rects fan exactly (integer corners)
        var solids = new List<(int X1, int Z1, int X2, int Z2, bool Ring)>();
        void Add(int[] rect, bool ring)
        {
            for (var k = 0; k < order; k++)
            {
                var (x1, z1, x2, z2) = ComposeGeometry.FanImage(
                    rect[0], rect[1], rect[0] + rect[2], rect[1] + rect[3], axes, k);
                solids.Add(((int)Math.Round(x1), (int)Math.Round(z1), (int)Math.Round(x2), (int)Math.Round(z2), ring));
            }
        }
        // A buffer marks EMPTY space — it must not rasterize as solid, or it would fill in (and erase) the very
        // rotation hole it documents, dropping the hole from the measurement.
        foreach (var p in plan.Pieces)
        {
            if (PlanRoles.IsAnnotation(p.Role)) continue;
            Add(p.Rect, ringIds?.Contains(p.Id) == true);
        }
        foreach (var z in plan.Zones) Add(z.Rect, false);
        if (solids.Count == 0) return ([], false);

        // rasterize with a one-cell void margin so the outside flood reaches around everything
        int minX = solids.Min(s => s.X1) - 1, minZ = solids.Min(s => s.Z1) - 1;
        int maxX = solids.Max(s => s.X2) + 1, maxZ = solids.Max(s => s.Z2) + 1;
        int w = maxX - minX, h = maxZ - minZ;
        var solid = new bool[w, h];
        var ringSolid = new bool[w, h];
        foreach (var s in solids)
            for (var x = s.X1; x < s.X2; x++)
                for (var z = s.Z1; z < s.Z2; z++)
                {
                    solid[x - minX, z - minZ] = true;
                    if (s.Ring) ringSolid[x - minX, z - minZ] = true;
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

        // remaining void cells are enclosed — group into 4-connected components; a component is "ringed"
        // when any of its cells borders a ring solid
        var seen = new bool[w, h];
        var sizes = new List<int>();
        var anyRinged = false;
        for (var x = 0; x < w; x++)
            for (var z = 0; z < h; z++)
            {
                if (solid[x, z] || outside[x, z] || seen[x, z]) continue;
                var size = 0;
                var ringed = false;
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
                            if (ringSolid[nx, nz]) ringed = true;
                            continue;
                        }
                        if (outside[nx, nz] || seen[nx, nz]) continue;
                        seen[nx, nz] = true;
                        q.Enqueue((nx, nz));
                    }
                }
                sizes.Add(size);
                anyRinged |= ringed;
            }
        return (sizes.OrderByDescending(s => s).ToList(), anyRinged);
    }
}
