using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PgmStudio.Client.Models;
using PgmStudio.Client.Pages.EditorActivities;

namespace PgmStudio.Client.Pages.Configure;

using W = WoolAuthoring;

// Wools · objectives step. On entry the world is scanned: signed monuments ("Place the X Wool here!")
// name the objective colours and give each capturing team (the island the monument sits on) → the wool's
// owner is the complement; the physical wool clusters give each objective's source location. Physical wool
// NOT named by a monument (or sitting in a team's own spawn) is decorative and excluded by default. The
// author confirms/rejects, fixes an owner, recolours, or hand-adds a missing wool. Writes the wools slice
// (owner + colour + a seed spawn + the detected monuments); the later sub-steps refine spawn/monuments/room.
public partial class WoolObjectivesPhase
{
    [CascadingParameter] public ConfigureWizard Wizard { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private sealed record Rect(double MinX, double MinZ, double MaxX, double MaxZ)
    {
        public bool Covers(double x, double z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
    }

    private sealed class Candidate
    {
        public string Color = "";
        public string Owner = "";
        public double X, Y, Z;          // source centroid (seeds the spawn step)
        public int Count;
        public bool Signed;
        public double Confidence;
        public string Evidence = "";
        public bool HasSource;          // a physical wool cluster was found
        public List<W.Monument> Monuments = new();
        public bool Included;
        public string? ExcludeReason;   // why it's excluded by default (null when included)
    }

    private readonly List<W.Team> teams = new();
    private string? symMode; private double symCx, symCz;
    private List<W.Island> islands = new();
    private readonly Dictionary<string, string> islandTeams = new();
    private readonly Dictionary<string, Rect> protection = new();   // team → spawn-protection rect
    private readonly List<Candidate> candidates = new();
    private string? selectedColor;
    private bool loading = true;
    private EditorCanvas? canvas;

    private string Slug => Wizard.Slug;
    private Candidate? Selected => candidates.FirstOrDefault(c => c.Color == selectedColor);
    private W.Team? TeamOf(string id) => teams.FirstOrDefault(t => t.Id == id);
    private string TeamName(string id) => TeamOf(id)?.Name ?? id;
    private int Included => candidates.Count(c => c.Included);

    protected override async Task OnInitializedAsync()
    {
        LoadContext();
        islands = await W.LoadIslandsAsync(Http, Slug);
        await DetectAsync();
        Reconcile();
        loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await JS.InvokeVoidAsync("studio.icons");

    private void LoadContext()
    {
        teams.Clear(); teams.AddRange(W.LoadTeams(Wizard.Intent));
        (symMode, symCx, symCz) = W.Sym(Wizard.Intent);
        foreach (var kv in W.LoadIslandTeams(Wizard.Intent)) islandTeams[kv.Key] = kv.Value;
        protection.Clear();
        if (Wizard.Intent["spawns"] is JsonArray sp)
            foreach (var s in sp.OfType<JsonObject>())
                if (s["protection"] is JsonObject pr)
                    protection[W.S(s, "team")] = new Rect(W.D(pr, "minX"), W.D(pr, "minZ"), W.D(pr, "maxX"), W.D(pr, "maxZ"));
    }

    // ── detection + combine (monuments authoritative, terrain wool = source/dedupe) ───────
    private async Task DetectAsync()
    {
        candidates.Clear();
        var (minX, minZ, maxX, maxZ) = W.MapBox(islands);

        // monuments: signed "place the X wool here" → objective colour + capturing team (its island)
        var monByColor = new Dictionary<string, List<W.Monument>>();
        var signed = new HashSet<string>(); var conf = new Dictionary<string, double>(); var evid = new Dictionary<string, string>();
        try
        {
            var ms = await Http.GetFromJsonAsync<JsonElement>(
                $"api/map/{Slug}/monument-suggestions?box={minX},0,{minZ},{maxX},255,{maxZ}&style=Any,Any,Any");
            if (ms.ValueKind == JsonValueKind.Array)
                foreach (var m in ms.EnumerateArray())
                {
                    var color = W.NormColor(Str(m, "color"));
                    if (color.Length == 0) continue;
                    double x = Dbl(m, "x"), y = Dbl(m, "y"), z = Dbl(m, "z");
                    var cap = W.IslandTeamAt(x, z, islands, islandTeams) ?? "";
                    monByColor.TryAdd(color, new());
                    monByColor[color].Add(new W.Monument { Team = cap, X = x, Y = y, Z = z });
                    if (Str(m, "source") == "sign") signed.Add(color);
                    var c = m.TryGetProperty("confidence", out var cf) ? cf.GetDouble() : 0;
                    if (c > conf.GetValueOrDefault(color)) { conf[color] = c; evid[color] = Str(m, "evidence"); }
                }
        }
        catch { /* no monuments gathered → terrain-only fallback below */ }

        // terrain wool: per colour, the source centroid (the room/pile) + the base (min) Y + a count
        var srcByColor = new Dictionary<string, (double x, double y, double z, int n, double minY)>();
        try
        {
            var body = new StringContent(
                new JsonObject { ["bounds"] = new JsonObject { ["minX"] = minX, ["minZ"] = minZ, ["maxX"] = maxX, ["maxZ"] = maxZ } }.ToJsonString(),
                Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync($"api/map/{Slug}/wool-sources", body);
            if (resp.IsSuccessStatusCode)
            {
                var d = await resp.Content.ReadFromJsonAsync<JsonElement>();
                if (d.TryGetProperty("colors", out var cols) && cols.ValueKind == JsonValueKind.Array)
                    foreach (var c in cols.EnumerateArray())
                    {
                        var color = W.NormColor(Str(c, "color"));
                        if (!c.TryGetProperty("sources", out var ss) || ss.ValueKind != JsonValueKind.Array || ss.GetArrayLength() == 0) continue;
                        double sx = 0, sy = 0, sz = 0, minY = double.MaxValue; int n = 0;
                        foreach (var s in ss.EnumerateArray()) { var y = Dbl(s, "y"); sx += Dbl(s, "x"); sy += y; sz += Dbl(s, "z"); minY = Math.Min(minY, y); n++; }
                        srcByColor[color] = (sx / n, sy / n, sz / n, n, minY);
                    }
            }
        }
        catch { /* no scan → monument-only candidates */ }

        foreach (var color in monByColor.Keys.Union(srcByColor.Keys))
        {
            var mons = monByColor.GetValueOrDefault(color) ?? new();
            var hasSrc = srcByColor.TryGetValue(color, out var src);
            // owner: monument-complement (the team NOT capturing it) wins; else the source island
            var capturers = mons.Select(m => m.Team).Where(t => t.Length > 0).ToHashSet();
            var byComplement = teams.Select(t => t.Id).Where(t => !capturers.Contains(t)).ToList();
            var owner = byComplement.Count == 1 ? byComplement[0]
                : (hasSrc ? W.IslandTeamAt(src.x, src.z, islands, islandTeams) : null) ?? "";

            var cand = new Candidate
            {
                Color = color, Owner = owner,
                X = hasSrc ? src.x : (mons.Count > 0 ? mons[0].X : 0),
                Y = hasSrc ? src.y : (mons.Count > 0 ? mons[0].Y : 0),
                Z = hasSrc ? src.z : (mons.Count > 0 ? mons[0].Z : 0),
                Count = hasSrc ? src.n : 0,
                Signed = signed.Contains(color),
                Confidence = conf.GetValueOrDefault(color),
                Evidence = evid.GetValueOrDefault(color, ""),
                HasSource = hasSrc,
                Monuments = mons,
            };

            // Seed the spawn Y onto the floor the wool rests on (the segment top at/below its base), not
            // the floating pile centroid — the author then fine-tunes it on the side-view.
            if (hasSrc)
                cand.Y = await RestingYAsync((int)Math.Round(cand.X), (int)Math.Round(cand.Z), (int)Math.Round(src.minY)) ?? cand.Y;

            // include rule: a monument-named colour is an objective; physical-only wool is decorative.
            if (mons.Count > 0) cand.Included = true;
            else { cand.Included = false; cand.ExcludeReason = "no monument names it"; }
            // a wool sitting in its owner's own spawn can't be captured → decorative
            if (cand.Owner.Length > 0 && protection.TryGetValue(cand.Owner, out var pz) && pz.Covers(cand.X, cand.Z))
            { cand.Included = false; cand.ExcludeReason = "in own spawn"; }

            candidates.Add(cand);
        }
        candidates.Sort((a, b) => string.CompareOrdinal(a.Color, b.Color));
        selectedColor = candidates.FirstOrDefault(c => c.Included)?.Color ?? candidates.FirstOrDefault()?.Color;
    }

    // Reconcile detection with an already-saved slice (revisit): the saved wools are the included set, so
    // adopt their colour/owner and drop any auto-include that contradicts it. First visit (empty slice)
    // persists the auto-detected objectives so the slice exists and the phase can advance.
    private void Reconcile()
    {
        if (Wizard.Intent["wools"] is JsonArray arr && arr.Count > 0)
        {
            var saved = arr.OfType<JsonObject>().ToDictionary(w => W.NormColor(W.S(w, "owner")) + "/" + W.NormColor(W.S(w, "color")), w => w);
            var savedColors = arr.OfType<JsonObject>().Select(w => W.NormColor(W.S(w, "color"))).ToHashSet();
            foreach (var c in candidates) c.Included = savedColors.Contains(c.Color);
            // any saved wool not in the detected pool (hand-added previously) → add it back as a candidate
            foreach (var w in arr.OfType<JsonObject>())
            {
                var color = W.NormColor(W.S(w, "color"));
                if (candidates.Any(c => c.Color == color)) continue;
                var sp = w["spawn"] as JsonObject;
                candidates.Add(new Candidate { Color = color, Owner = W.S(w, "owner"), Included = true,
                    X = W.D(sp, "x"), Y = W.D(sp, "y"), Z = W.D(sp, "z") });
            }
        }
        else PersistAndPaint();   // first visit — auto-confirm the detected objectives
    }

    private async Task OnCanvasReady() => await Paint();

    private void OnCanvasSelect(string? id)
    {
        if (id is { } s && s.StartsWith("wool-cand-")) selectedColor = s["wool-cand-".Length..];
    }

    private void Toggle(Candidate c)
    {
        c.Included = !c.Included;
        if (c.Included) c.ExcludeReason = null;
        selectedColor = c.Color;
        PersistAndPaint();
    }

    private void Select(Candidate c) => selectedColor = c.Color;

    private void SetOwner(Candidate c, string team) { c.Owner = team; PersistAndPaint(); }

    private void SetColor(Candidate c, string color)
    {
        var norm = W.NormColor(color);
        if (candidates.Any(o => o != c && o.Color == norm)) return;   // colours are unique (the wool key)
        selectedColor = (c.Color = norm);
        PersistAndPaint();
    }

    private void AddMissing()
    {
        var taken = candidates.Select(c => c.Color).ToHashSet();
        var color = GameColors.DyeColors.Select(d => W.NormColor(d.Value)).FirstOrDefault(v => !taken.Contains(v)) ?? "white";
        var cand = new Candidate { Color = color, Owner = teams.FirstOrDefault()?.Id ?? "", Included = true, HasSource = false };
        candidates.Add(cand);
        selectedColor = color;
        PersistAndPaint();
    }

    private void Remove(Candidate c) { candidates.Remove(c); if (selectedColor == c.Color) selectedColor = null; PersistAndPaint(); }

    // ── persistence ──────────────────────────────────────────────────────────────────────
    private void PersistAndPaint() { WriteWools(); _ = Paint(); StateHasChanged(); }

    private void WriteWools()
    {
        var wools = candidates.Where(c => c.Included).GroupBy(c => c.Color).Select(g => g.First())   // colour is the key
            .Select(c => new W.Wool
            {
                Owner = c.Owner, Color = c.Color,
                SpawnX = c.X, SpawnY = c.Y, SpawnZ = c.Z,
                Monuments = c.Monuments.Select(m => new W.Monument { Team = m.Team, X = m.X, Y = m.Y, Z = m.Z }).ToList(),
            }).ToList();
        W.WriteWools(Wizard.Intent, wools);
        Wizard.MarkDirty();
    }

    private async Task Paint()
    {
        if (canvas is null) return;
        await canvas.SetAuthorRegionsAsync(candidates.Select(c => (object)new
        {
            id = $"wool-cand-{c.Color}",
            type = "point",
            marker = true,
            primary = c.Included,
            color = W.Hex(c.Color),
            label = $"{c.Color}{(c.Included ? "" : " (excluded)")}",
            bounds = new { min_x = c.X - 0.5, min_z = c.Z - 0.5, max_x = c.X + 0.5, max_z = c.Z + 0.5 },
        }));
    }

    // The Y the wool rests at: one block above the floor under its detected pile (refY = the pile's base, so
    // an elevated wool room finds its own floor rather than a roof above it). Null when the column is void.
    private Task<int?> RestingYAsync(int x, int z, int refY) => ColumnFloor.RestingYAsync(Http, Slug, x, z, refY);

    private static string Str(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static double Dbl(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
}
