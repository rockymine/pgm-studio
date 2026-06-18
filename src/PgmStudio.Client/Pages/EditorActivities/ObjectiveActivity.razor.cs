using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class ObjectiveActivity
{
    [Parameter] public string Slug { get; set; } = "";

    private EditorCanvas? canvas;
    private readonly List<Wool> wools = new();
    private readonly List<Team> teams = new();
    private List<RegionGroup>? groups;
    private readonly Dictionary<string, RegionNode> nodeMap = new();

    private string? selWool, selRegion, selMon;
    private HashSet<string> selRegionSet = new();
    private string? error;

    // wool-location form fields + monument form fields
    private string? wLocX, wLocY, wLocZ, mLocX, mLocY, mLocZ;

    private Wool? CurrentWool => wools.FirstOrDefault(w => w.Id == selWool);
    private RegionNode? RegionNodeSel => selRegion is not null ? nodeMap.GetValueOrDefault(selRegion) : null;

    private sealed class Team { public string Id = ""; public string Name = ""; public string Color = "red"; }
    private sealed class Loc { public double X, Y, Z; }
    private sealed class Monument { public string Id = ""; public string? Team; public Loc? Location; public string? MonumentRegion; }
    private sealed class Wool { public string Id = ""; public string Color = ""; public string? Team; public Loc? Location; public string? WoolRoomRegion; public List<Monument> Monuments = new(); }

    // The objective trio is one `wool` category now; split it by subtype into the three sections authors
    // think in (rooms / monuments / spawners). Mirrors the Teams spawn point/protection split — and like
    // it, we walk EVERY tree group, because objective regions nest under rule-containers in "other": in
    // annealing the 12 monuments are carved-out children of the `spawns` complement, not wool-group roots.
    // We keep the top-most node of each subtype so a room union (woolrooms) stays nested while the buried
    // monuments surface as flat rows.
    private List<RegionGroup> CollectWoolGroups(List<RegionGroup> all)
    {
        var buckets = new Dictionary<string, List<RegionNode>> { ["room"] = new(), ["monument"] = new(), ["spawner"] = new(), ["draft"] = new() };
        var claimed = new HashSet<string>();
        foreach (var grp in all) foreach (var n in grp.Regions) CollectWool(n, claimed, buckets);
        return
        [
            new() { Name = "room",     Label = "Wool Rooms",    Regions = buckets["room"] },
            new() { Name = "monument", Label = "Monuments",     Regions = buckets["monument"] },
            new() { Name = "spawner",  Label = "Wool Spawners", Regions = buckets["spawner"] },
            new() { Name = "draft",    Label = "Draft",         Regions = buckets["draft"] },   // drawn here, not yet wired (E10)
        ];
    }

    private void CollectWool(RegionNode n, HashSet<string> claimed, Dictionary<string, List<RegionNode>> buckets)
    {
        if (!string.IsNullOrEmpty(n.Id)) nodeMap.TryAdd(n.Id, n);
        var s = n.Subtype;
        var claim = s is "room" or "monument" or "spawner" && claimed.Add(s!);   // top-most of this subtype on the path
        if (claim) buckets[s!].Add(n);
        else if (n.DraftStep == "objective" && n.Category == "other") buckets["draft"].Add(n);
        foreach (var c in n.Children) CollectWool(c, claimed, buckets);
        if (n.Source is not null) CollectWool(n.Source, claimed, buckets);
        if (claim) claimed.Remove(s!);
    }

    protected override async Task OnParametersSetAsync() => await Reload();

    private async Task Reload()
    {
        var keepWool = selWool; var keepMon = selMon; var keepRegion = selRegion;
        wools.Clear(); teams.Clear(); nodeMap.Clear(); groups = null;
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}");
            if (doc.TryGetProperty("teams", out var ts) && ts.ValueKind == JsonValueKind.Array)
                foreach (var t in ts.EnumerateArray())
                    teams.Add(new Team { Id = S(t, "id"), Name = S(t, "name"), Color = S(t, "color", "red") });
            if (doc.TryGetProperty("wools", out var ws) && ws.ValueKind == JsonValueKind.Array)
                foreach (var w in ws.EnumerateArray())
                {
                    var wool = new Wool
                    {
                        Id = S(w, "id"), Color = S(w, "color"),
                        Team = Opt(w, "team"), Location = ParseLoc(w, "location"), WoolRoomRegion = Opt(w, "wool_room_region"),
                    };
                    if (w.TryGetProperty("monuments", out var ms) && ms.ValueKind == JsonValueKind.Array)
                        foreach (var m in ms.EnumerateArray())
                            wool.Monuments.Add(new Monument { Id = S(m, "id"), Team = Opt(m, "team"), Location = ParseLoc(m, "location"), MonumentRegion = Opt(m, "monument_region") });
                    wools.Add(wool);
                }

            var tree = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/regions/tree");
            groups = tree.TryGetProperty("groups", out var g) ? CollectWoolGroups(RegionGroup.ParseGroups(g)) : new();
        }
        catch (Exception ex) { error = ex.Message; }

        // restore selection
        selWool = keepWool is not null && wools.Any(w => w.Id == keepWool) ? keepWool : null;
        selRegion = keepRegion is not null && nodeMap.ContainsKey(keepRegion) ? keepRegion : null;
        if (selWool is not null) { PopulateWoolForm(CurrentWool!); selMon = CurrentWool!.Monuments.Any(m => m.Id == keepMon) ? keepMon : CurrentWool.Monuments.FirstOrDefault()?.Id; if (selMon is not null) PopulateMonForm(CurrentWool.Monuments.First(m => m.Id == selMon)); }
        StateHasChanged();
    }


    // ── selection ──────────────────────────────────────────────────────────────

    private async Task SelectWool(string id)
    {
        selWool = id; selRegion = null; selRegionSet = new();
        var w = CurrentWool;
        if (w is not null) { PopulateWoolForm(w); selMon = w.Monuments.FirstOrDefault()?.Id; if (selMon is not null) PopulateMonForm(w.Monuments.First()); }
        if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>());
        StateHasChanged();
    }

    private async Task Select(string? id)
    {
        if (id is null || !nodeMap.TryGetValue(id, out var node)) { await Deselect(); return; }
        selRegion = id; selWool = null;
        selRegionSet = new(); CollectDescendants(node, selRegionSet);
        if (canvas is not null) await canvas.SetSelectionAsync(selRegionSet);
        StateHasChanged();
    }

    private static void CollectDescendants(RegionNode n, HashSet<string> outSet)
    {
        if (!string.IsNullOrEmpty(n.Id)) outSet.Add(n.Id);
        foreach (var c in n.Children) CollectDescendants(c, outSet);
    }

    private async Task Deselect() { selWool = null; selRegion = null; selRegionSet = new(); if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>()); StateHasChanged(); }
    private Task OnTreeSelect(RegionNode n) => Select(n.Id);
    private Task OnCanvasSelect(string? id) => Select(id);

    private void SelectMonument(string id) { selMon = id; var m = CurrentWool?.Monuments.FirstOrDefault(x => x.Id == id); if (m is not null) PopulateMonForm(m); }

    private void PopulateWoolForm(Wool w) { wLocX = w.Location?.X.ToString(); wLocY = w.Location?.Y.ToString(); wLocZ = w.Location?.Z.ToString(); }
    private void PopulateMonForm(Monument m) { mLocX = m.Location?.X.ToString(); mLocY = m.Location?.Y.ToString(); mLocZ = m.Location?.Z.ToString(); }

    // ── wool CRUD ───────────────────────────────────────────────────────────────

    private async Task AddWool()
    {
        if (NextWoolColor() is not { } c) return;
        await Post("wools", new Dictionary<string, object?> { ["color"] = c.Value });
        await Reload();
        var added = wools.FirstOrDefault(w => GameColors.DyeColors.Any(d => d.Value == w.Color && d.Value == c.Value));
        if (added is not null) await SelectWool(added.Id);
    }

    private async Task SaveWool(Dictionary<string, object?> patch)
    {
        if (selWool is null) return;
        if (await Patch($"wools/{selWool}", patch)) await Reload();
    }

    private Task SaveWoolColor(ChangeEventArgs e) => SaveWool(new() { ["color"] = e.Value?.ToString() });
    private Task SaveWoolTeam(ChangeEventArgs e) => SaveWool(new() { ["team"] = Empty(e.Value) });
    private Task SaveWoolRoom(ChangeEventArgs e) => SaveWool(new() { ["wool_room_region"] = e.Value?.ToString()?.Trim() });
    private Monument? CurMon() => CurrentWool?.Monuments.FirstOrDefault(m => m.Id == selMon);
    private Task SaveMonTeam(ChangeEventArgs e) => CurrentWool is { } w && CurMon() is { } m ? SaveMonument(w, m, new() { ["team"] = Empty(e.Value) }) : Task.CompletedTask;
    private Task SaveMonRegion(ChangeEventArgs e) => CurrentWool is { } w && CurMon() is { } m ? SaveMonument(w, m, new() { ["monument_region"] = e.Value?.ToString()?.Trim() }) : Task.CompletedTask;

    private Task SaveWoolLocation() => SaveWool(new() { ["location"] = BuildLoc(wLocX, wLocY, wLocZ) });

    private async Task DeleteWool(Wool w) { if (await Delete($"wools/{w.Id}")) { selWool = null; await Reload(); } }

    // ── monuments ───────────────────────────────────────────────────────────────

    private async Task AddMonument(Wool w)
    {
        if (NextMonumentTeam(w) is not { } team) return;
        if (await Post($"wools/{w.Id}/monuments", new Dictionary<string, object?> { ["team"] = team.Id })) await Reload();
    }

    private async Task SaveMonument(Wool w, Monument m, Dictionary<string, object?> patch)
    {
        if (await Patch($"wools/{w.Id}/monuments/{m.Id}", patch)) await Reload();
    }

    private Task SaveMonumentLocation(Wool w, Monument m) => SaveMonument(w, m, new() { ["location"] = BuildLoc(mLocX, mLocY, mLocZ) });

    private async Task DeleteMonument(Wool w, Monument m) { if (await Delete($"wools/{w.Id}/monuments/{m.Id}")) { selMon = null; await Reload(); } }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static string DyeLabel(string? color) => GameColors.DyeColors.FirstOrDefault(c => c.Value == (color ?? "").Replace('_', ' ').ToLowerInvariant()).Label is { Length: > 0 } l ? l : (color ?? "");
    private string TeamColor(string? teamId) => teams.FirstOrDefault(t => t.Id == teamId) is { } t ? GameColors.ChatHex(t.Color) : "var(--border)";

    private GameColors.Color? NextWoolColor()
    {
        var used = wools.Select(w => (w.Color ?? "").Replace('_', ' ').ToLowerInvariant()).ToHashSet();
        foreach (var c in GameColors.DyeColors) if (!used.Contains(c.Value)) return c;
        return null;
    }

    private IEnumerable<GameColors.Color> AvailableColors(Wool wool)
    {
        var others = wools.Where(w => w.Id != wool.Id).Select(w => (w.Color ?? "").Replace('_', ' ').ToLowerInvariant()).ToHashSet();
        var cur = (wool.Color ?? "").Replace('_', ' ').ToLowerInvariant();
        return GameColors.DyeColors.Where(c => c.Value == cur || !others.Contains(c.Value));
    }

    private Team? NextMonumentTeam(Wool wool)
    {
        var used = wool.Monuments.Select(m => m.Team).ToHashSet();
        return teams.FirstOrDefault(t => !used.Contains(t.Id));
    }

    private static Dictionary<string, object?>? BuildLoc(string? x, string? y, string? z)
    {
        var hx = double.TryParse(x, out var dx); var hy = double.TryParse(y, out var dy); var hz = double.TryParse(z, out var dz);
        return (!hx && !hy && !hz) ? null : new() { ["x"] = hx ? dx : 0, ["y"] = hy ? dy : 0, ["z"] = hz ? dz : 0 };
    }

    private static string? Empty(object? v) => v?.ToString() is { Length: > 0 } s ? s : null;

    // Side-view slice: set a point/block region's Y (coords patch); Reload keeps the selection.
    private async Task SetRegionY(int y)
    {
        if (selRegion is null) return;
        if (await Patch($"regions/{selRegion}", new Dictionary<string, object?> { ["coords"] = new Dictionary<string, object?> { ["y"] = y } })) await Reload();
    }

    // Geometry editing (canvas drag-resize + inspector coord fields) — persist + keep canvas/inspector in sync.
    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        if (!nodeMap.TryGetValue(e.Id, out var node)) return;
        if (await RegionEdits.SetBoundsAsync(Http, Slug, node, e.MinX, e.MinZ, e.MaxX, e.MaxZ) is null && canvas is not null)
            await canvas.ReloadAsync();
        else StateHasChanged();
    }

    private async Task OnSetCoord((string Key, double Value) e)
    {
        if (RegionNodeSel is null) return;
        var nb = await RegionEdits.SetCoordAsync(Http, Slug, RegionNodeSel, e.Key, e.Value);
        if (nb is null) { error = "Edit rejected."; StateHasChanged(); return; }
        if (canvas is not null && nb.Count == 4) await canvas.RefreshRegionBoundsAsync(RegionNodeSel.Id, nb);
        StateHasChanged();
    }

    // ── http ────────────────────────────────────────────────────────────────────

    private async Task<bool> Post(string path, object body) => await Send(Http.PostAsJsonAsync($"api/map/{Slug}/{path}", body));
    private async Task<bool> Patch(string path, object body) => await Send(Http.PatchAsJsonAsync($"api/map/{Slug}/{path}", body));
    private async Task<bool> Delete(string path) => await Send(Http.DeleteAsync($"api/map/{Slug}/{path}"));
    private async Task<bool> Send(Task<HttpResponseMessage> call)
    {
        error = null;
        var resp = await call;
        if (resp.IsSuccessStatusCode) return true;
        try { var d = await resp.Content.ReadFromJsonAsync<JsonElement>(); error = d.TryGetProperty("error", out var e) ? e.GetString() : $"error {(int)resp.StatusCode}"; }
        catch { error = $"error {(int)resp.StatusCode}"; }
        StateHasChanged();
        return false;
    }

    // ── parse ─────────────────────────────────────────────────────────────────────

    private static string S(JsonElement e, string k, string def = "") => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? def : def;
    private static string? Opt(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static Loc? ParseLoc(JsonElement e, string k)
    {
        if (!e.TryGetProperty(k, out var v) || v.ValueKind != JsonValueKind.Object) return null;
        return new Loc { X = LD(v, "x"), Y = LD(v, "y"), Z = LD(v, "z") };
    }
    private static double LD(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
