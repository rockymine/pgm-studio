using PgmStudio.Analysis.Footprint;
using PgmStudio.Analysis.Layer;
using PgmStudio.Geom;

namespace PgmStudio.Analysis.Playability;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The built fidelity's answer to <see cref="Walk"/>: a board's own columns, its buildable void, and the
/// height under each cell, in blocks.
///
/// <para>A plan states a piece's surface and knows nothing of relief, a boulder or a pond; a built world
/// holds all three and states no pieces. Both fill in the same <see cref="WalkGround"/>, which is why one
/// traversal serves them — what they share is the per-cell answer, not a reader.</para>
///
/// <para>Two ways in, because a built world reaches the studio two ways and neither can pretend to be the
/// other. <see cref="Ground"/> reads a <b>scanned</b> map: segment rows and a map document, which is what an
/// imported world is. <see cref="OfBuilt"/> reads a world the studio just <b>built</b> from its own layout,
/// where the columns are in hand and no scan exists.</para>
/// </summary>
public static class WorldWalk
{
    /// <summary>The ground a walk runs over on a scanned board.</summary>
    /// <param name="data">The map document — read for the build zones and the apply rules that say where a
    /// player may place a block.</param>
    /// <param name="segments">The scanned columns. Absent, nothing is walkable, and a caller should say that
    /// rather than report a board with no ground.</param>
    /// <param name="margin">How far past the declared regions the grid reaches.</param>
    public static WalkGround Ground(Dict data, SegmentIndex? segments, int margin = 16)
    {
        var build = Buildability.Compute(data, segments?.Y0Columns(), null, margin);

        // The cleaned base footprint, not the raw surface: a build floating over void must not pose as free
        // standing-ground at its own high Y, because the grid a walk runs over is one cell per column.
        var ground = segments is null
            ? []
            : new HashSet<(int X, int Z)>(IslandDetector.CleanedBaseFootprint(segments.BaseColumns()));

        var bridgeable = new HashSet<(int X, int Z)>();
        for (var i = 0; i < build.Verdict.Length; i++)
        {
            if (build.Verdict[i] is not (0 or 3)) continue;      // buildable, or restricted-but-placeable
            var cell = (build.MinX + i % build.Width, build.MinZ + i / build.Width);
            if (!ground.Contains(cell)) bridgeable.Add(cell);
        }

        var surface = new Dictionary<(int X, int Z), int>();
        if (segments is not null)
            foreach (var (x, z, top) in segments.StandingTops())
                surface[(x, z)] = top;

        Level(bridgeable, surface);
        return new WalkGround(ground, bridgeable, surface,
            new CellRect(build.MinX, build.MinZ, build.Width, build.Height));
    }

    /// <summary>Give every bridgeable cell the height of the ground nearest it, spreading outward from the
    /// shores. A player bridging builds out level from where they left, so a crossing costs nothing until it
    /// reaches the far side — and then costs the rise onto it, which is the climb out of a gap and the most
    /// common climb on a capture board. Without this a bridge has no stated height at either end, and a step
    /// between two heights one of which is unknown charges nothing at all.</summary>
    private static void Level(IReadOnlySet<(int X, int Z)> bridgeable, Dictionary<(int X, int Z), int> surface)
    {
        var queue = new Queue<(int X, int Z)>();
        foreach (var cell in bridgeable)
            foreach (var side in Cells.N4(cell))
                if (surface.ContainsKey(side)) { queue.Enqueue(side); break; }

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            var height = surface[cell];
            foreach (var side in Cells.N4(cell))
                if (bridgeable.Contains(side) && surface.TryAdd(side, height)) queue.Enqueue(side);
        }
    }

    /// <summary>The ground a walk runs over on a world the studio built for itself, from the rasterised
    /// columns rather than from a scan.</summary>
    /// <param name="columns">Every solid span, as the rasterizer emits them. A cell's standing surface is the
    /// <b>highest</b> top over it, which is the same rule the painter and every stamper read.</param>
    /// <param name="buildAreas">The intent's build zones, as <c>(minX, minZ, maxX, maxZ)</c> inclusive — void
    /// inside one is a route the moment a player may place a block in it.</param>
    /// <param name="water">Cells a player swims, or null on a board with none.</param>
    public static WalkGround OfBuilt(
        IEnumerable<(int X, int Z, int YFloor, int YTop)> columns,
        IEnumerable<(int MinX, int MinZ, int MaxX, int MaxZ)> buildAreas,
        IReadOnlySet<(int X, int Z)>? water = null)
    {
        var surface = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top) in columns)
            if (!surface.TryGetValue((x, z), out var known) || top > known) surface[(x, z)] = top;
        var ground = new HashSet<(int X, int Z)>(surface.Keys);

        var bridgeable = new HashSet<(int X, int Z)>();
        foreach (var (minX, minZ, maxX, maxZ) in buildAreas)
            for (var x = minX; x <= maxX; x++)
                for (var z = minZ; z <= maxZ; z++)
                    if (!ground.Contains((x, z))) bridgeable.Add((x, z));

        var all = new HashSet<(int X, int Z)>(ground);
        all.UnionWith(bridgeable);
        Level(bridgeable, surface);
        var bounds = all.Count == 0 ? new CellRect(0, 0, 0, 0) : Cells.BoundingBox(all);
        return new WalkGround(ground, bridgeable, surface, bounds, 1, water);
    }
}
