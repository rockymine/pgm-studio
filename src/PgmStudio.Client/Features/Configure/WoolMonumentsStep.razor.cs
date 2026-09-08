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
    // The monument whose side-view is showing, keyed by capturing team — one canvas rather than one per
    // capturer, because a four-team wool carries three and the inspector column is 230px wide.
    private string? selectedTeam;
    private BlockSeatDto? seat;
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
        if (selectedColor is not null) await SelectMonument(
            Selected is { } w0 ? Capturers(w0).FirstOrDefault(t => MonumentFor(w0, t.Id) is not null)?.Id : null);
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
            if (wools.Any(w => w.Color == color)) SelectWool(color);
        }
    }

    private void SelectWool(string color)
    {
        selectedColor = color;
        var first = Selected is { } w ? Capturers(w).FirstOrDefault(t => MonumentFor(w, t.Id) is not null)?.Id : null;
        _ = SelectMonument(first);
    }

    /// <summary>Show one monument's side-view and read whether its block can hold a wool. Raised by focus
    /// on a coordinate row, so touching a number is what points the slice at it.</summary>
    private async Task SelectMonument(string? team)
    {
        selectedTeam = team;
        seat = null;
        if (team is null || Selected is not { } wool || MonumentFor(wool, team) is not { } m) { StateHasChanged(); return; }
        seat = await ColumnFloor.SeatAtAsync(Http, Slug, m.X, m.Y, m.Z);
        StateHasChanged();
    }

    /// <summary>The sentence under the side-view: what the chosen block is, in the terms a wool is placed
    /// in. A monument is the block a player puts the wool into, so it needs to be clear and to stand on
    /// something.</summary>
    private string SeatMessage => seat switch
    {
        null => "",
        { Scanned: false } => "This column carries no scan — nothing can be said about the block.",
        { Clear: false } => "A block already stands here: the wool cannot be placed, and PGM warns on load.",
        { Pedestal: false } => "Nothing stands under it — a wool cannot be placed against air.",
        _ => "Clear, and standing on a pedestal: a wool can be placed here.",
    };

    /// <summary>A verdict that costs the author something is the tool's own warning panel; one that costs
    /// nothing is body text. Configure tints a fault and never an approval, which is what makes a tinted
    /// line mean something.</summary>
    private string SeatClass => seat is { Scanned: true, Clear: false } or { Scanned: true, Pedestal: false }
        ? "panel-warning" : "";

    // A point RegionNode for the reused SliceView. The id carries x/z so moving the monument re-points the
    // slice at its new column; a Y move keeps the same window, which is what the drag edits.
    private static RegionNode MonumentNode(W.Monument m) => new()
    {
        Id = $"monument@{m.X},{m.Z}", Type = "point",
        Coords = new() { ["x"] = m.X, ["y"] = m.Y, ["z"] = m.Z },
    };

    // Drag on the side-view, and the three coordinate inputs, write the same monument. Each rewrite re-asks
    // the seat, because the answer is about the block and the block just moved.
    private async Task SetY(W.Monument m, int y)
    {
        m.Y = y;
        Write();
        await SelectMonument(m.Team);
        await Paint();
    }

    private async Task SetCoord(W.Monument m, string axis, double v)
    {
        switch (axis) { case "x": m.X = v; break; case "y": m.Y = v; break; case "z": m.Z = v; break; }
        Write();
        await SelectMonument(m.Team);
        await Paint();
    }

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
            // Seated on the column the author boxed, not at world-bottom: a monument is a block a wool is
            // placed into, so the resting Y is the one position in that column that already answers the
            // seat check. The author moves it from there, and the side-view says what each move costs.
            var y = await ColumnFloor.RestingYAsync(Http, Slug, cx, cz) ?? 0;
            if (team != sel.Owner && AddMonument(sel, team, Math.Floor(cx), y, Math.Floor(cz)))
                await SelectMonument(team);
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
