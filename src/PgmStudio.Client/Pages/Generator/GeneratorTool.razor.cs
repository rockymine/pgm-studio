using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Pages.Generator;

/// <summary>
/// The generator browse feed (G117): compose boards ahead from the server, sieve them by size/symmetry/score/
/// wool count, and keep the ones worth keeping. Cards carry only their reproducible descriptor + SVG; pinning
/// or opening a card re-composes it server-side. The hold tray is the persisted generated corpus (G119); it
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
    private int cursor;              // next seed to request
    private int totalScanned;        // seeds composed for the current filter (matched = cards.Count)
    private bool loading, exhausted;
    private string? feedError;

    private bool StructuralActive => woolFilter.Count > 0 || hubFilter.Count > 0 || frontFilter.Count > 0;

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
    private string QueryString(int seedStart)
    {
        var q = $"players={players}&symmetry={symmetry}&seedStart={seedStart}&count={PageSize}";
        if (maxScore < ScoreCap) q += $"&maxScore={maxScore.ToString(CultureInfo.InvariantCulture)}";
        if (woolMin > 0) q += $"&woolMin={woolMin}";
        if (woolMax > 0) q += $"&woolMax={woolMax}";
        if (woolFilter.Count > 0) q += $"&wools={string.Join(",", woolFilter)}";
        if (hubFilter.Count > 0) q += $"&hub={string.Join(",", hubFilter)}";
        if (frontFilter.Count > 0) q += $"&front={string.Join(",", frontFilter)}";
        return q;
    }

    // Apply the filters: clear the feed and start the seed cursor over.
    private async Task Reload()
    {
        cards.Clear();
        cursor = 0;
        totalScanned = 0;
        exhausted = false;
        feedError = null;
        await LoadPage();
    }

    private async Task LoadPage()
    {
        if (loading || exhausted) return;
        loading = true;
        StateHasChanged();
        try
        {
            var page = await Http.GetFromJsonAsync<ComposePage>($"api/compose?{QueryString(cursor)}");
            if (page is not null)
            {
                cards.AddRange(page.Cards);
                cursor = page.NextSeed;
                totalScanned += page.Scanned;
                exhausted = page.Exhausted;
            }
        }
        catch { feedError = "Could not load boards."; exhausted = true; }
        finally { loading = false; StateHasChanged(); }
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

    // ── detail dialog ──────────────────────────────────────────────────────────────
    private void OpenDetail(ComposeCard c) => detail = c;
    private void CloseDetail() => detail = null;

    private static string DescriptorJson(ComposeCard c) =>
        JsonSerializer.Serialize(c.Descriptor, new JsonSerializerOptions { WriteIndented = true });

    private Task CopyDescriptor(ComposeCard c) => JS.InvokeAsync<bool>("studio.copyText", DescriptorJson(c)).AsTask();

    // Open a card in the plan editor: ensure it is pinned (so the editor can load it by id), then navigate.
    private async Task OpenInEditor(ComposeCard c)
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
        if (id is not null) Nav.NavigateTo($"/plan-editor?plan={id}");
    }

    // ── filter inputs ──────────────────────────────────────────────────────────────
    private void OnPlayers(ChangeEventArgs e) { if (int.TryParse(e.Value?.ToString(), out var v)) players = v; }
    private void OnMaxScore(ChangeEventArgs e) { if (double.TryParse(e.Value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) maxScore = v; }
    private void OnWoolMin(ChangeEventArgs e) { if (int.TryParse(e.Value?.ToString(), out var v)) woolMin = Math.Max(0, v); }
    private void OnWoolMax(ChangeEventArgs e) { if (int.TryParse(e.Value?.ToString(), out var v)) woolMax = Math.Max(0, v); }
    private void PickSymmetry(string s) => symmetry = s;

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
