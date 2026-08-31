using System.Text.RegularExpressions;

namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;
using PgmStudio.Geom;

/// <summary>
/// Build slice of the declarative generator (new-map-authoring.md §5/§5b; docs/pgm/filter-region-wiring.md
/// template 1). Projects the build intent into the PGM document. Two independent things it may wire, either
/// or both: the buildable rectangles (the over-void bridges/platforms) unioned into <c>build-area</c>,
/// optional no-build <see cref="MapIntent.Build"/> holes subtracted as
/// <c>buildable = complement(build-area, holes…)</c>, all wrapped in the <c>not-build-area</c> negative
/// with void enforcement — <c>block-place=no-void</c> where <c>no-void = not(void)</c> — so players can't
/// bridge over the void outside the buildable region and terrain-backed columns stay editable. Breaking is
/// the same rule with an exception: <c>block-break=over-void-breakable</c> also admits what the dressing
/// stage leaves hanging over the void, so a canopy past a coast can still be cut down instead of standing
/// there for the match. And, separately,
/// <see cref="BuildIntent.VoidEnforcement"/> — the corpus idiom
/// (<c>block-place=deny(void)</c> over everywhere minus its exclusions) — which fires whether or not
/// <see cref="BuildIntent.Areas"/> is declared, because a map with no buildable rectangles can still want the
/// void permanent. Sets the build height cap regardless of either.
/// <para>Mirror of <c>RegionCategorizer</c>'s build derivation (<c>DeriveBuildIds</c> walks the
/// negative/complement subtree under a void rule): the areas, the union, the complement and the holes all
/// read back as <c>build</c>; the negative as <c>other</c> + <c>rule_container</c>. The standalone
/// enforcement's exclusions read back the same way, mirroring how the categorizer already reads
/// <c>alpine_mining_ii</c>'s own <c>obs-spawn</c> exclusion.</para>
/// <para>Idempotent clear-then-build (the entity-replace save path rebuilds anyway).</para>
/// </summary>
public static class BuildGenerator
{
    private const string VoidMessage = "You may not edit the void!";
    private const string VoidEnforcementAreaId = "void-enforcement-area";
    private const string VoidEnforcementExclusionPrefix = "void-enforcement-exclusion";
    private const string OverVoidBreakable = "over-void-breakable";

    /// <summary>What the dressing stage puts over the void and a player must still be able to cut down: a
    /// tree's own two materials and every plant the flora overlay scatters. A canopy reaching past a coast
    /// lands in columns with nothing at y=0, and a void rule that covers breaking as well as placing seals
    /// it there for the rest of the match — ground a player stands beside and cannot clear.
    ///
    /// <para>Terrain-forming blocks are deliberately absent. The exception is for what decoration left over
    /// the void, not for the void's own ground: a sea stack or a crag is a shape the author built, and
    /// making stone breakable out there would let a team dismantle the board.</para></summary>
    private static readonly (string FilterId, string Material)[] OverVoidMaterials =
    [
        ("__ovb-log", "log"), ("__ovb-log2", "log 2"),
        ("__ovb-leaves", "leaves"), ("__ovb-leaves2", "leaves 2"),
        ("__ovb-grass", "long grass"), ("__ovb-dandelion", "yellow flower"),
        ("__ovb-flower", "red rose"), ("__ovb-double", "double plant"),
        ("__ovb-lily", "water lily"), ("__ovb-vine", "vine"),
    ];

    /// <summary>Upper bound on the authored build-height cap (blocks). Keeps a stored/out-of-range value
    /// from generating a map with an unreasonable ceiling.</summary>
    public const int MaxBuildHeight = 100;

    public static void Apply(Dict doc, MapIntent intent)
    {
        Clear(doc);
        if (intent.Build is not { } b) return;
        if (b.MaxHeight is { } h) doc["max_build_height"] = Math.Min(h, MaxBuildHeight);

        if (b.Areas.Count > 0) ApplyBuildAreas(doc, b);
        if (b.VoidEnforcement is { } voidEnforcement) ApplyVoidEnforcement(doc, voidEnforcement);
    }

    private static void ApplyBuildAreas(Dict doc, BuildIntent b)
    {
        var areaIds = CreateRects(doc, b.Areas, "build-area", "build");

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
            var holeIds = CreateRects(doc, b.Holes, "build-hole", "build");
            var children = new List<object?> { buildAreaId };
            children.AddRange(holeIds);
            RegionEditor.GroupRegions(doc, new Dict { ["type"] = "complement", ["id"] = "buildable", ["child_ids"] = children });
            buildableId = "buildable";
        }

        // "everywhere except the buildable region" — the void-enforcement wrapper.
        RegionEditor.GroupRegions(doc, new Dict { ["type"] = "negative", ["id"] = "not-build-area", ["child_ids"] = new List<object?> { buildableId } });

        // void enforcement, one scope at a time: no-void = not(void) denies placing where the column is void,
        // and breaking is the same rule with what decoration leaves over the void carved out of it.
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = "is-void", ["type"] = "void" });
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = "no-void", ["type"] = "not", ["child"] = "is-void" });
        EnsureOverVoidBreakable(doc);
        ApplyRuleEditor.CreateApplyRule(doc, new Dict
        {
            ["block_place"] = "no-void", ["block_break"] = OverVoidBreakable,
            ["region"] = "not-build-area", ["message"] = VoidMessage,
        });
    }

    /// <summary>The corpus idiom, standalone: deny placing (not breaking) over the void, applied everywhere
    /// except the stated exclusions — no build area required. No exclusions → the region is the PGM builtin
    /// <c>everywhere</c> region itself, materialised under <see cref="VoidEnforcementAreaId"/> so a re-apply
    /// can find and clear exactly what it wrote (<c>region="everywhere"</c> bare would also work, since PGM
    /// pre-registers it, but then two calls to this method couldn't tell "no exclusions" apart from "an
    /// unrelated rule some other feature also scoped to everywhere").</summary>
    private static void ApplyVoidEnforcement(Dict doc, VoidEnforcementIntent voidEnforcement)
    {
        if (voidEnforcement.Exclusions.Count > 0)
        {
            // negative(exclusion…) = not(union(exclusion…)) = everywhere minus the exclusions — the same
            // region alpine_mining_ii spells complement(everywhere, union(exclusion…)), with no explicit
            // `everywhere` node needed (PGM's <negative> already unions its children before negating).
            var exclusionIds = CreateRects(doc, voidEnforcement.Exclusions, VoidEnforcementExclusionPrefix, "other");
            RegionEditor.GroupRegions(doc, new Dict { ["type"] = "negative", ["id"] = VoidEnforcementAreaId, ["child_ids"] = exclusionIds });
        }
        else
        {
            DocAccess.Regions(doc)[VoidEnforcementAreaId] = new Dict { ["id"] = VoidEnforcementAreaId, ["type"] = "everywhere" };
        }

        // block-place, not block: a player may still break a block hanging over the void (alpine_mining_ii's
        // own comment states this is deliberate), only placing new blocks out there is denied. Breaking is
        // left unstated rather than granted, which is the same permission by PGM's default and the shape the
        // corpus writes.
        ApplyRuleEditor.CreateApplyRule(doc, new Dict
        {
            ["block_place"] = "deny(void)", ["region"] = VoidEnforcementAreaId, ["message"] = VoidMessage,
        });
    }

    /// <summary>The break-side filter: allow over ground exactly as the place side does, and over the void
    /// allow only what decoration puts there. One <c>any</c> of synthetic leaves so the serializer inlines
    /// it, the same idiom the wool-room whitelist uses.</summary>
    private static void EnsureOverVoidBreakable(Dict doc)
    {
        if (DocAccess.Filters(doc).ContainsKey(OverVoidBreakable)) return;

        var materials = new List<object?>();
        foreach (var (filterId, material) in OverVoidMaterials)
        {
            FilterEditor.CreateFilter(doc, new Dict { ["id"] = filterId, ["type"] = "material", ["material"] = material });
            materials.Add(filterId);
        }
        FilterEditor.CreateFilter(doc, new Dict { ["id"] = "__ovb-any", ["type"] = "any", ["children"] = materials });
        FilterEditor.CreateFilter(doc, new Dict
        {
            ["id"] = "__ovb-over-void", ["type"] = "all",
            ["children"] = new List<object?> { "__ovb-any", "is-void" },
        });
        FilterEditor.CreateFilter(doc, new Dict
        {
            ["id"] = OverVoidBreakable, ["type"] = "any",
            ["children"] = new List<object?> { "__ovb-over-void", "no-void" },
        });
    }

    private static List<object?> CreateRects(Dict doc, List<Rect> rects, string prefix, string category)
    {
        var ids = new List<object?>();
        var n = 1;
        foreach (var r in rects)
        {
            var id = $"{prefix}-{n++}";
            RegionEditor.CreateRegion(doc, new Dict
            {
                ["type"] = "rectangle", ["id"] = id, ["category"] = category,
                ["coords"] = new Dict { ["min_x"] = r.MinX, ["min_z"] = r.MinZ, ["max_x"] = r.MaxX, ["max_z"] = r.MaxZ },
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
        DocAccess.Filters(doc).Remove(OverVoidBreakable);
        DocAccess.Filters(doc).Remove("__ovb-any");
        DocAccess.Filters(doc).Remove("__ovb-over-void");
        foreach (var (filterId, _) in OverVoidMaterials) DocAccess.Filters(doc).Remove(filterId);
        if (doc.GetValueOrDefault("apply_rules") is List<object?> rules)
            rules.RemoveAll(r => r is Dict d && d.GetValueOrDefault("region") as string is "not-build-area" or VoidEnforcementAreaId);
    }

    private static bool IsGenerated(string k) =>
        k is "build-area" or "not-build-area" or "buildable" or VoidEnforcementAreaId
        || Regex.IsMatch(k, @"^build-area-\d+$") || Regex.IsMatch(k, @"^build-hole-\d+$")
        || Regex.IsMatch(k, @"^void-enforcement-exclusion-\d+$");
}
