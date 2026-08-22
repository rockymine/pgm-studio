using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using PgmStudio.Contracts;
using PgmStudio.Geom;

namespace PgmStudio.Client.Features.Configure;

/// <summary>
/// The map context every Configure step reads before it edits its own slice: the teams, the confirmed
/// symmetry, the islands and which team each belongs to, plus the position helpers that follow from them.
/// None of it is specific to one objective — a core step needs the same teams and the same orbit a wool
/// step does — so it lives here rather than in any one objective's authoring helper.
/// </summary>
public static class AuthoringContext
{
    /// <summary>One team, as the <b>intent</b> states it — the three fields every step that draws a team
    /// reads. The Edit tool's own team record is a different shape over a different document: it reads
    /// <c>GET /map/{slug}</c> and carries the dye colour and the player caps the intent has no field
    /// for.</summary>
    public sealed class Team { public string Id = ""; public string Name = ""; public string Color = ""; }

    /// <summary>The teams an intent states, in the order it states them. Nine steps read them and every one
    /// reads them here: a step walking the document itself is a second reader free to disagree about what a
    /// missing field means.</summary>
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

    /// <summary>The map's landmasses, as <c>GET /map/{slug}/islands</c> answers them. A map with no scan
    /// data answers none rather than failing, which is the state every step here starts in.</summary>
    public static async Task<List<IslandDto>> LoadIslandsAsync(HttpClient http, string slug)
    {
        try { return await http.GetFromJsonAsync<List<IslandDto>>($"api/map/{slug}/islands") ?? []; }
        catch { return []; }
    }

    /// <summary>Map XZ bounding box from the islands, padded — the area to scan for objectives.</summary>
    public static (int minX, int minZ, int maxX, int maxZ) MapBox(List<IslandDto> islands, int pad = 16)
    {
        var bounded = islands.Where(i => i.Bounds.Count >= 4).ToList();
        if (bounded.Count == 0) return (-256, -256, 256, 256);
        return (bounded.Min(i => i.Bounds[0]) - pad, bounded.Min(i => i.Bounds[1]) - pad,
                bounded.Max(i => i.Bounds[2]) + pad, bounded.Max(i => i.Bounds[3]) + pad);
    }

    /// <summary>The team owning the island that contains (x,z), or null when (x,z) is off every tagged island.</summary>
    public static string? IslandTeamAt(double x, double z, List<IslandDto> islands, Dictionary<string, string> islandTeams)
    {
        foreach (var isl in islands)
            if (Polygon.PointInRing(x, z, isl.Ring()) && islandTeams.TryGetValue(isl.Id.ToString(), out var t)) return t;
        return null;
    }

    /// <summary>An island's outer ring, in the <c>[x, z]</c> pairs the canvas and the point tests read. A
    /// scan that wrote no polygon answers an empty ring, which contains nothing.</summary>
    public static IReadOnlyList<IReadOnlyList<double>> Ring(this IslandDto island)
        => island.Polygon?.Coordinates is { Count: > 0 } rings ? rings[0] : [];

    /// <summary>An island's middle, east–west. The stored maxima are inclusive whole blocks, so the middle
    /// of the ground it covers is half a block past their mean.</summary>
    public static double CentreX(this IslandDto island)
        => island.Bounds.Count >= 4 ? (island.Bounds[0] + island.Bounds[2] + 1) / 2.0 : 0;

    /// <summary>An island's middle, north–south.</summary>
    public static double CentreZ(this IslandDto island)
        => island.Bounds.Count >= 4 ? (island.Bounds[1] + island.Bounds[3] + 1) / 2.0 : 0;

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

    internal static int I(JsonObject? o, string k, int fallback = 0)
    {
        if (o?[k] is JsonValue v) { if (v.TryGetValue(out int i)) return i; if (v.TryGetValue(out double d)) return (int)Math.Round(d); }
        return fallback;
    }

    internal static bool B(JsonObject? o, string k)
        => o?[k] is JsonValue v && v.TryGetValue(out bool b) && b;
}
