using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Catalog;

/// <summary>
/// The shape catalog (G144): the generation vocabulary as cards. The catalog is a <b>bounded</b> set — it is
/// derived from the emitters and the tuning constants, not composed — so the whole thing is fetched once and
/// filtered in the page, which makes every chip instant and needs no cursor.
///
/// <para>Each card carries its <b>reach</b>: <c>in-mix</c> (a sampler draws it, so boards really contain it),
/// <c>reachable</c> (the filler accepts it but nothing asks for one) or <c>emitter-only</c> (only a direct
/// emitter call builds it). Without that the page would assert a vocabulary the boards do not carry — the
/// browse tool draws the same distinction when it renders the Z and scythe chips disabled.</para>
/// </summary>
public partial class CatalogTool
{
    [Inject] private HttpClient Http { get; set; } = default!;

    private IReadOnlyList<CatalogShapeDto> shapes = [];
    private IReadOnlyDictionary<string, int> byTier = new Dictionary<string, int>();
    private IReadOnlyDictionary<string, int> byFamily = new Dictionary<string, int>();
    private IReadOnlyDictionary<string, int> byKind = new Dictionary<string, int>();
    private int total;
    private bool loading = true;
    private string? error;

    private readonly HashSet<string> kindFilter = [];
    private readonly HashSet<string> tierFilter = [];
    private readonly HashSet<string> familyFilter = [];

    /// <summary>The box kinds a shape can fill. Spawn boxes reuse the approach families at the map's lane
    /// width rather than adding forms of their own, so they are not a separate row yet.</summary>
    private static readonly (string Token, string Label, string Hint)[] Kinds =
    [
        ("wool", "Wool approach", "A terminal-capped approach: the lane to a wool, dead-ending at its room."),
        ("hub", "Hub body", "The unit's constraint-source body — terminal-free, its edge widths set every neighbour's menu."),
        ("frontline", "Frontline body", "The join toward the axis — terminal-free, one edge marked the face."),
    ];

    /// <summary>The reach tiers, in narrowing order. The hints are the page's whole honesty contract, so they
    /// name the mechanism rather than grading the shape.</summary>
    private static readonly (string Token, string Label, string Hint)[] Tiers =
    [
        ("in-mix", "In the mix", "A sampler draws this, so generated boards really contain it."),
        ("reachable", "Reachable", "BoxFiller fills it and the menu lists it, but no sampler ever asks for one."),
        ("emitter-only", "Emitter only", "Only a direct emitter call builds it — off the fill menu, or a knob the fill path drops."),
    ];

    /// <summary>Families in pipeline-legibility order (straight → bent → branch → enclosing → bodies), so the
    /// grid reads as a progression rather than alphabetically.</summary>
    private static readonly string[] FamilyOrderTokens =
    [
        "i", "l", "z", "scythe", "u", "h", "clamp", "donut",
        "bar", "single", "twin", "ring", "p", "g", "double-hole",
    ];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var page = await Http.GetFromJsonAsync<CatalogPage>("api/shapes/catalog");
            if (page is null) { error = "The catalog came back empty."; return; }
            shapes = page.Shapes;
            total = page.Total;
            byTier = page.ByTier;
            byFamily = page.ByFamily;
            byKind = page.ByKind;
        }
        catch (HttpRequestException e)
        {
            error = $"Could not load the catalog: {e.Message}";
        }
        finally
        {
            loading = false;
        }
    }

    /// <summary>The cards the current filters admit. Each facet is any-of within itself and all-of across
    /// facets — the usual sieve, and the same shape the browse rail uses for its form chips.</summary>
    private IReadOnlyList<CatalogShapeDto> Shown => shapes
        .Where(s => kindFilter.Count == 0 || kindFilter.Contains(s.Kind))
        .Where(s => tierFilter.Count == 0 || tierFilter.Contains(s.Tier))
        .Where(s => familyFilter.Count == 0 || familyFilter.Contains(s.Family))
        .ToList();

    private bool AnyFilter => kindFilter.Count > 0 || tierFilter.Count > 0 || familyFilter.Count > 0;

    private void Toggle(HashSet<string> into, string token)
    {
        if (!into.Add(token)) into.Remove(token);
    }

    private void ClearFilters()
    {
        kindFilter.Clear();
        tierFilter.Clear();
        familyFilter.Clear();
    }

    private static int Count(IReadOnlyDictionary<string, int> tally, string token) =>
        tally.TryGetValue(token, out var n) ? n : 0;

    private static int FamilyOrder(string family)
    {
        var at = Array.IndexOf(FamilyOrderTokens, family);
        return at < 0 ? FamilyOrderTokens.Length : at;
    }

    private static string TierLabel(string tier) =>
        Tiers.FirstOrDefault(t => t.Token == tier).Label ?? tier;

    private static string TierHint(string tier) =>
        Tiers.FirstOrDefault(t => t.Token == tier).Hint ?? string.Empty;
}
