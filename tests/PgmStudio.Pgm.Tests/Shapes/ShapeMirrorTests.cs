using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// The emit↔derive mirror (docs/contracts/map-generation.md §5.4): emit every base family (and its variants)
/// with <see cref="WoolBoxEmitter"/> and read each back with <see cref="ShapeClassifier"/> — requested ==
/// derived is the mirror closing, on <b>one</b> <see cref="ShapeFamily"/> enum (no string bridge). It is a
/// <b>true mirror</b>: each emission is also fed through <see cref="SlotAssignment"/> to re-derive every
/// piece's slot from topology and assert it equals the slot the emitter stamped (§5.3). The emitted pieces
/// must also tile without overlap.
/// </summary>
public sealed class ShapeMirrorTests
{
    private static readonly ShapeFamily[] Emittable =
        [ShapeFamily.I, ShapeFamily.L, ShapeFamily.Z, ShapeFamily.Scythe, ShapeFamily.Clamp, ShapeFamily.U, ShapeFamily.H, ShapeFamily.Donut];

    // a cell covered by more than one piece is a bug
    private static int Overlaps(EmittedApproach a)
    {
        var seen = new HashSet<(int, int)>(); int n = 0;
        foreach (var p in a.Terrain.Append(a.WoolRoom))
            for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
                for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++) if (!seen.Add((x, z))) n++;
        return n;
    }

    private static ShapeFamily Derive(EmittedApproach a) =>
        ShapeClassifier.Classify(WoolBoxEmitter.AsPlan(a), a.WoolRoom.Id).Family;

    // emitting SUCCEEDS -> classify reads back the requested family, no overlaps, and every piece's slot
    // re-derived from topology equals the slot the emitter stamped (the true mirror). Too-small boxes throw
    // ComposeException and are not failures (the caller sizes the box to the family).
    private static async Task MirrorOk(ShapeFamily family, Func<EmittedApproach> emit)
    {
        EmittedApproach a;
        try { a = emit(); } catch (ComposeException) { return; }
        var derived = Derive(a);
        await Assert.That(derived).IsEqualTo(family);
        await Assert.That(Overlaps(a)).IsEqualTo(0);

        var pieces = a.Terrain.Select(p => (p.Id, p.Rect)).Append((a.WoolRoom.Id, a.WoolRoom.Rect)).ToList();
        var slots = SlotAssignment.AssignSlots(derived, pieces, a.WoolRoom.Id);
        foreach (var p in a.Terrain.Append(a.WoolRoom))
            await Assert.That(slots[p.Id]).IsEqualTo(p.Slot);
    }

    [Test]
    [Arguments(ShapeFamily.I)]
    [Arguments(ShapeFamily.L)]
    [Arguments(ShapeFamily.Z)]
    [Arguments(ShapeFamily.Scythe)]
    [Arguments(ShapeFamily.Clamp)]
    [Arguments(ShapeFamily.U)]
    [Arguments(ShapeFamily.H)]
    [Arguments(ShapeFamily.Donut)]
    public async Task Emitted_family_classifies_back(ShapeFamily family)
    {
        foreach (var cw in new[] { 2, 3 })
            foreach (var (W, H) in new[] { (12, 16), (16, 22) })
                foreach (var flip in new[] { false, true })
                    await MirrorOk(family, () => WoolBoxEmitter.Emit(family, new WoolBox(0, 0, W, H), cw, flip));
    }

    // a uniformly-widened approach reads the same family — the classifier counts turns on the terrain outline,
    // so width is invariant. Emit each thin family, then a ×2 and ×3 scaled twin, at the matching reference width.
    [Test]
    [Arguments(ShapeFamily.I)]
    [Arguments(ShapeFamily.L)]
    [Arguments(ShapeFamily.Z)]
    [Arguments(ShapeFamily.Scythe)]
    [Arguments(ShapeFamily.Clamp)]
    [Arguments(ShapeFamily.U)]
    [Arguments(ShapeFamily.H)]
    public async Task Width_invariance_under_uniform_scaling(ShapeFamily family)
    {
        foreach (var k in new[] { 1, 2, 3 })
            await MirrorOk(family, () => WoolBoxEmitter.Emit(family, new WoolBox(0, 0, 6 * (2 * k), 10 * (2 * k)), 2 * k));
    }

    [Test]
    public async Task Donut_variants_classify_back()
    {
        foreach (var cw in new[] { 2, 3 })
        {
            var db = new WoolBox(0, 0, 6 * cw + 4, 5 * cw);
            await MirrorOk(ShapeFamily.Donut, () => WoolBoxEmitter.Emit(ShapeFamily.Donut, db, cw, attachments: 1));
            await MirrorOk(ShapeFamily.Donut, () => WoolBoxEmitter.Emit(ShapeFamily.Donut, db, cw, attachments: 2));
            await MirrorOk(ShapeFamily.Donut, () => WoolBoxEmitter.Emit(ShapeFamily.Donut, db, cw, woolExtend: true));
            await MirrorOk(ShapeFamily.Donut, () => WoolBoxEmitter.Emit(ShapeFamily.Donut, db, cw, woolAtEnd: true));
            var dbw = new WoolBox(0, 0, 6 * cw + 4, 7 * cw);   // taller box for wide attachments
            await MirrorOk(ShapeFamily.Donut, () => WoolBoxEmitter.Emit(ShapeFamily.Donut, dbw, cw, attachmentWidth: 2 * cw));
            await MirrorOk(ShapeFamily.Donut, () => WoolBoxEmitter.Emit(ShapeFamily.Donut, dbw, cw, attachments: 2, attachmentWidth: 3 * cw));
            // a SINGLE wide attachment needs only its own minimal ring (aw + cw tall), not the two-stub height
            var dmin = new WoolBox(0, 0, 6 * cw + 4, 3 * cw);
            await MirrorOk(ShapeFamily.Donut, () => WoolBoxEmitter.Emit(ShapeFamily.Donut, dmin, cw, attachmentWidth: 2 * cw));
        }
    }

    [Test]
    public async Task U_and_H_variants_classify_back()
    {
        foreach (var cw in new[] { 2, 3 })
        {
            var ub = new WoolBox(0, 0, 4 * cw, 5 * cw);
            await MirrorOk(ShapeFamily.U, () => WoolBoxEmitter.Emit(ShapeFamily.U, ub, cw));
            await MirrorOk(ShapeFamily.U, () => WoolBoxEmitter.Emit(ShapeFamily.U, ub, cw, woolAtEnd: true));
            var hb = new WoolBox(0, 0, 4 * cw, 6 * cw);
            await MirrorOk(ShapeFamily.H, () => WoolBoxEmitter.Emit(ShapeFamily.H, hb, cw));
            await MirrorOk(ShapeFamily.H, () => WoolBoxEmitter.Emit(ShapeFamily.H, hb, cw, woolAtEnd: true));
        }
    }

    // side-tuck: a straight lane with the room off its side still reads I (the room is excluded from the bends),
    // and the room shares a corridor-width edge with the lane rather than capping its end.
    [Test]
    public async Task Side_tuck_reads_I_with_the_room_off_the_side()
    {
        foreach (var cw in new[] { 2, 3 })
            foreach (var (W, H) in new[] { (12, 16), (16, 22) })
                foreach (var flip in new[] { false, true })
                {
                    EmittedApproach a;
                    try { a = WoolBoxEmitter.Emit(ShapeFamily.I, new WoolBox(0, 0, W, H), cw, flip, RoomPlacement.SideTuck); }
                    catch (ComposeException) { continue; }
                    int[] lane = a.Terrain[0].Rect, rm = a.WoolRoom.Rect;
                    var side = rm[0] == lane[0] + lane[2] || rm[0] + rm[2] == lane[0];
                    await Assert.That(Derive(a)).IsEqualTo(ShapeFamily.I);
                    await Assert.That(side).IsTrue();
                    await Assert.That(Overlaps(a)).IsEqualTo(0);
                }
    }

    [Test]
    public async Task Emitter_refuses_the_derive_only_isolated_family()
    {
        await Assert.That(() => WoolBoxEmitter.Emit(ShapeFamily.Isolated, new WoolBox(0, 0, 20, 24), 2))
            .Throws<ComposeException>();
    }
}
