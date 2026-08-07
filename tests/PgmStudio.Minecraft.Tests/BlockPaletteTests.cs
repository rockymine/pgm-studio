using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Block-colour lookup tests: the metadata-aware hierarchy (exact sub-type → base → hash), the sub-type
/// families that used to collapse onto one colour, the three separate dye ramps, naming, and coverage of
/// the whole 1.8 id range.
/// </summary>
public sealed class BlockPaletteTests
{
    [Test]
    public async Task Sub_types_of_one_block_get_distinct_colours()
    {
        // Stone (1): granite, diorite and andesite are three materials, not three shades of "stone grey".
        string[] stones = [.. Enumerable.Range(0, 7).Select(meta => BlockPalette.Hex(1, meta))];
        await Assert.That(stones.Distinct().Count()).IsEqualTo(7);

        // Planks (5): six woods, six colours.
        string[] planks = [.. Enumerable.Range(0, 6).Select(meta => BlockPalette.Hex(5, meta))];
        await Assert.That(planks.Distinct().Count()).IsEqualTo(6);

        // Dirt (3): dirt, coarse dirt, podzol.
        await Assert.That(BlockPalette.Hex(3, 0)).IsNotEqualTo(BlockPalette.Hex(3, 2));
    }

    [Test]
    public async Task A_wood_keeps_one_colour_across_every_id_it_owns()
    {
        // Spruce planks (5:1), spruce slab (126:1), spruce stairs (134) and spruce fence (188) are the same
        // material, so they are the same colour — and none of them is oak.
        var spruce = BlockPalette.Hex(5, 1);
        await Assert.That(BlockPalette.Hex(126, 1)).IsEqualTo(spruce);
        await Assert.That(BlockPalette.Hex(134, 0)).IsEqualTo(spruce);
        await Assert.That(BlockPalette.Hex(188, 0)).IsEqualTo(spruce);
        await Assert.That(spruce).IsNotEqualTo(BlockPalette.Hex(5, 0));
    }

    [Test]
    public async Task Doors_and_gates_take_their_own_wood()
    {
        // 193–197 run spruce, birch, jungle, acacia, dark oak; 183–187 run spruce, birch, jungle, dark oak,
        // acacia. The two families are numbered in different orders, so each needs its own colour.
        await Assert.That(BlockPalette.Name(193, 0)).IsEqualTo("Spruce Door");
        await Assert.That(BlockPalette.Name(196, 0)).IsEqualTo("Acacia Door");
        await Assert.That(BlockPalette.Name(186, 0)).IsEqualTo("Dark Oak Fence Gate");
        await Assert.That(BlockPalette.Name(187, 0)).IsEqualTo("Acacia Fence Gate");
        await Assert.That(BlockPalette.Hex(193, 0)).IsNotEqualTo(BlockPalette.Hex(194, 0));
    }

    [Test]
    public async Task Rotated_and_flagged_metadata_resolves_to_its_sub_type()
    {
        // A log's axis lives in bits 2–3: 17:8 is an east-west oak log, not an unknown block.
        await Assert.That(BlockPalette.Hex(17, 8)).IsEqualTo(BlockPalette.Hex(17, 0));
        await Assert.That(BlockPalette.Hex(17, 9)).IsEqualTo(BlockPalette.Hex(17, 1));
        // Leaf decay/persistence lives in bits 2–3 the same way.
        await Assert.That(BlockPalette.Hex(18, 6)).IsEqualTo(BlockPalette.Hex(18, 2));
        // A slab's upper-half flag is bit 3.
        await Assert.That(BlockPalette.Hex(126, 12)).IsEqualTo(BlockPalette.Hex(126, 4));
        // A sapling's growth stage is bit 3.
        await Assert.That(BlockPalette.Hex(6, 11)).IsEqualTo(BlockPalette.Hex(6, 3));
    }

    [Test]
    public async Task The_three_dye_families_are_three_different_ramps()
    {
        // Brown wool, brown stained glass and brown stained clay are three materials wearing one dye.
        var hexes = new[] { BlockPalette.Hex(35, 12), BlockPalette.Hex(95, 12), BlockPalette.Hex(159, 12) };
        await Assert.That(hexes.Distinct().Count()).IsEqualTo(3);
        // Carpet follows wool, panes follow glass.
        await Assert.That(BlockPalette.Hex(171, 12)).IsEqualTo(BlockPalette.Hex(35, 12));
        await Assert.That(BlockPalette.Hex(160, 12)).IsEqualTo(BlockPalette.Hex(95, 12));
        // Each ramp's sixteen colours are sixteen colours.
        foreach (var id in (int[])[35, 95, 159, 160, 171])
        {
            string[] ramp = [.. Enumerable.Range(0, 16).Select(dye => BlockPalette.Hex(id, dye))];
            await Assert.That(ramp.Distinct().Count()).IsEqualTo(16);
        }
    }

    [Test]
    public async Task Unclaimed_metadata_falls_back_to_the_block_base()
    {
        // Stone has seven sub-types; 7 is not one of them, so it resolves to the base (= plain stone).
        await Assert.That(BlockPalette.Hex(1, 7)).IsEqualTo(BlockPalette.Hex(1, -1));
        await Assert.That(BlockPalette.Hex(1, -1)).IsEqualTo(BlockPalette.Hex(1, 0));
        // A double plant's upper half does not record which plant it is, so it lands on the base.
        await Assert.That(BlockPalette.Hex(175, 8)).IsEqualTo(BlockPalette.Hex(175, -1));
        // A block with no sub-types at all ignores its metadata entirely.
        await Assert.That(BlockPalette.Hex(4, 11)).IsEqualTo(BlockPalette.Hex(4, 0));
    }

    [Test]
    public async Task Ores_are_told_apart_by_their_accent()
    {
        // Averaged raw, every ore is the stone it sits in. Gold, iron, coal, lapis, diamond, redstone,
        // emerald and quartz ore must each be distinct from plain stone and from each other.
        string[] ores = [.. ((int[])[14, 15, 16, 21, 56, 73, 129, 153]).Select(id => BlockPalette.Hex(id, 0))];
        await Assert.That(ores.Distinct().Count()).IsEqualTo(ores.Length);
        await Assert.That(ores).DoesNotContain(BlockPalette.Hex(1, 0));
    }

    [Test]
    public async Task Names_carry_the_sub_type_and_any_data_does_not()
    {
        await Assert.That(BlockPalette.Name(1, 1)).IsEqualTo("Granite");
        await Assert.That(BlockPalette.Name(35, 14)).IsEqualTo("Red Wool");
        await Assert.That(BlockPalette.Name(95, 3)).IsEqualTo("Light Blue Stained Glass");
        await Assert.That(BlockPalette.Name(17, 9)).IsEqualTo("Spruce Log");   // axis bits stripped
        await Assert.That(BlockPalette.Name(35, -1)).IsEqualTo("Wool");        // "any" names no colour
        await Assert.That(BlockPalette.Name(999, 0)).IsEqualTo("Block 999");   // unknown
    }

    [Test]
    public async Task Every_block_in_the_supported_id_range_is_named_and_coloured()
    {
        // Ids 0–197 are the whole 1.8 block set: none of them may reach the unknown-block fallback.
        for (var id = 0; id <= 197; id++)
        {
            await Assert.That(BlockPalette.Name(id, 0)).DoesNotStartWith("Block ");
            await Assert.That(BlockPalette.Hex(id, 0).Length).IsEqualTo(7);
        }
    }

    [Test]
    public async Task The_three_accessors_agree()
    {
        foreach (var (id, data) in ((int, int)[])[(1, 3), (35, 14), (17, 9), (250, 3), (0, 0)])
        {
            var packed = BlockPalette.PackedRgb(id, data);
            var rgb = BlockPalette.Color(id, data);
            await Assert.That((rgb.R << 16) | (rgb.G << 8) | rgb.B).IsEqualTo(packed);
            await Assert.That(BlockPalette.Hex(id, data)).IsEqualTo($"#{packed:x6}");
        }
    }

    [Test]
    public async Task Unknown_block_fallback_is_deterministic_and_valid_hex()
    {
        var first = BlockPalette.Hex(250, 3);
        var second = BlockPalette.Hex(250, 3);
        await Assert.That(first).IsEqualTo(second);                 // stable
        await Assert.That(first.Length).IsEqualTo(7);               // "#rrggbb"
        await Assert.That(first[0]).IsEqualTo('#');
        await Assert.That(first).IsNotEqualTo(BlockPalette.Hex(251, 3));
    }
}
