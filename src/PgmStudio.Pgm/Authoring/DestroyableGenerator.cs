using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;

/// <summary>
/// Destroyable (DTM) slice of the declarative generator. Per destroyable it emits a <c>cuboid</c> region
/// around the structure and the <c>&lt;destroyable&gt;</c> that scopes its goal to the region's
/// <c>materials</c> blocks.
/// <para>The region is the structure's own <see cref="DestroyableIntent.Box"/> — the box the stamper built
/// the blocks from — so the two cannot disagree (OB8). A destroyable whose box is unresolved is skipped
/// rather than given a guessed region: a region that misses its structure is a zero-health goal PGM accepts
/// with nothing louder than a warning, which is worse than no goal at all.</para>
/// <para>Generated regions are the exact structure bounds. Hand-authored maps draw a loose box around the
/// structure (OB12); that slack is a human artifact, not something to reproduce. Idempotent
/// clear-then-build.</para>
/// </summary>
public static class DestroyableGenerator
{
    public static void Apply(Dict doc, MapIntent intent)
    {
        Clear(doc);
        if (intent.Destroyables is null) return;

        foreach (var b in intent.Destroyables)
        {
            if (b.Box is not { } box) continue;

            var id = UniqueId(doc, IntentNaming.Slug(b.Name));
            var regionId = $"{id}-region";
            var max = box.CuboidMax;   // a PGM cuboid spans [min, max), so max is one past the last block

            RegionEditor.CreateRegion(doc, new Dict
            {
                ["type"] = "cuboid", ["id"] = regionId, ["category"] = "objective",
                ["min_x"] = box.MinX, ["min_y"] = box.MinY, ["min_z"] = box.MinZ,
                ["max_x"] = max.X, ["max_y"] = max.Y, ["max_z"] = max.Z,
            });

            Destroyables(doc).Add(new Dict
            {
                ["id"] = id,
                ["name"] = b.Name,
                ["owner"] = b.Owner,
                ["region"] = regionId,
                ["materials"] = b.Materials,
            });
        }
    }

    /// <summary>Drop what a previous run emitted, so regenerating is a replace rather than an append.</summary>
    private static void Clear(Dict doc)
    {
        var list = Destroyables(doc);
        var regions = DocAccess.Regions(doc);
        foreach (var d in list.OfType<Dict>())
            if (d.GetValueOrDefault("region") as string is { } r)
                regions.Remove(r);
        list.Clear();
    }

    private static List<object?> Destroyables(Dict doc)
    {
        if (doc.GetValueOrDefault("destroyables") is not List<object?> list)
            doc["destroyables"] = list = [];
        return list;
    }

    // Two teams' destroyables slug to the same word only if they are named the same, which the compiler's
    // owner-and-index naming rules out — but an authored name can collide, and a duplicate id silently
    // re-points every reference to one of them.
    private static string UniqueId(Dict doc, string baseId)
    {
        var taken = Destroyables(doc).OfType<Dict>()
            .Select(d => d.GetValueOrDefault("id") as string).Where(s => s is not null).ToHashSet();
        if (baseId.Length == 0) baseId = "destroyable";
        if (!taken.Contains(baseId)) return baseId;
        for (var i = 2; ; i++)
            if (!taken.Contains($"{baseId}-{i}")) return $"{baseId}-{i}";
    }
}
