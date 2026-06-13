namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>Spawn-link + observer-spawn CRUD on the doc dict (port of studio/services/spawn_editor.py).</summary>
public static class SpawnEditor
{
    public static Dict AddSpawnLink(Dict data, Dict payload)
    {
        var regionId = ((payload.GetValueOrDefault("region_id") as string) ?? "").Trim();
        if (regionId.Length == 0) throw EditException.BadRequest("region_id is required");
        if (!RegionsDict(data).ContainsKey(regionId)) throw EditException.NotFound($"region '{regionId}' not found");

        var spawns = EnsureList(data, "spawns");
        if (spawns.OfType<Dict>().Any(s => SpawnRegionId(s) == regionId))
            throw EditException.Conflict($"spawn for region '{regionId}' already exists");

        spawns.Add(new Dict
        {
            ["team"] = payload.GetValueOrDefault("team") as string ?? "",
            ["kit"] = payload.GetValueOrDefault("kit") as string ?? "",
            ["yaw"] = CoerceFloat(payload.GetValueOrDefault("yaw"), "yaw", 0.0),
            ["region"] = regionId,
        });
        return new Dict();
    }

    public static Dict UpdateSpawnLink(Dict data, string regionId, Dict payload)
    {
        var spawn = ListOf(data, "spawns").OfType<Dict>().FirstOrDefault(s => SpawnRegionId(s) == regionId)
                    ?? throw EditException.NotFound($"no spawn for region '{regionId}'");
        if (payload.ContainsKey("team")) spawn["team"] = payload["team"]?.ToString() ?? "";
        if (payload.ContainsKey("yaw")) spawn["yaw"] = CoerceFloat(payload["yaw"], "yaw", 0.0);
        if (payload.ContainsKey("kit")) spawn["kit"] = payload["kit"]?.ToString() ?? "";
        return new Dict();
    }

    public static Dict DeleteSpawnLink(Dict data, string regionId)
    {
        var spawns = ListOf(data, "spawns");
        if (!spawns.OfType<Dict>().Any(s => SpawnRegionId(s) == regionId))
            throw EditException.NotFound($"no spawn for region '{regionId}'");
        data["spawns"] = spawns.Where(s => SpawnRegionId(s as Dict) != regionId).ToList();
        return new Dict();
    }

    public static Dict SetObserverSpawn(Dict data, Dict payload)
    {
        var regionId = ((payload.GetValueOrDefault("region_id") as string) ?? "").Trim();
        if (regionId.Length == 0) throw EditException.BadRequest("region_id is required");
        if (!RegionsDict(data).ContainsKey(regionId)) throw EditException.NotFound($"region '{regionId}' not found");

        data["observer_spawn"] = new Dict
        {
            ["team"] = "",
            ["kit"] = payload.GetValueOrDefault("kit") as string ?? "",
            ["yaw"] = CoerceFloat(payload.GetValueOrDefault("yaw"), "yaw", 0.0),
            ["region"] = regionId,
        };
        return new Dict();
    }

    public static Dict DeleteObserverSpawn(Dict data)
    {
        if (data.GetValueOrDefault("observer_spawn") is not Dict) throw EditException.NotFound("no observer spawn defined");
        data["observer_spawn"] = null;
        return new Dict();
    }

    private static string SpawnRegionId(Dict? spawn) => spawn?.GetValueOrDefault("region") switch
    {
        string s => s,
        Dict d => d.GetValueOrDefault("id") as string ?? "",
        _ => "",
    };

    private static Dict RegionsDict(Dict data) => data.GetValueOrDefault("regions") as Dict ?? new Dict();
    private static List<object?> ListOf(Dict d, string k) => d.GetValueOrDefault(k) as List<object?> ?? [];

    private static List<object?> EnsureList(Dict d, string k)
    {
        if (d.GetValueOrDefault(k) is not List<object?> list) { list = []; d[k] = list; }
        return list;
    }

    private static double CoerceFloat(object? v, string field, double def) => v switch
    {
        null => def,
        double d => d,
        long l => l,
        int i => i,
        string s when double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p) => p,
        _ => throw EditException.BadRequest($"{field} must be a number"),
    };
}
