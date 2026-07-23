using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Geom;

namespace PgmStudio.Client.Pages.Configure;

// New-map landing: the Import flow — Source → Found → Plan — that creates a map from a terrain-only
// world and hands off to the Configure wizard. Source lists importable world folders; Next scans the
// chosen world into MariaDB; Found shows the detection brief over the reused editor canvas, with each
// finding selectable for a detail explanation; Plan starts the wizard at Identity.
public partial class ConfigureLanding : IAsyncDisposable
{
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private sealed record Candidate(string Folder, string Slug, int RegionFiles);

    private static readonly string[] Steps = ["Source", "Found", "Plan"];

    private List<Candidate> candidates = new();
    private Candidate? selected;
    private int step;
    private bool importing;
    private string? error;

    // URL-import source (the parallel "From a download link" path) — its own input + spinner, separate
    // from the folder scan. A successful fetch lands on Found with no folder `selected`, so the brief's
    // source label/count come from the slug + mca count the endpoint returned (SourceFolder/RegionFiles).
    private string urlInput = "";
    private bool importingUrl;
    private int mcaFiles;

    // Found brief — the scan (import-folder) returns the world-feature counts; symmetry is fetched after.
    private string? importedSlug;   // the map slug the scan created (canvas + endpoints + Start)
    private int woolBlocks, resourceBlocks, chestItems, spawnerBlocks, monumentCandidates, islandCount;
    private string? symType;   // symmetry + suggested teams are shown as inline facts, not selectable findings

    // Found detail brief — the currently-selected finding (left list) drives the right detail panel.
    private string selectedFinding = "islands";
    private sealed record IslandInfo(int BlockCount);
    private sealed record WoolColorInfo(string Name, string Hex, int Count);
    private sealed record ResourceTypeInfo(string Name, int Count);
    private List<IslandInfo> islands = new();
    private List<WoolColorInfo> woolColors = new();
    private List<ResourceTypeInfo> resourceTypes = new();
    private int chestCount;   // distinct chests (chestItems is the per-slot item total)

    // The findings listed in the left panel; selecting one explains it on the right.
    private sealed record Finding(string Key, string Icon, string Label, string Group);
    private static readonly Finding[] Findings =
    [
        new("islands",   "layers",  "Islands",         "detected"),
        new("wool",      "palette", "Wool blocks",     "built"),
        new("monuments", "flag",    "Monuments",       "built"),
        new("resources", "gem",     "Resource blocks", "built"),
        new("chests",    "box",     "Chests",          "built"),
        new("spawners",  "bug",     "Spawners",        "built"),
    ];

    private ElementReference svgRef, wrapRef;
    private IJSObjectReference? canvasHandle;

    private bool OnSource => step == 0;
    private bool OnFound => step == 1;
    private bool OnPlan => step == 2;

    // The scanned world's source label + region-file count for the Found brief — a folder candidate when
    // one was picked, else the URL import's slug + extracted .mca count.
    private string SourceFolder => selected?.Folder ?? importedSlug ?? "";
    private int SourceRegionFiles => selected?.RegionFiles ?? mcaFiles;

    // The scan is what unlocks Found + Plan: before it only Source is reachable; after it the user can
    // move freely across all three (a processed world has its whole brief ready). Re-picking a different
    // world on Source clears the scan, re-locking the later steps until it's scanned again.
    private bool Scanned => importedSlug is not null;
    private int MaxStep => Scanned ? 2 : 0;

    private bool BackEnabled => step > 0;
    private bool NextEnabled => OnSource ? selected is not null && !importing
                             : OnPlan ? importedSlug is not null
                             : true;
    private string NextLabel => OnSource ? importing ? "Scanning…" : "Scan & continue"
                             : OnPlan ? "Start authoring"
                             : "Next: Plan";

    // Team count the symmetry implies — its orbit order (rot_90 → 4-fold; mirror/rot_180 → 2); no symmetry → null.
    private int? SuggestedTeams => Symmetry.Order(symType) is var o && o > 1 ? o : null;

    private void SelectFinding(string key) => selectedFinding = key;
    private Finding? Find(string key) => Findings.FirstOrDefault(f => f.Key == key);
    private string FindingIcon(string key) => Find(key)?.Icon ?? "info";
    private string FindingLabel(string key) => Find(key)?.Label ?? "";

    // The short value shown on a finding's row + its detail header (the "what we found" at a glance).
    private string FindingTag(string key) => key switch
    {
        "islands"   => islandCount.ToString(),
        "wool"      => woolBlocks.ToString(),
        "monuments" => $"{monumentCandidates} candidates",
        "resources" => resourceBlocks.ToString(),
        "chests"    => chestCount.ToString(),
        "spawners"  => spawnerBlocks.ToString(),
        _ => "",
    };

    // How a finding was detected and what it means — the right panel's explanation.
    private string FindingExplanation(string key) => key switch
    {
        "islands"   => "Connected landmasses found by flood-filling the cleaned-up terrain. Each island is somewhere a team can be based — you'll review them, and drop any stray rocks, in the World phase.",
        "wool"      => "Wool already placed in the world, grouped by colour. These hint at the objective colours; you'll turn them into capturable wools in the Wools phase.",
        "monuments" => "Likely spots where a captured wool would be placed, detected from the build patterns around them. You'll confirm and place monuments in the Wools phase.",
        "resources" => "Ore and resource blocks found in the terrain, by type — useful context for kits and balance.",
        "chests"    => $"Chests found in the world, holding {chestItems} item stacks in all — their contents can seed starting kits later.",
        "spawners"  => "Mob and wool spawners found in the world. Wool spawners can feed objectives; mob spawners are usually decorative.",
        _ => "",
    };

    // What each phase does, in a sentence — leads with the studio's automation so the card sells the help.
    private static string PlanBlurb(string phaseId) => phaseId switch
    {
        "info"   => "Name your map and credit the authors — version, mode, and objective fill in automatically.",
        "world"  => "Confirm the islands and symmetry we detected; one click seeds your team count and spawn points.",
        "teams"  => "Set up a single team's islands, spawn, and protection — symmetry mirrors it to every other team for you.",
        "build"  => "Set the build height and bridge the gaps over the void. The scanned terrain is already playable.",
        "wools"  => "Choose your wool colours and place monuments — capture rules and room defenses wire themselves.",
        "review" => "Automatic checks for mirroring, buildability, and reachability, then your finished, PGM-ready map.xml.",
        _ => "",
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var arr = await Http.GetFromJsonAsync<JsonElement>("api/maps/import-candidates");
            candidates = arr.ValueKind == JsonValueKind.Array
                ? arr.EnumerateArray().Select(c => new Candidate(
                    Str(c, "folder"), Str(c, "slug"),
                    c.TryGetProperty("region_files", out var rf) ? rf.GetInt32() : 0)).ToList()
                : new();
            selected = candidates.FirstOrDefault();
        }
        catch { error = "Couldn't list import candidates."; }
    }

    private void Select(Candidate c)
    {
        if (selected == c) return;
        selected = c; error = null;
        // Picking a different world than the one already scanned drops that scan's brief, so Found/Plan
        // re-lock and the canvas/panels can't show a world other than the one currently selected.
        if (Scanned && importedSlug != c.Slug) ResetScan();
    }

    private void JumpStep(int j) { if (j >= 0 && j <= MaxStep && j != step) SetStep(j); }

    private void Back() { if (step > 0) SetStep(step - 1); }

    private async Task Next()
    {
        // Scanned already (a folder re-visit or a completed URL import) just advances; otherwise scan first.
        if (OnSource) { if (Scanned || await EnsureScan()) SetStep(1); }
        else if (OnFound) SetStep(2);
        else if (importedSlug is not null) Nav.NavigateTo($"maps/{importedSlug}/configure");
    }

    /// <summary>Fetch + import a world from a download link (allow-listed host, server-side) and load its
    /// brief — the URL twin of <see cref="EnsureScan"/>. On success there is no folder <c>selected</c>, so
    /// the brief's source label comes from the returned slug + .mca count; advances to Found.</summary>
    private async Task ImportFromUrl()
    {
        var url = urlInput.Trim();
        if (url.Length == 0 || importingUrl) return;

        importingUrl = true; error = null; StateHasChanged();
        try
        {
            var resp = await Http.PostAsJsonAsync("api/map/import-url",
                new Dictionary<string, object?> { ["url"] = url });
            if (!resp.IsSuccessStatusCode) { error = await ErrorMessage(resp, "Import failed"); return; }

            var r = await resp.Content.ReadFromJsonAsync<JsonElement>();
            selected = null;   // a URL world is not a local folder candidate — drop any prior pick
            selectedFinding = "islands";
            importedSlug = Str(r, "slug"); mcaFiles = Int(r, "mca_files");
            woolBlocks = Int(r, "wool_blocks"); resourceBlocks = Int(r, "resource_blocks");
            chestItems = Int(r, "chest_items"); spawnerBlocks = Int(r, "spawner_blocks");
            monumentCandidates = Int(r, "monument_candidates"); islandCount = Int(r, "islands");

            await LoadBrief();
            SetStep(1);
        }
        catch { error = "Import failed."; }
        finally { importingUrl = false; StateHasChanged(); }
    }

    // Surface the endpoint's own message (host not allowed / https required / already exists …) when it
    // sends one, so the allow-list and validation failures read clearly; else fall back to the status code.
    private static async Task<string> ErrorMessage(HttpResponseMessage resp, string fallback)
    {
        try
        {
            var e = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var msg = Str(e, "error");
            if (msg.Length > 0) return char.ToUpperInvariant(msg[0]) + msg[1..] + ".";
        }
        catch { /* non-JSON body — fall through to the status code */ }
        return $"{fallback} ({(int)resp.StatusCode}).";
    }

    // Single funnel for every step change: tear the canvas down the moment we leave Found so the next
    // visit re-mounts on the freshly-keyed <svg> (the workspace is @key'd, so its svg ref is recreated).
    private void SetStep(int to)
    {
        if (step == 1 && to != 1 && canvasHandle is not null) _ = DisposeCanvas();
        step = to;
    }

    // Drop a scan's brief (its slug + counts + canvas) so the import resets to "pick a world".
    private void ResetScan()
    {
        if (canvasHandle is not null) _ = DisposeCanvas();
        importedSlug = null;
        woolBlocks = resourceBlocks = chestItems = spawnerBlocks = monumentCandidates = islandCount = mcaFiles = 0;
        symType = null; selectedFinding = "islands";
        islands = new(); woolColors = new(); resourceTypes = new(); chestCount = 0;
        if (step != 0) step = 0;
    }

    /// <summary>Scan the selected world into MariaDB (once per session) and load its detection brief.</summary>
    private async Task<bool> EnsureScan()
    {
        if (selected is null) return false;
        if (importedSlug == selected.Slug) return true;   // already scanned this session

        importing = true; error = null; StateHasChanged();
        try
        {
            var resp = await Http.PostAsJsonAsync("api/map/import-folder",
                new Dictionary<string, object?> { ["folder"] = selected.Folder });
            if (resp.IsSuccessStatusCode)
            {
                var r = await resp.Content.ReadFromJsonAsync<JsonElement>();
                importedSlug = Str(r, "slug");
                woolBlocks = Int(r, "wool_blocks"); resourceBlocks = Int(r, "resource_blocks");
                chestItems = Int(r, "chest_items"); spawnerBlocks = Int(r, "spawner_blocks");
                monumentCandidates = Int(r, "monument_candidates"); islandCount = Int(r, "islands");
            }
            else if (resp.StatusCode == HttpStatusCode.Conflict)
            {
                importedSlug = selected.Slug;   // already a map — show what we can
            }
            else { error = $"Scan failed ({(int)resp.StatusCode})."; return false; }

            await LoadBrief();
            return true;
        }
        catch { error = "Scan failed."; return false; }
        finally { importing = false; StateHasChanged(); }
    }

    /// <summary>Load the per-finding detail data for the brief (symmetry, islands, wool/resource breakdowns).</summary>
    private Task LoadBrief() => Task.WhenAll(LoadSymmetry(), LoadIslands(), LoadScanSummary());

    private async Task LoadSymmetry()
    {
        symType = null;
        try
        {
            var resp = await Http.GetAsync($"api/map/{importedSlug}/symmetry");
            if (!resp.IsSuccessStatusCode) return;
            var s = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (s.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.Object)
                symType = Str(p, "type");
        }
        catch { /* leave the symmetry fact blank if it can't be computed */ }
    }

    private async Task LoadIslands()
    {
        islands = new();
        try
        {
            var resp = await Http.GetAsync($"api/map/{importedSlug}/islands");
            if (!resp.IsSuccessStatusCode) return;
            var arr = await resp.Content.ReadFromJsonAsync<JsonElement>();
            if (arr.ValueKind == JsonValueKind.Array)
                islands = arr.EnumerateArray()
                    .Select(i => new IslandInfo(i.TryGetProperty("block_count", out var bc) ? bc.GetInt32() : 0))
                    .OrderByDescending(i => i.BlockCount).ToList();
        }
        catch { /* canvas still renders islands; the size list just stays empty */ }
    }

    private async Task LoadScanSummary()
    {
        woolColors = new(); resourceTypes = new(); chestCount = 0;
        try
        {
            var s = await Http.GetFromJsonAsync<JsonElement>($"api/map/{importedSlug}/scan-summary");
            chestCount = Int(s, "chest_count");
            if (s.TryGetProperty("wool_colors", out var wc) && wc.ValueKind == JsonValueKind.Array)
                woolColors = wc.EnumerateArray()
                    .Select(w => new WoolColorInfo(Str(w, "name"), Str(w, "hex"),
                        w.TryGetProperty("count", out var c) ? c.GetInt32() : 0)).ToList();
            if (s.TryGetProperty("resource_types", out var rt) && rt.ValueKind == JsonValueKind.Array)
                resourceTypes = rt.EnumerateArray()
                    .Select(r => new ResourceTypeInfo(Str(r, "name"),
                        r.TryGetProperty("count", out var c) ? c.GetInt32() : 0)).ToList();
        }
        catch { /* breakdowns just stay empty */ }
    }

    private bool canvasBusy;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");
        await SyncCanvas();
    }

    // Reconcile the reused editor canvas with the current step. Re-reads the live state AFTER the
    // studio.icons await (so a step change mid-await is honoured) and serialises with a busy flag so two
    // close renders can't double-mount onto the same <svg>. The handle is nulled the instant we leave
    // Found (SetStep), so re-entry always re-mounts on the recreated svg ref rather than a detached one.
    private async Task SyncCanvas()
    {
        if (canvasBusy) return;
        canvasBusy = true;
        try
        {
            if (OnFound && importedSlug is not null)
            {
                if (canvasHandle is null)
                    canvasHandle = await JS.InvokeAsync<IJSObjectReference>("studio.mountScan", svgRef, wrapRef, importedSlug);
            }
            else if (canvasHandle is not null)
            {
                await DisposeCanvas();
            }
        }
        finally { canvasBusy = false; }
    }

    private async Task DisposeCanvas()
    {
        var h = canvasHandle; canvasHandle = null;
        if (h is null) return;
        try { await h.InvokeVoidAsync("dispose"); } catch { }
        try { await h.DisposeAsync(); } catch { }
    }

    public async ValueTask DisposeAsync() => await DisposeCanvas();

    private static string Str(JsonElement e, string k)
        => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static int Int(JsonElement e, string k)
        => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
}
