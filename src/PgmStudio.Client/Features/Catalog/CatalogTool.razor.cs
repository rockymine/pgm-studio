using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
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
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>Re-run the lucide factory after every render: the panel's glyphs are created when it opens,
    /// long after the page's first paint, and an unprocessed `&lt;i data-lucide&gt;` renders as a blank box.</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender) =>
        await JS.InvokeVoidAsync("studio.icons");

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

    // ── the knob panel ───────────────────────────────────────────────────────────
    // The half a gallery cannot be: emit one combination live and show what the emitter said — including
    // when it refuses, which is the only place the studio surfaces the fill guards at all.

    private static readonly string[] Edges = ["Top", "Bottom", "Left", "Right"];

    private IReadOnlyList<ShapeFamilyDto> schemaFamilies = [];
    private ShapeProbeResult? probe;
    private bool probeOpen;

    private string probeFamily = "i";
    private int probeW = 8, probeH = 8, probeCw = 2, probeAttachW;
    private string probeMouth = "Top";
    private bool probeFlip, probeSideTuck, probeWoolAtEnd;

    /// <summary>Serialized so a fast drag cannot land results out of order — a later request always wins,
    /// and a stale reply is dropped rather than repainting the panel with an older answer.</summary>
    private int probeGeneration;

    private ShapeFamilyDto? ProbeFamilySchema => schemaFamilies.FirstOrDefault(f => f.Token == probeFamily);

    private bool SideTuckAllowed => ProbeFamilySchema?.Knobs.Contains("sideTuck") ?? false;
    private bool WoolAtEndAllowed => ProbeFamilySchema?.Knobs.Contains("woolAtEnd") ?? false;
    private bool AttachWAllowed => ProbeFamilySchema?.Knobs.Contains("attachW") ?? false;

    private string SideTuckTitle =>
        SideTuckAllowed ? "Turn the room off the end, perpendicular" : "the emitter builds side-tuck for I, Z and scythe only";

    private string WoolAtEndTitle =>
        WoolAtEndAllowed ? "Put the terminal on an end rather than the middle" : "not a knob this family takes";

    /// <summary>Open the panel seeded from a card, so the first thing it shows is that exact shape and the
    /// author edits from a known-good state rather than guessing a starting box.</summary>
    private async Task OpenProbe(CatalogShapeDto shape)
    {
        probeOpen = true;
        if (shape.Kind == "wool") probeFamily = shape.Family;
        probeW = shape.BoxW;
        probeH = shape.BoxH;
        probeCw = shape.CorridorCells;
        probeMouth = "Top";
        probeFlip = shape.Knobs.Contains("flipped");
        probeSideTuck = shape.Knobs.Contains("side-tuck");
        probeWoolAtEnd = shape.Knobs.Any(k => k.Contains("wool at end") || k.Contains("corner wool"));
        probeAttachW = shape.Knobs
            .Where(k => k.StartsWith("attach "))
            .Select(k => int.TryParse(k[7..], out var n) ? n : 0)
            .FirstOrDefault();
        if (schemaFamilies.Count == 0) await LoadSchema();
        await Emit();
    }

    private void CloseProbe()
    {
        probeOpen = false;
        probe = null;
    }

    private async Task LoadSchema()
    {
        try
        {
            var schema = await Http.GetFromJsonAsync<ShapeProbeSchema>("api/shapes/probe/schema");
            if (schema is not null) schemaFamilies = schema.Families;
        }
        catch (HttpRequestException)
        {
            // the panel still works without it; only the knob gating goes conservative
        }
    }

    /// <summary>Emit the current knob combination. A refusal is a result, so there is no error path here —
    /// only a transport failure clears the panel.</summary>
    private async Task Emit()
    {
        var mine = ++probeGeneration;
        var query =
            $"api/shapes/probe?family={probeFamily}&w={probeW}&h={probeH}&cw={probeCw}&mouth={probeMouth}" +
            $"&flip={probeFlip}&sideTuck={probeSideTuck}&woolAtEnd={probeWoolAtEnd}&attachW={probeAttachW}";
        try
        {
            var result = await Http.GetFromJsonAsync<ShapeProbeResult>(query);
            if (mine == probeGeneration) probe = result;
        }
        catch (HttpRequestException)
        {
            if (mine == probeGeneration) probe = null;
        }
    }

    private async Task PickFamily(string token)
    {
        probeFamily = token;
        // a knob the new family does not take would otherwise ride along and produce a refusal the author
        // did not ask for — drop them rather than let the panel argue with itself
        if (!SideTuckAllowed) probeSideTuck = false;
        if (!WoolAtEndAllowed) probeWoolAtEnd = false;
        if (!AttachWAllowed) probeAttachW = 0;
        await Emit();
    }

    private async Task PickMouth(string edge) { probeMouth = edge; await Emit(); }
    private async Task ToggleFlip() { probeFlip = !probeFlip; await Emit(); }
    private async Task ToggleSideTuck() { probeSideTuck = !probeSideTuck; await Emit(); }
    private async Task ToggleWoolAtEnd() { probeWoolAtEnd = !probeWoolAtEnd; await Emit(); }

    private async Task ResizeTo(int w, int h)
    {
        // the emitter told us the minimum; take it verbatim rather than nudging one axis at a time
        (probeW, probeH) = (w, h);
        await Emit();
    }

    private Task OnWidth(ChangeEventArgs e) => SetNumber(e, v => probeW = v);
    private Task OnHeight(ChangeEventArgs e) => SetNumber(e, v => probeH = v);
    private Task OnCorridor(ChangeEventArgs e) => SetNumber(e, v => probeCw = v);
    private Task OnAttachW(ChangeEventArgs e) => SetNumber(e, v => probeAttachW = v);

    private async Task SetNumber(ChangeEventArgs e, Action<int> set)
    {
        if (!int.TryParse(e.Value?.ToString(), out var value)) return;
        set(value);
        await Emit();
    }
}
