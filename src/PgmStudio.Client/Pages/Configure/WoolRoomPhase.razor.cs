using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Pages.EditorActivities;

namespace PgmStudio.Client.Pages.Configure;

using W = WoolAuthoring;

// Wools · room step: draw the rectangle a wool lives in. The room is owned by the wool whose spawn it
// covers, and the confirmed symmetry orbits it onto the partner wools — each copy keyed by the spawn IT
// covers (shared point-aware OrbitAssignment, anchors = the wool spawns), exactly like spawn protection.
// Multiple wools per team accumulate (one draw per authored wool). The generator wires room defense
// (enter=not-owner) + build/break on each room; the summary previews that wiring.
public partial class WoolRoomPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record struct Rect(double MinX, double MinZ, double MaxX, double MaxZ);

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
    private bool IsAuthored(W.Wool w) => w.Owner == anchorTeam;
    private int WithRoom => wools.Count(w => w.HasRoom);
    private string OwnerSlug(W.Wool w) => string.IsNullOrEmpty(w.Owner) ? "owner" : w.Owner;

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

    private async Task OnRectDrawn((double MinX, double MinZ, double MaxX, double MaxZ) r)
    {
        if (ApplyRoom(r)) { await Paint(); if (selectedColor is not null) await SelectOnCanvas(selectedColor); }
    }

    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        var color = ColorFromRegionId(e.Id);
        if (color is null || wools.FirstOrDefault(w => w.Color == color) is not { } w || !IsAuthored(w)) return;   // ghosts don't resize
        if (ApplyRoom((e.MinX, e.MinZ, e.MaxX, e.MaxZ))) { await Paint(); await SelectOnCanvas(color); }
    }

    // Orbit the drawn rect and key each copy by the wool spawn it covers; merge into the room set (multiple
    // wools per team accumulate across draws). Ignored when the rect covers no wool spawn.
    private bool ApplyRoom((double MinX, double MinZ, double MaxX, double MaxZ) rect)
    {
        if (!wools.Any(w => Covers(rect, w.SpawnX, w.SpawnZ))) return false;
        var anchors = wools.Select(w => new OrbitAssignment.Anchor(w.Color, w.SpawnX, w.SpawnZ)).ToList();
        foreach (var z in OrbitAssignment.ByCoveredAnchor(rect, symMode, symCx, symCz, anchors))
            if (wools.FirstOrDefault(w => w.Color == z.Id) is { } w)
            { w.HasRoom = true; w.RoomMinX = z.MinX; w.RoomMinZ = z.MinZ; w.RoomMaxX = z.MaxX; w.RoomMaxZ = z.MaxZ; }
        var covered = wools.First(w => Covers(rect, w.SpawnX, w.SpawnZ));
        selectedColor = covered.Color;
        Write();
        return true;
    }

    private static bool Covers((double MinX, double MinZ, double MaxX, double MaxZ) r, double x, double z)
        => x >= r.MinX && x <= r.MaxX && z >= r.MinZ && z <= r.MaxZ;

    private async Task OnCanvasSelect(string? id)
    {
        var color = ColorFromRegionId(id);
        selectedColor = color;
        if (color is not null) await SelectOnCanvas(color);
        else if (canvas is not null) { await canvas.SetSelectionAsync(Array.Empty<string>()); StateHasChanged(); }
    }

    private async Task SelectWool(string color) { selectedColor = color; await SelectOnCanvas(color); }

    // Only an authored room shows resize handles; orbit copies are ghosts.
    private async Task SelectOnCanvas(string color)
    {
        if (canvas is not null)
        {
            var w = wools.FirstOrDefault(x => x.Color == color);
            await canvas.SetSelectionAsync(w is { HasRoom: true } && IsAuthored(w) ? new[] { RegionId(color) } : Array.Empty<string>());
        }
        StateHasChanged();
    }

    private async Task ClearRoom(W.Wool w)
    {
        w.HasRoom = false;
        Write();
        await Paint();
    }

    private static string RegionId(string color) => $"{color}-wool-room";
    private string? ColorFromRegionId(string? id)
    {
        const string sfx = "-wool-room";
        if (id is not null && id.EndsWith(sfx) && wools.Any(w => w.Color == id[..^sfx.Length])) return id[..^sfx.Length];
        return null;
    }

    private void Write() { W.WriteWools(Wizard.Intent, wools); Wizard.MarkDirty(); }

    private async Task Paint()
    {
        if (canvas is null) return;
        var nodes = new List<object>();
        foreach (var w in wools)
        {
            nodes.Add(new   // the wool source marker, for orientation
            {
                id = $"wool-src-{w.Color}", type = "point", marker = true, primary = false, color = W.Hex(w.Color),
                label = $"{w.Color} wool",
                bounds = new { min_x = w.SpawnX - 0.5, min_z = w.SpawnZ - 0.5, max_x = w.SpawnX + 0.5, max_z = w.SpawnZ + 0.5 },
            });
            if (w.HasRoom)
                nodes.Add(new
                {
                    id = RegionId(w.Color), type = "rectangle", label = $"{w.Color} wool room", color = W.Hex(w.Color),
                    ghost = !IsAuthored(w),   // orbit copies are derived previews
                    bounds = new { min_x = w.RoomMinX, min_z = w.RoomMinZ, max_x = w.RoomMaxX, max_z = w.RoomMaxZ },
                });
        }
        await canvas.SetAuthorRegionsAsync(nodes);
    }
}
