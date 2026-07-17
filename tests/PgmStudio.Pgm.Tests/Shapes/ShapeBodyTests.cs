using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// The Body/designation split (docs/contracts/shape-vocabulary.md §8/§9): <see cref="ShapeEmitter.Body"/> is the
/// terminal-free compound and <see cref="ShapeEmitter.Emit"/> is that same body finished by the approach
/// designation. The split is <b>byte-identical</b> — the body carries exactly the emission's terrain and
/// vacancies (no terminal), and re-stamping it with the emission's room + marker reconstructs the emission.
/// </summary>
public sealed class ShapeBodyTests
{
    private static readonly ShapeFamily[] Emittable =
        [ShapeFamily.I, ShapeFamily.L, ShapeFamily.Z, ShapeFamily.Scythe, ShapeFamily.Clamp, ShapeFamily.U, ShapeFamily.H, ShapeFamily.Donut];

    private static bool RectEq(int[] a, int[] b) => a.Length == b.Length && a.SequenceEqual(b);

    private static async Task AssertSameBody(EmittedShape emit, ShapeBody body)
    {
        // the body carries exactly the emission's terrain pieces (rect + slot), in order
        await Assert.That(body.Pieces.Count).IsEqualTo(emit.Terrain.Count);
        for (var i = 0; i < body.Pieces.Count; i++)
        {
            await Assert.That(RectEq(body.Pieces[i].Rect, emit.Terrain[i].Rect)).IsTrue();
            await Assert.That(body.Pieces[i].Slot).IsEqualTo(emit.Terrain[i].Slot);
        }
        // terminal-free: no piece is the room, and the emission's room rect is not among the body pieces
        await Assert.That(body.Pieces.Any(p => p.Slot == ApproachSlots.Room)).IsFalse();
        await Assert.That(body.Pieces.Any(p => RectEq(p.Rect, emit.Room))).IsFalse();
        // vacancies ride along the body unchanged
        await Assert.That(body.Vacancies.Count).IsEqualTo(emit.Vacancies.Count);

        // the approach designation over the body reconstructs the emission
        var restamped = ShapeEmitter.Approach(body, emit.Room, emit.At);
        await Assert.That(RectEq(restamped.Room, emit.Room)).IsTrue();
        await Assert.That(restamped.At.SequenceEqual(emit.At)).IsTrue();
        await Assert.That(ReferenceEquals(restamped.Body, body)).IsTrue();
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
                    EmittedShape emit;
                    try { emit = ShapeEmitter.Emit(family, W, H, cw, flip); }
                    catch (ArgumentException) { continue; }          // too small for these dims — skip
                    var body = ShapeEmitter.Body(family, W, H, cw, flip);
                    await AssertSameBody(emit, body);
                }
    }

    // the knobs (side-tuck, donut attachments/extend/at-end) must survive the split too
    [Test]
    public async Task Body_matches_the_emission_under_the_placement_knobs()
    {
        const int cw = 2;
        var cases = new (ShapeFamily Family, Func<(EmittedShape, ShapeBody)> Pair)[]
        {
            (ShapeFamily.I, () => (ShapeEmitter.Emit(ShapeFamily.I, 16, 22, cw, roomPlacement: RoomPlacement.SideTuck),
                                   ShapeEmitter.Body(ShapeFamily.I, 16, 22, cw, roomPlacement: RoomPlacement.SideTuck))),
            (ShapeFamily.Donut, () => (ShapeEmitter.Emit(ShapeFamily.Donut, 18, 16, cw, attachments: 2),
                                       ShapeEmitter.Body(ShapeFamily.Donut, 18, 16, cw, attachments: 2))),
            (ShapeFamily.Donut, () => (ShapeEmitter.Emit(ShapeFamily.Donut, 18, 14, cw, woolExtend: true),
                                       ShapeEmitter.Body(ShapeFamily.Donut, 18, 14, cw, woolExtend: true))),
            (ShapeFamily.Donut, () => (ShapeEmitter.Emit(ShapeFamily.Donut, 18, 14, cw, woolAtEnd: true),
                                       ShapeEmitter.Body(ShapeFamily.Donut, 18, 14, cw, woolAtEnd: true))),
            (ShapeFamily.Scythe, () => (ShapeEmitter.Emit(ShapeFamily.Scythe, 18, 16, cw, entryShift: 3),
                                        ShapeEmitter.Body(ShapeFamily.Scythe, 18, 16, cw, entryShift: 3))),
        };
        foreach (var (_, pair) in cases)
        {
            var (emit, body) = pair();
            await AssertSameBody(emit, body);
        }
    }
}
