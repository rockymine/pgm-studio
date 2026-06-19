using fNbt;
using static PgmStudio.Minecraft.Nbt;

namespace PgmStudio.Minecraft;

// Feature rows — one per instance, matching the parquet/DB feature-table shapes.
public readonly record struct WoolFeature(int WorldX, int WorldZ, int WorldY, string Color);
public readonly record struct ResourceFeature(int WorldX, int WorldZ, int WorldY, string ResourceType);
public readonly record struct ChestItemFeature(
    int WorldX, int WorldZ, int WorldY, string ChestType, int Slot, string ItemId, int ItemDamage, int Count);
public readonly record struct SpawnerFeature(
    int WorldX, int WorldZ, int WorldY, string? EntityId, bool SpawnsWool,
    string? SpawnItemId, int? SpawnItemDamage, int? SpawnCount, int? SpawnRange,
    int? MinSpawnDelay, int? MaxSpawnDelay, int? RequiredPlayerRange, int? MaxNearbyEntities);
public readonly record struct SegmentFeature(int WorldX, int WorldZ, int WorldYStart, int WorldYEnd);

/// <summary>
/// Locate specific block types across a set of region files — "where are the X blocks and what
/// are they?". Port of <c>minecraft/features.py</c> + the SegmentsExtractor from
/// <c>minecraft/layers.py</c>. Each method scans the decoded block stream / tile-entity NBT and
/// yields per-instance rows matching <c>wools/resources/chests/spawners/layer_segments</c>.
/// </summary>
public static class FeatureExtractors
{
    public const int WoolId = 35;

    /// <summary>Resource block id → label (iron/gold/diamond).</summary>
    public static readonly IReadOnlyDictionary<int, string> DefaultResourceBlocks = new Dictionary<int, string>
    {
        [41] = "gold_block", [42] = "iron_block", [57] = "diamond_block",
    };

    private static readonly HashSet<string> ChestTileIds = new(StringComparer.Ordinal) { "Chest", "TrappedChest" };

    // Non-solid decorative ids skipped in segment extraction (NON_SOLID_BLOCK_IDS), plus the
    // PISTON_MOVING_PIECE marker (36) many CTW maps use as an invisible build boundary.
    private static readonly HashSet<int> SegmentExclude = new()
    {
        6, 31, 32, 37, 38, 39, 40, 50, 55, 59, 63, 65, 66, 69, 70, 71, 72,
        75, 76, 77, 78, 83, 104, 105, 106, 115, 141, 142, 143, 147, 148, 166,
        36,
    };

    /// <summary>Wool blocks (id 35) with colour from the data nibble (→ wools.parquet).</summary>
    public static IEnumerable<WoolFeature> Wools(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        foreach (var chunk in chunks)
            foreach (var b in AnvilRegion.Blocks(chunk))
                if (b.Id == WoolId)
                    yield return new WoolFeature(b.X, b.Z, b.Y, PgmStudio.Domain.WoolColors.WoolColor(b.Data));
    }

    /// <summary>Iron/gold/diamond blocks with resource label (→ resources.parquet).</summary>
    public static IEnumerable<ResourceFeature> Resources(
        IEnumerable<AnvilRegion.Chunk> chunks, IReadOnlyDictionary<int, string>? targets = null)
    {
        targets ??= DefaultResourceBlocks;
        foreach (var chunk in chunks)
            foreach (var b in AnvilRegion.Blocks(chunk))
                if (targets.TryGetValue(b.Id, out var label))
                    yield return new ResourceFeature(b.X, b.Z, b.Y, label);
    }

    /// <summary>Chest / trapped-chest inventory from tile-entity NBT (→ chests.parquet).</summary>
    public static IEnumerable<ChestItemFeature> Chests(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            if (chunk.Level.Get<NbtList>("TileEntities") is not { } tiles) continue;
            foreach (var teObj in tiles)
            {
                if (teObj is not NbtCompound te) continue;
                var id = Str(te.Get("id"));
                if (id is null || !ChestTileIds.Contains(id)) continue;
                var chestType = id == "TrappedChest" ? "trapped_chest" : "chest";
                if (Int(te.Get("x")) is not { } wx || Int(te.Get("y")) is not { } wy || Int(te.Get("z")) is not { } wz)
                    continue;
                if (te.Get<NbtList>("Items") is not { } items) continue;
                foreach (var itObj in items)
                {
                    if (itObj is not NbtCompound item) continue;
                    yield return new ChestItemFeature(
                        wx, wz, wy, chestType,
                        Int(item.Get("Slot")) ?? 0,
                        Str(item.Get("id")) ?? "",
                        Int(item.Get("Damage")) ?? 0,
                        Int(item.Get("Count")) ?? 1);
                }
            }
        }
    }

    /// <summary>Mob-spawner config from tile-entity NBT; <c>SpawnsWool</c> flags wool respawners (→ spawners.parquet).</summary>
    public static IEnumerable<SpawnerFeature> Spawners(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            if (chunk.Level.Get<NbtList>("TileEntities") is not { } tiles) continue;
            foreach (var teObj in tiles)
            {
                if (teObj is not NbtCompound te) continue;
                if (Str(te.Get("id")) != "MobSpawner") continue;
                if (Int(te.Get("x")) is not { } wx || Int(te.Get("y")) is not { } wy || Int(te.Get("z")) is not { } wz)
                    continue;

                var spawnsWool = false;
                string? spawnItemId = null;
                int? spawnItemDamage = null;
                if (te.Get<NbtCompound>("SpawnData")?.Get<NbtCompound>("Item") is { } item)
                {
                    var itemId = Str(item.Get("id"));
                    if (itemId is not null)
                    {
                        spawnItemId = itemId;
                        spawnItemDamage = Int(item.Get("Damage"));
                        var lc = itemId.ToLowerInvariant();
                        spawnsWool = lc is "minecraft:wool" or "wool" or "35";
                    }
                }

                yield return new SpawnerFeature(
                    wx, wz, wy,
                    Str(te.Get("EntityId")),
                    spawnsWool, spawnItemId, spawnItemDamage,
                    Int(te.Get("SpawnCount")), Int(te.Get("SpawnRange")),
                    Int(te.Get("MinSpawnDelay")), Int(te.Get("MaxSpawnDelay")),
                    Int(te.Get("RequiredPlayerRange")), Int(te.Get("MaxNearbyEntities")));
            }
        }
    }

    /// <summary>All contiguous solid Y-runs per column, inclusive [start,end] (→ layer_segments.parquet).</summary>
    public static IEnumerable<SegmentFeature> Segments(IEnumerable<AnvilRegion.Chunk> chunks, int minRunLength = 1)
    {
        foreach (var chunk in chunks)
        {
            var full = AnvilRegion.FullVolume(chunk);   // (y<<8)|(z<<4)|x
            var baseX = chunk.ChunkX * 16;
            var baseZ = chunk.ChunkZ * 16;
            for (var lz = 0; lz < 16; lz++)
                for (var lx = 0; lx < 16; lx++)
                {
                    var col = (lz << 4) | lx;
                    var runStart = -1;
                    for (var y = 0; y < 256; y++)
                    {
                        var id = full[(y << 8) | col];
                        var solid = id != 0 && !SegmentExclude.Contains(id);
                        if (solid)
                        {
                            if (runStart < 0) runStart = y;
                        }
                        else if (runStart >= 0)
                        {
                            if (y - runStart >= minRunLength)
                                yield return new SegmentFeature(baseX + lx, baseZ + lz, runStart, y - 1);
                            runStart = -1;
                        }
                    }
                    if (runStart >= 0 && 256 - runStart >= minRunLength)
                        yield return new SegmentFeature(baseX + lx, baseZ + lz, runStart, 255);
                }
        }
    }
}
