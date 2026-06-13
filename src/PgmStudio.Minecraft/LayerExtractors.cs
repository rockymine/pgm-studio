namespace PgmStudio.Minecraft;

/// <summary>A surface-scan cell: the chosen block of a column at world (x,z).</summary>
public readonly record struct SurfaceBlock(int WorldX, int WorldZ, int WorldY, int BlockId, int BlockData);

/// <summary>
/// Layer extractors — one row per column, the input to island detection. Port of the
/// <c>SurfaceExtractor</c> from <c>minecraft/layers.py</c> (the default <c>ScanConfig.layer</c>).
/// </summary>
public static class LayerExtractors
{
    /// <summary>
    /// Highest non-excluded, non-air block per column (top-down). <paramref name="maxBuildHeight"/>
    /// caps the scan (inclusive) to ignore structures above the playable ceiling.
    /// </summary>
    public static IEnumerable<SurfaceBlock> Surface(
        IEnumerable<AnvilRegion.Chunk> chunks, ISet<int>? excludeIds = null, int? maxBuildHeight = null)
    {
        var exclude = excludeIds ?? new HashSet<int>();
        foreach (var chunk in chunks)
        {
            // Full 256-high id + data volumes for the chunk, index (y<<8)|(z<<4)|x.
            var ids = new ushort[256 * 256];
            var data = new byte[256 * 256];
            foreach (var (sectionY, sIds, sData) in AnvilRegion.Sections(chunk))
            {
                var yStart = sectionY * 16;
                if (yStart is < 0 or >= 256) continue;
                Array.Copy(sIds, 0, ids, yStart * 256, 4096);
                Array.Copy(sData, 0, data, yStart * 256, 4096);
            }

            var baseX = chunk.ChunkX * 16;
            var baseZ = chunk.ChunkZ * 16;
            var yTop = maxBuildHeight is { } mh ? Math.Min(255, mh) : 255;
            if (yTop < 0) continue;

            for (var lz = 0; lz < 16; lz++)
                for (var lx = 0; lx < 16; lx++)
                {
                    var col = (lz << 4) | lx;
                    for (var y = yTop; y >= 0; y--)
                    {
                        int id = ids[(y << 8) | col];
                        if (id == 0 || exclude.Contains(id)) continue;
                        yield return new SurfaceBlock(baseX + lx, baseZ + lz, y, id, data[(y << 8) | col]);
                        break;
                    }
                }
        }
    }
}
