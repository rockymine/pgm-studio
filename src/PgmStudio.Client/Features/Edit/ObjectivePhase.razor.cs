using PgmStudio.Contracts;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Models;

using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Edit;

public partial class ObjectivePhase
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public bool IsFirstPhase { get; set; }
    [Parameter] public bool IsLastPhase { get; set; }
    [Parameter] public EventCallback OnPrevPhase { get; set; }
    [Parameter] public EventCallback OnNextPhase { get; set; }

    private WorldCanvas? canvas;
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
            var doc = await Http.GetFromJsonAsync<MapDocumentDto>($"api/map/{Slug}");
            foreach (var t in doc?.Teams ?? [])
                teams.Add(new Team { Id = t.Id, Name = t.Name ?? "", Color = t.Color ?? "red" });
            foreach (var w in doc?.Wools ?? [])
            {
                var wool = new Wool
                {
                    Id = w.Id, Color = w.Color ?? "",
                    Team = w.Team, Location = ParseLoc(w.Location), WoolRoomRegion = w.WoolRoomRegion,
                };
                foreach (var m in w.Monuments)
                    wool.Monuments.Add(new Monument
                    {
                        Id = m.Id, Team = m.Team, Location = ParseLoc(m.Location), MonumentRegion = m.MonumentRegion,
                    });
                wools.Add(wool);
            }

            groups = CollectWoolGroups(RegionGroup.From(
                await Http.GetFromJsonAsync<RegionTreeDto>($"api/map/{Slug}/regions/tree")));
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
        await Ran(MapEdits.AddWool(Http, Slug, new Dictionary<string, object?> { ["color"] = c.Value }));
        await Reload();
        var added = wools.FirstOrDefault(w => GameColors.DyeColors.Any(d => d.Value == w.Color && d.Value == c.Value));
        if (added is not null) await SelectWool(added.Id);
    }

    private async Task SaveWool(Dictionary<string, object?> patch)
    {
        if (selWool is null) return;
        if (await Ran(MapEdits.PatchWool(Http, Slug, selWool, patch))) await Reload();
    }

    private Task SaveWoolColor(ChangeEventArgs e) => SaveWool(new() { ["color"] = e.Value?.ToString() });
    private Task SaveWoolTeam(ChangeEventArgs e) => SaveWool(new() { ["team"] = Empty(e.Value) });
    private Task SaveWoolRoom(ChangeEventArgs e) => SaveWool(new() { ["wool_room_region"] = e.Value?.ToString()?.Trim() });
    private Monument? CurMon() => CurrentWool?.Monuments.FirstOrDefault(m => m.Id == selMon);
    private Task SaveMonTeam(ChangeEventArgs e) => CurrentWool is { } w && CurMon() is { } m ? SaveMonument(w, m, new() { ["team"] = Empty(e.Value) }) : Task.CompletedTask;
    private Task SaveMonRegion(ChangeEventArgs e) => CurrentWool is { } w && CurMon() is { } m ? SaveMonument(w, m, new() { ["monument_region"] = e.Value?.ToString()?.Trim() }) : Task.CompletedTask;

    private Task SaveWoolLocation() => SaveWool(new() { ["location"] = BuildLoc(wLocX, wLocY, wLocZ) });

    private async Task DeleteWool(Wool w) { if (await Ran(MapEdits.DeleteWool(Http, Slug, w.Id))) { selWool = null; await Reload(); } }

    // ── monuments ───────────────────────────────────────────────────────────────

    private async Task AddMonument(Wool w)
    {
        if (NextMonumentTeam(w) is not { } team) return;
        if (await Ran(MapEdits.AddMonument(Http, Slug, w.Id, new Dictionary<string, object?> { ["team"] = team.Id })))
            await Reload();
    }

    private async Task SaveMonument(Wool w, Monument m, Dictionary<string, object?> patch)
    {
        if (await Ran(MapEdits.PatchMonument(Http, Slug, w.Id, m.Id, patch))) await Reload();
    }

    private Task SaveMonumentLocation(Wool w, Monument m) => SaveMonument(w, m, new() { ["location"] = BuildLoc(mLocX, mLocY, mLocZ) });

    private async Task DeleteMonument(Wool w, Monument m)
    {
        if (await Ran(MapEdits.DeleteMonument(Http, Slug, w.Id, m.Id))) { selMon = null; await Reload(); }
    }

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
        if (await Ran(MapEdits.PatchRegion(Http, Slug, selRegion,
                new Dictionary<string, object?> { ["coords"] = new Dictionary<string, object?> { ["y"] = y } })))
            await Reload();
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

    /// <summary>Run one edit and keep its refusal on screen. The route and the sentence are
    /// <see cref="MapEdits"/>'s; where the sentence goes is this phase's.</summary>
    private async Task<bool> Ran(Task<HttpResponseMessage> call)
    {
        error = await MapEdits.RefusedAsync(call);
        if (error is null) return true;
        StateHasChanged();
        return false;
    }

    // ── parse ─────────────────────────────────────────────────────────────────────

    /// <summary>A wool or monument states where it stands as an <c>{x, y, z}</c> object or not at all, which
    /// is the contract's own choice and the reason the record leaves that field open.</summary>
    private static Loc? ParseLoc(JsonElement location)
        => location.ValueKind != JsonValueKind.Object ? null
           : new Loc { X = LD(location, "x"), Y = LD(location, "y"), Z = LD(location, "z") };
    private static double LD(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
