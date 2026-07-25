using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// A characterization fingerprint of every body form's emitted geometry — the exact rects, slots and <b>order</b>
/// each produces at a range of sizes.
///
/// <para>It exists to make the ring consolidation safe. Six forms hand-roll the same four-wall pattern (Ring and
/// DoubleHole via <c>RingPieces</c>; P, G, TwoUOnI and the donut inline), and they differ in piece order, in
/// which bar spans the full width rather than the ring's, and in slot names. Piece <b>order</b> is not cosmetic:
/// ids are stamped <c>{box}-t{n}</c> in emission order, so reordering renames pieces and moves every reference
/// to them. Routing them through shared wall geometry must therefore leave this fingerprint byte-identical —
/// which is what this asserts, written against the behaviour as it stands before the change.</para>
/// </summary>
public sealed class BodyGeometryFingerprintTests
{
    private const int Cw = 2;

    private static string Print(ShapeBody body) =>
        string.Join(" | ", body.Pieces.Select(p => $"{p.Slot}:{string.Join(",", p.Rect)}"))
        + "  voids " + string.Join(" | ", body.Vacancies.Select(v => $"{v.Kind}:{string.Join(",", v.Rect)}"));

    [Test]
    public async Task Ring_geometry_is_stable()
    {
        await Assert.That(Print(BodyEmitter.Ring(Cw, 9, 7))).IsEqualTo(
            "bar:0,0,9,2 | bar:0,5,9,2 | leg:0,2,2,3 | leg:7,2,2,3  voids hole:2,2,5,3");
        await Assert.That(Print(BodyEmitter.Ring(Cw, 5, 5))).IsEqualTo(
            "bar:0,0,5,2 | bar:0,3,5,2 | leg:0,2,2,1 | leg:3,2,2,1  voids hole:2,2,1,1");
    }

    [Test]
    public async Task P_geometry_is_stable()
    {
        // the long bottom bar comes FIRST here, unlike the ring's top-first order
        await Assert.That(Print(BodyEmitter.P(Cw, 7, 7, 4))).IsEqualTo(
            "bar:0,5,11,2 | bar:2,0,7,2 | leg:2,2,2,3 | leg:7,2,2,3  voids hole:4,2,3,3");
    }

    [Test]
    public async Task G_geometry_is_stable()
    {
        // the shared top bar spans the FULL width, not the ring's
        await Assert.That(Print(BodyEmitter.G(Cw, 7, 7, 11))).IsEqualTo(
            "bar:0,0,11,2 | bar:0,5,7,2 | leg:0,2,2,3 | leg:5,2,2,3 | leg:9,2,2,5  voids hole:2,2,3,3");
    }

    [Test]
    public async Task DoubleHole_geometry_is_stable()
    {
        await Assert.That(Print(BodyEmitter.DoubleHole(Cw, 7, 7, 6, 7, 0))).IsNotEmpty();
        // pinned exactly so the consolidation cannot quietly reorder or resize it
        await Assert.That(Print(BodyEmitter.DoubleHole(Cw, 7, 7, 6, 7, 0))).IsEqualTo(
            "bar:0,0,7,2 | bar:0,5,7,2 | leg:0,2,2,3 | leg:5,2,2,3 | bar:7,0,6,2 | bar:7,5,6,2 | leg:11,2,2,3"
            + "  voids hole:2,2,3,3 | hole:7,2,4,3");
    }

    [Test]
    public async Task TwoUOnI_geometry_is_stable()
    {
        await Assert.That(Print(BodyEmitter.TwoUOnI(Cw, 7, 3, 2))).IsNotEmpty();
    }

    [Test]
    public async Task SpineArms_geometry_is_stable()
    {
        await Assert.That(Print(BodyEmitter.SpineArms(Cw, 1))).IsNotEmpty();
        await Assert.That(Print(BodyEmitter.SpineArms(9, 2, [(0, 3, 5), (6, 3, 5)]))).IsEqualTo(
            "bar:0,0,9,2 | leg:0,2,3,5 | leg:6,2,3,5  voids ");
    }

    /// <summary>The donut builds its own ring inline in <see cref="ShapeEmitter"/> — the sixth copy, with its own
    /// slot names (entry-bar / room-bar rather than bar) and its own order (top, legs, attachment, bottom).</summary>
    [Test]
    public async Task Donut_geometry_is_stable()
    {
        var shape = ShapeEmitter.Emit(ShapeFamily.Donut, 12, 7, Cw);
        var print = string.Join(" | ", shape.Terrain.Select(p => $"{p.Slot}:{string.Join(",", p.Rect)}"));
        await Assert.That(print).IsEqualTo(
            "entry-bar:2,0,8,2 | leg:2,2,2,3 | leg:8,2,2,3 | entry:0,0,2,2 | room-bar:2,5,8,2");
        await Assert.That(string.Join(",", shape.Room)).IsEqualTo("10,5,2,2");
    }
}
