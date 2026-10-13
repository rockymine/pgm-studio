using System.Globalization;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The King of the Flag slice of the declarative generator: one unowned <c>&lt;flag&gt;</c> per flag, paying its
/// points rate to whoever carries it, over one post — composite where it stands at several places, so it comes
/// back to one of them at random — and the two kits that carrying it gives and dropping it takes away
/// (<c>docs/pgm/flags.md</c>). Clear-then-build over its own output; a null slice leaves the map's flags alone.
/// </summary>
public static class FlagGenerator
{
    private const string Key = "flags";

    public static void Apply(Dict doc, MapIntent intent)
    {
        if (intent.Flags is null) return;
        Clear(doc);

        var flags = new List<object?>();
        var used = new HashSet<string>();
        foreach (var flag in intent.Flags)
        {
            if (flag.Posts.Count == 0) continue;
            var baseId = flag.Name.Length > 0 ? IntentNaming.Slug(flag.Name) : "flag";
            var id = baseId;
            for (var n = 2; !used.Add(id); n++) id = $"{baseId}-{n}";

            var (pickup, drop) = CarrierKits(flag, id);
            var kits = Kits(doc);
            kits.Add(pickup);
            if (drop is not null) kits.Add(drop);

            var entry = new Dict
            {
                ["id"] = id,
                ["color"] = flag.Color,
                ["points_rate"] = flag.PointsRate,
                ["pickup_kit"] = pickup["id"],
                ["post"] = Post(flag.Posts),
            };
            if (flag.Name.Length > 0) entry["name"] = flag.Name;
            if (drop is not null) entry["drop_kit"] = drop["id"];
            flags.Add(entry);
        }
        doc[Key] = flags;
    }

    /// <summary>One place is a single post; several are a composite whose next is chosen at random among
    /// them, starting at the first.</summary>
    private static Dict Post(List<FlagPostIntent> posts)
    {
        if (posts.Count == 1) return Place(posts[0]);
        return new Dict { ["posts"] = posts.Select(post => (object?)Place(post)).ToList() };
    }

    /// <summary>One place: the centre of the banner the build stood there, or the stated point where nothing
    /// was built.</summary>
    private static Dict Place(FlagPostIntent post)
    {
        var (x, y, z) = post.BannerBox is { } banner
            ? (banner.MinX + 0.5, (double)banner.MinY, banner.MinZ + 0.5)
            : (post.At.X, post.At.Y, post.At.Z);
        var place = new Dict { ["point"] = new Dict { ["x"] = x, ["y"] = y, ["z"] = z } };
        if (post.Name.Length > 0) place["name"] = post.Name;
        if (post.Yaw is { } yaw) place["yaw"] = yaw.ToString("0.##", CultureInfo.InvariantCulture);
        return place;
    }

    /// <summary>The kit carrying the flag gives, and the one dropping it gives to undo that — none where the
    /// first undoes itself. PGM sets max health back to 20 on respawn, so a carrier who dies is whole again.</summary>
    private static (Dict Pickup, Dict? Drop) CarrierKits(FlagIntent flag, string id)
    {
        var pickup = Kit($"{id}-carrier");
        switch (flag.Carrier)
        {
            case FlagCarriers.Absorption:
                pickup["force"] = true;
                pickup["effects"] = new List<object?> { Effect("absorption", "3s", 2) };
                return (pickup, null);
            case FlagCarriers.Boost:
                // Health boost adds four health a level from amplifier 0, so the hearts are taken up to the
                // even number above ten they reach.
                var boosted = Math.Max(12, flag.CarrierHearts + flag.CarrierHearts % 2);
                pickup["force"] = true;
                pickup["effects"] = new List<object?> { Effect("health boost", "oo", (boosted - 10) / 2 - 1) };
                var cleared = Kit($"{id}-dropped");
                cleared["force"] = true;
                cleared["effects"] = new List<object?> { Effect("health boost", "0", 0) };
                return (pickup, cleared);
            default:
                pickup["max_health"] = (double)Math.Clamp(flag.CarrierHearts, 1, 10) * 2;
                var restored = Kit($"{id}-dropped");
                restored["max_health"] = 20d;
                return (pickup, restored);
        }
    }

    private static Dict Kit(string id) => new()
    {
        ["id"] = id, ["force"] = false, ["clear"] = false,
        ["items"] = new List<object?>(), ["armor"] = new List<object?>(), ["effects"] = new List<object?>(),
    };

    private static Dict Effect(string type, string duration, int amplifier) =>
        new() { ["type"] = type, ["duration"] = duration, ["amplifier"] = amplifier };

    private static List<object?> Kits(Dict doc)
    {
        if (doc.GetValueOrDefault("kits") is not List<object?> kits) doc["kits"] = kits = [];
        return kits;
    }

    /// <summary>The flags this slice wrote and the kits they named.</summary>
    private static void Clear(Dict doc)
    {
        if (doc.GetValueOrDefault(Key) is not List<object?> previous) return;
        var kitIds = previous.OfType<Dict>()
            .SelectMany(flag => new[] { flag.GetValueOrDefault("pickup_kit") as string, flag.GetValueOrDefault("drop_kit") as string })
            .OfType<string>().ToHashSet();
        Kits(doc).RemoveAll(kit => kit is Dict d && d.GetValueOrDefault("id") is string kitId && kitIds.Contains(kitId));
        doc.Remove(Key);
    }
}
