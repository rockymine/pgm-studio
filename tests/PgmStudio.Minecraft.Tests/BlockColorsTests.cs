using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// P5 block-colour tests. The full known-table parity vs the Python <c>colors.py</c> oracle lives in
/// the harness (<c>tools/PgmStudio.RoundTrip --colors</c>, 197/197); these cover the lookup shape:
/// known blocks, stained sub-colours, the (-1) any-data fallback, names, and the unknown fallback.
/// </summary>
public sealed class BlockColorsTests
{
    [Test]
    public async Task Known_block_maps_to_its_map_colour()
    {
        await Assert.That(BlockColors.Hex(1, 0)).IsEqualTo("#707070");    // Stone (112,112,112)
        await Assert.That(BlockColors.Hex(2, -1)).IsEqualTo("#7fb238");   // Grass (127,178,56)
    }

    [Test]
    public async Task Stained_block_uses_its_damage_as_a_sub_colour()
    {
        await Assert.That(BlockColors.Hex(35, 14)).IsEqualTo("#993333");  // Red wool (153,51,51)
        await Assert.That(BlockColors.Hex(35, 0)).IsEqualTo("#dddddd");   // White wool (221,221,221)
    }

    [Test]
    public async Task Any_data_falls_back_to_the_minus_one_entry()
    {
        // Stone has no per-damage entries, so any data value resolves via (1, -1).
        await Assert.That(BlockColors.Hex(1, 7)).IsEqualTo(BlockColors.Hex(1, -1));
        // A stained block with no explicit damage entry → StainColors[0] (white).
        await Assert.That(BlockColors.Hex(35, -1)).IsEqualTo("#dddddd");
    }

    [Test]
    public async Task Names_format_known_and_stained_blocks()
    {
        await Assert.That(BlockColors.Name(1, 0)).IsEqualTo("Stone");
        await Assert.That(BlockColors.Name(35, 14)).IsEqualTo("Red Wool");
        await Assert.That(BlockColors.Name(95, 3)).IsEqualTo("Light Blue Stained Glass");
        await Assert.That(BlockColors.Name(999, 0)).IsEqualTo("Block 999");   // unknown
    }

    [Test]
    public async Task Unknown_block_fallback_is_deterministic_and_valid_hex()
    {
        var a = BlockColors.Hex(250, 3);
        var b = BlockColors.Hex(250, 3);
        await Assert.That(a).IsEqualTo(b);                 // stable
        await Assert.That(a.Length).IsEqualTo(7);          // "#rrggbb"
        await Assert.That(a[0]).IsEqualTo('#');
    }
}
