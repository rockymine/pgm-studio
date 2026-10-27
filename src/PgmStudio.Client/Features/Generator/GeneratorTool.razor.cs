using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Contracts;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Generator;

/// <summary>
/// The generator browse feed: page through the composed-board library the server holds, sieve it by
/// size band/symmetry/wool count, and make a map of the one worth taking. Cards carry only their descriptor + SVG;
/// opening a card makes a map of the stored board the descriptor names.
/// </summary>
public partial class GeneratorTool : IAsyncDisposable
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private ElementReference sentinelRef;
    private DotNetObjectReference<GeneratorTool>? selfRef;
    private IJSObjectReference? observer;

    // ── filters ──────────────────────────────────────────────────────────────────
    private string band = SizeBands.Nano;
    private string symmetry = "rot_180";
    private readonly SortedSet<int> woolCounts = [];   // empty = any

    private const int PageSize = 9;

    /// <summary>The symmetries the feed is asked for, each named by <see cref="SymmetryInfo"/>.</summary>
    private static readonly (string Id, bool Supported)[] Symmetries =
    [
        ("rot_180", true),
        ("mirror_z", true),
        ("rot_90", false),
        ("mirror_x", false),
    ];

    // Structural filter vocabularies. Wool families the composer emits are enabled; the classifier-only reads
    // (Z, scythe) render disabled — not in the production mix (same honesty the endpoint gives rot_90).
    private static readonly (string Token, string Label, bool InMix)[] WoolChips =
    [
        ("i", "I", true), ("l", "L", true), ("u", "U", true), ("h", "H", true),
        ("donut", "Donut", true), ("clamp", "Clamp", true), ("z", "Z", false), ("scythe", "Scythe", false),
    ];
    private static readonly (string Token, string Label)[] HubChips =
        [("i", "I"), ("l", "L"), ("u", "U"), ("donut", "Donut"), ("g", "G"), ("p", "P"), ("double-hole", "Double-hole")];
    private static readonly (string Token, string Label)[] FrontChips =
        [("none", "Hub edge"), ("i", "I"), ("l", "L"), ("u", "U")];

    // Selected structural filters: wools are must-include (each present), hub/front are any-of.
    private readonly HashSet<string> woolFilter = [];
    private readonly HashSet<string> hubFilter = [];
    private readonly HashSet<string> frontFilter = [];

    // ── feed ─────────────────────────────────────────────────────────────────────
    private readonly List<ComposeCard> cards = [];
    private int cursor;              // the library position to ask from next
    private int matching;            // how many library boards the current filters match
    private bool loading, atEnd;
    private string? feedError;

    // ── detail dialog ─────────────────────────────────────────────────────────────
    private ComposeCard? detail;

    // The roles the detail picture can tint, in the order a reader meets a layout: the middle out. Each is a
    // `role-*` class on the pieces the renderer drew, so a toggle changes classes on the picture already inline.
    private static readonly (string Role, string Label)[] HighlightRoles =
    [
        (BoardRoles.Hub, "Hubs"), (BoardRoles.Frontline, "Front lines"), (BoardRoles.Approach, "Approaches"),
        (BoardRoles.Spawn, "Spawns"), (BoardRoles.Wool, "Wool rooms"), (BoardRoles.Zone, "Build regions"),
    ];
    private readonly HashSet<string> highlights = [];
    private string HighlightClasses => string.Join(' ', highlights.Select(role => $"board-hl-{role}"));
    private void ToggleHighlight(string role) { if (!highlights.Remove(role)) highlights.Add(role); }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!firstRender) return;
        selfRef = DotNetObjectReference.Create(this);
        await Reload();
        try { observer = await JS.InvokeAsync<IJSObjectReference>("studio.onScrollEnd", sentinelRef, selfRef); }
        catch { /* infinite scroll unavailable — the Load more button still works */ }
    }

    // ── loading ────────────────────────────────────────────────────────────────────
    private string QueryString(int from)
    {
        var q = $"players={BandPlayers(band)}&symmetry={symmetry}&from={from}&count={PageSize}";
        if (woolCounts.Count > 0) q += $"&woolCount={string.Join(",", woolCounts)}";
        if (woolFilter.Count > 0) q += $"&wools={string.Join(",", woolFilter)}";
        if (hubFilter.Count > 0) q += $"&hub={string.Join(",", hubFilter)}";
        if (frontFilter.Count > 0) q += $"&front={string.Join(",", frontFilter)}";
        return q;
    }

    // Apply the filters: clear the feed and start from the library's first board.
    private async Task Reload()
    {
        cards.Clear();
        cursor = 0;
        matching = 0;
        atEnd = false;
        feedError = null;
        await LoadPage();
    }

    private async Task LoadPage()
    {
        if (loading || atEnd) return;
        loading = true;
        StateHasChanged();
        try
        {
            var page = await Http.GetFromJsonAsync<ComposePage>($"api/compose?{QueryString(cursor)}");
            if (page is not null)
            {
                cards.AddRange(page.Cards);
                cursor = page.Next;
                matching = page.Matching;
                atEnd = page.End;
                SetCensus(page.Observed);
                key = page.Key;
            }
        }
        catch { feedError = "Couldn't load the layouts. Reload the page to try again."; atEnd = true; }
        finally { loading = false; StateHasChanged(); }
    }

    // ── the structural census (what the library holds for these settings) ─────────────────────────────
    // Every page carries the census over the whole library for the band and symmetry, before the filters.
    private readonly Dictionary<string, int> seenWools = [], seenHubs = [], seenFronts = [];
    private IReadOnlyList<BoardKeyEntry>? key;
    private int censusBoards;

    /// <summary>How many boards must be held before a token's absence is worth reporting as absence rather than
    /// as a small sample — which matters only while a library is still being filled. Below it the chips carry
    /// their counts but nothing is called unavailable.</summary>
    private const int CensusConfidence = 150;

    private void SetCensus(ObservedForms? o)
    {
        seenWools.Clear(); seenHubs.Clear(); seenFronts.Clear();
        censusBoards = o?.Boards ?? 0;
        if (o is null) return;
        foreach (var (k, v) in o.Wools) seenWools[k] = v;
        foreach (var (k, v) in o.Hubs) seenHubs[k] = v;
        foreach (var (k, v) in o.Frontlines) seenFronts[k] = v;
    }

    private bool CensusIsTelling => censusBoards >= CensusConfidence;

    // A token this request has never produced, on a sample big enough to mean it. Distinct from a family the
    // composer cannot build at all (the wool chips' own InMix flag), which is true of every request.
    private bool Unseen(Dictionary<string, int> seen, string token) =>
        CensusIsTelling && seen.GetValueOrDefault(token) == 0;

    /// <summary>The picked shapes this request never produced, joined for a sentence, or empty. A filter naming
    /// one is a different answer from an unlucky run, and the census can tell them apart.</summary>
    private string NeverSeen() => string.Join(" or ", Selected()
        .Where(f => Unseen(f.Seen, f.Token))
        .Select(f => f.Label));

    // every structural filter currently picked, with the census it reads against
    private IEnumerable<(Dictionary<string, int> Seen, string Token, string Label)> Selected()
    {
        foreach (var t in woolFilter) yield return (seenWools, t, Label(WoolChips.Select(w => (w.Token, w.Label)), t));
        foreach (var t in hubFilter) yield return (seenHubs, t, Label(HubChips, t));
        foreach (var t in frontFilter) yield return (seenFronts, t, Label(FrontChips, t));
    }

    private static string Label(IEnumerable<(string Token, string Label)> chips, string token) =>
        chips.FirstOrDefault(c => c.Token == token).Label ?? token;

    private string ChipTitle(Dictionary<string, int> seen, string token, string label)
    {
        var n = seen.GetValueOrDefault(token);
        if (n > 0) return $"In {n} of {censusBoards} layouts";
        return CensusIsTelling
            ? "Never generated with these settings"
            : $"In none of {censusBoards} layout{(censusBoards == 1 ? "" : "s")} yet";
    }

    // ── structural filters (chips + card badges; toggling re-sieves the feed immediately) ────────────
    private Task ToggleWool(string t) { Toggle(woolFilter, t); return Reload(); }
    private Task ToggleHub(string t) { Toggle(hubFilter, t); return Reload(); }
    private Task ToggleFront(string t) { Toggle(frontFilter, t); return Reload(); }

    private static void Toggle(HashSet<string> set, string t) { if (!set.Remove(t)) set.Add(t); }

    /// <summary>Invoked from the infinite-scroll observer when the sentinel nears view.</summary>
    [JSInvokable]
    public Task LoadMore() => LoadPage();

    // ── detail dialog ──────────────────────────────────────────────────────────────
    private void OpenDetail(ComposeCard c) { detail = c; openError = null; }
    private void CloseDetail() { detail = null; openError = null; }

    private static IReadOnlyList<DescriptionPart> Describe(ComposeCard c) =>
        LayoutDescription.Of(c.Descriptor.Players, c.Descriptor.Teams, c.Descriptor.Symmetry, c.WoolCount,
            c.Structure.Wools, c.Structure.Hub, c.Structure.Frontline);

    private static string PlayersLabel(ComposeCard c) => BandLabel(SizeBands.Of(c.Descriptor.Players));

    private static string SymmetryLabel(string symmetry) => SymmetryInfo.Label(symmetry);

    // Make a map of the board (POST /api/compose/map) and open it in the Sketch tool. The board stays in the library,
    // so making a second map of it makes a second map.
    private bool opening;
    private string? openError;

    private async Task OpenInSketch(ComposeCard c)
    {
        opening = true;
        openError = null;
        try
        {
            var made = await Http.PostAsJsonAsync("api/compose/map", c.Descriptor);
            if (made.IsSuccessStatusCode
                && (await made.Content.ReadFromJsonAsync<OriginatedDto>())?.Slug is { Length: > 0 } slug)
            {
                Nav.NavigateTo($"maps/{slug}/sketch");
                return;
            }
            openError = "Couldn't make the map. Try again.";
        }
        catch { openError = "Couldn't make the map. Try again."; }
        finally { opening = false; }
    }

    // ── filter inputs ──────────────────────────────────────────────────────────────
    // A filter applies as soon as it is set.
    private Task PickBand(string size) { band = size; return Reload(); }
    private Task ToggleWoolCount(int count)
    {
        if (!woolCounts.Remove(count)) woolCounts.Add(count);
        return Reload();
    }
    private Task PickSymmetry(string s) { symmetry = s; return Reload(); }

    private bool ShapeFiltered => woolFilter.Count + hubFilter.Count + frontFilter.Count > 0;

    private Task ClearShapeFilters()
    {
        woolFilter.Clear(); hubFilter.Clear(); frontFilter.Clear();
        return Reload();
    }

    // ── size bands ───────────────────────────────────────────────────────────────
    // The feed serves one band at a time; a band is asked for by a player count inside it, and the cards are
    // labelled for that count, so the band's middle is the count that stands for it.

    private static int BandPlayers(string size)
    {
        var (low, high) = SizeBands.Players(size);
        return (low + high) / 2;
    }

    private static string BandRange(string size)
    {
        var (low, high) = SizeBands.Players(size);
        return size == SizeBands.Centi ? $"{low} and up" : $"{low} to {high}";
    }

    private static string BandLabel(string size) =>
        $"{char.ToUpperInvariant(size[0])}{size[1..]} · {BandRange(size)}";

    /// <summary>A card's name: the layout it is, which its seed tells apart from the others of its settings.</summary>
    private static string LayoutId(ComposeCard card) => $"Layout {card.Descriptor.Seed}";

    public async ValueTask DisposeAsync()
    {
        if (observer is not null)
        {
            try { await observer.InvokeVoidAsync("disconnect"); } catch { }
            await observer.DisposeAsync();
        }
        selfRef?.Dispose();
    }
}
