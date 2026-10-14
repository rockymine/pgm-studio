using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// The compound-layer emit↔derive mirror (docs/generator/model.md §5): every room-free compound
/// <see cref="CompoundEmitter"/> builds classifies back to itself through <see cref="ShapeClassifier.ClassifyCompound"/>.
/// Each emission must also be one connected mass, tile without overlap, and join along shared edges only — the
/// alignment law (§3), asserted as the absence of a diagonal pinch. The branch family is capped at three arms and
/// reads by arm count regardless of placement (an F and a Π both read <c>SpineArms(2)</c>).
/// </summary>
public sealed class CompoundMirrorTests
{
    private static HashSet<(int, int)> CellsOf(Compound compound)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var (r, _) in compound.Pieces)
            for (var x = r.X; x < r.X + r.Width; x++)
                for (var z = r.Z; z < r.Z + r.Height; z++) cells.Add((x, z));
        return cells;
    }

    private static int Overlaps(Compound compound)
    {
        var seen = new HashSet<(int, int)>(); var n = 0;
        foreach (var (r, _) in compound.Pieces)
            for (var x = r.X; x < r.X + r.Width; x++)
                for (var z = r.Z; z < r.Z + r.Height; z++) if (!seen.Add((x, z))) n++;
        return n;
    }

    private static async Task Wellformed(Compound compound)
    {
        var cells = CellsOf(compound);
        await Assert.That(Overlaps(compound)).IsEqualTo(0);                         // pieces tile without overlap
        await Assert.That(Cells.Components(cells)).IsEqualTo(1);                // one connected mass
        await Assert.That(Cells.HasDiagonalPinch(cells)).IsFalse();            // edge-aligned joins only (§3)
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task SpineArms_classifies_back_with_its_arm_count(int arms)
    {
        foreach (var laneWidth in new[] { 2, 3 })
        {
            var compound = CompoundEmitter.SpineArms(laneWidth, arms);
            await Wellformed(compound);
            var read = ShapeClassifier.ClassifyCompound(CellsOf(compound));
            await Assert.That(read.Form).IsEqualTo(CompoundForm.SpineArms);
            await Assert.That(read.Arms).IsEqualTo(arms);
        }
    }

    // arm placement is a knob, not an identity: F (an end + the middle) and Π (both ends) both read SpineArms(2);
    // E (three) reads SpineArms(3). The letter is the placement; the arm count is the topology.
    [Test]
    public async Task SpineArms_reads_by_count_regardless_of_placement()
    {
        const int laneWidth = 2;
        var pi = CompoundEmitter.SpineArms(laneWidth, [0, 4 * laneWidth], 5 * laneWidth);        // Π — arms at both ends
        var f = CompoundEmitter.SpineArms(laneWidth, [0, 2 * laneWidth], 5 * laneWidth);        // F — an end and the middle
        var e = CompoundEmitter.SpineArms(laneWidth, [0, 2 * laneWidth, 4 * laneWidth], 5 * laneWidth); // E — three arms
        foreach (var (compound, arms) in new[] { (pi, 2), (f, 2), (e, 3) })
        {
            await Wellformed(compound);
            var read = ShapeClassifier.ClassifyCompound(CellsOf(compound));
            await Assert.That(read.Form).IsEqualTo(CompoundForm.SpineArms);
            await Assert.That(read.Arms).IsEqualTo(arms);
        }
    }

    // the atom rectangles are free to differ in size — identity is width-independent, so an F with a fat bar and a
    // long + a short leg (different widths too) still reads SpineArms(2).
    [Test]
    public async Task SpineArms_reads_by_count_under_varied_atom_sizes()
    {
        var f = CompoundEmitter.SpineArms(spineLen: 14, barThickness: 4, arms: [(0, 4, 10), (8, 2, 4)]);
        await Wellformed(f);
        var read = ShapeClassifier.ClassifyCompound(CellsOf(f));
        await Assert.That(read.Form).IsEqualTo(CompoundForm.SpineArms);
        await Assert.That(read.Arms).IsEqualTo(2);
    }

    [Test]
    public async Task SpineArms_caps_at_three_arms()
    {
        await Assert.That(() => CompoundEmitter.SpineArms(2, 4)).Throws<ArgumentException>();
        await Assert.That(() => CompoundEmitter.SpineArms(2, [0, 2, 4, 6], 8)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Rectangle_classifies_back()
    {
        foreach (var laneWidth in new[] { 2, 3 })
        {
            var compound = CompoundEmitter.Rectangle(4 * laneWidth, 3 * laneWidth);
            await Wellformed(compound);
            await Assert.That(ShapeClassifier.ClassifyCompound(CellsOf(compound)).Form).IsEqualTo(CompoundForm.Rectangle);
        }
    }

    // one void: a clean ring vs a P (the loop overhangs a longer bar).
    [Test]
    public async Task Ring_and_P_split_on_the_overhang_at_one_void()
    {
        foreach (var laneWidth in new[] { 2, 3 })
        {
            var ring = CompoundEmitter.Ring(laneWidth, 4 * laneWidth, 4 * laneWidth);
            await Wellformed(ring);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(ring)))).IsEqualTo(1);
            await Assert.That(ShapeClassifier.ClassifyCompound(CellsOf(ring)).Form).IsEqualTo(CompoundForm.Ring);

            var p = CompoundEmitter.P(laneWidth, 4 * laneWidth, 4 * laneWidth);
            await Wellformed(p);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(p)))).IsEqualTo(1);
            await Assert.That(ShapeClassifier.ClassifyCompound(CellsOf(p)).Form).IsEqualTo(CompoundForm.P);
        }
    }

    // two voids: a double-hole (Ring + a docked U — a solid wall between the voids) vs two-U-on-I (an open channel).
    [Test]
    public async Task DoubleHole_and_TwoUOnI_split_on_the_channel_at_two_voids()
    {
        foreach (var laneWidth in new[] { 2, 3 })
        {
            var dh = CompoundEmitter.DoubleHole(laneWidth, 5 * laneWidth, 6 * laneWidth);
            await Wellformed(dh);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(dh)))).IsEqualTo(2);
            await Assert.That(ShapeClassifier.ClassifyCompound(CellsOf(dh)).Form).IsEqualTo(CompoundForm.DoubleHole);

            var twin = CompoundEmitter.TwoUOnI(laneWidth, 4 * laneWidth);
            await Wellformed(twin);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(twin)))).IsEqualTo(2);
            await Assert.That(ShapeClassifier.ClassifyCompound(CellsOf(twin)).Form).IsEqualTo(CompoundForm.TwoUOnI);
        }
    }

    // a G: a ring + an L — one enclosed void (the ring) plus an open three-walled bay (the L's recess). It reads
    // G, distinct from the plain Ring / P (which have one void and no bay) at the one-void branch.
    [Test]
    public async Task G_reads_one_void_plus_a_bay_distinct_from_ring_and_P()
    {
        foreach (var laneWidth in new[] { 2, 3 })
        {
            var g = CompoundEmitter.G(laneWidth, 4 * laneWidth, 4 * laneWidth, 6 * laneWidth);
            await Wellformed(g);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(g)))).IsEqualTo(1);   // only the ring is enclosed
            await Assert.That(ShapeClassifier.ClassifyCompound(CellsOf(g)).Form).IsEqualTo(CompoundForm.G);
        }
    }

    // the double-hole's holes can be equal (a full-height U flush with the ring's bars) or variant (a shorter U
    // that slides along the edge) — both keep two voids apart by the solid ring leg and read DoubleHole.
    [Test]
    public async Task DoubleHole_holes_can_be_equal_or_variant()
    {
        const int laneWidth = 2;
        // equal: a full-height U whose bay matches the ring interior exactly
        var equal = CompoundEmitter.DoubleHole(laneWidth, 4 * laneWidth, 5 * laneWidth, uW: 3 * laneWidth, uH: 5 * laneWidth, uz: 0);
        var holes = equal.Vacancies.Where(v => v.Kind == "hole").Select(v => (v.Rect.Width, v.Rect.Height)).ToList();
        await Wellformed(equal);
        await Assert.That(holes.Count).IsEqualTo(2);
        await Assert.That(holes[0]).IsEqualTo(holes[1]);                         // the two holes are the same size
        await Assert.That(ShapeClassifier.ClassifyCompound(CellsOf(equal)).Form).IsEqualTo(CompoundForm.DoubleHole);

        // variant: a shorter U slid down the edge, still two voids apart by the solid leg
        foreach (var uz in new[] { laneWidth, 2 * laneWidth, 3 * laneWidth })
        {
            var dh = CompoundEmitter.DoubleHole(laneWidth, 5 * laneWidth, 8 * laneWidth, uW: 2 * laneWidth, uH: 3 * laneWidth, uz: uz);
            await Wellformed(dh);
            await Assert.That(Cells.Components(Cells.EnclosedVoid(CellsOf(dh)))).IsEqualTo(2);
            await Assert.That(ShapeClassifier.ClassifyCompound(CellsOf(dh)).Form).IsEqualTo(CompoundForm.DoubleHole);
        }
    }
}
