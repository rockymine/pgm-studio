using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The relief read as a grid of worst-neighbour steps, classed by <c>Walk</c>'s own tiers.</summary>
public sealed class SlopeGridTests
{
    [Test]
    public async Task A_flat_board_is_all_walked()
    {
        var surface = new Dictionary<(int X, int Z), int>();
        for (var x = 0; x <= 5; x++) for (var z = 0; z <= 5; z++) surface[(x, z)] = 10;

        var grid = SlopeGrid.Build(surface, every: 1)!;

        await Assert.That(grid.Barrier).IsEqualTo(0);
        await Assert.That(grid.Scrambled).IsEqualTo(0);
        await Assert.That(grid.Walked).IsEqualTo(surface.Count);
        await Assert.That(grid.Faces).IsEmpty();

        foreach (var row in SlopeGrid.Rows(grid))
            await Assert.That(row).IsEqualTo(new string('0', row.Length));
    }

    [Test]
    public async Task An_8_block_riser_reads_as_a_barrier_face_across_the_step()
    {
        // A plateau at y0 for x 0..4 meeting one at y8 for x 5..9: both columns straddling the step read the
        // 8-block rise to their neighbour, so the step prints as one 8-connected face astride it.
        var surface = new Dictionary<(int X, int Z), int>();
        for (var z = 0; z <= 4; z++)
        {
            for (var x = 0; x <= 4; x++) surface[(x, z)] = 0;
            for (var x = 5; x <= 9; x++) surface[(x, z)] = 8;
        }

        var grid = SlopeGrid.Build(surface, every: 1)!;

        await Assert.That(grid.Barrier).IsEqualTo(10);            // x=4 and x=5, five rows each
        await Assert.That(grid.Walked).IsEqualTo(surface.Count - 10);
        await Assert.That(grid.Faces.Count).IsEqualTo(1);
        var face = grid.Faces[0];
        await Assert.That(face.Cells).IsEqualTo(10);
        await Assert.That(face.MinX).IsEqualTo(4);
        await Assert.That(face.MaxX).IsEqualTo(5);
        await Assert.That(face.MinZ).IsEqualTo(0);
        await Assert.That(face.MaxZ).IsEqualTo(4);

        var text = SlopeGrid.Render(grid);
        await Assert.That(text).Contains("faces: 1, largest 10 at x 4..5 z 0..4");
    }

    [Test]
    public async Task Counts_sum_to_the_sampled_cells_ground_or_void()
    {
        var surface = new Dictionary<(int X, int Z), int>();
        for (var x = 0; x <= 3; x++)
            for (var z = 0; z <= 3; z++)
                if (x != 2 || z != 2) surface[(x, z)] = 5;   // one void cell inside the sampled box

        var grid = SlopeGrid.Build(surface, every: 1)!;

        var sampled = grid.Width * grid.Height;
        var voided = grid.Classes.Count(stepClass => stepClass is null);
        await Assert.That(grid.Walked + grid.Scrambled + grid.Barrier + voided).IsEqualTo(sampled);
    }

    [Test]
    public async Task A_board_with_no_ground_column_has_no_grid()
    {
        await Assert.That(SlopeGrid.Build(new Dictionary<(int X, int Z), int>(), every: 1)).IsNull();
    }
}
