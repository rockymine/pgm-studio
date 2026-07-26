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

    /// <summary>The widening itself: a ring with one thicker wall is the same four walls, and the hole is what
    /// they leave — widening narrows the void rather than growing the box.</summary>
    [Test]
    public async Task A_ring_can_carry_one_wall_wider_than_the_rest()
    {
        // the right leg at 3 where the others are 2: the hole loses a column on that side, the box is unchanged
        var walls = new RingWalls(Top: 2, Right: 3, Bottom: 2, Left: 2);
        await Assert.That(Print(BodyEmitter.Ring(walls, 9, 7))).IsEqualTo(
            "bar:0,0,9,2 | bar:0,5,9,2 | leg:0,2,2,3 | leg:6,2,3,3  voids hole:2,2,4,3");

        // and it still reads as a ring — identity is width-independent, which is the whole premise
        var cells = new HashSet<(int, int)>();
        foreach (var (rect, _) in BodyEmitter.Ring(walls, 9, 7).Pieces)
            for (var x = rect[0]; x < rect[0] + rect[2]; x++)
                for (var z = rect[1]; z < rect[1] + rect[3]; z++) cells.Add((x, z));
        await Assert.That(ShapeClassifier.ClassifyBody(cells).Form).IsEqualTo(Compound.Ring);
    }

    /// <summary>The emitter enforces geometry only — walls must be a corridor and must leave a hole. How far the
    /// widths may differ from one another is a sampling law the composer owns, so the emitter builds a 2/5 ring
    /// without complaint.</summary>
    [Test]
    public async Task The_emitter_bounds_ring_walls_by_geometry_not_by_taste()
    {
        await Assert.That(() => BodyEmitter.Ring(new RingWalls(2, 1, 2, 2), 9, 7)).Throws<ArgumentException>()
            .Because("a 1-cell wall is under the corridor minimum");
        await Assert.That(() => BodyEmitter.Ring(new RingWalls(2, 5, 2, 5), 9, 7)).Throws<ArgumentException>()
            .Because("5 + 5 leaves no hole in a 9-wide box");
        await Assert.That(Print(BodyEmitter.Ring(new RingWalls(2, 5, 2, 2), 11, 7))).IsNotEmpty()
            .Because("a lopsided 2/5 ring is legal geometry; the ratio law lives in the sampler");
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

    /// <summary>The donut carrying a wider leg and a wider bar — the case both exemplars are authored around.
    /// The ring's walls differ, the hole absorbs it, and the wool still hangs off the bottom-right against the
    /// widened bottom wall rather than a nominal corridor.</summary>
    [Test]
    public async Task A_donut_can_carry_a_wider_leg_and_a_wider_bar()
    {
        var uniform = ShapeEmitter.Emit(ShapeFamily.Donut, 13, 8, Cw);
        var widened = ShapeEmitter.Emit(ShapeFamily.Donut, 13, 8, Cw,
            ringWalls: new RingWalls(Top: 3, Right: 2, Bottom: 2, Left: 2));

        // the top bar thickens; the hole loses that row rather than the box growing
        var uTop = uniform.Terrain.First(t => t.Slot == ApproachSlots.EntryBar).Rect;
        var wTop = widened.Terrain.First(t => t.Slot == ApproachSlots.EntryBar).Rect;
        await Assert.That(uTop[3]).IsEqualTo(2);
        await Assert.That(wTop[3]).IsEqualTo(3);
        await Assert.That(widened.Vacancies.Single(v => v.Kind == "hole").Rect[3])
            .IsEqualTo(uniform.Vacancies.Single(v => v.Kind == "hole").Rect[3] - 1);

        // and it still reads as a donut — a widened wall is the same family
        var cells = new HashSet<(int, int)>();
        var rooms = new HashSet<(int, int)>();
        foreach (var (rect, _) in widened.Terrain)
            for (var x = rect[0]; x < rect[0] + rect[2]; x++)
                for (var z = rect[1]; z < rect[1] + rect[3]; z++) cells.Add((x, z));
        for (var x = widened.Room[0]; x < widened.Room[0] + widened.Room[2]; x++)
            for (var z = widened.Room[1]; z < widened.Room[1] + widened.Room[3]; z++) { cells.Add((x, z)); rooms.Add((x, z)); }
        await Assert.That(ShapeClassifier.Classify(cells, rooms).Family).IsEqualTo(ShapeFamily.Donut);
    }

    /// <summary>A wall vector too fat for the box it is asked to fill is refused, not squashed — the box's slack
    /// is what widening spends, so there has to be some.</summary>
    [Test]
    public async Task A_donut_refuses_walls_its_box_has_no_slack_for()
    {
        await Assert.That(() => ShapeEmitter.Emit(ShapeFamily.Donut, 13, 8, Cw,
            ringWalls: new RingWalls(Top: 4, Right: 2, Bottom: 4, Left: 2))).Throws<ArgumentException>();
    }
}
