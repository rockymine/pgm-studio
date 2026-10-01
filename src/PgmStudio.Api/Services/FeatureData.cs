using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Analysis.Scan;
using PgmStudio.Analysis.Playability;
using PgmStudio.Data.Map;
using PgmStudio.Data.Schema;
using PgmStudio.Domain;
using System.Text.Json.Nodes;
using PgmStudio.Api.Endpoints;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Data.Features;
using PgmStudio.Geom;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The one reader of a map's scan — its segment rows, surface layer, islands, configuration and bounds — and of
/// the feature rows beside it, in the analysis layer's shapes. A finished sketch's scan is a copy of the stored
/// layout, so it is brought up to that layout before anything is read from it
/// (<see cref="SketchFinish.RefreshAsync"/>): whichever route asks, it answers for the board as it is drawn now.
/// An endpoint reads the scan here and nowhere else, since a read that skips the refresh answers for the board
/// Finish saw.
/// </summary>
public sealed class FeatureData(PgmDb db, MapArtifactStore artifacts, PgmStudio.Data.Features.WorldFeatureWriter writer)
{
    /// <summary>The maps this request has already brought up to their layout.</summary>
    private readonly HashSet<long> current = [];

    /// <summary>Bring a finished sketch's scan up to its stored layout, once a request. A scan written again
    /// leaves a symmetry detected off the old islands behind, so an unconfirmed one is dropped to be detected
    /// again; a confirmed one is the author's answer and stays.</summary>
    private async Task CurrentAsync(long mapId, CancellationToken ct)
    {
        if (!current.Add(mapId)) return;
        if (!await SketchFinish.RefreshAsync(mapId, artifacts, writer, ct)) return;
        if (await SymmetryStore.LoadAsync(db, mapId, ct) is { Status: "unconfirmed" })
            await SymmetryStore.DeleteAsync(db, mapId, ct);
    }

    /// <summary>True when the map was world-scanned (has a cached raw layer artifact).</summary>
    public Task<bool> HasScanAsync(long mapId, CancellationToken ct = default)
        => artifacts.HasAsync(mapId, ArtifactKind.SurfaceParquet, ct);

    /// <summary>The canonical map bounding box (surface-layer extent saved at scan, islands-AABB fallback) —
    /// the finite clip box for unbounded <c>half</c>/<c>negative</c> regions. Null when neither is available.</summary>
    public async Task<((double, double, double, double) bounds, Dict dict)?> MapBboxAsync(long mapId, CancellationToken ct = default)
    {
        await CurrentAsync(mapId, ct);
        return await MapBounds.ResolveAsync(artifacts, mapId, ct);
    }

    public async Task<SegmentIndex?> SegmentsAsync(long mapId, CancellationToken ct = default)
    {
        var rows = await SegmentRowsAsync(mapId, ct: ct);
        if (rows.Count == 0) return null;
        var marks = await db.FloorMarks.Where(m => m.MapId == mapId).Select(m => new { m.WorldX, m.WorldZ }).ToListAsync(ct);
        var doors = await db.DoorRuns.Where(d => d.MapId == mapId).ToListAsync(ct);
        return new SegmentIndex(rows.Select(r => (r.WorldX, r.WorldZ, r.WorldYStart, r.WorldYEnd)),
                                marks.Select(m => (m.WorldX, m.WorldZ)),
                                doors.Select(d => (d.WorldX, d.WorldZ, d.WorldYStart, d.WorldYEnd)));
    }

    /// <summary>The map's segment rows, narrowed in the database by <paramref name="narrow"/> where a read
    /// wants a window or a column rather than the board.</summary>
    public async Task<List<SegmentRow>> SegmentRowsAsync(long mapId,
        Func<IQueryable<SegmentRow>, IQueryable<SegmentRow>>? narrow = null, CancellationToken ct = default)
    {
        await CurrentAsync(mapId, ct);
        var rows = db.Segments.Where(s => s.MapId == mapId);
        return await (narrow is null ? rows : narrow(rows)).ToListAsync(ct);
    }

    /// <summary>The scan's surface layer (<c>layer.parquet</c>), or null where the map has none.</summary>
    public async Task<byte[]?> SurfaceLayerAsync(long mapId, CancellationToken ct = default)
    {
        await CurrentAsync(mapId, ct);
        return await artifacts.LoadAsync(mapId, ArtifactKind.SurfaceParquet, ct);
    }

    /// <summary>The cells of a surface layer. The studio-chosen <c>scan_read</c> is served from the canonical
    /// <c>layer.parquet</c>; any other type from its per-type cache if one was stored. Null when neither is.
    /// Never scans the world — the hosted tier has no <c>.mca</c> files.</summary>
    public async Task<List<SurfaceCell>?> LayerCellsAsync(long mapId, string layerType, CancellationToken ct = default)
    {
        var scanRead = (await ScanConfigAsync(mapId, ct))["scan_read"]?.GetValue<string>() ?? "surface";
        if (layerType == scanRead && await SurfaceLayerAsync(mapId, ct) is { } canon)
            return await SurfaceScan.ReadAsync(canon);
        return await artifacts.LoadAsync(mapId, $"layer_{layerType}_parquet", ct) is { } cached
            ? await SurfaceScan.ReadAsync(cached) : null;
    }

    /// <summary>The detected islands as the scan wrote them (<c>islands.json</c>), or null.</summary>
    public async Task<byte[]?> IslandsAsync(long mapId, CancellationToken ct = default)
    {
        await CurrentAsync(mapId, ct);
        return await artifacts.LoadAsync(mapId, ArtifactKind.IslandsJson, ct);
    }

    /// <summary>The map's scan configuration (<see cref="ScanConfig"/>), defaults where none is stored.</summary>
    public async Task<JsonObject> ScanConfigAsync(long mapId, CancellationToken ct = default)
    {
        await CurrentAsync(mapId, ct);
        return await ScanConfig.LoadAsync(artifacts, mapId, ct);
    }

    /// <summary>The islands the author excluded from detection — the symmetry input, empty when none.</summary>
    public async Task<HashSet<int>> ExcludedIslandsAsync(long mapId, CancellationToken ct = default)
        => (await ScanConfigAsync(mapId, ct))["exclude_islands"]?.AsArray()
            .Select(n => n!.GetValue<int>()).ToHashSet() ?? [];

    /// <summary>The map's edit pass over its scan (<see cref="Editability"/>): the grid is the scan's bounds,
    /// or the regions and a margin where there is no scan, and the void test and the floor marks are the
    /// scan's y=0 course. The scan comes back beside it, null where the map has none.</summary>
    public async Task<(Editability.Result Zones, SegmentIndex? Segments)> ZonesAsync(long mapId, Dict doc,
        CancellationToken ct = default)
    {
        var segments = await SegmentsAsync(mapId, ct);
        var grid = (await MapBboxAsync(mapId, ct))?.bounds is var (minX, minZ, maxX, maxZ)
            ? ((int)minX, (int)minZ, (int)maxX, (int)maxZ)
            : ((int, int, int, int)?)null;
        return (Editability.Compute(doc, segments?.Y0Columns(), grid, floorMarks: segments?.FloorMarks), segments);
    }

    /// <summary>The ground every connectivity read of a map walks, one per map. A board the studio builds from
    /// its stored layout walks that world (<see cref="PgmStudio.Export.BuiltWalk"/>) — its houses, trees and
    /// lava where they stand — so a verdict here is the one the export reaches. A map that ships its own world
    /// walks its scan (<see cref="WorldWalk.Ground"/>) sized to <paramref name="grid"/>, or to its regions and
    /// terrain without one. <paramref name="doc"/> states where building is granted either way.</summary>
    public async Task<WalkGround> WalkGroundAsync(long mapId, Dict doc,
        (int MinX, int MinZ, int MaxX, int MaxZ)? grid = null, CancellationToken ct = default) =>
        await BuiltAsync(mapId, ct) is ({ } built, _)
            ? PgmStudio.Export.BuiltWalk.Ground(built, doc)
            : WorldWalk.Ground(doc, await SegmentsAsync(mapId, ct), bbox: grid);

    /// <summary>The world a board the studio builds is, from its stored layout and intent, with the layout it
    /// was built from; null for a map that ships its own world. The same instance for as long as the documents
    /// are unchanged and the world is kept (<see cref="PgmStudio.Export.BuiltWorlds"/>).</summary>
    public async Task<(PgmStudio.Export.BuiltWorld World, string LayoutJson)?> BuiltAsync(long mapId, CancellationToken ct)
    {
        if (await artifacts.LoadAsync(mapId, ArtifactKind.SketchLayoutJson, ct) is not { } layout) return null;
        var layoutJson = System.Text.Encoding.UTF8.GetString(layout);
        var intent = await artifacts.LoadJsonOrEmptyAsync<PgmStudio.Pgm.Authoring.MapIntent>(
            mapId, ArtifactKind.MapIntentJson, ct);
        return (PgmStudio.Export.BuiltWorlds.Of(layoutJson, intent), layoutJson);
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
