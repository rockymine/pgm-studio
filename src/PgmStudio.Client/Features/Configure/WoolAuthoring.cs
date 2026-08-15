using System.Text.Json.Nodes;
using PgmStudio.Client.Models;

namespace PgmStudio.Client.Features.Configure;

using Ctx = AuthoringContext;
using PgmStudio.Geom;

// The wool slice (intent.wools) the four Wools steps (Objectives / Spawn / Monuments / Room) share: each
// parses it on init, mutates its part, writes it back and marks dirty. The teams / symmetry / islands every
// step reads first live in AuthoringContext, which the core steps read too. Positions follow the confirmed
// symmetry (orbit), exactly like spawns/protection; the wool COLOUR is author-assigned/confirmed, never
// defaulted to the team colour.
public static class WoolAuthoring
{
    public sealed class Monument { public string Team = ""; public double X, Y, Z; }

    public sealed class Wool
    {
        public string Owner = "";
        public string Color = "";
        public double SpawnX, SpawnY, SpawnZ;
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
        foreach (var w in arr.OfType<JsonObject>())
        {
            var sp = w["spawn"] as JsonObject;
            var wool = new Wool
            {
                Owner = Ctx.S(w, "owner"),
                Color = Ctx.S(w, "color"),
                SpawnX = Ctx.D(sp, "x"), SpawnY = Ctx.D(sp, "y"), SpawnZ = Ctx.D(sp, "z"),
            };
            wool.Rooms.AddRange(ParseRects(w["room"]));
            if (w["monuments"] is JsonArray ms)
                foreach (var m in ms.OfType<JsonObject>())
                {
                    var loc = m["location"] as JsonObject;
                    wool.Monuments.Add(new Monument { Team = Ctx.S(m, "team"), X = Ctx.D(loc, "x"), Y = Ctx.D(loc, "y"), Z = Ctx.D(loc, "z") });
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
        var carry = IntentSlice.Carrier(intent, "wools", w => w["color"]?.GetValue<string>());

        intent["wools"] = new JsonArray(wools.Select(w =>
        {
            var o = carry(w.Color);
            o["owner"] = w.Owner;
            o["color"] = w.Color;
            o["spawn"] = new JsonObject { ["x"] = w.SpawnX, ["y"] = w.SpawnY, ["z"] = w.SpawnZ };
            o["monuments"] = new JsonArray(w.Monuments.Select(m => (JsonNode)new JsonObject
            {
                ["team"] = m.Team,
                ["location"] = new JsonObject { ["x"] = m.X, ["y"] = m.Y, ["z"] = m.Z },
            }).ToArray());
            o["room"] = new JsonArray(w.Rooms.Select(RectNode).ToArray());
            return (JsonNode)o;
        }).ToArray());
    }

    // The room is a JSON array of {minX,minZ,maxX,maxZ}; tolerate a legacy single object too.
    public static IEnumerable<Rect> ParseRects(JsonNode? node) => node switch
    {
        JsonArray arr => arr.OfType<JsonObject>().Select(RectOf),
        JsonObject obj => new[] { RectOf(obj) },
        _ => Enumerable.Empty<Rect>(),
    };

    private static Rect RectOf(JsonObject r) => new(Ctx.D(r, "minX"), Ctx.D(r, "minZ"), Ctx.D(r, "maxX"), Ctx.D(r, "maxZ"));
    private static JsonNode RectNode(Rect r) => new JsonObject { ["minX"] = r.MinX, ["minZ"] = r.MinZ, ["maxX"] = r.MaxX, ["maxZ"] = r.MaxZ };

    // ── colour helpers ────────────────────────────────────────────────────────────────
    public static string Hex(string color) => GameColors.DyeHex(color);

    public static string NormColor(string? c) => (c ?? "").Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
}
