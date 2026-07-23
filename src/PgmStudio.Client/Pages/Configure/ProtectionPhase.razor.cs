using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Components;

namespace PgmStudio.Client.Pages.Configure;

// Teams · protection step: draw the rectangle(s) over a spawn — a protection zone is a union of rectangles,
// listed flat (one row per rectangle, with the generated region name + an authored/orbit badge, like the
// Spawn-points step) and edited one at a time (the Build · Buildable-layer pattern). The first rect over a
// spawn fixes the authored team; further rects add to its zone. Authored rects carry a stable id and resize
// on the canvas; the confirmed symmetry orbits them onto the other teams as non-editable copies — those are
// also listed (read-only, "orbit" badge) so the union reads coherently. Writes each spawn's `protection` slice.
public partial class ProtectionPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed class Team { public string Id = ""; public string Name = ""; public string Color = ""; }
    private sealed class Spawn { public string Team = ""; public double X, Y, Z; }
    private sealed class ProtRect { public int Id; public double MinX, MinZ, MaxX, MaxZ; }   // an authored rect (stable id)
    private readonly record struct Rect(double MinX, double MinZ, double MaxX, double MaxZ);
    // A list row: a rectangle (authored or orbit), its canvas region id, owning team, and the generated name.
    private sealed record Row(string RegionId, string Team, bool Authored, double MinX, double MinZ, double MaxX, double MaxZ, string Name);

    private readonly List<Team> teams = new();
    private string? symMode; private double symCx, symCz;
    private readonly List<Spawn> spawns = new();
    private readonly List<ProtRect> authored = new();               // the authored team's rects (stable ids, editable)
    private readonly Dictionary<string, List<Rect>> ghosts = new(); // orbit copies per partner team
    private string? authoredTeam;
    private int nextId = 1;
    private string? selectedRegionId;
    private EditorCanvas? canvas;

    private string Slug => Wizard.Slug;
    private Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string Hex(string teamId) => GameColors.ChatHex(TeamOf(teamId)?.Color ?? "");
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;

    // The flat list: the authored team's rects (editable) then each partner team's orbit copies (read-only).
    private List<Row> Rows()
    {
        var rows = new List<Row>();
        for (var i = 0; i < authored.Count; i++)
        {
            var r = authored[i];
            rows.Add(new Row(RegionId(r.Id), authoredTeam ?? "", true, r.MinX, r.MinZ, r.MaxX, r.MaxZ, ZoneName(authoredTeam ?? "", i, authored.Count)));
        }
        foreach (var kv in ghosts)
            for (var i = 0; i < kv.Value.Count; i++)
            {
                var r = kv.Value[i];
                rows.Add(new Row(GhostId(kv.Key, i), kv.Key, false, r.MinX, r.MinZ, r.MaxX, r.MaxZ, ZoneName(kv.Key, i, kv.Value.Count)));
            }
        return rows;
    }

    private Row? Selected => selectedRegionId is { } id ? Rows().FirstOrDefault(r => r.RegionId == id) : null;

    // The generated region name a rect becomes (single → {slug}-spawn, several → {slug}-spawn-N).
    private static string ZoneName(string team, int i, int count) => count <= 1 ? $"{TeamSlug(team)}-spawn" : $"{TeamSlug(team)}-spawn-{i + 1}";
    private static string TeamSlug(string teamId)
    {
        var s = teamId.Trim().ToLowerInvariant();
        if (s.EndsWith("-team")) s = s[..^5];
        s = Regex.Replace(s, "[^a-z0-9]+", "-").Trim('-');
        return s.Length > 0 ? s : teamId;
    }

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
        spawns.Clear(); authored.Clear(); ghosts.Clear(); authoredTeam = null; nextId = 1; selectedRegionId = null;
        if (Wizard.Intent["spawns"] is JsonArray sp)
            foreach (var s in sp.OfType<JsonObject>())
            {
                var pt = s["point"] as JsonObject;
                var team = S(s, "team");
                spawns.Add(new Spawn { Team = team, X = D(pt, "x"), Y = D(pt, "y"), Z = D(pt, "z") });
                var rects = ParseRects(s["protection"]).ToList();
                if (rects.Count == 0) continue;
                if (authoredTeam is null)   // first team with protection = the authored side; its rects are editable
                {
                    authoredTeam = team;
                    foreach (var r in rects) authored.Add(new ProtRect { Id = nextId++, MinX = r.MinX, MinZ = r.MinZ, MaxX = r.MaxX, MaxZ = r.MaxZ });
                }
                else ghosts[team] = rects;   // the rest are orbit copies
            }
    }

    private Task OnCanvasReady() => PaintProtection();

    private async Task OnRectDrawn((double MinX, double MinZ, double MaxX, double MaxZ) r)
    {
        // the authored team is fixed once set; before that the rect must cover a spawn to pick it
        var team = authoredTeam ?? spawns.FirstOrDefault(s => Covers(r, s.X, s.Z))?.Team;
        if (team is null) return;
        authoredTeam = team;
        var b = new ProtRect { Id = nextId++, MinX = Math.Round(r.MinX), MinZ = Math.Round(r.MinZ), MaxX = Math.Round(r.MaxX), MaxZ = Math.Round(r.MaxZ) };
        authored.Add(b);
        selectedRegionId = RegionId(b.Id);
        DeriveAndWrite();
        await PaintProtection();
        await SelectOnCanvas(selectedRegionId);
    }

    private async Task OnGeometrySaved((string Id, double MinX, double MinZ, double MaxX, double MaxZ) e)
    {
        if (AuthoredFromRegion(e.Id) is not { } b) return;   // only authored rects resize
        b.MinX = Math.Round(e.MinX); b.MinZ = Math.Round(e.MinZ); b.MaxX = Math.Round(e.MaxX); b.MaxZ = Math.Round(e.MaxZ);
        DeriveAndWrite();
        await PaintProtection();
        await SelectOnCanvas(e.Id);
    }

    private async Task OnCanvasSelect(string? id)
    {
        // the canvas reports authored rect ids (orbit copies aren't canvas-selectable); a marker/miss deselects
        if (AuthoredFromRegion(id) is { } b) { selectedRegionId = RegionId(b.Id); await SelectOnCanvas(selectedRegionId); }
        else { selectedRegionId = null; if (canvas is not null) await canvas.SetSelectionAsync(Array.Empty<string>()); StateHasChanged(); }
    }

    private async Task SelectRow(string regionId) { selectedRegionId = regionId; await SelectOnCanvas(regionId); }

    private async Task SelectOnCanvas(string regionId)
    {
        if (canvas is not null) await canvas.SetSelectionAsync(new[] { regionId });
        StateHasChanged();
    }

    private async Task Remove(string regionId)
    {
        if (AuthoredFromRegion(regionId) is not { } b) return;   // only authored rects are removable
        authored.Remove(b);
        if (selectedRegionId == regionId) selectedRegionId = null;
        if (authored.Count == 0) authoredTeam = null;
        DeriveAndWrite();
        await PaintProtection();
    }

    // Recompute the orbit copies from the authored rects (point-aware) and persist every team's protection slice.
    private void DeriveAndWrite()
    {
        ghosts.Clear();
        if (authoredTeam is not null && authored.Count > 0)
        {
            var primary = CoveringFirst(authoredTeam);
            var anchors = spawns.Select(s => new OrbitAssignment.Anchor(s.Team, s.X, s.Z)).ToList();
            foreach (var set in OrbitAssignment.ByCoveredAnchorSet(primary, symMode, symCx, symCz, anchors))
                if (set.Id != authoredTeam)
                    ghosts[set.Id] = set.Rects.Select(z => new Rect(z.MinX, z.MinZ, z.MaxX, z.MaxZ)).ToList();
        }
        WriteProtection();
    }

    // The authored rects as bound tuples, with one covering the authored team's spawn first (the orbit key).
    private List<(double MinX, double MinZ, double MaxX, double MaxZ)> CoveringFirst(string team)
    {
        var list = authored.Select(r => (r.MinX, r.MinZ, r.MaxX, r.MaxZ)).ToList();
        var spawn = spawns.FirstOrDefault(s => s.Team == team);
        if (spawn is null) return list;
        var i = list.FindIndex(r => Covers((r.MinX, r.MinZ, r.MaxX, r.MaxZ), spawn.X, spawn.Z));
        if (i > 0) { var p = list[i]; list.RemoveAt(i); list.Insert(0, p); }
        return list;
    }

    private static bool Covers((double MinX, double MinZ, double MaxX, double MaxZ) r, double x, double z)
        => x >= r.MinX && x <= r.MaxX && z >= r.MinZ && z <= r.MaxZ;

    private void WriteProtection()
    {
        Wizard.Intent["spawns"] = new JsonArray(spawns.Select(s =>
        {
            IEnumerable<Rect> rects =
                s.Team == authoredTeam ? authored.Select(r => new Rect(r.MinX, r.MinZ, r.MaxX, r.MaxZ))
                : ghosts.TryGetValue(s.Team, out var g) ? g
                : Enumerable.Empty<Rect>();
            return (JsonNode)new JsonObject
            {
                ["team"] = s.Team,
                ["point"] = new JsonObject { ["x"] = s.X, ["y"] = s.Y, ["z"] = s.Z },
                ["protection"] = new JsonArray(rects.Select(RectNode).ToArray()),
                ["yaw"] = SpawnYaw(s.Team),
            };
        }).ToArray());
        Wizard.MarkDirty();
    }

    private static JsonNode RectNode(Rect r) => new JsonObject { ["minX"] = r.MinX, ["minZ"] = r.MinZ, ["maxX"] = r.MaxX, ["maxZ"] = r.MaxZ };

    // Preserve the yaw the spawn step wrote (this step only edits protection).
    private double SpawnYaw(string team)
    {
        if (Wizard.Intent["spawns"] is JsonArray sp)
            foreach (var s in sp.OfType<JsonObject>())
                if (S(s, "team") == team) return D(s, "yaw");
        return 0;
    }

    // Spawn point markers (authored team brighter) + the authored rects (editable) + the orbit copies (ghosts).
    private async Task PaintProtection()
    {
        if (canvas is null) return;
        var markers = spawns.Select(s => (object)new
        {
            id = $"{s.Team}-spawn", type = "point", marker = true, primary = s.Team == authoredTeam,
            color = Hex(s.Team), label = $"{TeamName(s.Team)} spawn",
            bounds = new { min_x = s.X - 0.5, min_z = s.Z - 0.5, max_x = s.X + 0.5, max_z = s.Z + 0.5 },
        });
        var authoredNodes = authored.Select(r => (object)new
        {
            id = RegionId(r.Id), type = "rectangle", label = $"{TeamName(authoredTeam ?? "")} spawn protection",
            color = Hex(authoredTeam ?? ""),
            bounds = new { min_x = r.MinX, min_z = r.MinZ, max_x = r.MaxX, max_z = r.MaxZ },
        });
        var ghostNodes = ghosts.SelectMany(kv => kv.Value.Select((r, i) => (object)new
        {
            id = GhostId(kv.Key, i), type = "rectangle", ghost = true,
            label = $"{TeamName(kv.Key)} spawn protection", color = Hex(kv.Key),
            bounds = new { min_x = r.MinX, min_z = r.MinZ, max_x = r.MaxX, max_z = r.MaxZ },
        }));
        await canvas.SetAuthorRegionsAsync(markers.Concat(authoredNodes).Concat(ghostNodes));
    }

    private static string RegionId(int id) => $"prot-{id}";
    private static string GhostId(string team, int i) => $"prot-ghost-{team}-{i}";
    private ProtRect? AuthoredFromRegion(string? regionId)
        => regionId is { } s && s.StartsWith("prot-") && !s.StartsWith("prot-ghost-") && int.TryParse(s[5..], out var id)
            ? authored.FirstOrDefault(r => r.Id == id) : null;

    // protection is a JSON array of {minX,minZ,maxX,maxZ}; tolerate a legacy single object too.
    private static IEnumerable<Rect> ParseRects(JsonNode? node) => node switch
    {
        JsonArray arr => arr.OfType<JsonObject>().Select(RectOf),
        JsonObject obj => new[] { RectOf(obj) },
        _ => Enumerable.Empty<Rect>(),
    };
    private static Rect RectOf(JsonObject r) => new(D(r, "minX"), D(r, "minZ"), D(r, "maxX"), D(r, "maxZ"));

    private static string S(JsonObject? o, string k) => o?[k]?.GetValue<string>() ?? "";
    private static double D(JsonObject? o, string k)
    {
        if (o?[k] is JsonValue v) { if (v.TryGetValue(out double d)) return d; if (v.TryGetValue(out int i)) return i; }
        return 0;
    }
}
