using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Pages.EditorActivities;

namespace PgmStudio.Client.Pages.Configure;

using W = WoolAuthoring;

// Wools · room step: draw the rectangle(s) a wool lives in. A room is a union of rectangles — the first
// rect over a wool's source selects that wool; further rects while it's selected ADD to its room; selecting
// a rect + deleting removes one (nothing is touch-merged or auto-resized). The confirmed symmetry orbits
// the authored room onto the partner wools (ghost = orbit copy): the primary rect keys each copy by the
// wool spawn IT covers, and the extra rects ride the same orbit step. The generator wires room defense
// (enter=not-owner) + build/break on the union; the summary previews that wiring.
public partial class WoolRoomPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
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
    private bool IsAuthored(W.Wool w) => w.Owner == anchorTeam;
    private int WithRoom => wools.Count(w => w.HasRoom);
    private string OwnerSlug(W.Wool w) => string.IsNullOrEmpty(w.Owner) ? "owner" : w.Owner;

    protected override void OnInitialized()
    {
        teams.AddRange(W.LoadTeams(Wizard.Intent));
        (symMode, symCx, symCz) = W.Sym(Wizard.Intent);
        anchorTeam = teams.FirstOrDefault()?.Id ?? "";
        wools = W.ParseWools(Wizard.Intent);
        selectedColor = (wools.FirstOrDefault(IsAuthored) ?? wools.FirstOrDefault())?.Color;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private Task OnCanvasReady() => Paint();

    private async Task OnRectDrawn((double MinX, double MinZ, double MaxX, double MaxZ) r)
    {
        if (AddRect(r)) { await Paint(); if (selectedColor is not null) await SelectOnCanvas(selectedColor); }
    }

    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        var (color, idx) = ParseRectId(e.Id);
        if (color is null || wools.FirstOrDefault(w => w.Color == color) is not { } w || !IsAuthored(w)
            || idx < 0 || idx >= w.Rooms.Count) return;   // ghosts don't resize
        w.Rooms[idx] = new W.Rect(e.MinX, e.MinZ, e.MaxX, e.MaxZ);
        DeriveAndWrite();
        await Paint(); await SelectOnCanvas(color);
    }

    // Accumulate the drawn rect onto an authored room: the selected authored wool, else the authored wool
    // whose spawn the rect covers (the "first rect over a spawn selects" rule). Ignored when neither holds.
    private bool AddRect((double MinX, double MinZ, double MaxX, double MaxZ) rect)
    {
        var target = (Selected is { } s && IsAuthored(s)) ? s
            : wools.FirstOrDefault(w => IsAuthored(w) && Covers(rect, w.SpawnX, w.SpawnZ));
        if (target is null) return false;
        target.Rooms.Add(new W.Rect(rect.MinX, rect.MinZ, rect.MaxX, rect.MaxZ));
        selectedColor = target.Color;
        DeriveAndWrite();
        return true;
    }

    // Rebuild every wool's room from the authored wools' rect-sets: orbit each authored set, keying each
    // image-set by the wool spawn its primary rect covers. Block-rounds via OrbitAssignment.
    private void DeriveAndWrite()
    {
        var sources = wools.Where(w => IsAuthored(w) && w.Rooms.Count > 0).Select(PrimaryFirst).ToList();
        foreach (var w in wools) w.Rooms = new();
        var anchors = wools.Select(w => new OrbitAssignment.Anchor(w.Color, w.SpawnX, w.SpawnZ)).ToList();
        foreach (var rects in sources)
            foreach (var set in OrbitAssignment.ByCoveredAnchorSet(rects, symMode, symCx, symCz, anchors))
                if (wools.FirstOrDefault(w => w.Color == set.Id) is { } w)
                    w.Rooms = set.Rects.Select(z => new W.Rect(z.MinX, z.MinZ, z.MaxX, z.MaxZ)).ToList();
        Write();
    }

    // Order an authored wool's rects so a spawn-covering one is the primary (orbit owner key); extras follow.
    private List<(double MinX, double MinZ, double MaxX, double MaxZ)> PrimaryFirst(W.Wool w)
    {
        var rects = w.Rooms.Select(r => (r.MinX, r.MinZ, r.MaxX, r.MaxZ)).ToList();
        var i = rects.FindIndex(r => Covers((r.MinX, r.MinZ, r.MaxX, r.MaxZ), w.SpawnX, w.SpawnZ));
        if (i > 0) { var primary = rects[i]; rects.RemoveAt(i); rects.Insert(0, primary); }
        return rects;
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

    // Only an authored room's rects show resize handles; orbit copies are ghosts.
    private async Task SelectOnCanvas(string color)
    {
        if (canvas is not null)
        {
            var w = wools.FirstOrDefault(x => x.Color == color);
            var ids = w is { HasRoom: true } && IsAuthored(w)
                ? Enumerable.Range(0, w.Rooms.Count).Select(i => RectId(color, i)).ToArray()
                : Array.Empty<string>();
            await canvas.SetSelectionAsync(ids);
        }
        StateHasChanged();
    }

    private async Task ClearRoom(W.Wool w)
    {
        if (!IsAuthored(w)) return;
        w.Rooms.Clear();
        DeriveAndWrite();
        await Paint();
    }

    private async Task DeleteRect(W.Wool w, int index)
    {
        if (!IsAuthored(w) || index < 0 || index >= w.Rooms.Count) return;
        w.Rooms.RemoveAt(index);
        DeriveAndWrite();
        await Paint();
        await SelectOnCanvas(w.Color);
    }

    private static string RectId(string color, int i) => $"{color}-wool-room-{i + 1}";

    private (string? Color, int Index) ParseRectId(string? id)
    {
        const string mid = "-wool-room-";
        if (id is not null && id.LastIndexOf(mid, StringComparison.Ordinal) is var at and >= 0
            && int.TryParse(id[(at + mid.Length)..], out var n))
        {
            var color = id[..at];
            if (wools.Any(w => w.Color == color)) return (color, n - 1);
        }
        return (null, -1);
    }

    private string? ColorFromRegionId(string? id)
    {
        if (id is null) return null;
        var (color, _) = ParseRectId(id);
        if (color is not null) return color;
        const string sfx = "wool-src-";   // the source marker id
        if (id.StartsWith(sfx) && wools.Any(w => w.Color == id[sfx.Length..])) return id[sfx.Length..];
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
            for (var i = 0; i < w.Rooms.Count; i++)
            {
                var r = w.Rooms[i];
                nodes.Add(new
                {
                    id = RectId(w.Color, i), type = "rectangle", label = $"{w.Color} wool room", color = W.Hex(w.Color),
                    ghost = !IsAuthored(w),   // orbit copies are derived previews
                    bounds = new { min_x = r.MinX, min_z = r.MinZ, max_x = r.MaxX, max_z = r.MaxZ },
                });
            }
        }
        await canvas.SetAuthorRegionsAsync(nodes);
    }
}
