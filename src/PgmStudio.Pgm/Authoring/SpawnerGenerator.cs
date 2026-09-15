using PgmStudio.Pgm.Editing;

namespace PgmStudio.Pgm.Authoring;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Spawner slice of the declarative generator: the <c>&lt;spawner&gt;</c> elements a board mints its currency
/// with, and the regions each one references.
///
/// <para><b>It stamps nothing and it is not an objective.</b> A generator is a clock and a place; PGM drops
/// the stack itself, so the slice writes elements and no blocks. It runs beside the shop slice for the same
/// reason that one does.</para>
///
/// <para><b>The regions are minted here because PGM's element references them by id</b> rather than taking
/// coordinates. The drop becomes a <c>point</c> region on the block's centre, named for the spawner; who has
/// to be standing near is one <c>everywhere</c> region shared by every spawner on the board, which is what
/// 444 of the corpus's stated player-regions are.</para>
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

    /// <summary>The one region every studio-authored spawner takes as its <c>player-region</c>.</summary>
    public const string ReachRegionId = "spawner-reach";

    public static void Apply(Dict doc, MapIntent intent)
    {
        var regions = DocAccess.Regions(doc);
        var written = DocAccess.EnsureList(doc, "spawners");

        foreach (var previous in written.OfType<Dict>().ToList())
            if (previous.GetValueOrDefault("spawn_region") as string is { } id && id.EndsWith(DropSuffix))
                regions.Remove(id);
        written.RemoveAll(entry => entry is Dict spawner
            && spawner.GetValueOrDefault("spawn_region") as string is { } id && id.EndsWith(DropSuffix));
        regions.Remove(ReachRegionId);

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

        regions[ReachRegionId] = new Dict { ["id"] = ReachRegionId, ["type"] = "everywhere" };
        foreach (var spawner in stated)
        {
            var drop = IntentNaming.Slug(spawner.Id) + DropSuffix;
            regions.Remove(drop);
            RegionEditor.CreateRegion(doc, new Dict
            {
                ["type"] = "point", ["id"] = drop, ["category"] = "spawner",
                ["coords"] = new Dict
                {
                    ["x"] = Centre(spawner.At.X), ["y"] = spawner.At.Y, ["z"] = Centre(spawner.At.Z),
                },
            });

            var entry = new Dict
            {
                ["spawn_region"] = drop,
                ["player_region"] = ReachRegionId,
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
    }

    /// <summary>The centre of the block a coordinate falls in, so a stack lands in the middle of a block.</summary>
    private static double Centre(double value) => Math.Floor(value) + 0.5;
}
