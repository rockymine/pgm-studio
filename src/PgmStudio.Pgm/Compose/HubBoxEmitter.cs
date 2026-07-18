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
/// the neighbour's corridor width. The composer decides which offer each neighbour takes and drives the
/// per-edge widths (G63-C); this emitter produces the body and the offers it sources.
///
/// <para>Form menu (<see cref="Forms"/>, authored — §5.5): <b>Rectangle · L · U · Ring · Double-hole</b>,
/// compact optionally-holed bodies, deliberately not Zig/Hook/the higher combs. <b>Rectangle</b> (the solid hub
/// — the direct successor to the grower's <c>hubU×hubV</c> rect) is emittable today; the branch/holed forms
/// follow as they gain box-sizing. Pieces carry the hub <see cref="BoxRef"/> and their structural slot, extending
/// the label-preservation invariant to the hub.</para>
/// </summary>
public static class HubBoxEmitter
{
    /// <summary>The hub's authored form menu as data — the <see cref="Compound"/> bodies a hub may be (§5.5). A
    /// hub stays rectangle-ish, so the menu is the compact forms only. Rectangle is emittable by
    /// <see cref="Fill"/> today; L (<c>SpineArms(1)</c>), U (<c>SpineArms(2)</c>), Ring, and Double-hole are the
    /// authored set the sizing lands for next.</summary>
    public static readonly IReadOnlyList<CompoundRead> Forms = [new CompoundRead(Compound.Rectangle)];

    private static readonly BoxEdge[] AllEdges = [BoxEdge.Top, BoxEdge.Bottom, BoxEdge.Left, BoxEdge.Right];

    /// <summary>Fill a hub <see cref="Box"/> (plan cells) as <paramref name="form"/>, terminal-free, publishing
    /// one <see cref="EdgeOffer"/> per edge. <paramref name="edgeWidths"/> is the constraint the hub sources —
    /// the w2/w4/w6 rung each edge offers a neighbour; an edge left unset offers its own free width (the edge's
    /// length class). Pieces carry the hub <paramref name="box"/> label and their body slot; there is no room and
    /// no marker. Only <see cref="Compound.Rectangle"/> is emittable today (the solid hub); other forms throw a
    /// directed <see cref="ComposeException"/> until their box-sizing lands.</summary>
    public static EmittedHub Fill(Box box, CompoundRead form, IReadOnlyDictionary<BoxEdge, int>? edgeWidths = null)
    {
        if (form.Form != Compound.Rectangle)
            throw new ComposeException($"the hub emitter builds only the solid Rectangle so far (requested {form.Form}).");

        int boxW = box.Rect[2], boxH = box.Rect[3];
        var body = BodyEmitter.Rectangle(boxW, boxH);

        var boxRef = new BoxRef(box.Id, BoxKind.Hub);
        var pieces = new List<GrownPiece>(body.Pieces.Count);
        var n = 1;
        foreach (var (r, slot) in body.Pieces)
            pieces.Add(new GrownPiece($"{box.Id}-t{n++}", [box.Rect[0] + r[0], box.Rect[1] + r[1], r[2], r[3]],
                PlanRoles.Piece, slot, boxRef));

        // per-edge offers: the whole free edge is the interval a neighbour may dock along; the offered width is
        // the composer's per-edge constraint, defaulting to the edge's own width class (the geometric maximum).
        var offers = new List<EdgeOffer>(AllEdges.Length);
        foreach (var e in AllEdges)
        {
            var alongLen = e is BoxEdge.Top or BoxEdge.Bottom ? boxW : boxH;
            var width = edgeWidths is not null && edgeWidths.TryGetValue(e, out var w) ? w : BodyEdges.WidthClass(alongLen);
            offers.Add(new EdgeOffer(e, new EdgeInterval(0, alongLen, ApproachSlots.Bar), width,
                OfferGrouping.Several, $"{box.Id}-{e}"));
        }

        return new EmittedHub(pieces, offers, new CompoundRead(Compound.Rectangle));
    }
}
