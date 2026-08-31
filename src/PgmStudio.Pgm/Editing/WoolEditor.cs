namespace PgmStudio.Pgm.Editing;

using PgmStudio.Vocabulary;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Wool + monument CRUD on the grouped wool format. Ids are
/// content-derived (wool id = colour slug; monument id = <c>colour-team</c>), matching the serializer.
/// </summary>
public static class WoolEditor
{
    public static Dict AddWool(Dict data, Dict payload)
    {
        EnsureGrouped(data);
        var color = WoolColors.Normalize(payload.GetValueOrDefault("color") as string ?? "white");
        if (!WoolColors.IsColor(color)) throw EditException.Unreadable($"invalid wool color '{color}'", "color");
        if (Wools(data).OfType<Dict>().Any(w => w.GetValueOrDefault("color") as string == color))
            throw EditException.Conflict($"wool color '{color}' already exists", [color]);
        var wool = new Dict
        {
            ["id"] = color, ["color"] = color, ["team"] = null, ["location"] = null,
            ["wool_room_region"] = null, ["monuments"] = new List<object?>(),
        };
        EnsureList(data, "wools").Add(wool);
        return new Dict { ["wool"] = wool };
    }

    public static Dict UpdateWool(Dict data, string woolId, Dict payload)
    {
        EnsureGrouped(data);
        var wool = FindWool(data, woolId);
        if (payload.ContainsKey("color"))
        {
            var color = WoolColors.Normalize(payload["color"] as string ?? "");
            if (!WoolColors.IsColor(color)) throw EditException.Unreadable($"invalid wool color '{color}'", "color");
            if (color != wool.GetValueOrDefault("color") as string && Wools(data).OfType<Dict>().Any(w => !ReferenceEquals(w, wool) && w.GetValueOrDefault("color") as string == color))
                throw EditException.Conflict($"wool color '{color}' already exists", [color]);
            wool["color"] = color;
            wool["id"] = color;
            foreach (var mon in Monuments(wool)) mon["id"] = MonumentId(color, mon.GetValueOrDefault("team") as string ?? "");
        }
        if (payload.ContainsKey("team")) wool["team"] = NullIfEmpty((payload["team"] as string ?? "").Trim());
        if (payload.ContainsKey("location")) wool["location"] = payload["location"];
        if (payload.ContainsKey("wool_room_region")) wool["wool_room_region"] = NullIfEmpty((payload["wool_room_region"] as string ?? "").Trim());
        return new Dict { ["wool"] = wool };
    }

    public static Dict DeleteWool(Dict data, string woolId)
    {
        EnsureGrouped(data);
        var wools = Wools(data);
        if (!wools.OfType<Dict>().Any(w => w.GetValueOrDefault("id") as string == woolId))
            throw EditException.NoSuchSubject($"wool '{woolId}' not found");
        data["wools"] = wools.Where(w => (w as Dict)?.GetValueOrDefault("id") as string != woolId).ToList();
        return new Dict();
    }

    public static Dict AddMonument(Dict data, string woolId, Dict payload)
    {
        EnsureGrouped(data);
        var wool = FindWool(data, woolId);
        var team = payload.GetValueOrDefault("team") as string ?? "";
        if (team.Length > 0 && Monuments(wool).Any(m => m.GetValueOrDefault("team") as string == team))
            throw EditException.Conflict($"monument for team '{team}' already exists on this wool", [team]);
        var mon = new Dict
        {
            ["id"] = MonumentId(wool.GetValueOrDefault("color") as string ?? "", team),
            ["team"] = team,
            ["location"] = payload.GetValueOrDefault("location"),
            ["monument_region"] = NullIfEmpty((payload.GetValueOrDefault("monument_region") as string ?? "").Trim()),
        };
        EnsureList(wool, "monuments").Add(mon);
        return new Dict { ["monument"] = mon };
    }

    public static Dict UpdateMonument(Dict data, string woolId, string monId, Dict payload)
    {
        EnsureGrouped(data);
        var wool = FindWool(data, woolId);
        var mon = FindMonument(wool, monId);
        if (payload.ContainsKey("team"))
        {
            var newTeam = payload["team"] as string ?? "";
            if (newTeam != mon.GetValueOrDefault("team") as string && Monuments(wool).Any(m => !ReferenceEquals(m, mon) && m.GetValueOrDefault("team") as string == newTeam))
                throw EditException.Conflict($"monument for team '{newTeam}' already exists on this wool", [newTeam]);
            mon["team"] = newTeam;
            mon["id"] = MonumentId(wool.GetValueOrDefault("color") as string ?? "", newTeam);
        }
        if (payload.ContainsKey("location")) mon["location"] = payload["location"];
        if (payload.ContainsKey("monument_region")) mon["monument_region"] = NullIfEmpty((payload["monument_region"] as string ?? "").Trim());
        return new Dict { ["monument"] = mon };
    }

    public static Dict DeleteMonument(Dict data, string woolId, string monId)
    {
        EnsureGrouped(data);
        var wool = FindWool(data, woolId);
        if (!Monuments(wool).Any(m => m.GetValueOrDefault("id") as string == monId))
            throw EditException.NoSuchSubject($"monument '{monId}' not found in wool '{woolId}'");
        wool["monuments"] = Monuments(wool).Where(m => m.GetValueOrDefault("id") as string != monId).Cast<object?>().ToList();
        return new Dict();
    }

    // ── grouping / inference ──────────────────────────────────────────────────────
    private static void EnsureGrouped(Dict data)
    {
        var wools = Wools(data);
        if (wools.Count > 0 && IsOldFormat(wools)) data["wools"] = MigrateToGrouped(wools);
        InferWoolTeams(data);
    }

    private static bool IsOldFormat(List<object?> wools)
        => wools is [Dict first, ..] && first.ContainsKey("team") && !first.ContainsKey("monuments");

    private static List<object?> MigrateToGrouped(List<object?> wools)
    {
        var order = new List<string>();
        var byColor = new Dictionary<string, Dict>();
        foreach (var w in wools.OfType<Dict>())
        {
            var color = w.GetValueOrDefault("color") as string ?? "";
            if (!byColor.TryGetValue(color, out var group))
            {
                group = new Dict { ["id"] = Slug(color), ["color"] = color, ["location"] = w.GetValueOrDefault("location"), ["wool_room_region"] = w.GetValueOrDefault("wool_room_region"), ["monuments"] = new List<object?>() };
                byColor[color] = group; order.Add(color);
            }
            if (w.GetValueOrDefault("monument") is Dict mon)
            {
                var loc = new Dict();
                foreach (var k in new[] { "x", "y", "z" }) if (mon.ContainsKey(k)) loc[k] = mon[k];
                ((List<object?>)group["monuments"]!).Add(new Dict { ["id"] = MonumentId(color, w.GetValueOrDefault("team") as string ?? ""), ["team"] = w.GetValueOrDefault("team") as string ?? "", ["location"] = loc, ["monument_region"] = mon.GetValueOrDefault("region_id") });
            }
        }
        return order.Select(c => (object?)byColor[c]).ToList();
    }

    /// <summary>The team a wool is defended by: the one team no monument of it names. A monument is a
    /// capturing team's own drop point, so the teams named on them are the teams that must take the wool and
    /// the team left over is the one whose room it stands in — the fact <c>map.xml</c> states nowhere, since
    /// its <c>&lt;wool team&gt;</c> is a capturing team. Null where the wool leaves more than one team over,
    /// which is a wool with no single defender.</summary>
    public static string? DefendingTeam(IEnumerable<string> teamIds, IEnumerable<string?> monumentTeams)
    {
        var capturing = monumentTeams.ToHashSet();
        var defenders = teamIds.Where(team => !capturing.Contains(team)).ToList();
        return defenders.Count == 1 ? defenders[0] : null;
    }

    private static void InferWoolTeams(Dict data)
    {
        var teamIds = (data.GetValueOrDefault("teams") as List<object?> ?? []).OfType<Dict>()
            .Select(t => t.GetValueOrDefault("id")).OfType<string>().Where(id => id.Length > 0).ToList();
        if (teamIds.Count == 0) return;
        foreach (var wool in Wools(data).OfType<Dict>())
        {
            if (wool.GetValueOrDefault("team") is not null) continue;
            if (DefendingTeam(teamIds, Monuments(wool).Select(m => m.GetValueOrDefault("team") as string))
                is { } defender) wool["team"] = defender;
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────────
    private static Dict FindWool(Dict data, string woolId)
        => Wools(data).OfType<Dict>().FirstOrDefault(w => w.GetValueOrDefault("id") as string == woolId)
           ?? throw EditException.NoSuchSubject($"wool '{woolId}' not found");

    private static Dict FindMonument(Dict wool, string monId)
        => Monuments(wool).FirstOrDefault(m => m.GetValueOrDefault("id") as string == monId)
           ?? throw EditException.NoSuchSubject($"monument '{monId}' not found in wool '{wool.GetValueOrDefault("id")}'");

    private static List<object?> Wools(Dict data) => data.GetValueOrDefault("wools") as List<object?> ?? [];
    private static List<Dict> Monuments(Dict wool) => (wool.GetValueOrDefault("monuments") as List<object?> ?? []).OfType<Dict>().ToList();

    private static List<object?> EnsureList(Dict d, string k)
    {
        if (d.GetValueOrDefault(k) is not List<object?> list) { list = []; d[k] = list; }
        return list;
    }

    private static string Slug(string v) => v.Trim().ToLowerInvariant().Replace(" ", "_");
    private static string MonumentId(string color, string team) => $"{Slug(color)}-{Slug(team)}";
    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;
}
