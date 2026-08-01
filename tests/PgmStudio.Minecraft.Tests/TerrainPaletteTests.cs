using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The blocks a terrain-paint theme is offered — the vocabulary the Theme rail's picker shows. Every entry has
/// to be a block the palette actually knows, since a picker swatch promises the colour the export will place;
/// the three sixteen-colour families have to be whole, since the picker offers them as a colour row; and the
/// shipping default theme has to be expressible in what is offered, or a theme could be painted but not
/// re-picked.
/// </summary>
public sealed class TerrainPaletteTests
{
    [Test]
    public async Task Every_offered_block_is_one_the_palette_knows()
    {
        foreach (var block in TerrainPalette.Paintable)
        {
            // BlockPalette answers "Block <id>" for a block it has no name for — an entry reaching that is a
            // typo in the offer list, not a block.
            await Assert.That(block.Name).IsNotEqualTo($"Block {block.Id}");
            await Assert.That(block.Name).IsEqualTo(BlockPalette.Name(block.Id, block.Data));
            await Assert.That(block.Hex).IsEqualTo(BlockPalette.Hex(block.Id, block.Data));
            await Assert.That(block.Group).IsNotEmpty();
        }
    }

    [Test]
    public async Task No_block_is_offered_twice()
    {
        var pairs = TerrainPalette.Paintable.Select(b => (b.Id, b.Data)).ToList();
        await Assert.That(pairs.Count).IsEqualTo(pairs.Distinct().Count());
    }

    [Test]
    public async Task Each_sixteen_colour_family_offers_all_of_its_shades()
    {
        foreach (var family in new[] { Blocks.StainedClay, Blocks.Wool, Blocks.StainedGlass })
        {
            var shades = TerrainPalette.Paintable.Where(b => b.Id == family).Select(b => b.Data).ToList();
            await Assert.That(shades.Order().ToList()).IsEquivalentTo(Enumerable.Range(0, 16).ToList());
        }
    }

    [Test]
    public async Task The_default_theme_is_built_from_blocks_the_picker_offers()
    {
        var offered = TerrainPalette.Paintable.Select(b => (b.Id, b.Data)).ToHashSet();
        foreach (var bucket in new[] { TerrainBucket.Rim, TerrainBucket.Surface, TerrainBucket.Wall, TerrainBucket.Fill })
            foreach (var (id, data) in BlocksOf(TerrainTheme.Default.MaterialFor(bucket)))
                await Assert.That(offered.Contains((id, data))).IsTrue();
    }

    // Every block a material can place, walking into the composites and patterns it nests.
    private static IEnumerable<(int Id, int Data)> BlocksOf(TerrainMaterial material) => material switch
    {
        SolidMaterial solid => [(solid.Id, solid.Data)],
        // a tint places its block in all sixteen shades, so the family has to be offered whole
        TeamTintedMaterial tint => Enumerable.Range(0, 16).Select(data => (tint.BlockId, data)).Concat(BlocksOf(tint.Neutral)),
        LayeredMaterial layered => layered.Layers.SelectMany(layer => BlocksOf(layer.Material)),
        VoronoiMaterial voronoi => voronoi.Palette.SelectMany(BlocksOf),
        NoiseMaterial noise => noise.Stops.SelectMany(BlocksOf),
        WallRunMaterial run => run.Runs.SelectMany(stripe => BlocksOf(stripe.Material)),
        _ => [],
    };
}
