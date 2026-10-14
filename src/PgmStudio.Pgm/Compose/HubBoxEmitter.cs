using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The hub binding over <see cref="CompoundEmitter"/> — the <b>constraint-source box kind</b>
/// (docs/generator/model.md §4). Unlike the wool/spawn boxes the hub has <b>no room</b>: it is a
/// <see cref="Compound"/> of one <see cref="CompoundForm"/> whose edges carry widths. It <b>emits first</b> and
/// its edge widths set the fill menus
/// of the spawn/wool/frontline neighbours, published here as <see cref="EdgeOffer"/>s (the
/// hub's half of the offer mechanism): a consumed <see cref="EdgeOffer.Width"/> is
/// the neighbour's lane width. The composer decides which offer each neighbour takes and drives the per-edge
/// widths; this emitter produces the compound and reads the offers it sources.
///
/// <para>Form menu (<see cref="Forms"/>, authored — model.md §4): <b>Rectangle · L · U · Ring · P · Double-hole · G</b>,
/// compact optionally-holed compounds, deliberately not Zig/Hook/the higher combs. Each is one <see cref="CompoundForm"/>
/// sized to fill the box, so its outward walls touch the box edges and become offers; a compound too small for its
/// form at the given <c>laneWidth</c> is a <b>directed null</b>, not a throw.</para>
/// </summary>
public static class HubBoxEmitter
{
    /// <summary>The hub's authored form menu as data — the <see cref="CompoundForm"/>s a hub may be (model.md §4). A
    /// hub stays rectangle-ish, so the menu is the compact forms plus the <b>wide holed</b> compounds a laterally
    /// elongated hub affords: the solid Rectangle, the branch family at one arm (L) and two (U), the Ring, the
    /// <b>P</b> (a loop on a longer bar — the bar's overhang is a long free run), the <b>Double-hole</b> (a ring +
    /// a docked U — two equal holes), and the <b>G</b> (a ring + an L — the ring's hole plus a frontline-sealed bay,
    /// asymmetric holes). The wide forms need width ≥ 9 and fall back (a directed null) below it.</summary>
    public static readonly IReadOnlyList<CompoundRead> Forms =
    [
        new(CompoundForm.Rectangle),
        new(CompoundForm.SpineArms, 1),   // L — a spine with one end arm
        new(CompoundForm.SpineArms, 2),   // U — a spine with two end arms, the bay between
        new(CompoundForm.Ring),           // one enclosed hole
        new(CompoundForm.P),              // a loop on a longer bar — the overhang a long free run
        new(CompoundForm.DoubleHole),     // a ring + a docked U — two holes
        new(CompoundForm.G),              // a ring + an L — the ring's hole + a frontline-sealed bay (asymmetric holes)
    ];

    /// <summary>How wide a branch hub's leg may grow, as a multiple of the corridor — the numerator over
    /// <see cref="LegWidthCapDenominator"/>, so 5/2 is two and a half corridors, floored. A leg wider than this
    /// stops reading as a leg and starts reading as a second compound.</summary>
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
    /// <para>The laws, all in corridors (<paramref name="laneWidth"/>): every leg is at least one corridor and at most
    /// <see cref="LegWidthCapNumerator"/>/<see cref="LegWidthCapDenominator"/> of one; the <b>L</b> keeps at least
    /// a corridor of notch beside its leg, and the <b>U</b> at least a corridor of bay between its two. Widths are
    /// otherwise free — nothing here is constrained to a multiple of anything, because a hub leg sits away from
    /// the symmetry axis and so has no parity to answer to.</para>
    ///
    /// <para>A wide leg is what makes the L worth having on a small board, where it is the form the budget
    /// actually buys: at one corridor the leg is a stub against a full-width bar, and the compound reads as a bar
    /// with a nub rather than as an L.</para>
    /// </summary>
    public static IReadOnlyList<(int Start, int Width)>? SampleArms(
        ComposeRng rng, int spineLen, int arms, int laneWidth)
    {
        if (laneWidth < 1) return null;
        var cap = LegWidthCapNumerator * laneWidth / LegWidthCapDenominator;

        if (arms == 1)
        {
            var widest = Math.Min(cap, spineLen - laneWidth);        // the notch beside the leg keeps a corridor
            return widest < laneWidth ? null : [(0, rng.NextInt(laneWidth, widest + 1))];
        }
        if (arms != 2) return null;

        // the bay is drawn first, because it is the bounded quantity: whatever it does not take, the legs must
        // absorb, which is how a wide spine comes out with a wide leg instead of a wide gap
        var bayLo = Math.Max(laneWidth, spineLen - 2 * cap);              // the legs cannot absorb more than 2 caps
        var bayHi = Math.Min(BayCapCorridors * laneWidth, spineLen - 2 * laneWidth);
        if (bayLo > bayHi) return null;
        var bay = rng.NextInt(bayLo, bayHi + 1);

        var legs = spineLen - bay;
        var w1Lo = Math.Max(laneWidth, legs - cap);
        var w1Hi = Math.Min(cap, legs - laneWidth);
        if (w1Lo > w1Hi) return null;
        var w1 = rng.NextInt(w1Lo, w1Hi + 1);
        return [(0, w1), (w1 + bay, legs - w1)];
    }

    /// <summary>The layout a branch hub takes when none was drawn for it — the plainest one the laws admit, or
    /// <c>null</c> when the spine admits none at all. The L keeps its one-corridor leg (its notch has no upper
    /// bound to answer to); the U takes the widest lawful bay and splits what is left evenly, so the default obeys
    /// the same bay cap the sampler does rather than being a shape the composer could never draw.</summary>
    public static IReadOnlyList<(int Start, int Width)>? DefaultArms(int spineLen, int arms, int laneWidth)
    {
        if (laneWidth < 1) return null;
        var cap = LegWidthCapNumerator * laneWidth / LegWidthCapDenominator;

        if (arms == 1) return spineLen - laneWidth < laneWidth ? null : [(0, laneWidth)];
        if (arms != 2) return null;

        var bay = Math.Clamp(spineLen - 2 * laneWidth, laneWidth, BayCapCorridors * laneWidth);
        var legs = spineLen - bay;
        int w1 = legs / 2, w2 = legs - w1;
        return w1 < laneWidth || w2 > cap ? null : [(0, w1), (w1 + bay, w2)];
    }

    /// <summary>Fill a hub <see cref="Box"/> as <paramref name="form"/> at wall/lane width <paramref name="laneWidth"/>:
    /// the compound, box-local, with no room. <see cref="Offers"/> reads what each of its free runs can carry.
    /// <paramref name="flipV"/>
    /// reflects the compound across the box's horizontal midline (box-local z) — the branch/holed forms are built
    /// spine-first (arms hanging down), so flipping turns the open feet toward the box top; the allocator sets it
    /// per frame so a form's solid edges cover the demanded sides (symmetric forms are unaffected). <paramref name="laneWidth"/>
    /// is ignored by the solid Rectangle. <paramref name="ringWalls"/> widens the four walls of a ring-bodied form
    /// (Ring/P/Double-hole/G) independently, spending the box's slack rather than growing it; <c>null</c> keeps all
    /// four at <paramref name="laneWidth"/>, and it is ignored by the forms without a ring. Walls are given in the
    /// <b>pre-flip</b> compound frame, so <paramref name="flipV"/> swaps which of top/bottom faces the box top.
    /// <c>null</c> when the box is too small for the form (a directed signal);
    /// throws <see cref="ComposeException"/> only for a form off the hub menu.</summary>
    public static Compound? Fill(
        Box box, CompoundRead form, int laneWidth, bool flipV = false, RingWalls? ringWalls = null,
        IReadOnlyList<(int Start, int Width)>? armLayout = null) =>
        Fill(box, form, laneWidth, flipV, out _, ringWalls, armLayout);

    /// <inheritdoc cref="Fill(Box, CompoundRead, int, bool, RingWalls?, IReadOnlyList{ValueTuple{int, int}})"/>
    /// <remarks>On a <c>null</c> return, <b>why</b> the form was refused — the directed reason in
    /// the shared <see cref="FillRejection"/> vocabulary, carrying the compound emitter's own dimension message
    /// rather than discarding it. <c>null</c> when the fill succeeded.</remarks>
    public static Compound? Fill(
        Box box, CompoundRead form, int laneWidth, bool flipV, out FillRejection? rejection,
        RingWalls? ringWalls = null, IReadOnlyList<(int Start, int Width)>? armLayout = null)
    {
        rejection = null;
        int boxW = box.Rect.Width, boxH = box.Rect.Height;
        var compound = BuildCompound(form, boxW, boxH, laneWidth, ringWalls, armLayout, out var detail);
        if (compound is null)                                    // too small for the form at this laneWidth — a directed signal
        {
            rejection = new FillRejection.FormDoesNotFit(detail ?? $"{form.Form} does not fit {boxW}x{boxH} at laneWidth {laneWidth}.");
            return null;
        }
        return flipV ? compound.Turned(BoxTurn.FlipZ, boxW, boxH) : compound;   // feet toward the box top
    }

    // build the compound of `form` sized to fill the boxW×boxH box at `laneWidth`; null when the box is too small for it
    // (the CompoundEmitter's own dim guards, surfaced as a directed signal rather than an exception). `detail` carries
    // the guard's own message on refusal — the reason the caller reports, not a re-derivation of it.
    private static Compound? BuildCompound(
        CompoundRead form, int w, int h, int laneWidth, RingWalls? ringWalls,
        IReadOnlyList<(int Start, int Width)>? armLayout, out string? detail)
    {
        detail = null;
        var walls = ringWalls ?? RingWalls.Uniform(laneWidth);
        Compound Branch(int arms)
        {
            // the sampled legs, or the plainest lawful ones. A spine the laws admit no layout for — too short for
            // a leg beside its notch, or so long the legs could not absorb what the capped bay leaves — is refused
            // rather than built as a stub against a chasm, which is what a fixed default leg length gives.
            var legs = armLayout is { Count: > 0 } given ? given : DefaultArms(w, arms, laneWidth);
            if (legs is null)
                throw new ArgumentException(
                    $"spine {w} admits no {(arms == 1 ? "L" : "U")} layout at laneWidth {laneWidth} — the leg"
                    + $"{(arms == 1 ? " and its notch" : "s and their bay")} do not fit its length.");
            return CompoundEmitter.SpineArms(w, laneWidth, legs.Select(a => (a.Start, a.Width, h - laneWidth)).ToList());
        }
        try
        {
            return form.Form switch
            {
                CompoundForm.Rectangle => CompoundEmitter.Rectangle(w, h),
                CompoundForm.SpineArms when form.Arms == 1 => Branch(1),                                          // L: one end arm
                CompoundForm.SpineArms when form.Arms == 2 => Branch(2),                                          // U: two end arms
                CompoundForm.SpineArms => throw new ComposeException($"a hub arm-form takes 1 (L) or 2 (U) arms, not {form.Arms}."),
                CompoundForm.Ring => CompoundEmitter.Ring(walls, w, h),
                CompoundForm.P => CompoundEmitter.P(walls, laneWidth, w - 2 * laneWidth, h, 2 * laneWidth),                                  // loop of width w−bar, the bar overhanging by 2·laneWidth
                CompoundForm.DoubleHole => CompoundEmitter.DoubleHole(walls, laneWidth, w - 2 * laneWidth, h, 2 * laneWidth, h, 0),          // ring left, a full-height U on its right (two equal holes) — fits a shallow-wide hub
                CompoundForm.G => CompoundEmitter.G(walls, laneWidth, w - 2 * laneWidth, h, w),                                       // ring left, an L on its right — the ring hole + a frontline-sealed bay
                _ => throw new ComposeException($"the hub form menu excludes {form.Form}."),
            };
        }
        catch (ArgumentException e) { detail = e.Message; return null; }
    }

    /// <summary>One offer per contiguous free run on each edge of the hub <paramref name="compound"/> filling
    /// <paramref name="box"/>, at the w2/w4/w6 rung the run's own length can support — the hub's capacity. What a
    /// particular neighbour was granted is on its joint: two neighbours can share one run at different widths,
    /// which a per-run (let alone per-edge) width could not express.</summary>
    public static IReadOnlyList<EdgeOffer> Offers(Box box, Compound compound)
    {
        var offers = new List<EdgeOffer>();
        foreach (var edge in BoxInterfaces.Of(compound, box.Rect.Width, box.Rect.Height))
        {
            var k = 0;
            foreach (var run in BoxInterfaces.Runs(edge.Intervals))
                offers.Add(new EdgeOffer(run, CompoundEdges.LaneWidthOn(run.Length),
                    OfferGrouping.Several, $"{box.Id}-{edge.Edge}-{k++}"));
        }
        return offers;
    }
}
