using System.Text.RegularExpressions;

namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Apply-rule CRUD on the doc dict (port of studio/services/apply_rule_editor.py). Rules have no
/// id in the PGM model, so a stable synthetic <c>rule_&lt;n&gt;</c> is backfilled (positional, so it
/// survives save/reload). Filter/region refs may be ids or inline descriptors — only a plain id that
/// resolves to nothing is rejected.
/// </summary>
public static partial class ApplyRuleEditor
{
    private static readonly string[] RuleFilterKeys =
        ["enter", "leave", "block", "block_place", "block_break", "block_physics", "block_place_against", "use", "filter"];
    private static readonly string[] ActionKeys = ["kit", "lend_kit", "velocity", "message"];
    private const string IdPrefix = "rule_";
    private static readonly HashSet<string> BuiltinFilters = ["never", "always"];
    private static readonly HashSet<string> BuiltinRegions = ["everywhere", "nowhere"];

    public static Dict ListApplyRules(Dict data) => new() { ["apply_rules"] = EnsureRuleIds(data) };

    public static Dict CreateApplyRule(Dict data, Dict payload)
    {
        Validate(data, payload);
        var rules = EnsureRuleIds(data);
        var used = rules.OfType<Dict>().Select(r => r.GetValueOrDefault("id") as string).Where(x => x is not null).ToHashSet();
        var n = 1;
        while (used.Contains($"{IdPrefix}{n}")) n++;
        var rule = new Dict(payload.Where(kv => kv.Key != "id")) { ["id"] = $"{IdPrefix}{n}" };
        rules.Add(rule);
        return new Dict { ["id"] = rule["id"] };
    }

    public static Dict UpdateApplyRule(Dict data, string ruleId, Dict payload)
    {
        var rule = Find(data, ruleId);
        Validate(data, payload);
        rule.Clear();
        foreach (var (k, v) in payload.Where(kv => kv.Key != "id")) rule[k] = v;
        rule["id"] = ruleId;
        return new Dict { ["id"] = ruleId };
    }

    public static Dict DeleteApplyRule(Dict data, string ruleId)
    {
        var rules = EnsureRuleIds(data);
        var idx = rules.FindIndex(r => (r as Dict)?.GetValueOrDefault("id") as string == ruleId);
        if (idx < 0) throw EditException.NotFound($"no apply-rule '{ruleId}'");
        rules.RemoveAt(idx);
        return new Dict { ["id"] = ruleId };
    }

    // ── helpers ───────────────────────────────────────────────────────────────────
    private static List<object?> Rules(Dict data)
    {
        if (data.GetValueOrDefault("apply_rules") is not List<object?> list) { list = []; data["apply_rules"] = list; }
        return list;
    }

    private static List<object?> EnsureRuleIds(Dict data)
    {
        var rules = Rules(data);
        var nextN = 1 + rules.OfType<Dict>()
            .Select(r => r.GetValueOrDefault("id") as string)
            .Select(id => id is not null && RuleId().Match(id) is { Success: true } m ? int.Parse(m.Groups[1].Value) : 0)
            .DefaultIfEmpty(0).Max();
        foreach (var rule in rules.OfType<Dict>())
            if (rule.GetValueOrDefault("id") is not string s || s.Length == 0)
                rule["id"] = $"{IdPrefix}{nextN++}";
        return rules;
    }

    private static Dict Find(Dict data, string ruleId)
        => EnsureRuleIds(data).OfType<Dict>().FirstOrDefault(r => r.GetValueOrDefault("id") as string == ruleId)
           ?? throw EditException.NotFound($"no apply-rule '{ruleId}'");

    private static void Validate(Dict data, Dict payload)
    {
        var hasSomething = RuleFilterKeys.Concat(ActionKeys).Append("region")
            .Any(k => payload.GetValueOrDefault(k) is string s && s.Length > 0);
        if (!hasSomething) throw EditException.BadRequest("apply-rule has no region, filter, or action");

        var regions = data.GetValueOrDefault("regions") as Dict ?? new Dict();
        var filters = data.GetValueOrDefault("filters") as Dict ?? new Dict();

        if (payload.GetValueOrDefault("region") is string region && region.Length > 0 && IsSimpleRef(region)
            && !regions.ContainsKey(region) && !BuiltinRegions.Contains(region))
            throw EditException.BadRequest($"references unknown region '{region}'");

        foreach (var key in RuleFilterKeys)
            if (payload.GetValueOrDefault(key) is string val && val.Length > 0 && IsSimpleRef(val)
                && !filters.ContainsKey(val) && !regions.ContainsKey(val) && !BuiltinFilters.Contains(val))
                throw EditException.BadRequest($"{key} references unknown filter/region '{val}'");
    }

    private static bool IsSimpleRef(string value) => SimpleRef().IsMatch(value);

    [GeneratedRegex(@"^[A-Za-z0-9_-]+$")] private static partial Regex SimpleRef();
    [GeneratedRegex(@"^rule_(\d+)$")] private static partial Regex RuleId();
}
