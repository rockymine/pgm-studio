using PgmStudio.Analysis.Playability;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Export;

/// <summary>
/// The columns a world's map document opens to bridging, as <see cref="Editability"/> answers it — the set
/// <c>TraversabilityRender</c> draws as bridged. The render sits in <c>Minecraft</c>, which cannot see
/// <c>Analysis</c>, so the answer is taken here and handed in; the walk, coverage and dead-ground reads take
/// the same <see cref="Editability.Result.BridgeableCells"/>.
///
/// <para>Two readers of one world, because a world reaches the studio as region files or as the build in
/// hand. Both read the y=0 course — the layer PGM's <c>&lt;void/&gt;</c> filter reads — and the floor marks
/// on it (<see cref="FeatureExtractors.IsFloorMark"/>), and hand them to the one pass.</para>
/// </summary>
public static class BridgeableColumns
{
    /// <summary>The bridgeable columns of a world read from its chunks. The grid is the document's regions
    /// unioned with every chunk the world holds, each widened by <paramref name="margin"/> blocks.</summary>
    public static HashSet<(int X, int Z)> Of(
        IReadOnlyCollection<AnvilRegion.Chunk> chunks, Dictionary<string, object?> doc, int margin = 16)
    {
        var y0 = new HashSet<(int, int)>();
        var marks = new HashSet<(int, int)>();
        foreach (var chunk in chunks)
        {
            if (AnvilRegion.Sections(chunk).FirstOrDefault(section => section.SectionY == 0) is not { } floor)
                continue;
            for (var index = 0; index < 256; index++)
                Read(floor.Ids[index], (chunk.ChunkX * 16 + (index & 15), chunk.ChunkZ * 16 + (index >> 4)), y0, marks);
        }
        return [.. Open(doc, chunks.Select(chunk => (chunk.ChunkX, chunk.ChunkZ)), y0, marks, margin).BridgeableCells()];
    }

    /// <summary>The bridgeable columns of a world the studio has just built, read off its blocks.</summary>
    public static HashSet<(int X, int Z)> Of(VoxelWorld world, Dictionary<string, object?> doc, int margin = 16)
        => [.. Zones(world, doc, margin).BridgeableCells()];

    /// <summary>The whole edit pass over a world the studio has just built, for a caller that asks more of a
    /// column than whether it is bridged.</summary>
    public static Editability.Result Zones(VoxelWorld world, Dictionary<string, object?> doc, int margin = 16)
    {
        var y0 = new HashSet<(int, int)>();
        var marks = new HashSet<(int, int)>();
        foreach (var (chunkX, chunkZ) in world.ChunkCoords)
            for (var localZ = 0; localZ < 16; localZ++)
                for (var localX = 0; localX < 16; localX++)
                {
                    int x = chunkX * 16 + localX, z = chunkZ * 16 + localZ;
                    Read(world.GetBlock(x, 0, z).Id, (x, z), y0, marks);
                }
        return Open(doc, world.ChunkCoords, y0, marks, margin);
    }

    private static void Read(int id, (int X, int Z) cell, HashSet<(int, int)> y0, HashSet<(int, int)> marks)
    {
        if (id == 0) return;
        y0.Add(cell);
        if (FeatureExtractors.IsFloorMark(id)) marks.Add(cell);
    }

    private static Editability.Result Open(Dictionary<string, object?> doc,
        IEnumerable<(int ChunkX, int ChunkZ)> chunks, HashSet<(int, int)> y0, HashSet<(int, int)> marks, int margin)
    {
        var (minX, minZ, maxX, maxZ) = Editability.RegionBbox(doc, margin);
        foreach (var (chunkX, chunkZ) in chunks)
        {
            minX = Math.Min(minX, chunkX * 16 - margin);
            minZ = Math.Min(minZ, chunkZ * 16 - margin);
            maxX = Math.Max(maxX, chunkX * 16 + 16 + margin);
            maxZ = Math.Max(maxZ, chunkZ * 16 + 16 + margin);
        }
        return Editability.Compute(doc, y0, (minX, minZ, maxX, maxZ), floorMarks: marks);
    }
}
