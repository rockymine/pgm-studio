using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Client.Models;
using PgmStudio.Geom;

namespace PgmStudio.Client.Pages.Configure;

// Shared model + helpers for the four Wools sub-steps (Objectives / Spawn / Monuments / Room). The wool
// list (intent.wools) is the slice all four edit: each parses it on init, mutates its part, writes it back
// and marks dirty. Positions follow the confirmed symmetry (orbit), exactly like spawns/protection; the
// wool COLOUR is author-assigned/confirmed, never defaulted to the team colour.
public static class WoolAuthoring
{
    public sealed class Team { public string Id = ""; public string Name = ""; public string Color = ""; }
    public sealed record Island(int Id, double[][] Ring, double[] Bounds);

    public sealed class Monument { public string Team = ""; public double X, Y, Z; }

    public sealed class Wool
    {
        public string Owner = "";
        public string Color = "";
        public double SpawnX, SpawnY, SpawnZ;
        public bool HasRoom;
        public double RoomMinX, RoomMinZ, RoomMaxX, RoomMaxZ;
        public List<Monument> Monuments = new();
    }

    // ── context (identical shape to the Teams/Spawn steps) ──────────────────────────────
    public static List<Team> LoadTeams(JsonObject intent)
    {
        var teams = new List<Team>();
        if (intent["teams"] is JsonArray arr)
            foreach (var t in arr.OfType<JsonObject>())
                teams.Add(new Team { Id = S(t, "id"), Name = S(t, "name"), Color = S(t, "color") });
        return teams;
    }

    public static (string? mode, double cx, double cz) Sym(JsonObject intent)
    {
        if (intent["symmetry"] is JsonObject s)
            return (s["mode"]?.GetValue<string>(), D(s, "centerX"), D(s, "centerZ"));
        return (null, 0, 0);
    }

    public static Dictionary<string, string> LoadIslandTeams(JsonObject intent)
    {
        var map = new Dictionary<string, string>();
        if (intent["islandTeams"] is JsonObject it)
            foreach (var kv in it)
                if (kv.Value?.GetValue<string>() is { Length: > 0 } v) map[kv.Key] = v;
        return map;
    }

    public static async Task<List<Island>> LoadIslandsAsync(HttpClient http, string slug)
    {
        try
        {
            var arr = await http.GetFromJsonAsync<JsonElement>($"api/map/{slug}/islands");
            var islands = new List<Island>();
            if (arr.ValueKind == JsonValueKind.Array)
                foreach (var e in arr.EnumerateArray())
                {
                    var id = e.GetProperty("id").GetInt32();
                    var bounds = e.TryGetProperty("bounds", out var b) && b.ValueKind == JsonValueKind.Array
                        ? b.EnumerateArray().Select(v => v.GetDouble()).ToArray() : Array.Empty<double>();
                    if (e.TryGetProperty("polygon", out var poly) && poly.TryGetProperty("coordinates", out var co)
                        && co.ValueKind == JsonValueKind.Array && co.GetArrayLength() > 0)
                        islands.Add(new Island(id,
                            co[0].EnumerateArray().Select(p => new[] { p[0].GetDouble(), p[1].GetDouble() }).ToArray(),
                            bounds));
                }
            return islands;
        }
        catch { return new(); }
    }

    /// <summary>Map XZ bounding box from the islands, padded — the area to scan for wool/monuments.</summary>
    public static (int minX, int minZ, int maxX, int maxZ) MapBox(List<Island> islands, int pad = 16)
    {
        if (islands.Count == 0 || islands.All(i => i.Bounds.Length < 4)) return (-256, -256, 256, 256);
        var b = islands.Where(i => i.Bounds.Length >= 4).ToList();
        return ((int)b.Min(i => i.Bounds[0]) - pad, (int)b.Min(i => i.Bounds[1]) - pad,
                (int)b.Max(i => i.Bounds[2]) + pad, (int)b.Max(i => i.Bounds[3]) + pad);
    }

    // ── the wool slice ──────────────────────────────────────────────────────────────────
    public static List<Wool> ParseWools(JsonObject intent)
    {
        var wools = new List<Wool>();
        if (intent["wools"] is not JsonArray arr) return wools;
        foreach (var w in arr.OfType<JsonObject>())
        {
            var sp = w["spawn"] as JsonObject;
            var wool = new Wool
            {
                Owner = S(w, "owner"),
                Color = S(w, "color"),
                SpawnX = D(sp, "x"), SpawnY = D(sp, "y"), SpawnZ = D(sp, "z"),
            };
            if (w["room"] is JsonObject r)
            {
                wool.HasRoom = true;
                wool.RoomMinX = D(r, "minX"); wool.RoomMinZ = D(r, "minZ");
                wool.RoomMaxX = D(r, "maxX"); wool.RoomMaxZ = D(r, "maxZ");
            }
            if (w["monuments"] is JsonArray ms)
                foreach (var m in ms.OfType<JsonObject>())
                {
                    var loc = m["location"] as JsonObject;
                    wool.Monuments.Add(new Monument { Team = S(m, "team"), X = D(loc, "x"), Y = D(loc, "y"), Z = D(loc, "z") });
                }
            wools.Add(wool);
        }
        return wools;
    }

    public static void WriteWools(JsonObject intent, IEnumerable<Wool> wools)
    {
        intent["wools"] = new JsonArray(wools.Select(w =>
        {
            var o = new JsonObject
            {
                ["owner"] = w.Owner,
                ["color"] = w.Color,
                ["spawn"] = new JsonObject { ["x"] = w.SpawnX, ["y"] = w.SpawnY, ["z"] = w.SpawnZ },
                ["monuments"] = new JsonArray(w.Monuments.Select(m => (JsonNode)new JsonObject
                {
                    ["team"] = m.Team,
                    ["location"] = new JsonObject { ["x"] = m.X, ["y"] = m.Y, ["z"] = m.Z },
                }).ToArray()),
            };
            o["room"] = w.HasRoom
                ? new JsonObject { ["minX"] = w.RoomMinX, ["minZ"] = w.RoomMinZ, ["maxX"] = w.RoomMaxX, ["maxZ"] = w.RoomMaxZ }
                : null;
            return (JsonNode)o;
        }).ToArray());
    }

    // ── colour / owner / orbit helpers ────────────────────────────────────────────────
    public static string Hex(string color) => GameColors.DyeHex(color);

    public static string NormColor(string? c) => (c ?? "").Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

    /// <summary>The team owning the island that contains (x,z), or null when (x,z) is off every tagged island.</summary>
    public static string? IslandTeamAt(double x, double z, List<Island> islands, Dictionary<string, string> islandTeams)
    {
        foreach (var isl in islands)
            if (Polygon.PointInRing(x, z, isl.Ring) && islandTeams.TryGetValue(isl.Id.ToString(), out var t)) return t;
        return null;
    }

    public static int OrbitOrder(string? mode) => Symmetry.Order(mode);

    public static (double x, double z) Orbit(double x, double z, string? mode, double cx, double cz, int k)
        => Symmetry.Point(x, z, mode, cx, cz, k);

    /// <summary>Block-centre snap (matches the spawn step): integer block + 0.5.</summary>
    public static double Snap(double v) => Math.Floor(v) + 0.5;

    internal static string S(JsonObject? o, string k) => o?[k]?.GetValue<string>() ?? "";
    internal static double D(JsonObject? o, string k)
    {
        if (o?[k] is JsonValue v) { if (v.TryGetValue(out double d)) return d; if (v.TryGetValue(out int i)) return i; }
        return 0;
    }
}
