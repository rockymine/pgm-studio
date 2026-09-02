using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Render;

namespace PgmStudio.Minecraft.Tests.Render;

/// <summary>The vertical-slice stage image as characters, over an in-memory <see cref="VoxelWorld"/> — the
/// text twin of <see cref="SectionRenderTests"/>.</summary>
public sealed class SectionTextTests
{
    [Test]
    public async Task A_riser_prints_ground_up_to_the_surface_at_every_column()
    {
        // A flat floor at y4 for z 0..2, then a riser to y12 at z3 — a step of 8, the exact shape a
        // top-down render cannot show because it differs only in Y.
        var world = new VoxelWorld();
        for (var z = 0; z <= 2; z++)
            for (var y = 0; y <= 4; y++) world.SetBlock(10, y, z, Blocks.Stone);
        for (var y = 0; y <= 12; y++) world.SetBlock(10, y, 3, Blocks.Stone);

        var surface = new Dictionary<(int X, int Z), int> { [(10, 0)] = 4, [(10, 1)] = 4, [(10, 2)] = 4, [(10, 3)] = 12 };

        var text = SectionText.Render(world, new WorldProvenance(), surface, columns: null, groundLayer: "ground",
            SectionAxis.AlongZ, from: 0, to: 3, at: 10, yMin: 0, yMax: 12, depth: 0, every: 1);

        await Assert.That(text).IsNotNull();

        // y4 is where every column carries a block, and every one of them is at or below its own surface.
        await Assert.That(RowFor(text!, 4, highest: 12)).IsEqualTo("####");

        // y12 is the riser's own top: only the fourth column (z3) reaches it, and only it is ground there.
        var atTwelve = RowFor(text!, 12, highest: 12);
        await Assert.That(atTwelve[3]).IsEqualTo(SectionText.Ground);
        await Assert.That(atTwelve[..3]).DoesNotContain(SectionText.Ground.ToString());
    }

    [Test]
    public async Task The_key_names_every_character_used_and_nothing_else()
    {
        var world = new VoxelWorld();
        for (var z = 0; z <= 1; z++) world.SetBlock(0, 5, z, Blocks.Stone);
        var surface = new Dictionary<(int X, int Z), int> { [(0, 0)] = 5, [(0, 1)] = 5 };

        var text = SectionText.Render(world, new WorldProvenance(), surface, columns: null, groundLayer: "ground",
            SectionAxis.AlongZ, from: 0, to: 1, at: 0, yMin: 5, yMax: 6, depth: 0, every: 1);

        await Assert.That(text).IsNotNull();
        var key = text!.Split('\n').First(line => line.StartsWith("KEY"));
        await Assert.That(key).Contains("# ground");
        await Assert.That(key).DoesNotContain("~ liquid").Because("nothing on this cut is water");
        await Assert.That(key).DoesNotContain("L storey").Because("nothing on this cut stacks a second layer");
    }

    [Test]
    public async Task A_deck_two_blocks_over_the_ground_on_a_second_layer_prints_a_storey()
    {
        var world = new VoxelWorld();
        world.SetBlock(10, 7, 0, Blocks.Stone);

        var surface = new Dictionary<(int X, int Z), int> { [(10, 0)] = 5 };
        var columns = new List<ColumnSegment> { new(10, 0, 7, 8, "deck") };

        var text = SectionText.Render(world, new WorldProvenance(), surface, columns, groundLayer: "ground",
            SectionAxis.AlongX, from: 10, to: 10, at: 0, yMin: 5, yMax: 8, depth: 0, every: 1);

        await Assert.That(text).IsNotNull();
        await Assert.That(RowFor(text!, 7, highest: 8)).IsEqualTo(SectionText.Storey.ToString());

        var key = text!.Split('\n').First(line => line.StartsWith("KEY"));
        await Assert.That(key).Contains("L storey");
    }

    /// <summary>The glyphs of the row drawn for <paramref name="y"/>, with its label stripped off — rows are
    /// drawn top to bottom starting at line 2 (after the title and the key), one per Y from
    /// <paramref name="highest"/> down, so a row's position is fixed once its Y is.</summary>
    private static string RowFor(string text, int y, int highest)
    {
        var line = text.Split('\n')[2 + (highest - y)];
        var label = y % 4 == 0 ? $"y{y,3} " : "     ";
        return line[label.Length..];
    }
}
