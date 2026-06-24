using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Pages.EditorActivities;

namespace PgmStudio.Client.Pages.Configure;

// Teams · protection step: draw the rectangle(s) over a spawn — a protection zone is a union of rectangles.
// The first rect over a spawn selects that team; further rects while it's selected ADD to its zone;
// selecting a rect + deleting removes one (nothing is touch-merged or auto-resized). The confirmed symmetry
// orbits the authored zone onto the other teams, each copy owned by the team whose spawn its primary rect
// covers (shared point-aware OrbitAssignment, never default orbit order — so no spawn lands in an enemy's
// zone). The authored zone's rects are editable; the orbit copies are non-editable ghost previews. Writes
// each spawn's `protection` slice; the generator unions the rects + wires the enter/block filters.
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
    private readonly Dictionary<string, List<Rect>> protection = new();   // team id → protection rects (union)
    private string? authoredTeam;   // the team whose spawn the editable (parent) zone covers; the orbit derives from it
    private string? selectedTeamId;
    private EditorCanvas? canvas;

    private string Slug => Wizard.Slug;
    private Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string Hex(string teamId) => GameColors.ChatHex(TeamOf(teamId)?.Color ?? "");
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;
    private string? AuthoredTeam => authoredTeam;   // read-only alias the inspector/markup reads
    private IReadOnlyList<Rect> ZoneOf(string team) => protection.TryGetValue(team, out var r) ? r : Array.Empty<Rect>();

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
                var rects = ParseRects(s["protection"]).ToList();
                if (rects.Count > 0) protection[team] = rects;
            }
        // On reload the editable parent is the first spawn that has a zone — any one re-derives the same
        // symmetric set, so the choice is cosmetic; null when nothing is drawn yet.
        authoredTeam = spawns.FirstOrDefault(s => protection.ContainsKey(s.Team))?.Team;
        selectedTeamId = authoredTeam ?? spawns.FirstOrDefault()?.Team ?? teams.FirstOrDefault()?.Id;
    }

    private Task OnCanvasReady() => PaintProtection();

    // The drawn rectangle joins the authored team's zone (or seeds it from the spawn it covers).
    private async Task OnRectDrawn((double MinX, double MinZ, double MaxX, double MaxZ) r)
    {
        if (!AddRect((r.MinX, r.MinZ, r.MaxX, r.MaxZ))) return;
        await PaintProtection();
        if (authoredTeam is not null) await SelectOnCanvas(authoredTeam);
    }

    // Resizing one authored rect re-derives the orbit; orbit copies are ghosts and never fire this.
    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        var (team, idx) = ParseRectId(e.Id);
        if (team is null || team != authoredTeam || !protection.TryGetValue(team, out var rects)
            || idx < 0 || idx >= rects.Count) return;
        rects[idx] = new Rect(e.MinX, e.MinZ, e.MaxX, e.MaxZ);
        DeriveAndWrite();
        await PaintProtection();
        if (authoredTeam is not null) await SelectOnCanvas(authoredTeam);
    }

    // Accumulate the rect onto the authored team's zone: the current authored team, else the team whose
    // spawn this rect covers (the "first rect over a spawn selects" rule). Ignored when neither holds.
    private bool AddRect((double MinX, double MinZ, double MaxX, double MaxZ) rect)
    {
        var team = authoredTeam is not null && spawns.Any(s => s.Team == authoredTeam) ? authoredTeam
            : spawns.FirstOrDefault(s => Covers(rect, s.X, s.Z))?.Team;
        if (team is null) return false;
        if (!protection.TryGetValue(team, out var list)) { list = new(); protection[team] = list; }
        list.Add(new Rect(rect.MinX, rect.MinZ, rect.MaxX, rect.MaxZ));
        authoredTeam = team;
        selectedTeamId = team;
        DeriveAndWrite();
        return true;
    }

    // Rebuild every team's zone from the authored team's rect-set: orbit it, keying each image-set by the
    // spawn its primary rect covers. Block-rounds via OrbitAssignment.
    private void DeriveAndWrite()
    {
        if (authoredTeam is null || ZoneOf(authoredTeam).Count == 0)
        {
            protection.Clear(); authoredTeam = null; WriteProtection(); return;
        }
        var ordered = PrimaryFirst(authoredTeam, protection[authoredTeam]);
        var anchors = spawns.Select(s => new OrbitAssignment.Anchor(s.Team, s.X, s.Z)).ToList();
        protection.Clear();
        foreach (var set in OrbitAssignment.ByCoveredAnchorSet(ordered, symMode, symCx, symCz, anchors))
            protection[set.Id] = set.Rects.Select(z => new Rect(z.MinX, z.MinZ, z.MaxX, z.MaxZ)).ToList();
        WriteProtection();
    }

    // Order a team's rects so a spawn-covering one is the primary (orbit owner key); extras follow.
    private List<(double MinX, double MinZ, double MaxX, double MaxZ)> PrimaryFirst(string team, List<Rect> rects)
    {
        var list = rects.Select(r => (r.MinX, r.MinZ, r.MaxX, r.MaxZ)).ToList();
        var spawn = spawns.FirstOrDefault(s => s.Team == team);
        if (spawn is null) return list;
        var i = list.FindIndex(r => Covers((r.MinX, r.MinZ, r.MaxX, r.MaxZ), spawn.X, spawn.Z));
        if (i > 0) { var primary = list[i]; list.RemoveAt(i); list.Insert(0, primary); }
        return list;
    }

    private static bool Covers((double MinX, double MinZ, double MaxX, double MaxZ) r, double x, double z)
        => x >= r.MinX && x <= r.MaxX && z >= r.MinZ && z <= r.MaxZ;

    // Canvas click → select the picked team's zone (echo selection back so its handles render), or deselect on a miss.
    private async Task OnCanvasSelect(string? id)
    {
        var team = id is null ? null : TeamFromRegionId(id);
        selectedTeamId = team;
        if (team is not null) await SelectOnCanvas(team);
        else { if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>()); StateHasChanged(); }
    }

    // Only the authored team's rects show handles; orbit copies are ghosts (the canvas ignores them for anchors).
    private async Task SelectOnCanvas(string team)
    {
        if (canvas is not null)
        {
            var ids = team == authoredTeam
                ? Enumerable.Range(0, ZoneOf(team).Count).Select(i => RectId(team, i)).ToArray()
                : Array.Empty<string>();
            await canvas.SetSelectionAsync(ids);
        }
        StateHasChanged();
    }

    private async Task SelectTeam(string id) { selectedTeamId = id; await SelectOnCanvas(id); }

    private async Task ClearProtection(string team)
    {
        if (team != authoredTeam) return;
        protection.Clear();   // the whole set derives from the authored zone — clearing it removes all
        authoredTeam = null;
        WriteProtection();
        await PaintProtection();
    }

    private async Task DeleteRect(string team, int index)
    {
        if (team != authoredTeam || !protection.TryGetValue(team, out var rects) || index < 0 || index >= rects.Count) return;
        rects.RemoveAt(index);
        DeriveAndWrite();
        await PaintProtection();
        if (authoredTeam is not null) await SelectOnCanvas(authoredTeam);
    }

    private static string RectId(string team, int i) => $"{team}-spawn-protect-{i + 1}";

    private (string? Team, int Index) ParseRectId(string? id)
    {
        const string mid = "-spawn-protect-";
        if (id is not null && id.LastIndexOf(mid, StringComparison.Ordinal) is var at and >= 0
            && int.TryParse(id[(at + mid.Length)..], out var n))
        {
            var team = id[..at];
            if (teams.Any(t => t.Id == team)) return (team, n - 1);
        }
        return (null, -1);
    }

    // A click resolves to a team via a protection rect ("{team}-spawn-protect-N") or the spawn marker
    // ("{team}-spawn") sitting inside it — either selects that team's protection.
    private string? TeamFromRegionId(string? id)
    {
        if (id is null) return null;
        var (rectTeam, _) = ParseRectId(id);
        if (rectTeam is not null) return rectTeam;
        const string suffix = "-spawn";
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
            ["protection"] = new JsonArray(ZoneOf(s.Team).Select(RectNode).ToArray()),
            ["yaw"] = SpawnYaw(s.Team),
        }).ToArray());
        Wizard.MarkDirty();
    }

    private static JsonNode RectNode(Rect r) => new JsonObject { ["minX"] = r.MinX, ["minZ"] = r.MinZ, ["maxX"] = r.MaxX, ["maxZ"] = r.MaxZ };

    // The protection is a JSON array of {minX,minZ,maxX,maxZ}; tolerate a legacy single object too.
    private static IEnumerable<Rect> ParseRects(JsonNode? node) => node switch
    {
        JsonArray arr => arr.OfType<JsonObject>().Select(RectOf),
        JsonObject obj => new[] { RectOf(obj) },
        _ => Enumerable.Empty<Rect>(),
    };

    private static Rect RectOf(JsonObject r) => new(D(r, "minX"), D(r, "minZ"), D(r, "maxX"), D(r, "maxZ"));

    // Preserve the yaw the spawn step wrote (this step only edits protection).
    private double SpawnYaw(string team)
    {
        if (Wizard.Intent["spawns"] is JsonArray sp)
            foreach (var s in sp.OfType<JsonObject>())
                if (S(s, "team") == team) return D(s, "yaw");
        return 0;
    }

    // One author-region set: each spawn as a point marker (the authored team's brighter) + each team's zone
    // rects (authored editable, orbit copies non-editable ghosts).
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
        var zones = new List<object>();
        foreach (var (team, rects) in protection)
            for (var i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                zones.Add(new
                {
                    id = RectId(team, i),
                    type = "rectangle",
                    label = $"{TeamName(team)} spawn protection",
                    color = Hex(team),
                    ghost = team != authoredTeam,   // orbit copies are derived previews — non-editable
                    bounds = new { min_x = r.MinX, min_z = r.MinZ, max_x = r.MaxX, max_z = r.MaxZ },
                });
            }
        await canvas.SetAuthorRegionsAsync(markers.Concat(zones));
    }

    private static string S(JsonObject? o, string k) => o?[k]?.GetValue<string>() ?? "";
    private static double D(JsonObject? o, string k)
    {
        if (o?[k] is JsonValue v) { if (v.TryGetValue(out double d)) return d; if (v.TryGetValue(out int i)) return i; }
        return 0;
    }
}
