namespace PgmStudio.Pgm.Shapes;

/// <summary>
/// Emits the <b>terminal-free compounds</b> the vocabulary names (docs/contracts/shape-vocabulary.md §5) but the
/// approach emitter (<see cref="ShapeEmitter"/>) can't build, as pure <see cref="ShapeBody"/> — structural-slotted
/// rects (<see cref="ApproachSlots.Bar"/> spines/crossbars, <see cref="ApproachSlots.Leg"/> arms/ring-arms) and,
/// for the holed forms, the enclosed voids as <c>hole</c> vacancies. No terminal, marker, or id — a designation
/// (approach's room, the hub's interfaces, the frontline's face) finishes them later.
///
/// <para>Everything is one atom recombined <b>along shared edges</b> (§3): a spine plus <c>K</c> arms
/// (<see cref="SpineArms"/> — T at K=1, Π/F at K=2, E at K=3, the arm placement a knob, <b>3 arms the maximum</b>),
/// four bars around a void (<see cref="Ring"/>), a ring with one longer bar so a loop slides along it
/// (<see cref="P"/>), a ring with a shorter U docked on its edge for a second void (<see cref="DoubleHole"/>), and
/// two loops on a shared baseline (<see cref="TwoUOnI"/>). Each classifies back to itself through
/// <see cref="ShapeClassifier.ClassifyBody"/> — the emit↔derive mirror for the body layer. Shapes are emitted in a
/// canonical frame (spines horizontal on top, arms hanging down; rings axis-aligned); a box too small for the
/// form throws.</para>
/// </summary>
public static class BodyEmitter
{
    /// <summary>The most arms a spine carries (the branch family is capped at three).</summary>
    public const int MaxArms = 3;

    /// <summary>One filled <paramref name="w"/>×<paramref name="h"/> rectangle — a spine, the base of the ladder
    /// (the solid hub reads as this). A single <see cref="ApproachSlots.Bar"/> piece, no void.</summary>
    public static ShapeBody Rectangle(int w, int h)
    {
        if (w < 1 || h < 1) throw new ArgumentException($"a rectangle needs positive dims (got {w}x{h}).");
        return new ShapeBody([([0, 0, w, h], ApproachSlots.Bar)], []);
    }

    /// <summary>A full-width spine (top, <paramref name="cw"/> tall) with <paramref name="arms"/> equal arms
    /// hanging down, spread evenly with a one-corridor gap and overhang — K=1 a centred T, K=2 a Π, K=3 the E/comb.
    /// The <see cref="MaxArms">arm count</see> is capped at three. For a specific placement (an F, an off-centre
    /// arm) use the <see cref="SpineArms(int, IReadOnlyList{int}, int, int)"/> overload.</summary>
    public static ShapeBody SpineArms(int cw, int arms, int armLen = 0)
    {
        if (arms < 1 || arms > MaxArms)
            throw new ArgumentException($"the branch family supports 1..{MaxArms} arms (requested {arms}).");
        var starts = new int[arms];
        for (var i = 0; i < arms; i++) starts[i] = cw + i * 2 * cw;   // cw overhang, one gap apart
        return SpineArms(cw, starts, (2 * arms + 1) * cw, armLen);
    }

    /// <summary>A full-width spine of <paramref name="spineLen"/> (top, <paramref name="cw"/> tall) with uniform
    /// arms hanging down at the given <paramref name="armStarts"/> columns — the placement knob that reads L/T/Π/F/E
    /// off one family (arms at the ends → Π, an end + the middle → F, three → E). A convenience over the general
    /// <see cref="SpineArms(int, int, IReadOnlyList{ValueTuple{int, int, int}})"/> with every arm one corridor wide
    /// and the same length.</summary>
    public static ShapeBody SpineArms(int cw, IReadOnlyList<int> armStarts, int spineLen, int armLen = 0)
    {
        if (cw < 2) throw new ArgumentException($"corridor width {cw} < 2.");
        var al = armLen > 0 ? armLen : 3 * cw;
        return SpineArms(spineLen, cw, armStarts.Select(s => (s, cw, al)).ToList());
    }

    /// <summary>A spine (top, <paramref name="barThickness"/> tall, <paramref name="spineLen"/> wide) with arms
    /// hanging down, each atom rectangle <b>free to differ in size</b> — <paramref name="arms"/> gives each arm's
    /// <c>(Start, Width, Length)</c>, and the bar its own thickness — so an F can carry a long and a short leg on a
    /// fat bar, etc. Identity is <b>width-independent</b>: whatever the sizes, the classifier reads it by its arm
    /// count (an uneven F still reads <c>SpineArms(2)</c>). Arms must sit within the spine, a gap apart (so each is
    /// a distinct run), and number at most <see cref="MaxArms"/>. Spine is a <see cref="ApproachSlots.Bar"/>, each
    /// arm a <see cref="ApproachSlots.Leg"/>.</summary>
    public static ShapeBody SpineArms(int spineLen, int barThickness, IReadOnlyList<(int Start, int Width, int Length)> arms)
    {
        if (spineLen < 1 || barThickness < 1) throw new ArgumentException($"the spine needs positive dims ({spineLen}x{barThickness}).");
        if (arms.Count < 1 || arms.Count > MaxArms)
            throw new ArgumentException($"the branch family supports 1..{MaxArms} arms (requested {arms.Count}).");
        var sorted = arms.OrderBy(a => a.Start).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var (s, w, len) = sorted[i];
            if (w < 1 || len < 1) throw new ArgumentException($"arm {i} needs positive dims ({w}x{len}).");
            if (s < 0 || s + w > spineLen) throw new ArgumentException($"an arm at column {s} (width {w}) is off the spine (length {spineLen}).");
            if (i > 0 && s < sorted[i - 1].Start + sorted[i - 1].Width + 1)
                throw new ArgumentException("two arms touch — arms need a gap so each is a distinct run.");
        }
        var pieces = new List<(int[] Rect, string Slot)> { ([0, 0, spineLen, barThickness], ApproachSlots.Bar) };
        foreach (var (s, w, len) in arms) pieces.Add(([s, barThickness, w, len], ApproachSlots.Leg));
        return new ShapeBody(pieces, []);
    }

    /// <summary>A rectangular ring — four bars around one enclosed void (the donut body, terminal-free). Top and
    /// bottom are <see cref="ApproachSlots.Bar"/>, the sides <see cref="ApproachSlots.Leg"/>; the hole is a
    /// <c>hole</c> vacancy.</summary>
    public static ShapeBody Ring(int cw, int w, int h)
    {
        if (cw < 2) throw new ArgumentException($"corridor width {cw} < 2.");
        if (w < 2 * cw + 1 || h < 2 * cw + 1) throw new ArgumentException($"box {w}x{h} is too small for a ring at cw {cw}.");
        var pieces = RingPieces(cw, 0, 0, w, h);
        return new ShapeBody(pieces, [Hole(cw, cw, w - 2 * cw, h - 2 * cw)]);
    }

    /// <summary>A ring whose bottom bar is <b>longer</b> than its loop, so the loop sits on the bar with an
    /// overhang and can slide along it as long as the hole stays enclosed (the <c>P</c>/<c>b</c>/<c>d</c> glyph — a
    /// loop on a stem). One void. <paramref name="loopW"/>×<paramref name="ringH"/> is the loop; the bar overhangs
    /// it by <paramref name="barExtend"/> cells total, the loop centred on the overhang. Bars are
    /// <see cref="ApproachSlots.Bar"/>, the loop's sides <see cref="ApproachSlots.Leg"/>.</summary>
    public static ShapeBody P(int cw, int loopW, int ringH, int barExtend = 0)
    {
        if (cw < 2) throw new ArgumentException($"corridor width {cw} < 2.");
        if (loopW < 2 * cw + 1 || ringH < 2 * cw + 1) throw new ArgumentException($"loop {loopW}x{ringH} is too small for a P at cw {cw}.");
        var ext = barExtend > 0 ? barExtend : 2 * cw;             // total overhang of the long bar past the loop
        var ox = ext / 2;                                         // loop offset — centred on the overhang
        var w = loopW + ext;
        var pieces = new List<(int[] Rect, string Slot)>
        {
            ([0, ringH - cw, w, cw], ApproachSlots.Bar),          // the long bottom bar (the loop slides along it)
            ([ox, 0, loopW, cw], ApproachSlots.Bar),              // loop top bar
            ([ox, cw, cw, ringH - 2 * cw], ApproachSlots.Leg),    // loop left leg
            ([ox + loopW - cw, cw, cw, ringH - 2 * cw], ApproachSlots.Leg),  // loop right leg
        };
        return new ShapeBody(pieces, [Hole(ox + cw, cw, loopW - 2 * cw, ringH - 2 * cw)]);
    }

    /// <summary>A ring with a <b>U docked on its right edge</b> — the U's two arms land on the ring's right side and
    /// its bay, closed by that edge, is the second void (§5). The bay separates from the ring interior by the solid
    /// right leg, so the two holes can be <b>the same size</b> (a full-height U, arms flush with the ring's top and
    /// bottom bars) or <b>variant</b> (a shorter U that slides along the edge) — either way both stay enclosed.
    /// <paramref name="ringW"/>×<paramref name="ringH"/> is the ring; <paramref name="uW"/> the U's outward reach
    /// (bay width <c>uW−cw</c>), <paramref name="uH"/> its height, <paramref name="uz"/> its slide (0 = flush with
    /// the top bar; <c>-1</c> = a shorter, slid default). Bars are <see cref="ApproachSlots.Bar"/>, uprights
    /// <see cref="ApproachSlots.Leg"/>; two <c>hole</c> vacancies.</summary>
    public static ShapeBody DoubleHole(int cw, int ringW, int ringH, int uW = 0, int uH = 0, int uz = -1)
    {
        if (cw < 2) throw new ArgumentException($"corridor width {cw} < 2.");
        if (ringW < 2 * cw + 1 || ringH < 2 * cw + 1) throw new ArgumentException($"ring {ringW}x{ringH} is too small for a double-hole at cw {cw}.");
        int uw = uW > 0 ? uW : 2 * cw, uh = uH > 0 ? uH : 3 * cw, z = uz >= 0 ? uz : cw;
        if (uw < cw + 1) throw new ArgumentException($"the U reach {uw} leaves no bay (need > {cw}).");
        if (uh < 2 * cw + 1) throw new ArgumentException($"the U height {uh} leaves no bay at cw {cw}.");
        if (z < 0 || z + uh > ringH)
            throw new ArgumentException($"the U (z {z}, height {uh}) doesn't fit the ring's right edge (ring height {ringH}).");
        var pieces = RingPieces(cw, 0, 0, ringW, ringH);
        pieces.Add(([ringW, z, uw, cw], ApproachSlots.Bar));                       // U top arm (docks the ring's right leg)
        pieces.Add(([ringW, z + uh - cw, uw, cw], ApproachSlots.Bar));            // U bottom arm
        pieces.Add(([ringW + uw - cw, z + cw, cw, uh - 2 * cw], ApproachSlots.Leg)); // U outer wall (closes it)
        return new ShapeBody(pieces,
        [
            Hole(cw, cw, ringW - 2 * cw, ringH - 2 * cw),        // hole 1 — the ring interior
            Hole(ringW, z + cw, uw - cw, uh - 2 * cw),           // hole 2 — the U's bay, closed by the ring edge
        ]);
    }

    /// <summary>Two loops sharing one baseline — two enclosed voids kept apart by an <b>open</b> channel (twin
    /// loops on an I, §5). A full-width baseline <see cref="ApproachSlots.Bar"/>, two top-bar
    /// <see cref="ApproachSlots.Bar"/>s (one per loop) with a gap between them, and four
    /// <see cref="ApproachSlots.Leg"/> uprights; two <c>hole</c> vacancies. <paramref name="hole"/> is each void's
    /// width, <paramref name="gap"/> the open channel width.</summary>
    public static ShapeBody TwoUOnI(int cw, int h, int hole = 0, int gap = 0)
    {
        if (cw < 2) throw new ArgumentException($"corridor width {cw} < 2.");
        if (h < 3 * cw) throw new ArgumentException($"height {h} is too small for two-U-on-I at cw {cw}.");
        int hw = hole > 0 ? hole : cw, gp = gap > 0 ? gap : cw;
        int l0 = 0, l1 = cw + hw, l2 = 2 * cw + hw + gp, l3 = 3 * cw + hw + gp + hw;
        int w = l3 + cw, legH = h - 2 * cw, inner = cw;
        var pieces = new List<(int[] Rect, string Slot)>
        {
            ([0, h - cw, w, cw], ApproachSlots.Bar),                // shared baseline (bottom)
            ([l0, 0, 2 * cw + hw, cw], ApproachSlots.Bar),         // left loop top bar (over l0..l1)
            ([l2, 0, 2 * cw + hw, cw], ApproachSlots.Bar),         // right loop top bar (over l2..l3)
            ([l0, inner, cw, legH], ApproachSlots.Leg),
            ([l1, inner, cw, legH], ApproachSlots.Leg),
            ([l2, inner, cw, legH], ApproachSlots.Leg),
            ([l3, inner, cw, legH], ApproachSlots.Leg),
        };
        return new ShapeBody(pieces,
        [
            Hole(l0 + cw, inner, hw, legH),                        // left void
            Hole(l2 + cw, inner, hw, legH),                       // right void
        ]);
    }

    private static ShapeVacancy Hole(int x, int z, int w, int h) =>
        new("hole", [x, z, w, h], null, [ApproachSlots.Bar, ApproachSlots.Bar, ApproachSlots.Leg, ApproachSlots.Leg]);

    // the four bars of a rectangular ring at (x, z), size w×h — top/bottom bars, left/right legs.
    private static List<(int[] Rect, string Slot)> RingPieces(int cw, int x, int z, int w, int h) =>
    [
        ([x, z, w, cw], ApproachSlots.Bar),                        // top
        ([x, z + h - cw, w, cw], ApproachSlots.Bar),               // bottom
        ([x, z + cw, cw, h - 2 * cw], ApproachSlots.Leg),          // left
        ([x + w - cw, z + cw, cw, h - 2 * cw], ApproachSlots.Leg), // right
    ];
}
