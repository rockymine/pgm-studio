using PgmStudio.Pgm.Editing;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// F1 (C9): apply the v1 filter↔region wiring templates (port of the appliers in
/// studio/services/filter_wiring.py; see docs/contracts/filter-region-wiring.md). A template is a
/// pre-built Filter + ApplyRule (+ compound) combination emitted through the existing C3/C4 editors —
/// it adds no new persisted type. The <b>caller chooses the region</b> (after grouping, R1); there is
/// deliberately no suggestion engine here — proposing wiring from map signals targeted the spawn
/// <i>point</i>, which by corpus invariant is never wired (the protection is a separate region).
/// </summary>
public static class FilterWiring
{
    public static readonly string[] Templates = ["spawn_protection", "wool_room_defense", "wool_room_edit", "build_void_enforcement"];

    private static string TeamSlug(string teamId) => teamId.EndsWith("-team") ? teamId[..^5] : teamId;

    private static Dict Filters(Dict data)
    {
        if (data.GetValueOrDefault("filters") is not Dict f) { f = new Dict(); data["filters"] = f; }
        return f;
    }

    /// <summary>Idempotently ensure an <c>only-&lt;slug&gt;</c> (and, if negate, <c>not-&lt;slug&gt;</c>)
    /// team filter exists; returns the id to reference.</summary>
    private static string EnsureTeamFilter(Dict data, string teamId, bool negate = false)
    {
        var filters = Filters(data);
        var slug = TeamSlug(teamId);
        var onlyId = $"only-{slug}";
        if (!filters.ContainsKey(onlyId))
            FilterEditor.CreateFilter(data, new Dict { ["type"] = "team", ["team"] = teamId, ["id"] = onlyId });
        if (!negate) return onlyId;
        var notId = $"not-{slug}";
        if (!filters.ContainsKey(notId))
            FilterEditor.CreateFilter(data, new Dict { ["type"] = "not", ["child"] = onlyId, ["id"] = notId });
        return notId;
    }

    // ── appliers (one per template) ───────────────────────────────────────────────────
    private static Dict ApplySpawnProtection(Dict data, string region, string team)
    {
        var fid = EnsureTeamFilter(data, team);
        var res = ApplyRuleEditor.CreateApplyRule(data, new Dict { ["region"] = region, ["enter"] = fid, ["message"] = "You may not enter the enemy's spawn!" });
        return new Dict { ["rule_id"] = res["id"], ["filter_id"] = fid };
    }

    private static Dict ApplyWoolRoomDefense(Dict data, string region, string owner)
    {
        var fid = EnsureTeamFilter(data, owner, negate: true);   // not-<owner>
        var res = ApplyRuleEditor.CreateApplyRule(data, new Dict { ["region"] = region, ["enter"] = fid, ["message"] = "You may not enter your own wool room!" });
        return new Dict { ["rule_id"] = res["id"], ["filter_id"] = fid };
    }

    private static Dict ApplyWoolRoomEdit(Dict data, string region, string owner)
    {
        var fid = EnsureTeamFilter(data, owner, negate: true);   // only non-owners may edit
        var res = ApplyRuleEditor.CreateApplyRule(data, new Dict { ["region"] = region, ["block"] = fid, ["message"] = "You may not edit the wool room!" });
        return new Dict { ["rule_id"] = res["id"], ["filter_id"] = fid };
    }

    private static Dict ApplyBuildVoidEnforcement(Dict data, List<string> buildRegionIds)
    {
        if (buildRegionIds.Count == 0) throw EditException.BadRequest("no build regions to enforce");
        var neg = RegionEditor.GroupRegions(data, new Dict { ["child_ids"] = buildRegionIds.Cast<object?>().ToList(), ["type"] = "negative" });
        var res = ApplyRuleEditor.CreateApplyRule(data, new Dict { ["region"] = neg["id"], ["block_place"] = "deny(void)", ["message"] = "You may not build here!" });
        return new Dict { ["region_id"] = neg["id"], ["rule_id"] = res["id"] };
    }

    /// <summary>Execute one template; returns {template, …applier result}. Throws on bad name/params.</summary>
    public static Dict ApplyTemplate(Dict data, string template, Dict p)
    {
        Dict result = template switch
        {
            "spawn_protection"       => ApplySpawnProtection(data, Str(p, "region"), Str(p, "team")),
            "wool_room_defense"      => ApplyWoolRoomDefense(data, Str(p, "region"), Str(p, "owner")),
            "wool_room_edit"         => ApplyWoolRoomEdit(data, Str(p, "region"), Str(p, "owner")),
            "build_void_enforcement" => ApplyBuildVoidEnforcement(data, StrList(p, "build_region_ids")),
            _ => throw EditException.BadRequest($"unknown template '{template}'"),
        };
        return new Dict(result) { ["template"] = template };
    }

    private static string Str(Dict p, string k) => p.GetValueOrDefault(k) as string
        ?? throw EditException.BadRequest($"missing param '{k}'");
    private static List<string> StrList(Dict p, string k) =>
        (p.GetValueOrDefault(k) as List<object?> ?? throw EditException.BadRequest($"missing param '{k}'"))
        .Select(x => x?.ToString() ?? "").Where(x => x.Length > 0).ToList();
}
