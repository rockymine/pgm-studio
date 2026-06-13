namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Filter registry CRUD (port of studio/services/filter_editor.py, C3). Validates filter type +
/// refs; a filter can't be deleted while referenced (by apply-rules, other filters, renewables, or
/// block-drop rules); never/always builtins are protected.
/// </summary>
public static class FilterEditor
{
    private static readonly HashSet<string> KnownTypes =
    [
        "all", "any", "one", "not", "deny", "allow", "team", "material", "void", "cause", "blocks",
        "carrying", "wearing", "holding", "alive", "dead", "participating", "observing",
        "match-running", "match-started", "grounded", "never", "always", "time", "after", "pulse",
        "offset", "variable", "completed", "objective", "kill-streak", "class", "region", "players", "spawn",
    ];
    private static readonly HashSet<string> Builtins = ["never", "always"];

    public static Dict ListFilters(Dict data)
    {
        var filters = Filters(data);
        return new Dict
        {
            ["filters"] = filters,
            ["usage"] = filters.Keys.ToDictionary(fid => fid, fid => (object?)References(data, fid)),
        };
    }

    public static Dict CreateFilter(Dict data, Dict payload)
    {
        var filters = Filters(data);
        var fid = ((payload.GetValueOrDefault("id") as string) ?? "").Trim();
        if (fid.Length == 0)
        {
            var ftype = payload.GetValueOrDefault("type") as string ?? "filter";
            var i = 1;
            while (filters.ContainsKey($"{ftype}_{i}")) i++;
            fid = $"{ftype}_{i}";
        }
        else if (filters.ContainsKey(fid)) throw EditException.Conflict($"filter id '{fid}' already in use");

        Validate(data, payload, fid);
        filters[fid] = new Dict(payload) { ["id"] = fid };
        return new Dict { ["id"] = fid };
    }

    public static Dict UpdateFilter(Dict data, string fid, Dict payload)
    {
        var filters = Filters(data);
        if (!filters.TryGetValue(fid, out var existing)) throw EditException.NotFound($"no filter '{fid}'");
        var merged = new Dict(payload) { ["id"] = fid };
        if (!merged.ContainsKey("type")) merged["type"] = (existing as Dict)?.GetValueOrDefault("type");
        Validate(data, merged, fid);
        filters[fid] = merged;
        return new Dict { ["id"] = fid };
    }

    public static Dict DeleteFilter(Dict data, string fid)
    {
        var filters = Filters(data);
        if (Builtins.Contains(fid)) throw EditException.Conflict($"filter '{fid}' is a builtin and cannot be deleted");
        if (!filters.ContainsKey(fid)) throw EditException.NotFound($"no filter '{fid}'");
        var refs = References(data, fid);
        if (refs.Count > 0) throw EditException.Conflict($"filter '{fid}' is referenced by {refs.Count} item(s); unwire them first");
        filters.Remove(fid);
        return new Dict { ["id"] = fid };
    }

    // ── reference tracking ──────────────────────────────────────────────────────────
    private static HashSet<string> FilterFilterRefs(Dict f)
    {
        var refs = (f.GetValueOrDefault("children") as List<object?> ?? []).OfType<string>().ToHashSet();
        if (f.GetValueOrDefault("child") is string c && c.Length > 0) refs.Add(c);
        if (f.GetValueOrDefault("filter") is string fr && fr.Length > 0) refs.Add(fr);   // after/pulse
        return refs;
    }

    private static HashSet<string> FilterRegionRefs(Dict f)
        => f.GetValueOrDefault("type") is "blocks" or "region" && f.GetValueOrDefault("region") is string r && r.Length > 0 ? [r] : [];

    private static HashSet<string> ApplyFilterRefs(Dict rule)
    {
        var keys = new[] { "enter", "leave", "block", "block_place", "block_break", "block_physics", "block_place_against", "use", "filter" };
        return keys.Select(k => rule.GetValueOrDefault(k) as string).Where(v => !string.IsNullOrEmpty(v)).Cast<string>().ToHashSet();
    }

    public static List<Dict> References(Dict data, string fid)
    {
        var refs = new List<Dict>();
        foreach (var rule in (data.GetValueOrDefault("apply_rules") as List<object?> ?? []).OfType<Dict>())
            if (ApplyFilterRefs(rule).Contains(fid)) refs.Add(new Dict { ["kind"] = "apply_rule", ["id"] = rule.GetValueOrDefault("id") ?? "" });
        foreach (var (otherId, fObj) in Filters(data))
            if (otherId != fid && fObj is Dict fd && FilterFilterRefs(fd).Contains(fid)) refs.Add(new Dict { ["kind"] = "filter", ["id"] = otherId });
        foreach (var ren in (data.GetValueOrDefault("renewables") as List<object?> ?? []).OfType<Dict>())
            if (ren.GetValueOrDefault("renew_filter") as string == fid || ren.GetValueOrDefault("replace_filter") as string == fid)
                refs.Add(new Dict { ["kind"] = "renewable", ["region"] = ren.GetValueOrDefault("region_id") ?? "" });
        foreach (var bdr in (data.GetValueOrDefault("block_drop_rules") as List<object?> ?? []).OfType<Dict>())
            if (bdr.GetValueOrDefault("filter_id") as string == fid) refs.Add(new Dict { ["kind"] = "block_drop_rule", ["region"] = bdr.GetValueOrDefault("region_id") ?? "" });
        return refs;
    }

    private static void Validate(Dict data, Dict payload, string selfId)
    {
        if (payload.GetValueOrDefault("type") is not string ftype || !KnownTypes.Contains(ftype))
            throw EditException.BadRequest($"unknown filter type '{payload.GetValueOrDefault("type")}'");
        var filters = Filters(data);
        foreach (var r in FilterFilterRefs(payload))
        {
            if (r == selfId) throw EditException.BadRequest($"filter '{r}' cannot reference itself");
            if (!filters.ContainsKey(r) && !Builtins.Contains(r)) throw EditException.BadRequest($"references unknown filter '{r}'");
        }
        var regions = data.GetValueOrDefault("regions") as Dict ?? new Dict();
        foreach (var r in FilterRegionRefs(payload))
            if (!regions.ContainsKey(r)) throw EditException.BadRequest($"references unknown region '{r}'");
    }

    private static Dict Filters(Dict data)
    {
        if (data.GetValueOrDefault("filters") is not Dict f) { f = new Dict(); data["filters"] = f; }
        return f;
    }
}
