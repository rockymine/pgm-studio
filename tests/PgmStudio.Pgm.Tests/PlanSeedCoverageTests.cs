using PgmStudio.Geom;
using PgmStudio.Pgm.Plan;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// Layout-coverage invariant across the whole seed corpus: every piece a plan declares must be fully covered
/// by the compiled layout's shapes at that piece's surface. A piece resolves to a block rectangle at a single
/// plateau height, so every block cell inside it has to fall within some shape polygon carrying the matching
/// base height — a shape dropped by the union (a disjoint same-surface patch, say) leaves a terrain hole this
/// test detects before the rasterizer or the export gate does.
/// </summary>
public sealed class PlanSeedCoverageTests
{
    [Test]
    public async Task Every_pieces_footprint_is_covered_by_a_shape_at_its_surface()
    {
        var seeds = Directory.EnumerateFiles(PlanTestSupport.SeedDir(), "*.plan.json")
            .OrderBy(p => p, StringComparer.Ordinal).ToList();
        await Assert.That(seeds.Count).IsGreaterThan(0);

        var failures = new List<string>();
        foreach (var path in seeds)
        {
            var seed = Path.GetFileName(path);
            var plan = PlanModel.Parse(File.ReadAllText(path))!;
            var derived = PlanDerived.Build(plan);
            var (layout, _) = PlanCompiler.Compile(plan);
            var shapes = layout.Layout!.Shapes;

            foreach (var piece in derived.Pieces)
            {
                var atSurface = shapes.Where(s => s.BaseHeight == piece.Surface).ToList();
                // Sample the block-cell centres of the piece footprint (X.5, Z.5 — off the integer edge lines
                // where the rectilinear shape boundaries lie) and require one same-surface shape to contain each.
                for (double x = piece.Rect.MinX + 0.5; x < piece.Rect.MaxX; x += 1)
                    for (double z = piece.Rect.MinZ + 0.5; z < piece.Rect.MaxZ; z += 1)
                        if (!atSurface.Any(s => Polygon.PointInRing(x, z, s.Vertices!)))
                        {
                            failures.Add($"{seed}: piece '{piece.Id}' cell ({x},{z}) at surface {piece.Surface} uncovered");
                            goto nextPiece;   // one report per piece is enough
                        }
                nextPiece: ;
            }
        }

        await Assert.That(failures).IsEmpty();
    }
}
