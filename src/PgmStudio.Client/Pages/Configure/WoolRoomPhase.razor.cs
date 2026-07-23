using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Pages.Configure;

using W = WoolAuthoring;

// Wools · room step: draw the rectangle(s) a wool lives in. A room is a union of rectangles, listed flat
// (one row per rectangle, with the generated region name + an authored/orbit badge, like the Spawn-points
// step) and edited one at a time (the Build · Buildable-layer pattern). Drawing over a wool's source
// starts/targets that wool; further rects add to the selected rect's wool. Authored rects carry a stable id
// and resize on the canvas; the confirmed symmetry orbits them onto the partner wools as non-editable copies,
// which are also listed (read-only, "orbit" badge). The right panel shows coordinates.
public partial class WoolRoomPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed class RoomRect { public int Id; public string Color = ""; public double MinX, MinZ, MaxX, MaxZ; }
    // A list row: a rectangle (authored or orbit), its canvas region id, owning wool colour, and generated name.
    private sealed record Row(string RegionId, string Color, bool Authored, double MinX, double MinZ, double MaxX, double MaxZ, string Name);

    private readonly List<W.Team> teams = new();
    private List<W.Wool> wools = new();
    private string? symMode; private double symCx, symCz;
    private string anchorTeam = "";
    private readonly List<RoomRect> authored = new();                 // authored wools' rects (stable ids, editable)
    private readonly Dictionary<string, List<W.Rect>> ghosts = new(); // orbit copies per partner wool colour
    private int nextId = 1;
    private string? selectedRegionId;
    private EditorCanvas? canvas;

    private string Slug => Wizard.Slug;
    private W.Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;
    private bool IsAuthored(W.Wool w) => w.Owner == anchorTeam;
    private W.Wool? WoolOf(string color) => wools.FirstOrDefault(w => w.Color == color);
    private string OwnerSlug(string color) => WoolOf(color) is { Owner: { Length: > 0 } o } ? o : "owner";

    // The flat list: the authored wools' rects (editable) then each partner wool's orbit copies (read-only).
    private List<Row> Rows()
    {
        var rows = new List<Row>();
        foreach (var grp in authored.GroupBy(r => r.Color))
        {
            var list = grp.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var r = list[i];
                rows.Add(new Row(RegionId(r.Id), r.Color, true, r.MinX, r.MinZ, r.MaxX, r.MaxZ, RoomName(r.Color, i, list.Count)));
            }
        }
        foreach (var kv in ghosts)
            for (var i = 0; i < kv.Value.Count; i++)
            {
                var r = kv.Value[i];
                rows.Add(new Row(GhostId(kv.Key, i), kv.Key, false, r.MinX, r.MinZ, r.MaxX, r.MaxZ, RoomName(kv.Key, i, kv.Value.Count)));
            }
        return rows;
    }

    private Row? Selected => selectedRegionId is { } id ? Rows().FirstOrDefault(r => r.RegionId == id) : null;

    // The generated region name a rect becomes (single → {colour}-wool, several → {colour}-wool-N).
    private static string RoomName(string color, int i, int count) => count <= 1 ? $"{ColorSlug(color)}-wool" : $"{ColorSlug(color)}-wool-{i + 1}";
    private static string ColorSlug(string color) => color.Trim().ToLowerInvariant().Replace(' ', '_');

    // The id-slug a team id reduces to in the wiring (red-team → red), matching the generator's filter ids.
    private static string TeamSlug(string teamId)
    {
        var s = teamId.Trim().ToLowerInvariant();
        if (s.EndsWith("-team")) s = s[..^5];
        s = System.Text.RegularExpressions.Regex.Replace(s, "[^a-z0-9]+", "-").Trim('-');
        return s.Length > 0 ? s : teamId;
    }

    protected override void OnInitialized()
    {
        teams.AddRange(W.LoadTeams(Wizard.Intent));
        (symMode, symCx, symCz) = W.Sym(Wizard.Intent);
        anchorTeam = teams.FirstOrDefault()?.Id ?? "";
        wools = W.ParseWools(Wizard.Intent);
        authored.Clear(); ghosts.Clear(); nextId = 1; selectedRegionId = null;
        foreach (var w in wools)
            if (IsAuthored(w))   // the authored side's rooms are the editable rects
                foreach (var r in w.Rooms) authored.Add(new RoomRect { Id = nextId++, Color = w.Color, MinX = r.MinX, MinZ = r.MinZ, MaxX = r.MaxX, MaxZ = r.MaxZ });
            else if (w.Rooms.Count > 0)   // partner rooms are orbit copies
                ghosts[w.Color] = w.Rooms.ToList();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private Task OnCanvasReady() => Paint();

    private async Task OnRectDrawn((double MinX, double MinZ, double MaxX, double MaxZ) r)
    {
        // drawing over an authored wool's source starts/targets that wool; otherwise it joins the selected rect's wool
        var color = wools.FirstOrDefault(w => IsAuthored(w) && Covers(r, w.SpawnX, w.SpawnZ))?.Color
                    ?? (AuthoredFromRegion(selectedRegionId)?.Color);
        if (color is null) return;
        var b = new RoomRect { Id = nextId++, Color = color, MinX = Math.Round(r.MinX), MinZ = Math.Round(r.MinZ), MaxX = Math.Round(r.MaxX), MaxZ = Math.Round(r.MaxZ) };
        authored.Add(b);
        selectedRegionId = RegionId(b.Id);
        DeriveAndWrite();
        await Paint();
        await SelectOnCanvas(selectedRegionId);
    }

    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        if (AuthoredFromRegion(e.Id) is not { } b) return;   // only authored rects resize
        b.MinX = Math.Round(e.MinX); b.MinZ = Math.Round(e.MinZ); b.MaxX = Math.Round(e.MaxX); b.MaxZ = Math.Round(e.MaxZ);
        DeriveAndWrite();
        await Paint();
        await SelectOnCanvas(e.Id);
    }

    private async Task OnCanvasSelect(string? id)
    {
        if (AuthoredFromRegion(id) is { } b) { selectedRegionId = RegionId(b.Id); await SelectOnCanvas(selectedRegionId); }
        else { selectedRegionId = null; if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>()); StateHasChanged(); }
    }

    private async Task SelectRow(string regionId) { selectedRegionId = regionId; await SelectOnCanvas(regionId); }

    private async Task SelectOnCanvas(string regionId)
    {
        if (canvas is not null) await canvas.SetSelectionAsync(new[] { regionId });
        StateHasChanged();
    }

    private async Task Remove(string regionId)
    {
        if (AuthoredFromRegion(regionId) is not { } b) return;   // only authored rects are removable
        authored.Remove(b);
        if (selectedRegionId == regionId) selectedRegionId = null;
        DeriveAndWrite();
        await Paint();
    }

    // Recompute the orbit copies from the authored rects (grouped by wool, point-aware) and persist every
    // wool's room slice (authored wools = their authored rects; partner wools = the orbit copies).
    private void DeriveAndWrite()
    {
        ghosts.Clear();
        var anchors = wools.Select(w => new OrbitAssignment.Anchor(w.Color, w.SpawnX, w.SpawnZ)).ToList();
        foreach (var grp in authored.GroupBy(r => r.Color))
        {
            var wool = wools.FirstOrDefault(w => w.Color == grp.Key);
            if (wool is null) continue;
            var primary = CoveringFirst(grp.ToList(), wool.SpawnX, wool.SpawnZ);
            foreach (var set in OrbitAssignment.ByCoveredAnchorSet(primary, symMode, symCx, symCz, anchors))
                if (set.Id != grp.Key)
                    ghosts[set.Id] = set.Rects.Select(z => new W.Rect(z.MinX, z.MinZ, z.MaxX, z.MaxZ)).ToList();
        }
        Write();
    }

    // A wool's authored rects as bound tuples, with one covering its source first (the orbit key).
    private List<(double MinX, double MinZ, double MaxX, double MaxZ)> CoveringFirst(List<RoomRect> rects, double sx, double sz)
    {
        var list = rects.Select(r => (r.MinX, r.MinZ, r.MaxX, r.MaxZ)).ToList();
        var i = list.FindIndex(r => Covers((r.MinX, r.MinZ, r.MaxX, r.MaxZ), sx, sz));
        if (i > 0) { var p = list[i]; list.RemoveAt(i); list.Insert(0, p); }
        return list;
    }

    private static bool Covers((double MinX, double MinZ, double MaxX, double MaxZ) r, double x, double z)
        => x >= r.MinX && x <= r.MaxX && z >= r.MinZ && z <= r.MaxZ;

    private void Write()
    {
        foreach (var w in wools)
            w.Rooms = IsAuthored(w)
                ? authored.Where(r => r.Color == w.Color).Select(r => new W.Rect(r.MinX, r.MinZ, r.MaxX, r.MaxZ)).ToList()
                : ghosts.TryGetValue(w.Color, out var g) ? g.ToList() : new();
        W.WriteWools(Wizard.Intent, wools);
        Wizard.MarkDirty();
    }

    // Wool source markers + the authored rects (editable, tinted per wool) + the orbit copies (ghosts).
    private async Task Paint()
    {
        if (canvas is null) return;
        var nodes = new List<object>();
        foreach (var w in wools)
            nodes.Add(new
            {
                id = $"wool-src-{w.Color}", type = "point", marker = true, primary = false, color = W.Hex(w.Color),
                label = $"{w.Color} wool",
                bounds = new { min_x = w.SpawnX - 0.5, min_z = w.SpawnZ - 0.5, max_x = w.SpawnX + 0.5, max_z = w.SpawnZ + 0.5 },
            });
        foreach (var r in authored)
            nodes.Add(new
            {
                id = RegionId(r.Id), type = "rectangle", label = $"{r.Color} wool room", color = W.Hex(r.Color),
                bounds = new { min_x = r.MinX, min_z = r.MinZ, max_x = r.MaxX, max_z = r.MaxZ },
            });
        foreach (var kv in ghosts)
            for (var i = 0; i < kv.Value.Count; i++)
            {
                var r = kv.Value[i];
                nodes.Add(new
                {
                    id = GhostId(kv.Key, i), type = "rectangle", ghost = true,
                    label = $"{kv.Key} wool room", color = W.Hex(kv.Key),
                    bounds = new { min_x = r.MinX, min_z = r.MinZ, max_x = r.MaxX, max_z = r.MaxZ },
                });
            }
        await canvas.SetAuthorRegionsAsync(nodes);
    }

    private static string RegionId(int id) => $"room-{id}";
    private static string GhostId(string color, int i) => $"room-ghost-{color}-{i}";
    private RoomRect? AuthoredFromRegion(string? regionId)
        => regionId is { } s && s.StartsWith("room-") && !s.StartsWith("room-ghost-") && int.TryParse(s[5..], out var id)
            ? authored.FirstOrDefault(r => r.Id == id) : null;
}
