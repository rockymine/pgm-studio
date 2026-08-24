using PgmStudio.Analysis.Footprint;
using PgmStudio.Analysis.Scan;
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
///
/// <para><b>Ground is per team where the map bars a team from any of it.</b> <see cref="For"/> subtracts the
/// cells an <c>enter</c> rule denies a team (<see cref="EntryDenials"/>) from what it may stand on and what it
/// may bridge onto, so a walk measured for that team cannot price a route through ground the player is thrown
/// out of. The shared ground is the right question for a board's shape and the wrong one for a kit budget.
/// </para>
/// </summary>
public static class WorldWalk
{
    /// <summary>The ground a walk runs over on a scanned board.</summary>
    /// <param name="data">The map document — read for the build zones and the apply rules that say where a
    /// player may place a block.</param>
    /// <param name="segments">The scanned columns. Absent, nothing is walkable, and a caller should say that
    /// rather than report a board with no ground.</param>
    /// <param name="margin">How far past the declared regions the grid reaches.</param>
    /// <param name="bbox">The grid to size to, as <c>(minX, minZ, maxX, maxZ)</c>. Absent, it is the declared
    /// regions unioned with the terrain's own extent, so an objective standing on ground past every region
    /// stays inside the box the clearance read is taken in.</param>
    public static WalkGround Ground(Dict data, SegmentIndex? segments, int margin = 16,
        (int MinX, int MinZ, int MaxX, int MaxZ)? bbox = null)
    {
        var grid = bbox ?? TerrainBox(data, segments, margin);
        var build = Buildability.Compute(data, segments?.Y0Columns(), grid, margin);

        var places = new HashSet<WalkPlace>();
        var clear = new Dictionary<WalkPlace, int>();
        var floor = new Dictionary<(int X, int Z), int>();
        if (segments is not null)
            foreach (var (x, z, top, room) in segments.StandingTops())
            {
                var place = new WalkPlace(x, z, top);
                places.Add(place);
                if (room != int.MaxValue) clear[place] = room;
                if (!floor.TryGetValue(place.Cell, out var lowest) || top < lowest) floor[place.Cell] = top;
            }

        // Two filters, and each rules out something the other cannot see. The cleaned base footprint drops a
        // build floating over void, whose storeys must not pose as free standing-ground over nothing. The
        // standing surfaces drop a column with nowhere in it to stand.
        var standable = segments is null
            ? new HashSet<(int X, int Z)>()
            : new HashSet<(int X, int Z)>(
                IslandDetector.CleanedBaseFootprint(segments.BaseColumns()).Where(floor.ContainsKey));
        var ground = new HashSet<WalkPlace>(places.Where(place => standable.Contains(place.Cell)));

        // A cell is bridgeable only where the map GRANTS building: some apply rule's region covers it and
        // does not deny it. Reading an ungoverned cell as buildable makes every cell outside every rule
        // crossable, and a board then walks over void nobody can bridge.
        var open = new HashSet<(int X, int Z)>();
        for (var i = 0; i < build.Verdict.Length; i++)
        {
            if (!build.Governed[i]) continue;
            if (build.Verdict[i] is not (0 or 3)) continue;      // buildable, or restricted-but-placeable
            var cell = (build.MinX + i % build.Width, build.MinZ + i / build.Width);
            if (!standable.Contains(cell)) open.Add(cell);
        }

        Level(open, floor);
        var bridgeable = new HashSet<WalkPlace>(
            open.Where(floor.ContainsKey).Select(cell => new WalkPlace(cell.X, cell.Z, floor[cell])));

        return new WalkGround(ground, bridgeable, new CellRect(build.MinX, build.MinZ, build.Width, build.Height),
            1, null, clear);
    }

    /// <summary>The box a walk is sized to: every declared region, unioned with the terrain's own extent.
    /// A board's objectives can stand on ground past the last region, and a grid drawn round the regions
    /// alone would put them outside it.</summary>
    private static (int MinX, int MinZ, int MaxX, int MaxZ) TerrainBox(
        Dict data, SegmentIndex? segments, int margin)
    {
        var (minX, minZ, maxX, maxZ) = Buildability.RegionBbox(data, margin);
        if (segments is null) return (minX, minZ, maxX, maxZ);

        foreach (var columns in (ReadOnlySpan<HashSet<(int, int)>>)
                 [segments.StandingColumns(), segments.Y0Columns()])
        {
            if (columns.Count == 0) continue;
            minX = Math.Min(minX, columns.Min(cell => cell.Item1) - margin);
            minZ = Math.Min(minZ, columns.Min(cell => cell.Item2) - margin);
            maxX = Math.Max(maxX, columns.Max(cell => cell.Item1) + margin);
            maxZ = Math.Max(maxZ, columns.Max(cell => cell.Item2) + margin);
        }
        return (minX, minZ, maxX, maxZ);
    }

    /// <summary>One team's own ground: the same walk with every cell an <c>enter</c> rule bars it from taken
    /// out. A barred cell is neither standable nor bridgeable — bridging onto it still puts the player there —
    /// and a team nothing bars gets the ground it was given back unchanged.
    ///
    /// <para>Narrowing a ground already built rather than building a second one is what lets a caller with
    /// several teams pay for the columns once. A marker is still snapped on the <b>shared</b> ground and
    /// looked up here: snapping on this one slides a barred objective sideways until it finds a cell the team
    /// may stand on, and reports the walk to that cell as the walk to the objective.</para></summary>
    public static WalkGround For(WalkGround shared, Dict? data, string? team)
    {
        var over = shared.Bounds;
        if (data is null || team is null || over.Width <= 0 || over.Height <= 0) return shared;
        if (EntryDenials.Cells(data, team, over) is not { Count: > 0 } denied) return shared;

        return shared.Narrowed(new HashSet<(int X, int Z)>(shared.Footprint.Where(cell => !denied.Contains(cell))));
    }

    /// <summary>Give every bridgeable cell the height of the ground nearest it, spreading outward from the
    /// shores. A player bridging builds out level from where they left, so a crossing costs nothing until it
    /// reaches the far side — and then costs the rise onto it, which is the climb out of a gap and the most
    /// common climb on a capture board. Without this a bridge has no stated height at either end, and a step
    /// between two heights one of which is unknown charges nothing at all.</summary>
    private static void Level(IReadOnlySet<(int X, int Z)> bridgeable, Dictionary<(int X, int Z), int> floor)
    {
        var queue = new Queue<(int X, int Z)>();
        foreach (var cell in bridgeable)
            foreach (var side in Cells.N4(cell))
                if (floor.ContainsKey(side)) { queue.Enqueue(side); break; }

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            var height = floor[cell];
            foreach (var side in Cells.N4(cell))
                if (bridgeable.Contains(side) && floor.TryAdd(side, height)) queue.Enqueue(side);
        }
    }

    /// <summary>The ground a walk runs over on a world the studio built for itself, from the rasterised
    /// columns rather than from a scan.</summary>
    /// <param name="columns">Every solid span, as the rasterizer emits them. A cell's standing surface is
    /// read from them under the same rule a scan is: the lowest span whose top carries
    /// <see cref="Walk.Headroom"/> clear blocks over it.</param>
    /// <param name="buildAreas">The intent's build zones, as <c>(minX, minZ, maxX, maxZ)</c> inclusive — void
    /// inside one is a route the moment a player may place a block in it.</param>
    /// <param name="water">Cells a player swims, or null on a board with none.</param>
    public static WalkGround OfBuilt(
        IEnumerable<(int X, int Z, int YFloor, int YTop)> columns,
        IEnumerable<(int MinX, int MinZ, int MaxX, int MaxZ)> buildAreas,
        IReadOnlySet<(int X, int Z)>? water = null)
    {
        var solid = WalkGround.OfSpans(columns);
        var ground = new HashSet<WalkPlace>(solid.Ground);
        var clear = new Dictionary<WalkPlace, int>();
        var floor = new Dictionary<(int X, int Z), int>();
        foreach (var place in ground)
        {
            if (solid.ClearAbove(place) is var room and not int.MaxValue) clear[place] = room;
            if (!floor.TryGetValue(place.Cell, out var lowest) || place.Y < lowest) floor[place.Cell] = place.Y;
        }

        var open = new HashSet<(int X, int Z)>();
        foreach (var (minX, minZ, maxX, maxZ) in buildAreas)
            for (var x = minX; x <= maxX; x++)
                for (var z = minZ; z <= maxZ; z++)
                    if (!floor.ContainsKey((x, z))) open.Add((x, z));

        Level(open, floor);
        var bridgeable = new HashSet<WalkPlace>(
            open.Where(floor.ContainsKey).Select(cell => new WalkPlace(cell.X, cell.Z, floor[cell])));

        var all = new HashSet<(int X, int Z)>(floor.Keys);
        all.UnionWith(open);
        var bounds = all.Count == 0 ? new CellRect(0, 0, 0, 0) : Cells.BoundingBox(all);
        return new WalkGround(ground, bridgeable, bounds, 1, water, clear);
    }
}
