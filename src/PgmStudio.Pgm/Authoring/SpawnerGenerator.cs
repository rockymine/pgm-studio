using PgmStudio.Pgm.Editing;

namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Spawner slice of the declarative generator: the <c>&lt;spawner&gt;</c> elements a board mints its currency
/// with, the regions each one references, and the rule that keeps its ground intact.
///
/// <para><b>It stamps nothing and it is not an objective.</b> A generator is a clock and a place; PGM drops
/// the stack itself, so the slice writes elements and no blocks. It runs beside the shop slice for the same
/// reason that one does.</para>
///
/// <para><b>Three regions per spawner, because PGM's element names them by id rather than taking
/// coordinates.</b> The drop is a <c>point</c> on the block's centre; the reach is a <c>cylinder</c> based
/// there, so the clock runs while somebody is standing by rather than all match; and the keep is a
/// <c>cuboid</c> around it, unioned with every other spawner's into one <c>&lt;apply block="never"&gt;</c> —
/// a generator whose block can be mined out or walled in is a generator anyone can switch off.</para>
///
/// <para>Idempotent clear-then-build like every other slice, over the region names this one owns — a board
/// that states no spawner of its own leaves none behind, and the wool rooms' spawners are untouched because
/// they name regions of theirs.</para>
/// </summary>
public static class SpawnerGenerator
{
    /// <summary>The suffix on the region a spawner's stack lands in, and this slice's whole namespace: an
    /// entry naming one is this slice's to replace and anything else is somebody's.</summary>
    public const string DropSuffix = "-drop";

    /// <summary>The suffix on the region a player has to be standing in.</summary>
    public const string ReachSuffix = "-reach";

    /// <summary>The suffix on the region nobody may build in or dig out.</summary>
    public const string KeepSuffix = "-keep";

    /// <summary>The union of every spawner's kept ground, which the one apply rule is written over.</summary>
    public const string KeptRegionId = "spawner-protection";

    /// <summary>What a player is told when the rule turns an edit away.</summary>
    public const string KeptMessage = "You may not build or break around a spawner!";

    public static void Apply(Dict doc, MapIntent intent)
    {
        var regions = DocAccess.Regions(doc);
        var written = DocAccess.EnsureList(doc, "spawners");

        foreach (var previous in written.OfType<Dict>().ToList())
            if (Stem(previous) is { } stem)
                foreach (var suffix in new[] { DropSuffix, ReachSuffix, KeepSuffix }) regions.Remove(stem + suffix);
        written.RemoveAll(entry => entry is Dict spawner && Stem(spawner) is not null);
        regions.Remove(KeptRegionId);
        if (doc.GetValueOrDefault("apply_rules") is List<object?> rules)
            rules.RemoveAll(rule => rule is Dict applied
                && applied.GetValueOrDefault("region") as string == KeptRegionId);

        // A generator with nothing to drop produces nothing, and one with no id has no region to name.
        var stated = (intent.Spawners ?? [])
            .Where(spawner => spawner.Id.Trim().Length > 0
                              && spawner.Drops.Any(drop => drop.Material.Trim().Length > 0))
            .ToList();
        if (stated.Count == 0)
        {
            if (written.Count == 0) doc.Remove("spawners");
            return;
        }

        var kept = new List<string>();
        foreach (var spawner in stated)
        {
            var id = IntentNaming.Slug(spawner.Id);
            double x = Centre(spawner.At.X), y = spawner.At.Y, z = Centre(spawner.At.Z);

            foreach (var suffix in new[] { DropSuffix, ReachSuffix, KeepSuffix }) regions.Remove(id + suffix);
            RegionEditor.CreateRegion(doc, new Dict
            {
                ["type"] = "point", ["id"] = id + DropSuffix, ["category"] = "spawner",
                ["coords"] = new Dict { ["x"] = x, ["y"] = y, ["z"] = z },
            });
            RegionEditor.CreateRegion(doc, new Dict
            {
                ["type"] = "cylinder", ["id"] = id + ReachSuffix, ["category"] = "spawner",
                ["coords"] = new Dict
                {
                    ["base_x"] = x, ["base_y"] = y, ["base_z"] = z,
                    ["radius"] = Math.Max(1, spawner.Reach), ["height"] = SpawnerIntent.ReachHeight,
                },
            });

            // A cuboid's corners are block indices and both ends are inside it, so a box of N blocks reaches
            // (N−1)/2 either side of the drop's own block centre — which lands the corners on whole
            // coordinates and puts the drop in the middle, the way the corpus writes one.
            if (spawner.Protect > 0)
            {
                var half = (spawner.Protect - 1) / 2.0;
                var rise = (SpawnerIntent.ProtectHeight - 1) / 2.0;
                RegionEditor.CreateRegion(doc, new Dict
                {
                    ["type"] = "cuboid", ["id"] = id + KeepSuffix, ["category"] = "spawner",
                    ["coords"] = new Dict
                    {
                        ["min_x"] = x - half, ["min_y"] = y - rise, ["min_z"] = z - half,
                        ["max_x"] = x + half, ["max_y"] = y + rise, ["max_z"] = z + half,
                    },
                });
                kept.Add(id + KeepSuffix);
            }

            var entry = new Dict
            {
                ["spawn_region"] = id + DropSuffix,
                ["player_region"] = id + ReachSuffix,
                ["items"] = spawner.Drops
                    .Where(item => item.Material.Trim().Length > 0)
                    .Select(item => (object?)new Dict
                    {
                        ["material"] = item.Material.Trim(), ["amount"] = Math.Max(1, item.Amount),
                    }).ToList(),
            };
            if (spawner.Delay.Trim().Length > 0) entry["delay"] = spawner.Delay.Trim();
            if (spawner.MaxEntities is { } cap) entry["max_entities"] = cap;
            written.Add(entry);
        }

        // One rule over every spawner's kept ground rather than one each: the rule is the same sentence
        // whichever generator a player is standing at, and a union is what PGM reads it over.
        if (kept.Count == 0) return;
        var union = new Dict { ["id"] = KeptRegionId, ["type"] = "union", ["children"] = kept.Cast<object?>().ToList() };
        if (RegionBuilder.BuildUnionBounds(kept.Select(child => (Dict)regions[child]!)).bounds is { } bounds)
            union["bounds_2d"] = bounds;
        regions[KeptRegionId] = union;
        ApplyRuleEditor.CreateApplyRule(doc, new Dict
        {
            ["block"] = "never", ["region"] = KeptRegionId, ["message"] = KeptMessage,
        });
    }

    /// <summary>The spawner id an entry was written from, or null where the entry is not this slice's — a
    /// wool room's spawner names a region of its own and is left alone.</summary>
    private static string? Stem(Dict entry) =>
        entry.GetValueOrDefault("spawn_region") as string is { } id && id.EndsWith(DropSuffix)
            ? id[..^DropSuffix.Length]
            : null;

    /// <summary>The centre of the block a coordinate falls in, so a stack lands in the middle of a block.</summary>
    private static double Centre(double value) => Math.Floor(value) + 0.5;
}
