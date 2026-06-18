using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Pages.EditorActivities;

namespace PgmStudio.Client.Pages.Configure;

// Teams · protection step: draw a rectangle around the authored team's spawn with the rectangle tool; the
// confirmed symmetry orbits it onto the other teams. Each protection zone is a dummy region on the reused
// canvas (selectable + resizable). Writes each spawn's `protection` slice of the intent; the generator
// turns it into a spawn-protection rectangle region and wires the enter/block filters onto that rectangle.
public partial class ProtectionPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed class Team { public string Id = ""; public string Name = ""; public string Color = ""; }
    private sealed class Spawn { public string Team = ""; public double X, Y, Z; public bool Authored; }
    private sealed record Island(int Id, double[][] Ring);
    private record struct Rect(double MinX, double MinZ, double MaxX, double MaxZ);

    private readonly List<Team> teams = new();
    private readonly Dictionary<string, string> islandTeams = new();   // island id → team id
    private string? symMode; private double symCx, symCz;
    private List<Island> islands = new();
    private readonly List<Spawn> spawns = new();
    private readonly Dictionary<string, Rect> protection = new();       // team id → protection rect
    private string? selectedTeamId;
    private EditorCanvas? canvas;

    private string Slug => Wizard.Slug;
    private Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string Hex(string teamId) => GameColors.ChatHex(TeamOf(teamId)?.Color ?? "");
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;
    private Spawn? Authored => spawns.FirstOrDefault(s => s.Authored) ?? spawns.FirstOrDefault();
    private string? AuthoredTeam => Authored?.Team;

    protected override async Task OnInitializedAsync()
    {
        LoadFromIntent();
        await LoadIslands();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) => await JS.InvokeVoidAsync("studio.icons");

    private void LoadFromIntent()
    {
        teams.Clear();
        if (Wizard.Intent["teams"] is JsonArray arr)
            foreach (var t in arr.OfType<JsonObject>())
                teams.Add(new Team { Id = S(t, "id"), Name = S(t, "name"), Color = S(t, "color") });
        islandTeams.Clear();
        if (Wizard.Intent["islandTeams"] is JsonObject it)
            foreach (var kv in it) if (kv.Value?.GetValue<string>() is { Length: > 0 } v) islandTeams[kv.Key] = v;
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
        if (spawns.Count > 0) spawns[0].Authored = true;   // the placed one (heuristic on reload)
        selectedTeamId = AuthoredTeam ?? teams.FirstOrDefault()?.Id;
    }

    private async Task LoadIslands()
    {
        try
        {
            var arr = await Http.GetFromJsonAsync<JsonElement>($"api/map/{Slug}/islands");
            islands = new();
            if (arr.ValueKind == JsonValueKind.Array)
                foreach (var e in arr.EnumerateArray())
                {
                    var id = e.GetProperty("id").GetInt32();
                    if (e.TryGetProperty("polygon", out var poly) && poly.TryGetProperty("coordinates", out var co)
                        && co.ValueKind == JsonValueKind.Array && co.GetArrayLength() > 0)
                        islands.Add(new Island(id, co[0].EnumerateArray().Select(p => new[] { p[0].GetDouble(), p[1].GetDouble() }).ToArray()));
                }
        }
        catch { islands = new(); }
    }

    private Task OnCanvasReady() => PaintProtection();

    // Rectangle tool → the drawn zone belongs to the team whose spawn it wraps (else the selection); if that
    // team is the authored one, orbit-fill the rest. Drawing around an orbit team only hand-corrects that team.
    private async Task OnRectDrawn((double MinX, double MinZ, double MaxX, double MaxZ) r)
    {
        var rect = new Rect(r.MinX, r.MinZ, r.MaxX, r.MaxZ);
        var team = TeamForRect(rect) ?? selectedTeamId ?? AuthoredTeam;
        if (team is null) return;
        protection[team] = rect;
        if (team == AuthoredTeam) OrbitProtection();
        selectedTeamId = team;
        WriteProtection();
        await PaintProtection();
        await SelectOnCanvas(team);   // show the drawn zone's resize handles right away
    }

    // Canvas click → select the picked zone (echo selection back so its handles render), or deselect when
    // the click misses every zone (empty space), matching the edit canvas.
    private async Task OnCanvasSelect(string? id)
    {
        var team = id is null ? null : TeamFromRegionId(id);
        selectedTeamId = team;
        if (team is not null) await SelectOnCanvas(team);
        else { if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>()); StateHasChanged(); }
    }

    // Highlight a team's zone on the canvas (and so render its resize handles), syncing the sidebar.
    private async Task SelectOnCanvas(string team)
    {
        if (canvas is not null)
            await canvas.SetSelectionAsync(protection.ContainsKey(team) ? new[] { RegionId(team) } : Array.Empty<string>());
        StateHasChanged();
    }

    // A protection rect was resized on the canvas → update that team; re-orbit if it's the authored team.
    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        var team = TeamFromRegionId(e.Id);
        if (team is null) return;
        protection[team] = new Rect(e.MinX, e.MinZ, e.MaxX, e.MaxZ);
        if (team == AuthoredTeam) OrbitProtection();
        WriteProtection();
        await PaintProtection();
        await SelectOnCanvas(team);   // keep handles on the edited zone
    }

    private async Task SelectTeam(string id) { selectedTeamId = id; await SelectOnCanvas(id); }

    private async Task ClearProtection(string team)
    {
        protection.Remove(team);
        if (team == AuthoredTeam) protection.Clear();   // the orbit derives from the authored zone
        WriteProtection();
        await PaintProtection();
    }

    // Orbit the authored team's protection rect onto every other team, matched to each team's existing spawn
    // (the symmetry step k that maps the authored spawn closest to that team's spawn).
    private void OrbitProtection()
    {
        if (AuthoredTeam is not { } a0 || !protection.TryGetValue(a0, out var baseRect) || Authored is not { } a) return;
        var order = OrbitOrder();
        foreach (var s in spawns)
        {
            if (s.Team == a0) continue;
            var bestK = 1; var bestD = double.MaxValue;
            for (var k = 1; k < order; k++)
            {
                var (ox, oz) = Orbit(a.X, a.Z, k);
                var d = (ox - s.X) * (ox - s.X) + (oz - s.Z) * (oz - s.Z);
                if (d < bestD) { bestD = d; bestK = k; }
            }
            protection[s.Team] = OrbitRect(baseRect, bestK);
        }
    }

    private Rect OrbitRect(Rect r, int k)
    {
        ReadOnlySpan<(double x, double z)> corners =
            [(r.MinX, r.MinZ), (r.MaxX, r.MinZ), (r.MaxX, r.MaxZ), (r.MinX, r.MaxZ)];
        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        foreach (var (cx, cz) in corners)
        {
            var (ox, oz) = Orbit(cx, cz, k);
            minX = Math.Min(minX, ox); minZ = Math.Min(minZ, oz);
            maxX = Math.Max(maxX, ox); maxZ = Math.Max(maxZ, oz);
        }
        // Round away the rotation's float noise — protection zones are block-aligned like the drawn rect.
        return new Rect(Math.Round(minX), Math.Round(minZ), Math.Round(maxX), Math.Round(maxZ));
    }

    private int OrbitOrder() => symMode is null ? 1 : symMode == "rot_90" ? 4 : 2;

    // Orbit one point by the confirmed symmetry (mirrors the spawn step's orbit).
    private (double x, double z) Orbit(double x, double z, int k) => symMode switch
    {
        "rot_90" => Rotate(x, z, 90 * k),
        "rot_180" => Rotate(x, z, 180),
        "mirror_x" => Reflect(x, z, 1, 0),
        "mirror_z" => Reflect(x, z, 0, 1),
        "mirror_d1" => Reflect(x, z, 1, -1),
        "mirror_d2" => Reflect(x, z, 1, 1),
        _ => (x, z),
    };
    private (double, double) Rotate(double x, double z, double deg)
    {
        var rad = deg * Math.PI / 180; double dx = x - symCx, dz = z - symCz;
        return (symCx + dx * Math.Cos(rad) - dz * Math.Sin(rad), symCz + dx * Math.Sin(rad) + dz * Math.Cos(rad));
    }
    private (double, double) Reflect(double x, double z, double nx, double nz)
    {
        double dx = x - symCx, dz = z - symCz, d = (dx * nx + dz * nz) / (nx * nx + nz * nz);
        return (x - 2 * d * nx, z - 2 * d * nz);
    }

    private string? TeamForRect(Rect r)
        => spawns.FirstOrDefault(s => s.X >= r.MinX && s.X <= r.MaxX && s.Z >= r.MinZ && s.Z <= r.MaxZ)?.Team;

    private static string RegionId(string team) => $"{team}-spawn-protect";
    // A click resolves to a team via the protection rect ("{team}-spawn-protect") or the spawn marker
    // ("{team}-spawn"), which sits inside the zone — either selects that team's protection.
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

    // One author-region set: each spawn as a point marker (reference) + each team's protection rectangle.
    private async Task PaintProtection()
    {
        if (canvas is null) return;
        var markers = spawns.Select(s => (object)new
        {
            id = $"{s.Team}-spawn",
            type = "point",
            marker = true,
            primary = s.Authored,
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
