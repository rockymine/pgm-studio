using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Generator;

/// <summary>
/// The generator browse feed: page through the composed-board library the server holds, sieve it by
/// size/symmetry/score/wool count, and keep the ones worth keeping. Cards carry only their descriptor + SVG;
/// pinning or opening a card keeps the stored board the descriptor names. The hold tray is the persisted generated corpus; it
/// survives reload because pinned means stored.
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
    private int players = 12;
    private string symmetry = "rot_180";
    private double maxScore = ScoreCap;   // ScoreCap = "any" (unbounded); sent only when below
    private int woolMin, woolMax;         // 0 = unset

    private const double ScoreCap = 8;
    private const int PageSize = 9;

    private static readonly (string Id, string Label, bool Supported)[] Symmetries =
    [
        ("rot_180", "Rotate 180°", true),
        ("mirror_z", "Mirror Z", true),
        ("rot_90", "Rotate 90°", false),
        ("mirror_x", "Mirror X", false),
    ];

    // Structural filter vocabularies. Wool families the composer emits are enabled; the classifier-only reads
    // (Z, scythe) render disabled — not in the production mix (same honesty the endpoint gives rot_90).
    private static readonly (string Token, string Label, bool InMix)[] WoolChips =
    [
        ("i", "I", true), ("l", "L", true), ("u", "U", true), ("h", "H", true),
        ("donut", "Donut", true), ("clamp", "Clamp", true), ("z", "Z", false), ("scythe", "Scythe", false),
    ];
    private static readonly (string Token, string Label)[] HubChips =
        [("bar", "Bar"), ("single", "Single"), ("twin", "Twin"), ("ring", "Ring"), ("g", "G"), ("p", "P"), ("double-hole", "Double-hole")];
    private static readonly (string Token, string Label)[] FrontChips =
        [("none", "None"), ("bar", "Bar"), ("single", "Single"), ("twin", "Twin")];

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

    // ── hold tray (persisted generated plans, keyed by descriptor) ────────────────
    private List<PlanSummary> pinned = [];
    private readonly HashSet<string> pinnedKeys = [];
    private readonly Dictionary<long, string> traySvg = [];

    // ── detail dialog ─────────────────────────────────────────────────────────────
    private ComposeCard? detail;

    private static string Key(ComposeRequestDto d) => $"{d.Players}-{d.Teams}-{d.Symmetry}-{d.Cell}-{d.Seed}";
    private bool IsPinned(ComposeCard c) => pinnedKeys.Contains(Key(c.Descriptor));

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!firstRender) return;
        selfRef = DotNetObjectReference.Create(this);
        await RefreshPinned();
        await Reload();
        try { observer = await JS.InvokeAsync<IJSObjectReference>("studio.onScrollEnd", sentinelRef, selfRef); }
        catch { /* infinite scroll unavailable — the Load more button still works */ }
    }

    // ── loading ────────────────────────────────────────────────────────────────────
    private string QueryString(int from)
    {
        var q = $"players={players}&symmetry={symmetry}&from={from}&count={PageSize}";
        if (maxScore < ScoreCap) q += $"&maxScore={maxScore.ToString(CultureInfo.InvariantCulture)}";
        if (woolMin > 0) q += $"&woolMin={woolMin}";
        if (woolMax > 0) q += $"&woolMax={woolMax}";
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
        catch { feedError = "Couldn't load layouts. Reload the page to try again."; atEnd = true; }
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

    /// <summary>Why the grid is empty. A filter naming something this request never produced is a different
    /// answer from an unlucky run, and the census can tell them apart — so it says which one it is.</summary>
    private string EmptyFeedMessage()
    {
        var never = Selected()
            .Where(f => Unseen(f.Seen, f.Token))
            .Select(f => f.Label)
            .ToList();
        if (censusBoards == 0)
            return "Layouts for these settings are still being generated. Check back later.";
        if (never.Count == 0)
            return $"None of the {censusBoards} layouts for these settings match these filters.";
        return $"{string.Join(" and ", never)} doesn't appear in any of the {censusBoards} layouts for these "
             + "settings. These players and symmetry don't produce it.";
    }

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
        if (n > 0) return $"{label}: {n} of {censusBoards} layouts";
        return CensusIsTelling
            ? $"{label}: never generated with these settings (0 of {censusBoards} layouts)"
            : $"{label}: none yet in {censusBoards} layout{(censusBoards == 1 ? "" : "s")}";
    }

    // ── structural filters (chips + card badges; toggling re-sieves the feed immediately) ────────────
    private Task ToggleWool(string t) { Toggle(woolFilter, t); return Reload(); }
    private Task ToggleHub(string t) { Toggle(hubFilter, t); return Reload(); }
    private Task ToggleFront(string t) { Toggle(frontFilter, t); return Reload(); }

    private static void Toggle(HashSet<string> set, string t) { if (!set.Remove(t)) set.Add(t); }

    /// <summary>Invoked from the infinite-scroll observer when the sentinel nears view.</summary>
    [JSInvokable]
    public Task LoadMore() => LoadPage();

    // ── hold tray ──────────────────────────────────────────────────────────────────
    private async Task RefreshPinned()
    {
        try
        {
            pinned = await Http.GetFromJsonAsync<List<PlanSummary>>("api/plans?origin=generated") ?? [];
            pinnedKeys.Clear();
            foreach (var p in pinned)
                if (p.Descriptor is { } d) pinnedKeys.Add(Key(d));
            foreach (var p in pinned.Where(p => !traySvg.ContainsKey(p.Id)))
            {
                try
                {
                    var r = await Http.GetFromJsonAsync<SvgResult>($"api/plans/{p.Id}/svg");
                    if (r?.Svg is not null) traySvg[p.Id] = r.Svg;
                }
                catch { /* thumbnail is optional */ }
            }
        }
        catch { /* tray stays as-is */ }
    }

    private async Task TogglePin(ComposeCard c)
    {
        if (IsPinned(c))
        {
            var row = pinned.FirstOrDefault(p => p.Descriptor is { } d && Key(d) == Key(c.Descriptor));
            if (row is not null) { await Unpin(row.Id); return; }
        }
        else
        {
            try { await Http.PostAsJsonAsync("api/compose/pin", c.Descriptor); } catch { }
            await RefreshPinned();
        }
        StateHasChanged();
    }

    private async Task Unpin(long id)
    {
        try { await Http.DeleteAsync($"api/plans/{id}"); } catch { }
        traySvg.Remove(id);
        await RefreshPinned();
        StateHasChanged();
    }

    // A held board an older composer made. Its stored plan is intact and opens as-is; what has lapsed is the
    // descriptor's claim to reproduce it, so re-composing the same seed today gives a different board.
    private static string StaleTitle(PlanSummary p) =>
        $"Made by an older generator version ({p.ComposerVersion ?? "unknown"}). It opens as saved, but its seed "
        + "no longer produces this layout.";

    // ── detail dialog ──────────────────────────────────────────────────────────────
    private void OpenDetail(ComposeCard c) => detail = c;
    private void CloseDetail() => detail = null;

    private static string DescriptorJson(ComposeCard c) =>
        JsonSerializer.Serialize(c.Descriptor, new JsonSerializerOptions { WriteIndented = true });

    private Task CopyDescriptor(ComposeCard c) => JS.InvokeAsync<bool>("studio.copyText", DescriptorJson(c)).AsTask();

    // Author a generated candidate into a map: ensure it is pinned as a candidate plan row (so it has an id),
    // then commit it to a stage=plan map (POST /api/plan/{id}/author) and open the plan editor on that map.
    // This begins the map lifecycle (plan → sketch → configure → edit); the candidate stays in the pool.
    private async Task AuthorPlan(ComposeCard c)
    {
        var id = pinned.FirstOrDefault(p => p.Descriptor is { } d && Key(d) == Key(c.Descriptor))?.Id;
        if (id is null)
        {
            try
            {
                var resp = await Http.PostAsJsonAsync("api/compose/pin", c.Descriptor);
                id = (await resp.Content.ReadFromJsonAsync<PlanDetail>())?.Id;
            }
            catch { /* fall through — no navigation if pin failed */ }
        }
        if (id is null) return;
        try
        {
            var authored = await Http.PostAsync($"api/plan/{id}/author", null);
            if (authored.IsSuccessStatusCode
                && (await authored.Content.ReadFromJsonAsync<OriginatedDto>())?.Slug is { Length: > 0 } slug)
            {
                Nav.NavigateTo($"maps/{slug}/plan");
            }
        }
        catch { /* pin succeeded but authoring failed — stay put so the user can retry */ }
    }

    // ── filter inputs ──────────────────────────────────────────────────────────────
    // A slider shows its value while it moves and applies when it is let go; everything else applies at once.
    private void OnPlayers(double value) => players = (int)value;
    private Task ApplyPlayers(double value) { OnPlayers(value); return Reload(); }
    private void OnMaxScore(double value) => maxScore = value;
    private Task ApplyMaxScore(double value) { OnMaxScore(value); return Reload(); }
    private Task OnWoolMin(double value) { woolMin = Math.Max(0, (int)value); return Reload(); }
    private Task OnWoolMax(double value) { woolMax = Math.Max(0, (int)value); return Reload(); }
    private Task PickSymmetry(string s) { symmetry = s; return Reload(); }

    private bool ShapeFiltered => woolFilter.Count + hubFilter.Count + frontFilter.Count > 0;

    private Task ClearShapeFilters()
    {
        woolFilter.Clear(); hubFilter.Clear(); frontFilter.Clear();
        return Reload();
    }

    /// <summary>A card's wool approach families by their filter labels, each once.</summary>
    private string WoolLabels(IEnumerable<string> tokens) =>
        string.Join(", ", tokens.Distinct().Select(t => Label(WoolChips.Select(w => (w.Token, w.Label)), t)));

    // ── land spend ───────────────────────────────────────────────────────────────
    // Two currencies, never one: footprint is the box rect (fixed when the box was seated), land is what the
    // filled pieces cover, which is what the spend gate reads. The budget is the size band's, per TEAM UNIT —
    // the board is that unit fanned — so the card says "unit" rather than letting the number read as a
    // whole-board figure.

    /// <summary>The card-sized readout: the band, the unit's land against its budget, the share, and the mid's
    /// stones where the crossing carries any.</summary>
    private static string SpendShort(LandSpendDto spend) =>
        $"{spend.Band} {spend.Unit.Cells}/{spend.Unit.BudgetCells:0} · {SpendPercent(spend.Unit)}"
        + (spend.Mid.Cells > 0 ? $" · mid {spend.Mid.Cells}" : string.Empty);

    /// <summary>The share of a budget its land actually spent. Guards a zero budget rather than rendering a
    /// NaN into the card.</summary>
    private static string SpendPercent(LandAgainstBudgetDto land) =>
        land.BudgetCells > 0 ? $"{100 * land.Cells / land.BudgetCells:0}%" : "—";

    /// <summary>The hover: the same numbers spelled out, with the per-kind split and the units named.</summary>
    private static string SpendTitle(LandSpendDto spend)
    {
        var kinds = string.Join(", ", spend.ByKind.Select(k =>
            $"{k.Kind}{(k.Boxes > 1 ? $" x{k.Boxes}" : string.Empty)} {k.LandCells}"));
        return $"Size {spend.Band}: land {spend.Unit.Cells} of {spend.Unit.BudgetCells:0} budget cells for one "
             + $"team (footprint {spend.FootprintCells}). Mid: {spend.Mid.Cells} of "
             + $"{spend.Mid.BudgetCells:0} cells, shared. By box: {kinds}.";
    }

    public async ValueTask DisposeAsync()
    {
        if (observer is not null)
        {
            try { await observer.InvokeVoidAsync("disconnect"); } catch { }
            await observer.DisposeAsync();
        }
        selfRef?.Dispose();
    }

    private sealed record SvgResult(string Svg);
}
