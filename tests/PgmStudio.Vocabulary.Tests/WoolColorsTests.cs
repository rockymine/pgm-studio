using PgmStudio.Vocabulary;

namespace PgmStudio.Vocabulary.Tests;

/// <summary>
/// The dye set four parties spell: the plan validator refuses a word outside it, the compiler picks from it,
/// two renderers colour a marker by it and the plan editor offers it. What is worth pinning is not the list
/// itself but the properties a caller relies on — that every name has a swatch, that no two dyes share one,
/// and that the spellings PGM accepts all land on the same word.
/// </summary>
public sealed class WoolColorsTests
{
    [Test]
    public async Task Every_dye_has_its_own_swatch()
    {
        // A renderer reads Swatch by name and a picker reads All: a name in one and not the other renders
        // amber beside a picker offering it, which is the drift this set exists to stop.
        await Assert.That(WoolColors.Swatch.Count).IsEqualTo(WoolColors.All.Length);
        foreach (var dye in WoolColors.All) await Assert.That(WoolColors.Swatch.ContainsKey(dye)).IsTrue();
    }

    [Test]
    public async Task No_two_dyes_draw_the_same_colour()
    {
        // Two wools the author can tell apart on the board is the whole point of a per-dye swatch — a shared
        // hex makes the picture lie about which goal is which.
        var swatches = WoolColors.All.Select(WoolColors.SwatchOf).ToHashSet();
        await Assert.That(swatches.Count).IsEqualTo(WoolColors.All.Length);
    }

    [Test]
    public async Task A_name_that_is_not_a_dye_draws_as_no_dye_does()
    {
        // The unknown swatch has to sit outside the sixteen, or a mis-typed colour passes for a real one.
        await Assert.That(WoolColors.SwatchOf("chartreuse")).IsEqualTo(WoolColors.UnknownSwatch);
        await Assert.That(WoolColors.All.Select(WoolColors.SwatchOf)).DoesNotContain(WoolColors.UnknownSwatch);
    }

    [Test]
    [Arguments("light blue", "light_blue")]
    [Arguments("Light Blue", "light_blue")]
    [Arguments("LIGHT_BLUE", "light_blue")]
    [Arguments("  red  ", "red")]
    [Arguments("light_gray", "silver")]
    [Arguments("Light Gray", "silver")]
    public async Task Every_spelling_PGM_resolves_lands_on_one_word(string written, string canonical)
    {
        // PGM's DyeColors simplifies case and underscores and maps LIGHT_GRAY onto SILVER, so a plan written
        // in any of those spellings names a dye the studio must recognise rather than refuse.
        await Assert.That(WoolColors.Normalize(written)).IsEqualTo(canonical);
        await Assert.That(WoolColors.IsColor(written)).IsTrue();
    }

    [Test]
    public async Task A_word_outside_the_set_is_not_a_colour()
    {
        foreach (var word in new[] { "", "  ", "rainbow", "dark_red", "light purple" })
            await Assert.That(WoolColors.IsColor(word)).IsFalse();
    }

    [Test]
    public async Task A_swatch_reads_the_same_as_text_and_as_a_number()
    {
        // The SVG board takes the hex and the top-down raster takes the int; a marker that changed colour
        // between the two pictures would be reporting a different map depending on how it was drawn.
        foreach (var dye in WoolColors.All)
            await Assert.That(WoolColors.RgbOf(dye)).IsEqualTo(Convert.ToInt32(WoolColors.SwatchOf(dye)[1..], 16));
    }

    [Test]
    public async Task A_label_is_the_dye_a_person_reads()
        => await Assert.That(WoolColors.Label("light_blue")).IsEqualTo("Light Blue");
}
