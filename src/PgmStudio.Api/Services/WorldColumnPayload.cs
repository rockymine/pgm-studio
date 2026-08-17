using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Api.Services;

using Dict = Dictionary<string, object?>;

/// <summary>
/// A built world projected into the payload the 3-D preview meshes: every column's solid runs, palette-indexed
/// (docs/tools/sketch.md). The client decides what is visible, because only the client knows which way the
/// camera is pointing; this says what is there.
///
/// <para>Flat integer arrays rather than a record per run, for the reason the relief contours are already flat
/// — a board is over a hundred thousand runs, and a pair of field names each would be most of the bytes. The
/// palette is the same trick <see cref="LayerData"/> plays on the paint overlay: a board is tens of thousands
/// of cells drawn from under twenty materials, so the colour goes out once and an index stands in for it.</para>
/// </summary>
public static class WorldColumnPayload
{
    /// <summary>The runs of every column holding anything, as
    /// <c>[x, z, runCount, (yTop, yBottom, paletteIndex) × runCount, …]</c> — one flat array walked with the
    /// count as its stride. Bounds are the footprint the columns cover, degenerate when there are none, so a
    /// caller decodes one shape either way.</summary>
    public static Dict Of(VoxelWorld world, BlockBox? within = null)
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
        return new Dict
        {
            ["palette"] = palette, ["cols"] = cols.ToArray(),
            ["min_x"] = minX, ["min_z"] = minZ, ["max_x"] = maxX, ["max_z"] = maxZ,
        };
    }

    /// <summary>The payload for a world with nothing in it — an empty run list over a degenerate box, so the
    /// client meshes nothing through the same path rather than branching on a null.</summary>
    public static Dict Empty() => new()
    {
        ["palette"] = Array.Empty<string>(), ["cols"] = Array.Empty<int>(),
        ["min_x"] = 0, ["min_z"] = 0, ["max_x"] = -1, ["max_z"] = -1,
    };
}
