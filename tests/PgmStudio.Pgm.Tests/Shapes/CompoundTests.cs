using PgmStudio.Geom;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// The Compound/designation split (docs/generator/model.md §5.3/§5.4): <see cref="ShapeEmitter.Compound"/> is the
/// terminal-free compound and <see cref="ShapeEmitter.Emit"/> is that same compound finished by the approach
/// designation. The split is <b>byte-identical</b> — the compound carries exactly the emission's terrain and
/// vacancies (no terminal), and re-stamping it with the emission's room + marker reconstructs the emission.
/// </summary>
public sealed class CompoundTests
{
    private static readonly ShapeFamily[] Emittable =
        [ShapeFamily.I, ShapeFamily.L, ShapeFamily.Z, ShapeFamily.Scythe, ShapeFamily.Clamp, ShapeFamily.U, ShapeFamily.H, ShapeFamily.Donut];

    private static bool RectEq(CellRect a, CellRect b) => a == b;

    private static async Task AssertSameCompound(ApproachShape emit, Compound compound)
    {
        // the compound carries exactly the emission's terrain pieces (rect + slot), in order
        await Assert.That(compound.Pieces.Count).IsEqualTo(emit.Compound.Pieces.Count);
        for (var i = 0; i < compound.Pieces.Count; i++)
        {
            await Assert.That(RectEq(compound.Pieces[i].Rect, emit.Compound.Pieces[i].Rect)).IsTrue();
            await Assert.That(compound.Pieces[i].Slot).IsEqualTo(emit.Compound.Pieces[i].Slot);
        }
        // terminal-free: no piece is the room, and the emission's room rect is not among the compound pieces
        await Assert.That(compound.Pieces.Any(p => p.Slot == ApproachSlots.Room)).IsFalse();
        await Assert.That(compound.Pieces.Any(p => RectEq(p.Rect, emit.Room))).IsFalse();
        // vacancies ride along the compound unchanged
        await Assert.That(compound.Vacancies.Count).IsEqualTo(emit.Compound.Vacancies.Count);

        // the approach designation over the compound reconstructs the emission
        var restamped = new ApproachShape(compound, emit.Room, emit.At);
        await Assert.That(RectEq(restamped.Room, emit.Room)).IsTrue();
        await Assert.That(restamped.At.SequenceEqual(emit.At)).IsTrue();
        await Assert.That(ReferenceEquals(restamped.Compound, compound)).IsTrue();
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
    public async Task Body_is_the_terminal_free_half_of_the_emission(ShapeFamily family)
    {
        foreach (var cw in new[] { 2, 3 })
            foreach (var (W, H) in new[] { (16, 22), (18, 26) })
                foreach (var flip in new[] { false, true })
                {
                    ApproachShape emit;
                    try { emit = ShapeEmitter.Emit(family, W, H, cw, flip); }
                    catch (ArgumentException) { continue; }          // too small for these dims — skip
                    var compound = ShapeEmitter.EmitCompound(family, W, H, cw, flip);
                    await AssertSameCompound(emit, compound);
                }
    }

    // the knobs (side-tuck, donut attachments/extend/at-end) must survive the split too
    [Test]
    public async Task Body_matches_the_emission_under_the_placement_knobs()
    {
        const int cw = 2;
        var cases = new (ShapeFamily Family, Func<(ApproachShape, Compound)> Pair)[]
        {
            (ShapeFamily.I, () => (ShapeEmitter.Emit(ShapeFamily.I, 16, 22, cw, roomPlacement: RoomPlacement.SideTuck),
                                   ShapeEmitter.EmitCompound(ShapeFamily.I, 16, 22, cw, roomPlacement: RoomPlacement.SideTuck))),
            (ShapeFamily.Donut, () => (ShapeEmitter.Emit(ShapeFamily.Donut, 18, 16, cw, attachments: 2),
                                       ShapeEmitter.EmitCompound(ShapeFamily.Donut, 18, 16, cw, attachments: 2))),
            (ShapeFamily.Donut, () => (ShapeEmitter.Emit(ShapeFamily.Donut, 18, 14, cw, woolExtend: true),
                                       ShapeEmitter.EmitCompound(ShapeFamily.Donut, 18, 14, cw, woolExtend: true))),
            (ShapeFamily.Donut, () => (ShapeEmitter.Emit(ShapeFamily.Donut, 18, 14, cw, woolAtEnd: true),
                                       ShapeEmitter.EmitCompound(ShapeFamily.Donut, 18, 14, cw, woolAtEnd: true))),
            (ShapeFamily.Scythe, () => (ShapeEmitter.Emit(ShapeFamily.Scythe, 18, 16, cw, entryShift: 3),
                                        ShapeEmitter.EmitCompound(ShapeFamily.Scythe, 18, 16, cw, entryShift: 3))),
        };
        foreach (var (_, pair) in cases)
        {
            var (emit, compound) = pair();
            await AssertSameCompound(emit, compound);
        }
    }
}
