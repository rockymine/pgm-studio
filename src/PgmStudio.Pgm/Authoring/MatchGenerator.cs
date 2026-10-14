using System.Globalization;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;

/// <summary>
/// How the match ends and how the dead come back (<c>docs/pgm/match.md</c>): the time limit — the corpus's clock on a
/// deathmatch stating no limit — the map's own respawn, a blitz's lives, rage, and the kill height — a portal under the board dropping whoever falls past it
/// into the void. A rule the intent leaves null is left as the map has it; the kill height is this slice's own and
/// is rebuilt every time.
/// </summary>
public static class MatchGenerator
{
    /// <summary>The region under the kill height, and the portal on it.</summary>
    public const string KillHeightRegion = "kill-height";

    /// <summary>Where the kill height drops a player: under the world's floor, where the void kills.</summary>
    public const int VoidY = -64;

    public static void Apply(Dict doc, MapIntent intent)
    {
        if (intent.TimeLimit is { } time) Set(doc, "time", time.Duration.Length == 0 ? null : TimeLimit(time));
        // A deathmatch stating no limit of either kind would never end, so it ends on the corpus's clock.
        else if (intent.KillPoints > 0 && intent.ScoreLimit is null && !doc.ContainsKey("time"))
            doc["time"] = new Dict { ["duration"] = ObjectiveDefaults.DeathmatchTimeLimit };
        if (intent.Respawn is { } respawn) doc["respawn"] = Respawn(respawn);
        if (intent.Lives is { } lives) Set(doc, "blitz", lives > 0 ? new Dict { ["lives"] = (long)lives } : null);
        if (intent.Rage is { } rage) Set(doc, "rage", rage ? true : null);
        KillHeight(doc, intent.KillHeight);
    }

    private static Dict TimeLimit(TimeLimitIntent time)
    {
        var t = new Dict { ["duration"] = time.Duration };
        if (time.Result != TimeResults.Default) t["result"] = time.Result;
        return t;
    }

    private static Dict Respawn(RespawnIntent respawn)
    {
        var r = new Dict();
        if (respawn.Delay.Length > 0) r["delay"] = respawn.Delay;
        if (respawn.Auto) r["auto"] = true;
        if (respawn.Spectate) r["spectate"] = true;
        return r;
    }

    // A player below the height is moved to the void's depth, keeping where they were across the board, and dies
    // there with what they carry; an observer flies on.
    private static void KillHeight(Dict doc, int? height)
    {
        var regions = DocAccess.Regions(doc);
        regions.Remove(KillHeightRegion);
        if (doc.GetValueOrDefault("portals") is List<object?> portals)
            portals.RemoveAll(portal => portal is Dict d && d.GetValueOrDefault("region") as string == KillHeightRegion);
        if (height is not { } y) return;

        regions[KillHeightRegion] = Serializer.RegionToDict(new Region { Id = KillHeightRegion, Type = "below", HalfY = y });
        ObjectiveRegion.List(doc, "portals").Add(new Dict
        {
            ["region"] = KillHeightRegion,
            ["y"] = "@" + VoidY.ToString(CultureInfo.InvariantCulture),
            ["sound"] = false,
            ["observers"] = "never",
        });
    }

    private static void Set(Dict doc, string key, object? value)
    {
        if (value is null) doc.Remove(key);
        else doc[key] = value;
    }
}
