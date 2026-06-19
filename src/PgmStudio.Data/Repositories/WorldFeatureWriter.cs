using System.Text.Json.Nodes;
using LinqToDB;
using LinqToDB.Data;
using Parquet.Serialization;
using PgmStudio.Analysis.Footprint;
using PgmStudio.Minecraft;

namespace PgmStudio.Data.Repositories;

/// <summary>
/// Scans a Minecraft Anvil world (<c>region/*.mca</c>) with <see cref="FeatureExtractors"/> and
/// writes the resulting feature rows for a map — the world-import half of the pipeline (the xml
/// half is the importer / map editors). Replaces any existing feature rows for the map.
/// </summary>
public sealed class WorldFeatureWriter(PgmDb db)
{
    public readonly record struct Counts(int WoolBlocks, int ResourceBlocks, int ChestItems, int SpawnerBlocks, int LayerSegments, int Islands, int MonumentCandidates);

    /// <summary>One surface-scan row (layer.parquet schema); column names match the Python output.</summary>
    private sealed class LayerRow
    {
        [System.Text.Json.Serialization.JsonPropertyName("world_x")] public int WorldX { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("world_z")] public int WorldZ { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("world_y")] public int WorldY { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("block_id")] public int BlockId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("block_data")] public int BlockData { get; set; }
    }

    /// <summary>Read every <c>.mca</c> in <paramref name="regionDir"/> and write its features for <paramref name="mapId"/>.</summary>
    public async Task<Counts> WriteAsync(long mapId, string regionDir, CancellationToken ct = default)
    {
        // Materialise once — the region files are re-enumerated by each extractor.
        var chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks).ToList();

        await DeleteAsync(mapId, ct);

        var wool = FeatureExtractors.Wools(chunks)
            .Select(w => new WoolBlockRow { MapId = mapId, WorldX = w.WorldX, WorldZ = w.WorldZ, WorldY = w.WorldY, Color = w.Color }).ToList();
        var res = FeatureExtractors.Resources(chunks)
            .Select(r => new ResourceBlockRow { MapId = mapId, WorldX = r.WorldX, WorldZ = r.WorldZ, WorldY = r.WorldY, ResourceType = r.ResourceType }).ToList();
        var chests = FeatureExtractors.Chests(chunks)
            .Select(c => new ChestItemRow { MapId = mapId, WorldX = c.WorldX, WorldZ = c.WorldZ, WorldY = c.WorldY, ChestType = c.ChestType, Slot = c.Slot, ItemId = c.ItemId, ItemDamage = c.ItemDamage, Count = c.Count }).ToList();
        var spawners = FeatureExtractors.Spawners(chunks)
            .Select(s => new SpawnerBlockRow
            {
                MapId = mapId, WorldX = s.WorldX, WorldZ = s.WorldZ, WorldY = s.WorldY,
                EntityId = s.EntityId, SpawnsWool = s.SpawnsWool, SpawnItemId = s.SpawnItemId,
                SpawnItemDamage = s.SpawnItemDamage, SpawnCount = s.SpawnCount, SpawnRange = s.SpawnRange,
                MinSpawnDelay = s.MinSpawnDelay, MaxSpawnDelay = s.MaxSpawnDelay,
                RequiredPlayerRange = s.RequiredPlayerRange, MaxNearbyEntities = s.MaxNearbyEntities,
            }).ToList();
        var segs = FeatureExtractors.Segments(chunks)
            .Select(s => new LayerSegmentRow { MapId = mapId, WorldX = s.WorldX, WorldZ = s.WorldZ, WorldYStart = s.WorldYStart, WorldYEnd = s.WorldYEnd }).ToList();

        if (wool.Count > 0) await db.BulkCopyAsync(wool, ct);
        if (res.Count > 0) await db.BulkCopyAsync(res, ct);
        if (chests.Count > 0) await db.BulkCopyAsync(chests, ct);
        if (spawners.Count > 0) await db.BulkCopyAsync(spawners, ct);
        if (segs.Count > 0) await db.BulkCopyAsync(segs, ct);

        // Gather monument candidates over the whole world (F9) so the authoring tier can Score
        // suggestions without re-reading the .mca — idempotent delete-then-insert, like the features.
        var monuments = MonumentSuggester.Gather(chunks, WorldBox(chunks));
        var monCount = await MonumentCandidateStore.WriteAsync(db, mapId, monuments, ct);

        var islands = await WriteArtifactsAsync(mapId, chunks, ct);
        return new Counts(wool.Count, res.Count, chests.Count, spawners.Count, segs.Count, islands, monCount);
    }

    /// <summary>
    /// Persist the geometry artifacts for a <b>finished sketch</b> (docs/contracts/sketch-authoring.md §4):
    /// the rasterized cells become a synthetic surface layer (stone at Y=0) → layer.parquet, the supplied
    /// islands → islands.json, one single-block segment per column → layer_segment, plus the default
    /// map_config. The sketched map then has the same geometry shape an imported world does, so it flows
    /// into the Configure wizard. Replaces any prior features for the map.
    /// </summary>
    public async Task WriteSketchAsync(long mapId, IReadOnlyCollection<(int X, int Z)> cells, IReadOnlyList<IslandDetector.Island> islands, CancellationToken ct = default)
    {
        await DeleteAsync(mapId, ct);

        var layerRows = cells.Select(c => new LayerRow { WorldX = c.X, WorldZ = c.Z, WorldY = 0, BlockId = 1, BlockData = 0 }).ToList();
        byte[] layerBytes;
        using (var ms = new MemoryStream())
        {
            if (layerRows.Count > 0) await ParquetSerializer.SerializeAsync(layerRows, ms, cancellationToken: ct);
            layerBytes = ms.ToArray();
        }

        var segs = cells.Select(c => new LayerSegmentRow { MapId = mapId, WorldX = c.X, WorldZ = c.Z, WorldYStart = 0, WorldYEnd = 0 }).ToList();
        if (segs.Count > 0) await db.BulkCopyAsync(segs, ct);

        var config = new JsonObject
        {
            ["exclude_islands"] = new JsonArray(),
            ["exclude_blocks"] = new JsonArray(),
            ["scan_layer"] = "surface",
            ["scan_layer_confirmed"] = true,
            ["bounding_box"] = SurfaceBbox(cells.Select(c => (c.X, c.Z))),
        };

        await StoreArtifactAsync(mapId, ArtifactKind.LayerParquet, layerBytes, ct);
        await StoreArtifactAsync(mapId, ArtifactKind.IslandsJson,
            System.Text.Encoding.UTF8.GetBytes(IslandDetector.SerializeJson(islands)), ct);
        await StoreArtifactAsync(mapId, ArtifactKind.MapConfigJson,
            System.Text.Encoding.UTF8.GetBytes(config.ToJsonString()), ct);
    }

    /// <summary>The whole-world scan box for the monument gather (full chunk extent × full height).</summary>
    private static ScanBox WorldBox(IReadOnlyList<AnvilRegion.Chunk> chunks)
    {
        if (chunks.Count == 0) return new ScanBox(0, 0, 0, 0, 0, 0);
        int minX = chunks.Min(c => c.ChunkX) * 16, maxX = chunks.Max(c => c.ChunkX) * 16 + 15;
        int minZ = chunks.Min(c => c.ChunkZ) * 16, maxZ = chunks.Max(c => c.ChunkZ) * 16 + 15;
        return new ScanBox(minX, 0, minZ, maxX, 255, maxZ);
    }

    /// <summary>Persist the world-derived artifacts: the <b>Surface</b> layer → layer.parquet (the visual
    /// top-down render), island detection on the <b>cleaned Base</b> (ND2 §6a — height-aware, with a deferred
    /// y0/bedrock fallback for degenerate reads) → islands.json, and the initial map_config.json. Returns the
    /// island count. (Symmetry is derived from islands.json on demand by the B7 endpoint.)</summary>
    private async Task<int> WriteArtifactsAsync(long mapId, IReadOnlyList<AnvilRegion.Chunk> chunks, CancellationToken ct)
    {
        var surface = LayerExtractors.Surface(chunks).ToList();

        // Detection runs on the cleaned base, not the surface (decorated terrain makes the surface noisy).
        // Fallback layers are lazy — only scanned if the cleaned base reads degenerately (DetectCleaned).
        static (int X, int Z, int Y) Cell(SurfaceBlock b) => (b.WorldX, b.WorldZ, b.WorldY);
        var baseCells = LayerExtractors.CleanBase(chunks).Select(Cell).ToList();
        var fallbacks = new[] { LayerExtractors.Y0(chunks).Select(Cell), LayerExtractors.Bedrock(chunks).Select(Cell) };
        var islands = IslandDetector.DetectCleaned(baseCells, fallbacks);

        var layerRows = surface
            .Select(s => new LayerRow { WorldX = s.WorldX, WorldZ = s.WorldZ, WorldY = s.WorldY, BlockId = s.BlockId, BlockData = s.BlockData })
            .ToList();
        byte[] layerBytes;
        using (var ms = new MemoryStream())
        {
            if (layerRows.Count > 0) await ParquetSerializer.SerializeAsync(layerRows, ms, cancellationToken: ct);
            layerBytes = ms.ToArray();
        }

        var config = new JsonObject
        {
            ["exclude_islands"] = new JsonArray(),
            ["exclude_blocks"] = new JsonArray(),
            ["scan_layer"] = "cleanbase",
            ["scan_layer_confirmed"] = false,
            ["bounding_box"] = SurfaceBbox(surface.Select(s => (s.WorldX, s.WorldZ))),
        };

        await StoreArtifactAsync(mapId, ArtifactKind.LayerParquet, layerBytes, ct);
        await StoreArtifactAsync(mapId, ArtifactKind.IslandsJson,
            System.Text.Encoding.UTF8.GetBytes(IslandDetector.SerializeJson(islands)), ct);
        await StoreArtifactAsync(mapId, ArtifactKind.MapConfigJson,
            System.Text.Encoding.UTF8.GetBytes(config.ToJsonString()), ct);
        return islands.Count;
    }

    private async Task StoreArtifactAsync(long mapId, string kind, byte[] data, CancellationToken ct)
    {
        await db.Artifacts.Where(a => a.MapId == mapId && a.Kind == kind).DeleteAsync(ct);
        if (data.Length > 0)
            await db.InsertAsync(new MapArtifactRow { MapId = mapId, Kind = kind, Data = data }, token: ct);
    }

    // The surface-layer extent (min/max of the scanned surface cells) — the canonical map bounding box,
    // saved at scan and read back as the canvas frame + the analysis clip box. Null when there are no cells.
    private static JsonObject? SurfaceBbox(IEnumerable<(int X, int Z)> cells)
    {
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        var any = false;
        foreach (var (x, z) in cells)
        {
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            any = true;
        }
        return any ? new JsonObject { ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ } : null;
    }

    private async Task DeleteAsync(long mapId, CancellationToken ct)
    {
        await db.WoolBlocks.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.ResourceBlocks.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.ChestItems.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.SpawnerBlocks.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.MonumentCandidates.Where(x => x.MapId == mapId).DeleteAsync(ct);
        await db.LayerSegments.Where(x => x.MapId == mapId).DeleteAsync(ct);
    }
}
