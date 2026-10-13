using PgmStudio.Geom;

namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;

/// <summary>
/// What a zone states about the region it covers (<c>docs/pgm/zones.md</c>): who may enter, what may be done to
/// its blocks, what may be used in it, the kit given inside and whether anyone is hurt there. Each rule is a filter
/// id, or null where the zone says nothing about it.
/// </summary>
public sealed record ZoneRules
{
    /// <summary>Who may walk in.</summary>
    public string? Enter { get; init; }
    public string? EnterMessage { get; init; }
    /// <summary>What may be placed and broken, as one rule; <see cref="BlockPlace"/> and
    /// <see cref="BlockBreak"/> state the two apart.</summary>
    public string? Block { get; init; }
    public string? BlockPlace { get; init; }
    public string? BlockBreak { get; init; }
    public string? EditMessage { get; init; }
    /// <summary>What may be used — a chest, a door, a button.</summary>
    public string? Use { get; init; }
    /// <summary>The kit given to whoever is inside, by id.</summary>
    public string? Kit { get; init; }
    /// <summary>Whether no player is hurt inside.</summary>
    public bool Harmless { get; init; }
}

/// <summary>
/// The one writer every zone goes through — a spawn's protection, a wool room, the build region's void rule and a
/// zone the author states: its region as a union of rects, and its rules as apply rules in one order, enter before
/// blocks before use before kit, with a harmless zone's deny written into the damage module.
/// </summary>
internal static class ZoneWriter
{
    /// <summary>The prefix of the damage filters a harmless zone writes, so a rebuild can find its own.</summary>
    public const string HarmlessPrefix = "__harmless-";

    /// <summary>A footprint of rects as the region <paramref name="id"/>: one rect is the region itself, several
    /// are <c>id-1…id-n</c> unioned into it.</summary>
    public static void Region(Dict doc, string id, string category, IReadOnlyList<Rect> rects)
    {
        Dict RectDict(string rid, Rect r) => new()
        {
            ["type"] = "rectangle", ["id"] = rid, ["category"] = category,
            ["coords"] = new Dict { ["min_x"] = r.MinX, ["min_z"] = r.MinZ, ["max_x"] = r.MaxX, ["max_z"] = r.MaxZ },
        };
        if (rects.Count == 1) { RegionEditor.CreateRegion(doc, RectDict(id, rects[0])); return; }
        var rectIds = new List<string>();
        for (var i = 0; i < rects.Count; i++)
        {
            var rid = $"{id}-{i + 1}";
            RegionEditor.CreateRegion(doc, RectDict(rid, rects[i]));
            rectIds.Add(rid);
        }
        RegionEditor.Gather(doc, id, rectIds);
    }

    /// <summary>Write <paramref name="rules"/> over <paramref name="regionId"/>.</summary>
    public static void Rules(Dict doc, string regionId, ZoneRules rules)
    {
        if (rules.Enter is { } enter)
            Apply(doc, new Dict { ["enter"] = enter, ["region"] = regionId }, rules.EnterMessage);
        if (rules.Block is { } block)
            Apply(doc, new Dict { ["block"] = block, ["region"] = regionId }, rules.EditMessage);
        else if (rules.BlockPlace is not null || rules.BlockBreak is not null)
        {
            var rule = new Dict();
            if (rules.BlockPlace is { } place) rule["block_place"] = place;
            if (rules.BlockBreak is { } breaking) rule["block_break"] = breaking;
            rule["region"] = regionId;
            Apply(doc, rule, rules.EditMessage);
        }
        if (rules.Use is { } use) Apply(doc, new Dict { ["use"] = use, ["region"] = regionId }, null);
        if (rules.Kit is { } kit) Apply(doc, new Dict { ["kit"] = kit, ["region"] = regionId }, null);
        if (rules.Harmless) Harmless(doc, regionId);
    }

    private static void Apply(Dict doc, Dict rule, string? message)
    {
        if (message is { Length: > 0 }) rule["message"] = message;
        ApplyRuleEditor.CreateApplyRule(doc, rule);
    }

    // No player is hurt inside: a deny over the region, asked of every hit before anything after it.
    private static void Harmless(Dict doc, string regionId)
    {
        var inside = $"__in-{regionId}";
        var deny = HarmlessPrefix + regionId;
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = inside, ["type"] = "region", ["region"] = regionId });
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = deny, ["type"] = "deny", ["child"] = inside });
        if (doc.GetValueOrDefault("damage") is not Dict damage) doc["damage"] = damage = new Dict { ["filters"] = new List<object?>() };
        if (damage.GetValueOrDefault("filters") is not List<object?> filters) damage["filters"] = filters = [];
        filters.Add(deny);
    }
}
