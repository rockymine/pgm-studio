using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis.Scan;
using PgmStudio.Analysis.Playability;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using PgmStudio.Geom;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>Loads a map's relational feature rows into the analysis layer's input shapes. A finished sketch's
/// scan is brought up to the stored layout first (<see cref="SketchFinish.RefreshAsync"/>), so a read answers
/// for the board as it is drawn now.</summary>
public sealed class FeatureData(PgmDb db, MapArtifactStore artifacts, PgmStudio.Data.Features.WorldFeatureWriter writer)
{
    /// <summary>True when the map was world-scanned (has a cached raw layer artifact).</summary>
    public Task<bool> HasScanAsync(long mapId, CancellationToken ct = default)
        => artifacts.HasAsync(mapId, ArtifactKind.SurfaceParquet, ct);

    /// <summary>The canonical map bounding box (surface-layer extent saved at scan, islands-AABB fallback) —
    /// the finite clip box for unbounded <c>half</c>/<c>negative</c> regions. Null when neither is available.</summary>
    public async Task<((double, double, double, double) bounds, Dict dict)?> MapBboxAsync(long mapId, CancellationToken ct = default)
    {
        await SketchFinish.RefreshAsync(mapId, artifacts, writer, ct);
        return await MapBounds.ResolveAsync(artifacts, mapId, ct);
    }

    public async Task<SegmentIndex?> SegmentsAsync(long mapId, CancellationToken ct = default)
    {
        await SketchFinish.RefreshAsync(mapId, artifacts, writer, ct);
        var rows = await db.Segments.Where(s => s.MapId == mapId).ToListAsync(ct);
        if (rows.Count == 0) return null;
        var marks = await db.FloorMarks.Where(m => m.MapId == mapId).Select(m => new { m.WorldX, m.WorldZ }).ToListAsync(ct);
        var doors = await db.DoorRuns.Where(d => d.MapId == mapId).ToListAsync(ct);
        return new SegmentIndex(rows.Select(r => (r.WorldX, r.WorldZ, r.WorldYStart, r.WorldYEnd)),
                                marks.Select(m => (m.WorldX, m.WorldZ)),
                                doors.Select(d => (d.WorldX, d.WorldZ, d.WorldYStart, d.WorldYEnd)));
    }

    /// <summary>The ground every connectivity read of a map walks, one per map. A board the studio builds from
    /// its stored layout walks that world (<see cref="PgmStudio.Export.BuiltWalk"/>) — its houses, trees and
    /// lava where they stand — so a verdict here is the one the export reaches. A map that ships its own world
    /// walks its scan (<see cref="WorldWalk.Ground"/>) sized to <paramref name="grid"/>, or to its regions and
    /// terrain without one. <paramref name="doc"/> states where building is granted either way.</summary>
    public async Task<WalkGround> WalkGroundAsync(long mapId, Dict doc,
        (int MinX, int MinZ, int MaxX, int MaxZ)? grid = null, CancellationToken ct = default)
    {
        if (await artifacts.LoadAsync(mapId, ArtifactKind.SketchLayoutJson, ct) is { } layout)
        {
            var layoutJson = System.Text.Encoding.UTF8.GetString(layout);
            var intent = await artifacts.LoadJsonOrEmptyAsync<PgmStudio.Pgm.Authoring.MapIntent>(
                mapId, ArtifactKind.MapIntentJson, ct);
            return PgmStudio.Export.BuiltWalk.Ground(PgmStudio.Export.BuiltWorlds.Of(layoutJson, intent), doc, layoutJson);
        }
        return WorldWalk.Ground(doc, await SegmentsAsync(mapId, ct), bbox: grid);
    }

    /// <summary>The ground the map's stored plan covers, in world blocks, or null for a map built without one
    /// (<see cref="PgmStudio.Pgm.Derive.BoardDeriver.GroundBlocks"/>).</summary>
    public async Task<HashSet<(int X, int Z)>?> PlannedGroundAsync(long mapId, CancellationToken ct = default)
    {
        if (await artifacts.LoadAsync(mapId, ArtifactKind.PlanJson, ct) is not { } data) return null;
        return PgmStudio.Pgm.Plan.PlanModel.Stated(System.Text.Encoding.UTF8.GetString(data)) is { } plan
            ? PgmStudio.Pgm.Derive.BoardDeriver.GroundBlocks(plan)
            : null;
    }

    public async Task<List<WoolSources.Source>> WoolSourcesAsync(long mapId, Dict doc, CancellationToken ct = default)
    {
        var sources = new List<WoolSources.Source>();
        foreach (var r in await db.WoolBlocks.Where(x => x.MapId == mapId).ToListAsync(ct))
            sources.Add(new("block", BlockColors.Normalize(r.Color), r.WorldX, r.WorldY, r.WorldZ, 1));
        foreach (var r in await db.ChestItems.Where(x => x.MapId == mapId).ToListAsync(ct))
            if (r.ItemId.Contains("wool", StringComparison.OrdinalIgnoreCase)
                && BlockColors.BlockDamageToColor.TryGetValue(r.ItemDamage, out var c))
                sources.Add(new("chest", c, r.WorldX, r.WorldY, r.WorldZ, r.Count));
        foreach (var r in await db.SpawnerBlocks.Where(x => x.MapId == mapId).ToListAsync(ct))
            if (r.SpawnsWool == true && r.SpawnItemDamage is { } dmg
                && BlockColors.BlockDamageToColor.TryGetValue(dmg, out var c))
                sources.Add(new("spawner", c, r.WorldX, r.WorldY, r.WorldZ, (r.SpawnCount ?? 1) == 0 ? 1 : r.SpawnCount ?? 1));
        sources.AddRange(WoolSources.PgmSpawnerSources(doc, (await MapBboxAsync(mapId, ct))?.bounds));   // PGM <spawner> modules (from the map XML)
        return sources;
    }

    public async Task<List<ResourceSources.Block>> ResourceBlocksAsync(long mapId, CancellationToken ct = default)
        => (await db.ResourceBlocks.Where(x => x.MapId == mapId).ToListAsync(ct))
            .Select(r => new ResourceSources.Block(r.ResourceType, r.WorldX, r.WorldY, r.WorldZ)).ToList();
}
