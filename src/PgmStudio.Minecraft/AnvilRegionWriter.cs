using fNbt;

namespace PgmStudio.Minecraft;

/// <summary>
/// Writes a <see cref="VoxelWorld"/> to old-format (1.8–1.12) Minecraft Anvil region files
/// (<c>r.X.Z.mca</c>): the mirror of <see cref="AnvilRegion"/>. Per region — an 8 KiB header
/// (1024-entry location table + timestamp table) followed by sector-aligned chunks, each a zlib
/// NBT payload with numeric <c>Blocks</c>/<c>Data</c>/<c>Add</c> sections. Each section also carries the
/// <c>BlockLight</c>/<c>SkyLight</c> nibble arrays and each chunk a <c>HeightMap</c> + <c>Biomes</c> — the
/// loader rejects a chunk missing any of them.
/// </summary>
public static class AnvilRegionWriter
{
    /// <summary>Encode <paramref name="world"/> into <c>r.X.Z.mca</c> files under <paramref name="regionDir"/>.</summary>
    public static void Write(VoxelWorld world, string regionDir)
    {
        Directory.CreateDirectory(regionDir);
        foreach (var region in world.Chunks.GroupBy(kv => (Rx: kv.Key.Cx >> 5, Rz: kv.Key.Cz >> 5)))
        {
            var path = Path.Combine(regionDir, $"r.{region.Key.Rx}.{region.Key.Rz}.mca");
            WriteRegion(path, region);
        }
    }

    private static void WriteRegion(string path, IEnumerable<KeyValuePair<(int Cx, int Cz), VoxelWorld.ChunkData>> chunks)
    {
        var location = new byte[4096];    // 1024 × (3-byte sector offset + 1-byte sector count)
        var timestamps = new byte[4096];  // 1024 × 4-byte timestamp (left zero)
        var body = new List<byte[]>();
        var nextSector = 2;               // chunk data starts after the two header sectors

        foreach (var (key, data) in chunks)
        {
            var nbt = new NbtFile(BuildChunkRoot(key.Cx, key.Cz, data));
            var compressed = nbt.SaveToBuffer(NbtCompression.ZLib);

            var payloadLen = compressed.Length + 1;      // + 1 compression byte
            var sectors = (4 + payloadLen + 4095) / 4096;
            var buf = new byte[sectors * 4096];
            buf[0] = (byte)(payloadLen >> 24);
            buf[1] = (byte)(payloadLen >> 16);
            buf[2] = (byte)(payloadLen >> 8);
            buf[3] = (byte)payloadLen;
            buf[4] = 2;                                    // zlib
            Array.Copy(compressed, 0, buf, 5, compressed.Length);
            body.Add(buf);

            var i = (key.Cx & 31) + (key.Cz & 31) * 32;
            location[i * 4] = (byte)(nextSector >> 16);
            location[i * 4 + 1] = (byte)(nextSector >> 8);
            location[i * 4 + 2] = (byte)nextSector;
            location[i * 4 + 3] = (byte)sectors;
            nextSector += sectors;
        }

        using var fs = File.Create(path);
        fs.Write(location);
        fs.Write(timestamps);
        foreach (var b in body) fs.Write(b);
    }

    private static NbtCompound BuildChunkRoot(int cx, int cz, VoxelWorld.ChunkData chunk)
    {
        var sections = new NbtList("Sections", NbtTagType.Compound);
        for (var sy = 0; sy < 16; sy++)
        {
            var ids = chunk.Ids[sy];
            if (ids is null) continue;
            var data = chunk.Data[sy] ?? new byte[4096];

            var blocks = new byte[4096];
            byte[]? add = null;
            for (var idx = 0; idx < 4096; idx++)
            {
                int id = ids[idx];
                blocks[idx] = (byte)(id & 0xFF);
                if (id > 0xFF) SetNibble(add ??= new byte[2048], idx, (id >> 8) & 0xF);
            }

            var sky = new byte[2048];
            Array.Fill(sky, (byte)0xFF);   // full skylight (nibble 15) — a lit arena; no relight needed

            var section = new NbtCompound
            {
                new NbtByte("Y", (byte)sy),
                new NbtByteArray("Blocks", blocks),
                new NbtByteArray("Data", PackNibbles(data)),
                // The loader requires both nibble arrays per section (2048 bytes each) — omitting them
                // aborts the chunk read ("ChunkNibbleArrays should be 2048 bytes not: 0").
                new NbtByteArray("BlockLight", new byte[2048]),
                new NbtByteArray("SkyLight", sky),
            };
            if (add is not null) section.Add(new NbtByteArray("Add", add));
            sections.Add(section);
        }

        var biomes = new byte[256];
        Array.Fill(biomes, (byte)1);   // plains everywhere

        var level = new NbtCompound("Level")
        {
            new NbtInt("xPos", cx),
            new NbtInt("zPos", cz),
            new NbtByte("TerrainPopulated", 1),
            new NbtByte("LightPopulated", 1),   // light is authored above; the server trusts it
            new NbtLong("LastUpdate", 0),
            new NbtLong("InhabitedTime", 0),
            new NbtByteArray("Biomes", biomes),
            // The loader requires a 256-int column heightmap ("heightmap array length is 0 instead of 256").
            new NbtIntArray("HeightMap", HeightMap(chunk)),
            sections,
        };

        var tiles = new NbtList("TileEntities", NbtTagType.Compound);
        foreach (var t in chunk.TileEntities) tiles.Add(t);
        level.Add(tiles);

        var entities = new NbtList("Entities", NbtTagType.Compound);
        foreach (var e in chunk.Entities) entities.Add(e);
        level.Add(entities);

        return new NbtCompound("") { level };
    }

    /// <summary>The per-column heightmap: 256 ints (indexed <c>z*16 + x</c>), each the Y just above the
    /// highest non-air block in that column (0 for an empty column).</summary>
    private static int[] HeightMap(VoxelWorld.ChunkData chunk)
    {
        var height = new int[256];
        for (var lx = 0; lx < 16; lx++)
        for (var lz = 0; lz < 16; lz++)
        {
            var h = 0;
            for (var sy = 15; sy >= 0 && h == 0; sy--)
            {
                var ids = chunk.Ids[sy];
                if (ids is null) continue;
                for (var y = 15; y >= 0; y--)
                    if (ids[(y << 8) | (lz << 4) | lx] != 0) { h = sy * 16 + y + 1; break; }
            }
            height[lz * 16 + lx] = h;
        }
        return height;
    }

    /// <summary>Pack one nibble per cell into 2048 bytes (inverse of <see cref="AnvilRegion"/>'s reader).</summary>
    private static byte[] PackNibbles(byte[] values)
    {
        var packed = new byte[2048];
        for (var i = 0; i < 4096; i++) SetNibble(packed, i, values[i] & 0xF);
        return packed;
    }

    private static void SetNibble(byte[] arr, int index, int value)
    {
        var b = arr[index >> 1];
        arr[index >> 1] = (index & 1) == 0
            ? (byte)((b & 0xF0) | (value & 0x0F))
            : (byte)((b & 0x0F) | ((value & 0x0F) << 4));
    }
}
