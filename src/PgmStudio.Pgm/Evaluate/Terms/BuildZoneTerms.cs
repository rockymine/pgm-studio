using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
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
    public string RuleId => LayoutRules.MidZoneNearWool;
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

/// <summary>MD7: the mid build region is at least <see cref="WidthFloorBlocks"/> wide across the fronts for the
/// layout's size band. The distance is the shortfall over half the floor. A plan with no <c>mid-band</c> zone is
/// not read.</summary>
public sealed class ThinMiddle : ILayoutTerm
{
    /// <summary>The narrowest band the author accepts for a layout of <paramref name="band"/>, in blocks.</summary>
    public static int WidthFloorBlocks(string band) => SizeBands.Canonical(band) switch
    {
        SizeBands.Nano => 24,
        SizeBands.Micro => 32,
        SizeBands.Milli => 40,
        _ => 48,
    };

    public string Id => "thin-middle";
    public string RuleId => LayoutRules.ThinMiddle;
    public TermKind Kind => TermKind.Soft;

    public TermScore Measure(EvalContext ctx)
    {
        if (MidBand.Read(ctx.Plan) is not { } band) return TermScores.Clean(this);
        var floor = WidthFloorBlocks(SizeBands.Of(ctx.Plan.Globals.MaxPlayers));
        var distance = Math.Max(0, floor - band.Width) / (floor / 2.0);
        if (distance <= 0) return TermScores.Clean(this);
        return TermScores.Soft(this, distance, $"the mid build region is {band.Width:0} blocks wide, less than {floor} blocks",
            [band.Zone.Id], [Ev.Rect(EvidenceTags.Offender, band.Zone.Rect)]);
    }
}

/// <summary>MD8: the mid build region runs front to front at most <see cref="MaxLengthPerWidth"/> times its width.
/// The distance is how far the ratio runs over. A plan with no <c>mid-band</c> zone is not read.</summary>
public sealed class LongMiddle : ILayoutTerm
{
    /// <summary>A band longer than this many times its width reads as a corridor to walk rather than ground to
    /// fight over.</summary>
    public const double MaxLengthPerWidth = 2.0;

    public string Id => "long-middle";
    public string RuleId => LayoutRules.LongMiddle;
    public TermKind Kind => TermKind.Soft;

    public TermScore Measure(EvalContext ctx)
    {
        if (MidBand.Read(ctx.Plan) is not { } band) return TermScores.Clean(this);
        var distance = Math.Max(0, band.Length / band.Width - MaxLengthPerWidth);
        if (distance <= 0) return TermScores.Clean(this);
        return TermScores.Soft(this, distance,
            $"the mid build region is {band.Length:0} blocks long, {band.Length / band.Width:0.##} times its width, more "
            + $"than {MaxLengthPerWidth:0} times",
            [band.Zone.Id], [Ev.Rect(EvidenceTags.Offender, band.Zone.Rect)]);
    }
}

/// <summary>The <c>mid-band</c> zone of a composed layout, measured in blocks across the fronts and front to
/// front. Null where the plan has none, or one with no width.</summary>
internal sealed record MidBand(PlanZone Zone, double Width, double Length)
{
    public static MidBand? Read(PlanModel plan)
    {
        if (plan.Zones.FirstOrDefault(z => z.Id == "mid-band") is not { } zone) return null;
        var span = Frame.For(plan.Globals.Symmetry).FromRect(zone.Rect);
        double width = span.VSpan * plan.Globals.Cell, length = span.USpan * plan.Globals.Cell;
        return width <= 0 ? null : new MidBand(zone, width, length);
    }
}
