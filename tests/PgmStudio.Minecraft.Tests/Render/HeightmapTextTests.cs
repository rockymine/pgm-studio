using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The heightmap's text twin: a height-banded grid over the board's own surface, with the map's
/// spawns, goals, houses and water overprinted.</summary>
public sealed class HeightmapTextTests
{
    private static Dictionary<(int X, int Z), int> Surface(params ((int X, int Z) Cell, int Height)[] cells) =>
        cells.ToDictionary(entry => entry.Cell, entry => entry.Height);

    [Test]
    public async Task Two_ground_heights_print_two_distinct_band_characters()
    {
        var surface = Surface(((0, 0), 5), ((1, 0), 14));
        var text = HeightmapText.Render(surface, new WorldProvenance(), [], every: 1)!;

        var row = text.TrimEnd('\n').Split('\n')[4][5..];   // past the "{z,4} " prefix
        await Assert.That(row.Length).IsEqualTo(2);
        await Assert.That(row[0]).IsNotEqualTo(row[1]);
    }

    [Test]
    public async Task A_spawn_marker_overprints_its_cell_and_the_key_names_it()
    {
        var surface = Surface(((0, 0), 5), ((1, 0), 14));
        var text = HeightmapText.Render(surface, new WorldProvenance(), [("spawn", 0, 0)], every: 1)!;
        var lines = text.TrimEnd('\n').Split('\n');

        await Assert.That(lines[1]).Contains("@ spawn point");
        await Assert.That(lines[4][5..]).StartsWith("@");
    }

    [Test]
    public async Task The_extent_line_matches_the_surfaces_bounds()
    {
        var surface = Surface(((-5, 2), 0), ((10, 20), 8));
        var text = HeightmapText.Render(surface, new WorldProvenance(), [], every: 1)!;

        await Assert.That(text.Split('\n')[0]).Contains("x -5..10 across, z 2..20 down");
    }

    [Test]
    public async Task Every_2_halves_the_grid_width()
    {
        var surface = new Dictionary<(int X, int Z), int>();
        for (var x = 0; x <= 9; x++) surface[(x, 0)] = 5;

        var one = HeightmapText.Render(surface, new WorldProvenance(), [], every: 1)!;
        var two = HeightmapText.Render(surface, new WorldProvenance(), [], every: 2)!;

        var widthOne = one.TrimEnd('\n').Split('\n')[4][5..].Length;
        var widthTwo = two.TrimEnd('\n').Split('\n')[4][5..].Length;
        await Assert.That(widthTwo).IsEqualTo(widthOne / 2);
    }

    [Test]
    public async Task A_board_with_no_ground_column_has_nothing_to_print()
    {
        await Assert.That(HeightmapText.Render(new Dictionary<(int X, int Z), int>(), new WorldProvenance(), [], every: 1))
            .IsNull();
    }
}
