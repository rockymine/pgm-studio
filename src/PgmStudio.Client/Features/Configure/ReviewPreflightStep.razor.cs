using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using PgmStudio.Client.Models;
using PgmStudio.Contracts;
using Sym = PgmStudio.Geom.Symmetry;
using PgmStudio.Geom;

namespace PgmStudio.Client.Features.Configure;

// Export · Checks step: the validation gate. Not an editing workspace — a read-only
// overview. Runs the four generated-map checks server-side (GET /preflight) and surfaces them as check
// rows + a validate log over a single static top-down map (real island geometry + the orbit-filled build
// bridges + the spawn↔wool nodes in their real team/dye colours), the playability picture in one image.
// Export (the XML step's Next) stays blocked until the gate is open; a failed check links the author
// back to Build. Writes no intent.
public partial class ReviewPreflightStep
{
    [CascadingParameter] public ConfigureTool Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    private PreflightDto? data;
    private MapView? view;
    private bool running = true;
    private string? error;

    protected override Task OnInitializedAsync() => RunAsync();

    // Re-run the gate (also the manual "Re-run checks" button) — the checks read the current generated map,
    // so revisiting after a Build edit re-evaluates connectivity without a page reload.
    private async Task RunAsync()
    {
        running = true; error = null; StateHasChanged();
        try
        {
            data = await Http.GetFromJsonAsync<PreflightDto>($"api/map/{Wizard.Slug}/preflight");
            view = await BuildViewAsync();
        }
        catch (Exception ex) { error = ex.Message; data = null; view = null; }
        running = false;
        StateHasChanged();
    }

    private IReadOnlyList<PreflightCheckDto> Checks => data?.Checks ?? [];
    private bool Blocked => data is { IntentMap: true, ExportReady: false };
    private PreflightCheckDto? Failed(string key) => Checks.FirstOrDefault(c => c.Key == key && c.Status == "fail");

    private static string DotClass(string status) => status switch
    {
        "pass" => "step-dot step-dot--done",
        "fail" => "step-dot step-dot--error",
        _ => "step-dot",   // skip
    };

    // Colour a console line by content — the log is plain text from the gate; error keywords win over the
    // success ones (so "not connected" reads as an error, not a "connected" success).
    // A check's line takes its check's status; the closing line says whether export is open.
    private string LineClass(string line)
    {
        var status = Checks.FirstOrDefault(check => line.StartsWith(check.Label + ":", StringComparison.Ordinal))?.Status;
        if (status == "fail" || line.StartsWith("Export is blocked", StringComparison.Ordinal)) return "console-line console-line--error";
        if (status == "skip") return "console-line console-line--warning";
        if (status == "pass" || line.StartsWith("Export is open", StringComparison.Ordinal)) return "console-line console-line--success";
        return "console-line console-line--info";
    }

    // ── the static overview map ──
    // A top-down SVG in world coords (viewBox = the map bbox): island polygons, the orbit-filled buildable
    // bridges, the authored spawn-protection zones + wool rooms (in team/dye colour), the spawn (circle) /
    // wool (square) / monument (diamond) nodes in their real colours — everything the author authored, in
    // one image. A node cut off from the spawn↔wool chain is ringed red. No interaction.
    private sealed record MapView(
        double MinX, double MinZ, double W, double H,
        IReadOnlyList<string> Islands,
        IReadOnlyList<(double X, double Z, double W, double H)> Bridges,
        IReadOnlyList<Zone> Protections,
        IReadOnlyList<Zone> Rooms,
        IReadOnlyList<Marker> Monuments,
        IReadOnlyList<Dot> Points);

    /// <summary>A walk point: its kind (spawn, wool, destroyable, core, point, flag, scorebox), where it is, its
    /// colour, what its tooltip calls it, and whether the walk cut it off.</summary>
    private readonly record struct Dot(double X, double Z, string Kind, string Color, string Name, bool Isolated);
    private readonly record struct Zone(double X, double Z, double W, double H, string Color, string Name);
    private readonly record struct Marker(double X, double Z, string Color, string Name);

    private async Task<MapView?> BuildViewAsync()
    {
        var islands = await LoadIslandsAsync();
        var bridges = LoadBridges();

        var teamColors = TeamColors();
        var isolated = (data?.Traversability?.Isolated ?? [])
            .Select(i => (i.Kind, i.Name)).ToHashSet();
        var flagColors = FlagColors(teamColors);
        var points = (data?.Traversability?.Points ?? []).Select(p => new Dot(
            p.X, p.Z, p.Kind, DotColor(p.Kind, p.Name, teamColors, flagColors), DotName(p.Kind, p.Name),
            isolated.Contains((p.Kind, p.Name)))).ToList();
        var (protections, rooms, monuments) = LoadAuthoredRegions(teamColors);
        if (islands.Count == 0 && bridges.Count == 0 && points.Count == 0) return null;

        // Bbox over everything drawn, with a small margin so nothing touches the edge.
        double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
        void Grow(double x, double z) { minX = Math.Min(minX, x); minZ = Math.Min(minZ, z); maxX = Math.Max(maxX, x); maxZ = Math.Max(maxZ, z); }
        void GrowZone(Zone z) { Grow(z.X, z.Z); Grow(z.X + z.W, z.Z + z.H); }
        foreach (var (_, bx) in islands) { Grow(bx.MinX, bx.MinZ); Grow(bx.MaxX, bx.MaxZ); }
        foreach (var b in bridges) { Grow(b.X, b.Z); Grow(b.X + b.W, b.Z + b.H); }
        foreach (var z in protections) GrowZone(z);
        foreach (var z in rooms) GrowZone(z);
        foreach (var mo in monuments) Grow(mo.X, mo.Z);
        foreach (var p in points) Grow(p.X, p.Z);

        var m = Math.Max(4, Math.Max(maxX - minX, maxZ - minZ) * 0.04);
        minX -= m; minZ -= m; maxX += m; maxZ += m;
        return new MapView(minX, minZ, maxX - minX, maxZ - minZ,
            islands.Select(i => i.Points).ToList(), bridges, protections, rooms, monuments, points);
    }

    // Everything the author placed that the generator stores materialised per-team (unlike the one-side
    // build areas): spawn-protection zones (intent.spawns[].protection) + wool rooms / monuments
    // (intent.wools[].room / .monuments[].location). Read straight from the wizard's in-memory intent.
    private (List<Zone> Protections, List<Zone> Rooms, List<Marker> Monuments) LoadAuthoredRegions(Dictionary<string, string> teamColors)
    {
        var protections = new List<Zone>();
        var rooms = new List<Zone>();
        var monuments = new List<Marker>();

        if (Wizard.Intent["spawns"] is JsonArray spawns)
            foreach (var s in spawns.OfType<JsonObject>())
            {
                var team = s["team"]?.GetValue<string>() ?? "";
                foreach (var r in RectsOf(s["protection"]))
                    protections.Add(new Zone(r.X, r.Z, r.W, r.H, GameColors.ChatHex(teamColors.GetValueOrDefault(team, team)), $"{TeamNameOf(team)} protection"));
            }

        if (Wizard.Intent["wools"] is JsonArray wools)
        {
            var teams = AuthoringContext.LoadTeams(Wizard.Intent);
            foreach (var w in wools.OfType<JsonObject>())
            {
                var color = WoolAuthoring.ColorOf(w, teams);
                var dye = GameColors.DyeHex(color);
                var label = color.Length == 0 ? "Wool" : WoolAuthoring.Name(color);
                foreach (var rr in RectsOf(w["protection"])) rooms.Add(new Zone(rr.X, rr.Z, rr.W, rr.H, dye, $"{label} wool room"));
                if (w["monuments"] is JsonArray ms)
                    foreach (var mo in ms.OfType<JsonObject>())
                        if (mo["location"] is JsonObject loc)
                            monuments.Add(new Marker(Num(loc, "x"), Num(loc, "z"), dye, $"{label} wool monument"));
            }
        }

        return (protections, rooms, monuments);
    }

    private static double Num(JsonObject o, string k) => o[k] is JsonValue v && v.TryGetValue(out double d) ? d : 0;

    // A protection/room footprint is a JSON array of rects; tolerate a legacy single object too.
    private static IEnumerable<(double X, double Z, double W, double H)> RectsOf(JsonNode? n) => n switch
    {
        JsonArray arr => arr.OfType<JsonObject>().Select(RectOf),
        JsonObject obj => new[] { RectOf(obj) },
        _ => Enumerable.Empty<(double, double, double, double)>(),
    };

    private static (double X, double Z, double W, double H) RectOf(JsonObject r)
    {
        double x0 = Num(r, "minX"), z0 = Num(r, "minZ"), x1 = Num(r, "maxX"), z1 = Num(r, "maxZ");
        return (Math.Min(x0, x1), Math.Min(z0, z1), Math.Abs(x1 - x0), Math.Abs(z1 - z0));
    }

    private async Task<List<(string Points, Rect Bounds)>> LoadIslandsAsync()
    {
        var result = new List<(string, Rect)>();
        try
        {
            foreach (var isl in await AuthoringContext.LoadIslandsAsync(Http, Wizard.Slug))
            {
                var pts = isl.Ring().Where(p => p.Count >= 2).Select(p => (p[0], p[1])).ToList();
                pts = Simplify(pts);   // drop the collinear unit-steps of a block-edge polygon
                if (pts.Count < 3) continue;
                var s = string.Join(" ", pts.Select(p => $"{F(p.Item1)},{F(p.Item2)}"));
                var bx = isl.Bounds.Count >= 4
                    ? new Rect(isl.Bounds[0], isl.Bounds[1], isl.Bounds[2], isl.Bounds[3])
                    : new Rect(pts.Min(p => p.Item1), pts.Min(p => p.Item2), pts.Max(p => p.Item1), pts.Max(p => p.Item2));
                result.Add((s, bx));
            }
        }
        catch { /* no scan geometry → the overview still shows the checks + nodes */ }
        return result;
    }

    // The buildable bridges the author drew (intent.build.areas), orbit-filled by the confirmed symmetry —
    // the generator mirrors the authored areas onto the other teams, so the overview must too (the same
    // canonical Geom.Symmetry transform the generator uses, since this is a static SVG, not the live canvas).
    private List<(double X, double Z, double W, double H)> LoadBridges()
    {
        var authored = new List<(double MinX, double MinZ, double MaxX, double MaxZ)>();
        if (Wizard.Intent["build"] is JsonObject b && b["areas"] is JsonArray areas)
            foreach (var a in areas.OfType<JsonObject>())
            {
                double N(string k) => a[k] is JsonValue v && v.TryGetValue(out double d) ? d : 0;
                authored.Add((Math.Min(N("minX"), N("maxX")), Math.Min(N("minZ"), N("maxZ")),
                              Math.Max(N("minX"), N("maxX")), Math.Max(N("minZ"), N("maxZ"))));
            }

        var (mode, cx, cz) = Symmetry();
        var rects = new List<(double, double, double, double)>();
        foreach (var (minX, minZ, maxX, maxZ) in authored)
        {
            rects.Add((minX, minZ, maxX, maxZ));
            for (var k = 1; k < Sym.Order(mode); k++)
                rects.Add(Sym.Rect(minX, minZ, maxX, maxZ, mode, cx, cz, k));
        }
        return rects.Select(r => (r.Item1, r.Item2, r.Item3 - r.Item1, r.Item4 - r.Item2)).ToList();
    }

    private (string? Mode, double Cx, double Cz) Symmetry()
    {
        if (Wizard.Intent["symmetry"] is JsonObject s)
        {
            double N(string k) => s[k] is JsonValue v && v.TryGetValue(out double d) ? d : 0;
            return (s["mode"]?.GetValue<string>(), N("centerX"), N("centerZ"));
        }
        return (null, 0, 0);
    }

    // A spawn and a score box take their team's colour, a wool its dye, a flag post its flag's banner, and a capture
    // point, owned by nobody, white.
    private static string DotColor(string kind, string name, Dictionary<string, string> teamColors,
                                   Dictionary<string, string> flagColors) => kind switch
    {
        "spawn" => GameColors.ChatHex(teamColors.GetValueOrDefault(name, name)),
        "scorebox" => GameColors.ChatHex(teamColors.GetValueOrDefault(name.Replace(" score box", ""), "")),
        "flag" => flagColors.GetValueOrDefault(name, "#ffffff"),
        "point" => "#ffffff",
        _ => GameColors.DyeHex(name),
    };

    private static string DotName(string kind, string name) => kind switch
    {
        "spawn" => $"{name} spawn",
        "wool" => $"{name} wool",
        "flag" => $"{name} post",
        _ => name,
    };

    // Each flag's drawn colour by the name the walk reads it under: its own dye, or its team's colour.
    private Dictionary<string, string> FlagColors(Dictionary<string, string> teamColors)
    {
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var flag in (Wizard.Intent["flags"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var team = flag["team"]?.GetValue<string>() ?? "";
            var name = flag["name"]?.GetValue<string>() ?? "";
            var dye = flag["color"]?.GetValue<string>() ?? "";
            if (name.Length == 0) continue;
            colors[name] = dye.Length > 0 ? GameColors.DyeHex(dye) : GameColors.ChatHex(teamColors.GetValueOrDefault(team, ""));
        }
        return colors;
    }

    private string TeamNameOf(string id) =>
        AuthoringContext.LoadTeams(Wizard.Intent).FirstOrDefault(team => team.Id == id)?.Name is { Length: > 0 } name ? name : id;

    private Dictionary<string, string> TeamColors()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Wizard.Intent["teams"] is JsonArray teams)
            foreach (var t in teams.OfType<JsonObject>())
            {
                var id = t["id"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(id)) map[id] = t["color"]?.GetValue<string>() ?? id;
            }
        return map;
    }

    // Remove points collinear with their neighbours (a block-edge polygon steps one unit at a time along a
    // straight edge → keep only the corners). Keeps the static SVG light without changing the shape.
    private static List<(double, double)> Simplify(List<(double, double)> pts)
    {
        if (pts.Count > 1 && pts[0] == pts[^1]) pts = pts[..^1];   // GeoJSON closes the ring; drop the dup
        if (pts.Count < 3) return pts;
        var outv = new List<(double, double)>();
        for (var i = 0; i < pts.Count; i++)
        {
            var a = pts[(i - 1 + pts.Count) % pts.Count];
            var b = pts[i];
            var c = pts[(i + 1) % pts.Count];
            // cross product of (b-a)×(c-b); ~0 ⇒ b lies on the a→c line ⇒ drop it.
            var cross = (b.Item1 - a.Item1) * (c.Item2 - b.Item2) - (b.Item2 - a.Item2) * (c.Item1 - b.Item1);
            if (Math.Abs(cross) > 1e-6) outv.Add(b);
        }
        return outv.Count >= 3 ? outv : pts;
    }

    // Stroke + node sizes scale with the map so they read the same at any map size.
    private double Span => view is null ? 1 : Math.Max(view.W, view.H);
    private string Sw => F(Math.Max(0.4, Span * 0.004));
    private string IsoSw => F(Math.Max(1.0, Span * 0.011));   // the red ring on a cut-off node
    private double MarkerR => Math.Max(1.5, Span * 0.013);
    private string ProtDash => $"{F(Math.Max(1.0, Span * 0.012))} {F(Math.Max(0.8, Span * 0.009))}";   // dashed protection outline

    // Triangle polygon points around a centre: a flag post, set apart from the spawn circle and the wool square.
    private string Triangle(double x, double z) =>
        $"{F(x)},{F(z - MarkerR * 1.2)} {F(x + MarkerR * 1.1)},{F(z + MarkerR)} {F(x - MarkerR * 1.1)},{F(z + MarkerR)}";

    // The legend names only what the picture draws, each as its mark and its name.
    private static IEnumerable<(string Key, string Label)> Legend(MapView v)
    {
        yield return ("", "Islands");
        if (v.Bridges.Count > 0) yield return ("bridge", "buildable bridges");
        if (v.Protections.Count > 0) yield return ("dashed box", "spawn protection");
        if (v.Rooms.Count > 0) yield return ("filled box", "wool room");
        foreach (var (kind, mark, label) in new[]
                 {
                     ("spawn", "●", "spawn"), ("wool", "■", "wool"), ("destroyable", "■", "monument"), ("core", "■", "core"),
                     ("flag", "▲", "flag post"), ("scorebox", "□", "score box"), ("point", "○", "capture point"),
                 })
            if (v.Points.Any(point => point.Kind == kind)) yield return (mark, label);
        if (v.Monuments.Count > 0) yield return ("◆", "wool monument");
    }

    // Diamond polygon points around a centre (distinguishes a monument from the wool square / spawn circle).
    private string Diamond(double x, double z)
    {
        var r = MarkerR;
        return $"{F(x)},{F(z - r)} {F(x + r)},{F(z)} {F(x)},{F(z + r)} {F(x - r)},{F(z)}";
    }

    // SVG coords must use '.' regardless of the WASM thread's culture.
    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
