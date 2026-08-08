using PgmStudio.Domain;

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
    /// The "cleaned base" exclude set for island detection. The bottom-up base scan "looks up" into anything
    /// over the terrain, so on decorated worlds it catches connected masses that destabilise the island
    /// picture. Everything here is <b>noise that is never ground</b>, whatever map it is in: water and lava
    /// (the usual island <i>bridge</i>), foliage (overlapping leaves/logs/canopy), redstone lines, cobweb, and
    /// the invisible block-36 marker. Specks like a lone cobweb never form an island anyway
    /// (<see cref="IslandDetector"/>'s min-size prune drops them); this set targets the <i>connected</i> masses.
    /// <para><b>Stained glass (95) is deliberately not here</b> — it moved to <see cref="FloorMarkerIds"/>,
    /// which applies only at the world floor. It sat here as a blanket exclusion, and that was two mistakes in
    /// one: it deleted glass wherever glass was lowest, which on the_high_seas meant a 30,872-column glass sea
    /// at y=58, and on rock_the_casbah 17,369 columns at y≈60–77. Neither is a marker; both are the map.</para>
    /// </summary>
    public static readonly IReadOnlySet<int> CleanBaseExclude = new HashSet<int>
    {
        36,                                 // piston_moving_piece (the existing Base default)
        8, 9, 10, 11,                       // water (flow/still), lava (flow/still) — the bridge
        6, 17, 18, 31, 32, 106, 111, 161, 162, // sapling, log, leaves, tallgrass, deadbush, vine, lily_pad, leaves2, log2
        55, 75, 76, 131, 132,               // redstone wire, redstone torch (off/on), tripwire hook, tripwire (string)
        30,                                 // cobweb
    };

    /// <summary>
    /// Blocks that are a <b>build-region marker</b> rather than ground, and only where a marker can do its job:
    /// the world floor. PGM's void filter reads <c>(x, 0, z)</c> and nothing else, so a sheet laid there makes
    /// the whole column buildable — which is why authors lay one, and why it is not terrain. The same sheet at
    /// y=58 is a glass roof over the sea.
    /// <para>The invisible block-36 marker needs no height rule (it is noise anywhere and lives in
    /// <see cref="CleanBaseExclude"/>); stained glass does, because it is also a building material. Measured on
    /// the corpus: every map whose glass floor bridges its islands lays it at y=0 — newgen, outlyne and
    /// rushers_vs_defenders at 100% — while the maps the blanket rule damaged carry theirs at y≥4.</para>
    /// </summary>
    public static readonly IReadOnlySet<int> FloorMarkerIds = new HashSet<int> { 95 };

    /// <summary>The highest Y a <see cref="FloorMarkerIds"/> block counts as a marker. A marker sheet spans the
    /// single floor layer, which maps write as a cuboid <c>y = 0..1</c>.</summary>
    public const int FloorMarkerMaxY = 1;

    /// <summary>Columns whose lowest non-air block is stained glass, with that block's Y — the measurement
    /// behind <see cref="FloorMarkerMaxY"/>, kept because the rule is only as good as that distribution.</summary>
    public static IEnumerable<(int X, int Z, int Y)> BaseGlassColumns(IEnumerable<AnvilRegion.Chunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            var (ids, _) = BuildVolume(chunk);
            for (var lz = 0; lz < 16; lz++)
                for (var lx = 0; lx < 16; lx++)
                {
                    var col = (lz << 4) | lx;
                    for (var y = 0; y <= 255; y++)
                    {
                        int id = ids[(y << 8) | col];
                        if (id == 0) continue;
                        if (id == 95) yield return (chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz, y);
                        break;
                    }
                }
        }
    }

    /// <summary>The Base extractor with the cleaned-base noise exclusion (<see cref="CleanBaseExclude"/>) —
    /// the detection layer for new-map authoring (ND2 §6a). Carries per-cell <c>WorldY</c> for the
    /// height-aware island detection that prunes floating builds.</summary>
    public static IEnumerable<SurfaceBlock> CleanBase(IEnumerable<AnvilRegion.Chunk> chunks) =>
        Base(chunks, (ISet<int>)CleanBaseExclude);

    /// <summary>A column of the cleaned world: its lowest cleaned-solid Y (<see cref="BaseY"/>, the footprint
    /// anchor + terrain reference) and every <see cref="Surfaces"/> Y a player can stand on (a cleaned-solid
    /// block with no cleaned-solid block directly above). The surfaces let island detection follow a walkable
    /// staircase up onto a raised structure instead of reading a cliff at the structure's high base.</summary>
    public readonly record struct CleanColumn(int WorldX, int WorldZ, int BaseY, IReadOnlyList<int> Surfaces);

    /// <summary>
    /// Per-column cleaned profile (<see cref="CleanColumn"/>) over the same noise exclusion as
    /// <see cref="CleanBase"/>: the lowest cleaned-solid block plus the set of standable surface heights.
    /// Where <see cref="CleanBase"/> keeps only the lowest cleaned block (so a structure over excluded
    /// foliage/water reads at its high floor and detaches from the terrace), this also reports the
    /// intermediate stair/ledge surfaces, so stair-aware detection can keep a walkable structure attached.
    /// </summary>
    /// <inheritdoc cref="CleanColumns(IEnumerable{AnvilRegion.Chunk}, PhantomErasure)"/>
    public static IEnumerable<CleanColumn> CleanColumns(IEnumerable<AnvilRegion.Chunk> chunks) =>
        CleanColumns(chunks, PhantomErasure.None);

    /// <param name="erased">What the map deletes before the first tick (<see cref="PhantomErasure"/>). Those
    /// blocks are skipped as if they were air, because at the moment the match starts that is what they are:
    /// a build-floor sheet laid to satisfy the void filter is gone before anyone plays, and counting it as
    /// ground merges the islands it bridges. <see cref="PhantomErasure.None"/> reads the world as it sits.</param>
    /// <inheritdoc cref="CleanColumns(IEnumerable{AnvilRegion.Chunk})"/>
    /// <param name="excludeIds">The noise set, defaulting to <see cref="CleanBaseExclude"/>. A parameter for the
    /// same reason <see cref="Base"/>'s is: a measurement that compares two readings of one world has to be able
    /// to state both.</param>
    public static IEnumerable<CleanColumn> CleanColumns(IEnumerable<AnvilRegion.Chunk> chunks, PhantomErasure erased,
                                                       IReadOnlySet<int>? excludeIds = null)
    {
        var exclude = excludeIds ?? CleanBaseExclude;
        foreach (var chunk in chunks)
        {
            var (ids, _) = BuildVolume(chunk);
            var baseX = chunk.ChunkX * 16;
            var baseZ = chunk.ChunkZ * 16;
            for (var lz = 0; lz < 16; lz++)
                for (var lx = 0; lx < 16; lx++)
                {
                    var col = (lz << 4) | lx;
                    var worldX = baseX + lx;
                    var worldZ = baseZ + lz;
                    int baseY = -1;
                    var surfaces = new List<int>();

                    bool Solid(int y)
                    {
                        int id = ids[(y << 8) | col];
                        if (id == 0 || exclude.Contains(id)) return false;
                        if (y <= FloorMarkerMaxY && FloorMarkerIds.Contains(id)) return false;   // build-region marker
                        return !erased.Erases(worldX, y, worldZ, id);
                    }

                    for (var y = 0; y <= 255; y++)
                    {
                        if (!Solid(y)) continue;                                  // air / noise / erased pre-game
                        if (baseY < 0) baseY = y;
                        if (y == 255 || !Solid(y + 1)) surfaces.Add(y);           // standable top
                    }
                    if (baseY >= 0) yield return new CleanColumn(worldX, worldZ, baseY, surfaces);
                }
        }
    }


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
