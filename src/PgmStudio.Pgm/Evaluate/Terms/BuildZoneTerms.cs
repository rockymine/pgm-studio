using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Evaluate.Terms;

/// <summary>BZ6: the mid build band never comes within two cells of a wool-carrying piece, across every orbit
/// image pair — the band must not interface (or nearly interface) a wool. Fans the band and every wool piece to
/// all images and measures the gap; an overlap or a &lt;2-cell separation on both axes fires. (Re-checked here
/// rather than trusting the carve, since an isolation cut can move a wool after the band is laid.)</summary>
public sealed class BandWoolClearance : ILayoutTerm
{
    private const double MinClearanceCells = 2.0;

    public string Id => "band-wool-clearance";
    public string RuleId => "BZ6";
    public TermKind Kind => TermKind.Hard;

    public TermScore Measure(EvalContext ctx)
    {
        var plan = ctx.Plan;
        var band = plan.Zones.FirstOrDefault(z => z.Id == "mid-band");
        if (band is null) return TermScores.Clean(this);

        var woolPieces = plan.Placements.Wools.Select(w => w.Piece).ToHashSet();
        var order = Symmetry.Order(plan.Globals.Symmetry);
        var axes = Symmetry.OrbitAxes(plan.Globals.Symmetry);

        var bandImages = Enumerable.Range(0, order)
            .Select(k => ComposeGeometry.FanImage(
                band.Rect[0], band.Rect[1], band.Rect[0] + band.Rect[2], band.Rect[1] + band.Rect[3], axes, k))
            .ToList();

        foreach (var piece in plan.Pieces.Where(p => woolPieces.Contains(p.Id)))
            for (var k = 0; k < order; k++)
            {
                var (px1, pz1, px2, pz2) = ComposeGeometry.FanImage(
                    piece.Rect[0], piece.Rect[1], piece.Rect[0] + piece.Rect[2], piece.Rect[1] + piece.Rect[3], axes, k);
                foreach (var b in bandImages)
                {
                    var ix = Math.Min(px2, b.X2) - Math.Max(px1, b.X1);
                    var iz = Math.Min(pz2, b.Z2) - Math.Max(pz1, b.Z1);
                    if (ix > -MinClearanceCells + 1e-9 && iz > -MinClearanceCells + 1e-9)
                        return TermScores.Violated(this,
                            $"mid band comes within {MinClearanceCells} cells of wool piece '{piece.Id}'",
                            [piece.Id, band.Id]);
                }
            }

        return TermScores.Clean(this);
    }
}
