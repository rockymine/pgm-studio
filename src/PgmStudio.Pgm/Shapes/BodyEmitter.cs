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
    public static ShapeBody Ring(int cw, int w, int h) => Ring(RingWalls.Uniform(cw), w, h);

    /// <summary>A ring at per-side <paramref name="walls"/> — the widened form: one side thicker than the rest is
    /// the same shape carrying more play through that side. The hole is whatever the walls leave, so widening a
    /// wall narrows the void rather than growing the box.</summary>
    public static ShapeBody Ring(RingWalls walls, int w, int h)
    {
        RequireRing(walls, w, h, "ring");
        return new ShapeBody(RingPieces(walls, 0, 0, w, h), [RingHole(walls, 0, 0, w, h)]);
    }

    /// <summary>The geometric requirement of a ring occupying <paramref name="w"/>×<paramref name="h"/> at
    /// <paramref name="walls"/>: every wall at least a corridor, and the opposing pairs leaving at least one cell
    /// of hole between them. It is <b>only</b> geometry — how far the widths may differ from each other is a
    /// sampling law the composer owns, not something an emitter should refuse.</summary>
    private static void RequireRing(RingWalls walls, int w, int h, string form)
    {
        if (walls.Min < 2) throw new ArgumentException($"corridor width {walls.Min} < 2.");
        if (w < walls.Left + walls.Right + 1 || h < walls.Top + walls.Bottom + 1)
            throw new ArgumentException(
                $"box {w}x{h} is too small for a {form} at walls {walls.Top}/{walls.Right}/{walls.Bottom}/{walls.Left}.");
    }

    /// <summary>A ring whose bottom bar is <b>longer</b> than its loop, so the loop sits on the bar with an
    /// overhang and can slide along it as long as the hole stays enclosed (the <c>P</c>/<c>b</c>/<c>d</c> glyph — a
    /// loop on a stem). One void. <paramref name="loopW"/>×<paramref name="ringH"/> is the loop; the bar overhangs
    /// it by <paramref name="barExtend"/> cells total, the loop centred on the overhang. Bars are
    /// <see cref="ApproachSlots.Bar"/>, the loop's sides <see cref="ApproachSlots.Leg"/>.</summary>
    public static ShapeBody P(int cw, int loopW, int ringH, int barExtend = 0) =>
        P(RingWalls.Uniform(cw), cw, loopW, ringH, barExtend);

    /// <summary>A P whose <b>loop</b> carries per-side <paramref name="walls"/>. <paramref name="cw"/> stays the
    /// corridor width the bar's overhang is measured in — only the loop's four walls widen, since the long bar
    /// past the loop is a run rather than a ring side.</summary>
    public static ShapeBody P(RingWalls walls, int cw, int loopW, int ringH, int barExtend = 0)
    {
        RequireRing(walls, loopW, ringH, "P loop");
        var ext = barExtend > 0 ? barExtend : 2 * cw;             // total overhang of the long bar past the loop
        var ox = ext / 2;                                         // loop offset — centred on the overhang
        var w = loopW + ext;
        var loop = RingWallRects(walls, ox, 0, loopW, ringH);
        var pieces = new List<(int[] Rect, string Slot)>
        {
            ([0, ringH - walls.Bottom, w, walls.Bottom], ApproachSlots.Bar),  // the long bottom bar (the loop slides along it)
            (loop.Top, ApproachSlots.Bar),                        // loop top bar
            (loop.Left, ApproachSlots.Leg),                       // loop left leg
            (loop.Right, ApproachSlots.Leg),                      // loop right leg
        };
        return new ShapeBody(pieces, [RingHole(walls, ox, 0, loopW, ringH)]);
    }

    /// <summary>A ring with a <b>U docked on its right edge</b> — the U's two arms land on the ring's right side and
    /// its bay, closed by that edge, is the second void (§5). The bay separates from the ring interior by the solid
    /// right leg, so the two holes can be <b>the same size</b> (a full-height U, arms flush with the ring's top and
    /// bottom bars) or <b>variant</b> (a shorter U that slides along the edge) — either way both stay enclosed.
    /// <paramref name="ringW"/>×<paramref name="ringH"/> is the ring; <paramref name="uW"/> the U's outward reach
    /// (bay width <c>uW−cw</c>), <paramref name="uH"/> its height, <paramref name="uz"/> its slide (0 = flush with
    /// the top bar; <c>-1</c> = a shorter, slid default). Bars are <see cref="ApproachSlots.Bar"/>, uprights
    /// <see cref="ApproachSlots.Leg"/>; two <c>hole</c> vacancies.</summary>
    public static ShapeBody DoubleHole(int cw, int ringW, int ringH, int uW = 0, int uH = 0, int uz = -1) =>
        DoubleHole(RingWalls.Uniform(cw), cw, ringW, ringH, uW, uH, uz);

    /// <summary>A double-hole whose <b>ring</b> carries per-side <paramref name="walls"/>. The docked U keeps
    /// <paramref name="cw"/>: its arms and outer wall are a corridor, and its bay is closed by the ring's right
    /// leg — so widening that leg thickens the wall between the two voids.</summary>
    public static ShapeBody DoubleHole(
        RingWalls walls, int cw, int ringW, int ringH, int uW = 0, int uH = 0, int uz = -1)
    {
        RequireRing(walls, ringW, ringH, "double-hole ring");
        int uw = uW > 0 ? uW : 2 * cw, uh = uH > 0 ? uH : 3 * cw, z = uz >= 0 ? uz : cw;
        if (uw < cw + 1) throw new ArgumentException($"the U reach {uw} leaves no bay (need > {cw}).");
        if (uh < 2 * cw + 1) throw new ArgumentException($"the U height {uh} leaves no bay at cw {cw}.");
        if (z < 0 || z + uh > ringH)
            throw new ArgumentException($"the U (z {z}, height {uh}) doesn't fit the ring's right edge (ring height {ringH}).");
        var pieces = RingPieces(walls, 0, 0, ringW, ringH);
        pieces.Add(([ringW, z, uw, cw], ApproachSlots.Bar));                       // U top arm (docks the ring's right leg)
        pieces.Add(([ringW, z + uh - cw, uw, cw], ApproachSlots.Bar));            // U bottom arm
        pieces.Add(([ringW + uw - cw, z + cw, cw, uh - 2 * cw], ApproachSlots.Leg)); // U outer wall (closes it)
        return new ShapeBody(pieces,
        [
            RingHole(walls, 0, 0, ringW, ringH),                 // hole 1 — the ring interior
            Hole(ringW, z + cw, uw - cw, uh - 2 * cw),           // hole 2 — the U's bay, closed by the ring edge
        ]);
    }

    /// <summary>A ring with an <b>L docked on its right edge</b> — the G glyph. Unlike the <see cref="DoubleHole"/>'s
    /// U (two arms closing an enclosed bay), the L is a single upright plus the shared bottom bar as its foot, so its
    /// recess is a <b>bay open at the top</b> (three walls: the ring's right leg, the L's upright, the bottom bar) —
    /// a second void the docking frontline seals flush into a <b>taller hole</b>, beside the ring's smaller one. The
    /// body carries one enclosed void (the ring); the bay is the derive-side second space. <paramref name="ringW"/>×
    /// <paramref name="ringH"/> is the ring; the upright sits at the box's right edge (width <paramref name="w"/>),
    /// the bay between it and the ring. Bars are <see cref="ApproachSlots.Bar"/>, uprights/legs
    /// <see cref="ApproachSlots.Leg"/>.</summary>
    public static ShapeBody G(int cw, int ringW, int ringH, int w) =>
        G(RingWalls.Uniform(cw), cw, ringW, ringH, w);

    /// <summary>A G whose <b>ring</b> carries per-side <paramref name="walls"/>. <paramref name="cw"/> stays the
    /// corridor width of the docked L — its upright and the bay it leaves are a corridor, not a ring side — while
    /// the shared top bar takes the ring's top wall across its whole span.</summary>
    public static ShapeBody G(RingWalls walls, int cw, int ringW, int ringH, int w)
    {
        RequireRing(walls, ringW, ringH, "G ring");
        if (w < ringW + cw + 1) throw new ArgumentException($"width {w} leaves no bay + upright past the ring {ringW} at cw {cw}.");
        // built bay-opening-DOWN (the shared bar on top, the L's foot part of it), so the hub's vertical flip turns
        // the open side toward the front — the branch convention, so a docking frontline seals the bay into a hole
        var ring = RingWallRects(walls, 0, 0, ringW, ringH);
        var pieces = new List<(int[] Rect, string Slot)>
        {
            ([0, 0, w, walls.Top], ApproachSlots.Bar),                   // the shared top bar (ring top + the L's foot)
            (ring.Bottom, ApproachSlots.Bar),                           // ring bottom bar
            (ring.Left, ApproachSlots.Leg),                             // ring left leg
            (ring.Right, ApproachSlots.Leg),                            // ring right leg — the bay's left wall
            ([w - cw, walls.Top, cw, ringH - walls.Top], ApproachSlots.Leg), // the L's upright — the bay's right wall, down to the open edge
        };
        return new ShapeBody(pieces, [RingHole(walls, 0, 0, ringW, ringH)]);   // one enclosed void (the ring); the bay opens down
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
    private static List<(int[] Rect, string Slot)> RingPieces(RingWalls walls, int x, int z, int w, int h)
    {
        var r = RingWallRects(walls, x, z, w, h);
        return
        [
            (r.Top, ApproachSlots.Bar),
            (r.Bottom, ApproachSlots.Bar),
            (r.Left, ApproachSlots.Leg),
            (r.Right, ApproachSlots.Leg),
        ];
    }

    /// <summary>
    /// The four wall rects of a ring occupying <c>(x, z, w, h)</c> at per-side <paramref name="walls"/>: the top
    /// and bottom bars span the full width, the legs fill between them.
    ///
    /// <para>This is <b>the</b> place ring wall geometry lives. Six forms are built from these four walls — the
    /// Ring and Double-hole directly, the P's loop, the G's ring, and the donut's (in
    /// <see cref="ShapeEmitter"/>) — and each assembles them in its own order with its own slot names, because
    /// piece order fixes emitted ids and the donut's walls are an entry/room bar rather than plain bars. So this
    /// owns <em>where</em> the walls are, not what they are called or in what order they are laid down; that
    /// split is what lets a per-side width be added once rather than in six copies.</para>
    /// </summary>
    public static (int[] Top, int[] Bottom, int[] Left, int[] Right) RingWallRects(
        RingWalls walls, int x, int z, int w, int h)
    {
        var mid = h - walls.Top - walls.Bottom;
        return (
            [x, z, w, walls.Top],
            [x, z + h - walls.Bottom, w, walls.Bottom],
            [x, z + walls.Top, walls.Left, mid],
            [x + w - walls.Right, z + walls.Top, walls.Right, mid]);
    }

    /// <summary>The enclosed void a ring at <paramref name="walls"/> leaves inside <c>(x, z, w, h)</c>.</summary>
    public static ShapeVacancy RingHole(RingWalls walls, int x, int z, int w, int h) =>
        Hole(x + walls.Left, z + walls.Top, w - walls.Left - walls.Right, h - walls.Top - walls.Bottom);
}
