using PgmStudio.Contracts;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Configure;

using W = WoolAuthoring;
using Ctx = AuthoringContext;

// Wools · monuments step. Each wool is captured by every team except its owner (N−1 monuments). The
// objectives scan usually pre-fills them (signed pedestals); here the author confirms them and fills any
// gap by BOXING a cluster — monument-suggestions routes each hit to its colour's wool, capturing team =
// the island the monument sits on. An empty box drops one manual monument at its centre for the selected
// wool. The capturing team is editable per row.
public partial class WoolMonumentsStep
{
    // Sidebar/inspector icon for a monument — the canonical point icon, kept in sync with the region tree.
    private static readonly string PointIcon = RegionNode.Icon("point");

    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly List<Ctx.Team> teams = new();
    private List<W.Wool> wools = new();
    private string? symMode; private double symCx, symCz;
    private List<IslandDto> islands = new();
    private readonly Dictionary<string, string> islandTeams = new();
    private string? selectedColor;
    private bool detecting;
    private WorldCanvas? canvas;

    private string Slug => Wizard.Slug;
    private W.Wool? Selected => wools.FirstOrDefault(w => w.Color == selectedColor);
    private Ctx.Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;
    private string TeamColorOf(string id) => TeamOf(id)?.Color ?? "";
    private int Expected => Math.Max(0, teams.Count - 1);
    private IEnumerable<Ctx.Team> Capturers(W.Wool w) => teams.Where(t => t.Id != w.Owner);
    private W.Monument? MonumentFor(W.Wool w, string team) => w.Monuments.FirstOrDefault(m => m.Team == team);

    protected override async Task OnInitializedAsync()
    {
        teams.AddRange(Ctx.LoadTeams(Wizard.Intent));
        (symMode, symCx, symCz) = Ctx.Sym(Wizard.Intent);
        foreach (var kv in Ctx.LoadIslandTeams(Wizard.Intent)) islandTeams[kv.Key] = kv.Value;
        wools = W.ParseWools(Wizard.Intent);
        selectedColor = wools.FirstOrDefault()?.Color;
        islands = await Ctx.LoadIslandsAsync(Http, Slug);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private Task OnCanvasReady() => Paint();

    private void OnCanvasSelect(string? id)
    {
        const string pfx = "wool-mon-";
        if (id is { } s && s.StartsWith(pfx))
        {
            var color = s[pfx.Length..].Split('-')[0];
            if (wools.Any(w => w.Color == color)) selectedColor = color;
        }
    }

    private void SelectWool(string color) => selectedColor = color;

    private async Task DetectMapWide()
    {
        var (minX, minZ, maxX, maxZ) = Ctx.MapBox(islands);
        await DetectInBox(minX, minZ, maxX, maxZ);
    }

    private async Task OnBoxDrawn((double MinX, double MinZ, double MaxX, double MaxZ) r)
        => await DetectInBox((int)Math.Floor(r.MinX), (int)Math.Floor(r.MinZ), (int)Math.Ceiling(r.MaxX), (int)Math.Ceiling(r.MaxZ));

    // Box → monument-suggestions; each hit is routed to its colour's wool (capturing team = its island).
    // A box that finds nothing drops one manual monument at its centre for the selected wool.
    private async Task DetectInBox(int minX, int minZ, int maxX, int maxZ)
    {
        detecting = true; StateHasChanged();
        var added = 0;
        try
        {
            var suggestions = await Http.GetFromJsonAsync<List<MonumentSuggestionDto>>(
                $"api/map/{Slug}/monument-suggestions?box={minX},0,{minZ},{maxX},255,{maxZ}&style=Any,Any,Any");
            foreach (var m in suggestions ?? [])
            {
                var wool = wools.FirstOrDefault(w => w.Color == W.NormColor(m.Color ?? ""));
                if (wool is null) continue;
                var team = Ctx.IslandTeamAt(m.X, m.Z, islands, islandTeams) ?? "";
                if (team == wool.Owner) continue;                       // can't capture your own wool
                if (AddMonument(wool, team, m.X, m.Y, m.Z)) added++;
            }
        }
        catch { /* no candidates gathered */ }

        if (added == 0 && Selected is { } sel)                              // empty box → manual placement
        {
            double cx = (minX + maxX) / 2.0, cz = (minZ + maxZ) / 2.0;
            var team = Ctx.IslandTeamAt(cx, cz, islands, islandTeams)
                       ?? Capturers(sel).FirstOrDefault(t => MonumentFor(sel, t.Id) is null)?.Id ?? "";
            if (team != sel.Owner) AddMonument(sel, team, Math.Floor(cx), 0, Math.Floor(cz));
        }
        detecting = false;
        Write();
        await Paint();
    }

    // Add a monument unless one already sits at that block (dedupe with the auto-detected set).
    private bool AddMonument(W.Wool w, string team, double x, double y, double z)
    {
        if (w.Monuments.Any(m => (int)m.X == (int)x && (int)m.Y == (int)y && (int)m.Z == (int)z)) return false;
        w.Monuments.Add(new W.Monument { Team = team, X = x, Y = y, Z = z });
        return true;
    }

    private void SetMonumentTeam(W.Wool w, W.Monument m, string team) { m.Team = team; Write(); _ = Paint(); }

    private void DeleteMonument(W.Wool w, W.Monument m) { w.Monuments.Remove(m); Write(); _ = Paint(); }

    private void Write() { W.WriteWools(Wizard.Intent, wools); Wizard.MarkDirty(); }

    private async Task Paint()
    {
        if (canvas is null) return;
        var markers = new List<object>();
        foreach (var w in wools)
        {
            // the wool source (faint, for orientation)
            markers.Add(new
            {
                id = $"wool-src-{w.Color}",
                type = "point", marker = true, primary = false, color = W.Hex(w.Color),
                label = $"{w.Color} source",
                bounds = new { min_x = w.SpawnX - 0.5, min_z = w.SpawnZ - 0.5, max_x = w.SpawnX + 0.5, max_z = w.SpawnZ + 0.5 },
            });
            foreach (var m in w.Monuments)
                markers.Add(new
                {
                    id = $"wool-mon-{w.Color}-{m.Team}",
                    type = "point", marker = true, primary = w.Color == selectedColor, color = W.Hex(w.Color),
                    label = $"{w.Color} monument · {(string.IsNullOrEmpty(m.Team) ? "?" : TeamName(m.Team))} captures",
                    bounds = new { min_x = m.X - 0.5, min_z = m.Z - 0.5, max_x = m.X + 0.5, max_z = m.Z + 0.5 },
                });
        }
        await canvas.SetAuthorRegionsAsync(markers);
    }

}
