using PgmStudio.Geom;

namespace PgmStudio.Minecraft.Anvil;

/// <summary>
/// One storey of a built world, as a world of its own: the blocks a named sketch layer laid down, and
/// everything standing on them up to whatever the next layer starts at.
///
/// <para>The reads that project a column to one cell — top-down, heightmap, surface, structures — draw the
/// highest thing in it, so on a stacked board they draw the topmost storey and nothing else. <c>ymax</c> is
/// the only cut they had, and a single height separates two storeys only where the upper one happens to be
/// flat: on <c>opus5-mineshaft</c> the deck roofs all 6,400 cells and the gallery under it is reachable at
/// <c>ymax=19</c> because that deck is level, which is a property of that board rather than of stacking.</para>
///
/// <para><b>A storey is the layer's own span plus what stands on it.</b> Keeping only the span would drop the
/// houses, the trees and the goal markers — which is most of what a picture of a storey is for — so the
/// window runs from the layer's floor up to the block below the next layer's floor in that same column, and
/// to the world ceiling for the topmost. A column the layer never drew contributes nothing, which is what
/// makes a gallery under a deck read as the gallery's own footprint rather than as the whole board.</para>
/// </summary>
public static class WorldStorey
{
    /// <summary>The world narrowed to one layer's storey, or the world itself where the layer is not named.
    /// Null where the spans carry no such layer — the caller's cue to refuse by naming the ones they do.
    /// </summary>
    /// <param name="world">The built world.</param>
    /// <param name="columns">The rasterizer's spans, which carry the layer that drew each.</param>
    /// <param name="layer">The layer id to keep. Null or empty answers <paramref name="world"/> unchanged.</param>
    public static VoxelWorld? Of(VoxelWorld world, IReadOnlyList<ColumnSegment>? columns, string? layer)
    {
        if (layer is not { Length: > 0 }) return world;
        if (columns is null || !columns.Any(segment => segment.Layer == layer)) return null;

        // Per column: where this layer starts, and where the next one up does. Both are read off the spans
        // rather than off the world, because the world no longer knows which slab any of its blocks came from.
        var window = new Dictionary<(int X, int Z), (int Floor, int Ceiling)>();
        foreach (var group in columns.GroupBy(segment => segment.Cell))
        {
            var ordered = group.OrderBy(segment => segment.YFloor).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Layer != layer) continue;
                var above = ordered.Skip(i + 1).Select(segment => segment.YFloor)
                                   .Where(floor => floor > ordered[i].YTop)
                                   .DefaultIfEmpty(VoxelWorld.MaxHeight).Min();
                window[group.Key] = (ordered[i].YFloor, above - 1);
                break;
            }
        }

        var storey = new VoxelWorld();
        foreach (var (x, z, runs) in WorldColumns.Of(world))
        {
            if (!window.TryGetValue((x, z), out var keep)) continue;
            foreach (var run in runs)
                for (var y = Math.Max(run.YBottom, keep.Floor); y <= Math.Min(run.YTop, keep.Ceiling); y++)
                    storey.SetBlock(x, y, z, run.BlockId, run.BlockData);
        }
        return storey;
    }

    /// <summary>The layer ids a board's spans carry, in the order the stack holds them — for the refusal that
    /// names what a caller could have asked for.</summary>
    public static IReadOnlyList<string> Names(IReadOnlyList<ColumnSegment>? columns)
        => [.. (columns ?? []).Select(segment => segment.Layer).Distinct()];
}
