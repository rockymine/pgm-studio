using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Features.Configure;

using C = CoreAuthoring;
using Ctx = AuthoringContext;

// Cores · objectives. The ingest scan already found this map's cores — a lava volume sealed in obsidian is a
// signature nothing but a core produces — and stored them in core_candidate, so the step's real job is
// confirmation rather than detection: the author accepts a proposal, fixes the team defending it, and the
// casing the detector measured becomes the core's own <region> (OB8).
//
// A proposal is not always there (the detector finds about three in four), so a core can also be placed by
// drawing its casing footprint. That core's volume is derived from the terrain the same way the world-export
// stamper derives one — float blocks of air above the ground under the footprint — and the Casing step shows
// the resulting base Y as an editable number, because a derived height nobody can see is a height nobody can
// correct.
public partial class CoreObjectivesStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly List<Ctx.Team> teams = new();
    private List<Ctx.Island> islands = new();
    private readonly Dictionary<string, string> islandTeams = new();
    private List<C.Core> cores = new();
    private List<C.Core> detected = new();
    private C.Defaults defaults = C.Defaults.Empty;
    private bool loading = true;
    private int selected = -1;
    private WorldCanvas? canvas;

    private string Slug => Wizard.Slug;
    private C.Core? Selected => selected >= 0 && selected < cores.Count ? cores[selected] : null;
    private Ctx.Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;
    private string TeamHex(string id) => GameColors.DyeHex(TeamOf(id)?.Color ?? "");

    /// <summary>Proposals the intent does not already hold — the confirm list. Matched on the casing volume,
    /// which is the one thing a confirmed core carries verbatim from the proposal it came from.</summary>
    private IEnumerable<C.Core> Unconfirmed =>
        detected.Where(d => cores.All(c => c.Volume != d.Volume));

    protected override async Task OnInitializedAsync()
    {
        teams.AddRange(Ctx.LoadTeams(Wizard.Intent));
        foreach (var kv in Ctx.LoadIslandTeams(Wizard.Intent)) islandTeams[kv.Key] = kv.Value;
        cores = C.ParseCores(Wizard.Intent);
        if (cores.Count > 0) selected = 0;
        islands = await Ctx.LoadIslandsAsync(Http, Slug);
        await LoadSuggestionsAsync(null);
        loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private Task OnCanvasReady() => Paint();

    private void OnCanvasSelect(string? id)
    {
        const string prefix = "core-";
        if (id is { } s && s.StartsWith(prefix) && int.TryParse(s[prefix.Length..], out var i) && i < cores.Count)
            selected = i;
    }

    private void Select(int i) => selected = i;

    // The stored proposals, optionally narrowed to the box the author drew. Also the one place the casing
    // defaults arrive from — the client must not carry a second copy of ObjectiveDefaults.
    private async Task LoadSuggestionsAsync((int MinX, int MinZ, int MaxX, int MaxZ)? box)
    {
        try
        {
            var query = $"api/map/{Slug}/core-suggestions";
            if (box is { } b) query += $"?box={b.MinX},0,{b.MinZ},{b.MaxX},255,{b.MaxZ}";
            var doc = await Http.GetFromJsonAsync<JsonElement>(query);

            if (doc.TryGetProperty("defaults", out var d) && d.ValueKind == JsonValueKind.Object)
                defaults = new C.Defaults(Int(d, "size", 5), Int(d, "height", 5), Int(d, "shell", 1),
                                          Int(d, "float", 6), Int(d, "leak", 5));

            detected = new List<C.Core>();
            if (!doc.TryGetProperty("cores", out var arr) || arr.ValueKind != JsonValueKind.Array) return;
            foreach (var s in arr.EnumerateArray())
            {
                if (!s.TryGetProperty("box", out var bx) || bx.ValueKind != JsonValueKind.Object) continue;
                var volume = new C.Box(Int(bx, "minX"), Int(bx, "minY"), Int(bx, "minZ"),
                                       Int(bx, "maxX"), Int(bx, "maxY"), Int(bx, "maxZ"));
                detected.Add(new C.Core
                {
                    Owner = Ctx.IslandTeamAt(volume.CentreX, volume.CentreZ, islands, islandTeams) ?? "",
                    AnchorX = volume.CentreX, AnchorY = volume.MinY, AnchorZ = volume.CentreZ,
                    Size = Int(s, "size", defaults.Size), Height = Int(s, "height", defaults.Height),
                    Shell = Int(s, "shell", defaults.Shell), Float = Int(s, "float", defaults.Float),
                    Leak = defaults.Leak,           // not measurable from the world; PGM's own default
                    OpenTop = s.TryGetProperty("openTop", out var ot) && ot.ValueKind == JsonValueKind.True,
                    Lava = Int(s, "lava"),
                    Volume = volume,
                });
            }
        }
        catch { detected = new List<C.Core>(); }   // no scan / no candidates gathered
    }

    private async Task DetectMapWide()
    {
        loading = true; StateHasChanged();
        await LoadSuggestionsAsync(null);
        loading = false;
        await Paint();
    }

    // A drawn box confirms every proposal inside it; a box that holds none places a core on that footprint,
    // which is how a core the detector missed gets authored.
    private async Task OnBoxDrawn((double MinX, double MinZ, double MaxX, double MaxZ) rect)
    {
        int minX = (int)Math.Floor(rect.MinX), minZ = (int)Math.Floor(rect.MinZ);
        int maxX = (int)Math.Ceiling(rect.MaxX) - 1, maxZ = (int)Math.Ceiling(rect.MaxZ) - 1;
        if (maxX < minX) maxX = minX;
        if (maxZ < minZ) maxZ = minZ;

        await LoadSuggestionsAsync((minX, minZ, maxX, maxZ));
        var fresh = Unconfirmed.ToList();
        if (fresh.Count > 0) foreach (var core in fresh) Add(core);
        else await AddManualAsync(minX, minZ, maxX, maxZ);

        Write();
        await Paint();
    }

    private void Confirm(C.Core proposal) { Add(proposal); Write(); _ = Paint(); }

    private void ConfirmAll()
    {
        foreach (var proposal in Unconfirmed.ToList()) Add(proposal);
        Write();
        _ = Paint();
    }

    private void Add(C.Core core)
    {
        cores.Add(core);
        selected = cores.Count - 1;
    }

    // A hand-placed core: the drawn rectangle is the casing footprint, and its base sits `float` blocks above
    // the ground under it — the world-export stamper's own rule (ObjectiveStamper.CoreBox), so a core placed
    // here and one built from a plan end up at the same height over the same terrain. A column with no
    // segment data leaves the volume unresolved rather than guessing a Y.
    private async Task AddManualAsync(int minX, int minZ, int maxX, int maxZ)
    {
        var size = Math.Max(1, Math.Max(maxX - minX + 1, maxZ - minZ + 1));
        double centreX = (minX + maxX + 1) / 2.0, centreZ = (minZ + maxZ + 1) / 2.0;
        var core = new C.Core
        {
            Owner = Ctx.IslandTeamAt(centreX, centreZ, islands, islandTeams) ?? "",
            AnchorX = centreX, AnchorZ = centreZ,
            Size = size, Height = defaults.Height, Shell = defaults.Shell,
            Float = defaults.Float, Leak = defaults.Leak,
        };
        if (await ColumnFloor.RestingYAsync(Http, Slug, centreX, centreZ) is { } restingY)
        {
            var baseY = restingY + core.Float;
            core.AnchorY = restingY;
            core.Volume = new C.Box(minX, baseY, minZ, maxX, baseY + core.Height - 1, maxZ);
        }
        Add(core);
    }

    private void SetOwner(C.Core core, string team) { core.Owner = team; Write(); _ = Paint(); }

    private void SetName(C.Core core, string name) { core.Name = name.Trim(); Write(); }

    private void Remove(C.Core core)
    {
        cores.Remove(core);
        selected = Math.Min(selected, cores.Count - 1);
        Write();
        _ = Paint();
    }

    private void Write() { C.WriteCores(Wizard.Intent, cores); Wizard.MarkDirty(); }

    // Confirmed cores draw as their casing footprint in the defending team's colour; an unconfirmed proposal
    // draws alongside them so accepting one is a matter of recognising the box already on the canvas.
    private async Task Paint()
    {
        if (canvas is null) return;
        var shapes = new List<object>();
        for (var i = 0; i < cores.Count; i++)
        {
            if (cores[i].Volume is not { } volume) continue;
            shapes.Add(Shape($"core-{i}", volume, TeamHex(cores[i].Owner), i == selected,
                             $"core · {(string.IsNullOrEmpty(cores[i].Owner) ? "no team" : TeamName(cores[i].Owner))}"));
        }
        var n = 0;
        foreach (var proposal in Unconfirmed)
            if (proposal.Volume is { } volume)
                shapes.Add(Shape($"core-proposal-{n++}", volume, "#8a8f98", false, "proposed core"));
        await canvas.SetAuthorRegionsAsync(shapes);
    }

    private static object Shape(string id, C.Box volume, string color, bool primary, string label) => new
    {
        id,
        type = "cuboid", primary, color, label,
        bounds = new { min_x = (double)volume.MinX, min_z = (double)volume.MinZ, max_x = volume.MaxX + 1.0, max_z = volume.MaxZ + 1.0 },
    };

    private static int Int(JsonElement e, string key, int fallback = 0)
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;
}
