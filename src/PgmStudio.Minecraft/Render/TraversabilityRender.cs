using PgmStudio.Domain;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Geom.Render;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// Whether a player standing at spawn can walk to every objective — the reading a top-down cannot give,
/// because a top-down shows where ground is and says nothing about whether it joins up. A column is
/// <b>navigable</b> when it has ground and two clear blocks of headroom over it; the navigable columns are
/// split into 4-connected components, and every spawn/wool/monument/core region is coloured by the component
/// its centre falls in. One dominant colour reading through every marker is a connected board; a marker in a
/// second colour is cut off from the rest, however good the terrain looks from above.
///
/// <para>This is a lighter question than <c>Analysis.Playability.Traversability</c> asks of an imported map —
/// no build-region awareness, no bridgeable-gap classification, just headroom and 4-connectivity — because the
/// stage image only needs to be a plausible connectivity read a generator can glance at the moment it finishes
/// building, not a re-implementation of the parquet-driven oracle the corpus is checked against.</para>
/// </summary>
public static class TraversabilityRender
{
    public sealed record Marker(BlockBox Box, string Label, int PackedRgb);

    public sealed record Result(byte[] Pixels, int BlocksWide, int BlocksHigh, int ComponentCount,
        int NavigableCount, int MarkerCount, int IsolatedCount);

    /// <summary>Reads a built region directory from disk.</summary>
    public static int Run(string regionDir, string outPng, MapXml? map, int scale)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks).ToList();
        if (chunks.Count == 0) { Console.Error.WriteLine($"no chunks in {regionDir}"); return 1; }
        return Emit(chunks, outPng, map, scale);
    }

    /// <summary>Renders a world still held in memory, via <see cref="AnvilRegion.FromWorld"/>.</summary>
    public static int Run(VoxelWorld world, string outPng, MapXml? map, int scale)
        => Emit(AnvilRegion.FromWorld(world).ToList(), outPng, map, scale);

    private static int Emit(List<AnvilRegion.Chunk> chunks, string outPng, MapXml? map, int scale)
    {
        var markers = map is null ? [] : Markers(map);
        var result = Render(chunks, markers);
        if (result is null) { Console.Error.WriteLine("no ground columns"); return 1; }

        var scaled = Raster.Upscale(result.Pixels, result.BlocksWide, result.BlocksHigh, scale);
        PngWriter.Write(outPng, result.BlocksWide * scale, result.BlocksHigh * scale, scaled);

        Console.WriteLine($"traversability: {result.NavigableCount} navigable columns, {result.ComponentCount} component(s)" +
            (result.MarkerCount > 0 ? $", {result.MarkerCount} objective marker(s), {result.IsolatedCount} isolated" : ""));
        Console.WriteLine($"  wrote {outPng} ({result.BlocksWide * scale}x{result.BlocksHigh * scale} px, {scale} px/block)");
        return 0;
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

    /// <summary>The pure render: chunks + objective markers in, findings + an RGB pixel buffer out.</summary>
    public static Result? Render(IEnumerable<AnvilRegion.Chunk> chunks, IReadOnlyList<Marker> markers)
    {
        var ground = new Dictionary<(int X, int Z), bool>();   // value: navigable (has headroom)
        foreach (var chunk in chunks) Scan(chunk, ground);
        if (ground.Count == 0) return null;

        int minX = ground.Keys.Min(cell => cell.X), maxX = ground.Keys.Max(cell => cell.X);
        int minZ = ground.Keys.Min(cell => cell.Z), maxZ = ground.Keys.Max(cell => cell.Z);
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;

        var navigable = ground.Where(entry => entry.Value).Select(entry => entry.Key).ToList();
        var components = GridComponents.Label(navigable, connectivity: 4);
        var labelOf = new Dictionary<(int X, int Z), int>();
        for (var index = 0; index < components.Count; index++)
            foreach (var cell in components[index]) labelOf[cell] = index;

        var main = components.Count == 0 ? -1
            : Enumerable.Range(0, components.Count).OrderByDescending(index => components[index].Count).First();

        var pixels = new byte[blocksWide * blocksHigh * 3];
        int[] palette = [0x2f6f4e, 0x6f5a2f, 0x2f5a6f, 0x6f2f5a, 0x4a6f2f, 0x5a2f6f];
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                var cell = (minX + col, minZ + row);
                if (!ground.TryGetValue(cell, out var isNavigable)) { Raster.Set(pixels, blocksWide, col, row, 0x0E0E12); continue; }
                if (!isNavigable) { Raster.Set(pixels, blocksWide, col, row, 0x1c1f26); continue; }
                var label = labelOf.TryGetValue(cell, out var component) ? component : -1;
                var rgb = label == main ? 0x3fae72 : label < 0 ? 0x1c1f26 : palette[label % palette.Length];
                Raster.Set(pixels, blocksWide, col, row, rgb);
            }

        var isolated = 0;
        foreach (var marker in markers)
        {
            var centre = (X: (marker.Box.MinX + marker.Box.MaxX) / 2, Z: (marker.Box.MinZ + marker.Box.MaxZ) / 2);
            var component = ComponentNear(labelOf, centre, blocksWide, blocksHigh, minX, minZ);
            var connected = component == main;
            if (!connected) isolated++;
            DrawMarker(pixels, blocksWide, blocksHigh, minX, minZ, marker.Box, connected ? 0xf5f5f0 : 0xef4444);
        }

        return new Result(pixels, blocksWide, blocksHigh, components.Count, navigable.Count, markers.Count, isolated);
    }

    /// <summary>The component nearest a point, searching a small ring outward — an objective box's centre can
    /// itself be a wall/room-floor cell (not navigable ground), so the nearest navigable neighbour is what
    /// answers which component it opens onto.</summary>
    private static int ComponentNear(Dictionary<(int X, int Z), int> labelOf, (int X, int Z) at,
        int blocksWide, int blocksHigh, int minX, int minZ, int snap = 6)
    {
        for (var radius = 0; radius <= snap; radius++)
            for (var dz = -radius; dz <= radius; dz++)
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var cell = (at.X + dx, at.Z + dz);
                    if (labelOf.TryGetValue(cell, out var label)) return label;
                }
        return -1;
    }

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

    /// <summary>A column is navigable when it has ground and two blocks of clear headroom above it — nothing
    /// standing on it and nothing overhanging it a player's head would clip. Ground itself is the same "not
    /// decoration, not liquid, not air" read <see cref="HeightProfileRender"/> uses, so the two stage images
    /// agree about where the ground is even though they answer different questions about it.</summary>
    private static void Scan(AnvilRegion.Chunk chunk, Dictionary<(int X, int Z), bool> ground)
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
                for (var y = 255; y >= 0; y--)
                {
                    var id = ids[(y << 8) | col];
                    if (id == 0 || BlockRoles.IsLiquid(id) || BlockRoles.StandsOnGround(id)) continue;
                    var cell = (chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz);
                    var clear = y + 2 < 256
                        && ids[((y + 1) << 8) | col] == 0
                        && ids[((y + 2) << 8) | col] == 0;
                    ground[cell] = clear;
                    break;
                }
            }
    }
}
