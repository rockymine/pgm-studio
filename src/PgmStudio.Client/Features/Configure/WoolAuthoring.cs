using System.Text.Json.Nodes;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Features.Configure;

using Ctx = AuthoringContext;
using PgmStudio.Geom;

// The wool slice (intent.wools) the four Wools steps (Objectives / Spawn / Monuments / Room) share: each
// parses it on init, mutates its part, writes it back and marks dirty. A wool's supply is a spawner unless the
// spawn step finds a chest of its colour in its room and the author keeps it. The teams / symmetry / islands every
// step reads first live in AuthoringContext, which the core steps read too. Positions follow the confirmed
// symmetry (orbit), exactly like spawns/protection. A wool stating no colour is its owner's colour, which is
// what the export writes, so the steps read and match it as that colour.
public static class WoolAuthoring
{
    public sealed class Monument { public string Team = ""; public double X, Y, Z; }

    public sealed class Wool
    {
        public string Owner = "";
        public string Color = "";
        public double SpawnX, SpawnY, SpawnZ;
        // What the attackers take it from (a WoolSupplies word); null where no step has stated it yet, which the
        // studio reads as a spawner.
        public string? Supply;
        // The room footprint as a union of rectangles (empty = no room yet). For an authored wool these are
        // the drawn pieces; for an orbit copy they're derived from its authored partner.
        public List<Rect> Rooms = new();
        public List<Monument> Monuments = new();

        public bool HasRoom => Rooms.Count > 0;
    }

    // ── the wool slice ──────────────────────────────────────────────────────────────────
    public static List<Wool> ParseWools(JsonObject intent)
    {
        var wools = new List<Wool>();
        if (intent["wools"] is not JsonArray arr) return wools;
        var teams = Ctx.LoadTeams(intent);
        foreach (var w in arr.OfType<JsonObject>())
        {
            var sp = w["spawn"] as JsonObject;
            var wool = new Wool
            {
                Owner = Ctx.S(w, "owner"),
                Color = ColorOf(w, teams),
                SpawnX = Ctx.D(sp, "x"), SpawnY = Ctx.D(sp, "y"), SpawnZ = Ctx.D(sp, "z"),
                Supply = w["supply"]?.GetValue<string>(),
            };
            wool.Rooms.AddRange(ParseRects(w["protection"]));
            if (w["monuments"] is JsonArray ms)
                foreach (var m in ms.OfType<JsonObject>())
                {
                    // One monument for each capturing team (OB32): a second stated for a team is the one the
                    // step never showed, so the first is kept and the next save states the wool as shown.
                    var team = Ctx.S(m, "team");
                    if (wool.Monuments.Any(kept => kept.Team == team)) continue;
                    var loc = m["location"] as JsonObject;
                    wool.Monuments.Add(new Monument { Team = team, X = Ctx.D(loc, "x"), Y = Ctx.D(loc, "y"), Z = Ctx.D(loc, "z") });
                }
            wools.Add(wool);
        }
        return wools;
    }

    public static void WriteWools(JsonObject intent, IEnumerable<Wool> wools)
    {
        // Colour is the wool's identity across all four steps, so it is what a rewritten entry is matched on.
        // The four steps between them model owner / colour / spawn / monuments / room and nothing else; the
        // plan compiler's `piece` and `entries` (which size the stamped cage and cut its doors) ride through
        // on the entry being replaced rather than being dropped by a from-scratch rebuild (IntentSlice).
        var teams = Ctx.LoadTeams(intent);
        var carry = IntentSlice.Carrier(intent, "wools", w => ColorOf(w, teams));

        intent["wools"] = new JsonArray(wools.Select(w =>
        {
            var o = carry(w.Color);
            o["owner"] = w.Owner;
            o["color"] = w.Color;
            o["spawn"] = new JsonObject { ["x"] = w.SpawnX, ["y"] = w.SpawnY, ["z"] = w.SpawnZ };
            if (w.Supply is { } supply) o["supply"] = supply;
            o["monuments"] = new JsonArray(w.Monuments.Select(m => (JsonNode)new JsonObject
            {
                ["team"] = m.Team,
                ["location"] = new JsonObject { ["x"] = m.X, ["y"] = m.Y, ["z"] = m.Z },
            }).ToArray());
            o["protection"] = new JsonArray(w.Rooms.Select(RectNode).ToArray());
            return (JsonNode)o;
        }).ToArray());
    }

    // The room is the wool's `protection`, a JSON array of {minX,minZ,maxX,maxZ}; tolerate a legacy single object too.
    public static IEnumerable<Rect> ParseRects(JsonNode? node) => node switch
    {
        JsonArray arr => arr.OfType<JsonObject>().Select(RectOf),
        JsonObject obj => new[] { RectOf(obj) },
        _ => Enumerable.Empty<Rect>(),
    };

    private static Rect RectOf(JsonObject r) => new(Ctx.D(r, "minX"), Ctx.D(r, "minZ"), Ctx.D(r, "maxX"), Ctx.D(r, "maxZ"));
    private static JsonNode RectNode(Rect r) => new JsonObject { ["minX"] = r.MinX, ["minZ"] = r.MinZ, ["maxX"] = r.MaxX, ["maxZ"] = r.MaxZ };

    // ── colour helpers ────────────────────────────────────────────────────────────────
    /// <summary>The colour a stated wool is: its own, else its owner team's.</summary>
    public static string ColorOf(JsonObject wool, IEnumerable<Ctx.Team> teams)
    {
        var stated = NormColor(Ctx.S(wool, "color"));
        if (stated.Length > 0) return stated;
        var owner = Ctx.S(wool, "owner");
        return NormColor(teams.FirstOrDefault(team => team.Id == owner)?.Color);
    }

    public static string Hex(string color) => GameColors.DyeHex(color);

    /// <summary>A dye colour as a person reads it, in sentence case: <c>light_blue</c> is "Light blue".</summary>
    public static string Name(string? color)
    {
        var norm = NormColor(color);
        var label = GameColors.DyeColors.FirstOrDefault(dye => NormColor(dye.Value) == norm).Label;
        if (string.IsNullOrEmpty(label)) label = norm.Replace('_', ' ');
        return label.Length == 0 ? "" : char.ToUpperInvariant(label[0]) + label[1..].ToLowerInvariant();
    }

    public static string NormColor(string? c) => (c ?? "").Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
}
