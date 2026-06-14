using System.Text.RegularExpressions;

namespace PgmStudio.Pgm.Editing;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Build slice of the declarative generator (new-map-authoring.md §5; filter-region-wiring.md template
/// 1). Projects the build intent into the PGM document: the buildable rectangles (areas + bridges)
/// unioned into <c>build-area</c>, wrapped in the <c>not-build-area</c> negative, with void enforcement
/// — <c>block=no-void</c> where <c>no-void = not(void)</c> — so players can't bridge over the void
/// outside the build area. Sets the build height cap.
/// <para>Mirror of <c>RegionCategorizer</c>'s build derivation: the rectangles read back as
/// <c>build</c> and the negative as <c>other</c> + <c>rule_container</c>.</para>
/// <para>Idempotent clear-then-build (the entity-replace save path rebuilds anyway).</para>
/// </summary>
public static class BuildGenerator
{
    private const string VoidMessage = "You may not edit the void!";

    public static void Apply(Dict doc, MapIntent intent)
    {
        Clear(doc);
        if (intent.Build is not { } b) return;
        if (b.MaxHeight is { } h) doc["max_build_height"] = h;

        var rects = b.Areas;
        if (rects.Count == 0) return;

        var ids = new List<object?>();
        var n = 1;
        foreach (var r in rects)
        {
            var id = $"build-area-{n++}";
            RegionEditor.CreateRegion(doc, new Dict
            {
                ["type"] = "rectangle", ["id"] = id, ["category"] = "build",
                ["min_x"] = r.MinX, ["min_z"] = r.MinZ, ["max_x"] = r.MaxX, ["max_z"] = r.MaxZ,
            });
            ids.Add(id);
        }

        // ≥2 rects → union them into build-area; a lone rectangle is the build region itself.
        string buildAreaId;
        if (ids.Count >= 2)
        {
            RegionEditor.GroupRegions(doc, new Dict { ["type"] = "union", ["id"] = "build-area", ["child_ids"] = ids });
            buildAreaId = "build-area";
        }
        else buildAreaId = (string)ids[0]!;

        // "everywhere except the build area" — the void-enforcement wrapper.
        RegionEditor.GroupRegions(doc, new Dict { ["type"] = "negative", ["id"] = "not-build-area", ["child_ids"] = new List<object?> { buildAreaId } });

        // void enforcement: no-void = not(void); deny block edits where it's void, in not-build-area.
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = "is-void", ["type"] = "void" });
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = "no-void", ["type"] = "not", ["child"] = "is-void" });
        ApplyRuleEditor.CreateApplyRule(doc, new Dict { ["block"] = "no-void", ["region"] = "not-build-area", ["message"] = VoidMessage });
    }

    private static void Clear(Dict doc)
    {
        var regions = Regions(doc);
        foreach (var k in regions.Keys.Where(IsGenerated).ToList()) regions.Remove(k);
        Filters(doc).Remove("no-void");
        Filters(doc).Remove("is-void");
        if (doc.GetValueOrDefault("apply_rules") is List<object?> rules)
            rules.RemoveAll(r => r is Dict d && d.GetValueOrDefault("region") as string == "not-build-area");
    }

    private static bool IsGenerated(string k) =>
        k is "build-area" or "not-build-area" || Regex.IsMatch(k, @"^build-area-\d+$");

    private static Dict Regions(Dict doc) =>
        doc.TryGetValue("regions", out var r) && r is Dict d ? d : (Dict)(doc["regions"] = new Dict());
    private static Dict Filters(Dict doc) =>
        doc.TryGetValue("filters", out var f) && f is Dict d ? d : (Dict)(doc["filters"] = new Dict());
}
