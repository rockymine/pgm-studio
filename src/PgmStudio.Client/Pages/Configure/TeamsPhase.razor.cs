using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Pages.EditorActivities;
using PgmStudio.Geom;

namespace PgmStudio.Client.Pages.Configure;

// Teams · step 1 (N02, "Teams & island assignment"): create the teams (symmetry suggests the count) and
// tag islands to them. Writes the intent's teams + maxPlayers slice, plus the islandTeams authoring aid.
// The reused EditorCanvas runs in island-select mode (base layer only): selecting a team then clicking an
// island tints it that team's colour. Orbit-fill mirrors team 0 onto the rest from the confirmed symmetry.
public partial class TeamsPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed class Team { public string Id = ""; public string Name = ""; public string Color = ""; }
    private sealed record IslandInfo(int Id, int BlockCount);

    private readonly List<Team> teams = new();
    private int maxPlayers = 12;
    private string? selectedTeamId;
    private List<IslandInfo> islands = new();
    private readonly Dictionary<string, string> islandTeams = new();   // island id → team id
    private string? symMode;
    private bool dismissedSuggestion;
    private EditorCanvas? canvas;

    private string Slug => Wizard.Slug;
    private Team? Selected => teams.FirstOrDefault(t => t.Id == selectedTeamId);
    private static string Hex(string color) => GameColors.ChatHex(color);

    // Teams the symmetry suggests = its orbit order (rot_90 → 4, mirror/rot_180 → 2); no symmetry → no suggestion.
    private int SuggestedCount => Symmetry.Order(symMode) is var o && o > 1 ? o : 0;
    private bool ShowSuggestion => !dismissedSuggestion && teams.Count == 0 && SuggestedCount > 0;
    private IReadOnlyList<GameColors.Color> SuggestedColors => GameColors.FirstTeamColors(SuggestedCount);
    private int IslandsFor(string teamId) => islandTeams.Count(kv => kv.Value == teamId);

    protected override async Task OnInitializedAsync()
    {
        LoadFromIntent();
        symMode = (Wizard.Intent["symmetry"] as JsonObject)?["mode"]?.GetValue<string>();
        await LoadIslands();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private void LoadFromIntent()
    {
        teams.Clear();
        if (Wizard.Intent["teams"] is JsonArray arr)
            foreach (var t in arr.OfType<JsonObject>())
                teams.Add(new Team { Id = Str(t, "id"), Name = Str(t, "name"), Color = Str(t, "color") });
        if (Wizard.Intent["maxPlayers"] is JsonValue mv && mv.TryGetValue(out int mp)) maxPlayers = mp;
        islandTeams.Clear();
        if (Wizard.Intent["islandTeams"] is JsonObject it)
            foreach (var kv in it)
                if (kv.Value?.GetValue<string>() is { Length: > 0 } team) islandTeams[kv.Key] = team;
        selectedTeamId = teams.FirstOrDefault()?.Id;
    }

    private async Task LoadIslands()
    {
        try
        {
            var arr = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/islands");
            if (arr.ValueKind == JsonValueKind.Array)
                islands = arr.EnumerateArray()
                    .Select(e => new IslandInfo(e.GetProperty("id").GetInt32(), e.GetProperty("block_count").GetInt32()))
                    .OrderByDescending(i => i.BlockCount).ToList();
        }
        catch { islands = new(); }
    }

    private async Task OnCanvasReady() => await PaintIslands();

    private async Task PaintIslands()
    {
        if (canvas is null) return;
        var map = islandTeams.ToDictionary(
            kv => kv.Key,
            kv => Hex(teams.FirstOrDefault(t => t.Id == kv.Value)?.Color ?? ""));
        await canvas.SetIslandTeamsAsync(map);
    }

    // ── teams ────────────────────────────────────────────────────────────────
    private void CreateSuggestedTeams()
    {
        teams.Clear();
        foreach (var c in SuggestedColors)
            teams.Add(new Team { Id = c.Value.Replace(' ', '-') + "-team", Name = c.Label, Color = c.Value });
        selectedTeamId = teams.FirstOrDefault()?.Id;
        WriteTeams();
    }

    private void AddTeam()
    {
        if (GameColors.NextTeamColor(teams.Select(t => t.Color)) is not { } c) return;
        var team = new Team { Id = c.Value.Replace(' ', '-') + "-team", Name = c.Label, Color = c.Value };
        teams.Add(team);
        selectedTeamId = team.Id;
        WriteTeams();
    }

    private async Task RemoveTeam(Team t)
    {
        teams.Remove(t);
        foreach (var isl in islandTeams.Where(kv => kv.Value == t.Id).Select(kv => kv.Key).ToList())
            islandTeams.Remove(isl);
        if (selectedTeamId == t.Id) selectedTeamId = teams.FirstOrDefault()?.Id;
        WriteTeams(); WriteIslandTeams();
        await PaintIslands();
    }

    private void Select(string id) => selectedTeamId = id;

    private void SetName(ChangeEventArgs e) { if (Selected is { } t) { t.Name = e.Value?.ToString() ?? ""; WriteTeams(); } }

    private async Task SetColor(ChangeEventArgs e)
    {
        if (Selected is { } t) { t.Color = e.Value?.ToString() ?? t.Color; WriteTeams(); await PaintIslands(); }
    }

    private void SetMaxPlayers(double v) { maxPlayers = (int)v; WriteMaxPlayers(); }

    // ── island assignment ──────────────────────────────────────────────────────
    // Click an island with a team selected → toggle it onto that team (tinting it); clicking its own team
    // again clears it back to neutral.
    private async Task OnAssignIsland(int? islandId)
    {
        if (islandId is not { } id || selectedTeamId is null) return;
        var key = id.ToString();
        if (islandTeams.TryGetValue(key, out var cur) && cur == selectedTeamId) islandTeams.Remove(key);
        else islandTeams[key] = selectedTeamId;
        WriteIslandTeams();
        await PaintIslands();
    }

    // ── intent writers ──────────────────────────────────────────────────────────
    private void WriteTeams()
    {
        Wizard.Intent["teams"] = new JsonArray(teams.Select(t =>
            (JsonNode)new JsonObject { ["id"] = t.Id, ["name"] = t.Name, ["color"] = t.Color }).ToArray());
        Wizard.Intent["maxPlayers"] = maxPlayers;
        Wizard.MarkDirty();
    }

    private void WriteMaxPlayers() { Wizard.Intent["maxPlayers"] = maxPlayers; Wizard.MarkDirty(); }

    private void WriteIslandTeams()
    {
        var o = new JsonObject();
        foreach (var (isl, team) in islandTeams) o[isl] = team;
        Wizard.Intent["islandTeams"] = o;
        Wizard.MarkDirty();
    }

    private static string Str(JsonObject o, string key) => o[key]?.GetValue<string>() ?? "";
}
