using PgmStudio.Analysis.Playability;
using PgmStudio.Minecraft.Anvil;

namespace PgmStudio.Export;

/// <summary>
/// The columns a world's map document opens to bridging, as <see cref="Editability"/> answers it — the set
/// <c>TraversabilityRender</c> draws as bridged. The render sits in <c>Minecraft</c>, which cannot see
/// <c>Analysis</c>, so the answer is taken here and handed in; the walk, coverage and dead-ground reads take
/// the same <see cref="Editability.Result.BridgeableCells"/>.
/// </summary>
public static class BridgeableColumns
{
    /// <summary>The bridgeable columns of a world. The grid is the document's regions unioned with every
    /// chunk the world holds, each widened by <paramref name="margin"/> blocks, and the void test reads the
    /// chunks' own y=0 course — the layer PGM's <c>&lt;void/&gt;</c> filter reads.</summary>
    public static HashSet<(int X, int Z)> Of(
        IReadOnlyCollection<AnvilRegion.Chunk> chunks, Dictionary<string, object?> doc, int margin = 16)
    {
        var (minX, minZ, maxX, maxZ) = Editability.RegionBbox(doc, margin);
        var y0 = new HashSet<(int, int)>();
        foreach (var chunk in chunks)
        {
            minX = Math.Min(minX, chunk.ChunkX * 16 - margin);
            minZ = Math.Min(minZ, chunk.ChunkZ * 16 - margin);
            maxX = Math.Max(maxX, chunk.ChunkX * 16 + 16 + margin);
            maxZ = Math.Max(maxZ, chunk.ChunkZ * 16 + 16 + margin);

            if (AnvilRegion.Sections(chunk).FirstOrDefault(section => section.SectionY == 0) is not { } floor)
                continue;
            for (var localZ = 0; localZ < 16; localZ++)
                for (var localX = 0; localX < 16; localX++)
                    if (floor.Ids[(localZ << 4) | localX] != 0)
                        y0.Add((chunk.ChunkX * 16 + localX, chunk.ChunkZ * 16 + localZ));
        }

        return [.. Editability.Compute(doc, y0, (minX, minZ, maxX, maxZ)).BridgeableCells()];
    }
}
