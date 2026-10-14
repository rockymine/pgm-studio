using PgmStudio.Geom;

namespace PgmStudio.Pgm.Shapes;

/// <summary>
/// Emits the <b>room-free compounds</b> the vocabulary names (docs/generator/model.md §4) but the
/// approach emitter (<see cref="ShapeEmitter"/>) can't build, as pure <see cref="Compound"/> — structural-part-named
/// rects (<see cref="CompoundParts.Bar"/> spines/crossbars, <see cref="CompoundParts.Leg"/> arms/ring-arms) and,
/// for the holed forms, the enclosed voids as <c>hole</c> vacancies. No room, marker or id: these are the hub's
/// and the front line's forms.
///
/// <para>Everything is one atom recombined <b>along shared edges</b> (§3): a spine plus <c>K</c> arms
/// (<c>SpineArms</c> — T at K=1, Π/F at K=2, E at K=3, the arm placement a knob, <b>3 arms the maximum</b>),
/// four bars around a void (<c>Ring</c>), a ring with one longer bar so a loop slides along it
/// (<c>P</c>), a ring with a shorter U docked on its edge for a second void (<c>DoubleHole</c>), and
/// two loops on a shared baseline (<see cref="TwoUOnI"/>). Each classifies back to itself through
/// <see cref="ShapeClassifier.ClassifyCompound"/> — the emit↔derive mirror for the compound layer. Shapes are emitted in a
/// canonical frame (spines horizontal on top, arms hanging down; rings axis-aligned); a box too small for the
/// form throws.</para>
/// </summary>
public static class CompoundEmitter
{
    /// <summary>The most arms a spine carries (the branch family is capped at three).</summary>
    public const int MaxArms = 3;

    /// <summary>One filled <paramref name="w"/>×<paramref name="h"/> rectangle — a spine, the base of the ladder
    /// (the solid hub reads as this). A single <see cref="CompoundParts.Bar"/> piece, no void.</summary>
    public static Compound Rectangle(int w, int h)
    {
        if (w < 1 || h < 1) throw new ArgumentException($"a rectangle needs positive dims (got {w}x{h}).");
        return new Compound([new(new(0, 0, w, h), CompoundParts.Bar)], []);
    }

    /// <summary>A full-width spine (top, <paramref name="laneWidth"/> tall) with <paramref name="arms"/> equal arms
    /// hanging down, spread evenly with a one-corridor gap and overhang — K=1 a centred T, K=2 a Π, K=3 the E/comb.
    /// The <see cref="MaxArms">arm count</see> is capped at three. For a specific placement (an F, an off-centre
    /// arm) use the <see cref="SpineArms(int, IReadOnlyList{int}, int, int)"/> overload.</summary>
    public static Compound SpineArms(int laneWidth, int arms, int armLen = 0)
    {
        if (arms < 1 || arms > MaxArms)
            throw new ArgumentException($"the branch family supports 1..{MaxArms} arms (requested {arms}).");
        var starts = new int[arms];
        for (var i = 0; i < arms; i++) starts[i] = laneWidth + i * 2 * laneWidth;   // laneWidth overhang, one gap apart
        return SpineArms(laneWidth, starts, (2 * arms + 1) * laneWidth, armLen);
    }

    /// <summary>A full-width spine of <paramref name="spineLen"/> (top, <paramref name="laneWidth"/> tall) with uniform
    /// arms hanging down at the given <paramref name="armStarts"/> columns — the placement knob that reads L/T/Π/F/E
    /// off one family (arms at the ends → Π, an end + the middle → F, three → E). A convenience over the general
    /// <see cref="SpineArms(int, int, IReadOnlyList{ValueTuple{int, int, int}})"/> with every arm one corridor wide
    /// and the same length.</summary>
    public static Compound SpineArms(int laneWidth, IReadOnlyList<int> armStarts, int spineLen, int armLen = 0)
    {
        if (laneWidth < 2) throw new ArgumentException($"lane width {laneWidth} < 2.");
        var al = armLen > 0 ? armLen : 3 * laneWidth;
        return SpineArms(spineLen, laneWidth, armStarts.Select(s => (s, laneWidth, al)).ToList());
    }

    /// <summary>A spine (top, <paramref name="barThickness"/> tall, <paramref name="spineLen"/> wide) with arms
    /// hanging down, each atom rectangle <b>free to differ in size</b> — <paramref name="arms"/> gives each arm's
    /// <c>(Start, Width, Length)</c>, and the bar its own thickness — so an F can carry a long and a short leg on a
    /// fat bar, etc. Identity is <b>width-independent</b>: whatever the sizes, the classifier reads it by its arm
    /// count (an uneven F still reads <c>SpineArms(2)</c>). Arms must sit within the spine, a gap apart (so each is
    /// a distinct run), and number at most <see cref="MaxArms"/>. Spine is a <see cref="CompoundParts.Bar"/>, each
    /// arm a <see cref="CompoundParts.Leg"/>.</summary>
    public static Compound SpineArms(int spineLen, int barThickness, IReadOnlyList<(int Start, int Width, int Length)> arms)
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
        var pieces = new List<Piece> { new(new(0, 0, spineLen, barThickness), CompoundParts.Bar) };
        foreach (var (s, w, len) in arms) pieces.Add(new(new(s, barThickness, w, len), CompoundParts.Leg));
        return new Compound(pieces, []);
    }

    /// <summary>A rectangular ring — four bars around one enclosed void (the donut compound, room-free). Top and
    /// bottom are <see cref="CompoundParts.Bar"/>, the sides <see cref="CompoundParts.Leg"/>; the hole is a
    /// <c>hole</c> vacancy.</summary>
    public static Compound Ring(int laneWidth, int w, int h) => Ring(RingWalls.Uniform(laneWidth), w, h);

    /// <summary>A ring at per-side <paramref name="walls"/> — the widened form: one side thicker than the rest is
    /// the same shape carrying more play through that side. The hole is whatever the walls leave, so widening a
    /// wall narrows the void rather than growing the box.</summary>
    public static Compound Ring(RingWalls walls, int w, int h)
    {
        RequireRing(walls, w, h, "ring");
        return new Compound(RingPieces(walls, 0, 0, w, h), [RingHole(walls, 0, 0, w, h)]);
    }

    /// <summary>The geometric requirement of a ring occupying <paramref name="w"/>×<paramref name="h"/> at
    /// <paramref name="walls"/>: every wall at least a corridor, and the opposing pairs leaving at least one cell
    /// of hole between them. It is <b>only</b> geometry — how far the widths may differ from each other is a
    /// sampling law the composer owns, not something an emitter should refuse.</summary>
    private static void RequireRing(RingWalls walls, int w, int h, string form)
    {
        if (walls.Min < 2) throw new ArgumentException($"lane width {walls.Min} < 2.");
        if (w < walls.Left + walls.Right + 1 || h < walls.Top + walls.Bottom + 1)
            throw new ArgumentException(
                $"box {w}x{h} is too small for a {form} at walls {walls.Top}/{walls.Right}/{walls.Bottom}/{walls.Left}.");
    }

    /// <summary>A ring whose bottom bar is <b>longer</b> than its loop, so the loop sits on the bar with an
    /// overhang and can slide along it as long as the hole stays enclosed (the <c>P</c>/<c>b</c>/<c>d</c> glyph — a
    /// loop on a stem). One void. <paramref name="loopW"/>×<paramref name="ringH"/> is the loop; the bar overhangs
    /// it by <paramref name="barExtend"/> cells total, the loop centred on the overhang. Bars are
    /// <see cref="CompoundParts.Bar"/>, the loop's sides <see cref="CompoundParts.Leg"/>.</summary>
    public static Compound P(int laneWidth, int loopW, int ringH, int barExtend = 0) =>
        P(RingWalls.Uniform(laneWidth), laneWidth, loopW, ringH, barExtend);

    /// <summary>A P whose <b>loop</b> carries per-side <paramref name="walls"/>. <paramref name="laneWidth"/> stays the
    /// lane width the bar's overhang is measured in — only the loop's four walls widen, since the long bar
    /// past the loop is a run rather than a ring side.</summary>
    public static Compound P(RingWalls walls, int laneWidth, int loopW, int ringH, int barExtend = 0)
    {
        RequireRing(walls, loopW, ringH, "P loop");
        var ext = barExtend > 0 ? barExtend : 2 * laneWidth;             // total overhang of the long bar past the loop
        var ox = ext / 2;                                         // loop offset — centred on the overhang
        var w = loopW + ext;
        var loop = RingWallRects(walls, ox, 0, loopW, ringH);
        var pieces = new List<Piece>
        {
            new(new(0, ringH - walls.Bottom, w, walls.Bottom), CompoundParts.Bar),  // the long bottom bar (the loop slides along it)
            new(loop.Top, CompoundParts.Bar),                        // loop top bar
            new(loop.Left, CompoundParts.Leg),                       // loop left leg
            new(loop.Right, CompoundParts.Leg),                      // loop right leg
        };
        return new Compound(pieces, [RingHole(walls, ox, 0, loopW, ringH)]);
    }

    /// <summary>A ring with a <b>U docked on its right edge</b> — the U's two arms land on the ring's right side and
    /// its bay, closed by that edge, is the second void (§5). The bay separates from the ring interior by the solid
    /// right leg, so the two holes can be <b>the same size</b> (a full-height U, arms flush with the ring's top and
    /// bottom bars) or <b>variant</b> (a shorter U that slides along the edge) — either way both stay enclosed.
    /// <paramref name="ringW"/>×<paramref name="ringH"/> is the ring; <paramref name="uW"/> the U's outward reach
    /// (bay width <c>uW−laneWidth</c>), <paramref name="uH"/> its height, <paramref name="uz"/> its slide (0 = flush with
    /// the top bar; <c>-1</c> = a shorter, slid default). Bars are <see cref="CompoundParts.Bar"/>, uprights
    /// <see cref="CompoundParts.Leg"/>; two <c>hole</c> vacancies.</summary>
    public static Compound DoubleHole(int laneWidth, int ringW, int ringH, int uW = 0, int uH = 0, int uz = -1) =>
        DoubleHole(RingWalls.Uniform(laneWidth), laneWidth, ringW, ringH, uW, uH, uz);

    /// <summary>A double-hole whose <b>ring</b> carries per-side <paramref name="walls"/>. The docked U keeps
    /// <paramref name="laneWidth"/>: its arms and outer wall are a corridor, and its bay is closed by the ring's right
    /// leg — so widening that leg thickens the wall between the two voids.</summary>
    public static Compound DoubleHole(
        RingWalls walls, int laneWidth, int ringW, int ringH, int uW = 0, int uH = 0, int uz = -1)
    {
        RequireRing(walls, ringW, ringH, "double-hole ring");
        int uw = uW > 0 ? uW : 2 * laneWidth, uh = uH > 0 ? uH : 3 * laneWidth, z = uz >= 0 ? uz : laneWidth;
        if (uw < laneWidth + 1) throw new ArgumentException($"the U reach {uw} leaves no bay (need > {laneWidth}).");
        if (uh < 2 * laneWidth + 1) throw new ArgumentException($"the U height {uh} leaves no bay at laneWidth {laneWidth}.");
        if (z < 0 || z + uh > ringH)
            throw new ArgumentException($"the U (z {z}, height {uh}) doesn't fit the ring's right edge (ring height {ringH}).");
        var pieces = RingPieces(walls, 0, 0, ringW, ringH);
        pieces.Add(new(new(ringW, z, uw, laneWidth), CompoundParts.Bar));                       // U top arm (docks the ring's right leg)
        pieces.Add(new(new(ringW, z + uh - laneWidth, uw, laneWidth), CompoundParts.Bar));            // U bottom arm
        pieces.Add(new(new(ringW + uw - laneWidth, z + laneWidth, laneWidth, uh - 2 * laneWidth), CompoundParts.Leg)); // U outer wall (closes it)
        return new Compound(pieces,
        [
            RingHole(walls, 0, 0, ringW, ringH),                 // hole 1 — the ring interior
            Hole(ringW, z + laneWidth, uw - laneWidth, uh - 2 * laneWidth),           // hole 2 — the U's bay, closed by the ring edge
        ]);
    }

    /// <summary>A ring with an <b>L docked on its right edge</b> — the G glyph. Unlike the <c>DoubleHole</c>'s
    /// U (two arms closing an enclosed bay), the L is a single upright plus the shared bottom bar as its foot, so its
    /// recess is a <b>bay open at the top</b> (three walls: the ring's right leg, the L's upright, the bottom bar) —
    /// a second void the docking frontline seals flush into a <b>taller hole</b>, beside the ring's smaller one. The
    /// compound carries one enclosed void (the ring); the bay is the derive-side second space. <paramref name="ringW"/>×
    /// <paramref name="ringH"/> is the ring; the upright sits at the box's right edge (width <paramref name="w"/>),
    /// the bay between it and the ring. Bars are <see cref="CompoundParts.Bar"/>, uprights/legs
    /// <see cref="CompoundParts.Leg"/>.</summary>
    public static Compound G(int laneWidth, int ringW, int ringH, int w) =>
        G(RingWalls.Uniform(laneWidth), laneWidth, ringW, ringH, w);

    /// <summary>A G whose <b>ring</b> carries per-side <paramref name="walls"/>. <paramref name="laneWidth"/> stays the
    /// lane width of the docked L — its upright and the bay it leaves are a corridor, not a ring side — while
    /// the shared top bar takes the ring's top wall across its whole span.</summary>
    public static Compound G(RingWalls walls, int laneWidth, int ringW, int ringH, int w)
    {
        RequireRing(walls, ringW, ringH, "G ring");
        if (w < ringW + laneWidth + 1) throw new ArgumentException($"width {w} leaves no bay + upright past the ring {ringW} at laneWidth {laneWidth}.");
        // built bay-opening-DOWN (the shared bar on top, the L's foot part of it), so the hub's vertical flip turns
        // the open side toward the front — the branch convention, so a docking frontline seals the bay into a hole
        var ring = RingWallRects(walls, 0, 0, ringW, ringH);
        var pieces = new List<Piece>
        {
            new(new(0, 0, w, walls.Top), CompoundParts.Bar),                   // the shared top bar (ring top + the L's foot)
            new(ring.Bottom, CompoundParts.Bar),                           // ring bottom bar
            new(ring.Left, CompoundParts.Leg),                             // ring left leg
            new(ring.Right, CompoundParts.Leg),                            // ring right leg — the bay's left wall
            new(new(w - laneWidth, walls.Top, laneWidth, ringH - walls.Top), CompoundParts.Leg), // the L's upright — the bay's right wall, down to the open edge
        };
        return new Compound(pieces, [RingHole(walls, 0, 0, ringW, ringH)]);   // one enclosed void (the ring); the bay opens down
    }

    /// <summary>Two loops sharing one baseline — two enclosed voids kept apart by an <b>open</b> channel (twin
    /// loops on an I, §5). A full-width baseline <see cref="CompoundParts.Bar"/>, two top-bar
    /// <see cref="CompoundParts.Bar"/>s (one per loop) with a gap between them, and four
    /// <see cref="CompoundParts.Leg"/> uprights; two <c>hole</c> vacancies. <paramref name="hole"/> is each void's
    /// width, <paramref name="gap"/> the open channel width.</summary>
    public static Compound TwoUOnI(int laneWidth, int h, int hole = 0, int gap = 0)
    {
        if (laneWidth < 2) throw new ArgumentException($"lane width {laneWidth} < 2.");
        if (h < 3 * laneWidth) throw new ArgumentException($"height {h} is too small for two-U-on-I at laneWidth {laneWidth}.");
        int hw = hole > 0 ? hole : laneWidth, gp = gap > 0 ? gap : laneWidth;
        int l0 = 0, l1 = laneWidth + hw, l2 = 2 * laneWidth + hw + gp, l3 = 3 * laneWidth + hw + gp + hw;
        int w = l3 + laneWidth, legH = h - 2 * laneWidth, inner = laneWidth;
        var pieces = new List<Piece>
        {
            new(new(0, h - laneWidth, w, laneWidth), CompoundParts.Bar),                // shared baseline (bottom)
            new(new(l0, 0, 2 * laneWidth + hw, laneWidth), CompoundParts.Bar),         // left loop top bar (over l0..l1)
            new(new(l2, 0, 2 * laneWidth + hw, laneWidth), CompoundParts.Bar),         // right loop top bar (over l2..l3)
            new(new(l0, inner, laneWidth, legH), CompoundParts.Leg),
            new(new(l1, inner, laneWidth, legH), CompoundParts.Leg),
            new(new(l2, inner, laneWidth, legH), CompoundParts.Leg),
            new(new(l3, inner, laneWidth, legH), CompoundParts.Leg),
        };
        return new Compound(pieces,
        [
            Hole(l0 + laneWidth, inner, hw, legH),                        // left void
            Hole(l2 + laneWidth, inner, hw, legH),                       // right void
        ]);
    }

    private static ShapeVacancy Hole(int x, int z, int w, int h) =>
        new("hole", new(x, z, w, h), null, [CompoundParts.Bar, CompoundParts.Bar, CompoundParts.Leg, CompoundParts.Leg]);

    // the four bars of a rectangular ring at (x, z), size w×h — top/bottom bars, left/right legs.
    private static List<Piece> RingPieces(RingWalls walls, int x, int z, int w, int h)
    {
        var r = RingWallRects(walls, x, z, w, h);
        return
        [
            new(r.Top, CompoundParts.Bar),
            new(r.Bottom, CompoundParts.Bar),
            new(r.Left, CompoundParts.Leg),
            new(r.Right, CompoundParts.Leg),
        ];
    }

    /// <summary>
    /// The four wall rects of a ring occupying <c>(x, z, w, h)</c> at per-side <paramref name="walls"/>: the top
    /// and bottom bars span the full width, the legs fill between them.
    ///
    /// <para>This is <b>the</b> place ring wall geometry lives. Six forms are built from these four walls — the
    /// Ring and Double-hole directly, the P's loop, the G's ring, and the donut's (in
    /// <see cref="ShapeEmitter"/>) — and each assembles them in its own order with its own part names, because
    /// piece order fixes emitted ids and the donut's walls are an entry/room bar rather than plain bars. So this
    /// owns <em>where</em> the walls are, not what they are called or in what order they are laid down; that
    /// split is what lets a per-side width be added once rather than in six copies.</para>
    /// </summary>
    public static (CellRect Top, CellRect Bottom, CellRect Left, CellRect Right) RingWallRects(
        RingWalls walls, int x, int z, int w, int h)
    {
        var mid = h - walls.Top - walls.Bottom;
        return (
            new(x, z, w, walls.Top),
            new(x, z + h - walls.Bottom, w, walls.Bottom),
            new(x, z + walls.Top, walls.Left, mid),
            new(x + w - walls.Right, z + walls.Top, walls.Right, mid));
    }

    /// <summary>The enclosed void a ring at <paramref name="walls"/> leaves inside <c>(x, z, w, h)</c>.</summary>
    public static ShapeVacancy RingHole(RingWalls walls, int x, int z, int w, int h) =>
        Hole(x + walls.Left, z + walls.Top, w - walls.Left - walls.Right, h - walls.Top - walls.Bottom);
}
