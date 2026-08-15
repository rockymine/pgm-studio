using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

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
    public async Task A_data_variant_is_offered_as_its_own_block_with_its_own_name_and_colour()
    {
        // Andesite is stone with a data value of 5, and until it was offered it could only be reached by
        // hand-writing the pair. It is a different block from stone, so it carries its own name and colour.
        var andesite = TerrainPalette.Paintable.Single(block => block is { Id: Blocks.Stone, Data: 5 });
        await Assert.That(andesite.Name).IsEqualTo("Andesite");
        await Assert.That(andesite.Hex).IsNotEqualTo(BlockPalette.Hex(Blocks.Stone, 0));
    }

    [Test]
    public async Task A_variant_takes_the_family_of_the_ground_it_reads_as_not_of_the_id_it_shares()
    {
        // Granite and andesite are both stone by id, and grouping them by that would put a warm rock in with
        // the grey — the single thing a family exists to distinguish.
        await Assert.That(TerrainPalette.FamilyOf(Blocks.Stone, 1)).IsEqualTo("brick");
        await Assert.That(TerrainPalette.FamilyOf(Blocks.Stone, 5)).IsEqualTo("grey stone");
        await Assert.That(TerrainPalette.FamilyOf(Blocks.Stone, 0)).IsEqualTo("grey stone");
    }

    [Test]
    public async Task Every_family_is_named_coloured_and_whole()
    {
        await Assert.That(TerrainPalette.Families).IsNotEmpty();
        var offered = TerrainPalette.Paintable.Select(block => (block.Id, block.Data)).ToHashSet();
        foreach (var family in TerrainPalette.Families)
        {
            await Assert.That(family.Name).IsNotEmpty();
            await Assert.That(family.Blocks).IsNotEmpty();
            await Assert.That(TerrainPalette.ColourOf(family.Name, fallback: -1)).IsEqualTo(family.Rgb);
            // A family is applied as a whole palette, so every block in it has to be one the picker can then
            // show — a member missing from the offered list would paint but could not be re-picked.
            foreach (var block in family.Blocks)
            {
                await Assert.That(offered.Contains((block.Id, block.Data))).IsTrue();
                await Assert.That(TerrainPalette.FamilyOf(block.Id, block.Data)).IsEqualTo(family.Name);
            }
        }
    }

    [Test]
    public async Task No_block_belongs_to_two_families()
    {
        var members = TerrainPalette.Families.SelectMany(family => family.Blocks)
            .Select(block => (block.Id, block.Data)).ToList();
        await Assert.That(members.Count).IsEqualTo(members.Distinct().Count());
    }

    [Test]
    public async Task A_block_is_flagged_in_a_family_exactly_when_its_group_is_one()
    {
        var families = TerrainPalette.Families.Select(family => family.Name).ToHashSet();
        foreach (var block in TerrainPalette.Paintable)
            await Assert.That(block.InFamily).IsEqualTo(families.Contains(block.Group));
    }

    [Test]
    public async Task A_shade_a_family_claims_is_offered_under_that_family()
    {
        // Lime stained clay is a shade of 159 and also the light end of the green ground. It belongs to the
        // family — the shade row reads every damage value of an id regardless of group, so the sixteen stay
        // whole either way, and it is the family that would otherwise lose a member it needs.
        var lime = TerrainPalette.Paintable.Single(block => block is { Id: Blocks.StainedClay, Data: 5 });
        await Assert.That(lime.Group).IsEqualTo("verdant");
        await Assert.That(lime.InFamily).IsTrue();

        // Stained glass names no tone at all — every shade still falls through to the plain shade row.
        var white = TerrainPalette.Paintable.Single(block => block is { Id: Blocks.StainedGlass, Data: 0 });
        await Assert.That(white.Group).IsEqualTo("Stained glass");
        await Assert.That(white.InFamily).IsFalse();
    }

    [Test]
    public async Task Every_stained_clay_shade_resolves_to_a_tone_family()
    {
        // Half the ramp used to fall through to the plain "Stained clay" shade row — a themed board built on
        // the other eight dyes painted mostly as unnamed magenta.
        for (var data = 0; data < 16; data++)
            await Assert.That(TerrainPalette.FamilyOf(Blocks.StainedClay, data)).IsNotNull();
    }

    [Test]
    public async Task Hay_bale_and_packed_ice_resolve_to_a_tone_family()
    {
        await Assert.That(TerrainPalette.FamilyOf(170, 0)).IsNotNull(); // hay bale
        await Assert.That(TerrainPalette.FamilyOf(174, 0)).IsNotNull(); // packed ice
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
        LayeredMaterial layered => layered.Stack.Bands.SelectMany(layer => BlocksOf(layer.Material)),
        VoronoiMaterial voronoi => voronoi.Bands.SelectMany(band => BlocksOf(band.Material)),
        CellMaterial cell => cell.Palette.SelectMany(BlocksOf),
        FieldPatternMaterial field => field.Stops.SelectMany(BlocksOf),
        WallRunMaterial run => run.Runs.SelectMany(stripe => BlocksOf(stripe.Material)),
        _ => [],
    };
}
