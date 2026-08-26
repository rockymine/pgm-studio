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

    /// <summary>A family whose own colour is a grey holds blocks that read as one. A warm block among neutrals
    /// is a block an author cannot reach for: asking the picker for the pale grey and getting a yellow-tan is
    /// the swatch lying about what it offers, which is what `opus5-sandcaster` painted with when the two
    /// mushroom blocks sat in pale stone.
    ///
    /// <para>Measured as <b>warmth</b> — the spread between a colour's highest and lowest channel — because
    /// distance to the family's average does not see it: brown mushroom is 42 apart on its channels and only
    /// 42 from pale stone's own colour, which is inside what an ordinary member spends. Two exceptions the
    /// palette's docstring already names are allowed by name rather than by rule: an ore sits with the stone
    /// it is embedded in, and gravel and mossy cobble go by use.</para></summary>
    [Test]
    public async Task A_neutral_family_holds_no_warm_block()
    {
        // What the family's own colour has to spend before it stops being a grey, and what a block in one may.
        const int NeutralFamily = 8, WarmestMember = 25;
        var byUse = new HashSet<(int, int)> { (15, 0), (16, 0), (13, 0), (48, 0), (98, 1) };

        static int Warmth(string hex)
        {
            var value = Convert.ToInt32(hex.TrimStart('#'), 16);
            int red = (value >> 16) & 255, green = (value >> 8) & 255, blue = value & 255;
            return Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue));
        }

        foreach (var family in TerrainPalette.Families)
        {
            if (Warmth(family.Hex) > NeutralFamily) continue;      // a family that is a colour, not a grey
            foreach (var block in family.Blocks)
            {
                if (byUse.Contains((block.Id, block.Data))) continue;
                await Assert.That(Warmth(block.Hex))
                    .IsLessThanOrEqualTo(WarmestMember)
                    .Because($"{block.Name} ({block.Id}:{block.Data}, {block.Hex}) is warm and "
                             + $"{family.Name} ({family.Hex}) is a grey");
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
    public async Task A_full_cube_a_board_is_built_of_resolves_to_a_tone_family()
    {
        // `SurfaceReport` counts a full cube no family names as "unnamed material" and legends it magenta,
        // which is the vocabulary reporting its own gap. Four of these sit beside a variant of the same id the
        // vocabulary already claims; the fifth is the block an iron cube is built of, which stands on the
        // ground of every board that carries one.
        await Assert.That(TerrainPalette.FamilyOf(24, 2)).IsEqualTo("sand");        // smooth sandstone, beside 24:0
        await Assert.That(TerrainPalette.FamilyOf(98, 1)).IsEqualTo("cobble");      // mossy stone brick, beside 48:0
        await Assert.That(TerrainPalette.FamilyOf(98, 3)).IsEqualTo("grey stone");  // chiselled stone brick, beside 98:0
        await Assert.That(TerrainPalette.FamilyOf(99, 0)).IsEqualTo("sand");        // mushroom pores, a warm tan
        await Assert.That(TerrainPalette.FamilyOf(42, 0)).IsEqualTo("ash");         // iron block
    }

    [Test]
    public async Task Bedrock_is_named_by_no_family_because_it_is_a_fixture_rather_than_a_ground()
    {
        // Bedrock is what a board stands on and what its rim is cut from, not a ground an author paints, so
        // the picker does not offer it. It is a full cube, so a column whose surface is bedrock still reports
        // as unnamed material.
        await Assert.That(TerrainPalette.FamilyOf(Blocks.Bedrock, 0)).IsNull();
        await Assert.That(BlockRoles.IsFullCube(Blocks.Bedrock)).IsTrue();
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
        NoiseMaterial noise => noise.Stops.SelectMany(BlocksOf),
        TurbulenceMaterial turbulence => turbulence.Stops.SelectMany(BlocksOf),
        ElectricMaterial electric => electric.Stops.SelectMany(BlocksOf),
        WallRunMaterial run => run.Runs.SelectMany(stripe => BlocksOf(stripe.Material)),
        _ => [],
    };
}
