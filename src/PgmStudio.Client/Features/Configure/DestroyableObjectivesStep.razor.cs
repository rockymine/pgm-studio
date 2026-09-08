using PgmStudio.Contracts;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Features.Configure;

using D = DestroyableAuthoring;
using Ctx = AuthoringContext;
using PgmStudio.Geom;

// Destroyables · objectives. The ingest scan proposed this map's goals from what surrounds them rather than
// from what they are (destroyable_candidate, docs/world-scan/objective-suggestion.md §3), so the step's job
// is confirmation: the author accepts a proposal, names the team defending it, and the mass the detector
// measured becomes the goal's own <region> (OB8).
//
// It is a confirm list and not an auto-fill, and the difference is the precision. A core's signature is
// unambiguous, so its proposals are nearly all real; a destroyable's is a judgement about its neighbourhood,
// and about one proposal in three is decoration that happens to look like a goal — the observer platform
// built to the same symmetry as the map, the bell hung in a church. So the row carries the two readings in
// the words a person can check against the world rather than a score they cannot.
public partial class DestroyableObjectivesStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly List<Ctx.Team> teams = new();
    private List<IslandDto> islands = new();
    private readonly Dictionary<string, string> islandTeams = new();
    private List<D.Destroyable> destroyables = new();
    private List<D.Destroyable> detected = new();
    private D.Defaults defaults = D.Defaults.Empty;
    private bool loading = true;
    private int selected = -1;
    private WorldCanvas? canvas;

    private string Slug => Wizard.Slug;
    private D.Destroyable? Selected => selected >= 0 && selected < destroyables.Count ? destroyables[selected] : null;
    private Ctx.Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;
    private string TeamHex(string id) => GameColors.DyeHex(TeamOf(id)?.Color ?? "");

    /// <summary>Proposals the intent does not already hold — the confirm list. Matched on the mass's volume,
    /// which is the one thing a confirmed destroyable carries verbatim from the proposal it came from.</summary>
    private IEnumerable<D.Destroyable> Unconfirmed =>
        detected.Where(d => destroyables.All(existing => existing.Volume != d.Volume));

    protected override async Task OnInitializedAsync()
    {
        teams.AddRange(Ctx.LoadTeams(Wizard.Intent));
        foreach (var kv in Ctx.LoadIslandTeams(Wizard.Intent)) islandTeams[kv.Key] = kv.Value;
        destroyables = D.Parse(Wizard.Intent);
        if (destroyables.Count > 0) selected = 0;
        islands = await Ctx.LoadIslandsAsync(Http, Slug);
        await LoadSuggestionsAsync(null);
        loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private Task OnCanvasReady() => Paint();

    private void OnCanvasSelect(string? id)
    {
        const string prefix = "destroyable-";
        if (id is { } s && s.StartsWith(prefix) && int.TryParse(s[prefix.Length..], out var i) && i < destroyables.Count)
            selected = i;
    }

    private void Select(int i) => selected = i;

    // The stored proposals, optionally narrowed to the box the author drew. Also the one place the style,
    // material and float defaults arrive from — the client must not carry a second copy of ObjectiveDefaults.
    private async Task LoadSuggestionsAsync((int MinX, int MinZ, int MaxX, int MaxZ)? box)
    {
        try
        {
            var query = $"api/map/{Slug}/destroyable-suggestions";
            if (box is { } b) query += $"?box={b.MinX},0,{b.MinZ},{b.MaxX},255,{b.MaxZ}";
            var suggestions = await Http.GetFromJsonAsync<DestroyableSuggestionsDto>(query);

            if (suggestions?.Defaults is { } d)
                defaults = new D.Defaults(d.Style, d.Materials, d.Float, d.StyleOptions, d.MaterialOptions);

            detected = new List<D.Destroyable>();
            foreach (var proposal in suggestions?.Destroyables ?? [])
            {
                var mass = proposal.Box;
                var volume = new BlockBox(mass.MinX, mass.MinY, mass.MinZ, mass.MaxX, mass.MaxY, mass.MaxZ);
                detected.Add(new D.Destroyable
                {
                    Owner = Ctx.IslandTeamAt(volume.CentreX, volume.CentreZ, islands, islandTeams) ?? "",
                    AnchorX = volume.CentreX, AnchorY = volume.MinY, AnchorZ = volume.CentreZ,
                    // The material is read off the world; the shape is not, since a scanned mass is whatever
                    // its own author built rather than one of the six the stamper offers.
                    Materials = proposal.Materials,
                    Style = defaults.Style,
                    Float = defaults.Float,
                    Volume = volume,
                    Blocks = proposal.Blocks,
                    SameNearby = proposal.SameNearby,
                    Elevation = proposal.Elevation,
                });
            }
        }
        catch { detected = new List<D.Destroyable>(); }   // no scan / no candidates gathered
    }

    private async Task DetectMapWide()
    {
        loading = true; StateHasChanged();
        await LoadSuggestionsAsync(null);
        loading = false;
        await Paint();
    }

    // A drawn box confirms every proposal inside it; a box that holds none places a destroyable on that
    // footprint, which is how a goal the detector missed — or one on a map with no scan at all — is authored.
    private async Task OnBoxDrawn((double MinX, double MinZ, double MaxX, double MaxZ) rect)
    {
        int minX = (int)Math.Floor(rect.MinX), minZ = (int)Math.Floor(rect.MinZ);
        int maxX = (int)Math.Ceiling(rect.MaxX) - 1, maxZ = (int)Math.Ceiling(rect.MaxZ) - 1;
        if (maxX < minX) maxX = minX;
        if (maxZ < minZ) maxZ = minZ;

        await LoadSuggestionsAsync((minX, minZ, maxX, maxZ));
        var fresh = Unconfirmed.ToList();
        if (fresh.Count > 0) foreach (var proposal in fresh) Add(proposal);
        else await AddManualAsync(minX, minZ, maxX, maxZ);

        Write();
        await Paint();
    }

    private void Confirm(D.Destroyable proposal) { Add(proposal); Write(); _ = Paint(); }

    private void ConfirmAll()
    {
        foreach (var proposal in Unconfirmed.ToList()) Add(proposal);
        Write();
        _ = Paint();
    }

    private void Add(D.Destroyable goal)
    {
        // PGM rejects a nameless destroyable, so one is named on the way in rather than left for a refusal
        // three phases later.
        if (goal.Name.Length == 0)
            goal.Name = D.DefaultName(TeamName(goal.Owner), destroyables.Count(d => d.Owner == goal.Owner));
        destroyables.Add(goal);
        selected = destroyables.Count - 1;
    }

    // A hand-placed destroyable: the drawn rectangle is the footprint, and the structure floats `float` blocks
    // over the ground under it — the world-export stamper's own rule, so a goal placed here and one built from
    // a plan stand at the same height over the same terrain. A column with no segment data leaves the volume
    // unresolved rather than guessing a Y.
    private async Task AddManualAsync(int minX, int minZ, int maxX, int maxZ)
    {
        double centreX = (minX + maxX + 1) / 2.0, centreZ = (minZ + maxZ + 1) / 2.0;
        var goal = new D.Destroyable
        {
            Owner = Ctx.IslandTeamAt(centreX, centreZ, islands, islandTeams) ?? "",
            AnchorX = centreX, AnchorZ = centreZ,
            Style = defaults.Style, Materials = defaults.Materials, Float = defaults.Float,
        };
        if (await ColumnFloor.RestingYAsync(Http, Slug, centreX, centreZ) is { } restingY)
        {
            var baseY = restingY + goal.Float;
            goal.AnchorY = restingY;
            goal.Volume = new BlockBox(minX, baseY, minZ, maxX, baseY, maxZ);
        }
        Add(goal);
    }

    private void SetOwner(D.Destroyable goal, string team) { goal.Owner = team; Write(); _ = Paint(); }

    private void SetName(D.Destroyable goal, string name) { goal.Name = name.Trim(); Write(); }

    private void SetStyle(D.Destroyable goal, string style) { goal.Style = style; Write(); }

    private void SetMaterials(D.Destroyable goal, string materials) { goal.Materials = materials; Write(); }

    private void SetFloat(D.Destroyable goal, double blocks) { goal.Float = (int)blocks; Write(); }

    private void Remove(D.Destroyable goal)
    {
        destroyables.Remove(goal);
        selected = Math.Min(selected, destroyables.Count - 1);
        Write();
        _ = Paint();
    }

    private void Write() { D.Write(Wizard.Intent, destroyables); Wizard.MarkDirty(); }

    // Confirmed goals draw as their footprint in the defending team's colour; an unconfirmed proposal draws
    // alongside them, so accepting one is a matter of recognising the box already on the canvas.
    private async Task Paint()
    {
        if (canvas is null) return;
        var shapes = new List<object>();
        for (var i = 0; i < destroyables.Count; i++)
        {
            if (destroyables[i].Volume is not { } volume) continue;
            shapes.Add(Shape($"destroyable-{i}", volume, TeamHex(destroyables[i].Owner), i == selected,
                $"destroyable · {(string.IsNullOrEmpty(destroyables[i].Owner) ? "no team" : TeamName(destroyables[i].Owner))}"));
        }
        var n = 0;
        foreach (var proposal in Unconfirmed)
            if (proposal.Volume is { } volume)
                shapes.Add(Shape($"destroyable-proposal-{n++}", volume, "#8a8f98", false, "proposed destroyable"));
        await canvas.SetAuthorRegionsAsync(shapes);
    }

    private static object Shape(string id, BlockBox volume, string color, bool primary, string label) => new
    {
        id,
        type = "cuboid", primary, color, label,
        bounds = new { min_x = (double)volume.MinX, min_z = (double)volume.MinZ, max_x = volume.MaxX + 1.0, max_z = volume.MaxZ + 1.0 },
    };
}
