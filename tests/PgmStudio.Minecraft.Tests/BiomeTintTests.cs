using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// What a biome does to a block's colour. The claims are the ones the rescale rests on: an untinted block is
/// itself everywhere, a tinted one moves with the biome, the biome the palette was baked at moves nothing,
/// and swampland is two-tone over a column while every other biome is flat.
/// </summary>
public sealed class BiomeTintTests
{
    private const int GrassBlock = 2;
    private const int OakLeaves = 18;
    private const int SpruceLeaves = 18;
    private const int Cobblestone = 4;

    [Test]
    public async Task A_block_the_biome_does_not_touch_is_itself_everywhere()
    {
        await Assert.That(BlockTints.Of(Cobblestone, 0)).IsEqualTo(TintChannel.None);
        await Assert.That(BlockPalette.PackedRgbIn(Cobblestone, 0, Biome.Desert, 0, 0))
            .IsEqualTo(BlockPalette.PackedRgb(Cobblestone, 0));
    }

    /// <summary>The forest tint is what the palette baked in, so a forest column is the stored colour and the
    /// rescale is a no-op there. Every render before a biome could be sampled showed exactly this.</summary>
    [Test]
    public async Task The_biome_the_palette_was_baked_at_moves_nothing()
    {
        await Assert.That(BlockPalette.PackedRgbIn(GrassBlock, 0, Biome.Forest, 0, 0))
            .IsEqualTo(BlockPalette.PackedRgb(GrassBlock, 0));
    }

    /// <summary>A grass block's texture is nearly white, so its colour in a biome is that biome's own grass
    /// colour — which is what makes a desert board read as desert in a picture.</summary>
    [Test]
    public async Task Grass_takes_the_biomes_own_colour()
    {
        var desert = BlockPalette.PackedRgbIn(GrassBlock, 0, Biome.Desert, 0, 0);

        await Assert.That(desert).IsNotEqualTo(BlockPalette.PackedRgb(GrassBlock, 0));
        await Assert.That((uint)desert).IsEqualTo(BiomeTint.Of(Biome.Desert, TintChannel.Grass, 0, 0));
    }

    /// <summary>Oak leaves follow the ground; spruce leaves carry the constant tint the game applies to them
    /// everywhere, so they are the same green in a desert as in a forest.</summary>
    [Test]
    public async Task Only_the_biome_tinted_woods_leaves_move()
    {
        await Assert.That(BlockTints.Of(OakLeaves, 0)).IsEqualTo(TintChannel.Foliage);
        await Assert.That(BlockTints.Of(SpruceLeaves, 1)).IsEqualTo(TintChannel.None);
        await Assert.That(BlockPalette.PackedRgbIn(SpruceLeaves, 1, Biome.Desert, 0, 0))
            .IsEqualTo(BlockPalette.PackedRgb(SpruceLeaves, 1));
        await Assert.That(BlockPalette.PackedRgbIn(OakLeaves, 0, Biome.Desert, 0, 0))
            .IsNotEqualTo(BlockPalette.PackedRgb(OakLeaves, 0));
    }

    /// <summary>Every biome but swampland answers one grass colour over the whole board; swampland answers
    /// two, which is what makes its ground read splotchy whatever a field says about it.</summary>
    [Test]
    public async Task Swampland_is_the_one_biome_whose_grass_varies_over_the_board()
    {
        var flatSeen = new HashSet<uint>();
        var swampSeen = new HashSet<uint>();
        for (var x = 0; x < 400; x += 7)
            for (var z = 0; z < 400; z += 7)
            {
                flatSeen.Add(BiomeTint.Of(Biome.Forest, TintChannel.Grass, x, z));
                swampSeen.Add(BiomeTint.Of(Biome.Swampland, TintChannel.Grass, x, z));
            }

        await Assert.That(flatSeen.Count).IsEqualTo(1);
        await Assert.That(swampSeen.Count).IsEqualTo(2);
    }

    /// <summary>Water is white everywhere but swampland, which is the one biome that tints it.</summary>
    [Test]
    public async Task Only_swampland_tints_water()
    {
        await Assert.That(BiomeTint.Of(Biome.Jungle, TintChannel.Water, 0, 0))
            .IsEqualTo(BiomeTint.ReferenceWater);
        await Assert.That(BiomeTint.Of(Biome.Swampland, TintChannel.Water, 0, 0))
            .IsNotEqualTo(BiomeTint.ReferenceWater);
    }

    /// <summary>Every biome the picker offers has a tint of its own to show, and one no row names falls to
    /// plains rather than to nothing.</summary>
    [Test]
    public async Task Every_named_biome_answers_a_tint_and_an_unnamed_one_falls_to_plains()
    {
        foreach (var (id, _) in Biome.All)
            await Assert.That(BiomeTint.Of(id, TintChannel.Grass, 0, 0)).IsNotEqualTo(0u);

        await Assert.That(BiomeTint.Of(200, TintChannel.Grass, 0, 0))
            .IsEqualTo(BiomeTint.Of(Biome.Plains, TintChannel.Grass, 0, 0));
    }
}
