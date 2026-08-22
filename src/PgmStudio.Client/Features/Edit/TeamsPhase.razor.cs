using PgmStudio.Contracts;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Models;

using PgmStudio.Client.Components;

namespace PgmStudio.Client.Features.Edit;

public partial class TeamsPhase
{
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public bool IsFirstPhase { get; set; }
    [Parameter] public bool IsLastPhase { get; set; }
    [Parameter] public EventCallback OnPrevPhase { get; set; }
    [Parameter] public EventCallback OnNextPhase { get; set; }

    private WorldCanvas? canvas;
    private readonly List<Team> teams = new();
    private readonly List<Spawn> spawns = new();
    private ObserverSpawn? observer;
    private readonly List<RegionNode> spawnRegions = new();
    private readonly List<RegionNode> draftRegions = new();   // drawn in this step, not yet wired (E10)
    private readonly Dictionary<string, RegionNode> nodeMap = new();

    private string? selTeam;
    private string? selSpawn;
    private string? error;

    // intelligent team-setup suggestion (driven by the map's detected symmetry)
    private string? symMode;             // primary symmetry type, e.g. "rot_90"; null = none detected
    private bool suggestionDismissed;    // user rejected the suggestion this session
    private bool suggestionBusy;         // accept in flight

    // spawn-assignment form state
    private string spawnTeam = "";
    private double spawnYaw;
    private string spawnKit = "";

    // spawn regions split by subtype (C-series classification improvement): the actual spawn points
    // (where players spawn, from spawns[].region) vs the surrounding anti-grief protection zones.
    private IEnumerable<RegionNode> SpawnPoints => spawnRegions.Where(r => r.Subtype == "point");
    private IEnumerable<RegionNode> SpawnProtection => spawnRegions.Where(r => r.Subtype == "protection");

    private Team? CurrentTeam => teams.FirstOrDefault(t => t.Id == selTeam);
    private RegionNode? SpawnNode => selSpawn is not null ? nodeMap.GetValueOrDefault(selSpawn) : null;

    private sealed class Team { public string Id = ""; public string Name = ""; public string Color = "red"; public string? DyeColor; public int MaxPlayers = 20; public int MinPlayers = 0; }
    private sealed class Spawn { public string RegionId = ""; public string Team = ""; public double Yaw; public string Kit = ""; }
    private sealed class ObserverSpawn { public string RegionId = ""; public double Yaw; public string Kit = ""; }

    protected override async Task OnParametersSetAsync() => await Reload();

    private async Task Reload()
    {
        teams.Clear(); spawns.Clear(); spawnRegions.Clear(); draftRegions.Clear(); nodeMap.Clear(); observer = null;
        try
        {
            var doc = await Http.GetFromJsonAsync<MapDocumentDto>($"api/map/{Slug}");
            foreach (var t in doc?.Teams ?? [])
                teams.Add(new Team
                {
                    Id = t.Id, Name = t.Name ?? "", Color = t.Color ?? "red", DyeColor = t.DyeColor,
                    MaxPlayers = t.MaxPlayers ?? 20, MinPlayers = t.MinPlayers ?? 0,
                });
            foreach (var s in doc?.Spawns ?? [])
                spawns.Add(new Spawn { RegionId = RegionId(s.Region), Team = s.Team ?? "", Yaw = s.Yaw ?? 0, Kit = s.Kit ?? "" });
            if (doc?.ObserverSpawn is { } ob)
                observer = new ObserverSpawn { RegionId = RegionId(ob.Region), Yaw = ob.Yaw ?? 0, Kit = ob.Kit ?? "" };

            var tree = await Http.GetFromJsonAsync<RegionTreeDto>($"api/map/{Slug}/regions/tree");
            foreach (var grp in RegionGroup.From(tree))
                foreach (var n in grp.Regions) CollectSpawn(n);

            symMode = await DetectedSymmetryAsync();
        }
        catch (Exception ex) { error = ex.Message; }
        StateHasChanged();
    }

    /// <summary>The map's detected primary symmetry mode (null if none/rejected or no scan).</summary>
    private async Task<string?> DetectedSymmetryAsync()
    {
        try
        {
            var sym = await Http.GetFromJsonAsync<SymmetryDto>($"api/map/{Slug}/symmetry");
            return sym?.Primary?.Type;
        }
        catch { return null; }   // no islands/symmetry artifact → no suggestion
    }

    // ── intelligent team suggestion ───────────────────────────────────────────────

    private sealed record Suggested(string Color, string Name, string Slug);

    /// <summary>Teams implied by the detected symmetry: rot_90 → 4 (one per quadrant), else → 2.</summary>
    private List<Suggested> SuggestedTeams()
    {
        var colors = symMode == "rot_90"
            ? new[] { "red", "blue", "green", "yellow" }
            : new[] { "red", "blue" };
        return colors.Select(c => new Suggested(c, $"{char.ToUpperInvariant(c[0])}{c[1..]} Team", $"{c}-team")).ToList();
    }

    private string AcceptLabel() => $"Create {SuggestedTeams().Count} teams";

    /// <summary>Short header badge for the detected symmetry, e.g. "rot 90" / "mirror x".</summary>
    private string SymBadge() => (symMode ?? "").Replace('_', ' ');

    private string SuggestionText() => symMode switch
    {
        "rot_90"   => "90° rotational symmetry suggests four teams.",
        "rot_180"  => "180° rotational symmetry suggests two teams.",
        "mirror_x" => "Mirror symmetry across X suggests two teams.",
        "mirror_z" => "Mirror symmetry across Z suggests two teams.",
        "mirror_d1" or "mirror_d2" => "Diagonal mirror symmetry suggests two teams.",
        _ => "A symmetric layout suggests these teams.",
    };

    private async Task AcceptSuggestion()
    {
        suggestionBusy = true; StateHasChanged();
        foreach (var s in SuggestedTeams())
            await Ran(MapEdits.AddTeam(Http, Slug, new Dictionary<string, object?>
            {
                ["id"] = s.Slug, ["name"] = s.Name, ["color"] = s.Color, ["max_players"] = 20, ["min_players"] = 0,
            }));
        suggestionBusy = false;
        await Reload();   // teams now non-empty → suggestion hides itself
    }

    private void RejectSuggestion() => suggestionDismissed = true;

    /// <summary>Walk the whole tree: index every node, and collect spawn-family regions by subtype.
    /// Protection zones nest under the <c>spawns</c> rule-container (in the "other" group), so we can't
    /// rely on spawn-group roots; synthetic compound wrappers are skipped from the protection list.</summary>
    private void CollectSpawn(RegionNode n)
    {
        if (!string.IsNullOrEmpty(n.Id)) nodeMap.TryAdd(n.Id, n);
        if (n.Subtype == "point" || (n.Subtype == "protection" && !IsSyntheticId(n.Id)))
            spawnRegions.Add(n);
        else if (n.DraftStep == "teams" && n.Category == "other")   // drawn here, not yet wired (E10)
            draftRegions.Add(n);
        foreach (var c in n.Children) CollectSpawn(c);
        if (n.Source is not null) CollectSpawn(n.Source);
    }

    // generated compound ids (the spawns union's anonymous intermediates) — structure, not authored zones
    private static bool IsSyntheticId(string id) => id.Length == 0 || id.Contains("__anon_") || id.Contains("__apply_");

    private Spawn? SpawnFor(string regionId) => spawns.FirstOrDefault(s => s.RegionId == regionId);

    // ── selection ──────────────────────────────────────────────────────────────

    private async Task SelectTeam(string id)
    {
        selTeam = id; selSpawn = null;
        if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>());
        StateHasChanged();
    }

    private async Task SelectSpawn(string regionId)
    {
        selSpawn = regionId; selTeam = null;
        var sp = SpawnFor(regionId);
        var isObs = observer?.RegionId == regionId;
        spawnTeam = isObs ? "__observer__" : sp?.Team ?? "";
        spawnYaw = isObs ? observer!.Yaw : sp?.Yaw ?? 0;
        spawnKit = isObs ? observer!.Kit : sp?.Kit ?? "";
        if (canvas is not null) await canvas.SetSelectionAsync(new[] { regionId });
        StateHasChanged();
    }

    private Task OnCanvasSelect(string? id) => id is null ? Deselect() : SelectSpawn(id);

    private async Task Deselect()
    {
        selTeam = null; selSpawn = null;
        if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>());
        StateHasChanged();
    }

    // ── team CRUD ───────────────────────────────────────────────────────────────

    private async Task AddTeam()
    {
        var pick = GameColors.NextTeamColor(teams.Select(t => t.Color));
        var (color, name, baseId) = pick is { } p
            ? (p.Value, $"{p.Label} Team", $"{p.Value.Replace(' ', '-')}-team")
            : ("blue", "New Team", "new-team");
        var used = teams.Select(t => t.Id).ToHashSet();
        var slug = baseId; var n = 2;
        while (used.Contains(slug)) slug = $"{baseId}-{n++}";
        await Ran(MapEdits.AddTeam(Http, Slug, new Dictionary<string, object?>
        {
            ["id"] = slug, ["name"] = name, ["color"] = color, ["max_players"] = 20, ["min_players"] = 0,
        }));
        await Reload();
        await SelectTeam(slug);
    }

    private async Task SaveTeam(Team t)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = string.IsNullOrWhiteSpace(t.Name) ? t.Id : t.Name,
            ["color"] = t.Color,
            ["dye_color"] = string.IsNullOrEmpty(t.DyeColor) ? null : t.DyeColor,
            ["max_players"] = t.MaxPlayers,
            ["min_players"] = t.MinPlayers,
        };
        await Ran(MapEdits.PatchTeam(Http, Slug, t.Id, payload));
    }

    private async Task RenameTeam(Team t, string? raw)
    {
        var newId = (raw ?? "").Trim();
        if (newId.Length == 0 || newId == t.Id) return;
        if (teams.Any(x => x.Id == newId)) { error = $"Team ID \"{newId}\" is already in use."; StateHasChanged(); return; }
        var payload = new Dictionary<string, object?> { ["id"] = newId, ["name"] = t.Name, ["color"] = t.Color, ["dye_color"] = string.IsNullOrEmpty(t.DyeColor) ? null : t.DyeColor, ["max_players"] = t.MaxPlayers, ["min_players"] = t.MinPlayers };
        if (await Ran(MapEdits.PatchTeam(Http, Slug, t.Id, payload))) { await Reload(); await SelectTeam(newId); }
    }

    private async Task DeleteTeam(Team t)
    {
        if (await Ran(MapEdits.DeleteTeam(Http, Slug, t.Id))) { selTeam = null; await Reload(); }
    }

    // ── spawn assignment ────────────────────────────────────────────────────────

    private async Task SaveSpawn()
    {
        if (selSpawn is null) return;
        var id = selSpawn;
        var existing = SpawnFor(id);
        var wasObs = observer?.RegionId == id;
        if (spawnTeam == "__observer__")
        {
            if (existing is not null) await Ran(MapEdits.DeleteSpawn(Http, Slug, id));
            await Ran(MapEdits.SetObserverSpawn(Http, Slug,
                new Dictionary<string, object?> { ["region_id"] = id, ["yaw"] = spawnYaw, ["kit"] = spawnKit }));
        }
        else if (spawnTeam.Length > 0)
        {
            if (wasObs) await Ran(MapEdits.DeleteObserverSpawn(Http, Slug));
            if (existing is not null)
                await Ran(MapEdits.PatchSpawn(Http, Slug, id,
                    new Dictionary<string, object?> { ["team"] = spawnTeam, ["yaw"] = spawnYaw, ["kit"] = spawnKit }));
            else
                await Ran(MapEdits.AddSpawn(Http, Slug, new Dictionary<string, object?>
                {
                    ["region_id"] = id, ["team"] = spawnTeam, ["yaw"] = spawnYaw, ["kit"] = spawnKit,
                }));
        }
        await Reload();
        await SelectSpawn(id);
    }

    private async Task UnlinkSpawn()
    {
        if (selSpawn is null) return;
        var id = selSpawn;
        if (observer?.RegionId == id) await Ran(MapEdits.DeleteObserverSpawn(Http, Slug));
        else await Ran(MapEdits.DeleteSpawn(Http, Slug, id));
        await Reload();
        await SelectSpawn(id);
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
        if (SpawnNode is null) return;
        var nb = await RegionEdits.SetCoordAsync(Http, Slug, SpawnNode, e.Key, e.Value);
        if (nb is null) { error = "Edit rejected."; StateHasChanged(); return; }
        if (canvas is not null && nb.Count == 4) await canvas.RefreshRegionBoundsAsync(SpawnNode.Id, nb);
        StateHasChanged();
    }

    // ── http helpers ────────────────────────────────────────────────────────────

    /// <summary>Run one edit and keep its refusal on screen. The route and the sentence are
    /// <see cref="MapEdits"/>'s; where the sentence goes is this phase's.</summary>
    private async Task<bool> Ran(Task<HttpResponseMessage> call)
    {
        error = await MapEdits.RefusedAsync(call);
        if (error is null) return true;
        StateHasChanged();
        return false;
    }

    // ── parse helpers ────────────────────────────────────────────────────────────

    /// <summary>A spawn's region is a key where it names one and a whole inline region where it states one,
    /// which is the contract's own choice and the reason the record leaves that one field open.</summary>
    private static string RegionId(JsonElement region)
        => region.ValueKind == JsonValueKind.String ? region.GetString() ?? ""
           : region.ValueKind == JsonValueKind.Object && region.TryGetProperty("id", out var id) ? id.GetString() ?? ""
           : "";

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
