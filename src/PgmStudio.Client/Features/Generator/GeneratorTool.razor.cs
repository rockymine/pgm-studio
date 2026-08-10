using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Generator;

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
    private static readonly (string Token, string Label)[] SeatChips =
        [("canonical", "Canonical"), ("lopsided", "Lopsided")];

    // Selected structural filters: wools are must-include (each present), hub/front/seat are any-of.
    private readonly HashSet<string> woolFilter = [];
    private readonly HashSet<string> hubFilter = [];
    private readonly HashSet<string> frontFilter = [];
    private readonly HashSet<string> seatFilter = [];

    // ── feed ─────────────────────────────────────────────────────────────────────
    private readonly List<ComposeCard> cards = [];
    private int cursor;              // next seed to request
    private int totalScanned;        // seeds composed for the current filter (matched = cards.Count)
    private bool loading, exhausted;
    private string? feedError;

    private bool StructuralActive =>
        woolFilter.Count > 0 || hubFilter.Count > 0 || frontFilter.Count > 0 || seatFilter.Count > 0;

    // ── hold tray (persisted generated plans, keyed by descriptor) ────────────────
    private List<PlanSummary> pinned = [];
    private readonly HashSet<string> pinnedKeys = [];
    private readonly Dictionary<long, string> traySvg = [];

    // ── verdicts (G118) — the current judgment per board, keyed by descriptor ─────
    private readonly Dictionary<string, VerdictDto> votes = [];

    // ── detail dialog ─────────────────────────────────────────────────────────────
    private ComposeCard? detail;

    // the annotation draft the drawer edits — seeded from the stored verdict when the drawer opens
    private string? draftVerdict;
    private readonly HashSet<string> draftTags = [];
    private string draftNote = "";

    private static string Key(ComposeRequestDto d) => $"{d.Players}-{d.Teams}-{d.Symmetry}-{d.Cell}-{d.Seed}";
    private bool IsPinned(ComposeCard c) => pinnedKeys.Contains(Key(c.Descriptor));

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        if (!firstRender) return;
        selfRef = DotNetObjectReference.Create(this);
        await RefreshPinned();
        await RefreshVerdicts();
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
        if (seatFilter.Count > 0) q += $"&seat={string.Join(",", seatFilter)}";
        return q;
    }

    // Apply the filters: clear the feed and start the seed cursor over. The structural census survives a
    // re-sieve of the same request — it is counted before the sieve, so picking a filter never invalidates it —
    // but not a change of players or symmetry, which is a different request producing different forms. Keyed on
    // the request rather than on which control fired, so no caller can forget.
    private async Task Reload()
    {
        cards.Clear();
        cursor = 0;
        totalScanned = 0;
        exhausted = false;
        feedError = null;
        if (censusKey != RequestKey) ResetCensus();
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
                Accumulate(page.Observed);
            }
        }
        catch { feedError = "Could not load boards."; exhausted = true; }
        finally { loading = false; StateHasChanged(); }
    }

    // ── the structural census (what this request actually produces) ──────────────────────────────────
    // Accumulated across pages because one page is a small sample: a form absent from 48 boards may simply not
    // have come up, while one absent from several hundred is telling you the request cannot make it.
    private readonly Dictionary<string, int> seenWools = [], seenHubs = [], seenFronts = [], seenSeats = [];
    private int censusBoards;
    private string censusKey = "";

    private string RequestKey => $"{players}/{symmetry}";

    private void ResetCensus()
    {
        seenWools.Clear(); seenHubs.Clear(); seenFronts.Clear(); seenSeats.Clear();
        censusBoards = 0;
        censusKey = RequestKey;
    }

    /// <summary>How many boards must be seen before a token's absence is worth reporting as absence rather than
    /// as a small sample. Below it the chips carry their counts but nothing is called unavailable.</summary>
    private const int CensusConfidence = 150;

    private void Accumulate(ObservedForms? o)
    {
        if (o is null) return;
        censusBoards += o.Boards;
        foreach (var (k, v) in o.Wools) seenWools[k] = seenWools.GetValueOrDefault(k) + v;
        foreach (var (k, v) in o.Hubs) seenHubs[k] = seenHubs.GetValueOrDefault(k) + v;
        foreach (var (k, v) in o.Frontlines) seenFronts[k] = seenFronts.GetValueOrDefault(k) + v;
        foreach (var (k, v) in o.Seats ?? new Dictionary<string, int>()) seenSeats[k] = seenSeats.GetValueOrDefault(k) + v;
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
        if (never.Count == 0)
            return $"No boards match these filters in the {totalScanned} scanned.";
        return $"{string.Join(" and ", never)} did not turn up in any of the {censusBoards} boards this request "
             + "composed — it is not a mix these players and symmetry produce.";
    }

    // every structural filter currently picked, with the census it reads against
    private IEnumerable<(Dictionary<string, int> Seen, string Token, string Label)> Selected()
    {
        foreach (var t in woolFilter) yield return (seenWools, t, Label(WoolChips.Select(w => (w.Token, w.Label)), t));
        foreach (var t in hubFilter) yield return (seenHubs, t, Label(HubChips, t));
        foreach (var t in frontFilter) yield return (seenFronts, t, Label(FrontChips, t));
        foreach (var t in seatFilter) yield return (seenSeats, t, Label(SeatChips, t));
    }

    private static string Label(IEnumerable<(string Token, string Label)> chips, string token) =>
        chips.FirstOrDefault(c => c.Token == token).Label ?? token;

    private string ChipTitle(Dictionary<string, int> seen, string token, string label)
    {
        var n = seen.GetValueOrDefault(token);
        if (n > 0) return $"{label} — {n} of the {censusBoards} boards scanned so far";
        return CensusIsTelling
            ? $"{label} — not produced by this request (none in {censusBoards} boards scanned)"
            : $"{label} — none yet in {censusBoards} board{(censusBoards == 1 ? "" : "s")} scanned";
    }

    /// <summary>The seat chip's tooltip: what the arrangement is, then the same census line every other chip
    /// carries — the tokens are opaque without the sides spelled out.</summary>
    private string SeatTitle(string token, string label)
    {
        var meaning = token == "canonical"
            ? "spawn on the back, wools flanking left and right"
            : "spawn on a lateral side, a wool on the back";
        return $"{ChipTitle(seenSeats, token, label)} — {meaning}";
    }

    // ── structural filters (chips + card badges; toggling re-sieves the feed immediately) ────────────
    private Task ToggleWool(string t) { Toggle(woolFilter, t); return Reload(); }
    private Task ToggleHub(string t) { Toggle(hubFilter, t); return Reload(); }
    private Task ToggleFront(string t) { Toggle(frontFilter, t); return Reload(); }
    private Task ToggleSeat(string t) { Toggle(seatFilter, t); return Reload(); }

    private static void Toggle(HashSet<string> set, string t) { if (!set.Remove(t)) set.Add(t); }

    /// <summary>Invoked from the infinite-scroll observer when the sentinel nears view.</summary>
    [JSInvokable]
    public Task LoadMore() => LoadPage();

    // ── hold tray ──────────────────────────────────────────────────────────────────
    private async Task RefreshPinned()
    {
        try
        {
            pinned = await Http.GetFromJsonAsync<List<PlanSummary>>("api/plans?origin=generated&pinned=true") ?? [];
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
        $"Held from composer {p.ComposerVersion ?? "(unrecorded)"}. Opens as stored; its seed no longer "
        + "re-composes to this board.";

    // ── verdicts (G118) ────────────────────────────────────────────────────────────
    // Voting persists: the endpoint re-composes the descriptor and stores the plan (unpinned) with the
    // verdict, so the vote map is server truth reloaded on init, exactly like the pin set.
    private async Task RefreshVerdicts()
    {
        try
        {
            var list = await Http.GetFromJsonAsync<List<VerdictDto>>("api/verdicts") ?? [];
            votes.Clear();
            foreach (var v in list)
                if (v.Descriptor is { } d) votes[Key(d)] = v;
        }
        catch { /* the vote map stays as-is */ }
    }

    private VerdictDto? VoteOf(ComposeCard c) => votes.GetValueOrDefault(Key(c.Descriptor));

    // The card's one-tap vote: cast, flip, or (same direction again) retract. A flip keeps the stored tags
    // and note — the annotation describes the board, not the direction.
    private Task VoteUp(ComposeCard c) => QuickVote(c, "up");
    private Task VoteDown(ComposeCard c) => QuickVote(c, "down");

    private async Task QuickVote(ComposeCard c, string direction)
    {
        if (VoteOf(c) is { } current && current.Verdict == direction) { await Retract(c); return; }
        await SaveVerdict(c, direction, VoteOf(c)?.Tags ?? [], VoteOf(c)?.Note);
    }

    private async Task SaveVerdict(ComposeCard c, string direction, IReadOnlyList<string> tags, string? note)
    {
        try
        {
            var resp = await Http.PostAsJsonAsync("api/compose/verdict", new VerdictSaveRequest(c.Descriptor, direction, tags, note));
            if (resp.IsSuccessStatusCode && await resp.Content.ReadFromJsonAsync<VerdictDto>() is { } dto)
                votes[Key(c.Descriptor)] = dto;
        }
        catch { /* the vote stays uncast; the card shows it */ }
        StateHasChanged();
    }

    private async Task Retract(ComposeCard c)
    {
        if (VoteOf(c) is not { } vote) return;
        try
        {
            await Http.DeleteAsync($"api/verdicts/{vote.PlanId}");
            votes.Remove(Key(c.Descriptor));
            draftVerdict = null;
        }
        catch { /* the vote stays; the card shows it */ }
        StateHasChanged();
    }

    // ── the drawer's annotation draft ─────────────────────────────────────────────
    private bool DraftIsUp => draftVerdict == "up";
    private bool DraftIsDown => draftVerdict == "down";
    private void DraftUp() => draftVerdict = "up";
    private void DraftDown() => draftVerdict = "down";
    private void ToggleDraftTag(string token) { if (!draftTags.Remove(token)) draftTags.Add(token); }
    private void OnDraftNote(ChangeEventArgs e) => draftNote = e.Value?.ToString() ?? "";

    private Task SaveDraft(ComposeCard c) => draftVerdict is null
        ? Task.CompletedTask
        : SaveVerdict(c, draftVerdict, draftTags.ToList(), string.IsNullOrWhiteSpace(draftNote) ? null : draftNote);

    private static string TagTitle(VerdictTag tag) => tag.RuleId is { } rule
        ? $"{tag.Label} — indicts rule {rule}; on a downvote where its term did not fire, that is an evaluator bug report"
        : $"{tag.Label} — no rule carries this yet";

    // ── detail dialog ──────────────────────────────────────────────────────────────
    private void OpenDetail(ComposeCard c)
    {
        detail = c;
        var vote = VoteOf(c);
        draftVerdict = vote?.Verdict;
        draftTags.Clear();
        foreach (var t in vote?.Tags ?? []) draftTags.Add(t);
        draftNote = vote?.Note ?? "";
    }

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
                && (await authored.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("slug", out var s)
                && s.GetString() is { Length: > 0 } slug)
            {
                Nav.NavigateTo($"maps/{slug}/plan");
            }
        }
        catch { /* pin succeeded but authoring failed — stay put so the user can retry */ }
    }

    // ── filter inputs ──────────────────────────────────────────────────────────────
    private void OnPlayers(ChangeEventArgs e) { if (int.TryParse(e.Value?.ToString(), out var v)) players = v; }
    private void OnMaxScore(ChangeEventArgs e) { if (double.TryParse(e.Value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) maxScore = v; }
    private void OnWoolMin(ChangeEventArgs e) { if (int.TryParse(e.Value?.ToString(), out var v)) woolMin = Math.Max(0, v); }
    private void OnWoolMax(ChangeEventArgs e) { if (int.TryParse(e.Value?.ToString(), out var v)) woolMax = Math.Max(0, v); }
    private void PickSymmetry(string s) => symmetry = s;

    // ── land spend (G148) ────────────────────────────────────────────────────────
    // Two currencies, never one: footprint is the box rect (fixed when the box was seated), land is the
    // walkable terrain inside it (what the fill spends). The budget is per TEAM UNIT — the board is that unit
    // fanned — so the card says "unit" rather than letting the number read as a whole-board figure.

    /// <summary>The card-sized readout: land against budget, plus the share.</summary>
    private static string SpendShort(LandSpendDto spend) =>
        $"{spend.LandCells}/{spend.BudgetCells:0} · {SpendPercent(spend)}";

    /// <summary>The share of the land budget the unit actually spent. Guards a zero budget rather than
    /// rendering a NaN into the card.</summary>
    private static string SpendPercent(LandSpendDto spend) =>
        spend.BudgetCells > 0 ? $"{100 * spend.LandCells / spend.BudgetCells:0}%" : "—";

    /// <summary>The hover: the same numbers spelled out, with the per-kind split and the units named.</summary>
    private static string SpendTitle(LandSpendDto spend)
    {
        var kinds = string.Join(", ", spend.ByKind.Select(k =>
            $"{k.Kind}{(k.Boxes > 1 ? $" x{k.Boxes}" : string.Empty)} {k.LandCells}"));
        return $"Land {spend.LandCells} of {spend.BudgetCells:0} budget cells, one team unit " +
               $"(footprint {spend.FootprintCells}). By box: {kinds}.";
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
