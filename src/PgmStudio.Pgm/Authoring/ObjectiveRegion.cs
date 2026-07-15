using PgmStudio.Domain;

namespace PgmStudio.Pgm.Authoring;

using PgmStudio.Pgm.Editing;
using Dict = Dictionary<string, object?>;

/// <summary>
/// The half a destroyable and a core generate identically: a cuboid region around the stamped structure, and
/// an id that no sibling already took. Shared so the two objectives cannot drift into emitting different
/// region geometry for the same box.
/// </summary>
internal static class ObjectiveRegion
{
    /// <summary>
    /// Emit the <c>&lt;region&gt;</c> for a stamped structure and return its id. The box is the stamper's own
    /// (OB8) and is written out with an exclusive max, because a PGM cuboid spans blocks <c>[min, max)</c> —
    /// an inclusive max would scope a region the structure's far face falls outside of (OB13).
    /// </summary>
    public static string Emit(Dict doc, string objectiveId, BlockBox box)
    {
        var regionId = $"{objectiveId}-region";
        var max = box.CuboidMax;
        RegionEditor.CreateRegion(doc, new Dict
        {
            ["type"] = "cuboid", ["id"] = regionId, ["category"] = "objective",
            ["min_x"] = box.MinX, ["min_y"] = box.MinY, ["min_z"] = box.MinZ,
            ["max_x"] = max.X, ["max_y"] = max.Y, ["max_z"] = max.Z,
        });
        return regionId;
    }

    /// <summary>An id not already taken by a sibling objective. A duplicate id silently re-points every
    /// reference to whichever won, so a collision must resolve rather than land.</summary>
    public static string UniqueId(List<object?> siblings, string baseId, string fallback)
    {
        var taken = siblings.OfType<Dict>()
            .Select(d => d.GetValueOrDefault("id") as string).Where(s => s is not null).ToHashSet();
        if (baseId.Length == 0) baseId = fallback;
        if (!taken.Contains(baseId)) return baseId;
        for (var i = 2; ; i++)
            if (!taken.Contains($"{baseId}-{i}")) return $"{baseId}-{i}";
    }

    /// <summary>Drop the objectives a previous run emitted, and the regions they owned, so regenerating is a
    /// replace rather than an append.</summary>
    public static void Clear(Dict doc, List<object?> list)
    {
        var regions = DocAccess.Regions(doc);
        foreach (var d in list.OfType<Dict>())
            if (d.GetValueOrDefault("region") as string is { } r)
                regions.Remove(r);
        list.Clear();
    }

    /// <summary>The doc's list for an objective kind, created on first use.</summary>
    public static List<object?> List(Dict doc, string key)
    {
        if (doc.GetValueOrDefault(key) is not List<object?> list)
            doc[key] = list = [];
        return list;
    }
}
