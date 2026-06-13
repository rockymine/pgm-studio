using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Pages.EditorActivities;

public partial class TeamsActivity
{
    [Parameter] public string Slug { get; set; } = "";

    private EditorCanvas? canvas;
    private readonly List<Team> teams = new();
    private readonly List<Spawn> spawns = new();
    private ObserverSpawn? observer;
    private readonly List<RegionNode> spawnRegions = new();
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

    private Team? CurrentTeam => teams.FirstOrDefault(t => t.Id == selTeam);
    private RegionNode? SpawnNode => selSpawn is not null ? nodeMap.GetValueOrDefault(selSpawn) : null;

    private sealed class Team { public string Id = ""; public string Name = ""; public string Color = "red"; public string? DyeColor; public int MaxPlayers = 20; public int MinPlayers = 0; }
    private sealed class Spawn { public string RegionId = ""; public string Team = ""; public double Yaw; public string Kit = ""; }
    private sealed class ObserverSpawn { public string RegionId = ""; public double Yaw; public string Kit = ""; }

    protected override async Task OnParametersSetAsync() => await Reload();

    private async Task Reload()
    {
        teams.Clear(); spawns.Clear(); spawnRegions.Clear(); nodeMap.Clear(); observer = null;
        try
        {
            var doc = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}");
            if (doc.TryGetProperty("teams", out var ts) && ts.ValueKind == JsonValueKind.Array)
                foreach (var t in ts.EnumerateArray())
                    teams.Add(new Team
                    {
                        Id = S(t, "id"), Name = S(t, "name"), Color = S(t, "color", "red"),
                        DyeColor = t.TryGetProperty("dye_color", out var dv) && dv.ValueKind == JsonValueKind.String ? dv.GetString() : null,
                        MaxPlayers = I(t, "max_players", 20), MinPlayers = I(t, "min_players", 0),
                    });
            if (doc.TryGetProperty("spawns", out var sp) && sp.ValueKind == JsonValueKind.Array)
                foreach (var s in sp.EnumerateArray())
                    spawns.Add(new Spawn { RegionId = RegionId(s), Team = S(s, "team"), Yaw = D(s, "yaw"), Kit = S(s, "kit") });
            if (doc.TryGetProperty("observer_spawn", out var ob) && ob.ValueKind == JsonValueKind.Object)
                observer = new ObserverSpawn { RegionId = RegionId(ob), Yaw = D(ob, "yaw"), Kit = S(ob, "kit") };

            var tree = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/regions/tree");
            if (tree.TryGetProperty("groups", out var g))
                foreach (var grp in RegionGroup.ParseGroups(g).Where(x => x.Name == "spawn"))
                    foreach (var n in grp.Regions) { spawnRegions.Add(n); Index(n); }

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
            var sym = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/symmetry");
            return sym.TryGetProperty("primary", out var pr) && pr.ValueKind == JsonValueKind.Object
                && pr.TryGetProperty("type", out var ty) ? ty.GetString() : null;
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
            await Post("teams", new Dictionary<string, object?> { ["id"] = s.Slug, ["name"] = s.Name, ["color"] = s.Color, ["max_players"] = 20, ["min_players"] = 0 });
        suggestionBusy = false;
        await Reload();   // teams now non-empty → suggestion hides itself
    }

    private void RejectSuggestion() => suggestionDismissed = true;

    private void Index(RegionNode n)
    {
        if (!string.IsNullOrEmpty(n.Id)) nodeMap.TryAdd(n.Id, n);
        foreach (var c in n.Children) Index(c);
        if (n.Source is not null) Index(n.Source);
    }

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
        await Post("teams", new Dictionary<string, object?> { ["id"] = slug, ["name"] = name, ["color"] = color, ["max_players"] = 20, ["min_players"] = 0 });
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
        await Patch($"teams/{t.Id}", payload);
    }

    private async Task RenameTeam(Team t, string? raw)
    {
        var newId = (raw ?? "").Trim();
        if (newId.Length == 0 || newId == t.Id) return;
        if (teams.Any(x => x.Id == newId)) { error = $"Team ID \"{newId}\" is already in use."; StateHasChanged(); return; }
        var payload = new Dictionary<string, object?> { ["id"] = newId, ["name"] = t.Name, ["color"] = t.Color, ["dye_color"] = string.IsNullOrEmpty(t.DyeColor) ? null : t.DyeColor, ["max_players"] = t.MaxPlayers, ["min_players"] = t.MinPlayers };
        if (await Patch($"teams/{t.Id}", payload)) { await Reload(); await SelectTeam(newId); }
    }

    private async Task DeleteTeam(Team t)
    {
        if (await Delete($"teams/{t.Id}")) { selTeam = null; await Reload(); }
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
            if (existing is not null) await Delete($"spawns/{id}");
            await Patch("observer-spawn", new Dictionary<string, object?> { ["region_id"] = id, ["yaw"] = spawnYaw, ["kit"] = spawnKit });
        }
        else if (spawnTeam.Length > 0)
        {
            if (wasObs) await Delete("observer-spawn");
            if (existing is not null) await Patch($"spawns/{id}", new Dictionary<string, object?> { ["team"] = spawnTeam, ["yaw"] = spawnYaw, ["kit"] = spawnKit });
            else await Post("spawns", new Dictionary<string, object?> { ["region_id"] = id, ["team"] = spawnTeam, ["yaw"] = spawnYaw, ["kit"] = spawnKit });
        }
        await Reload();
        await SelectSpawn(id);
    }

    private async Task UnlinkSpawn()
    {
        if (selSpawn is null) return;
        var id = selSpawn;
        if (observer?.RegionId == id) await Delete("observer-spawn");
        else await Delete($"spawns/{id}");
        await Reload();
        await SelectSpawn(id);
    }

    // ── http helpers ────────────────────────────────────────────────────────────

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

    // ── parse helpers ────────────────────────────────────────────────────────────

    private static string S(JsonElement e, string k, string def = "") => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? def : def;
    private static int I(JsonElement e, string k, int def) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;
    private static double D(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
    private static string RegionId(JsonElement s)
    {
        if (!s.TryGetProperty("region", out var r)) return "";
        return r.ValueKind == JsonValueKind.String ? r.GetString() ?? "" : r.ValueKind == JsonValueKind.Object && r.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
    }
    private static int ToInt(object? v, int def) => int.TryParse(v?.ToString(), out var n) ? n : def;
    private static double ToDouble(object? v) => double.TryParse(v?.ToString(), out var n) ? n : 0;

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");
}
