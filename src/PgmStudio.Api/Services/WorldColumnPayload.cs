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
    /// <summary>The runs of every column holding anything, each attributed to the layer that drew it.</summary>
    /// <param name="world">The built world.</param>
    /// <param name="segments">The rasterizer's own spans, which carry the layer that produced each; a run takes
    /// the layer <see cref="WorldColumns.Attributed"/> attributes it to, and one standing on the terrain rather
    /// than being it answers <c>-1</c>. Absent, nothing is attributed and every run answers <c>-1</c>.</param>
    /// <param name="within">The box to read, or the whole world.</param>
    public static WorldColumnsDto Of(VoxelWorld world, IReadOnlyList<ColumnSegment>? segments = null,
        BlockBox? within = null)
    {
        var palette = new List<string>();
        var index = new Dictionary<(int Id, int Data), int>();
        // A biome-tinted block is one colour per column rather than one per pair, so it is keyed by the
        // colour the tint resolved to and shares a slot with every other column that came out the same.
        var tinted = new Dictionary<string, int>(StringComparer.Ordinal);
        var cols = new List<int>();
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;

        var layers = new List<string>();
        var layerOf = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var segment in segments ?? [])
            if (layerOf.TryAdd(segment.Layer, layers.Count)) layers.Add(segment.Layer);

        foreach (var (x, z, runs) in WorldColumns.Attributed(world, segments, within))
        {
            cols.Add(x); cols.Add(z); cols.Add(runs.Count);
            var biome = world.GetBiome(x, z);
            foreach (var (run, drawnOn) in runs)
            {
                int slot;
                if (BlockTints.IsTinted(run.BlockId, run.BlockData))
                {
                    var hex = BlockPalette.Hex(run.BlockId, run.BlockData, biome, x, z);
                    if (!tinted.TryGetValue(hex, out slot))
                    {
                        tinted[hex] = slot = palette.Count;
                        palette.Add(hex);
                    }
                }
                else if (!index.TryGetValue((run.BlockId, run.BlockData), out slot))
                {
                    index[(run.BlockId, run.BlockData)] = slot = palette.Count;
                    palette.Add(BlockPalette.Hex(run.BlockId, run.BlockData));
                }
                cols.Add(run.YTop); cols.Add(run.YBottom); cols.Add(slot);
                cols.Add(drawnOn is null ? -1 : layerOf[drawnOn]);
            }

            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
        }

        if (cols.Count == 0) return Empty();
        return new WorldColumnsDto(palette, cols, minX, minZ, maxX, maxZ, layers);
    }

    /// <summary>The payload for a world with nothing in it — an empty run list over a degenerate box, so the
    /// client meshes nothing through the same path rather than branching on a null.</summary>
    public static WorldColumnsDto Empty() => new([], [], 0, 0, -1, -1, []);
}
