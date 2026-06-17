using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PgmStudio.Client.Pages.Configure;

// New-map landing: the Import flow — Source → Found → Plan — that creates a map from a terrain-only
// world and hands off to the Configure wizard. Source lists importable world folders; Next scans the
// chosen world into MariaDB; Found shows the detection brief over the reused editor canvas, with each
// finding selectable for a detail explanation; Plan starts the wizard at Map Info.
public partial class ConfigureLanding : IAsyncDisposable
{
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private sealed record Candidate(string Folder, string Slug, int RegionFiles);

    private static readonly string[] Steps = ["Source", "Found", "Plan"];
    private IReadOnlyList<string> SubSteps => Steps;

    private List<Candidate> candidates = new();
    private Candidate? selected;
    private int step;
    private int maxReached;
    private bool importing;
    private string? error;

    // Found brief — the scan (import-folder) returns the world-feature counts; symmetry is fetched after.
    private string? importedSlug;   // the map slug the scan created (canvas + endpoints + Start)
    private int woolBlocks, resourceBlocks, chestItems, spawnerBlocks, monumentCandidates, islandCount;
    private bool haveCounts;
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

    private bool BackEnabled => step > 0;
    private bool NextEnabled => OnSource ? selected is not null && !importing
                             : OnPlan ? importedSlug is not null
                             : true;
    private string NextLabel => OnSource ? importing ? "Scanning…" : "Scan & continue"
                             : OnPlan ? "Start authoring"
                             : "Next: Plan";

    // Team count the symmetry implies — the seed Teams confirms (rot_90 → 4-fold; mirror/rot_180 → 2).
    private int? SuggestedTeams => symType switch
    {
        "rot_90" => 4,
        "rot_180" or "mirror_x" or "mirror_z" => 2,
        _ => null,
    };

    // Friendly label for a detected symmetry type (matches the Configure wizard's wording).
    private static string SymLabel(string? type) => type switch
    {
        "rot_90" => "Rotate 90°",
        "rot_180" => "Rotate 180°",
        "mirror_x" => "Mirror X (left/right)",
        "mirror_z" => "Mirror Z (front/back)",
        "mirror_d1" => "Mirror ╲ (diagonal)",
        "mirror_d2" => "Mirror ╱ (diagonal)",
        _ => type ?? "",
    };

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

    private void Select(Candidate c) { selected = c; error = null; }

    private void JumpStep(int j) { if (j >= 0 && j <= maxReached && j != step) step = j; }

    private void Back() { if (step > 0) step--; }

    private async Task Next()
    {
        if (OnSource) { if (await EnsureScan()) Advance(1); }
        else if (OnFound) Advance(2);
        else if (importedSlug is not null) Nav.NavigateTo($"maps/{importedSlug}/configure");
    }

    private void Advance(int to) { step = to; if (to > maxReached) maxReached = to; }

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
                haveCounts = true;
            }
            else if (resp.StatusCode == HttpStatusCode.Conflict)
            {
                importedSlug = selected.Slug; haveCounts = false;   // already a map — show what we can
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("studio.icons");

        // Mount the reused editor canvas only while Found is showing; tear it down when leaving (the
        // step body is @key'd, so the svg refs are recreated each visit and the handle must follow).
        if (OnFound && canvasHandle is null && importedSlug is not null)
            canvasHandle = await JS.InvokeAsync<IJSObjectReference>("studio.mountScan", svgRef, wrapRef, importedSlug);
        else if (!OnFound && canvasHandle is not null)
            await DisposeCanvas();
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
