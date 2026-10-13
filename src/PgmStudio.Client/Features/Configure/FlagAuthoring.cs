using System.Text.Json.Nodes;
using PgmStudio.Vocabulary;

namespace PgmStudio.Client.Features.Configure;

using Ctx = AuthoringContext;

// The flag slice (intent.flags) the Flags phase authors: a flag is a name, the team defending it or nobody, a
// colour, the posts it stands at — the first is where it starts, and PGM loads it only where a banner stands
// there — and what carrying it does to the carrier. A flag nobody owns is King of the Flag's and pays while
// carried; a team's flag is Capture the Flag's, takes its team's colour and is captured in another team's net.
// Everything else a flag in PGM can say is the generator's convention (docs/pgm/flags.md §7).
public static class FlagAuthoring
{
    /// <summary>The numbers a flag takes when the author states none. Served by <c>GET /flag-suggestions</c> so
    /// the one definition (<c>ObjectiveDefaults</c>) is not copied here.</summary>
    public sealed record Defaults(double PointsRate, string Carrier, int CarrierHearts)
    {
        public static readonly Defaults Empty = new(1, FlagCarriers.Lowered, 6);
    }

    public sealed class Post
    {
        public string Name = "";
        /// <summary>Where the flag stands: the block a banner is in, at its centre.</summary>
        public double X, Y, Z;
        public double? Yaw;
    }

    public sealed class Flag
    {
        public string Name = "";
        /// <summary>The team defending it; empty for a flag nobody owns.</summary>
        public string Team = "";
        /// <summary>The banner's dye; empty on a team's flag, which takes its team's colour.</summary>
        public string Color = "white";
        public List<Post> Posts = new();
        public double PointsRate;
        public string Carrier = FlagCarriers.Lowered;
        public int CarrierHearts;
    }

    /// <summary>The block a post stands its flag in.</summary>
    public static (int X, int Y, int Z) Block(Post post)
        => ((int)Math.Floor(post.X), (int)Math.Floor(post.Y), (int)Math.Floor(post.Z));

    public static List<Flag> ParseFlags(JsonObject intent, Defaults defaults)
    {
        var flags = new List<Flag>();
        if (intent["flags"] is not JsonArray arr) return flags;
        foreach (var f in arr.OfType<JsonObject>())
        {
            var team = Ctx.S(f, "team");
            var flag = new Flag
            {
                Name = Ctx.S(f, "name"),
                Team = team,
                Color = Ctx.S(f, "color") is { Length: > 0 } color ? color : team.Length > 0 ? "" : "white",
                PointsRate = f["pointsRate"] is null ? defaults.PointsRate : Ctx.D(f, "pointsRate"),
                Carrier = Ctx.S(f, "carrier") is { Length: > 0 } carrier ? carrier
                    : team.Length > 0 ? FlagCarriers.None : defaults.Carrier,
                CarrierHearts = Ctx.I(f, "carrierHearts", defaults.CarrierHearts),
            };
            foreach (var p in (f["posts"] as JsonArray ?? []).OfType<JsonObject>())
            {
                var at = p["at"] as JsonObject;
                flag.Posts.Add(new Post
                {
                    Name = Ctx.S(p, "name"),
                    X = Ctx.D(at, "x"), Y = Ctx.D(at, "y"), Z = Ctx.D(at, "z"),
                    Yaw = p["yaw"] is null ? null : Ctx.D(p, "yaw"),
                });
            }
            flags.Add(flag);
        }
        return flags;
    }

    public static void WriteFlags(JsonObject intent, IEnumerable<Flag> flags)
    {
        // A flag's name is its identity, and a post's block is its own: keys any entry the compiler or a
        // driver wrote (a post's layer) ride onto the rewrite.
        var carry = IntentSlice.Carrier(intent, "flags", entry => Ctx.S(entry, "name"));
        intent["flags"] = new JsonArray(flags.Select(flag =>
        {
            var o = carry(flag.Name);
            var priorPosts = (o["posts"] as JsonArray ?? []).OfType<JsonObject>()
                .GroupBy(PostKey).ToDictionary(group => group.Key, group => group.First());
            o["name"] = flag.Name;
            // A team's flag takes its team's colour and pays nothing while carried, so it states neither.
            if (flag.Team.Length > 0) o["team"] = flag.Team; else o.Remove("team");
            if (flag.Color.Length > 0) o["color"] = flag.Color; else o.Remove("color");
            if (flag.Team.Length > 0) o.Remove("pointsRate"); else o["pointsRate"] = flag.PointsRate;
            o["carrier"] = flag.Carrier;
            o["carrierHearts"] = flag.CarrierHearts;
            o["posts"] = new JsonArray(flag.Posts.Select(post =>
            {
                var p = priorPosts.TryGetValue(Key(Block(post)), out var kept) ? (JsonObject)kept.DeepClone() : new JsonObject();
                p["name"] = post.Name;
                p["at"] = new JsonObject { ["x"] = post.X, ["y"] = post.Y, ["z"] = post.Z };
                p["yaw"] = post.Yaw;
                return (JsonNode)p;
            }).ToArray());
            return (JsonNode)o;
        }).ToArray());
    }

    private static string PostKey(JsonObject post)
    {
        var at = post["at"] as JsonObject;
        return Key(((int)Math.Floor(Ctx.D(at, "x")), (int)Math.Floor(Ctx.D(at, "y")), (int)Math.Floor(Ctx.D(at, "z"))));
    }

    private static string Key((int X, int Y, int Z) block) => $"{block.X}|{block.Y}|{block.Z}";
}
