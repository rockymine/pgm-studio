namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Wool + monument CRUD on the grouped wool format (port of studio/services/wool_editor.py). Ids are
/// content-derived (wool id = colour slug; monument id = <c>colour-team</c>), matching the serializer.
/// </summary>
public static class WoolEditor
{
    private static readonly HashSet<string> ValidColors =
    [
        "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
        "silver", "cyan", "purple", "blue", "brown", "green", "red", "black",
    ];

    public static Dict AddWool(Dict data, Dict payload)
    {
        EnsureGrouped(data);
        var color = Slug(payload.GetValueOrDefault("color") as string ?? "white");
        if (!ValidColors.Contains(color)) throw EditException.BadRequest($"invalid wool color '{color}'");
        if (Wools(data).OfType<Dict>().Any(w => w.GetValueOrDefault("color") as string == color))
            throw EditException.BadRequest($"wool color '{color}' already exists");
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
            var color = Slug(payload["color"] as string ?? "");
            if (!ValidColors.Contains(color)) throw EditException.BadRequest($"invalid wool color '{color}'");
            if (color != wool.GetValueOrDefault("color") as string && Wools(data).OfType<Dict>().Any(w => !ReferenceEquals(w, wool) && w.GetValueOrDefault("color") as string == color))
                throw EditException.BadRequest($"wool color '{color}' already exists");
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
            throw EditException.NotFound($"wool '{woolId}' not found");
        data["wools"] = wools.Where(w => (w as Dict)?.GetValueOrDefault("id") as string != woolId).ToList();
        return new Dict();
    }

    public static Dict AddMonument(Dict data, string woolId, Dict payload)
    {
        EnsureGrouped(data);
        var wool = FindWool(data, woolId);
        var team = payload.GetValueOrDefault("team") as string ?? "";
        if (team.Length > 0 && Monuments(wool).Any(m => m.GetValueOrDefault("team") as string == team))
            throw EditException.BadRequest($"monument for team '{team}' already exists on this wool");
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
                throw EditException.BadRequest($"monument for team '{newTeam}' already exists on this wool");
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
            throw EditException.NotFound($"monument '{monId}' not found in wool '{woolId}'");
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

    private static void InferWoolTeams(Dict data)
    {
        var teamIds = (data.GetValueOrDefault("teams") as List<object?> ?? []).OfType<Dict>()
            .Select(t => t.GetValueOrDefault("id") as string).Where(x => !string.IsNullOrEmpty(x)).ToHashSet();
        if (teamIds.Count == 0) return;
        foreach (var wool in Wools(data).OfType<Dict>())
        {
            if (wool.GetValueOrDefault("team") is not null) continue;
            var monumentTeams = Monuments(wool).Select(m => m.GetValueOrDefault("team") as string).ToHashSet();
            var missing = teamIds.Where(t => !monumentTeams.Contains(t)).ToList();
            if (missing.Count == 1) wool["team"] = missing[0];
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────────
    private static Dict FindWool(Dict data, string woolId)
        => Wools(data).OfType<Dict>().FirstOrDefault(w => w.GetValueOrDefault("id") as string == woolId)
           ?? throw EditException.NotFound($"wool '{woolId}' not found");

    private static Dict FindMonument(Dict wool, string monId)
        => Monuments(wool).FirstOrDefault(m => m.GetValueOrDefault("id") as string == monId)
           ?? throw EditException.NotFound($"monument '{monId}' not found in wool '{wool.GetValueOrDefault("id")}'");

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
