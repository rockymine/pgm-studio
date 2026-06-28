using System.Text.RegularExpressions;

namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;

/// <summary>
/// Build slice of the declarative generator (new-map-authoring.md §5; filter-region-wiring.md template
/// 1). Projects the build intent into the PGM document: the buildable rectangles (the over-void
/// bridges/platforms) unioned into <c>build-area</c>, optional no-build <see cref="MapIntent.Build"/>
/// holes subtracted as <c>buildable = complement(build-area, holes…)</c>, all wrapped in the
/// <c>not-build-area</c> negative with void enforcement — <c>block=no-void</c> where
/// <c>no-void = not(void)</c> — so players can't bridge over the void outside the buildable region and
/// terrain-backed columns stay editable. Sets the build height cap.
/// <para>Mirror of <c>RegionCategorizer</c>'s build derivation (<c>DeriveBuildIds</c> walks the
/// negative/complement subtree under a void rule): the areas, the union, the complement and the holes all
/// read back as <c>build</c>; the negative as <c>other</c> + <c>rule_container</c>.</para>
/// <para>Idempotent clear-then-build (the entity-replace save path rebuilds anyway).</para>
/// </summary>
public static class BuildGenerator
{
    private const string VoidMessage = "You may not edit the void!";

    /// <summary>Upper bound on the authored build-height cap (blocks). Keeps a stored/out-of-range value
    /// from generating a map with an unreasonable ceiling.</summary>
    public const int MaxBuildHeight = 100;

    public static void Apply(Dict doc, MapIntent intent)
    {
        Clear(doc);
        if (intent.Build is not { } b) return;
        if (b.MaxHeight is { } h) doc["max_build_height"] = Math.Min(h, MaxBuildHeight);

        if (b.Areas.Count == 0) return;

        var areaIds = CreateRects(doc, b.Areas, "build-area");

        // ≥2 rects → union them into build-area; a lone rectangle is the area region itself.
        string buildAreaId;
        if (areaIds.Count >= 2)
        {
            RegionEditor.GroupRegions(doc, new Dict { ["type"] = "union", ["id"] = "build-area", ["child_ids"] = areaIds });
            buildAreaId = "build-area";
        }
        else buildAreaId = (string)areaIds[0]!;

        // holes (no-build cutouts) → buildable = complement(build-area, hole…); none → buildable = build-area.
        string buildableId = buildAreaId;
        if (b.Holes.Count > 0)
        {
            var holeIds = CreateRects(doc, b.Holes, "build-hole");
            var children = new List<object?> { buildAreaId };
            children.AddRange(holeIds);
            RegionEditor.GroupRegions(doc, new Dict { ["type"] = "complement", ["id"] = "buildable", ["child_ids"] = children });
            buildableId = "buildable";
        }

        // "everywhere except the buildable region" — the void-enforcement wrapper.
        RegionEditor.GroupRegions(doc, new Dict { ["type"] = "negative", ["id"] = "not-build-area", ["child_ids"] = new List<object?> { buildableId } });

        // void enforcement: no-void = not(void); deny block edits where it's void, in not-build-area.
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = "is-void", ["type"] = "void" });
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = "no-void", ["type"] = "not", ["child"] = "is-void" });
        ApplyRuleEditor.CreateApplyRule(doc, new Dict { ["block"] = "no-void", ["region"] = "not-build-area", ["message"] = VoidMessage });
    }

    private static List<object?> CreateRects(Dict doc, List<Rect> rects, string prefix)
    {
        var ids = new List<object?>();
        var n = 1;
        foreach (var r in rects)
        {
            var id = $"{prefix}-{n++}";
            RegionEditor.CreateRegion(doc, new Dict
            {
                ["type"] = "rectangle", ["id"] = id, ["category"] = "build",
                ["min_x"] = r.MinX, ["min_z"] = r.MinZ, ["max_x"] = r.MaxX, ["max_z"] = r.MaxZ,
            });
            ids.Add(id);
        }
        return ids;
    }

    private static void Clear(Dict doc)
    {
        var regions = DocAccess.Regions(doc);
        foreach (var k in regions.Keys.Where(IsGenerated).ToList()) regions.Remove(k);
        DocAccess.Filters(doc).Remove("no-void");
        DocAccess.Filters(doc).Remove("is-void");
        if (doc.GetValueOrDefault("apply_rules") is List<object?> rules)
            rules.RemoveAll(r => r is Dict d && d.GetValueOrDefault("region") as string == "not-build-area");
    }

    private static bool IsGenerated(string k) =>
        k is "build-area" or "not-build-area" or "buildable"
        || Regex.IsMatch(k, @"^build-area-\d+$") || Regex.IsMatch(k, @"^build-hole-\d+$");
}
