using fNbt;
using static PgmStudio.Minecraft.Anvil.Nbt;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Minecraft.Anvil;

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

/// <summary>A block at y=0 that no segment holds — the invisible block-36 marker, a glass floor sheet. PGM's
/// void filter reads (x, 0, z) and counts any block there, a removed block 36 included, so the column is not
/// void and may be built over; nobody stands on it, so it is no segment.</summary>
public readonly record struct FloorMarkFeature(int WorldX, int WorldZ, int BlockId);

/// <summary>A run of door blocks standing on solid ground — a doorway's glass, a wool room's pane wall, a
/// nether-brick-fence gate. Solid, so it stays in its segment; a walk that knows the map lets players break
/// blocks there opens it.</summary>
public readonly record struct DoorRunFeature(int WorldX, int WorldZ, int WorldYStart, int WorldYEnd);

/// <summary>
/// Locate specific block types across a set of region files — "where are the X blocks and what
/// are they?". <c>minecraft/layers.py</c>. Each method scans the decoded block stream / tile-entity NBT and
/// yields per-instance rows matching <c>wools/resources/chests/spawners/segments</c>.
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

    // Blocks a player walks through, which segment extraction skips: plants, torches, redstone, signs and
    // plates; what a player opens or pushes through (doors, fence gates) and the cobweb a wool-room entrance
    // is guarded with; and the PISTON_MOVING_PIECE marker (36) many CTW maps use as an invisible build boundary.
    private static readonly HashSet<int> SegmentExclude = new()
    {
        6, 31, 32, 37, 38, 39, 40, 50, 55, 59, 63, 65, 66, 69, 70, 71, 72,
        75, 76, 77, 78, 83, 104, 105, 106, 115, 141, 142, 143, 147, 148, 166,
        30,                                 // cobweb
        64, 193, 194, 195, 196, 197,        // wooden doors
        107, 183, 184, 185, 186, 187,       // fence gates
        68,                                 // wall sign
        36,
    };

    /// <summary>Wool blocks (id 35) with colour from the data nibble (→ wools.parquet).</summary>
    public static IEnumerable<WoolFeature> Wools(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        foreach (var chunk in chunks)
            foreach (var b in AnvilRegion.Blocks(chunk))
                if (b.Id == WoolId)
                    yield return new WoolFeature(b.X, b.Z, b.Y, PgmStudio.Domain.BlockColors.BlockColor(b.Data));
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

    /// <summary>Whether a block is ground a player stands on: not air, not one of the non-solid ids, and not a
    /// build-region marker laid at the world floor (<see cref="SurfaceExtractors.FloorMarkerIds"/>), the rule
    /// the island scan reads too.</summary>
    private static bool IsSolid(int id, int y) =>
        id != 0 && !SegmentExclude.Contains(id)
        && !(y <= SurfaceExtractors.FloorMarkerMaxY && SurfaceExtractors.FloorMarkerIds.Contains(id));

    /// <summary>Every y=0 block no segment holds (→ floor_marks.parquet): what makes a column not void to PGM
    /// without being ground.</summary>
    public static IEnumerable<FloorMarkFeature> FloorMarks(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            if (AnvilRegion.Sections(chunk).FirstOrDefault(section => section.SectionY == 0) is not { } floor)
                continue;
            for (var index = 0; index < 256; index++)
            {
                var id = floor.Ids[index];
                if (id == 0 || IsSolid(id, 0)) continue;
                yield return new FloorMarkFeature(chunk.ChunkX * 16 + (index & 15), chunk.ChunkZ * 16 + (index >> 4), id);
            }
        }
    }

    /// <summary>What a map closes a doorway with for players to break: the breakable door materials the
    /// stamper builds with (<see cref="PgmStudio.Domain.DoorMaterials"/>), plain glass and glass panes, and the
    /// nether brick fence. A cobweb is among the door materials but is walked through already.</summary>
    public static readonly IReadOnlySet<int> DoorIds = new HashSet<int>(
        PgmStudio.Domain.DoorMaterials.Breakable.Select(choice => choice.BlockId).Where(id => !SegmentExclude.Contains(id)))
    {
        20, 102,                            // glass, glass pane
        113,                                // nether brick fence
    };

    /// <summary>Every run of door blocks that closes a way through (→ door_runs.parquet): standing on solid
    /// ground that is not a door itself, at least two blocks tall or held under something solid, and with open
    /// space on both sides of it in a line — west and east, or north and south — at the height a player walks
    /// in. A doorway, a window and a pane wall have that; a run with air under it is a roof or a hanging floor,
    /// a single block with air over it is a floor course, and glass buried in a solid mass separates nothing.
    /// </summary>
    public static IEnumerable<DoorRunFeature> DoorRuns(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        var volumes = chunks.ToDictionary(chunk => (chunk.ChunkX, chunk.ChunkZ), AnvilRegion.FullVolume);
        bool Open(int x, int y, int z) =>
            !volumes.TryGetValue((x >> 4, z >> 4), out var full) || !IsSolid(full[(y << 8) | ((z & 15) << 4) | (x & 15)], y);

        foreach (var ((chunkX, chunkZ), full) in volumes)
            for (var col = 0; col < 256; col++)
            {
                int x = chunkX * 16 + (col & 15), z = chunkZ * 16 + (col >> 4);
                var runStart = -1;
                for (var y = 1; y < 256; y++)
                {
                    var id = full[(y << 8) | col];
                    var door = DoorIds.Contains(id) && IsSolid(id, y);
                    if (door && runStart < 0)
                    {
                        var below = full[((y - 1) << 8) | col];
                        if (IsSolid(below, y - 1) && !DoorIds.Contains(below)) runStart = y;
                    }
                    else if (!door && runStart >= 0)
                    {
                        var through = (Open(x - 1, runStart, z) && Open(x + 1, runStart, z))
                                      || (Open(x, runStart, z - 1) && Open(x, runStart, z + 1));
                        if (through && (y - runStart >= 2 || IsSolid(id, y)))
                            yield return new DoorRunFeature(x, z, runStart, y - 1);
                        runStart = -1;
                    }
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
                        var solid = IsSolid(full[(y << 8) | col], y);
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
