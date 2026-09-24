using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Vocabulary;

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
                band.Rect.X, band.Rect.Z, band.Rect.X + band.Rect.Width, band.Rect.Z + band.Rect.Height, axes, k))
            .ToList();

        foreach (var piece in plan.Pieces.Where(p => woolPieces.Contains(p.Id)))
            for (var k = 0; k < order; k++)
            {
                var (px1, pz1, px2, pz2) = ComposeGeometry.FanImage(
                    piece.Rect.X, piece.Rect.Z, piece.Rect.X + piece.Rect.Width, piece.Rect.Z + piece.Rect.Height, axes, k);
                foreach (var b in bandImages)
                {
                    var ix = Math.Min(px2, b.X2) - Math.Max(px1, b.X1);
                    var iz = Math.Min(pz2, b.Z2) - Math.Max(pz1, b.Z1);
                    if (ix > -MinClearanceCells + 1e-9 && iz > -MinClearanceCells + 1e-9)
                        return TermScores.Violated(this,
                            $"mid band comes within {MinClearanceCells} cells of wool piece '{piece.Id}'",
                            [piece.Id, band.Id],
                            [Ev.Rect(EvidenceTags.Offender, piece.Rect), Ev.Rect(EvidenceTags.Context, band.Rect)]);
                }
            }

        return TermScores.Clean(this);
    }
}

/// <summary>MD7: the crossing's build band is not a long, thin strip. Two readings of the band a composed board
/// carries: its width across the fronts against <see cref="WidthFloorBlocks"/> for the board's size band, and its
/// length front to front against <see cref="MaxLengthPerWidth"/> times that width. The distance is each shortfall
/// over half its band — the floor for the width, the ratio for the length — summed, so a band both thin and long
/// scores both. A plan with no <c>mid-band</c> zone is not read.</summary>
public sealed class ThinMiddle : ILayoutTerm
{
    /// <summary>A band longer than this many times its width reads as a corridor to walk rather than ground to
    /// fight over.</summary>
    public const double MaxLengthPerWidth = 2.0;

    /// <summary>The narrowest band the author accepts for a board of <paramref name="band"/>, in blocks.</summary>
    public static int WidthFloorBlocks(string band) => SizeBands.Canonical(band) switch
    {
        SizeBands.Nano => 24,
        SizeBands.Micro => 32,
        SizeBands.Milli => 40,
        _ => 48,
    };

    public string Id => "thin-middle";
    public string RuleId => "MD7";
    public TermKind Kind => TermKind.Soft;

    public TermScore Measure(EvalContext ctx)
    {
        var plan = ctx.Plan;
        if (plan.Zones.FirstOrDefault(z => z.Id == "mid-band") is not { } band) return TermScores.Clean(this);
        var span = Frame.For(plan.Globals.Symmetry).FromRect(band.Rect);
        double width = span.VSpan * plan.Globals.Cell, length = span.USpan * plan.Globals.Cell;
        if (width <= 0) return TermScores.Clean(this);

        var floor = WidthFloorBlocks(SizeBands.Of(plan.Globals.MaxPlayers));
        var thin = Math.Max(0, floor - width) / (floor / 2.0);
        var tooLong = Math.Max(0, length / width - MaxLengthPerWidth);
        var distance = thin + tooLong;
        if (distance <= 0) return TermScores.Clean(this);
        return TermScores.Soft(this, distance,
            $"mid band {width:0} blocks wide (floor {floor}) and {length:0} long ({length / width:0.##}× its width, at most {MaxLengthPerWidth:0})",
            [band.Id], [Ev.Rect(EvidenceTags.Offender, band.Rect)]);
    }
}
