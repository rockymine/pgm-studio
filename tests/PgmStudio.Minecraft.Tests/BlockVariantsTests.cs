using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Metadata normalization: which nibble bits are a block's sub-type and which are placement state. Every
/// case here is one the raw nibble would have missed.
/// </summary>
public sealed class BlockVariantsTests
{
    [Test]
    public async Task A_block_without_sub_types_ignores_its_metadata()
    {
        foreach (var meta in Enumerable.Range(0, 16))
        {
            await Assert.That(BlockVariants.Normalize(4, meta)).IsEqualTo(0);    // cobblestone
            await Assert.That(BlockVariants.Normalize(53, meta)).IsEqualTo(0);   // oak stairs: facing + half
            await Assert.That(BlockVariants.Normalize(8, meta)).IsEqualTo(0);    // water: flow level
        }
        await Assert.That(BlockVariants.HasVariants(4)).IsFalse();
    }

    [Test]
    public async Task Logs_and_leaves_keep_only_their_low_two_bits()
    {
        // 17: bits 0–1 wood, bits 2–3 axis (12–15 = bark on all six faces).
        await Assert.That(BlockVariants.Normalize(17, 0b1001)).IsEqualTo(1);     // east-west spruce
        await Assert.That(BlockVariants.Normalize(17, 0b1111)).IsEqualTo(3);     // all-bark jungle
        await Assert.That(BlockVariants.Normalize(162, 0b1101)).IsEqualTo(1);    // all-bark dark oak
        // 18: bit 2 = persistent, bit 3 = decay-check pending.
        await Assert.That(BlockVariants.Normalize(18, 0b0110)).IsEqualTo(2);     // persistent birch
        await Assert.That(BlockVariants.Normalize(161, 0b1100)).IsEqualTo(0);    // acacia, decay flagged
    }

    [Test]
    public async Task Halves_and_growth_stages_are_stripped()
    {
        await Assert.That(BlockVariants.Normalize(44, 0b1101)).IsEqualTo(5);     // upper stone-brick slab
        await Assert.That(BlockVariants.Normalize(126, 0b1010)).IsEqualTo(2);    // upper birch slab
        await Assert.That(BlockVariants.Normalize(6, 0b1100)).IsEqualTo(4);      // acacia sapling, stage 1
    }

    [Test]
    public async Task A_double_stone_slabs_seamless_forms_land_on_their_ordinary_twins()
    {
        await Assert.That(BlockVariants.Normalize(43, 8)).IsEqualTo(0);          // seamless stone
        await Assert.That(BlockVariants.Normalize(43, 9)).IsEqualTo(1);          // seamless sandstone
        await Assert.That(BlockVariants.Normalize(43, 15)).IsEqualTo(7);         // seamless quartz
    }

    [Test]
    public async Task The_three_shapes_a_mask_cannot_express()
    {
        // Anvil: damage in bits 2–3, facing in bits 0–1.
        await Assert.That(BlockVariants.Normalize(145, 0b0111)).IsEqualTo(1);    // chipped, facing 3
        await Assert.That(BlockVariants.Normalize(145, 0b1010)).IsEqualTo(2);    // damaged, facing 2
        // Quartz: the pillar's three axis values are one texture.
        await Assert.That(BlockVariants.Normalize(155, 1)).IsEqualTo(1);         // chiseled
        await Assert.That(BlockVariants.Normalize(155, 3)).IsEqualTo(2);
        await Assert.That(BlockVariants.Normalize(155, 4)).IsEqualTo(2);
        // Double plant: the upper half records facing, not which plant it belongs to.
        await Assert.That(BlockVariants.Normalize(175, 4)).IsEqualTo(4);         // lower rose bush
        await Assert.That(BlockVariants.Normalize(175, 9)).IsEqualTo(8);         // an upper half
    }

    [Test]
    public async Task Negative_and_out_of_range_input_normalizes_to_the_base_variant()
    {
        await Assert.That(BlockVariants.Normalize(35, -1)).IsEqualTo(0);
        await Assert.That(BlockVariants.Normalize(999, 12)).IsEqualTo(0);
        await Assert.That(BlockVariants.HasVariants(999)).IsFalse();
    }

    [Test]
    public async Task Normalization_always_lands_inside_the_nibble()
    {
        for (var id = 0; id < BlockVariants.BlockIdCount; id++)
            for (var meta = 0; meta < 16; meta++)
            {
                var normalized = BlockVariants.Normalize(id, meta);
                await Assert.That(normalized).IsGreaterThanOrEqualTo(0);
                await Assert.That(normalized).IsLessThan(16);
            }
    }
}
