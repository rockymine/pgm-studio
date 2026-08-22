namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Region CRUD + grouping. Regions are an id-keyed dict
/// of canonical region dicts; compound children are string-id refs into that registry.
/// </summary>
public static class RegionEditor
{
    private static readonly HashSet<string> CreateTypes = ["rectangle", "cuboid", "point", "block", "cylinder", "circle"];
    private static readonly HashSet<string> CompoundTypes = ["union", "complement", "intersect", "negative"];
    private static readonly HashSet<string> OrderedCompoundTypes = ["complement", "negative"];

    public static Dict CreateRegion(Dict data, Dict payload)
    {
        var type = payload.GetValueOrDefault("type") as string ?? "rectangle";
        if (!CreateTypes.Contains(type)) throw EditException.Unreadable($"unsupported type '{type}'", "type");
        var regions = Regions(data);

        var id = ((payload.GetValueOrDefault("id") as string) ?? "").Trim();
        if (id.Length == 0)
        {
            var prefix = type == "rectangle" ? "region" : type;
            var i = 1; while (regions.ContainsKey($"{prefix}_{i}")) i++;
            id = $"{prefix}_{i}";
        }
        else if (regions.ContainsKey(id)) throw EditException.Conflict($"id '{id}' already in use", [id]);

        var coords = payload.GetValueOrDefault("coords") as Dict;
        if (coords is null) throw EditException.Unreadable("coords required", "coords");

        try { regions[id] = RegionBuilder.BuildRegionDict(type, coords, id); }
        catch (EditException) { throw; }
        catch (Exception ex) { throw EditException.Unreadable($"missing or invalid field: {ex.Message}"); }

        TrackCategory(data, payload.GetValueOrDefault("category") as string ?? "other", id);
        return new Dict { ["id"] = id };
    }

    public static Dict GroupRegions(Dict data, Dict payload)
    {
        var compType = ((payload.GetValueOrDefault("type") as string) ?? "union").Trim();
        if (compType.Length == 0) compType = "union";
        if (!CompoundTypes.Contains(compType)) throw EditException.Unreadable($"'{compType}' is not a compound type", "type");

        var childIds = (payload.GetValueOrDefault("child_ids") as List<object?> ?? []).Select(c => c?.ToString() ?? "").ToList();
        var minChildren = compType == "negative" ? 1 : 2;
        if (childIds.Count < minChildren) throw EditException.Inapplicable($"{compType} requires at least {minChildren} region(s)");

        var regions = Regions(data);
        var missing = childIds.Where(c => !regions.ContainsKey(c)).ToList();
        if (missing.Count > 0) throw EditException.NoSuchSubject($"unknown region(s): {string.Join(", ", missing)}", missing);

        var compoundId = ((payload.GetValueOrDefault("id") as string) ?? "").Trim();
        if (compoundId.Length == 0) { var i = 1; while (regions.ContainsKey($"{compType}_{i}")) i++; compoundId = $"{compType}_{i}"; }
        else if (regions.ContainsKey(compoundId)) throw EditException.Conflict($"id '{compoundId}' already in use", [compoundId]);

        var (bounds, minX, minZ, maxX, maxZ) = RegionBuilder.BuildUnionBounds(childIds.Select(c => (Dict)regions[c]!));
        var compound = new Dict { ["id"] = compoundId, ["type"] = compType, ["children"] = childIds.Cast<object?>().ToList() };
        if (bounds is not null) compound["bounds_2d"] = bounds;
        regions[compoundId] = compound;
        TrackCategory(data, "other", compoundId);
        return new Dict { ["id"] = compoundId, ["bounds"] = new Dict { ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ } };
    }

    public static Dict UngroupRegion(Dict data, Dict payload)
    {
        var regionId = ((payload.GetValueOrDefault("region_id") as string) ?? "").Trim();
        if (regionId.Length == 0) throw EditException.Unreadable("region_id required", "region_id");
        var regions = Regions(data);
        if (!regions.TryGetValue(regionId, out var compObj) || compObj is not Dict compound) throw EditException.NoSuchSubject($"region '{regionId}' not found");
        var compType = compound.GetValueOrDefault("type") as string ?? "";
        if (!CompoundTypes.Contains(compType)) throw EditException.Inapplicable($"region '{regionId}' is not a compound region");

        var childIds = (compound.GetValueOrDefault("children") as List<object?> ?? []).Select(ChildId).Where(x => x.Length > 0).ToList();
        regions.Remove(regionId);
        RemoveFromCategories(data, regionId);

        var result = new Dict { ["child_ids"] = childIds.Cast<object?>().ToList() };
        if (OrderedCompoundTypes.Contains(compType))
            result["warning"] = $"Dissolved {compType} region '{regionId}'; its base/subtrahend ordering was discarded.";
        return result;
    }

    public static Dict DeleteRegion(Dict data, string regionId)
    {
        var regions = Regions(data);
        if (!regions.ContainsKey(regionId)) throw EditException.NoSuchSubject($"region '{regionId}' not found");

        var subtreeIds = CollectSubtreeIds(regions, regionId);
        var subtreeSet = subtreeIds.ToHashSet();
        foreach (var rid in subtreeIds) regions.Remove(rid);
        foreach (var (_, ids) in Categories(data)) ids.RemoveAll(rid => rid is string s && subtreeSet.Contains(s));
        RemoveInlineChildren(regions, subtreeSet);

        return [];
    }

    public static Dict PatchRegion(Dict data, string regionId, Dict payload)
    {
        var coords = payload.GetValueOrDefault("coords") as Dict;
        if (string.IsNullOrEmpty(payload.GetValueOrDefault("id") as string) && coords is null)
            throw EditException.Unreadable("provide 'id' or 'coords'");

        var regions = Regions(data);
        if (!regions.TryGetValue(regionId, out var regObj) || regObj is not Dict region) throw EditException.NoSuchSubject($"region '{regionId}' not found");

        var newId = ((payload.GetValueOrDefault("id") as string) ?? "").Trim();
        if (newId.Length > 0 && newId != regionId)
        {
            if (regions.ContainsKey(newId)) throw EditException.Conflict($"id '{newId}' already in use", [newId]);
            regions[newId] = region; regions.Remove(regionId); region["id"] = newId;
            foreach (var (_, ids) in Categories(data)) for (var i = 0; i < ids.Count; i++) if (ids[i] as string == regionId) ids[i] = newId;
            foreach (var r in regions.Values.OfType<Dict>()) RenameInChildren(r, regionId, newId);
            foreach (var spawn in (data.GetValueOrDefault("spawns") as List<object?> ?? []).OfType<Dict>())
                if (spawn.GetValueOrDefault("region") as string == regionId) spawn["region"] = newId;
            foreach (var wool in (data.GetValueOrDefault("wools") as List<object?> ?? []).OfType<Dict>())
                if (wool.GetValueOrDefault("wool_room_region") as string == regionId) wool["wool_room_region"] = newId;
            regionId = newId;
        }

        Dict? updatedBounds = null;
        if (coords is not null)
            updatedBounds = RegionBuilder.ApplyCoordUpdate(region, region.GetValueOrDefault("type") as string ?? "", coords);

        if (updatedBounds is not null)
        {
            var mn = (Dict)updatedBounds["min"]!; var mx = (Dict)updatedBounds["max"]!;
            return new Dict { ["bounds"] = new Dict { ["min_x"] = mn["x"], ["min_z"] = mn["z"], ["max_x"] = mx["x"], ["max_z"] = mx["z"] } };
        }
        return new Dict();
    }

    // ── helpers ───────────────────────────────────────────────────────────────────
    private static Dict Regions(Dict data)
    {
        if (data.GetValueOrDefault("regions") is Dict d) return d;
        if (data.GetValueOrDefault("regions") is List<object?> list)
        {
            var dict = new Dict();
            foreach (var r in list.OfType<Dict>()) if (r.GetValueOrDefault("id") is string id && id.Length > 0) dict[id] = r;
            data["regions"] = dict; return dict;
        }
        var fresh = new Dict(); data["regions"] = fresh; return fresh;
    }

    private static Dict Region(Dict data, string id)
        => Regions(data).GetValueOrDefault(id) as Dict ?? throw EditException.NoSuchSubject($"region '{id}' not found");

    private static string ChildId(object? child) => child switch { string s => s, Dict d => d.GetValueOrDefault("id") as string ?? "", _ => "" };

    private static List<string> CollectSubtreeIds(Dict regions, string regionId)
    {
        var result = new List<string> { regionId };
        if (regions.GetValueOrDefault(regionId) is Dict r)
            foreach (var child in r.GetValueOrDefault("children") as List<object?> ?? [])
            {
                var cid = ChildId(child);
                if (cid.Length > 0 && regions.ContainsKey(cid)) result.AddRange(CollectSubtreeIds(regions, cid));
            }
        return result;
    }

    private static void RemoveInlineChildren(Dict regions, HashSet<string> idsToRemove)
    {
        foreach (var region in regions.Values.OfType<Dict>())
            if (region.GetValueOrDefault("children") is List<object?> children)
                region["children"] = children.Where(c => !idsToRemove.Contains(ChildId(c))).ToList();
    }

    private static void RenameInChildren(Dict region, string oldId, string newId)
    {
        if (region.GetValueOrDefault("children") is not List<object?> children) return;
        for (var i = 0; i < children.Count; i++)
            if (children[i] is string s) { if (s == oldId) children[i] = newId; }
            else if (children[i] is Dict d) { if (d.GetValueOrDefault("id") as string == oldId) d["id"] = newId; RenameInChildren(d, oldId, newId); }
    }

    // region_categories is an editor-only undo hint; it is not persisted (FromDict drops it).
    private static Dict CategoriesDict(Dict data)
    {
        if (data.GetValueOrDefault("region_categories") is not Dict d) { d = new Dict(); data["region_categories"] = d; }
        return d;
    }

    private static IEnumerable<(string cat, List<object?> ids)> Categories(Dict data)
        => CategoriesDict(data).Where(kv => kv.Value is List<object?>).Select(kv => (kv.Key, (List<object?>)kv.Value!));

    private static void TrackCategory(Dict data, string category, string id)
    {
        var cats = CategoriesDict(data);
        if (cats.GetValueOrDefault(category) is not List<object?> list) { list = []; cats[category] = list; }
        list.Add(id);
    }

    private static void EnsureCategorised(Dict data, string id)
    {
        if (!Categories(data).Any(c => c.ids.Contains(id))) TrackCategory(data, "other", id);
    }

    private static void RemoveFromCategories(Dict data, string id)
    {
        foreach (var (_, ids) in Categories(data)) if (ids.Remove(id)) break;
    }
}
