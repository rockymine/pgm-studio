namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Region CRUD + grouping (port of studio/services/region_editor.py). Regions are an id-keyed dict
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
        if (!CreateTypes.Contains(type)) throw EditException.BadRequest($"unsupported type '{type}'");
        var regions = Regions(data);

        var id = ((payload.GetValueOrDefault("id") as string) ?? "").Trim();
        if (id.Length == 0)
        {
            var prefix = type == "rectangle" ? "region" : type;
            var i = 1; while (regions.ContainsKey($"{prefix}_{i}")) i++;
            id = $"{prefix}_{i}";
        }
        else if (regions.ContainsKey(id)) throw EditException.Conflict($"id '{id}' already in use");

        try { regions[id] = RegionBuilder.BuildRegionDict(type, payload, id); }
        catch (EditException) { throw; }
        catch (Exception ex) { throw EditException.BadRequest($"missing or invalid field: {ex.Message}"); }

        TrackCategory(data, payload.GetValueOrDefault("category") as string ?? "other", id);
        return new Dict { ["id"] = id };
    }

    public static Dict GroupRegions(Dict data, Dict payload)
    {
        var compType = ((payload.GetValueOrDefault("type") as string) ?? "union").Trim();
        if (compType.Length == 0) compType = "union";
        if (!CompoundTypes.Contains(compType)) throw EditException.BadRequest($"'{compType}' is not a compound type");

        var childIds = (payload.GetValueOrDefault("child_ids") as List<object?> ?? []).Select(c => c?.ToString() ?? "").ToList();
        var minChildren = compType == "negative" ? 1 : 2;
        if (childIds.Count < minChildren) throw EditException.BadRequest($"{compType} requires at least {minChildren} region(s)");

        var regions = Regions(data);
        var missing = childIds.Where(c => !regions.ContainsKey(c)).ToList();
        if (missing.Count > 0) throw EditException.NotFound($"unknown region(s): {string.Join(", ", missing)}");

        var compoundId = ((payload.GetValueOrDefault("id") as string) ?? "").Trim();
        if (compoundId.Length == 0) { var i = 1; while (regions.ContainsKey($"{compType}_{i}")) i++; compoundId = $"{compType}_{i}"; }
        else if (regions.ContainsKey(compoundId)) throw EditException.Conflict($"id '{compoundId}' already in use");

        var (bounds, minX, minZ, maxX, maxZ) = RegionBuilder.BuildUnionBounds(childIds.Select(c => (Dict)regions[c]!));
        var compound = new Dict { ["id"] = compoundId, ["type"] = compType, ["children"] = childIds.Cast<object?>().ToList() };
        if (bounds is not null) compound["bounds_2d"] = bounds;
        regions[compoundId] = compound;
        TrackCategory(data, "other", compoundId);
        return new Dict { ["id"] = compoundId, ["bounds"] = new Dict { ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ } };
    }

    public static Dict ChangeRegionType(Dict data, string regionId, Dict payload)
    {
        var newType = ((payload.GetValueOrDefault("type") as string) ?? "").Trim();
        if (newType.Length == 0) throw EditException.BadRequest("type required");
        if (!CompoundTypes.Contains(newType)) throw EditException.BadRequest($"'{newType}' is not a compound type");
        var region = Region(data, regionId);
        if (!CompoundTypes.Contains(region.GetValueOrDefault("type") as string ?? "")) throw EditException.BadRequest($"region '{regionId}' is not a compound type");
        region["type"] = newType;
        return new Dict();
    }

    public static Dict RemoveFromGroup(Dict data, string regionId, Dict payload)
    {
        var childId = ((payload.GetValueOrDefault("child_id") as string) ?? "").Trim();
        if (childId.Length == 0) throw EditException.BadRequest("child_id required");
        var region = Region(data, regionId);
        if (region.GetValueOrDefault("children") is not List<object?> children) throw EditException.BadRequest($"region '{regionId}' has no children");
        var idx = children.FindIndex(c => ChildId(c) == childId);
        if (idx < 0) throw EditException.NotFound($"child '{childId}' not found in '{regionId}'");
        children.RemoveAt(idx);
        EnsureCategorised(data, childId);
        return new Dict();
    }

    public static Dict SetBaseChild(Dict data, string regionId, Dict payload)
    {
        var childId = ((payload.GetValueOrDefault("child_id") as string) ?? "").Trim();
        if (childId.Length == 0) throw EditException.BadRequest("child_id required");
        var region = Region(data, regionId);
        if (region.GetValueOrDefault("type") as string != "complement") throw EditException.BadRequest($"region '{regionId}' is not a complement");
        var children = region.GetValueOrDefault("children") as List<object?> ?? [];
        var idx = children.FindIndex(c => ChildId(c) == childId);
        if (idx < 0) throw EditException.NotFound($"child '{childId}' not found in complement '{regionId}'");
        if (idx != 0) { var c = children[idx]; children.RemoveAt(idx); children.Insert(0, c); }
        return new Dict();
    }

    public static Dict UngroupRegion(Dict data, Dict payload)
    {
        var regionId = ((payload.GetValueOrDefault("region_id") as string) ?? "").Trim();
        if (regionId.Length == 0) throw EditException.BadRequest("region_id required");
        var regions = Regions(data);
        if (!regions.TryGetValue(regionId, out var compObj) || compObj is not Dict compound) throw EditException.NotFound($"region '{regionId}' not found");
        var compType = compound.GetValueOrDefault("type") as string ?? "";
        if (!CompoundTypes.Contains(compType)) throw EditException.BadRequest($"region '{regionId}' is not a compound region");

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
        if (!regions.ContainsKey(regionId)) throw EditException.NotFound($"region '{regionId}' not found");

        var subtreeIds = CollectSubtreeIds(regions, regionId);
        var category = "other";
        foreach (var (cat, ids) in Categories(data)) if (ids.Contains(regionId)) { category = cat; break; }
        var entries = subtreeIds.Where(regions.ContainsKey).ToDictionary(rid => rid, rid => regions[rid]);

        var subtreeSet = subtreeIds.ToHashSet();
        foreach (var rid in subtreeIds) regions.Remove(rid);
        foreach (var (_, ids) in Categories(data)) ids.RemoveAll(rid => rid is string s && subtreeSet.Contains(s));
        RemoveInlineChildren(regions, subtreeSet);

        return new Dict { ["snapshot"] = new Dict { ["root_id"] = regionId, ["category"] = category, ["region_entries"] = entries.ToDictionary(kv => kv.Key, kv => kv.Value) } };
    }

    public static Dict RestoreRegion(Dict data, Dict snapshot)
    {
        var rootId = snapshot.GetValueOrDefault("root_id") as string ?? "";
        var category = snapshot.GetValueOrDefault("category") as string ?? "other";
        if (rootId.Length == 0 || snapshot.GetValueOrDefault("region_entries") is not Dict entries || entries.Count == 0)
            throw EditException.BadRequest("invalid snapshot");

        var regions = Regions(data);
        var conflicts = entries.Keys.Where(regions.ContainsKey).ToList();
        if (conflicts.Count > 0) throw EditException.Conflict($"id(s) already in use: {string.Join(", ", conflicts)}");
        foreach (var (rid, r) in entries) regions[rid] = r;
        TrackCategory(data, category, rootId);
        return new Dict { ["id"] = rootId };
    }

    public static Dict PatchRegion(Dict data, string regionId, Dict payload)
    {
        var bounds = payload.GetValueOrDefault("bounds") as Dict;
        var coords = payload.GetValueOrDefault("coords") as Dict;
        if (string.IsNullOrEmpty(payload.GetValueOrDefault("id") as string) && bounds is null && coords is null)
            throw EditException.BadRequest("provide 'id', 'bounds', or 'coords'");

        var regions = Regions(data);
        if (!regions.TryGetValue(regionId, out var regObj) || regObj is not Dict region) throw EditException.NotFound($"region '{regionId}' not found");

        var newId = ((payload.GetValueOrDefault("id") as string) ?? "").Trim();
        if (newId.Length > 0 && newId != regionId)
        {
            if (regions.ContainsKey(newId)) throw EditException.Conflict($"id '{newId}' already in use");
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
        if (bounds is not null)
        {
            updatedBounds = new Dict
            {
                ["min"] = new Dict { ["x"] = bounds["min_x"], ["z"] = bounds["min_z"] },
                ["max"] = new Dict { ["x"] = bounds["max_x"], ["z"] = bounds["max_z"] },
            };
            region["bounds_2d"] = updatedBounds;
        }
        if (coords is not null)
            updatedBounds = RegionBuilder.ApplyCoordUpdate(region, region.GetValueOrDefault("type") as string ?? "", coords) ?? updatedBounds;

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
        => Regions(data).GetValueOrDefault(id) as Dict ?? throw EditException.NotFound($"region '{id}' not found");

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
