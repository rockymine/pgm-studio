using PgmStudio.Geom.Render;

namespace PgmStudio.Geom.Tests.Render;

/// <summary>The 5×5 bitmap font a legend's swatch labels and a section's scale numbers are drawn from.</summary>
public sealed class PixelFontTests
{
    [Test]
    [Arguments('A')]
    [Arguments('Z')]
    [Arguments('0')]
    [Arguments('9')]
    [Arguments(':')]
    [Arguments('-')]
    public async Task Every_glyph_used_by_a_legend_or_a_scale_is_five_rows_of_five(char character)
    {
        var glyph = PixelFont.Glyph(character);
        await Assert.That(glyph.Length).IsEqualTo(PixelFont.GlyphSize);
        foreach (var row in glyph) await Assert.That(row.Length).IsEqualTo(PixelFont.GlyphSize);
    }

    [Test]
    public async Task A_letter_and_its_lowercase_form_read_as_the_same_glyph()
    {
        await Assert.That(PixelFont.Glyph('a')).IsEquivalentTo(PixelFont.Glyph('A'));
    }

    [Test]
    public async Task An_unknown_character_falls_back_to_a_blank_cell_rather_than_throwing()
    {
        var glyph = PixelFont.Glyph('#');
        await Assert.That(glyph).IsEquivalentTo(PixelFont.Glyph(' '));
    }

    [Test]
    public async Task No_two_letters_share_the_exact_same_shape()
    {
        // Not a claim of typographic beauty — just that the font does not silently alias two different
        // characters onto one glyph, which would make a legend label unreadable regardless of colour.
        var letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var shapes = letters.Select(c => string.Join('|', PixelFont.Glyph(c))).ToList();
        await Assert.That(shapes.Distinct().Count()).IsEqualTo(letters.Length);
    }
}
