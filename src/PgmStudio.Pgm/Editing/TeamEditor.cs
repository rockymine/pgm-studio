namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>Team add/update/delete on the map document dict (port of studio/services/team_editor.py).</summary>
public static class TeamEditor
{
    public static Dict AddTeam(Dict data, Dict payload)
    {
        var teamId = ((payload.GetValueOrDefault("id") as string) ?? "").Trim();
        if (teamId.Length == 0) throw EditException.BadRequest("id is required");

        var teams = EnsureList(data, "teams");
        if (teams.OfType<Dict>().Any(t => t.GetValueOrDefault("id") as string == teamId))
            throw EditException.Conflict($"team id '{teamId}' already in use");

        var team = new Dict
        {
            ["id"] = teamId,
            ["name"] = payload.GetValueOrDefault("name") as string ?? teamId,
            ["color"] = payload.GetValueOrDefault("color") as string ?? "red",
            ["max_players"] = CoerceInt(payload.GetValueOrDefault("max_players"), "max_players", 20),
            ["min_players"] = CoerceInt(payload.GetValueOrDefault("min_players"), "min_players", 0),
        };
        if (payload.GetValueOrDefault("dye_color") is string dye && dye.Length > 0) team["dye_color"] = dye;

        teams.Add(team);
        return new Dict { ["team"] = team };
    }

    public static Dict UpdateTeam(Dict data, string teamId, Dict payload)
    {
        var teams = ListOf(data, "teams");
        var team = teams.OfType<Dict>().FirstOrDefault(t => t.GetValueOrDefault("id") as string == teamId)
                   ?? throw EditException.NotFound($"team '{teamId}' not found");

        var newId = ((payload.GetValueOrDefault("id") as string) ?? "").Trim();
        if (newId.Length > 0 && newId != teamId)
        {
            if (teams.OfType<Dict>().Any(t => !ReferenceEquals(t, team) && t.GetValueOrDefault("id") as string == newId))
                throw EditException.Conflict($"team id '{newId}' already in use");
            foreach (var spawn in ListOf(data, "spawns").OfType<Dict>())
                if (spawn.GetValueOrDefault("team") as string == teamId) spawn["team"] = newId;
            if (data.GetValueOrDefault("observer_spawn") is Dict obs && obs.GetValueOrDefault("team") as string == teamId)
                obs["team"] = newId;
            team["id"] = newId;
        }

        foreach (var field in new[] { "name", "color", "dye_color" })
            if (payload.ContainsKey(field)) team[field] = payload[field]?.ToString() ?? "";
        foreach (var field in new[] { "max_players", "min_players" })
            if (payload.ContainsKey(field)) team[field] = CoerceInt(payload[field], field, 0);

        return new Dict { ["team"] = team };
    }

    public static Dict DeleteTeam(Dict data, string teamId)
    {
        var teams = ListOf(data, "teams");
        if (!teams.OfType<Dict>().Any(t => t.GetValueOrDefault("id") as string == teamId))
            throw EditException.NotFound($"team '{teamId}' not found");

        data["teams"] = teams.Where(t => (t as Dict)?.GetValueOrDefault("id") as string != teamId).ToList();
        data["spawns"] = ListOf(data, "spawns").Where(s => (s as Dict)?.GetValueOrDefault("team") as string != teamId).ToList();
        return new Dict();
    }

    private static List<object?> ListOf(Dict d, string k) => d.GetValueOrDefault(k) as List<object?> ?? [];

    private static List<object?> EnsureList(Dict d, string k)
    {
        if (d.GetValueOrDefault(k) is not List<object?> list) { list = []; d[k] = list; }
        return list;
    }

    private static int CoerceInt(object? v, string field, int def) => v switch
    {
        null => def,
        int i => i,
        long l => (int)l,
        double dd => (int)dd,
        string s when int.TryParse(s, out var p) => p,
        _ => throw EditException.BadRequest($"{field} must be an integer"),
    };
}
