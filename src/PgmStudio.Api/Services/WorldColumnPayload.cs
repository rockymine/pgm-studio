using PgmStudio.Contracts;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Api.Services;

/// <summary>
/// A built world projected into the payload the 3-D preview meshes: every column's solid runs, palette-indexed
/// (docs/tools/sketch.md). The client decides what is visible, because only the client knows which way the
/// camera is pointing; this says what is there.
///
/// <para>The encoding and why it is flat are <see cref="WorldColumnsDto"/>'s; this builds one from a
/// world.</para>
/// </summary>
public static class WorldColumnPayload
{
    /// <summary>The runs of every column holding anything.</summary>
    public static WorldColumnsDto Of(VoxelWorld world, BlockBox? within = null)
    {
        var palette = new List<string>();
        var index = new Dictionary<(int Id, int Data), int>();
        var cols = new List<int>();
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;

        foreach (var (x, z, runs) in WorldColumns.Of(world, within))
        {
            cols.Add(x); cols.Add(z); cols.Add(runs.Count);
            foreach (var run in runs)
            {
                var key = (run.BlockId, run.BlockData);
                if (!index.TryGetValue(key, out var slot))
                {
                    index[key] = slot = palette.Count;
                    palette.Add(BlockPalette.Hex(run.BlockId, run.BlockData));
                }
                cols.Add(run.YTop); cols.Add(run.YBottom); cols.Add(slot);
            }

            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
        }

        if (cols.Count == 0) return Empty();
        return new WorldColumnsDto(palette, cols, minX, minZ, maxX, maxZ);
    }

    /// <summary>The payload for a world with nothing in it — an empty run list over a degenerate box, so the
    /// client meshes nothing through the same path rather than branching on a null.</summary>
    public static WorldColumnsDto Empty() => new([], [], 0, 0, -1, -1);
}
