using System.Text.RegularExpressions;

namespace PgmStudio.Api.Tests;

/// <summary>The picture a list row's card names: the row carries an <c>&lt;img&gt;</c> of
/// <c>GET /api/cards/{name}</c>, so a test reading what a card shows fetches that route.</summary>
internal static partial class CardPictures
{
    /// <summary>The route the card markup names.</summary>
    public static string RouteOf(string card) => Source().Match(card) is { Success: true } found
        ? found.Groups[1].Value
        : throw new InvalidOperationException($"no card route in {card}");

    /// <summary>The card's picture as text, which is SVG on a studio without sprites.</summary>
    public static Task<string> SvgAsync(HttpClient client, string card) => client.GetStringAsync(RouteOf(card));

    [GeneratedRegex("src=\"(/api/cards/[0-9a-f]{64})\"")]
    private static partial Regex Source();
}
