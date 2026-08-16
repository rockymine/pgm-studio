using PgmStudio.Geom;

namespace PgmStudio.Minecraft.Anvil;

/// <summary>One vertical span of a column that is solid throughout and one material throughout:
/// <paramref name="YTop"/> and <paramref name="YBottom"/> are both inclusive, so a single block has them
/// equal. A run ends where the column turns to air or where the block changes, which is the same boundary
/// twice — what a surface stops at and what a colour changes at.</summary>
public readonly record struct ColumnRun(int YTop, int YBottom, int BlockId, int BlockData);

/// <summary>
/// A built <see cref="VoxelWorld"/> read back as per-column runs: everything solid in the world, in the order
/// a column stacks it, with nothing resolved a second time. Distinct from <see cref="WorldColumns"/>, which
/// answers <em>which</em> columns hold something and which hold a floor block — a membership question, over
/// the same chunk walk. This one answers what a column is made of, top to bottom.
///
/// <para>One structure answers three questions a top-only read cannot. A column with air in the middle keeps
/// both of its spans, so a sky bridge over ground reads as two things rather than one; a wall reports its own
/// courses rather than the surface colour smeared down it; and water is a run like any other, so a viewer that
/// stops at the topmost gets an opaque surface without a special case.</para>
///
/// <para>Reading the chunk sections directly rather than through <see cref="VoxelWorld.GetBlock"/> is what
/// makes a whole-board read cheap: one dictionary lookup per column instead of one per block, and a section
/// that was never allocated is skipped as a unit instead of probed sixteen times. It also settles the extent
/// — the allocated chunks <em>are</em> the built world, so nothing has to guess a bounding box or emit empty
/// columns to prove they are empty.</para>
/// </summary>
public static class WorldColumnRuns
{
    /// <summary>Every column holding at least one solid block, top run first. Columns come back in chunk
    /// order and, inside a chunk, in x-then-z order, so the same world reads back the same way twice.
    /// <paramref name="within"/> restricts the read on all three axes; omitted, the whole world is read.</summary>
    public static IEnumerable<(int X, int Z, IReadOnlyList<ColumnRun> Runs)> Of(
        VoxelWorld world, BlockBox? within = null)
    {
        // Chunk order is the dictionary's, which is insertion order in practice and nothing in principle;
        // sorting the keys costs one pass over a few hundred entries and buys a reproducible payload.
        var chunks = world.Chunks.OrderBy(entry => entry.Key.Cx).ThenBy(entry => entry.Key.Cz);

        foreach (var ((chunkX, chunkZ), chunk) in chunks)
        {
            for (var localX = 0; localX < 16; localX++)
            for (var localZ = 0; localZ < 16; localZ++)
            {
                var x = (chunkX << 4) | localX;
                var z = (chunkZ << 4) | localZ;
                if (within is { } box && (x < box.MinX || x > box.MaxX || z < box.MinZ || z > box.MaxZ)) continue;

                var runs = ColumnRuns(chunk, localX, localZ, within);
                if (runs.Count > 0) yield return (x, z, runs);
            }
        }
    }

    /// <summary>One column's runs, walked from the top of the world down so the list comes back top first.
    /// A run is carried while the block below matches it and closed by air, by a different block, by a
    /// section that was never allocated, or by the bottom of the read.</summary>
    private static List<ColumnRun> ColumnRuns(VoxelWorld.ChunkData chunk, int localX, int localZ, BlockBox? within)
    {
        var minY = within is { } box ? Math.Max(0, box.MinY) : 0;
        var maxY = within is { } clip ? Math.Min(VoxelWorld.MaxHeight - 1, clip.MaxY) : VoxelWorld.MaxHeight - 1;

        var runs = new List<ColumnRun>();
        int openTop = 0, openId = 0, openData = 0;
        var open = false;

        void Close(int bottom)
        {
            if (open) runs.Add(new ColumnRun(openTop, bottom, openId, openData));
            open = false;
        }

        for (var sectionY = maxY >> 4; sectionY >= minY >> 4; sectionY--)
        {
            var ids = chunk.Ids[sectionY];
            if (ids is null) { Close(((sectionY + 1) << 4)); continue; }   // a whole section of air
            var data = chunk.Data[sectionY];

            var from = Math.Min(maxY, ((sectionY + 1) << 4) - 1);
            var to = Math.Max(minY, sectionY << 4);
            for (var y = from; y >= to; y--)
            {
                var index = ((y & 15) << 8) | (localZ << 4) | localX;
                int id = ids[index];
                if (id == 0) { Close(y + 1); continue; }

                int value = data?[index] ?? 0;
                if (open && id == openId && value == openData) continue;   // the run carries on down
                Close(y + 1);
                openTop = y; openId = id; openData = value; open = true;
            }
        }

        Close(minY);
        return runs;
    }
}
