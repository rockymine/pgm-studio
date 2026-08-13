using PgmStudio.Geom;
using PgmStudio.Pgm.Derive;

namespace PgmStudio.Pgm.Plan;

/// <summary>
/// The compile step that gives a plan's negative space a name (docs/tools/plan.md). Terrain is
/// stated by the pieces that generate it, so the ground a body encircles and no piece covers — a ring hub's
/// hole — is void by omission, and nothing in the document says so. This writes it down: every enclosed void
/// becomes a <see cref="PlanRoles.Buffer"/> piece, the role that already means reserved empty space.
///
/// <para>An author may draw those buffers and need not: the step runs on every compile and adds whatever is
/// missing, so a hole is declared whether or not anyone declared it. It is <b>idempotent</b> — a plan that
/// already covers its voids is returned unchanged, and a buffer deleted from a generated plan comes back on
/// the next compile.</para>
/// </summary>
public static class PlanVoids
{
    /// <summary>The plan with a buffer piece over every enclosed void it does not already cover. Returns the
    /// same instance when nothing is missing, so a caller can persist the result unconditionally and write
    /// only when the plan actually gained something.</summary>
    public static PlanModel Declare(PlanModel plan)
    {
        var missing = Undeclared(plan);
        if (missing.Count == 0) return plan;

        // A fresh document rather than the caller's — a compile must not mutate the plan handed to it.
        var declared = PlanModel.Parse(plan.ToJson())!;
        var next = 1;
        var taken = plan.Pieces.Select(p => p.Id).ToHashSet();
        foreach (var (rect, mirrors) in missing)
        {
            string id;
            do { id = $"void-{next++}"; } while (!taken.Add(id));
            declared.Pieces.Add(new PlanPiece
            {
                Id = id,
                Role = PlanRoles.Buffer,
                Rect = rect,
                Mirrors = mirrors ? null : false,
            });
        }
        return declared;
    }

    /// <summary>The enclosed voids no buffer covers yet, as cell rects paired with the mirror flag of the
    /// body enclosing them — what <see cref="Declare"/> adds. Empty when the plan declares everything.</summary>
    public static IReadOnlyList<(CellRect Rect, bool Mirrors)> Undeclared(PlanModel plan)
    {
        var graph = ContactGraph.Build(plan);
        var cell = graph.Cell;

        // The union outline the compiler emits is per component and per surface, so that is the grain a void
        // is enclosed at. Everything that generates terrain counts as ground against it, whatever component
        // or surface it belongs to: a void a stepped plateau seats a piece into is a plateau, not a void.
        var ground = graph.Pieces.Select(p => Cells(p.Rect, cell)).ToList();
        var declared = plan.Pieces
            .Where(p => p.Role == PlanRoles.Buffer)
            .Select(p => Bounds(p.Rect)).ToList();

        var found = new List<(CellRect, bool)>();
        foreach (var component in graph.Components)
        {
            var pieces = component.Select(id => graph.Piece(id)!.Value).ToList();
            foreach (var group in pieces.GroupBy(p => p.Surface).OrderBy(g => g.Key))
            {
                var body = group.Select(p => Cells(p.Rect, cell)).ToList();
                foreach (var (minX, minZ, maxX, maxZ) in RectilinearUnion.EnclosedVoids(body, [.. ground, .. declared]))
                    found.Add((CellRect.FromBounds(minX, minZ, maxX, maxZ), group.First().Mirrors));
            }
        }
        return found;
    }

    private static (int MinX, int MinZ, int MaxX, int MaxZ) Cells(BlockRect rect, int cell)
        => (rect.MinX / cell, rect.MinZ / cell, rect.MaxX / cell, rect.MaxZ / cell);

    private static (int MinX, int MinZ, int MaxX, int MaxZ) Bounds(CellRect rect)
        => (rect.X, rect.Z, rect.X + rect.Width, rect.Z + rect.Height);
}
