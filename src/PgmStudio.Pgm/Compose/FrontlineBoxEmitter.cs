using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The filled frontline box: its terrain pieces (front-labeled, <b>no terminal, no marker</b>), the
/// <see cref="FaceOffers"/> the mid consumes, the <see cref="SpineEdge"/> that docks the hub and the
/// <see cref="FaceEdge"/> toward the axis, and the <see cref="CompoundRead"/> form it was built as.</summary>
public sealed record EmittedFrontline(
    IReadOnlyList<GrownPiece> Pieces, IReadOnlyList<EdgeOffer> FaceOffers,
    BoxEdge SpineEdge, BoxEdge FaceEdge, CompoundRead Form);

/// <summary>
/// The frontline binding over <see cref="BodyEmitter"/> — the <b>join box kind</b>
/// (docs/contracts/map-generation.md §5.5). Terminal-free like the hub, finished by the <b>Front designation</b>:
/// one edge marked the <c>face</c> (where the fanned images meet the mid), no room. It is a <b>join, not a
/// placement</b> — its only interfaces are the <b>spine</b> (which docks the hub, consuming the hub's front-edge
/// offer) and the <b>face</b> (which it offers to the mid); the sides are inert. <b>Rotation is fixed by the
/// designation</b>: the spine docks the hub and the arm-tips are the face toward the axis, so the emitter works
/// in one canonical frame — spine <see cref="BoxEdge.Top"/>, face <see cref="BoxEdge.Bottom"/> — and the composer
/// orients the placed box onto the hub (G63-C).
///
/// <para>The <b>face offer</b> is <see cref="Designation.Frontline"/>'s half of the offer mechanism, and its
/// <see cref="OfferGrouping"/> is the mid's contract (§1.14): <b>joint</b> — one mid consumer spans all tips
/// (FR6's wide face; the inter-tip recess is simply not offered, staying CT9's hole) — vs <b>several</b> — one
/// mid per tip (the twin / double frontline, K derived runs). Form menu (<see cref="Forms"/>): <b>Bar</b> (the
/// wide face, FR6), and the branch family <b>single</b> (<c>SpineArms(1)</c>, FR3/FR4) and <b>twin</b>
/// (<c>SpineArms(2)</c>, CT8) — lifting the grower's <c>FrontForm { None, Single, Wide, Twin }</c> into data.</para>
/// </summary>
public static class FrontlineBoxEmitter
{
    /// <summary>The frontline's authored form menu as data (§5.5): the wide <b>Bar</b> and the branch family at
    /// one arm (single strand) and two (twin). The holed forms (P, two-U-on-I) are authored additions beyond the
    /// grower's <c>FrontForm</c> and land next.</summary>
    public static readonly IReadOnlyList<CompoundRead> Forms =
    [
        new(Compound.Rectangle),      // Bar — the wide face (FR6 / FrontForm.Wide)
        new(Compound.SpineArms, 1),   // single strand (FR3/FR4 / FrontForm.Single)
        new(Compound.SpineArms, 2),   // twin (CT8 / FrontForm.Twin)
    ];

    /// <summary>Fill a frontline <see cref="Box"/> (plan cells) as <paramref name="form"/> at strand width
    /// <paramref name="cw"/>, terminal-free, with the spine docking <paramref name="spineMouth"/> (the box edge
    /// facing the hub) and the face on the opposite edge toward the axis. The body is built spine-up and oriented
    /// onto the mouth (<see cref="BodyOrient"/>), so the composer places the box against any hub edge —
    /// <see cref="BoxEdge.Top"/> is the canonical frame (spine top, face bottom). Publishes the <b>face offer</b>
    /// over the arm-tip runs on the face edge, grouped <paramref name="faceGrouping"/> (joint — one shared group
    /// the mid spans; several — one group per tip). <paramref name="faceWidth"/> is the width the mid reads,
    /// defaulting to each run's own class. Pieces carry the frontline <paramref name="box"/> label; no room, no
    /// marker. <c>null</c> when the box is too small for the form; throws for a form off the frontline menu.</summary>
    public static EmittedFrontline? Fill(
        Box box, CompoundRead form, int cw, OfferGrouping faceGrouping, BoxEdge spineMouth = BoxEdge.Top, int? faceWidth = null)
    {
        int boxW = box.Rect[2], boxH = box.Rect[3];
        // build spine-up in the spine-length × reach frame (transposed when the spine docks a lateral edge), then
        // orient onto the mouth — the twin of a spawn/wool box emitting mouth-up and orienting via MouthOrient
        var lateral = spineMouth is BoxEdge.Left or BoxEdge.Right;
        var (spineLen, reach) = lateral ? (boxH, boxW) : (boxW, boxH);
        var built = BuildBody(form, spineLen, reach, cw);
        if (built is null) return null;
        var body = BodyOrient.To(built, spineMouth, spineLen, reach);

        var boxRef = new BoxRef(box.Id, BoxKind.Frontline);
        var pieces = new List<GrownPiece>(body.Pieces.Count);
        var n = 1;
        foreach (var (r, slot) in body.Pieces)
            pieces.Add(new GrownPiece($"{box.Id}-t{n++}", [box.Rect[0] + r[0], box.Rect[1] + r[1], r[2], r[3]],
                PlanRoles.Piece, slot, boxRef));

        // the face is the arm-tip edge opposite the spine; only it is offered — the sides are inert and the spine
        // is the consumer side (it lands on the hub's offer).
        var face = Opposite(spineMouth);
        var faceIface = BoxInterfaces.Of(body, boxW, boxH).Single(e => e.Edge == face);
        var joint = faceGrouping == OfferGrouping.Joint;
        var offers = BoxInterfaces.Runs(faceIface.Intervals)
            .Select((run, k) => new EdgeOffer(face, run, faceWidth ?? BodyEdges.WidthClass(run.LengthCells),
                faceGrouping, joint ? $"{box.Id}-face" : $"{box.Id}-face-{k}"))
            .ToList();

        return new EmittedFrontline(pieces, offers, spineMouth, face, form);
    }

    /// <summary>The box edge opposite <paramref name="e"/> — the face sits opposite the spine.</summary>
    private static BoxEdge Opposite(BoxEdge e) => e switch
    {
        BoxEdge.Top => BoxEdge.Bottom, BoxEdge.Bottom => BoxEdge.Top,
        BoxEdge.Left => BoxEdge.Right, _ => BoxEdge.Left,
    };

    // build the body of `form` sized to fill the box, spine along the top; null when the box is too small.
    private static ShapeBody? BuildBody(CompoundRead form, int w, int h, int cw)
    {
        try
        {
            return form.Form switch
            {
                Compound.Rectangle => BodyEmitter.Rectangle(w, h),                                              // Bar — the wide face
                Compound.SpineArms when form.Arms == 1 => BodyEmitter.SpineArms(cw, [(w - cw) / 2], w, h - cw), // single — a centred strand
                Compound.SpineArms when form.Arms == 2 => BodyEmitter.SpineArms(cw, [0, w - cw], w, h - cw),    // twin — a strand at each end
                _ => throw new ComposeException($"the frontline menu is Bar / single / twin, not {form.Form} (arms {form.Arms})."),
            };
        }
        catch (ArgumentException) { return null; }
    }
}
