using PgmStudio.Domain;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Geom.Render;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// Whether a player standing at spawn can walk to every objective — the reading a top-down cannot give,
/// because a top-down shows where ground is and says nothing about whether it joins up. A column is
/// <b>navigable</b> when it has ground and two clear blocks of headroom over it, <b>or</b> when it has
/// neither but the map opens it to bridging — a void gap a build carries no ground
/// over is still a route the moment PGM lets players bridge it, and a capture board routinely joins its
/// islands exactly that way (<c>docs/pgm/water-lanes.md</c> §1, <c>ruediger</c>'s build regions). The
/// navigable columns are split into 4-connected components, and every spawn/wool/monument/core region is
/// coloured by the component its centre falls in. One dominant colour reading through every marker is a
/// connected board; a marker in a second colour is cut off from the rest, however good the terrain looks
/// from above.
///
/// <para>The render does not read the map's filters. Which void columns a board opens to bridging is
/// <c>Analysis.Playability.Editability</c>'s answer — PGM's own first-rule-wins resolution over the place scope
/// — and the caller that reaches both projects computes it and hands the set in, so the picture and the walk,
/// coverage and dead-ground reads cannot disagree about it. A <b>water lane</b> is not in that set: it opens
/// only after the match clock passes its timer, and no apply rule opens its footprint at kickoff, so it reads
/// here as any other void, cut off until it opens.</para>
///
/// <para>This still falls short of the full question <c>Analysis.Playability.Traversability</c> asks of an
/// imported map — no <c>never</c>/<c>restricted</c> apply-rule classes, just ground, headroom and the columns
/// it is handed as bridgeable.</para>
/// </summary>
public static class TraversabilityRender
{
    public sealed record Marker(BlockBox Box, string Label, int PackedRgb);

    /// <summary>One patch of standing ground no player can get to, and why. <see cref="Reason"/> is
    /// <c>no-build-zone</c> where nothing the map opens to bridging reaches it, and <c>above-ceiling</c> where
    /// its ground stands over the map's <c>maxbuildheight</c> and cannot be built up to. <see cref="Floor"/>
    /// is its lowest standing course, and the box is what a reader stands in to check it.
    ///
    /// <para><b>Neither is a fault.</b> A side observer island is meant to be unreachable, and scenery is
    /// scenery — this states where the ground is, not that it is wrong.</para></summary>
    public sealed record Stranded(int Cells, int MinX, int MinZ, int MaxX, int MaxZ, int Floor, string Reason);

    public sealed record Result(byte[] Pixels, int BlocksWide, int BlocksHigh, int ComponentCount,
        int NavigableCount, int BridgeableCount, int MarkerCount, int IsolatedCount,
        IReadOnlyList<Stranded> OutOfReach);

    /// <summary>The finished navigability picture, written to <paramref name="outPng"/>. Nonzero where the
    /// chunks hold no ground column.</summary>
    public static int Run(IReadOnlyList<AnvilRegion.Chunk> chunks, string outPng, MapXml? map,
                          IReadOnlySet<(int X, int Z)>? bridgeable, int scale)
        => Emit(chunks, outPng, map, bridgeable, scale) is null ? 1 : 0;

    /// <summary>The finished navigability picture as bytes, for a caller that wants the image rather than a
    /// file. Null where the chunks hold no ground column.</summary>
    public static byte[]? Png(IReadOnlyList<AnvilRegion.Chunk> chunks, MapXml? map,
                              IReadOnlySet<(int X, int Z)>? bridgeable, int scale)
        => Emit(chunks, null, map, bridgeable, scale);

    /// <summary>The navigability reading without the picture — the same components, markers and bridged
    /// columns the render is drawn from, for a caller that wants the numbers. Null where the chunks hold no
    /// ground column.</summary>
    public static Result? Read(IReadOnlyList<AnvilRegion.Chunk> chunks, MapXml? map,
                               IReadOnlySet<(int X, int Z)>? bridgeable)
        => Render(chunks, map is null ? [] : Markers(map), bridgeable, map?.MaxBuildHeight);

    private static byte[]? Emit(IReadOnlyList<AnvilRegion.Chunk> chunks, string? outPng, MapXml? map,
                                IReadOnlySet<(int X, int Z)>? bridgeable, int scale)
    {
        var result = Render(chunks, map is null ? [] : Markers(map), bridgeable, map?.MaxBuildHeight);
        if (result is null) { if (outPng is not null) Console.Error.WriteLine("no ground columns"); return null; }

        var scaled = Raster.Upscale(result.Pixels, result.BlocksWide, result.BlocksHigh, scale);
        List<Legend.Entry> entries =
        [
            new("MAIN COMPONENT (SPAWN'S)", 0x3fae72),
            new("OTHER COMPONENT (ISOLATED)", 0x6f5a2f),
            new("NOT NAVIGABLE", 0x1c1f26),
            new("BRIDGED BY A BUILD REGION", BridgeTint),
            new("MARKER: CONNECTED", 0xf5f5f0),
            new("MARKER: ISOLATED", 0xef4444),
            new("VOID", 0x0E0E12),
        ];
        var withLegend = Legend.AppendBelow(scaled, result.BlocksWide * scale, result.BlocksHigh * scale, entries,
            out var legendHeight,
            scaleLabel: $"SCALE: 1 BLOCK = {scale} PX - {result.BlocksWide} X {result.BlocksHigh} BLOCKS");
        var png = PngWriter.Encode(result.BlocksWide * scale, legendHeight, withLegend);
        if (outPng is null) return png;
        File.WriteAllBytes(outPng, png);

        Console.WriteLine($"traversability: {result.NavigableCount} navigable columns" +
            (result.BridgeableCount > 0 ? $" ({result.BridgeableCount} bridged over void)" : "") +
            $", {result.ComponentCount} component(s)" +
            (result.MarkerCount > 0 ? $", {result.MarkerCount} objective marker(s), {result.IsolatedCount} isolated" : ""));
        Console.WriteLine($"  wrote {outPng} ({result.BlocksWide * scale}x{legendHeight} px, {scale} px/block)");
        return png;
    }

    /// <summary>Every spawn region, wool room / location, destroyable region and core region as a box to
    /// colour by component — the same declared-goal boxes <see cref="TopDownRender.Overlays"/> outlines.</summary>
    public static List<Marker> Markers(MapXml map)
    {
        var markers = new List<Marker>();
        foreach (var spawn in map.Spawns.Where(spawn => spawn.Region is not null))
            foreach (var box in RegionBoxes.Of(map.Regions, spawn.Region!))
                markers.Add(new Marker(box, $"spawn {spawn.Team}", 0x34d399));
        foreach (var wool in map.Wools)
        {
            if (wool.WoolRoomRegion is { Length: > 0 } roomRegion)
                foreach (var box in RegionBoxes.Of(map.Regions, roomRegion))
                    markers.Add(new Marker(box, $"wool {wool.Color}", 0xfbbf24));
            else
                markers.Add(new Marker(PointBox(wool.Location), $"wool {wool.Color}", 0xfbbf24));
        }
        foreach (var destroyable in map.Destroyables.Where(d => d.IsObjective))
            foreach (var box in RegionBoxes.Of(map.Regions, destroyable.RegionId))
                markers.Add(new Marker(box, $"destroyable {destroyable.Name}", 0xfb923c));
        foreach (var core in map.Cores)
            foreach (var box in RegionBoxes.Of(map.Regions, core.RegionId))
                markers.Add(new Marker(box, "core", 0xfb923c));
        return markers;
    }

    /// <summary>A single point widened to a one-cell box — a wool with no declared room region still marks
    /// the block its goal is read at.</summary>
    private static BlockBox PointBox(Vec3 point)
    {
        int x = (int)Math.Floor(point.X), y = (int)Math.Floor(point.Y), z = (int)Math.Floor(point.Z);
        return new BlockBox(x, y, z, x, y, z);
    }

    /// <summary>
    /// The patches of standing ground no player can get to, off the same components the picture is drawn
    /// from — largest first.
    ///
    /// <para>A component that is not the main one is not walked to. Two things still let a player reach it, so
    /// neither is reported: a <b>marker</b> on it, since a spawn or an objective is where players are put and
    /// what they fight over, and a <b>build zone</b> reaching it, since what the map opens to bridging is a
    /// route from the first tick. What is left is ground standing outside both — and ground above the map's
    /// <c>maxbuildheight</c>, which cannot be built up to whatever else is true of it.</para>
    ///
    /// <para><b>It is a reading and not a rule.</b> An island drawn to be looked at, a side platform players
    /// spawn on and never leave, a shelf above the ceiling: each is ground an author meant, and none of them
    /// is named here as wrong. A component carrying a marker is left out for a second reason — a spawn or a
    /// goal cut off from the board is the connectivity rule's to report, and saying it twice in two
    /// vocabularies is how a reader learns to believe neither.</para>
    /// </summary>
    private static List<Stranded> Unreached(
        IReadOnlyList<List<(int X, int Z)>> components, int main,
        Dictionary<(int X, int Z), int> labelOf, IReadOnlyList<Marker> markers,
        IReadOnlySet<(int X, int Z)> bridged, IReadOnlyDictionary<(int X, int Z), int> standing,
        int? maxBuildHeight)
    {
        var navigableCells = new HashSet<(int X, int Z)>(labelOf.Keys);
        var marked = new HashSet<int>();
        foreach (var marker in markers)
        {
            var centre = (X: (marker.Box.MinX + marker.Box.MaxX) / 2, Z: (marker.Box.MinZ + marker.Box.MaxZ) / 2);
            var component = ComponentNear(navigableCells, labelOf, centre);
            if (component >= 0) marked.Add(component);
        }

        var found = new List<Stranded>();
        for (var index = 0; index < components.Count; index++)
        {
            if (index == main || marked.Contains(index)) continue;
            var cells = components[index];
            var standingCells = cells.Where(standing.ContainsKey).ToList();
            if (standingCells.Count == 0) continue;      // a bridge over void stands on nothing to report

            var floor = standingCells.Min(cell => standing[cell]);
            var reason = maxBuildHeight is { } ceiling && floor > ceiling ? "above-ceiling"
                : cells.Any(bridged.Contains) ? null
                : "no-build-zone";
            if (reason is null) continue;

            found.Add(new Stranded(standingCells.Count,
                standingCells.Min(cell => cell.X), standingCells.Min(cell => cell.Z),
                standingCells.Max(cell => cell.X), standingCells.Max(cell => cell.Z), floor, reason));
        }
        return [.. found.OrderByDescending(patch => patch.Cells)];
    }

    /// <summary>A packed colour tinting a bridgeable-but-ungrounded cell — distinct from every ground shade,
    /// palette entry and marker colour this render already uses, so a column carried only by a build region
    /// never reads as ordinary ground: the connectivity it grants is real from the first tick, but the
    /// picture still says which columns are standing on nothing.</summary>
    private const int BridgeTint = 0x38bdf8;

    /// <summary>The pure render: chunks + objective markers in, findings + an RGB pixel buffer out.
    /// <paramref name="bridgeable"/> is the set of columns the map opens to bridging from the first tick, as
    /// the caller computed it; only the void ones among them are drawn as bridged, and null or empty means the
    /// render reads ground and headroom only.</summary>
    public static Result? Render(IEnumerable<AnvilRegion.Chunk> chunks, IReadOnlyList<Marker> markers,
        IReadOnlySet<(int X, int Z)>? bridgeable = null, int? maxBuildHeight = null)
    {
        var ground = new Dictionary<(int X, int Z), int?>();   // value: the standing Y, null = no headroom
        foreach (var chunk in chunks) Scan(chunk, ground);
        if (ground.Count == 0) return null;

        // A void column has no entry in `ground` at all, so a bridge only ever adds cells outside it —
        // real ground, walkable or not, always keeps its own reading regardless of what the region covers.
        var bridged = (bridgeable ?? new HashSet<(int X, int Z)>()).Where(cell => !ground.ContainsKey(cell)).ToHashSet();

        var xs = ground.Keys.Select(cell => cell.X).Concat(bridged.Select(cell => cell.X)).ToList();
        var zs = ground.Keys.Select(cell => cell.Z).Concat(bridged.Select(cell => cell.Z)).ToList();
        int minX = xs.Min(), maxX = xs.Max();
        int minZ = zs.Min(), maxZ = zs.Max();
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;

        var navigable = ground.Where(entry => entry.Value is not null).Select(entry => entry.Key).Concat(bridged).ToList();

        // Two navigable cells join only where the ground between them is ground rather than a wall: past
        // Walk.WallRise a player goes round the face instead of up it, so a house, a cliff and a wall each
        // stop the flood at their own foot. Without the bound every roof with headroom over it is a place to
        // stand and a route runs across the building — a board reads whole that a player cannot cross. A
        // bridged cell carries no height of its own, so it joins whatever it touches, which is what a build
        // zone means.
        var standing = ground.Where(entry => entry.Value is not null)
                             .ToDictionary(entry => entry.Key, entry => entry.Value!.Value);
        bool Joins((int X, int Z) here, (int X, int Z) there) =>
            !standing.TryGetValue(here, out var a) || !standing.TryGetValue(there, out var b)
            || Math.Abs(a - b) <= Walk.WallRise;

        var components = GridComponents.Label(navigable, connectivity: 4, canJoin: Joins);
        var labelOf = new Dictionary<(int X, int Z), int>();
        for (var index = 0; index < components.Count; index++)
            foreach (var cell in components[index]) labelOf[cell] = index;

        var main = components.Count == 0 ? -1
            : Enumerable.Range(0, components.Count).OrderByDescending(index => components[index].Count).First();

        var pixels = new byte[blocksWide * blocksHigh * 3];
        int[] palette = [0x2f6f4e, 0x6f5a2f, 0x2f5a6f, 0x6f2f5a, 0x4a6f2f, 0x5a2f6f];
        int ComponentRgb((int X, int Z) cell)
        {
            var label = labelOf.TryGetValue(cell, out var component) ? component : -1;
            return label == main ? 0x3fae72 : label < 0 ? 0x1c1f26 : palette[label % palette.Length];
        }
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                var cell = (minX + col, minZ + row);
                if (ground.TryGetValue(cell, out var standingY))
                {
                    Raster.Set(pixels, blocksWide, col, row, standingY is not null ? ComponentRgb(cell) : 0x1c1f26);
                    continue;
                }
                if (bridged.Contains(cell))
                {
                    Raster.Set(pixels, blocksWide, col, row, Raster.Lerp(ComponentRgb(cell), BridgeTint, 0.55));
                    continue;
                }
                Raster.Set(pixels, blocksWide, col, row, 0x0E0E12);
            }

        var navigableCells = new HashSet<(int X, int Z)>(labelOf.Keys);
        var isolated = 0;
        foreach (var marker in markers)
        {
            var centre = (X: (marker.Box.MinX + marker.Box.MaxX) / 2, Z: (marker.Box.MinZ + marker.Box.MaxZ) / 2);
            var component = ComponentNear(navigableCells, labelOf, centre);
            var connected = component == main;
            if (!connected) isolated++;
            DrawMarker(pixels, blocksWide, blocksHigh, minX, minZ, marker.Box, connected ? 0xf5f5f0 : 0xef4444);
        }

        return new Result(pixels, blocksWide, blocksHigh, components.Count, navigable.Count, bridged.Count,
                          markers.Count, isolated,
                          Unreached(components, main, labelOf, markers, bridged, standing, maxBuildHeight));
    }

    /// <summary>The component nearest a point, searching a small ring outward — an objective box's centre can
    /// itself be a wall/room-floor cell (not navigable ground), so the nearest navigable neighbour is what
    /// answers which component it opens onto.</summary>
    private static int ComponentNear(IReadOnlySet<(int X, int Z)> navigableCells, Dictionary<(int X, int Z), int> labelOf,
        (int X, int Z) at, int snap = 6) =>
        Cells.SnapToWalkable(at, navigableCells, snap) is { } cell ? labelOf[cell] : -1;

    private static void DrawMarker(byte[] pixels, int blocksWide, int blocksHigh, int minX, int minZ, BlockBox box, int rgb)
    {
        int left = Math.Max(0, box.MinX - minX - 1), right = Math.Min(blocksWide - 1, box.MaxX - minX + 1);
        int top = Math.Max(0, box.MinZ - minZ - 1), bottom = Math.Min(blocksHigh - 1, box.MaxZ - minZ + 1);
        for (var row = top; row <= bottom; row++)
            for (var col = left; col <= right; col++)
            {
                var onBorder = row == top || row == bottom || col == left || col == right;
                if (onBorder) Raster.Over(pixels, blocksWide, col, row, rgb, 0.9);
            }
    }

    /// <summary>Whether the <see cref="Walk.Headroom"/> blocks from <paramref name="from"/> up leave a player
    /// room to stand — air, or something their body passes through. A flower, a torch and a carpet are
    /// stepped past when the ground is found and must be stepped past here too, or a column reads as ground a
    /// player cannot stand on for the sake of the daisy on it; a fence is decoration as well and does stop
    /// them, which is why <see cref="BlockRoles.StoodThrough"/> and not
    /// <see cref="BlockRoles.StandsOnGround"/> is the predicate.</summary>
    private static bool Clear(ushort[] ids, int col, int from)
    {
        for (var y = from; y < from + Walk.Headroom; y++)
        {
            var id = ids[(y << 8) | col];
            if (id != 0 && !BlockRoles.StoodThrough(id)) return false;
        }
        return true;
    }

    /// <summary>Each column's standing surface: the Y a player walking in at terrain level meets, or null
    /// where the column has ground and nowhere to stand. A void column is absent.
    ///
    /// <para>A column is navigable when it has ground and two blocks of clear headroom above it — nothing
    /// standing on it and nothing overhanging it a player's head would clip. Ground itself is the same "not
    /// decoration, not liquid, not air" read <see cref="HeightProfileRender"/> uses, so the two stage images
    /// agree about where the ground is even though they answer different questions about it.</para></summary>
    private static void Scan(AnvilRegion.Chunk chunk, Dictionary<(int X, int Z), int?> ground)
    {
        var ids = new ushort[256 * 256];
        foreach (var section in AnvilRegion.Sections(chunk))
        {
            var yStart = section.SectionY * 16;
            if (yStart is < 0 or >= 256) continue;
            Array.Copy(section.Ids, 0, ids, yStart * 256, 4096);
        }

        for (var lz = 0; lz < 16; lz++)
            for (var lx = 0; lx < 16; lx++)
            {
                var col = (lz << 4) | lx;
                var cell = (chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz);
                var solid = false;

                // Bottom up, so the surface is the one a player walking in at terrain level meets: a wooded
                // cell reads on its terrain rather than on its canopy, and a walled one on the wall rather
                // than on the floor the wall stands beside. A column with ground but nowhere to stand is
                // recorded as not navigable rather than dropped, because it is not void.
                for (var y = 0; y + Walk.Headroom < Walk.WorldHeight; y++)
                {
                    var id = ids[(y << 8) | col];
                    if (id == 0 || BlockRoles.IsLiquid(id) || BlockRoles.StandsOnGround(id)) continue;
                    solid = true;
                    if (!Clear(ids, col, y + 1)) continue;
                    ground[cell] = y + 1;
                    solid = false;
                    break;
                }
                if (solid) ground[cell] = null;
            }
    }
}
