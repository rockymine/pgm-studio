using System.Net.Http;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Pages.EditorActivities;

namespace PgmStudio.Client.Pages.Configure;

using W = WoolAuthoring;

// Wools · spawn step: confirm/adjust where each wool dispenses (the <wool location> + spawner spawn point),
// seeded by the objectives step's detected source centroid. The point tool moves the selected wool's
// source; editing a wool owned by the anchor team re-derives its symmetric partners (position orbits, like
// the team-spawn step), while editing an orbit copy nudges it alone. The side-view sets the Y.
public partial class WoolSpawnPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly List<W.Team> teams = new();
    private List<W.Wool> wools = new();
    private string? symMode; private double symCx, symCz;
    private string anchorTeam = "";
    private string? selectedColor;
    private EditorCanvas? canvas;

    private string Slug => Wizard.Slug;
    private W.Wool? Selected => wools.FirstOrDefault(w => w.Color == selectedColor);
    private W.Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;
    private string TeamColorOf(string id) => TeamOf(id)?.Color ?? "";
    private bool IsAuthored(W.Wool w) => w.Owner == anchorTeam;

    protected override void OnInitialized()
    {
        teams.AddRange(W.LoadTeams(Wizard.Intent));
        (symMode, symCx, symCz) = W.Sym(Wizard.Intent);
        anchorTeam = teams.FirstOrDefault()?.Id ?? "";
        wools = W.ParseWools(Wizard.Intent);
        selectedColor = wools.FirstOrDefault()?.Color;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private Task OnCanvasReady() => Paint();

    private async Task OnPointPick((double X, double Z) p)
    {
        if (Selected is not { } w) return;
        // The wool's own level anchors the search: it usually sits in a covered room, whose roof is the
        // column's topmost surface. Read before X/Z move, and re-seat before the partners are re-derived,
        // since they copy the authored wool's Y.
        var refY = (int)Math.Floor(w.SpawnY);
        w.SpawnX = W.Snap(p.X); w.SpawnZ = W.Snap(p.Z);
        if (await ColumnFloor.RestingYAsync(Http, Slug, w.SpawnX, w.SpawnZ, refY) is { } y) w.SpawnY = y;
        if (IsAuthored(w)) ReDerivePartners(w);
        Write();
        await Paint();
    }

    private void OnCanvasSelect(string? id)
    {
        const string pfx = "wool-spawn-";
        if (id is { } s && s.StartsWith(pfx) && wools.Any(w => w.Color == s[pfx.Length..])) selectedColor = s[pfx.Length..];
    }

    private void SelectWool(string color) => selectedColor = color;

    private void SetCoord(W.Wool w, string axis, ChangeEventArgs e)
    {
        if (!double.TryParse(e.Value?.ToString(), out var v)) return;
        switch (axis)
        {
            case "x": w.SpawnX = v; break;
            case "z": w.SpawnZ = v; break;
            case "y": w.SpawnY = v; break;
        }
        if (IsAuthored(w))
        {
            if (axis is "x" or "z") ReDerivePartners(w);
            if (axis == "y") foreach (var p in Partners(w)) p.SpawnY = v;
        }
        Write();
        _ = Paint();
    }

    private void SetY(W.Wool w, int y)
    {
        w.SpawnY = y;
        if (IsAuthored(w)) foreach (var p in Partners(w)) p.SpawnY = y;
        Write();
    }

    // The orbit partners of an authored wool: for each symmetry step the wool of another owner nearest the
    // mirrored point. Position-only — colour/owner are untouched (green's mirror stays yellow, never "blue").
    private IEnumerable<W.Wool> Partners(W.Wool authored)
    {
        var order = W.OrbitOrder(symMode);
        for (var k = 1; k < order; k++)
        {
            var (mx, mz) = W.Orbit(authored.SpawnX, authored.SpawnZ, symMode, symCx, symCz, k);
            var partner = wools.Where(o => o != authored && o.Owner != anchorTeam)
                .OrderBy(o => (o.SpawnX - mx) * (o.SpawnX - mx) + (o.SpawnZ - mz) * (o.SpawnZ - mz))
                .FirstOrDefault();
            if (partner is not null) yield return partner;
        }
    }

    private void ReDerivePartners(W.Wool authored)
    {
        var order = W.OrbitOrder(symMode);
        for (var k = 1; k < order; k++)
        {
            var (mx, mz) = W.Orbit(authored.SpawnX, authored.SpawnZ, symMode, symCx, symCz, k);
            var partner = wools.Where(o => o != authored && o.Owner != anchorTeam)
                .OrderBy(o => (o.SpawnX - mx) * (o.SpawnX - mx) + (o.SpawnZ - mz) * (o.SpawnZ - mz))
                .FirstOrDefault();
            if (partner is not null) { partner.SpawnX = mx; partner.SpawnZ = mz; partner.SpawnY = authored.SpawnY; }
        }
    }

    private RegionNode SpawnNode(W.Wool w) => new()
    {
        Id = $"{w.Color}-wool-spawn@{w.SpawnX},{w.SpawnZ}", Type = "point",
        Coords = new() { ["x"] = w.SpawnX, ["y"] = w.SpawnY, ["z"] = w.SpawnZ },
    };

    private void Write() { W.WriteWools(Wizard.Intent, wools); Wizard.MarkDirty(); }

    private async Task Paint()
    {
        if (canvas is null) return;
        await canvas.SetAuthorRegionsAsync(wools.Select(w => (object)new
        {
            id = $"wool-spawn-{w.Color}",
            type = "point",
            marker = true,
            primary = w.Color == selectedColor,
            color = W.Hex(w.Color),
            label = $"{w.Color} wool",
            bounds = new { min_x = w.SpawnX - 0.5, min_z = w.SpawnZ - 0.5, max_x = w.SpawnX + 0.5, max_z = w.SpawnZ + 0.5 },
        }));
    }
}
