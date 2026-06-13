using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis;
using PgmStudio.Data;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>Loads a map's relational feature rows into the analysis layer's input shapes.</summary>
public sealed class FeatureData(PgmDb db)
{
    /// <summary>True when the map was world-scanned (has a cached raw layer artifact).</summary>
    public Task<bool> HasScanAsync(long mapId, CancellationToken ct = default)
        => db.Artifacts.AnyAsync(a => a.MapId == mapId && a.Kind == ArtifactKind.LayerParquet, ct);

    public async Task<SegmentIndex?> SegmentsAsync(long mapId, CancellationToken ct = default)
    {
        var rows = await db.LayerSegments.Where(s => s.MapId == mapId).ToListAsync(ct);
        return rows.Count == 0 ? null : new SegmentIndex(rows.Select(r => (r.WorldX, r.WorldZ, r.WorldYStart, r.WorldYEnd)));
    }

    public async Task<List<WoolSources.Source>> WoolSourcesAsync(long mapId, Dict doc, CancellationToken ct = default)
    {
        var sources = new List<WoolSources.Source>();
        foreach (var r in await db.WoolBlocks.Where(x => x.MapId == mapId).ToListAsync(ct))
            sources.Add(new("block", WoolColors.Normalize(r.Color), r.WorldX, r.WorldY, r.WorldZ, 1));
        foreach (var r in await db.ChestItems.Where(x => x.MapId == mapId).ToListAsync(ct))
            if (r.ItemId.Contains("wool", StringComparison.OrdinalIgnoreCase)
                && WoolColors.WoolDamageToColor.TryGetValue(r.ItemDamage, out var c))
                sources.Add(new("chest", c, r.WorldX, r.WorldY, r.WorldZ, r.Count));
        foreach (var r in await db.SpawnerBlocks.Where(x => x.MapId == mapId).ToListAsync(ct))
            if (r.SpawnsWool == true && r.SpawnItemDamage is { } dmg
                && WoolColors.WoolDamageToColor.TryGetValue(dmg, out var c))
                sources.Add(new("spawner", c, r.WorldX, r.WorldY, r.WorldZ, (r.SpawnCount ?? 1) == 0 ? 1 : r.SpawnCount ?? 1));
        sources.AddRange(WoolSources.PgmSpawnerSources(doc));   // PGM <spawner> modules (from the map XML)
        return sources;
    }

    public async Task<List<ResourceSources.Block>> ResourceBlocksAsync(long mapId, CancellationToken ct = default)
        => (await db.ResourceBlocks.Where(x => x.MapId == mapId).ToListAsync(ct))
            .Select(r => new ResourceSources.Block(r.ResourceType, r.WorldX, r.WorldY, r.WorldZ)).ToList();
}
