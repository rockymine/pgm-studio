using System.Net.Http.Json;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Components;

/// <summary>One thing the command palette offers beside the commands: a map, a glossary term or a rule, and the
/// page it opens.</summary>
/// <param name="Label">What the row reads: the map's name, the term, the rule's id.</param>
/// <param name="Group">What kind of thing it is: <c>Map</c>, <c>Glossary</c> or <c>Rule</c>.</param>
/// <param name="Detail">The muted line beside it: the slug, the definition, what the rule refuses.</param>
/// <param name="Href">The page it opens, relative to the studio's base.</param>
public sealed record StudioSearchHit(string Label, string Group, string Detail, string Href);

/// <summary>
/// What the command palette finds besides commands: the maps the caller may see (<c>GET /api/maps</c>, asked
/// again once the answer is a minute old), every glossary term and every rule (<see cref="RuleBook"/>). The
/// palette is JavaScript and asks <see cref="FindAsync"/> through <see cref="Reference"/>, so a map opens in the
/// tool its row on the maps page opens and a term at the anchor the glossary page gives it.
/// </summary>
public sealed class StudioSearch(HttpClient http, RuleBook rules)
{
    /// <summary>How many of each kind the palette lists.</summary>
    private const int PerGroup = 5;

    private static readonly TimeSpan MapsFresh = TimeSpan.FromMinutes(1);

    private List<MapSummary>? maps;
    private DateTime mapsAt;

    /// <summary>The handle the palette calls back through, made once and kept for the life of the page.</summary>
    public DotNetObjectReference<StudioSearch> Reference => reference ??= DotNetObjectReference.Create(this);
    private DotNetObjectReference<StudioSearch>? reference;

    /// <summary>The maps, terms and rules matching <paramref name="needle"/>, maps first, at most
    /// <see cref="PerGroup"/> of each; a needle under two letters finds nothing.</summary>
    [JSInvokable("Find")]
    public async Task<IReadOnlyList<StudioSearchHit>> FindAsync(string needle)
    {
        needle = needle.Trim();
        if (needle.Length < 2) return [];
        var hits = new List<StudioSearchHit>();

        hits.AddRange((await MapsAsync())
            .Where(map => Matches(needle, map.Name, map.Slug))
            .Select(map => (Map: map, Opens: MapLayers.Opens(map)))
            .Where(entry => entry.Opens is not null)
            .OrderBy(entry => Rank(needle, entry.Map.Name))
            .Take(PerGroup)
            .Select(entry => new StudioSearchHit(entry.Map.Name, "Map", entry.Map.Slug, $"maps/{entry.Map.Slug}/{entry.Opens}")));

        hits.AddRange(Glossary.Terms
            .Where(term => Matches(needle, [term.Term, .. term.AlsoCalled]))
            .OrderBy(term => Rank(needle, term.Term))
            .Take(PerGroup)
            .Select(term => new StudioSearchHit(term.Term, "Glossary", term.Definition, $"glossary#{Glossary.Slug(term.Term)}")));

        hits.AddRange((await rules.RulesAsync()).Values
            .Where(rule => rule.Rule.StartsWith(needle, StringComparison.OrdinalIgnoreCase) || Matches(needle, rule.Means))
            .OrderBy(rule => Rank(needle, rule.Rule))
            .Take(PerGroup)
            .Select(rule => new StudioSearchHit(rule.Rule, "Rule", rule.Means, $"rules?rule={Uri.EscapeDataString(rule.Rule)}")));
        return hits;
    }

    private async Task<IReadOnlyList<MapSummary>> MapsAsync()
    {
        if (maps is not null && DateTime.UtcNow - mapsAt < MapsFresh) return maps;
        try
        {
            maps = await http.GetFromJsonAsync<List<MapSummary>>("api/maps") ?? [];
            mapsAt = DateTime.UtcNow;
        }
        catch (HttpRequestException) { maps ??= []; }
        return maps;
    }

    private static bool Matches(string needle, params IEnumerable<string> texts) =>
        texts.Any(text => text.Contains(needle, StringComparison.OrdinalIgnoreCase));

    /// <summary>An exact name first, then one that starts with the needle, then the rest.</summary>
    private static int Rank(string needle, string name) =>
        name.Equals(needle, StringComparison.OrdinalIgnoreCase) ? 0
        : name.StartsWith(needle, StringComparison.OrdinalIgnoreCase) ? 1 : 2;
}
