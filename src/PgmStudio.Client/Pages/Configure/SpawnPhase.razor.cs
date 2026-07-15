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

// Teams · spawn step: the point tool drops team 0's spawn; the rest are orbit-filled from the confirmed
// symmetry. Placement is island-aware — the clicked island's team (from the team-assignment step's
// islandTeams) owns the spawn, and each orbit-filled spawn is reassigned by the island it lands in (so a
// slightly-off rotation still lands on the right team). The select tool picks a placed marker to inspect
// it; the reused side-view sets each spawn's Y on its terrain. Writes the intent's spawns slice.
public partial class SpawnPhase
{
    // Sidebar/inspector icon for a spawn — the canonical point icon, kept in sync with the region tree.
    private static readonly string PointIcon = RegionNode.Icon("point");

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
    private Spawn? observer;        // the <default> (observer/spectator) spawn — always present, editable like a team spawn
    private string? selectedTeamId;
    private EditorCanvas? canvas;

    // Sentinel "team" id for the observer so it reuses the team-spawn selection/marker/inspector path.
    private const string ObserverId = "__observer__";
    private const string ObserverHex = "#e2e8f0";   // neutral (spectator) marker colour

    private string Slug => Wizard.Slug;
    private Spawn? Selected => selectedTeamId == ObserverId ? observer : spawns.FirstOrDefault(s => s.Team == selectedTeamId);
    private static bool IsObserver(Spawn s) => s.Team == ObserverId;
    private Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string Hex(string teamId) => teamId == ObserverId ? ObserverHex : GameColors.ChatHex(TeamOf(teamId)?.Color ?? "");
    private string TeamName(string id) => id == ObserverId ? "Observer" : TeamOf(id)?.Name ?? id;

    protected override async Task OnInitializedAsync()
    {
        LoadFromIntent();
        await LoadIslands();
        await EnsureObserverDefault();
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
        observer = null;
        if (Wizard.Intent["observer"] is JsonObject ob && ob["point"] is JsonObject opt)
            observer = new Spawn { Team = ObserverId, X = D(opt, "x"), Y = D(opt, "y"), Z = D(opt, "z"), Yaw = D(ob, "yaw") };
        selectedTeamId = spawns.FirstOrDefault()?.Team ?? teams.FirstOrDefault()?.Id;
    }

    // Every PGM map needs a <default> spawn; show one immediately (defaulting to the map middle) so the
    // author can edit it like a team spawn instead of leaving observers to fall in at 0,0,0.
    private async Task EnsureObserverDefault()
    {
        if (observer is not null) return;
        var (mx, mz) = MapMiddle();
        double x = Snap(mx), z = Snap(mz);
        // Seat the default observer on the terrain at the map middle (falling back to the first team spawn's
        // height, else world-bottom) instead of dropping it at Y=0.
        double y = await StandingYAsync(x, z) is { } f ? f : (spawns.FirstOrDefault()?.Y ?? 0);
        observer = new Spawn { Team = ObserverId, X = x, Y = y, Z = z };
        RecomputeObserverYaw();
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
        // With the observer selected, the point tool relocates it (it has no orbit) rather than placing a team.
        if (selectedTeamId == ObserverId && observer is not null)
        {
            observer.X = x; observer.Z = z;
            if (await StandingYAsync(x, z) is { } oy) observer.Y = oy;   // seat on terrain, not world-bottom
            RecomputeObserverYaw();
            WriteIntent(); await PaintSpawns(); return;
        }
        var team0 = IslandTeamAt(x, z) ?? selectedTeamId ?? teams.FirstOrDefault()?.Id;
        if (team0 is null) return;
        // Stand the spawn on the clicked column's terrain; the orbit shares it (symmetric terrain).
        var y = await StandingYAsync(x, z) ?? 0;
        PlaceAndOrbit(team0, x, z, y);
        selectedTeamId = team0;
        WriteIntent();
        await PaintSpawns();
    }

    // Select tool → pick the spawn point dummy region (id "{team}-spawn"); clicking empty (null) deselects.
    private void OnCanvasSelect(string? id) => selectedTeamId = TeamFromSpawnId(id);

    private string? TeamFromSpawnId(string? id)
    {
        const string suffix = "-spawn";
        if (id is null || !id.EndsWith(suffix)) return null;
        var team = id[..^suffix.Length];
        if (team == ObserverId && observer is not null) return ObserverId;
        return spawns.Any(s => s.Team == team) ? team : null;
    }

    // Rebuild the spawn list from team0's authored point: drop it at (x,y,z) and orbit-fill the rest by the
    // confirmed symmetry, each orbit spawn reassigned by the island it lands in. The orbit sits on symmetric
    // terrain, so the whole group shares the authored Y. Resets the list.
    private void PlaceAndOrbit(string team0, double x, double z, double y = 0)
    {
        spawns.Clear();
        spawns.Add(new Spawn { Team = team0, X = x, Y = y, Z = z, Authored = true });
        var i0 = teams.FindIndex(t => t.Id == team0);
        for (var k = 1; k < Symmetry.Order(symMode) && i0 >= 0; k++)
        {
            var (ox, oz) = Symmetry.Point(x, z, symMode, symCx, symCz, k);
            ox = Snap(ox); oz = Snap(oz);
            var tk = IslandTeamAt(ox, oz) ?? teams[(i0 + k) % teams.Count].Id;
            if (spawns.All(s => s.Team != tk)) spawns.Add(new Spawn { Team = tk, X = ox, Y = y, Z = oz });
        }
        RecomputeTeamYaws();       // each team looks at the map middle
        RecomputeObserverYaw();    // the observer re-aims at the (new) team-0 spawn
    }

    // The map middle a team spawn faces: the confirmed symmetry centre, else the islands' bounding-box
    // centre, else the spawn centroid, else the origin.
    private (double X, double Z) MapMiddle()
    {
        if (symMode is not null) return (symCx, symCz);
        double minX = double.PositiveInfinity, minZ = double.PositiveInfinity, maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
        foreach (var isl in islands)
            foreach (var p in isl.Ring)
            { minX = Math.Min(minX, p[0]); maxX = Math.Max(maxX, p[0]); minZ = Math.Min(minZ, p[1]); maxZ = Math.Max(maxZ, p[1]); }
        if (!double.IsInfinity(minX)) return ((minX + maxX) / 2, (minZ + maxZ) / 2);
        if (spawns.Count > 0) return (spawns.Average(s => s.X), spawns.Average(s => s.Z));
        return (0, 0);
    }

    // Auto-aim: team spawns look at the map middle; the observer looks at the authored (else first) team spawn.
    private void RecomputeTeamYaws()
    {
        var (mx, mz) = MapMiddle();
        foreach (var s in spawns) s.Yaw = Math.Round(Heading.YawTo(s.X, s.Z, mx, mz), 1);
    }

    private void RecomputeObserverYaw()
    {
        if (observer is null) return;
        var target = spawns.FirstOrDefault(s => s.Authored) ?? spawns.FirstOrDefault();
        observer.Yaw = target is null ? 0 : Math.Round(Heading.YawTo(observer.X, observer.Z, target.X, target.Z), 1);
    }

    private string? IslandTeamAt(double x, double z)
    {
        foreach (var isl in islands)
            if (Polygon.PointInRing(x, z, isl.Ring) && islandTeams.TryGetValue(isl.Id.ToString(), out var t)) return t;
        return null;
    }

    private static double Snap(double v) => Math.Floor(v) + 0.5;

    // The Y a spawn stands at in a column: one block above the terrain floor, or null when the column has no
    // segment data. `column-floor` reports the floor block itself (the topmost solid block, inclusive), so a
    // spawn placed at that Y would sit inside it rather than on top of it.
    private async Task<int?> StandingYAsync(double x, double z)
    {
        try
        {
            var d = await Http.GetFromJsonAsync<JsonElement>(
                $"api/map/{Slug}/column-floor?x={(int)Math.Floor(x)}&z={(int)Math.Floor(z)}");
            return d.TryGetProperty("y", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() + 1 : null;
        }
        catch { return null; }
    }

    private void SelectTeam(string id) => selectedTeamId = id;

    // A point RegionNode for the reused SliceView (the edit page's mini side-view) — it reads x/y/z + Type.
    // The id carries x/z so moving the spawn re-points the slice at its new column (Y drags don't, by design).
    private RegionNode SpawnNode(Spawn s) => new()
    {
        Id = $"{s.Team}-spawn@{s.X},{s.Z}", Type = "point",
        Coords = new() { ["x"] = s.X, ["y"] = s.Y, ["z"] = s.Z },
    };

    // Drag on the side-view → set the spawn's Y. Team spawns' orbit partners sit on symmetric terrain, so
    // they share the same height (propagate across the group); the observer owns its own Y.
    private void SetY(Spawn s, int y)
    {
        if (IsObserver(s)) s.Y = y;
        else foreach (var sp in spawns) sp.Y = y;
        WriteIntent();
    }

    private void SetCoord(Spawn s, string axis, double v)
    {
        if (IsObserver(s))
        {
            switch (axis) { case "x": s.X = v; break; case "z": s.Z = v; break; case "y": s.Y = v; break; case "yaw": s.Yaw = v; break; }
            if (axis is "x" or "z") RecomputeObserverYaw();   // moving it re-aims at the team spawn (manual yaw edits stick)
            WriteIntent();
            _ = PaintSpawns();
            return;
        }
        switch (axis)
        {
            case "x": s.X = v; break;
            case "z": s.Z = v; break;
            case "yaw": s.Yaw = v; break;
            case "y": foreach (var sp in spawns) sp.Y = v; break;   // orbit partners share terrain height
        }
        // Moving the authored spawn re-derives the symmetric orbit (and all yaws); an orbit spawn nudged on
        // its own just re-aims itself + the observer. A manual yaw edit is left untouched.
        if (s.Authored && axis is "x" or "z") PlaceAndOrbit(s.Team, s.X, s.Z, s.Y);
        else if (axis is "x" or "z")
        {
            var (mx, mz) = MapMiddle();
            s.Yaw = Math.Round(Heading.YawTo(s.X, s.Z, mx, mz), 1);
            RecomputeObserverYaw();
        }
        WriteIntent();
        _ = PaintSpawns();
    }

    private void WriteIntent()
    {
        // The Protection step owns each spawn's protection rects; this phase doesn't model them, so carry the
        // existing union through by team — otherwise re-saving spawns here silently drops it (→ protection: null).
        var keptProtection = new Dictionary<string, JsonNode>();
        if (Wizard.Intent["spawns"] is JsonArray prior)
            foreach (var s in prior.OfType<JsonObject>())
                if (s["team"]?.GetValue<string>() is { } t && s["protection"] is JsonArray pr)
                    keptProtection[t] = pr.DeepClone();

        Wizard.Intent["spawns"] = new JsonArray(spawns.Select(s =>
        {
            var o = new JsonObject
            {
                ["team"] = s.Team,
                ["point"] = new JsonObject { ["x"] = s.X, ["y"] = s.Y, ["z"] = s.Z },
                ["yaw"] = s.Yaw,
            };
            if (keptProtection.TryGetValue(s.Team, out var pr)) o["protection"] = pr;
            return (JsonNode)o;
        }).ToArray());
        if (observer is { } o)
            Wizard.Intent["observer"] = new JsonObject
            {
                ["point"] = new JsonObject { ["x"] = o.X, ["y"] = o.Y, ["z"] = o.Z },
                ["yaw"] = o.Yaw,
            };
        Wizard.MarkDirty();
    }

    // Render the spawns as point dummy regions (a marker each — authored brighter), selectable by the
    // normal canvas hit-test (id "{team}-spawn"); a 1-block footprint at the spawn point.
    private async Task PaintSpawns()
    {
        if (canvas is null) return;
        var markers = spawns.Select(s => Marker(s, s.Authored)).ToList();
        if (observer is { } o) markers.Add(Marker(o, primary: true));   // the observer always shows bright
        await canvas.SetAuthorRegionsAsync(markers);
    }

    private object Marker(Spawn s, bool primary) => new
    {
        id = $"{s.Team}-spawn",
        type = "point",
        marker = true,
        primary,
        color = Hex(s.Team),
        label = IsObserver(s) ? "Observer spawn" : $"{TeamName(s.Team)} spawn",
        bounds = new { min_x = s.X - 0.5, min_z = s.Z - 0.5, max_x = s.X + 0.5, max_z = s.Z + 0.5 },
    };

    private static string S(JsonObject? o, string k) => o?[k]?.GetValue<string>() ?? "";
    private static double D(JsonObject? o, string k)
    {
        if (o?[k] is JsonValue v) { if (v.TryGetValue(out double d)) return d; if (v.TryGetValue(out int i)) return i; }
        return 0;
    }
}
