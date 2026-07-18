using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>The filled hub box: its terrain pieces (slot- and box-labeled, <b>no terminal, no marker</b>), the
/// per-edge <see cref="EdgeOffer"/>s it publishes as the constraint source, and the <see cref="CompoundRead"/>
/// form it was built as (the emit-side record the body mirror verifies).</summary>
public sealed record EmittedHub(
    IReadOnlyList<GrownPiece> Pieces, IReadOnlyList<EdgeOffer> Offers, CompoundRead Form);

/// <summary>
/// The hub binding over <see cref="BodyEmitter"/> — the <b>constraint-source box kind</b>
/// (docs/contracts/map-generation.md §5.5). Unlike the wool/spawn boxes the hub is <b>terminal-free</b>: it is a
/// <see cref="ShapeBody"/> (a <see cref="Compound"/>) finished by the <b>hub designation</b> — per-edge
/// <c>interface</c> marks carrying widths, no room. It <b>emits first</b> and its edge widths set the fill menus
/// of the spawn/wool/frontline neighbours, published here as <see cref="EdgeOffer"/>s (the
/// <see cref="Designation.Hub"/> half of the offer mechanism): a consumed <see cref="EdgeOffer.WidthClass"/> is
/// the neighbour's corridor width. The composer decides which offer each neighbour takes and drives the per-edge
/// widths (G63-C); this emitter produces the body and the offers it sources.
///
/// <para>Form menu (<see cref="Forms"/>, authored — §5.5): <b>Rectangle · L · U · Ring · Double-hole</b>,
/// compact optionally-holed bodies, deliberately not Zig/Hook/the higher combs. Each is a <see cref="Compound"/>
/// sized to fill the box, so its outward walls touch the box edges and become offers; a body too small for its
/// form at the given <c>cw</c> is a <b>directed null</b>, not a throw. Pieces carry the hub <see cref="BoxRef"/>
/// and their structural slot, extending the label-preservation invariant to the hub.</para>
/// </summary>
public static class HubBoxEmitter
{
    /// <summary>The hub's authored form menu as data — the <see cref="Compound"/> bodies a hub may be (§5.5). A
    /// hub stays rectangle-ish, so the menu is the compact forms only: the solid Rectangle, the branch family at
    /// one arm (L) and two (U), the Ring, and the Double-hole.</summary>
    public static readonly IReadOnlyList<CompoundRead> Forms =
    [
        new(Compound.Rectangle),
        new(Compound.SpineArms, 1),   // L — a spine with one end arm
        new(Compound.SpineArms, 2),   // U — a spine with two end arms, the bay between
        new(Compound.Ring),           // one enclosed hole
        new(Compound.DoubleHole),     // a ring + a docked U — two holes
    ];

    /// <summary>Fill a hub <see cref="Box"/> (plan cells) as <paramref name="form"/> at wall/corridor width
    /// <paramref name="cw"/>, terminal-free, publishing one <see cref="EdgeOffer"/> per free run on each edge.
    /// <paramref name="edgeWidths"/> is the constraint the hub sources — the w2/w4/w6 rung each edge offers a
    /// neighbour; an edge left unset offers its own free width (the run's length class). <paramref name="flipV"/>
    /// reflects the body across the box's horizontal midline (box-local z) — the branch/holed forms are built
    /// spine-first (arms hanging down), so flipping turns the open feet toward the box top; the allocator sets it
    /// per frame so a form's solid edges cover the demanded sides (symmetric forms are unaffected). Pieces carry
    /// the hub <paramref name="box"/> label and their body slot; there is no room and no marker. <paramref name="cw"/>
    /// is ignored by the solid Rectangle. <c>null</c> when the box is too small for the form (a directed signal);
    /// throws <see cref="ComposeException"/> only for a form off the hub menu.</summary>
    public static EmittedHub? Fill(
        Box box, CompoundRead form, int cw, IReadOnlyDictionary<BoxEdge, int>? edgeWidths = null, bool flipV = false)
    {
        int boxW = box.Rect[2], boxH = box.Rect[3];
        var body = BuildBody(form, boxW, boxH, cw);
        if (body is null) return null;                       // too small for the form at this cw — a directed signal
        if (flipV) body = BodyOrient.To(body, BoxEdge.Bottom, boxW, boxH);   // feet toward the box top, spine to bottom

        var boxRef = new BoxRef(box.Id, BoxKind.Hub);
        var pieces = new List<GrownPiece>(body.Pieces.Count);
        var n = 1;
        foreach (var (r, slot) in body.Pieces)
            pieces.Add(new GrownPiece($"{box.Id}-t{n++}", [box.Rect[0] + r[0], box.Rect[1] + r[1], r[2], r[3]],
                PlanRoles.Piece, slot, boxRef));

        return new EmittedHub(pieces, Offers(box, body, boxW, boxH, edgeWidths), form);
    }

    // build the body of `form` sized to fill the boxW×boxH box at `cw`; null when the box is too small for it
    // (the BodyEmitter's own dim guards, surfaced as a directed signal rather than an exception).
    private static ShapeBody? BuildBody(CompoundRead form, int w, int h, int cw)
    {
        try
        {
            return form.Form switch
            {
                Compound.Rectangle => BodyEmitter.Rectangle(w, h),
                Compound.SpineArms when form.Arms == 1 => BodyEmitter.SpineArms(cw, [0], w, h - cw),           // L: one end arm
                Compound.SpineArms when form.Arms == 2 => BodyEmitter.SpineArms(cw, [0, w - cw], w, h - cw),   // U: two end arms
                Compound.SpineArms => throw new ComposeException($"a hub arm-form takes 1 (L) or 2 (U) arms, not {form.Arms}."),
                Compound.Ring => BodyEmitter.Ring(cw, w, h),
                Compound.DoubleHole => BodyEmitter.DoubleHole(cw, w - 2 * cw, h, 2 * cw),                       // ring left, U reaching to the box's right edge
                _ => throw new ComposeException($"the hub form menu excludes {form.Form}."),
            };
        }
        catch (ArgumentException) { return null; }
    }

    // one offer per contiguous free run on each box edge, Several-grouped (each neighbour docks its own run).
    // The offered width is the composer's per-edge constraint, defaulting to the run's own width class.
    private static IReadOnlyList<EdgeOffer> Offers(
        Box box, ShapeBody body, int boxW, int boxH, IReadOnlyDictionary<BoxEdge, int>? edgeWidths)
    {
        var offers = new List<EdgeOffer>();
        foreach (var edge in BoxInterfaces.Of(body, boxW, boxH))
        {
            var k = 0;
            foreach (var run in BoxInterfaces.Runs(edge.Intervals))
            {
                var width = edgeWidths is not null && edgeWidths.TryGetValue(edge.Edge, out var w)
                    ? w : BodyEdges.WidthClass(run.LengthCells);
                offers.Add(new EdgeOffer(edge.Edge, run, width, OfferGrouping.Several, $"{box.Id}-{edge.Edge}-{k++}"));
            }
        }
        return offers;
    }
}
