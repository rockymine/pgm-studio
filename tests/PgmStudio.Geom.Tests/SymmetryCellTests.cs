using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// The cell half of the symmetry transform. Every case here asserts a property a <em>point</em> reflection
/// does not have, because that is the whole distinction: a cell is a unit square, so its image is the square
/// the transform lands on rather than the index its corner lands on.
/// </summary>
public sealed class SymmetryCellTests
{
    [Test]
    [Arguments(-45)] [Arguments(-1)] [Arguments(0)] [Arguments(7)] [Arguments(44)]
    public async Task A_cell_and_its_rot180_image_straddle_the_boundary_they_turn_about(int x)
    {
        // The centre is the grid line at 0, so a cell and its image lie one on each side of it and their
        // indices sum to -1. A point reflection sums them to 0, which is the off-by-one.
        var (imageX, imageZ) = Symmetry.Cell(x, x, "rot_180", 0, 0, 1);

        await Assert.That(x + imageX).IsEqualTo(-1);
        await Assert.That(x + imageZ).IsEqualTo(-1);
    }

    [Test]
    public async Task A_board_of_even_extent_maps_onto_itself()
    {
        // firnline's own extent. Every cell of a board whose centre is a boundary has its image on the same
        // board; a point reflection walks one cell off the far edge and leaves the near edge unpaired.
        const int MinX = -45, MaxX = 44;

        for (var x = MinX; x <= MaxX; x++)
        {
            var (imageX, _) = Symmetry.Cell(x, 0, "rot_180", 0, 0, 1);
            await Assert.That(imageX).IsGreaterThanOrEqualTo(MinX);
            await Assert.That(imageX).IsLessThanOrEqualTo(MaxX);
        }
    }

    [Test]
    public async Task Every_cell_of_an_even_board_is_the_image_of_exactly_one_other()
    {
        // The pairing is a bijection, which is what "the board mirrors" means and what a picture of it shows.
        const int MinX = -45, MaxX = 44;
        var images = new HashSet<int>();

        for (var x = MinX; x <= MaxX; x++) images.Add(Symmetry.Cell(x, 0, "rot_180", 0, 0, 1).X);

        await Assert.That(images.Count).IsEqualTo(MaxX - MinX + 1);
    }

    [Test]
    [Arguments("rot_180")] [Arguments("mirror_x")] [Arguments("mirror_z")]
    [Arguments("mirror_d1")] [Arguments("mirror_d2")]
    public async Task An_order_two_mode_takes_a_cell_back_to_itself(string mode)
    {
        foreach (var (x, z) in new[] { (0, 0), (-1, -1), (13, -8), (-45, 44) })
        {
            var once = Symmetry.Cell(x, z, mode, 0, 0, 1);
            var twice = Symmetry.Cell(once.X, once.Z, mode, 0, 0, 1);
            await Assert.That(twice).IsEqualTo((x, z));
        }
    }

    [Test]
    public async Task Image_zero_is_the_cell_itself()
    {
        await Assert.That(Symmetry.Cell(13, -8, "rot_180", 0, 0, 0)).IsEqualTo((13, -8));
    }

    [Test]
    public async Task A_cell_rect_keeps_the_footprint_it_had()
    {
        // A stamp covers as many blocks after the turn as before it — the property that fails when a rect's
        // bounds are reflected as positions and re-floored, because one edge rounds inward.
        var (minX, minZ, maxX, maxZ) = Symmetry.CellRect(25, 66, 34, 78, "rot_180", 0, 0, 1);

        await Assert.That(maxX - minX + 1).IsEqualTo(34 - 25 + 1);
        await Assert.That(maxZ - minZ + 1).IsEqualTo(78 - 66 + 1);
        await Assert.That((minX, minZ, maxX, maxZ)).IsEqualTo((-35, -79, -26, -67));
    }

    [Test]
    public async Task A_quarter_turn_swaps_a_rect_width_and_depth()
    {
        var (minX, minZ, maxX, maxZ) = Symmetry.CellRect(2, 5, 11, 9, "rot_90", 0, 0, 1);

        await Assert.That(maxX - minX + 1).IsEqualTo(9 - 5 + 1);     // the old depth is the new width
        await Assert.That(maxZ - minZ + 1).IsEqualTo(11 - 2 + 1);
    }

    [Test]
    public async Task A_rect_and_its_image_cover_exactly_each_other_s_cells()
    {
        // The invariant stated over the cells themselves rather than over the bounds: the image of the rect
        // is the set of images of its cells, with nothing gained and nothing dropped.
        var (minX, minZ, maxX, maxZ) = Symmetry.CellRect(-34, 66, -25, 78, "rot_180", 0, 0, 1);

        var fromCells = new HashSet<(int, int)>();
        for (var z = 66; z <= 78; z++)
        for (var x = -34; x <= -25; x++)
            fromCells.Add(Symmetry.Cell(x, z, "rot_180", 0, 0, 1));

        var fromRect = new HashSet<(int, int)>();
        for (var z = minZ; z <= maxZ; z++)
        for (var x = minX; x <= maxX; x++)
            fromRect.Add((x, z));

        await Assert.That(fromRect).IsEquivalentTo(fromCells);
    }

    [Test]
    public async Task A_rect_that_spans_the_centre_is_its_own_image()
    {
        // A footprint centred on the board's own centre must come back unchanged, or a centre-piece drifts
        // every time the board is fanned.
        await Assert.That(Symmetry.CellRect(-6, -3, 5, 2, "rot_180", 0, 0, 1)).IsEqualTo((-6, -3, 5, 2));
    }
}
