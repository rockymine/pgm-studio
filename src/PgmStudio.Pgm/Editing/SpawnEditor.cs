namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Spawn-link + observer-spawn CRUD on the doc dict. A link is one region of a spawn, named by the region's id: a
/// new link is a new spawn of one region, its team and kit are the whole spawn's, its yaw that region's own, and
/// removing a spawn's last link removes the spawn.
/// </summary>
public static class SpawnEditor
{
    public static Dict AddSpawnLink(Dict data, Dict payload)
    {
        var regionId = ((payload.GetValueOrDefault("region_id") as string) ?? "").Trim();
        if (regionId.Length == 0) throw EditException.Unreadable("the request states no `region_id`", "region_id");
        if (!RegionsDict(data).ContainsKey(regionId)) throw EditException.NoSuchSubject($"region '{regionId}' does not exist");

        var spawns = EnsureList(data, "spawns");
        if (spawns.OfType<Dict>().Any(s => DocAccess.SpawnRegionIds(s).Contains(regionId)))
            throw EditException.Conflict($"region '{regionId}' already has a spawn", [regionId]);

        spawns.Add(new Dict
        {
            ["team"] = payload.GetValueOrDefault("team") as string ?? "",
            ["kit"] = payload.GetValueOrDefault("kit") as string ?? "",
            ["regions"] = new List<object?> { Link(regionId, payload) },
        });
        return new Dict();
    }

    public static Dict UpdateSpawnLink(Dict data, string regionId, Dict payload)
    {
        var spawn = ListOf(data, "spawns").OfType<Dict>().FirstOrDefault(s => DocAccess.SpawnRegionIds(s).Contains(regionId))
                    ?? throw EditException.NoSuchSubject($"region '{regionId}' has no spawn");
        if (payload.ContainsKey("team")) spawn["team"] = payload["team"]?.ToString() ?? "";
        if (payload.ContainsKey("kit")) spawn["kit"] = payload["kit"]?.ToString() ?? "";
        if (payload.ContainsKey("yaw"))
            foreach (var entry in DocAccess.SpawnRegions(spawn).Where(entry => DocAccess.SpawnRegionId(entry) == regionId))
                entry["yaw"] = CoerceFloat(payload["yaw"], "yaw", 0.0);
        return new Dict();
    }

    public static Dict DeleteSpawnLink(Dict data, string regionId)
    {
        var spawns = ListOf(data, "spawns");
        if (!spawns.OfType<Dict>().Any(s => DocAccess.SpawnRegionIds(s).Contains(regionId)))
            throw EditException.NoSuchSubject($"region '{regionId}' has no spawn");
        foreach (var spawn in spawns.OfType<Dict>())
            if (spawn.GetValueOrDefault("regions") is List<object?> entries)
                entries.RemoveAll(entry => entry is Dict linked && DocAccess.SpawnRegionId(linked) == regionId);
        data["spawns"] = spawns.Where(s => DocAccess.SpawnRegions(s as Dict).Any()).ToList();
        return new Dict();
    }

    public static Dict SetObserverSpawn(Dict data, Dict payload)
    {
        var regionId = ((payload.GetValueOrDefault("region_id") as string) ?? "").Trim();
        if (regionId.Length == 0) throw EditException.Unreadable("the request states no `region_id`", "region_id");
        if (!RegionsDict(data).ContainsKey(regionId)) throw EditException.NoSuchSubject($"region '{regionId}' does not exist");

        data["observer_spawn"] = new Dict
        {
            ["team"] = "",
            ["kit"] = payload.GetValueOrDefault("kit") as string ?? "",
            ["regions"] = new List<object?> { Link(regionId, payload) },
        };
        return new Dict();
    }

    public static Dict DeleteObserverSpawn(Dict data)
    {
        if (data.GetValueOrDefault("observer_spawn") is not Dict) throw EditException.NoSuchSubject("the document has no observer spawn");
        data["observer_spawn"] = null;
        return new Dict();
    }

    private static Dict Link(string regionId, Dict payload) => new()
    {
        ["region"] = regionId,
        ["yaw"] = CoerceFloat(payload.GetValueOrDefault("yaw"), "yaw", 0.0),
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
        _ => throw EditException.Unreadable($"`{field}` is not a number", field),
    };
}
