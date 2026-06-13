using fNbt;

namespace PgmStudio.Minecraft;

/// <summary>A decoded block in world coordinates (old 1.8-format numeric id + data nibble).</summary>
public readonly record struct WorldBlock(int X, int Y, int Z, int Id, int Data);

/// <summary>
/// Reads old-format (1.8–1.12) Minecraft Anvil region files (<c>r.X.Z.mca</c>): region header →
/// per-chunk zlib/gzip NBT (via fNbt) → sections with numeric Blocks/Data/Add arrays. Modern C#
/// anvil libraries target the 1.13+ palette format, so this is hand-rolled for the numeric format
/// these CTW maps use.
/// </summary>
public static class AnvilRegion
{
    public sealed record Chunk(int ChunkX, int ChunkZ, NbtCompound Level);

    /// <summary>Decode every present chunk in a region file (regionX/Z taken from the filename).</summary>
    public static IEnumerable<Chunk> ReadChunks(string mcaPath)
    {
        var bytes = File.ReadAllBytes(mcaPath);
        if (bytes.Length < 8192) yield break;
        var parts = Path.GetFileNameWithoutExtension(mcaPath).Split('.');   // r.X.Z
        if (parts.Length < 3 || !int.TryParse(parts[1], out var regionX) || !int.TryParse(parts[2], out var regionZ)) yield break;

        for (var i = 0; i < 1024; i++)
        {
            var o = i * 4;
            var offset = (bytes[o] << 16) | (bytes[o + 1] << 8) | bytes[o + 2];   // in 4096-byte sectors
            if (offset == 0 || bytes[o + 3] == 0) continue;
            var bo = offset * 4096;
            if (bo + 5 > bytes.Length) continue;
            var length = (bytes[bo] << 24) | (bytes[bo + 1] << 16) | (bytes[bo + 2] << 8) | bytes[bo + 3];
            if (length <= 1 || bo + 5 + (length - 1) > bytes.Length) continue;
            var compression = bytes[bo + 4] switch { 1 => NbtCompression.GZip, 2 => NbtCompression.ZLib, _ => NbtCompression.AutoDetect };

            NbtCompound? level = null;
            try
            {
                var nf = new NbtFile();
                nf.LoadFromBuffer(bytes, bo + 5, length - 1, compression);
                level = nf.RootTag.Get<NbtCompound>("Level") ?? nf.RootTag;
            }
            catch { /* skip a corrupt chunk */ }
            if (level is null) continue;

            yield return new Chunk(regionX * 32 + (i & 31), regionZ * 32 + (i >> 5), level);
        }
    }

    /// <summary>A decoded 16×16×16 section: ids/data unpacked to one entry per cell, index <c>(y&lt;&lt;8)|(z&lt;&lt;4)|x</c>.</summary>
    public sealed record Section(int SectionY, ushort[] Ids, byte[] Data);

    /// <summary>Decode every well-formed section of a chunk in ascending Y order (port of <c>_iter_chunk_sections</c>).</summary>
    public static IEnumerable<Section> Sections(Chunk chunk)
    {
        if (chunk.Level.Get<NbtList>("Sections") is not { } sections) yield break;
        var parsed = new List<Section>();
        foreach (var sObj in sections)
        {
            if (sObj is not NbtCompound section) continue;
            if (section.Get<NbtByteArray>("Blocks")?.Value is not { Length: 4096 } blocks) continue;
            var dataRaw = section.Get<NbtByteArray>("Data")?.Value;
            if (dataRaw is not { Length: 2048 }) continue;
            var add = section.Get<NbtByteArray>("Add")?.Value;
            var sectionY = section.Get("Y")?.ByteValue ?? 0;

            var ids = new ushort[4096];
            var data = new byte[4096];
            for (var idx = 0; idx < 4096; idx++)
            {
                int id = blocks[idx];
                if (add is { Length: 2048 }) id |= Nibble(add, idx) << 8;
                ids[idx] = (ushort)id;
                data[idx] = (byte)Nibble(dataRaw, idx);
            }
            parsed.Add(new Section(sectionY, ids, data));
        }
        parsed.Sort((a, b) => a.SectionY.CompareTo(b.SectionY));
        foreach (var s in parsed) yield return s;
    }

    /// <summary>Yield every non-air block of a chunk in world coordinates.</summary>
    public static IEnumerable<WorldBlock> Blocks(Chunk chunk)
    {
        var baseX = chunk.ChunkX * 16;
        var baseZ = chunk.ChunkZ * 16;
        foreach (var (sectionY, ids, data) in Sections(chunk))
        {
            var baseY = sectionY * 16;
            for (var idx = 0; idx < 4096; idx++)
            {
                if (ids[idx] == 0) continue;   // air
                var lx = idx & 0xF;
                var lz = (idx >> 4) & 0xF;
                var ly = idx >> 8;
                yield return new WorldBlock(baseX + lx, baseY + ly, baseZ + lz, ids[idx], data[idx]);
            }
        }
    }

    /// <summary>
    /// Full (256-high) decoded block-id volume for a chunk, index <c>(y&lt;&lt;8)|(z&lt;&lt;4)|x</c> with
    /// y∈0..255. Absent sections stay 0 (air). Port of <c>_build_full_blocks</c>.
    /// </summary>
    public static ushort[] FullVolume(Chunk chunk)
    {
        var full = new ushort[256 * 256];   // 256 (y) × 256 (z·x)
        foreach (var (sectionY, ids, _) in Sections(chunk))
        {
            var yStart = sectionY * 16;
            if (yStart is < 0 or >= 256) continue;
            Array.Copy(ids, 0, full, yStart * 256, 4096);
        }
        return full;
    }

    private static int Nibble(byte[] arr, int index)
        => (index & 1) == 0 ? arr[index >> 1] & 0x0F : (arr[index >> 1] >> 4) & 0x0F;
}
