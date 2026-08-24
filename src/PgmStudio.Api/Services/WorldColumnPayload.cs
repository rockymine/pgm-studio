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
    /// <param name="segments">The rasterizer's own spans, which carry the layer that produced each. A run is
    /// attributed by where it <b>starts</b>: the painter writes several runs inside one span — a stone core
    /// and the bands over it — and every one of them begins inside the span that made the ground. A run
    /// beginning outside every span is a structure standing on the terrain rather than being it, and answers
    /// no layer. Absent, nothing is attributed and every run answers <c>-1</c>.</param>
    /// <param name="within">The box to read, or the whole world.</param>
    public static WorldColumnsDto Of(VoxelWorld world, IReadOnlyList<ColumnSegment>? segments = null,
        BlockBox? within = null)
    {
        var palette = new List<string>();
        var index = new Dictionary<(int Id, int Data), int>();
        var cols = new List<int>();
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;

        var layers = new List<string>();
        var layerOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var spans = new Dictionary<(int X, int Z), List<(int Floor, int Top, int Layer)>>();
        foreach (var segment in segments ?? [])
        {
            if (!layerOf.TryGetValue(segment.Layer, out var slot))
            {
                layerOf[segment.Layer] = slot = layers.Count;
                layers.Add(segment.Layer);
            }
            if (!spans.TryGetValue(segment.Cell, out var here)) spans[segment.Cell] = here = [];
            here.Add((segment.YFloor, segment.YTop, slot));
        }

        foreach (var (x, z, runs) in WorldColumns.Of(world, within))
        {
            cols.Add(x); cols.Add(z); cols.Add(runs.Count);
            spans.TryGetValue((x, z), out var here);
            foreach (var run in runs)
            {
                var key = (run.BlockId, run.BlockData);
                if (!index.TryGetValue(key, out var slot))
                {
                    index[key] = slot = palette.Count;
                    palette.Add(BlockPalette.Hex(run.BlockId, run.BlockData));
                }
                var layer = -1;
                foreach (var span in here ?? [])
                    if (span.Floor <= run.YBottom && run.YBottom <= span.Top) { layer = span.Layer; break; }
                cols.Add(run.YTop); cols.Add(run.YBottom); cols.Add(slot); cols.Add(layer);
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
