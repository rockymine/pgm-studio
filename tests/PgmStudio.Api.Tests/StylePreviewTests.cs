using System.Text.RegularExpressions;
using PgmStudio.Api.Services;
using PgmStudio.Contracts;
using PgmStudio.Minecraft;

namespace PgmStudio.Api.Tests;

/// <summary>
/// The library's pictures (B44). The load-bearing case is the one the top-down swatch could never answer: a
/// layer stack varies with <em>depth</em>, so sampled one course from above it is a single flat colour and a
/// library card for it says nothing. The section view is the answer, and these assert that the two views really
/// do differ on a stack and agree on a solid, that a theme's cut-open sample carries every bucket's block, and
/// that the raster emits runs rather than a rect per cell.
/// </summary>
public sealed class StylePreviewTests
{
    private static readonly TerrainMaterial GrassOverDirt = new LayeredMaterial(
        [new MaterialLayer(new SolidMaterial(Blocks.Grass), 1), new MaterialLayer(new SolidMaterial(Blocks.Dirt), 2)]);

    /// <summary>The distinct fill colours an SVG uses — what "the picture shows more than one thing" means here.</summary>
    private static HashSet<string> Fills(string svg)
        => Regex.Matches(svg, "fill='(#[0-9a-f]{6})'").Select(m => m.Groups[1].Value).ToHashSet();

    [Test]
    public async Task A_layer_stack_reads_as_one_colour_from_above_and_as_its_layers_in_section()
    {
        // From above, every cell is the stack's top course — one colour, which is why the card cannot use it.
        await Assert.That(Fills(StylePreview.PlanSvg(GrassOverDirt)).Count).IsEqualTo(1);

        // Cut open, the courses are the stack: grass over dirt, both present.
        var section = Fills(StylePreview.SectionSvg(GrassOverDirt));
        await Assert.That(section).Contains(BlockPalette.Hex(Blocks.Grass, 0));
        await Assert.That(section).Contains(BlockPalette.Hex(Blocks.Dirt, 0));
    }

    [Test]
    public async Task The_card_view_is_the_one_that_shows_the_kind_something()
    {
        // A stack gets the section (its layers show); everything else gets the plan view.
        await Assert.That(StylePreview.CardSvg(MaterialKind.Layered, GrassOverDirt))
            .IsNotEqualTo(StylePreview.CardSvg(MaterialKind.Solid, GrassOverDirt));

        // A solid is one colour either way — the choice of view cannot invent detail that is not there.
        var solid = new SolidMaterial(Blocks.Stone);
        await Assert.That(Fills(StylePreview.CardSvg(MaterialKind.Solid, solid)).Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_team_tint_shows_its_colour_beside_its_neutral_fallback()
    {
        var tint = new TeamTintedMaterial(Blocks.StainedClay, new SolidMaterial(Blocks.StainedClay, 8));
        var fills = Fills(StylePreview.PlanSvg(tint));
        await Assert.That(fills).Contains(BlockPalette.Hex(Blocks.StainedClay, 8));    // neutral half
        await Assert.That(fills).Contains(BlockPalette.Hex(Blocks.StainedClay, 14));   // team half
    }

    [Test]
    public async Task The_theme_section_carries_every_bucket_of_the_theme_it_paints()
    {
        // A theme whose four buckets are four unmistakable blocks: whatever the sample plateau's geometry does,
        // each bucket has to appear somewhere in the cut-open picture, bedrock included.
        var theme = new TerrainTheme
        {
            Rim = new TopBand(new SolidMaterial(Blocks.GoldBlock), Depth: 1),
            Surface = new TopBand(new SolidMaterial(Blocks.Grass), Depth: 2),
            Wall = new SolidMaterial(Blocks.Obsidian),
            Fill = new SolidMaterial(Blocks.EndStone),
        };

        var fills = Fills(StylePreview.ThemeSectionSvg(theme));
        await Assert.That(fills).Contains(BlockPalette.Hex(Blocks.GoldBlock, 0));
        await Assert.That(fills).Contains(BlockPalette.Hex(Blocks.Grass, 0));
        await Assert.That(fills).Contains(BlockPalette.Hex(Blocks.Obsidian, 0));
        await Assert.That(fills).Contains(BlockPalette.Hex(Blocks.EndStone, 0));
        await Assert.That(fills).Contains(BlockPalette.Hex(Blocks.Bedrock, 0));
    }

    [Test]
    public async Task Switching_a_theme_bucket_off_changes_the_picture()
    {
        var painting = new TerrainTheme { Wall = new SolidMaterial(Blocks.Obsidian) };
        var off = painting with { WallEnabled = false };

        await Assert.That(Fills(StylePreview.ThemeSectionSvg(painting))).Contains(BlockPalette.Hex(Blocks.Obsidian, 0));
        await Assert.That(Fills(StylePreview.ThemeSectionSvg(off))).DoesNotContain(BlockPalette.Hex(Blocks.Obsidian, 0));
    }

    [Test]
    public async Task A_theme_preview_carries_a_swatch_per_themeable_bucket()
    {
        var views = StylePreview.ThemeViews(TerrainTheme.Default);
        await Assert.That(views.Buckets.Keys.Order()).IsEquivalentTo(ThemeBuckets.All.Order());
        await Assert.That(views.Section).Contains("<svg");
    }

    [Test]
    public async Task One_colour_is_one_rect_however_many_cells_it_covers()
    {
        // The reason the picture can travel with the row at all: a solid style's 24×24 swatch is one rect, not
        // 576 — merged along the row, then down the rows that repeat it.
        var svg = StylePreview.PlanSvg(new SolidMaterial(Blocks.Stone), columns: 24);
        await Assert.That(Regex.Matches(svg, "<rect").Count).IsEqualTo(1);

        // A stack is one rect per layer: the rows within a layer repeat, and each row is one colour.
        var section = StylePreview.SectionSvg(GrassOverDirt, columns: 24, courses: 6);
        await Assert.That(Regex.Matches(section, "<rect").Count).IsEqualTo(2);
    }

    [Test]
    public async Task An_undrawn_cell_emits_no_rect_so_what_is_behind_it_shows_through()
    {
        var svg = SvgRaster.Raster(4, 1, cell: 2, (x, _) => x == 0 ? "#ffffff" : null);
        await Assert.That(Regex.Matches(svg, "<rect").Count).IsEqualTo(1);
    }
}
