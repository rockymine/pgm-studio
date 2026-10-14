using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// The emit↔derive mirror (docs/generator/model.md §5.4): emit every base family (and its variants)
/// with <see cref="WoolBoxEmitter"/> and read each back with <see cref="ShapeClassifier"/> — requested ==
/// derived is the mirror closing, on <b>one</b> <see cref="CompoundForm"/> enum (no string bridge). It is a
/// <b>true mirror</b>: each emission is also fed through <see cref="PartAssignment"/> to re-derive every
/// piece's part from topology and assert it equals the part the emitter stamped (§5.3). The emitted pieces
/// must also tile without overlap.
/// </summary>
public sealed class ShapeMirrorTests
{
    private static readonly CompoundForm[] Emittable =
        [CompoundForm.I, CompoundForm.L, CompoundForm.Z, CompoundForm.Scythe, CompoundForm.Clamp, CompoundForm.U, CompoundForm.H, CompoundForm.Donut];

    // a cell covered by more than one piece is a bug
    private static int Overlaps(Compound a)
    {
        var seen = new HashSet<(int, int)>(); int n = 0;
        foreach (var p in a.Pieces)
            for (var x = p.Rect.X; x < p.Rect.X + p.Rect.Width; x++)
                for (var z = p.Rect.Z; z < p.Rect.Z + p.Rect.Height; z++) if (!seen.Add((x, z))) n++;
        return n;
    }

    private static CompoundForm? Derive(Compound a) =>
        ShapeClassifier.Classify(UnitPieces.AsPlan(a, "wool-box"), "wa-room").Family;

    // emitting SUCCEEDS -> classify reads back the requested family, no overlaps, and every piece's part
    // re-derived from topology equals the part the emitter stamped (the true mirror). Too-small boxes throw
    // ArgumentException and are not failures (the caller sizes the box to the family).
    private static async Task MirrorOk(CompoundForm family, Func<Compound> emit)
    {
        Compound a;
        try { a = emit(); } catch (ArgumentException) { return; }
        var derived = Derive(a) ?? throw new InvalidOperationException($"no ground reaches the {family}'s room");
        await Assert.That(derived).IsEqualTo(family);
        await Assert.That(Overlaps(a)).IsEqualTo(0);

        var pieces = a.Pieces.Select((p, i) => ($"p{i}", p.Rect)).ToList();
        var parts = PartAssignment.AssignParts(derived, pieces, $"p{a.Pieces.Count - 1}");
        for (var i = 0; i < a.Pieces.Count; i++)
            await Assert.That(parts[$"p{i}"]).IsEqualTo(a.Pieces[i].Part);
    }

    [Test]
    [Arguments(CompoundForm.L)]
    [Arguments(CompoundForm.Z)]
    [Arguments(CompoundForm.Donut)]
    public async Task A_flip_mirrors_the_entry_along_the_mouth(CompoundForm family)
    {
        // a single-entry family docked on a box's top: flipped, its entry lands at the mirror position along
        // that edge, so an overhanging dock can hang the compound either way
        const int laneWidth = 3;
        var (along, depth) = WoolBoxEmitter.MouthBox(family, laneWidth);
        var box = new PgmStudio.Pgm.Compose.Box("probe", BoxKind.Wool, new CellRect(0, 0, along + 2, depth + 2), 0);
        var plain = BoxFiller.EntryOn(box, BoxEdge.Top, laneWidth, family);
        var flipped = BoxFiller.EntryOn(box, BoxEdge.Top, laneWidth, family, flip: true);
        await Assert.That(plain).IsNotNull();
        await Assert.That(flipped!.Start).IsEqualTo(along + 2 - plain!.Start - plain.Length);
        await Assert.That(flipped.Start).IsNotEqualTo(plain.Start);
    }

    [Test]
    [Arguments(CompoundForm.I)]
    [Arguments(CompoundForm.L)]
    [Arguments(CompoundForm.Z)]
    [Arguments(CompoundForm.Scythe)]
    [Arguments(CompoundForm.Clamp)]
    [Arguments(CompoundForm.U)]
    [Arguments(CompoundForm.H)]
    [Arguments(CompoundForm.Donut)]
    public async Task Emitted_family_classifies_back(CompoundForm family)
    {
        foreach (var laneWidth in new[] { 2, 3 })
            foreach (var (W, H) in new[] { (12, 16), (16, 22) })
                foreach (var flip in new[] { false, true })
                    await MirrorOk(family, () => ShapeEmitter.Emit(family, W, H, laneWidth, flip));
    }

    // a uniformly-widened approach reads the same family — the classifier counts turns on the terrain outline,
    // so width is invariant. Emit each thin family, then a ×2 and ×3 scaled twin, at the matching reference width.
    [Test]
    [Arguments(CompoundForm.I)]
    [Arguments(CompoundForm.L)]
    [Arguments(CompoundForm.Z)]
    [Arguments(CompoundForm.Scythe)]
    [Arguments(CompoundForm.Clamp)]
    [Arguments(CompoundForm.U)]
    [Arguments(CompoundForm.H)]
    public async Task Width_invariance_under_uniform_scaling(CompoundForm family)
    {
        foreach (var k in new[] { 1, 2, 3 })
            await MirrorOk(family, () => ShapeEmitter.Emit(family, 6 * (2 * k), 10 * (2 * k), 2 * k));
    }

    [Test]
    public async Task Donut_variants_classify_back()
    {
        foreach (var laneWidth in new[] { 2, 3 })
        {
            var db = (W: 6 * laneWidth + 4, H: 5 * laneWidth);
            await MirrorOk(CompoundForm.Donut, () => ShapeEmitter.Emit(CompoundForm.Donut, db.W, db.H, laneWidth, attachments: 1));
            await MirrorOk(CompoundForm.Donut, () => ShapeEmitter.Emit(CompoundForm.Donut, db.W, db.H, laneWidth, attachments: 2));
            await MirrorOk(CompoundForm.Donut, () => ShapeEmitter.Emit(CompoundForm.Donut, db.W, db.H, laneWidth, woolExtend: true));
            await MirrorOk(CompoundForm.Donut, () => ShapeEmitter.Emit(CompoundForm.Donut, db.W, db.H, laneWidth, woolAtEnd: true));
            var dbw = (W: 6 * laneWidth + 4, H: 7 * laneWidth);   // taller box for wide attachments
            await MirrorOk(CompoundForm.Donut, () => ShapeEmitter.Emit(CompoundForm.Donut, dbw.W, dbw.H, laneWidth, attachmentWidth: 2 * laneWidth));
            await MirrorOk(CompoundForm.Donut, () => ShapeEmitter.Emit(CompoundForm.Donut, dbw.W, dbw.H, laneWidth, attachments: 2, attachmentWidth: 3 * laneWidth));
            // a SINGLE wide attachment needs only its own minimal ring (aw + laneWidth tall), not the two-stub height
            var dmin = (W: 6 * laneWidth + 4, H: 3 * laneWidth);
            await MirrorOk(CompoundForm.Donut, () => ShapeEmitter.Emit(CompoundForm.Donut, dmin.W, dmin.H, laneWidth, attachmentWidth: 2 * laneWidth));
        }
    }

    [Test]
    public async Task U_and_H_variants_classify_back()
    {
        foreach (var laneWidth in new[] { 2, 3 })
        {
            var ub = (W: 4 * laneWidth, H: 5 * laneWidth);
            await MirrorOk(CompoundForm.U, () => ShapeEmitter.Emit(CompoundForm.U, ub.W, ub.H, laneWidth));
            await MirrorOk(CompoundForm.U, () => ShapeEmitter.Emit(CompoundForm.U, ub.W, ub.H, laneWidth, woolAtEnd: true));
            var hb = (W: 4 * laneWidth, H: 6 * laneWidth);
            await MirrorOk(CompoundForm.H, () => ShapeEmitter.Emit(CompoundForm.H, hb.W, hb.H, laneWidth));
            await MirrorOk(CompoundForm.H, () => ShapeEmitter.Emit(CompoundForm.H, hb.W, hb.H, laneWidth, woolAtEnd: true));
        }
    }

    // side-tuck: a straight lane with the room off its side still reads I (the room is excluded from the bends),
    // and the room shares a corridor-width edge with the lane rather than capping its end.
    [Test]
    public async Task Side_tuck_reads_I_with_the_room_off_the_side()
    {
        foreach (var laneWidth in new[] { 2, 3 })
            foreach (var (W, H) in new[] { (12, 16), (16, 22) })
                foreach (var flip in new[] { false, true })
                {
                    Compound a;
                    try { a = ShapeEmitter.Emit(CompoundForm.I, W, H, laneWidth, flip, RoomPlacement.SideTuck); }
                    catch (ArgumentException) { continue; }
                    CellRect lane = a.Pieces[0].Rect, rm = a.Room!.Rect;
                    var side = rm.X == lane.X + lane.Width || rm.X + rm.Width == lane.X;
                    await Assert.That(Derive(a)).IsEqualTo(CompoundForm.I);
                    await Assert.That(side).IsTrue();
                    await Assert.That(Overlaps(a)).IsEqualTo(0);
                }
    }

    [Test]
    public async Task Emitter_refuses_the_room_free_forms()
    {
        foreach (var form in Enum.GetValues<CompoundForm>().Except(ShapeEmitter.Families))
            await Assert.That(() => ShapeEmitter.Emit(form, 20, 24, 2)).Throws<ArgumentException>();
    }
}
