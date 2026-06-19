using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Pages.EditorActivities;

namespace PgmStudio.Client.Pages.Configure;

// Teams · protection step: draw a rectangle over a spawn — it's owned by that spawn's team, and the
// confirmed symmetry orbits it onto the other teams, each copy owned by the team whose spawn IT covers
// (shared point-aware OrbitAssignment, never default orbit order — so no spawn lands in an enemy's zone).
// The authored zone is editable; the orbit copies are non-editable ghost previews. Writes each spawn's
// `protection` slice; the generator builds the rectangle + wires the enter/block filters on it.
public partial class ProtectionPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed class Team { public string Id = ""; public string Name = ""; public string Color = ""; }
    private sealed class Spawn { public string Team = ""; public double X, Y, Z; }
    private record struct Rect(double MinX, double MinZ, double MaxX, double MaxZ);

    private readonly List<Team> teams = new();
    private string? symMode; private double symCx, symCz;
    private readonly List<Spawn> spawns = new();
    private readonly Dictionary<string, Rect> protection = new();   // team id → protection rect
    private string? authoredTeam;   // the team whose spawn the editable (parent) zone covers; the orbit derives from it
    private string? selectedTeamId;
    private EditorCanvas? canvas;

    private string Slug => Wizard.Slug;
    private Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string Hex(string teamId) => GameColors.ChatHex(TeamOf(teamId)?.Color ?? "");
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;
    private string? AuthoredTeam => authoredTeam;   // read-only alias the inspector/markup reads

    protected override void OnInitialized() => LoadFromIntent();

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private void LoadFromIntent()
    {
        teams.Clear();
        if (Wizard.Intent["teams"] is JsonArray arr)
            foreach (var t in arr.OfType<JsonObject>())
                teams.Add(new Team { Id = S(t, "id"), Name = S(t, "name"), Color = S(t, "color") });
        if (Wizard.Intent["symmetry"] is JsonObject sym)
        {
            symMode = sym["mode"]?.GetValue<string>();
            symCx = D(sym, "centerX"); symCz = D(sym, "centerZ");
        }
        spawns.Clear(); protection.Clear();
        if (Wizard.Intent["spawns"] is JsonArray sp)
            foreach (var s in sp.OfType<JsonObject>())
            {
                var pt = s["point"] as JsonObject;
                var team = S(s, "team");
                spawns.Add(new Spawn { Team = team, X = D(pt, "x"), Y = D(pt, "y"), Z = D(pt, "z") });
                if (s["protection"] is JsonObject pr)
                    protection[team] = new Rect(D(pr, "minX"), D(pr, "minZ"), D(pr, "maxX"), D(pr, "maxZ"));
            }
        // On reload the editable parent is the first spawn that has a zone — any one re-derives the same
        // symmetric set, so the choice is cosmetic; null when nothing is drawn yet.
        authoredTeam = spawns.FirstOrDefault(s => protection.ContainsKey(s.Team))?.Team;
        selectedTeamId = authoredTeam ?? spawns.FirstOrDefault()?.Team ?? teams.FirstOrDefault()?.Id;
    }

    private Task OnCanvasReady() => PaintProtection();

    // The drawn rectangle is owned by the team whose spawn it covers; the symmetry fills the rest.
    private async Task OnRectDrawn((double MinX, double MinZ, double MaxX, double MaxZ) r)
    {
        if (!ApplyAuthoredZone((r.MinX, r.MinZ, r.MaxX, r.MaxZ))) return;
        await PaintProtection();
        if (authoredTeam is not null) await SelectOnCanvas(authoredTeam);
    }

    // Resizing the authored zone re-derives the orbit; orbit copies are ghosts and never fire this.
    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        if (TeamFromRegionId(e.Id) != authoredTeam) return;
        if (!ApplyAuthoredZone((e.MinX, e.MinZ, e.MaxX, e.MaxZ))) return;
        await PaintProtection();
        if (authoredTeam is not null) await SelectOnCanvas(authoredTeam);
    }

    // Shared point-aware assignment: orbit the drawn rect and key each copy by the spawn it covers. Leaves
    // state untouched (returns false) when the rect wraps no spawn — a protection zone must enclose one.
    private bool ApplyAuthoredZone((double MinX, double MinZ, double MaxX, double MaxZ) rect)
    {
        var parent = spawns.FirstOrDefault(s => Covers(rect, s.X, s.Z))?.Team;
        if (parent is null) return false;
        var anchors = spawns.Select(s => new OrbitAssignment.Anchor(s.Team, s.X, s.Z)).ToList();
        var zones = OrbitAssignment.ByCoveredAnchor(rect, symMode, symCx, symCz, anchors);
        protection.Clear();
        foreach (var z in zones) protection[z.Id] = new Rect(z.MinX, z.MinZ, z.MaxX, z.MaxZ);
        authoredTeam = parent;
        selectedTeamId = parent;
        WriteProtection();
        return true;
    }

    private static bool Covers((double MinX, double MinZ, double MaxX, double MaxZ) r, double x, double z)
        => x >= r.MinX && x <= r.MaxX && z >= r.MinZ && z <= r.MaxZ;

    // Canvas click → select the picked zone (echo selection back so its handles render), or deselect on a miss.
    private async Task OnCanvasSelect(string? id)
    {
        var team = id is null ? null : TeamFromRegionId(id);
        selectedTeamId = team;
        if (team is not null) await SelectOnCanvas(team);
        else { if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>()); StateHasChanged(); }
    }

    // Only the authored zone shows handles; orbit copies are ghosts (the canvas ignores them for anchors).
    private async Task SelectOnCanvas(string team)
    {
        if (canvas is not null)
            await canvas.SetSelectionAsync(protection.ContainsKey(team) ? new[] { RegionId(team) } : Array.Empty<string>());
        StateHasChanged();
    }

    private async Task SelectTeam(string id) { selectedTeamId = id; await SelectOnCanvas(id); }

    private async Task ClearProtection(string team)
    {
        protection.Clear();   // the whole set derives from the authored zone — clearing it removes all
        authoredTeam = null;
        WriteProtection();
        await PaintProtection();
    }

    private static string RegionId(string team) => $"{team}-spawn-protect";
    // A click resolves to a team via the protection rect ("{team}-spawn-protect") or the spawn marker
    // ("{team}-spawn") sitting inside it — either selects that team's protection.
    private string? TeamFromRegionId(string? id)
    {
        if (id is null) return null;
        foreach (var suffix in new[] { "-spawn-protect", "-spawn" })
            if (id.EndsWith(suffix))
            {
                var team = id[..^suffix.Length];
                if (teams.Any(t => t.Id == team)) return team;
            }
        return null;
    }

    private void WriteProtection()
    {
        Wizard.Intent["spawns"] = new JsonArray(spawns.Select(s => (JsonNode)new JsonObject
        {
            ["team"] = s.Team,
            ["point"] = new JsonObject { ["x"] = s.X, ["y"] = s.Y, ["z"] = s.Z },
            ["protection"] = protection.TryGetValue(s.Team, out var r)
                ? new JsonObject { ["minX"] = r.MinX, ["minZ"] = r.MinZ, ["maxX"] = r.MaxX, ["maxZ"] = r.MaxZ }
                : null,
            ["yaw"] = SpawnYaw(s.Team),
        }).ToArray());
        Wizard.MarkDirty();
    }

    // Preserve the yaw the spawn step wrote (this step only edits protection).
    private double SpawnYaw(string team)
    {
        if (Wizard.Intent["spawns"] is JsonArray sp)
            foreach (var s in sp.OfType<JsonObject>())
                if (S(s, "team") == team) return D(s, "yaw");
        return 0;
    }

    // One author-region set: each spawn as a point marker (the authored team's brighter) + each team's zone
    // (authored editable, orbit copies non-editable ghosts).
    private async Task PaintProtection()
    {
        if (canvas is null) return;
        var markers = spawns.Select(s => (object)new
        {
            id = $"{s.Team}-spawn",
            type = "point",
            marker = true,
            primary = s.Team == authoredTeam,
            color = Hex(s.Team),
            label = $"{TeamName(s.Team)} spawn",
            bounds = new { min_x = s.X - 0.5, min_z = s.Z - 0.5, max_x = s.X + 0.5, max_z = s.Z + 0.5 },
        });
        var zones = protection.Select(kv => (object)new
        {
            id = RegionId(kv.Key),
            type = "rectangle",
            label = $"{TeamName(kv.Key)} spawn protection",
            color = Hex(kv.Key),
            ghost = kv.Key != authoredTeam,   // orbit copies are derived previews — non-editable
            bounds = new { min_x = kv.Value.MinX, min_z = kv.Value.MinZ, max_x = kv.Value.MaxX, max_z = kv.Value.MaxZ },
        });
        await canvas.SetAuthorRegionsAsync(markers.Concat(zones));
    }

    private static string S(JsonObject? o, string k) => o?[k]?.GetValue<string>() ?? "";
    private static double D(JsonObject? o, string k)
    {
        if (o?[k] is JsonValue v) { if (v.TryGetValue(out double d)) return d; if (v.TryGetValue(out int i)) return i; }
        return 0;
    }
}
