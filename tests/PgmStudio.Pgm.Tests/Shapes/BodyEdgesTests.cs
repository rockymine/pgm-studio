using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Shapes;

/// <summary>
/// The edge taxonomy (docs/contracts/shape-vocabulary.md): a body's negative spaces classed by wall count —
/// notch (2), bay (3), hole (enclosed) — and every boundary edge classed by the space it faces, Open being the
/// free outward surface. Fixtures pin the read on the compounds and the approach emissions, plus the
/// designation interplay (a clamp's recess is a bay only once the room closes it).
/// </summary>
public sealed class BodyEdgesTests
{
    private const int Cw = 3;

    private static int Count(EdgeClassification c, NegativeSpaceKind kind) => c.Spaces.Count(s => s.Kind == kind);

    private static async Task AssertSpaces(EdgeClassification c, int notches, int bays, int holes)
    {
        await Assert.That(Count(c, NegativeSpaceKind.Notch)).IsEqualTo(notches);
        await Assert.That(Count(c, NegativeSpaceKind.Bay)).IsEqualTo(bays);
        await Assert.That(Count(c, NegativeSpaceKind.Hole)).IsEqualTo(holes);
    }

    [Test]
    public async Task A_solid_rectangle_is_all_free_surface()
    {
        var c = BodyEdges.Classify(BodyEmitter.Rectangle(6 * Cw, 4 * Cw));
        await Assert.That(c.Spaces).IsEmpty();
        await Assert.That(c.Edges.All(e => e.Faces == NegativeSpaceKind.Open)).IsTrue();
    }

    // the branch family: the arm placement decides which negative spaces the same K yields
    [Test]
    public async Task Spine_arm_placement_decides_notch_vs_bay()
    {
        // T (one middle arm): two wrapped corners
        await AssertSpaces(BodyEdges.Classify(BodyEmitter.SpineArms(Cw, 1)), notches: 2, bays: 0, holes: 0);
        // Π (arms at both ends): one recess between the legs
        await AssertSpaces(BodyEdges.Classify(BodyEmitter.SpineArms(Cw, [0, 4 * Cw], 5 * Cw)), notches: 0, bays: 1, holes: 0);
        // F (an end arm + a middle arm): a recess between the arms and a wrapped corner beyond the middle one
        await AssertSpaces(BodyEdges.Classify(BodyEmitter.SpineArms(Cw, [0, 2 * Cw], 5 * Cw)), notches: 1, bays: 1, holes: 0);
        // E (three arms): two recesses
        await AssertSpaces(BodyEdges.Classify(BodyEmitter.SpineArms(Cw, [0, 2 * Cw, 4 * Cw], 5 * Cw)), notches: 0, bays: 2, holes: 0);
    }

    [Test]
    public async Task Holed_compounds_read_their_voids()
    {
        await AssertSpaces(BodyEdges.Classify(BodyEmitter.Ring(Cw, 5 * Cw, 5 * Cw)), notches: 0, bays: 0, holes: 1);
        await AssertSpaces(BodyEdges.Classify(BodyEmitter.DoubleHole(Cw, 4 * Cw, 5 * Cw, uW: 3 * Cw, uH: 5 * Cw, uz: 0)),
            notches: 0, bays: 0, holes: 2);
        // two loops on a shared baseline: two holes, and the open channel between them is a bay
        await AssertSpaces(BodyEdges.Classify(BodyEmitter.TwoUOnI(Cw, 5 * Cw)), notches: 0, bays: 1, holes: 2);
        // P: the loop's hole plus the wrapped corners where the long bar overhangs it
        var p = BodyEdges.Classify(BodyEmitter.P(Cw, 4 * Cw, 5 * Cw));
        await Assert.That(Count(p, NegativeSpaceKind.Hole)).IsEqualTo(1);
        await Assert.That(Count(p, NegativeSpaceKind.Notch)).IsEqualTo(2);
    }

    [Test]
    public async Task Approach_emissions_read_their_features()
    {
        // L: one wrapped corner; Z: one per opposing bend
        await AssertSpaces(BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.L, 15, 18, Cw)), notches: 1, bays: 0, holes: 0);
        await AssertSpaces(BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.Z, 15, 21, Cw)), notches: 2, bays: 0, holes: 0);
        // scythe: the fold's bay + the corner behind the entry tail
        await AssertSpaces(BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.Scythe, 18, 15, Cw)), notches: 1, bays: 1, holes: 0);
        // U: the recess between the legs; the room on the bar wraps a corner each side
        await AssertSpaces(BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.U, 15, 18, Cw)), notches: 2, bays: 1, holes: 0);
        // donut: the enclosed ring void (the stub and room wrap corners, never a bay)
        var donut = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.Donut, 18, 15, Cw));
        await Assert.That(Count(donut, NegativeSpaceKind.Hole)).IsEqualTo(1);
        await Assert.That(Count(donut, NegativeSpaceKind.Bay)).IsEqualTo(0);
    }

    // the designation interplay: the same two bars read a bay with the room sealing the recess and a mere
    // notch-free corridor gap without it — the negative-space class can be a property of the designation
    [Test]
    public async Task Clamp_recess_is_a_bay_only_with_the_room()
    {
        var emission = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.Clamp, 12, 15, Cw));
        await Assert.That(Count(emission, NegativeSpaceKind.Bay)).IsEqualTo(1);

        var bodyOnly = BodyEdges.Classify(ShapeEmitter.Body(ShapeFamily.Clamp, 12, 15, Cw));
        await Assert.That(Count(bodyOnly, NegativeSpaceKind.Bay)).IsEqualTo(0);
        await Assert.That(Count(bodyOnly, NegativeSpaceKind.Notch)).IsEqualTo(1);
    }

    // a straight lane: the box remainder beside it (a published emit-time vacancy) lies outside the shape's
    // own bounding box — the shape-relative read sees no negative space there at all, only free surface
    [Test]
    public async Task A_straight_lane_has_no_negative_space()
    {
        var c = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.I, 9, 15, Cw));
        await Assert.That(c.Spaces).IsEmpty();
        await Assert.That(c.Edges.All(e => e.Faces == NegativeSpaceKind.Open)).IsTrue();
    }

    // the terminal seals its own wall: room-owned boundary runs carry Terminal, split from colinear terrain
    // runs, and their summed length is exactly the room's exposed perimeter (its outline minus terrain seams)
    [Test]
    public async Task Terminal_runs_split_from_terrain_and_cover_the_rooms_exposed_wall()
    {
        // I 9x15 cw3: the room caps the lane — each side line splits into a free lane interval and a sealed
        // room interval, and the room's exposed wall is 3 of its 10-perimeter shared with the lane
        var i = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.I, 9, 15, Cw));
        await Assert.That(i.Edges.Contains(new ClassifiedEdge(3, 0, 3, 13, NegativeSpaceKind.Open, 13))).IsTrue();
        await Assert.That(i.Edges.Contains(new ClassifiedEdge(3, 13, 3, 15, NegativeSpaceKind.Open, 2, Terminal: true))).IsTrue();
        await Assert.That(i.Edges.Where(e => e.Terminal).Sum(e => e.Length)).IsEqualTo(7);

        // L 15x18 cw3: the room caps the band's far end — sealed right/bottom walls, and its top wall faces
        // the notch (owner and faced space are independent axes)
        var l = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.L, 15, 18, Cw));
        await Assert.That(l.Edges.Where(e => e.Terminal).Sum(e => e.Length)).IsEqualTo(7);
        await Assert.That(l.Edges.Any(e => e.Terminal && e.Faces == NegativeSpaceKind.Notch)).IsTrue();

        // clamp 12x15 cw3: the room's bay face is the designated seat — a terminal wall on the bay
        var clamp = BodyEdges.Classify(ShapeEmitter.Emit(ShapeFamily.Clamp, 12, 15, Cw));
        await Assert.That(clamp.Edges.Any(e => e.Terminal && e.Faces == NegativeSpaceKind.Bay)).IsTrue();
        await Assert.That(clamp.Edges.Where(e => e.Terminal).Sum(e => e.Length)).IsEqualTo(18);
    }

    [Test]
    public async Task Terminal_free_bodies_have_no_terminal_runs()
    {
        var body = BodyEdges.Classify(ShapeEmitter.Body(ShapeFamily.U, 15, 18, Cw));
        await Assert.That(body.Edges.Any(e => e.Terminal)).IsFalse();
    }

    // every filled↔empty seam is covered by exactly one classified run: the run lengths sum to the perimeter
    [Test]
    [Arguments(ShapeFamily.L, 15, 18)]
    [Arguments(ShapeFamily.Scythe, 18, 15)]
    [Arguments(ShapeFamily.U, 15, 18)]
    [Arguments(ShapeFamily.Donut, 18, 15)]
    public async Task Classified_runs_cover_the_whole_boundary(ShapeFamily family, int w, int h)
    {
        var emit = ShapeEmitter.Emit(family, w, h, Cw);
        var cells = new HashSet<(int, int)>();
        foreach (var r in emit.Terrain.Select(p => p.Rect).Append(emit.Room))
            for (var x = r[0]; x < r[0] + r[2]; x++)
                for (var z = r[1]; z < r[1] + r[3]; z++) cells.Add((x, z));
        var perimeter = cells.Sum(c => PgmStudio.Geom.Cells.N4(c).Count(n => !cells.Contains(n)));

        var read = BodyEdges.Classify(emit);
        await Assert.That(read.Edges.Sum(e => e.Length)).IsEqualTo(perimeter);
        await Assert.That(read.Edges.All(e => e.Length > 0)).IsTrue();
    }
}
