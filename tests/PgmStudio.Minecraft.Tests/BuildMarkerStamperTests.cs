using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The build-region outline marker (docs/generator/rules.md ST5): an unpowered redstone line at y=1, two
/// blocks out from every void-facing edge of the build region, turning the corner where two such edges meet
/// and pulling back to keep the same one-block clearance from terrain. The cases mirror the teaching seed
/// <c>tools/seeds/teaching/build-region-examples.plan.json</c>, whose five zones are the authored
/// expectations for this shape.
/// </summary>
public sealed class BuildMarkerStamperTests
{
    private static readonly IReadOnlyList<(int MinX, int MinZ, int MaxX, int MaxZ)> NoHoles = [];

    // A terrain footprint over a rectangle of columns (min inclusive, max exclusive), surface height unused.
    private static Dictionary<(int X, int Z), int> Terrain(params (int MinX, int MinZ, int MaxX, int MaxZ)[] rects)
    {
        var footprint = new Dictionary<(int X, int Z), int>();
        foreach (var (minX, minZ, maxX, maxZ) in rects)
            for (var x = minX; x < maxX; x++)
            for (var z = minZ; z < maxZ; z++)
                footprint[(x, z)] = 9;
        return footprint;
    }

    // The marker columns on one line of constant X, low to high Z (empty when that line carries none).
    private static List<int> Column(IEnumerable<(int X, int Z)> cells, int x)
        => [.. cells.Where(c => c.X == x).Select(c => c.Z).Order()];

    private static List<int> Row(IEnumerable<(int X, int Z)> cells, int z)
        => [.. cells.Where(c => c.Z == z).Select(c => c.X).Order()];

    // The marker blocks inside a window, so one ring can be read out of a board that holds several.
    private static List<string> Within(IEnumerable<(int X, int Z)> cells, int minX, int minZ, int maxX, int maxZ)
        => [.. cells.Where(cell => cell.X >= minX && cell.X <= maxX && cell.Z >= minZ && cell.Z <= maxZ)
                    .OrderBy(cell => cell.Z).ThenBy(cell => cell.X).Select(cell => $"{cell.X},{cell.Z}")];

    // The blocks of a closed rectangular ring, in the same form Within returns.
    private static List<string> Ring(int minX, int minZ, int maxX, int maxZ)
    {
        var cells = new List<(int X, int Z)>();
        for (var x = minX; x <= maxX; x++) { cells.Add((x, minZ)); cells.Add((x, maxZ)); }
        for (var z = minZ + 1; z < maxZ; z++) { cells.Add((minX, z)); cells.Add((maxX, z)); }
        return [.. cells.OrderBy(cell => cell.Z).ThenBy(cell => cell.X).Select(cell => $"{cell.X},{cell.Z}")];
    }

    [Test]
    public async Task A_region_bridging_two_terrain_pieces_is_marked_on_its_two_free_sides_only()
    {
        // An 8×8 region docked north and south against terrain of the same width — the seed's example-1.
        var areas = new[] { (0, 0, 8, 8) };
        var terrain = Terrain((0, -8, 8, 0), (0, 8, 8, 16));

        var cells = BuildMarkerStamper.MarkerCells(areas, NoHoles, terrain);

        // Two lines, one air block clear of the region's west and east edges, each spanning exactly the
        // region's depth: the docked edges carry no line, so neither line turns a corner.
        await Assert.That(Column(cells, -2)).IsEquivalentTo(Enumerable.Range(0, 8).ToList());
        await Assert.That(Column(cells, 9)).IsEquivalentTo(Enumerable.Range(0, 8).ToList());
        await Assert.That(cells.Count).IsEqualTo(16);
    }

    [Test]
    public async Task A_region_free_on_every_side_is_ringed()
    {
        // The seed's example-4: nothing to dock against, so all four lines turn into each other.
        var areas = new[] { (0, 0, 8, 8) };

        var cells = BuildMarkerStamper.MarkerCells(areas, NoHoles, Terrain());

        // A closed ring two blocks out on every side: 12 × 12, so 12 blocks per side and 44 in all.
        await Assert.That(Column(cells, -2)).IsEquivalentTo(Enumerable.Range(-2, 12).ToList());
        await Assert.That(Column(cells, 9)).IsEquivalentTo(Enumerable.Range(-2, 12).ToList());
        await Assert.That(Row(cells, -2)).IsEquivalentTo(Enumerable.Range(-2, 12).ToList());
        await Assert.That(Row(cells, 9)).IsEquivalentTo(Enumerable.Range(-2, 12).ToList());
        await Assert.That(cells.Count).IsEqualTo(44);
    }

    [Test]
    public async Task Two_free_edges_meeting_at_a_corner_turn_into_one_L()
    {
        // The seed's example-2: terrain covers the whole south edge but only part of the north one, leaving
        // the north-west corner over the void.
        var areas = new[] { (0, 0, 12, 8) };
        var terrain = Terrain((4, -8, 12, 0), (0, 8, 12, 16));

        var cells = BuildMarkerStamper.MarkerCells(areas, NoHoles, terrain);

        // The west line runs the region's depth and carries on past the corner to meet the north line, which
        // stops one air block short of the terrain that covers the rest of that edge.
        await Assert.That(Column(cells, -2)).IsEquivalentTo(Enumerable.Range(-2, 10).ToList());
        await Assert.That(Row(cells, -2)).IsEquivalentTo(new List<int> { -2, -1, 0, 1, 2 });
        // The east edge is free too, but both its corners are docked — a plain line, no turn.
        await Assert.That(Column(cells, 13)).IsEquivalentTo(Enumerable.Range(0, 8).ToList());
    }

    [Test]
    public async Task A_line_pulls_back_from_terrain_that_overhangs_the_region()
    {
        // The seed's example-5: the terrain north and south of the region reaches past its east edge, so the
        // east line would run alongside it.
        var areas = new[] { (0, 0, 12, 8) };
        var terrain = Terrain((0, -8, 16, 0), (0, 8, 16, 16));

        var cells = BuildMarkerStamper.MarkerCells(areas, NoHoles, terrain);

        // West is clear of the overhang and spans the full depth; east loses a block at each end to keep the
        // one-block clearance from the terrain above and below it.
        await Assert.That(Column(cells, -2)).IsEquivalentTo(Enumerable.Range(0, 8).ToList());
        await Assert.That(Column(cells, 13)).IsEquivalentTo(Enumerable.Range(1, 6).ToList());
    }

    [Test]
    public async Task A_region_docked_on_every_side_is_not_marked()
    {
        var areas = new[] { (0, 0, 8, 8) };
        var terrain = Terrain((-8, -8, 16, 0), (-8, 8, 16, 16), (-8, 0, 0, 8), (8, 0, 16, 8));

        var cells = BuildMarkerStamper.MarkerCells(areas, NoHoles, terrain);

        await Assert.That(cells).IsEmpty();
    }

    [Test]
    public async Task A_hole_is_cut_out_of_the_region_before_the_outline_is_traced()
    {
        // A hole big enough to hold its own clearance gets an inner ring; the outer ring is unaffected.
        var areas = new[] { (0, 0, 20, 20) };
        var holes = new[] { (6, 6, 14, 14) };

        var cells = BuildMarkerStamper.MarkerCells(areas, holes, Terrain());

        await Assert.That(Row(cells, 8)).IsEquivalentTo(new List<int> { -2, 7, 12, 21 });
    }

    [Test]
    public async Task Regions_ringing_a_void_pocket_each_mark_one_side_of_it()
    {
        // The seed's example-5 pinwheel: four regions on the edges of a 3×3, terrain at its corners, nothing
        // in the middle. The four regions never touch, so the pocket's outline is contributed a side at a time.
        var areas = new[] { (8, 0, 16, 8), (0, 8, 8, 16), (16, 8, 24, 16), (8, 16, 16, 24) };
        var terrain = Terrain((0, 0, 8, 8), (16, 0, 24, 8), (0, 16, 8, 24), (16, 16, 24, 24));

        var cells = BuildMarkerStamper.MarkerCells(areas, NoHoles, terrain);

        // Outward, each region carries a plain line spanning its own edge — the edges onto the corner terrain
        // are docked and carry nothing.
        await Assert.That(Column(cells, -2)).IsEquivalentTo(Enumerable.Range(8, 8).ToList());
        // The pocket comes out ringed anyway: each side is cut back by the clearance to the region diagonally
        // beside it, which is exactly where the next side takes over.
        await Assert.That(Within(cells, 8, 8, 15, 15)).IsEquivalentTo(Ring(9, 9, 14, 14));
    }

    [Test]
    public async Task Terrain_beside_a_line_shortens_it_where_terrain_past_its_corner_does_not()
    {
        // The seed's example-6: two spans between the same pair of terrain columns.
        var areas = new[] { (8, 0, 24, 8), (8, 16, 24, 24) };
        var terrain = Terrain((0, 0, 8, 24), (24, 0, 32, 24));

        var cells = BuildMarkerStamper.MarkerCells(areas, NoHoles, terrain);

        // Outward, the terrain is two blocks off the line's end corner, so the line spans the full span.
        await Assert.That(Row(cells, -2)).IsEquivalentTo(Enumerable.Range(8, 16).ToList());
        // Into the gap, that same terrain is directly beside the line's ends, which costs a block at each.
        await Assert.That(Row(cells, 9)).IsEquivalentTo(Enumerable.Range(9, 14).ToList());
    }

    [Test]
    public async Task Regions_sharing_a_border_are_outlined_as_one_and_their_pocket_gets_its_own_ring()
    {
        // The seed's example-7: four regions meeting edge to edge in a closed ring, nothing but void around
        // and inside. Membership is never consulted — the outline follows the cells, so the four trace as one.
        var areas = new[] { (0, 0, 8, 24), (8, 0, 16, 8), (16, 0, 24, 24), (8, 16, 16, 24) };

        var cells = BuildMarkerStamper.MarkerCells(areas, NoHoles, Terrain());

        // One closed 28×28 outer ring, and the enclosed pocket ringed in its own right — a row through the
        // middle of the pocket crosses the marker four times, twice outside and twice in.
        await Assert.That(Column(cells, -2)).IsEquivalentTo(Enumerable.Range(-2, 28).ToList());
        await Assert.That(Within(cells, 8, 8, 15, 15)).IsEquivalentTo(Ring(9, 9, 14, 14));
        await Assert.That(Row(cells, 11)).IsEquivalentTo(new List<int> { -2, 9, 14, 25 });
    }

    [Test]
    public async Task Regions_touching_at_a_corner_are_outlined_by_one_unbroken_staircase()
    {
        // The seed's example-8: two regions meeting at a single point. The outline has to step around the
        // pinch on both sides without breaking or doubling back over itself.
        var areas = new[] { (0, 0, 8, 8), (8, 8, 16, 16) };

        var cells = BuildMarkerStamper.MarkerCells(areas, NoHoles, Terrain());
        var marker = cells.ToHashSet();

        // A simple closed curve: every block has exactly two neighbours along the line…
        foreach (var (x, z) in cells)
        {
            var neighbours = new[] { (x - 1, z), (x + 1, z), (x, z - 1), (x, z + 1) }.Count(marker.Contains);
            await Assert.That(neighbours).IsEqualTo(2);
        }
        // …and it is one curve, not two, so the corner is where the two outlines became a single staircase.
        await Assert.That(ConnectedFrom(marker, cells[0])).IsEqualTo(cells.Count);
    }

    // How many marker blocks are reachable from `start` by orthogonal steps along the marker.
    private static int ConnectedFrom(HashSet<(int X, int Z)> marker, (int X, int Z) start)
    {
        var seen = new HashSet<(int X, int Z)> { start };
        var pending = new Stack<(int X, int Z)>([start]);
        while (pending.TryPop(out var cell))
            foreach (var next in new[]
                     { (cell.X - 1, cell.Z), (cell.X + 1, cell.Z), (cell.X, cell.Z - 1), (cell.X, cell.Z + 1) })
                if (marker.Contains(next) && seen.Add(next)) pending.Push(next);
        return seen.Count;
    }

    [Test]
    public async Task A_symmetric_pair_of_regions_is_marked_symmetrically()
    {
        // The stamper never sees the symmetry — it is handed areas already fanned across the orbit, and the
        // terrain footprint is the whole world's. Symmetric in must therefore be symmetric out, which is the
        // property the export relies on and nothing else asserts.
        var areas = new[] { (10, 10, 18, 18), (-18, -18, -10, -10) };   // a rot_180 pair about the origin

        var marker = BuildMarkerStamper.MarkerCells(areas, NoHoles, Terrain()).ToHashSet();

        await Assert.That(marker).IsNotEmpty();
        foreach (var (x, z) in marker)
            await Assert.That(marker.Contains((-1 - x, -1 - z))).IsTrue();   // block x spans [x, x+1)
    }

    [Test]
    public async Task A_pocket_too_narrow_to_hold_an_outline_is_still_marked_as_a_patch()
    {
        // Below six blocks across, the four sides of a pocket's outline land on top of each other and the ring
        // collapses. The pocket is still marked, which is the point of marking it — it just reads as a patch.
        var areas = new[] { (0, 0, 20, 20) };
        var holes = new[] { (8, 8, 12, 12) };   // a 4×4 pocket

        var cells = BuildMarkerStamper.MarkerCells(areas, holes, Terrain());

        await Assert.That(Within(cells, 8, 8, 11, 11))
            .IsEquivalentTo(new List<string> { "9,9", "10,9", "9,10", "10,10" });
    }

    [Test]
    public async Task Two_regions_four_blocks_apart_put_their_lines_side_by_side()
    {
        // The clearance is promised against the build region and the terrain, not between two marker lines: a
        // gap this narrow seats them touching rather than dropping one of them.
        var areas = new[] { (0, 0, 8, 8), (12, 0, 20, 8) };

        var cells = BuildMarkerStamper.MarkerCells(areas, NoHoles, Terrain());

        await Assert.That(Column(cells, 9)).IsEquivalentTo(Enumerable.Range(-2, 12).ToList());
        await Assert.That(Column(cells, 10)).IsEquivalentTo(Enumerable.Range(-2, 12).ToList());
        // Between them there is room for exactly those two: the next column in holds only the outer outline
        // passing over the gap, never a line of its own against the far region.
        await Assert.That(Column(cells, 11)).IsEquivalentTo(new List<int> { -2, 9 });
    }

    [Test]
    public async Task An_empty_build_region_marks_nothing()
    {
        await Assert.That(BuildMarkerStamper.MarkerCells([], NoHoles, Terrain())).IsEmpty();
    }

    [Test]
    public async Task The_stamp_lays_unpowered_wire_at_y_one_and_leaves_the_region_itself_clear()
    {
        var world = new VoxelWorld();
        BuildMarkerStamper.Stamp(world, [(0, 0, 8, 8)], NoHoles, Terrain());

        var wire = world.GetBlock(-2, 1, 0);
        await Assert.That(wire.Id).IsEqualTo(Blocks.RedstoneWire);
        await Assert.That(wire.Data).IsEqualTo(0);                // a marker, not a signal
        // The air gap and the region interior stay empty, and nothing is written at any other height.
        await Assert.That(world.GetBlock(-1, 1, 0).Id).IsEqualTo(Blocks.Air);
        await Assert.That(world.GetBlock(4, 1, 4).Id).IsEqualTo(Blocks.Air);
        await Assert.That(world.GetBlock(-2, 0, 0).Id).IsEqualTo(Blocks.Air);
        await Assert.That(world.GetBlock(-2, 2, 0).Id).IsEqualTo(Blocks.Air);
    }
}
