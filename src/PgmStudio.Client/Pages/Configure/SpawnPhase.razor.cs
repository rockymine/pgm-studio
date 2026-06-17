using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Pages.EditorActivities;

namespace PgmStudio.Client.Pages.Configure;

// Teams · spawn step: the point tool drops team 0's spawn; the rest are orbit-filled from the confirmed
// symmetry. Placement is island-aware — the clicked island's team (from the team-assignment step's
// islandTeams) owns the spawn, and each orbit-filled spawn is reassigned by the island it lands in (so a
// slightly-off rotation still lands on the right team). The select tool picks a placed marker to inspect
// it; the reused side-view sets each spawn's Y on its terrain. Writes the intent's spawns slice.
public partial class SpawnPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed class Team { public string Id = ""; public string Name = ""; public string Color = ""; }
    private sealed class Spawn { public string Team = ""; public double X, Y, Z, Yaw; public bool Authored; }
    private sealed record Island(int Id, double[][] Ring);

    private readonly List<Team> teams = new();
    private readonly Dictionary<string, string> islandTeams = new();   // island id → team id
    private string? symMode; private double symCx, symCz;
    private List<Island> islands = new();
    private readonly List<Spawn> spawns = new();
    private string? selectedTeamId;
    private EditorCanvas? canvas;

    private string Slug => Wizard.Slug;
    private Spawn? Selected => spawns.FirstOrDefault(s => s.Team == selectedTeamId);
    private Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string Hex(string teamId) => GameColors.ChatHex(TeamOf(teamId)?.Color ?? "");
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;

    protected override async Task OnInitializedAsync()
    {
        LoadFromIntent();
        await LoadIslands();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

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
        spawns.Clear();
        if (Wizard.Intent["spawns"] is JsonArray sp)
            foreach (var s in sp.OfType<JsonObject>())
            {
                var pt = s["point"] as JsonObject;
                spawns.Add(new Spawn { Team = S(s, "team"), X = D(pt, "x"), Y = D(pt, "y"), Z = D(pt, "z"), Yaw = D(s, "yaw") });
            }
        if (spawns.Count > 0) spawns[0].Authored = true;   // the placed one (heuristic on reload)
        selectedTeamId = spawns.FirstOrDefault()?.Team ?? teams.FirstOrDefault()?.Id;
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

    private async Task OnCanvasReady() => await PaintSpawns();

    // Point tool → place the spawn on the clicked island's team + orbit-fill the rest (island-aware).
    private async Task OnPointPick((double X, double Z) p)
    {
        double x = Snap(p.X), z = Snap(p.Z);
        var team0 = IslandTeamAt(x, z) ?? selectedTeamId ?? teams.FirstOrDefault()?.Id;
        if (team0 is null) return;
        PlaceAndOrbit(team0, x, z);
        selectedTeamId = team0;
        WriteSpawns();
        await PaintSpawns();
    }

    // Select tool → pick the spawn marker under the cursor (null = clicked empty space; keep the selection).
    private void OnSpawnSelected(string? team) { if (team is not null) selectedTeamId = team; }

    // Rebuild the spawn list from team0's authored point: drop it at (x,y,z) and orbit-fill the rest by the
    // confirmed symmetry, each orbit spawn reassigned by the island it lands in. The orbit sits on symmetric
    // terrain, so the whole group shares the authored Y. Resets the list.
    private void PlaceAndOrbit(string team0, double x, double z, double y = 0)
    {
        spawns.Clear();
        spawns.Add(new Spawn { Team = team0, X = x, Y = y, Z = z, Authored = true });
        var i0 = teams.FindIndex(t => t.Id == team0);
        for (var k = 1; k < OrbitOrder() && i0 >= 0; k++)
        {
            var (ox, oz) = Orbit(x, z, k);
            ox = Snap(ox); oz = Snap(oz);
            var tk = IslandTeamAt(ox, oz) ?? teams[(i0 + k) % teams.Count].Id;
            if (spawns.All(s => s.Team != tk)) spawns.Add(new Spawn { Team = tk, X = ox, Y = y, Z = oz });
        }
    }

    private int OrbitOrder() => symMode is null ? 1 : symMode == "rot_90" ? 4 : 2;

    private string? IslandTeamAt(double x, double z)
    {
        foreach (var isl in islands)
            if (PointInRing(x, z, isl.Ring) && islandTeams.TryGetValue(isl.Id.ToString(), out var t)) return t;
        return null;
    }

    private static bool PointInRing(double x, double z, double[][] ring)
    {
        var inside = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            double xi = ring[i][0], zi = ring[i][1], xj = ring[j][0], zj = ring[j][1];
            if (((zi > z) != (zj > z)) && (x < (xj - xi) * (z - zi) / (zj - zi) + xi)) inside = !inside;
        }
        return inside;
    }

    // Orbit one point by the confirmed symmetry (mirrors SymmetryExpander.Step).
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
        var r = deg * Math.PI / 180; double dx = x - symCx, dz = z - symCz;
        return (symCx + dx * Math.Cos(r) - dz * Math.Sin(r), symCz + dx * Math.Sin(r) + dz * Math.Cos(r));
    }
    private (double, double) Reflect(double x, double z, double nx, double nz)
    {
        double dx = x - symCx, dz = z - symCz, d = (dx * nx + dz * nz) / (nx * nx + nz * nz);
        return (x - 2 * d * nx, z - 2 * d * nz);
    }
    private static double Snap(double v) => Math.Floor(v) + 0.5;

    private void SelectTeam(string id) => selectedTeamId = id;

    // A point RegionNode for the reused SliceView (the edit page's mini side-view) — it reads x/y/z + Type.
    // The id carries x/z so moving the spawn re-points the slice at its new column (Y drags don't, by design).
    private RegionNode SpawnNode(Spawn s) => new()
    {
        Id = $"{s.Team}-spawn@{s.X},{s.Z}", Type = "point",
        Coords = new() { ["x"] = s.X, ["y"] = s.Y, ["z"] = s.Z },
    };

    // Drag on the side-view → set the spawn's Y. The orbit partners sit on symmetric terrain, so they
    // share the same height — propagate it across the whole orbit group.
    private void SetY(Spawn s, int y) { foreach (var sp in spawns) sp.Y = y; WriteSpawns(); }

    private void SetCoord(Spawn s, string axis, ChangeEventArgs e)
    {
        if (!double.TryParse(e.Value?.ToString(), out var v)) return;
        switch (axis)
        {
            case "x": s.X = v; break;
            case "z": s.Z = v; break;
            case "yaw": s.Yaw = v; break;
            case "y": foreach (var sp in spawns) sp.Y = v; break;   // orbit partners share terrain height
        }
        // Moving the authored spawn re-derives the symmetric orbit; orbit spawns are nudged on their own.
        if (s.Authored && axis is "x" or "z") PlaceAndOrbit(s.Team, s.X, s.Z, s.Y);
        WriteSpawns();
        _ = PaintSpawns();
    }

    private void WriteSpawns()
    {
        Wizard.Intent["spawns"] = new JsonArray(spawns.Select(s => (JsonNode)new JsonObject
        {
            ["team"] = s.Team,
            ["point"] = new JsonObject { ["x"] = s.X, ["y"] = s.Y, ["z"] = s.Z },
            ["yaw"] = s.Yaw,
        }).ToArray());
        Wizard.MarkDirty();
    }

    private async Task PaintSpawns()
    {
        if (canvas is not null)
            await canvas.SetAuthorSpawnsAsync(spawns.Select(s => (object)new { x = s.X, z = s.Z, color = Hex(s.Team), primary = s.Authored, team = s.Team }));
    }

    private static string S(JsonObject? o, string k) => o?[k]?.GetValue<string>() ?? "";
    private static double D(JsonObject? o, string k)
    {
        if (o?[k] is JsonValue v) { if (v.TryGetValue(out double d)) return d; if (v.TryGetValue(out int i)) return i; }
        return 0;
    }
}
