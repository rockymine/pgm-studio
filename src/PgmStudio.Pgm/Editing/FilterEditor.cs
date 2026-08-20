namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Adds a filter to the registry. The type must be one PGM knows, and every filter or region the new one
/// names must already resolve — a filter referencing itself is refused, and <c>never</c>/<c>always</c> are
/// builtins that always do.
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
        else if (filters.ContainsKey(fid)) throw EditException.Conflict($"filter id '{fid}' already in use", [fid]);

        Validate(data, payload, fid);
        filters[fid] = new Dict(payload) { ["id"] = fid };
        return new Dict { ["id"] = fid };
    }

    // ── reference resolution ────────────────────────────────────────────────────────
    private static HashSet<string> FilterFilterRefs(Dict f)
    {
        var refs = (f.GetValueOrDefault("children") as List<object?> ?? []).OfType<string>().ToHashSet();
        if (f.GetValueOrDefault("child") is string c && c.Length > 0) refs.Add(c);
        if (f.GetValueOrDefault("filter") is string fr && fr.Length > 0) refs.Add(fr);   // after/pulse
        return refs;
    }

    private static HashSet<string> FilterRegionRefs(Dict f)
        => f.GetValueOrDefault("type") is "blocks" or "region" && f.GetValueOrDefault("region") is string r && r.Length > 0 ? [r] : [];

    private static void Validate(Dict data, Dict payload, string selfId)
    {
        if (payload.GetValueOrDefault("type") is not string ftype || !KnownTypes.Contains(ftype))
            throw EditException.Unreadable($"unknown filter type '{payload.GetValueOrDefault("type")}'", "type");
        var filters = Filters(data);
        foreach (var r in FilterFilterRefs(payload))
        {
            if (r == selfId) throw EditException.Unresolved($"filter '{r}' cannot reference itself");
            if (!filters.ContainsKey(r) && !Builtins.Contains(r)) throw EditException.Unresolved($"references unknown filter '{r}'");
        }
        var regions = data.GetValueOrDefault("regions") as Dict ?? new Dict();
        foreach (var r in FilterRegionRefs(payload))
            if (!regions.ContainsKey(r)) throw EditException.Unresolved($"references unknown region '{r}'");
    }

    private static Dict Filters(Dict data)
    {
        if (data.GetValueOrDefault("filters") is not Dict f) { f = new Dict(); data["filters"] = f; }
        return f;
    }
}
