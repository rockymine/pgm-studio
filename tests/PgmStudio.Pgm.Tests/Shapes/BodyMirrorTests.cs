using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// The body-layer emit↔derive mirror (docs/contracts/shape-vocabulary.md §5): every terminal-free compound
/// <see cref="BodyEmitter"/> builds classifies back to itself through <see cref="ShapeClassifier.ClassifyBody"/>.
/// Each emission must also be one connected mass, tile without overlap, and join along shared edges only — the
/// alignment law (§3), asserted as the absence of a diagonal pinch. The branch family is capped at three arms and
/// reads by arm count regardless of placement (an F and a Π both read <c>SpineArms(2)</c>).
/// </summary>
public sealed class BodyMirrorTests
{
    private static HashSet<(int, int)> CellsOf(ShapeBody body)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var (r, _) in body.Pieces)
            for (var x = r[0]; x < r[0] + r[2]; x++)
                for (var z = r[1]; z < r[1] + r[3]; z++) cells.Add((x, z));
        return cells;
    }

    private static int Overlaps(ShapeBody body)
    {
        var seen = new HashSet<(int, int)>(); var n = 0;
        foreach (var (r, _) in body.Pieces)
            for (var x = r[0]; x < r[0] + r[2]; x++)
                for (var z = r[1]; z < r[1] + r[3]; z++) if (!seen.Add((x, z))) n++;
        return n;
    }

    private static async Task Wellformed(ShapeBody body)
    {
        var cells = CellsOf(body);
        await Assert.That(Overlaps(body)).IsEqualTo(0);                         // pieces tile without overlap
        await Assert.That(Cells.Components(cells)).IsEqualTo(1);                // one connected mass
        await Assert.That(Cells.HasDiagonalPinch(cells)).IsFalse();            // edge-aligned joins only (§3)
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task SpineArms_classifies_back_with_its_arm_count(int arms)
    {
        foreach (var cw in new[] { 2, 3 })
        {
            var body = BodyEmitter.SpineArms(cw, arms);
            await Wellformed(body);
            var read = ShapeClassifier.ClassifyBody(CellsOf(body));
            await Assert.That(read.Form).IsEqualTo(Compound.SpineArms);
            await Assert.That(read.Arms).IsEqualTo(arms);
        }
    }

    // arm placement is a knob, not an identity: F (an end + the middle) and Π (both ends) both read SpineArms(2);
    // E (three) reads SpineArms(3). The letter is the placement; the arm count is the topology.
    [Test]
    public async Task SpineArms_reads_by_count_regardless_of_placement()
    {
        const int cw = 2;
        var pi = BodyEmitter.SpineArms(cw, [0, 4 * cw], 5 * cw);        // Π — arms at both ends
        var f = BodyEmitter.SpineArms(cw, [0, 2 * cw], 5 * cw);        // F — an end and the middle
        var e = BodyEmitter.SpineArms(cw, [0, 2 * cw, 4 * cw], 5 * cw); // E — three arms
        foreach (var (body, arms) in new[] { (pi, 2), (f, 2), (e, 3) })
        {
            await Wellformed(body);
            var read = ShapeClassifier.ClassifyBody(CellsOf(body));
            await Assert.That(read.Form).IsEqualTo(Compound.SpineArms);
            await Assert.That(read.Arms).IsEqualTo(arms);
        }
    }

    // the atom rectangles are free to differ in size — identity is width-independent, so an F with a fat bar and a
    // long + a short leg (different widths too) still reads SpineArms(2).
    [Test]
    public async Task SpineArms_reads_by_count_under_varied_atom_sizes()
    {
        var f = BodyEmitter.SpineArms(spineLen: 14, barThickness: 4, arms: [(0, 4, 10), (8, 2, 4)]);
        await Wellformed(f);
        var read = ShapeClassifier.ClassifyBody(CellsOf(f));
        await Assert.That(read.Form).IsEqualTo(Compound.SpineArms);
        await Assert.That(read.Arms).IsEqualTo(2);
    }

    [Test]
    public async Task SpineArms_caps_at_three_arms()
    {
        await Assert.That(() => BodyEmitter.SpineArms(2, 4)).Throws<ArgumentException>();
        await Assert.That(() => BodyEmitter.SpineArms(2, [0, 2, 4, 6], 8)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Rectangle_classifies_back()
    {
        foreach (var cw in new[] { 2, 3 })
        {
            var body = BodyEmitter.Rectangle(4 * cw, 3 * cw);
            await Wellformed(body);
            await Assert.That(ShapeClassifier.ClassifyBody(CellsOf(body)).Form).IsEqualTo(Compound.Rectangle);
        }
    }

    // one void: a clean ring vs a P (the loop overhangs a longer bar).
    [Test]
    public async Task Ring_and_P_split_on_the_overhang_at_one_void()
    {
        foreach (var cw in new[] { 2, 3 })
        {
            var ring = BodyEmitter.Ring(cw, 4 * cw, 4 * cw);
            await Wellformed(ring);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(ring)))).IsEqualTo(1);
            await Assert.That(ShapeClassifier.ClassifyBody(CellsOf(ring)).Form).IsEqualTo(Compound.Ring);

            var p = BodyEmitter.P(cw, 4 * cw, 4 * cw);
            await Wellformed(p);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(p)))).IsEqualTo(1);
            await Assert.That(ShapeClassifier.ClassifyBody(CellsOf(p)).Form).IsEqualTo(Compound.P);
        }
    }

    // two voids: a double-hole (Ring + a docked U — a solid wall between the voids) vs two-U-on-I (an open channel).
    [Test]
    public async Task DoubleHole_and_TwoUOnI_split_on_the_channel_at_two_voids()
    {
        foreach (var cw in new[] { 2, 3 })
        {
            var dh = BodyEmitter.DoubleHole(cw, 5 * cw, 6 * cw);
            await Wellformed(dh);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(dh)))).IsEqualTo(2);
            await Assert.That(ShapeClassifier.ClassifyBody(CellsOf(dh)).Form).IsEqualTo(Compound.DoubleHole);

            var twin = BodyEmitter.TwoUOnI(cw, 4 * cw);
            await Wellformed(twin);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(twin)))).IsEqualTo(2);
            await Assert.That(ShapeClassifier.ClassifyBody(CellsOf(twin)).Form).IsEqualTo(Compound.TwoUOnI);
        }
    }

    // a G: a ring + an L — one enclosed void (the ring) plus an open three-walled bay (the L's recess). It reads
    // G, distinct from the plain Ring / P (which have one void and no bay) at the one-void branch.
    [Test]
    public async Task G_reads_one_void_plus_a_bay_distinct_from_ring_and_P()
    {
        foreach (var cw in new[] { 2, 3 })
        {
            var g = BodyEmitter.G(cw, 4 * cw, 4 * cw, 6 * cw);
            await Wellformed(g);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(g)))).IsEqualTo(1);   // only the ring is enclosed
            await Assert.That(ShapeClassifier.ClassifyBody(CellsOf(g)).Form).IsEqualTo(Compound.G);
        }
    }

    // the double-hole's holes can be equal (a full-height U flush with the ring's bars) or variant (a shorter U
    // that slides along the edge) — both keep two voids apart by the solid ring leg and read DoubleHole.
    [Test]
    public async Task DoubleHole_holes_can_be_equal_or_variant()
    {
        const int cw = 2;
        // equal: a full-height U whose bay matches the ring interior exactly
        var equal = BodyEmitter.DoubleHole(cw, 4 * cw, 5 * cw, uW: 3 * cw, uH: 5 * cw, uz: 0);
        var holes = equal.Vacancies.Where(v => v.Kind == "hole").Select(v => (v.Rect[2], v.Rect[3])).ToList();
        await Wellformed(equal);
        await Assert.That(holes.Count).IsEqualTo(2);
        await Assert.That(holes[0]).IsEqualTo(holes[1]);                         // the two holes are the same size
        await Assert.That(ShapeClassifier.ClassifyBody(CellsOf(equal)).Form).IsEqualTo(Compound.DoubleHole);

        // variant: a shorter U slid down the edge, still two voids apart by the solid leg
        foreach (var uz in new[] { cw, 2 * cw, 3 * cw })
        {
            var dh = BodyEmitter.DoubleHole(cw, 5 * cw, 8 * cw, uW: 2 * cw, uH: 3 * cw, uz: uz);
            await Wellformed(dh);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(dh)))).IsEqualTo(2);
            await Assert.That(ShapeClassifier.ClassifyBody(CellsOf(dh)).Form).IsEqualTo(Compound.DoubleHole);
        }
    }
}
