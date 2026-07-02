using PgmStudio.Domain;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// Colour-name normalization: real wool slugs pass through, display spellings and chat colours (the team
/// palette) coerce to their nearest wool, and the damage nibble follows. Guards the chat-colour → wool
/// mapping the spawn/wool cube stampers depend on (gold/aqua/dark aqua must not fall through to white).
/// </summary>
public sealed class WoolColorsTests
{
    [Test]
    [Arguments("red", "red")]
    [Arguments("Light Blue", "light_blue")]     // real wool, spaced display form
    [Arguments("light gray", "silver")]         // modern name for damage 8
    [Arguments("Gold", "orange")]               // chat colours with no wool of their own …
    [Arguments("aqua", "cyan")]
    [Arguments("dark aqua", "cyan")]
    [Arguments("dark_aqua", "cyan")]            // underscore form maps identically
    [Arguments("light purple", "purple")]
    [Arguments("dark red", "red")]
    [Arguments("dark green", "green")]
    [Arguments("dark blue", "blue")]
    [Arguments("dark gray", "gray")]
    public async Task Normalize_maps_display_and_chat_colours_to_wool_slugs(string input, string expected)
        => await Assert.That(WoolColors.Normalize(input)).IsEqualTo(expected);

    [Test]
    [Arguments("gold", 1)]      // orange
    [Arguments("aqua", 9)]      // cyan
    [Arguments("dark aqua", 9)] // cyan
    [Arguments("light purple", 10)] // purple
    public async Task WoolDamage_resolves_chat_colours_off_white(string input, int expectedDamage)
        => await Assert.That(WoolColors.WoolDamage(input)).IsEqualTo(expectedDamage);

    [Test]
    public async Task Unknown_colour_still_falls_back_to_white()
        => await Assert.That(WoolColors.WoolDamage("chartreuse")).IsEqualTo(0);
}
