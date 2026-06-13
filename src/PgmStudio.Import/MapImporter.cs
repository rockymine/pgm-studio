using System.Text.Json;
using LinqToDB;
using LinqToDB.Data;
using PgmStudio.Data;
using PgmStudio.Data.Repositories;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Import;

using Dict = Dictionary<string, object?>;

/// <summary>
/// Imports a processed map output directory into MariaDB: xml_data.json → entity rows (via the
/// validated codec), feature parquet → feature rows, and raw layer.parquet + the side JSON files
/// → map_artifact blobs. Re-importing a slug replaces it (FK cascade).
/// </summary>
public sealed class MapImporter(PgmDb db)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never };

    public sealed record Counts(int Regions, int Filters, int Wools, int Monuments, int Spawns,
        int WoolBlocks, int ResourceBlocks, int ChestItems, int SpawnerBlocks, int LayerSegments, int Artifacts);

    public async Task<Counts> ImportDirAsync(string slug, string dir)
    {
        var repo = new MapRepository(db);
        var existing = await repo.GetBySlugAsync(slug);
        if (existing is not null) await repo.DeleteMapAsync(existing.Id);

        var docDict = (Dict)JsonTree.FromJsonLenient(File.ReadAllText(Path.Combine(dir, "xml_data.json")))!;
        var m = Deserializer.FromDict(docDict);

        var now = DateTime.UtcNow;
        var mapId = await db.InsertWithInt64IdentityAsync(new MapRow
        {
            Slug = slug, Name = m.Name, Version = NullIfEmpty(m.Version), Gamemode = NullIfEmpty(m.Gamemode),
            Objective = NullIfEmpty(m.Objective), MaxBuildHeight = m.MaxBuildHeight, CreatedAt = now, UpdatedAt = now,
        });

        var mw = new MapWriter(db);
        await mw.WriteEntitiesAsync(mapId, m);
        await mw.WriteWoolsFromDocAsync(mapId, docDict);     // wools from the grouped doc
        var (wb, rb, ci, sb, ls) = await ImportFeaturesAsync(mapId, dir);
        var artifacts = await ImportArtifactsAsync(mapId, dir);

        var woolGroups = (docDict.GetValueOrDefault("wools") as List<object?> ?? []).OfType<Dict>().ToList();
        var monuments = woolGroups.Sum(g => (g.GetValueOrDefault("monuments") as List<object?>)?.Count ?? 0);
        return new Counts(m.Regions.Count, m.Filters.Count, woolGroups.Count,
            monuments, m.Spawns.Count + (m.ObserverSpawn is null ? 0 : 1), wb, rb, ci, sb, ls, artifacts);
    }

    private async Task<(int, int, int, int, int)> ImportFeaturesAsync(long mapId, string dir)
    {
        var wb = await BulkAsync(dir, "wools.parquet", r => new WoolBlockRow { MapId = mapId, WorldX = ParquetIo.I(r["world_x"]), WorldZ = ParquetIo.I(r["world_z"]), WorldY = ParquetIo.I(r["world_y"]), Color = ParquetIo.S(r["color"]) });
        var rb = await BulkAsync(dir, "resources.parquet", r => new ResourceBlockRow { MapId = mapId, WorldX = ParquetIo.I(r["world_x"]), WorldZ = ParquetIo.I(r["world_z"]), WorldY = ParquetIo.I(r["world_y"]), ResourceType = ParquetIo.S(r["resource_type"]) });
        var ci = await BulkAsync(dir, "chests.parquet", r => new ChestItemRow { MapId = mapId, WorldX = ParquetIo.I(r["world_x"]), WorldZ = ParquetIo.I(r["world_z"]), WorldY = ParquetIo.I(r["world_y"]), ChestType = ParquetIo.S(r["chest_type"]), Slot = ParquetIo.I(r["slot"]), ItemId = ParquetIo.S(r["item_id"]), ItemDamage = ParquetIo.I(r["item_damage"]), Count = ParquetIo.I(r["count"]) });
        var sb = await BulkAsync(dir, "spawners.parquet", r => new SpawnerBlockRow { MapId = mapId, WorldX = ParquetIo.I(r["world_x"]), WorldZ = ParquetIo.I(r["world_z"]), WorldY = ParquetIo.I(r["world_y"]), EntityId = r.GetValueOrDefault("entity_id") as string, SpawnsWool = ParquetIo.BN(r.GetValueOrDefault("spawns_wool")), SpawnItemId = r.GetValueOrDefault("spawn_item_id") as string, SpawnItemDamage = ParquetIo.IN(r.GetValueOrDefault("spawn_item_damage")), SpawnCount = ParquetIo.IN(r.GetValueOrDefault("spawn_count")), SpawnRange = ParquetIo.IN(r.GetValueOrDefault("spawn_range")), MinSpawnDelay = ParquetIo.IN(r.GetValueOrDefault("min_spawn_delay")), MaxSpawnDelay = ParquetIo.IN(r.GetValueOrDefault("max_spawn_delay")), RequiredPlayerRange = ParquetIo.IN(r.GetValueOrDefault("required_player_range")), MaxNearbyEntities = ParquetIo.IN(r.GetValueOrDefault("max_nearby_entities")) });
        var ls = await BulkAsync(dir, "layer_segments.parquet", r => new LayerSegmentRow { MapId = mapId, WorldX = ParquetIo.I(r["world_x"]), WorldZ = ParquetIo.I(r["world_z"]), WorldYStart = ParquetIo.I(r["world_y_start"]), WorldYEnd = ParquetIo.I(r["world_y_end"]) });
        return (wb, rb, ci, sb, ls);
    }

    private async Task<int> BulkAsync<T>(string dir, string file, Func<Dict, T> map) where T : class
    {
        var path = Path.Combine(dir, file);
        if (!File.Exists(path)) return 0;
        var rows = (await ParquetIo.ReadRowsAsync(path)).Select(map).ToList();
        if (rows.Count > 0) await db.BulkCopyAsync(rows);
        return rows.Count;
    }

    private async Task<int> ImportArtifactsAsync(long mapId, string dir)
    {
        var n = 0;
        foreach (var (file, kind) in new[]
                 {
                     ("layer.parquet", ArtifactKind.LayerParquet),
                     ("islands.json", ArtifactKind.IslandsJson),
                     // symmetry.json is intentionally NOT imported: symmetry is computed on demand by
                     // the B7 endpoint (the pipeline symmetry step isn't ported), and old pipeline
                     // outputs carry a stale pre-diagonal-modes format. The endpoint owns the cache.
                     ("map_config.json", ArtifactKind.MapConfigJson),
                 })
        {
            var path = Path.Combine(dir, file);
            if (!File.Exists(path)) continue;
            await db.InsertAsync(new MapArtifactRow { MapId = mapId, Kind = kind, Data = await File.ReadAllBytesAsync(path) });
            n++;
        }
        return n;
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────
    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
    private static string Slug(string v) => v.Trim().ToLowerInvariant().Replace(" ", "_");
    private static Dict Xyz(Vec3 v) => new() { ["x"] = v.X, ["y"] = v.Y, ["z"] = v.Z };
    private static string Json(object? v) => JsonSerializer.Serialize(v, JsonOpts);
    private static string? JsonOrNull(object? v, bool present) => present ? JsonSerializer.Serialize(v, JsonOpts) : null;

    private static string? SubsetJson(Dict d, params string[] exclude)
    {
        var copy = new Dict(d);
        foreach (var k in exclude) copy.Remove(k);
        return copy.Count == 0 ? null : JsonSerializer.Serialize(copy, JsonOpts);
    }
}
