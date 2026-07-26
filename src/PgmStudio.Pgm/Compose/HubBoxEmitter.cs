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
/// <para>Form menu (<see cref="Forms"/>, authored — §5.5): <b>Rectangle · L · U · Ring · P · Double-hole · G</b>,
/// compact optionally-holed bodies, deliberately not Zig/Hook/the higher combs. Each is a <see cref="Compound"/>
/// sized to fill the box, so its outward walls touch the box edges and become offers; a body too small for its
/// form at the given <c>cw</c> is a <b>directed null</b>, not a throw. Pieces carry the hub <see cref="BoxRef"/>
/// and their structural slot, extending the label-preservation invariant to the hub.</para>
/// </summary>
public static class HubBoxEmitter
{
    /// <summary>The hub's authored form menu as data — the <see cref="Compound"/> bodies a hub may be (§5.5). A
    /// hub stays rectangle-ish, so the menu is the compact forms plus the <b>wide holed</b> bodies a laterally
    /// elongated hub affords: the solid Rectangle, the branch family at one arm (L) and two (U), the Ring, the
    /// <b>P</b> (a loop on a longer bar — the bar's overhang is a long free run), the <b>Double-hole</b> (a ring +
    /// a docked U — two equal holes), and the <b>G</b> (a ring + an L — the ring's hole plus a frontline-sealed bay,
    /// asymmetric holes). The wide forms need width ≥ 9 and fall back (a directed null) below it.</summary>
    public static readonly IReadOnlyList<CompoundRead> Forms =
    [
        new(Compound.Rectangle),
        new(Compound.SpineArms, 1),   // L — a spine with one end arm
        new(Compound.SpineArms, 2),   // U — a spine with two end arms, the bay between
        new(Compound.Ring),           // one enclosed hole
        new(Compound.P),              // a loop on a longer bar — the overhang a long free run
        new(Compound.DoubleHole),     // a ring + a docked U — two holes
        new(Compound.G),              // a ring + an L — the ring's hole + a frontline-sealed bay (asymmetric holes)
    ];

    /// <summary>How wide a branch hub's leg may grow, as a multiple of the corridor — the numerator over
    /// <see cref="LegWidthCapDenominator"/>, so 5/2 is two and a half corridors, floored. A leg wider than this
    /// stops reading as a leg and starts reading as a second body.</summary>
    public const int LegWidthCapNumerator = 5;

    /// <inheritdoc cref="LegWidthCapNumerator"/>
    public const int LegWidthCapDenominator = 2;

    /// <summary>How many corridors wide a branch hub's <b>bay</b> may be at most — the gap between a U's two legs.
    /// It is bounded on both sides: at least one corridor so the legs read apart, at most this so a wide spine is
    /// spent on the legs rather than swallowed by the gap. That is what makes a big box produce a wide leg rather
    /// than two stubs either side of a chasm, and it matches the bound the frontline's own bay already keeps.
    /// The <b>notch</b> of an L is not a bay and carries only the lower bound.</summary>
    public const int BayCapCorridors = 2;

    /// <summary>
    /// Sample a branch hub's <b>leg layout</b> — each leg's <c>(Start, Width)</c> on a
    /// <paramref name="spineLen"/>-wide spine — or <c>null</c> when the spine cannot host one, which leaves the
    /// caller its uniform default.
    ///
    /// <para>The laws, all in corridors (<paramref name="cw"/>): every leg is at least one corridor and at most
    /// <see cref="LegWidthCapNumerator"/>/<see cref="LegWidthCapDenominator"/> of one; the <b>L</b> keeps at least
    /// a corridor of notch beside its leg, and the <b>U</b> at least a corridor of bay between its two. Widths are
    /// otherwise free — nothing here is constrained to a multiple of anything, because a hub leg sits away from
    /// the symmetry axis and so has no parity to answer to.</para>
    ///
    /// <para>A wide leg is what makes the L worth having on a small board, where it is the form the budget
    /// actually buys: at one corridor the leg is a stub against a full-width bar, and the body reads as a bar
    /// with a nub rather than as an L.</para>
    /// </summary>
    public static IReadOnlyList<(int Start, int Width)>? SampleArms(
        ComposeRng rng, int spineLen, int arms, int cw)
    {
        if (cw < 1) return null;
        var cap = LegWidthCapNumerator * cw / LegWidthCapDenominator;

        if (arms == 1)
        {
            var widest = Math.Min(cap, spineLen - cw);        // the notch beside the leg keeps a corridor
            return widest < cw ? null : [(0, rng.NextInt(cw, widest + 1))];
        }
        if (arms != 2) return null;

        // the bay is drawn first, because it is the bounded quantity: whatever it does not take, the legs must
        // absorb, which is how a wide spine comes out with a wide leg instead of a wide gap
        var bayLo = Math.Max(cw, spineLen - 2 * cap);              // the legs cannot absorb more than 2 caps
        var bayHi = Math.Min(BayCapCorridors * cw, spineLen - 2 * cw);
        if (bayLo > bayHi) return null;
        var bay = rng.NextInt(bayLo, bayHi + 1);

        var legs = spineLen - bay;
        var w1Lo = Math.Max(cw, legs - cap);
        var w1Hi = Math.Min(cap, legs - cw);
        if (w1Lo > w1Hi) return null;
        var w1 = rng.NextInt(w1Lo, w1Hi + 1);
        return [(0, w1), (w1 + bay, legs - w1)];
    }

    /// <summary>The layout a branch hub takes when none was drawn for it — the plainest one the laws admit, or
    /// <c>null</c> when the spine admits none at all. The L keeps its one-corridor leg (its notch has no upper
    /// bound to answer to); the U takes the widest lawful bay and splits what is left evenly, so the default obeys
    /// the same bay cap the sampler does rather than being a shape the composer could never draw.</summary>
    public static IReadOnlyList<(int Start, int Width)>? DefaultArms(int spineLen, int arms, int cw)
    {
        if (cw < 1) return null;
        var cap = LegWidthCapNumerator * cw / LegWidthCapDenominator;

        if (arms == 1) return spineLen - cw < cw ? null : [(0, cw)];
        if (arms != 2) return null;

        var bay = Math.Clamp(spineLen - 2 * cw, cw, BayCapCorridors * cw);
        var legs = spineLen - bay;
        int w1 = legs / 2, w2 = legs - w1;
        return w1 < cw || w2 > cap ? null : [(0, w1), (w1 + bay, w2)];
    }

    /// <summary>Fill a hub <see cref="Box"/> (plan cells) as <paramref name="form"/> at wall/corridor width
    /// <paramref name="cw"/>, terminal-free, publishing one <see cref="EdgeOffer"/> per free run on each edge at
    /// the width that run can <b>support</b> (its length class). That is the hub's surface, not a per-neighbour
    /// agreement: one run can carry two docks at two widths, so the width a given neighbour builds to rides on
    /// its <see cref="BoxJoint.Offer"/>, not here. <paramref name="flipV"/>
    /// reflects the body across the box's horizontal midline (box-local z) — the branch/holed forms are built
    /// spine-first (arms hanging down), so flipping turns the open feet toward the box top; the allocator sets it
    /// per frame so a form's solid edges cover the demanded sides (symmetric forms are unaffected). Pieces carry
    /// the hub <paramref name="box"/> label and their body slot; there is no room and no marker. <paramref name="cw"/>
    /// is ignored by the solid Rectangle. <paramref name="ringWalls"/> widens the four walls of a ring-bodied form
    /// (Ring/P/Double-hole/G) independently, spending the box's slack rather than growing it; <c>null</c> keeps all
    /// four at <paramref name="cw"/>, and it is ignored by the forms without a ring. Walls are given in the
    /// <b>pre-flip</b> body frame, so <paramref name="flipV"/> swaps which of top/bottom faces the box top.
    /// <c>null</c> when the box is too small for the form (a directed signal);
    /// throws <see cref="ComposeException"/> only for a form off the hub menu.</summary>
    public static EmittedHub? Fill(
        Box box, CompoundRead form, int cw, bool flipV = false, RingWalls? ringWalls = null,
        IReadOnlyList<(int Start, int Width)>? armLayout = null) =>
        Fill(box, form, cw, flipV, out _, ringWalls, armLayout);

    /// <inheritdoc cref="Fill(Box, CompoundRead, int, bool, RingWalls?, IReadOnlyList{ValueTuple{int, int}})"/>
    /// <param name="rejection">On a <c>null</c> return, <b>why</b> the form was refused — the directed reason in
    /// the shared <see cref="FillRejection"/> vocabulary, carrying the body emitter's own dimension message
    /// rather than discarding it. <c>null</c> when the fill succeeded.</param>
    public static EmittedHub? Fill(
        Box box, CompoundRead form, int cw, bool flipV, out FillRejection? rejection,
        RingWalls? ringWalls = null, IReadOnlyList<(int Start, int Width)>? armLayout = null)
    {
        rejection = null;
        int boxW = box.Rect[2], boxH = box.Rect[3];
        var body = BuildBody(form, boxW, boxH, cw, ringWalls, armLayout, out var detail);
        if (body is null)                                    // too small for the form at this cw — a directed signal
        {
            rejection = new FillRejection.FormDoesNotFit(detail ?? $"{form.Form} does not fit {boxW}x{boxH} at cw {cw}.");
            return null;
        }
        if (flipV) body = BodyOrient.To(body, BoxEdge.Bottom, boxW, boxH);   // feet toward the box top, spine to bottom

        var boxRef = new BoxRef(box.Id, BoxKind.Hub);
        var pieces = new List<GrownPiece>(body.Pieces.Count);
        var n = 1;
        foreach (var (r, slot) in body.Pieces)
            pieces.Add(new GrownPiece($"{box.Id}-t{n++}", [box.Rect[0] + r[0], box.Rect[1] + r[1], r[2], r[3]],
                PlanRoles.Piece, slot, boxRef));

        return new EmittedHub(pieces, Offers(box, body, boxW, boxH), form);
    }

    // build the body of `form` sized to fill the boxW×boxH box at `cw`; null when the box is too small for it
    // (the BodyEmitter's own dim guards, surfaced as a directed signal rather than an exception). `detail` carries
    // the guard's own message on refusal — the reason the caller reports, not a re-derivation of it.
    private static ShapeBody? BuildBody(
        CompoundRead form, int w, int h, int cw, RingWalls? ringWalls,
        IReadOnlyList<(int Start, int Width)>? armLayout, out string? detail)
    {
        detail = null;
        var walls = ringWalls ?? RingWalls.Uniform(cw);
        ShapeBody Branch(int arms)
        {
            // the sampled legs, or the plainest lawful ones. A spine the laws admit no layout for — too short for
            // a leg beside its notch, or so long the legs could not absorb what the capped bay leaves — is refused
            // rather than built as a stub against a chasm, which is what the old fixed default did.
            var legs = armLayout is { Count: > 0 } given ? given : DefaultArms(w, arms, cw);
            if (legs is null)
                throw new ArgumentException(
                    $"spine {w} admits no {(arms == 1 ? "L" : "U")} layout at cw {cw} — the leg"
                    + $"{(arms == 1 ? " and its notch" : "s and their bay")} do not fit its length.");
            return BodyEmitter.SpineArms(w, cw, legs.Select(a => (a.Start, a.Width, h - cw)).ToList());
        }
        try
        {
            return form.Form switch
            {
                Compound.Rectangle => BodyEmitter.Rectangle(w, h),
                Compound.SpineArms when form.Arms == 1 => Branch(1),                                          // L: one end arm
                Compound.SpineArms when form.Arms == 2 => Branch(2),                                          // U: two end arms
                Compound.SpineArms => throw new ComposeException($"a hub arm-form takes 1 (L) or 2 (U) arms, not {form.Arms}."),
                Compound.Ring => BodyEmitter.Ring(walls, w, h),
                Compound.P => BodyEmitter.P(walls, cw, w - 2 * cw, h, 2 * cw),                                  // loop of width w−bar, the bar overhanging by 2·cw
                Compound.DoubleHole => BodyEmitter.DoubleHole(walls, cw, w - 2 * cw, h, 2 * cw, h, 0),          // ring left, a full-height U on its right (two equal holes) — fits a shallow-wide hub
                Compound.G => BodyEmitter.G(walls, cw, w - 2 * cw, h, w),                                       // ring left, an L on its right — the ring hole + a frontline-sealed bay
                _ => throw new ComposeException($"the hub form menu excludes {form.Form}."),
            };
        }
        catch (ArgumentException e) { detail = e.Message; return null; }
    }

    // one offer per contiguous free run on each box edge, Several-grouped (each neighbour docks its own run),
    // at the w2/w4/w6 rung the run's own length can support — the hub's capacity. What a particular neighbour
    // was granted is on its joint: two neighbours can share one run at different widths, which a per-run (let
    // alone per-edge) width could not express.
    private static IReadOnlyList<EdgeOffer> Offers(Box box, ShapeBody body, int boxW, int boxH)
    {
        var offers = new List<EdgeOffer>();
        foreach (var edge in BoxInterfaces.Of(body, boxW, boxH))
        {
            var k = 0;
            foreach (var run in BoxInterfaces.Runs(edge.Intervals))
                offers.Add(new EdgeOffer(edge.Edge, run, BodyEdges.WidthClass(run.LengthCells),
                    OfferGrouping.Several, $"{box.Id}-{edge.Edge}-{k++}"));
        }
        return offers;
    }
}
