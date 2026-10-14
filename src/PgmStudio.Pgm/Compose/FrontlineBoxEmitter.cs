using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Compose;

/// <summary>
/// The frontline binding over <see cref="CompoundEmitter"/> — the <b>join box kind</b>
/// (docs/generator/model.md §4). Room-free like the hub, with one edge the <c>face</c> (where the fanned images
/// meet the mid). It is a <b>join, not a
/// placement</b> — its only interfaces are the <b>spine</b> (which docks the hub, consuming the hub's front-edge
/// offer) and the <b>face</b> (which the mid docks); the sides are inert. <b>Rotation is fixed by the
/// box kind</b>: the spine docks the hub and the arm-tips are the face toward the axis, so the emitter works
/// in one canonical frame — spine <see cref="BoxEdge.Top"/>, face <see cref="BoxEdge.Bottom"/> — and the composer
/// orients the placed box onto the hub.
///
/// <para>Form menu (<see cref="Forms"/>): <b>Bar</b> (the wide face, FR6), and the branch family <b>single</b>
/// (<c>SpineArms(1)</c>, FR3/FR4) and <b>twin</b> (<c>SpineArms(2)</c>, CT8).</para>
/// </summary>
public static class FrontlineBoxEmitter
{
    /// <summary>The frontline's authored form menu as data (model.md §4): the wide <b>Bar</b> and the branch family at
    /// one arm (single — the <b>fat L</b>) and two (twin). The holed forms (P, two-U-on-I) are authored additions
    /// beyond the grower's <c>FrontForm</c> and land next.</summary>
    public static readonly IReadOnlyList<CompoundRead> Forms =
    [
        new(CompoundForm.Rectangle),      // Bar — the wide face (FR6 / FrontForm.Wide)
        new(CompoundForm.SpineArms, 1),   // single — the fat L (FR3/FR4 / FrontForm.Single)
        new(CompoundForm.SpineArms, 2),   // twin (CT8 / FrontForm.Twin)
    ];

    /// <summary>Sample a branch frontline's <b>leg layout</b> — the variation knob over the canonical forms (the
    /// fat L, the symmetric twin): per-leg (Start, Width) on a <paramref name="w"/>-wide spine. The laws: every
    /// leg at least <b>2 wide</b>; at most two legs, their widths within <b>factor 2</b> of each other (never a 2
    /// beside a 5); the inter-leg <b>bay 2–4</b> wide; end recesses allowed but together at most a <b>third</b> of
    /// the spine (the front stays substantial); the single leg strictly wider than its notch (the fat-L law — a
    /// thin leg re-creates the banned T's thin band). Returns <c>null</c> when <paramref name="w"/> cannot host
    /// the layout — the caller falls back to the canonical form.</summary>
    public static IReadOnlyList<(int Start, int Width)>? SampleArms(ComposeRng rng, int w, int arms)
    {
        if (arms == 1)
        {
            var notchMax = Math.Min(4, (w - 1) / 2);
            if (notchMax < 2) return null;
            var notch = rng.NextInt(2, notchMax + 1);
            var atStart = rng.NextBool(0.5);
            return [(atStart ? 0 : notch, w - notch)];
        }
        if (arms != 2 || w < 6) return null;
        var bay = rng.NextInt(2, Math.Min(4, w - 4) + 1);           // the inter-leg bay
        var slack = rng.NextInt(0, Math.Min(w - 4 - bay, w / 3) + 1); // end recess total, capped at w/3
        var rem = w - bay - slack;                                   // the two legs' combined width
        var lo = Math.Max(2, (rem + 2) / 3);                         // factor-2 law: w1 in [ceil(rem/3)..2rem/3]
        var hi = Math.Min(rem - 2, 2 * rem / 3);
        if (lo > hi) return null;
        var w1 = rng.NextInt(lo, hi + 1);
        var end0 = rng.NextInt(0, slack + 1);
        return [(end0, w1), (end0 + w1 + bay, rem - w1)];
    }

    /// <summary>Fill a frontline <see cref="Box"/> as <paramref name="form"/> at strand width <paramref name="cw"/>:
    /// the compound, box-local, with no room, its spine docking <paramref name="spineMouth"/> (the box edge facing
    /// the hub) and its face on the opposite edge toward the axis. An optional <paramref name="armLayout"/> (from
    /// <see cref="SampleArms"/>) overrides the branch forms' canonical legs; when absent the canonical fat L /
    /// symmetric twin builds. The compound is built spine-up and turned onto the mouth
    /// (<see cref="Compound.Turned"/>), so the composer places the box against any hub edge. <c>null</c> when the
    /// box is too small for the form (a directed signal).</summary>
    public static Compound? Fill(
        Box box, CompoundRead form, int cw, BoxEdge spineMouth = BoxEdge.Top,
        IReadOnlyList<(int Start, int Width)>? armLayout = null) =>
        Fill(box, form, cw, out _, spineMouth, armLayout);

    /// <inheritdoc cref="Fill(Box, CompoundRead, int, BoxEdge, IReadOnlyList{ValueTuple{int, int}})"/>
    /// <remarks>On a <c>null</c> return, <b>why</b> the form was refused, in the shared <see cref="FillRejection"/>
    /// vocabulary.</remarks>
    public static Compound? Fill(
        Box box, CompoundRead form, int cw, out FillRejection? rejection, BoxEdge spineMouth = BoxEdge.Top,
        IReadOnlyList<(int Start, int Width)>? armLayout = null)
    {
        rejection = null;
        // build spine-up in the spine-length × reach frame (transposed when the spine docks a lateral edge), then
        // turn onto the mouth, as a spawn or wool box turns its mouth-up emission
        var lateral = spineMouth is BoxEdge.Left or BoxEdge.Right;
        var (spineLen, reach) = lateral ? (box.Rect.Height, box.Rect.Width) : (box.Rect.Width, box.Rect.Height);
        var built = BuildCompound(form, spineLen, reach, cw, armLayout, out var detail);
        if (built is null)
        {
            rejection = new FillRejection.FormDoesNotFit(
                detail ?? $"{form.Form} (arms {form.Arms}) does not fit a {spineLen}x{reach} spine frame at cw {cw}.");
            return null;
        }
        return built.Turned(spineMouth.TopOnto(), spineLen, reach);
    }

    // build the compound of `form` sized to fill the box, spine along the top; null when the box is too small. An
    // armLayout (SampleArms) overrides the branch forms' canonical legs with the sampled per-leg sizes. `detail`
    // carries the refusing guard's own message — the reason the caller reports, not a re-derivation of it.
    private static Compound? BuildCompound(
        CompoundRead form, int w, int h, int cw, IReadOnlyList<(int Start, int Width)>? armLayout,
        out string? detail)
    {
        detail = null;
        try
        {
            if (form.Form == CompoundForm.SpineArms && armLayout is not null)
            {
                if (armLayout.Count == form.Arms)
                    return CompoundEmitter.SpineArms(w, cw, armLayout.Select(a => (a.Start, a.Width, h - cw)).ToList());
                detail = $"the arm layout names {armLayout.Count} legs, the form {form.Arms}.";
                return null;
            }
            return form.Form switch
            {
                CompoundForm.Rectangle => CompoundEmitter.Rectangle(w, h),                                              // Bar — the wide face
                // single — the FAT L, never a centred T strand: a T's narrow tip is the whole front-face hull,
                // which forces a too-thin mid band. The one arm anchors at the spine's start and spans all but
                // one corridor width (the void notch a real recess), so the face tip carries most of the bar;
                // a leg not strictly wider than the notch (w < 2·cw + 1) is the thin-leg L with the same thin
                // band and directed-nulls instead
                CompoundForm.SpineArms when form.Arms == 1 => w - cw >= cw + 1
                    ? CompoundEmitter.SpineArms(w, cw, [(0, w - cw, h - cw)])
                    : Thin(out detail, w, cw),
                CompoundForm.SpineArms when form.Arms == 2 => CompoundEmitter.SpineArms(cw, [0, w - cw], w, h - cw),    // twin — a strand at each end
                _ => throw new ComposeException($"the frontline menu is Bar / single / twin, not {form.Form} (arms {form.Arms})."),
            };
        }
        catch (ArgumentException e) { detail = e.Message; return null; }
    }

    // the fat-L law refusing a thin leg — named so the switch arm can report the reason it refuses on
    private static Compound? Thin(out string? detail, int w, int cw)
    {
        detail = $"the single arm's leg ({w - cw}) must be wider than its notch ({cw}) — a thin leg " +
                 $"re-creates the banned T's thin band; needs a spine of at least {2 * cw + 1}.";
        return null;
    }
}
