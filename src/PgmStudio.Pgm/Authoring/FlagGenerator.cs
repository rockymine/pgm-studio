using System.Globalization;
using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Editing;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The flag slice of the declarative generator (<c>docs/pgm/flags.md</c>). A flag nobody owns is King of the
/// Flag's: one <c>&lt;flag&gt;</c> paying its points rate to whoever carries it, over one post — composite where it
/// stands at several places, so it comes back to one of them at random — and the kits carrying it gives and
/// dropping it takes away. A team's flag is Capture the Flag's: owned, carried off by any other team, and captured
/// in that team's net — a region round its own flag's first post taking every other team's flag. Clear-then-build
/// over its own output; a null slice leaves the map's flags alone.
/// </summary>
public static class FlagGenerator
{
    private const string Key = "flags";
    private const string NetKey = "flag_nets";
    private const string TakersSuffix = "-takers";
    private const string NetSuffix = "net";
    private const string HomeSuffix = "-home";

    public static void Apply(Dict doc, MapIntent intent)
    {
        if (intent.Flags is null) return;
        Clear(doc);

        var flags = new List<object?>();
        var written = new List<(FlagIntent Flag, string Id)>();
        var used = new HashSet<string>();
        foreach (var flag in intent.Flags)
        {
            if (flag.Posts.Count == 0) continue;
            var team = flag.Owned ? intent.Teams?.FirstOrDefault(t => IntentNaming.TeamId(t.Id) == IntentNaming.TeamId(flag.Team)) : null;
            var name = flag.Name.Length > 0 ? flag.Name
                : flag.Owned ? $"{(team is { Name.Length: > 0 } ? team.Name : TeamPalette.Label(flag.Team))} Flag" : "";
            var baseId = name.Length > 0 ? IntentNaming.Slug(name) : "flag";
            var id = baseId;
            for (var n = 2; !used.Add(id); n++) id = $"{baseId}-{n}";

            var entry = new Dict
            {
                ["id"] = id,
                ["color"] = flag.BannerColor(intent.Teams),
                ["post"] = Post(flag),
            };
            if (name.Length > 0) entry["name"] = name;
            if (flag.PaysPerSecond > 0) entry["points_rate"] = flag.PaysPerSecond;
            if (flag.Owned)
            {
                var owner = IntentNaming.TeamId(flag.Team);
                entry["owner"] = owner;
                // PGM lets anyone pick a flag up, its own team included, so a team's flag names who may.
                FilterEditor.CreateFilter(doc, new Dict { ["id"] = id + TakersSuffix, ["type"] = "not", ["child"] = owner });
                entry["pickup_filter"] = id + TakersSuffix;
            }

            if (CarrierKits(flag, id) is var (pickup, drop))
            {
                var kits = Kits(doc);
                kits.Add(pickup);
                entry["pickup_kit"] = pickup["id"];
                if (drop is not null)
                {
                    kits.Add(drop);
                    entry["drop_kit"] = drop["id"];
                }
            }
            flags.Add(entry);
            written.Add((flag, id));
        }
        doc[Key] = flags;

        var nets = Nets(doc, written);
        if (nets.Count > 0) doc[NetKey] = nets;
    }

    /// <summary>One net per team owning a flag: its own flag's net box, or a box round that flag's first post,
    /// taking every flag another team owns. A team with no other team's flag to take has none, since a net naming
    /// no flag takes every flag. A team whose flag captures only at <see cref="FlagCaptures.Home"/> captures while
    /// every flag of its own stands at a post.</summary>
    private static List<object?> Nets(Dict doc, List<(FlagIntent Flag, string Id)> written)
    {
        var nets = new List<object?>();
        var owned = written.Where(w => w.Flag.Owned).ToList();
        var netted = new HashSet<string>();
        foreach (var (flag, id) in owned)
        {
            var team = IntentNaming.TeamId(flag.Team);
            if (!netted.Add(team)) continue;
            var takes = owned.Where(other => IntentNaming.TeamId(other.Flag.Team) != team).Select(other => (object?)other.Id).ToList();
            if (takes.Count == 0) continue;
            var region = ObjectiveRegion.Emit(doc, id, flag.Net ?? NetRound(flag.Posts[0]), NetSuffix);
            var net = new Dict
            {
                ["region"] = region,
                ["points"] = (double)ObjectiveDefaults.FlagNetPoints,
                ["flags"] = takes,
            };
            var own = owned.Where(other => IntentNaming.TeamId(other.Flag.Team) == team).ToList();
            if (own.Any(other => other.Flag.Capture == FlagCaptures.Home)) net["capture_filter"] = HomeFilter(doc, own.Select(other => other.Id).ToList());
            nets.Add(net);
        }
        return nets;
    }

    /// <summary>A filter holding while each of <paramref name="flagIds"/> stands at a post — PGM's returned state —
    /// one per flag, under an <c>all</c> where there are several.</summary>
    private static string HomeFilter(Dict doc, List<string> flagIds)
    {
        foreach (var flagId in flagIds)
            FilterEditor.CreateFilter(doc, new Dict { ["id"] = flagId + HomeSuffix, ["type"] = "flag-returned", ["objective"] = flagId });
        if (flagIds.Count == 1) return flagIds[0] + HomeSuffix;
        var all = flagIds[0] + "-all" + HomeSuffix;
        FilterEditor.CreateFilter(doc, new Dict
        {
            ["id"] = all, ["type"] = "all", ["children"] = flagIds.Select(flagId => (object?)(flagId + HomeSuffix)).ToList(),
        });
        return all;
    }

    /// <summary>A box round a post: the block its banner stands in, reaching out either side and up.</summary>
    public static BlockBox NetRound(FlagPostIntent post)
    {
        var (x, y, z) = post.BannerBox is { } banner
            ? (banner.MinX, banner.MinY, banner.MinZ)
            : ((int)Math.Floor(post.At.X), (int)Math.Floor(post.At.Y), (int)Math.Floor(post.At.Z));
        const int reach = ObjectiveDefaults.FlagNetReach;
        return new BlockBox(x - reach, y, z - reach, x + reach, y + ObjectiveDefaults.FlagNetHeight - 1, z + reach);
    }

    /// <summary>One place is a single post; several are a composite whose next is chosen at random among
    /// them, starting at the first.</summary>
    private static Dict Post(FlagIntent flag)
    {
        var post = flag.Posts.Count == 1
            ? Place(flag.Posts[0])
            : new Dict { ["posts"] = flag.Posts.Select(post => (object?)Place(post)).ToList() };
        if ((flag.RecoverTime ?? (flag.Owned ? ObjectiveDefaults.FlagRecoverTime : null)) is { Length: > 0 } recover)
            post["recover_time"] = recover;
        return post;
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
    private static (Dict Pickup, Dict? Drop)? CarrierKits(FlagIntent flag, string id)
    {
        var pickup = Kit($"{id}-carrier");
        switch (flag.CarrierEffect)
        {
            case FlagCarriers.None:
                return null;
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

    /// <summary>The flags this slice wrote, the kits and filters they named, and the nets and their regions.</summary>
    private static void Clear(Dict doc)
    {
        if (doc.GetValueOrDefault(NetKey) is List<object?> nets)
        {
            var netFilters = DocAccess.Filters(doc);
            foreach (var home in nets.OfType<Dict>().Select(net => net.GetValueOrDefault("capture_filter") as string).OfType<string>())
            {
                if ((netFilters.GetValueOrDefault(home) as Dict)?.GetValueOrDefault("children") is List<object?> children)
                    foreach (var child in children.OfType<string>()) netFilters.Remove(child);
                netFilters.Remove(home);
            }
            ObjectiveRegion.Clear(doc, nets);
            doc.Remove(NetKey);
        }
        if (doc.GetValueOrDefault(Key) is not List<object?> previous) return;
        var written = previous.OfType<Dict>().ToList();
        var kitIds = written
            .SelectMany(flag => new[] { flag.GetValueOrDefault("pickup_kit") as string, flag.GetValueOrDefault("drop_kit") as string })
            .OfType<string>().ToHashSet();
        Kits(doc).RemoveAll(kit => kit is Dict d && d.GetValueOrDefault("id") is string kitId && kitIds.Contains(kitId));
        var filters = DocAccess.Filters(doc);
        foreach (var filter in written.Select(flag => flag.GetValueOrDefault("pickup_filter") as string).OfType<string>())
            if (filter.EndsWith(TakersSuffix, StringComparison.Ordinal)) filters.Remove(filter);
        doc.Remove(Key);
    }
}
