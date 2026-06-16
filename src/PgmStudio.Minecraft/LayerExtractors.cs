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
    // Default exclude for the Base extractor — the PISTON_MOVING_PIECE build-boundary marker
    // (reference _DEFAULT_BASE_EXCLUDE). Surface/Y0/Bedrock have no default exclusion.
    private static readonly HashSet<int> DefaultBaseExclude = [36];

    /// <summary>
    /// The "cleaned base" exclude set for new-map island detection (ND2 §6a / A5). The bottom-up base
    /// scan "looks up" into anything over the terrain, so on decorated worlds it catches connected masses
    /// that destabilise the island picture. This set is the corpus-derived noise the reference project's
    /// per-map `map_layouts.json` hand-excluded — water/lava (the usual island *bridge*), foliage
    /// (overlapping leaves/logs/canopy), redstone lines, and cobweb — unioned with the {36} marker.
    /// (Specks like a lone cobweb never form an island anyway; <see cref="IslandDetector"/>'s min-size
    /// prune drops them. This set targets the *connected* masses.)
    /// </summary>
    public static readonly IReadOnlySet<int> CleanBaseExclude = new HashSet<int>
    {
        36,                                 // piston_moving_piece (the existing Base default)
        8, 9, 10, 11,                       // water (flow/still), lava (flow/still) — the bridge
        6, 17, 18, 31, 32, 106, 111, 161, 162, // sapling, log, leaves, tallgrass, deadbush, vine, lily_pad, leaves2, log2
        55, 75, 76, 131, 132,               // redstone wire, redstone torch (off/on), tripwire hook, tripwire (string)
        30,                                 // cobweb
    };

    /// <summary>The Base extractor with the cleaned-base noise exclusion (<see cref="CleanBaseExclude"/>) —
    /// the detection layer for new-map authoring (ND2 §6a). Carries per-cell <c>WorldY</c> for the
    /// height-aware island detection that prunes floating builds.</summary>
    public static IEnumerable<SurfaceBlock> CleanBase(IEnumerable<AnvilRegion.Chunk> chunks) =>
        Base(chunks, (ISet<int>)CleanBaseExclude);

    /// <summary>Full 256-high id + data volumes for a chunk; index (y&lt;&lt;8)|(z&lt;&lt;4)|x. Absent sections stay air (0).</summary>
    private static (ushort[] ids, byte[] data) BuildVolume(AnvilRegion.Chunk chunk)
    {
        var ids = new ushort[256 * 256];
        var data = new byte[256 * 256];
        foreach (var (sectionY, sIds, sData) in AnvilRegion.Sections(chunk))
        {
            var yStart = sectionY * 16;
            if (yStart is < 0 or >= 256) continue;
            Array.Copy(sIds, 0, ids, yStart * 256, 4096);
            Array.Copy(sData, 0, data, yStart * 256, 4096);
        }
        return (ids, data);
    }

    public static IEnumerable<SurfaceBlock> Surface(
        IEnumerable<AnvilRegion.Chunk> chunks, ISet<int>? excludeIds = null, int? maxBuildHeight = null)
    {
        var exclude = excludeIds ?? new HashSet<int>();
        foreach (var chunk in chunks)
        {
            var (ids, data) = BuildVolume(chunk);
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

    /// <summary>Non-air blocks at world y=0 (port of Y0Extractor). WorldY is always 0.</summary>
    public static IEnumerable<SurfaceBlock> Y0(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            var (ids, data) = BuildVolume(chunk);
            var baseX = chunk.ChunkX * 16;
            var baseZ = chunk.ChunkZ * 16;
            for (var lz = 0; lz < 16; lz++)
                for (var lx = 0; lx < 16; lx++)
                {
                    var col = (lz << 4) | lx;       // y = 0 → index is just col
                    int id = ids[col];
                    if (id != 0) yield return new SurfaceBlock(baseX + lx, baseZ + lz, 0, id, data[col]);
                }
        }
    }

    /// <summary>Lowest bedrock block (id=7) per column, bottom-up (port of BedrockExtractor).</summary>
    public static IEnumerable<SurfaceBlock> Bedrock(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            var (ids, data) = BuildVolume(chunk);
            var baseX = chunk.ChunkX * 16;
            var baseZ = chunk.ChunkZ * 16;
            for (var lz = 0; lz < 16; lz++)
                for (var lx = 0; lx < 16; lx++)
                {
                    var col = (lz << 4) | lx;
                    for (var y = 0; y <= 255; y++)
                    {
                        if (ids[(y << 8) | col] != 7) continue;
                        yield return new SurfaceBlock(baseX + lx, baseZ + lz, y, 7, data[(y << 8) | col]);
                        break;
                    }
                }
        }
    }

    /// <summary>Lowest non-excluded non-air block per column, bottom-up (port of BaseExtractor;
    /// default exclude = {36}). Works for bedrock-floored, raised-floor and floating-island maps.</summary>
    public static IEnumerable<SurfaceBlock> Base(IEnumerable<AnvilRegion.Chunk> chunks, ISet<int>? excludeIds = null)
    {
        var exclude = excludeIds ?? DefaultBaseExclude;
        foreach (var chunk in chunks)
        {
            var (ids, data) = BuildVolume(chunk);
            var baseX = chunk.ChunkX * 16;
            var baseZ = chunk.ChunkZ * 16;
            for (var lz = 0; lz < 16; lz++)
                for (var lx = 0; lx < 16; lx++)
                {
                    var col = (lz << 4) | lx;
                    for (var y = 0; y <= 255; y++)
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
