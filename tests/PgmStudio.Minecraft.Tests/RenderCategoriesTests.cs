namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The false-colour scheme every stage image's legibility depends on (<c>docs/tools/capabilities.md</c>'s
/// renderer section, <c>B98</c>): the categories a reader must tell apart at a glance have to separate by more
/// than shade, and a block has to land in the category a reader would actually call it.
/// </summary>
public sealed class RenderCategoriesTests
{
    // A perceptual "redmean" distance rather than a plain Euclidean one, so two colours that differ mostly in
    // green (where the eye is most sensitive) are not scored as close just because their raw RGB numbers are.
    private static double Distance(int a, int b)
    {
        double r1 = (a >> 16) & 0xFF, g1 = (a >> 8) & 0xFF, b1 = a & 0xFF;
        double r2 = (b >> 16) & 0xFF, g2 = (b >> 8) & 0xFF, b2 = b & 0xFF;
        var rMean = (r1 + r2) / 2;
        var dr = r1 - r2; var dg = g1 - g2; var db = b1 - b2;
        return Math.Sqrt((2 + rMean / 256) * dr * dr + 4 * dg * dg + (2 + (255 - rMean) / 256) * db * db);
    }

    // Chosen so two genuinely different hues clear it comfortably (the five category colours here score in
    // the 250-500 range against each other) while two shades of the same hue - the exact failure B98 reports
    // (stone/andesite/cobblestone all reading as one grey) - fall well under it.
    private const double MinDistinctDistance = 120;

    [Test]
    public async Task Every_pair_of_visible_categories_separates_by_more_than_shade()
    {
        var colours = RenderCategories.Visible.Select(RenderCategories.ColourOf).ToList();
        for (var i = 0; i < colours.Count; i++)
            for (var j = i + 1; j < colours.Count; j++)
                await Assert.That(Distance(colours[i], colours[j])).IsGreaterThanOrEqualTo(MinDistinctDistance);
    }

    [Test]
    public async Task Two_shades_of_the_same_grey_stone_family_fail_the_same_bar_the_categories_clear()
    {
        // The exact bug the category scheme replaces: stone (1:0) and andesite (1:5) both read as
        // near-identical greys under the real block palette, which is why a section through Ashen Quarry's
        // town rendered as one undifferentiated slab. Proving the old material colours fail this bar is what
        // shows the category scheme is doing real work rather than restating an already-legible picture.
        var stone = BlockPalette.PackedRgb(1, 0);
        var andesite = BlockPalette.PackedRgb(1, 5);
        await Assert.That(Distance(stone, andesite)).IsLessThan(MinDistinctDistance);
    }

    private static double Luminance(int rgb) =>
        0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF);

    [Test]
    public async Task Void_is_darker_than_every_visible_category()
    {
        // Void carries its own meaning ("no data") and must never be mistaken for a dim reading of a real
        // category, so it sits well outside the range any visible category's own shading reaches.
        foreach (var category in RenderCategories.Visible)
            await Assert.That(Luminance(RenderCategories.VoidRgb)).IsLessThan(Luminance(RenderCategories.ColourOf(category)));
    }

    [Test]
    [Arguments(0, RenderCategory.Void)]
    [Arguments(Blocks.Water, RenderCategory.Water)]
    [Arguments(Blocks.StationaryWater, RenderCategory.Water)]
    [Arguments(Blocks.Log, RenderCategory.Foliage)]
    [Arguments(Blocks.Leaves, RenderCategory.Foliage)]
    [Arguments(37, RenderCategory.Foliage)]     // dandelion — grows, not built
    [Arguments(1, RenderCategory.Ground)]       // stone — natural, not built
    [Arguments(3, RenderCategory.Ground)]       // dirt
    [Arguments(5, RenderCategory.Structure)]    // planks — a world never generates these on its own
    [Arguments(98, RenderCategory.Structure)]   // stone bricks — the exact town-wall material the bug report names
    public async Task Of_sorts_a_block_into_the_category_a_reader_would_call_it(int blockId, RenderCategory expected)
    {
        await Assert.That(RenderCategories.Of(blockId)).IsEqualTo(expected);
    }

    // ── the provenance-aware overload (B133) ────────────────────────────────────────────────────────
    // "A block does not know what placed it" — these prove the overload actually answers a different
    // question than the material-only one above, for the exact pair a material test cannot separate.

    [Test]
    public async Task A_structure_claim_reads_as_structure_whatever_the_material_is()
    {
        // Stone (1) reads Ground by material alone (see the table above) — the recorded claim overrides it.
        await Assert.That(RenderCategories.Of(1, ProvenanceLayer.Structure)).IsEqualTo(RenderCategory.Structure);
    }

    [Test]
    public async Task A_ground_claim_reads_as_ground_even_over_a_built_looking_material()
    {
        // Stone brick (98) reads Structure by material alone — this is B133's own finding, reproduced as the
        // failing case the provenance overload exists to fix: a plaza painted in it is still ground once the
        // build says so.
        await Assert.That(RenderCategories.Of(98, null)).IsEqualTo(RenderCategory.Structure);
        await Assert.That(RenderCategories.Of(98, ProvenanceLayer.Ground)).IsEqualTo(RenderCategory.Ground);
    }

    [Test]
    public async Task No_provenance_falls_back_to_the_material_estimate()
    {
        // A scanned world, or a column the build never claimed — the overload has to answer exactly what the
        // material-only method already does, so a caller with no recorded provenance loses nothing.
        foreach (var blockId in new[] { 0, Blocks.Water, Blocks.Log, 37, 1, 5, 98 })
            await Assert.That(RenderCategories.Of(blockId, null)).IsEqualTo(RenderCategories.Of(blockId));
    }

    [Test]
    [Arguments(0, RenderCategory.Void)]
    [Arguments(Blocks.Water, RenderCategory.Water)]
    [Arguments(Blocks.Log, RenderCategory.Foliage)]
    public async Task Void_liquid_and_foliage_are_never_overridden_by_a_structure_claim(int blockId, RenderCategory expected)
    {
        // Provenance answers the Ground/Structure question only; a column provenance never asked about
        // (water, a tree) keeps reading by material even when a caller hands in a Structure claim for it —
        // which should not happen in practice, but the priority order is worth pinning down.
        await Assert.That(RenderCategories.Of(blockId, ProvenanceLayer.Structure)).IsEqualTo(expected);
    }

    [Test]
    public async Task Ground_highlight_differs_from_grounds_own_backdrop_shade()
    {
        // The backdrop shade is deliberately muted (it is what the other categories read against in the
        // combined view); an isolated ground layer has nothing to sit behind and needs its own colour rather
        // than inheriting that muted shade.
        await Assert.That(RenderCategories.HighlightOf(RenderCategory.Ground)).IsNotEqualTo(RenderCategories.GroundRgb);
        await Assert.That(Distance(RenderCategories.HighlightOf(RenderCategory.Ground), RenderCategories.GroundRgb))
            .IsGreaterThan(MinDistinctDistance);
    }

    [Test]
    public async Task Every_other_categorys_highlight_is_its_own_combined_colour()
    {
        foreach (var category in RenderCategories.Visible.Where(category => category != RenderCategory.Ground))
            await Assert.That(RenderCategories.HighlightOf(category)).IsEqualTo(RenderCategories.ColourOf(category));
    }
}
