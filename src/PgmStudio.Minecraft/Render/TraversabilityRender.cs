using PgmStudio.Domain;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Geom.Render;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// Whether a player standing at spawn can walk to every objective — the reading a top-down cannot give,
/// because a top-down shows where ground is and says nothing about whether it joins up. A column is
/// <b>navigable</b> when it has ground and two clear blocks of headroom over it, <b>or</b> when it has
/// neither but sits inside the map's own declared buildable region — a void gap a build carries no ground
/// over is still a route the moment PGM lets players bridge it, and a capture board routinely joins its
/// islands exactly that way (<c>docs/pgm/water-lanes.md</c> §1, <c>ruediger</c>'s build regions). The
/// navigable columns are split into 4-connected components, and every spawn/wool/monument/core region is
/// coloured by the component its centre falls in. One dominant colour reading through every marker is a
/// connected board; a marker in a second colour is cut off from the rest, however good the terrain looks
/// from above.
///
/// <para>The buildable region is read the same way this render already reads its markers: straight out of
/// the parsed <c>map.xml</c>, off the <c>&lt;apply&gt;</c> rule that gates block edits by a "not void"
/// filter over some region (<see cref="BridgeableColumns"/>) — the same wiring <c>BuildGenerator</c> writes
/// and any hand-authored map uses to the same end, since PGM offers no other way to open a void gap to
/// building. A <b>water lane</b> is deliberately not this: it opens only after the match clock passes its
/// timer, so treating it as day-one navigable would read a board as connected before it is, and the generator
/// never wires one into the buildable region for exactly that reason — a lane's footprint carries no such
/// apply rule of its own, so it is read here the same as any other void, cut off until it opens.</para>
///
/// <para>This still falls short of the full question <c>Analysis.Playability.Traversability</c> asks of an
/// imported map — no NTS geometry (only the box shapes <see cref="PgmStudio.Domain.RegionBoxes"/> reduces to:
/// rectangles, cuboids, unions of them), no <c>never</c>/<c>restricted</c> apply-rule classes, just ground,
/// headroom and the one buildable-region rule. <c>Minecraft</c> and <c>Analysis</c> are dependency siblings —
/// neither project references the other — and that oracle is built on NTS region geometry plus a JSON
/// dictionary shape this render never holds (it reads a live <see cref="VoxelWorld"/>/region directory and a
/// parsed <c>MapXml</c>), so calling it directly would mean handing <c>Minecraft</c> a reference to
/// <c>Analysis</c> and reconstructing that dictionary just to ask it. Reducing a region to boxes is already
/// the shared, Domain-level piece (<see cref="PgmStudio.Domain.RegionBoxes"/>); duplicating the much smaller
/// "does this apply rule gate on void" read is the cheaper and more honest choice than either of those, and
/// is the whole of what stands in for the oracle here.</para>
/// </summary>
public static class TraversabilityRender
{
    public sealed record Marker(BlockBox Box, string Label, int PackedRgb);

    public sealed record Result(byte[] Pixels, int BlocksWide, int BlocksHigh, int ComponentCount,
        int NavigableCount, int BridgeableCount, int MarkerCount, int IsolatedCount);

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
        var bridgeable = map is null ? null : BridgeableColumns(map);
        var result = Render(chunks, markers, bridgeable);
        if (result is null) { Console.Error.WriteLine("no ground columns"); return 1; }

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
        PngWriter.Write(outPng, result.BlocksWide * scale, legendHeight, withLegend);

        Console.WriteLine($"traversability: {result.NavigableCount} navigable columns" +
            (result.BridgeableCount > 0 ? $" ({result.BridgeableCount} bridged over void)" : "") +
            $", {result.ComponentCount} component(s)" +
            (result.MarkerCount > 0 ? $", {result.MarkerCount} objective marker(s), {result.IsolatedCount} isolated" : ""));
        Console.WriteLine($"  wrote {outPng} ({result.BlocksWide * scale}x{legendHeight} px, {scale} px/block)");
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

    /// <summary>The void columns this map's own apply rules make bridgeable at kickoff — the buildable
    /// region read straight off the "not void" wiring PGM enforces, the same one <see cref="BuildGenerator"/>
    /// writes and any hand-authored map reaches for to the same end, since PGM offers no other way to open a
    /// void gap to building. A water lane carries no apply rule of its own over its footprint (it opens by a
    /// timed fill firing later in the match, not by this wiring — <c>docs/pgm/water-lanes.md</c> §1,
    /// §4), so it is not found here and reads as void until the render that watches it actually opens.
    /// <para>Reduces only the box shapes <see cref="RegionBoxes.FootprintXZ"/> can state — rectangles,
    /// cuboids and unions of them, which is every shape the generator's own build regions are drawn from. A
    /// region built from a circle, a polygon or anything else that does not reduce to boxes contributes
    /// nothing, the same safe-empty answer <see cref="RegionBoxes"/> gives everywhere else it is asked a
    /// shape it cannot state exactly.</para></summary>
    public static HashSet<(int X, int Z)> BridgeableColumns(MapXml map)
    {
        var columns = new HashSet<(int X, int Z)>();
        foreach (var rule in map.ApplyRules)
        {
            if (rule.RegionId.Length == 0 || !GatesOnVoid(rule.BlockFilter, map.Filters, [])) continue;
            foreach (var box in RegionBoxes.FootprintXZ(map.Regions, rule.RegionId))
                for (var x = box.MinX; x <= box.MaxX; x++)
                    for (var z = box.MinZ; z <= box.MaxZ; z++)
                        columns.Add((x, z));
        }
        return columns;
    }

    /// <summary>Whether a block-edit filter value ultimately reads "void" — chasing the same
    /// <c>not</c>/<c>deny</c>/<c>allow</c> wrapping <see cref="BuildGenerator"/> writes around its own
    /// <c>void</c> filter (<c>no-void = not(void)</c>), plus the inline <c>deny(void)</c> shorthand a
    /// hand-authored map can write straight into the attribute without registering a filter at all.</summary>
    private static bool GatesOnVoid(string filterValue, IReadOnlyDictionary<string, Filter> filters, HashSet<string> seen)
    {
        if (filterValue.Length == 0 || !seen.Add(filterValue)) return false;
        if (!filters.TryGetValue(filterValue, out var filter))
            return filterValue.Contains("void", StringComparison.OrdinalIgnoreCase);
        if (filter.Type == "void") return true;
        if (filter.Type is "not" or "deny" or "allow") return GatesOnVoid(filter.Child ?? "", filters, seen);
        return false;
    }

    /// <summary>A packed colour tinting a bridgeable-but-ungrounded cell — distinct from every ground shade,
    /// palette entry and marker colour this render already uses, so a column carried only by a build region
    /// never reads as ordinary ground: the connectivity it grants is real from the first tick, but the
    /// picture still says which columns are standing on nothing.</summary>
    private const int BridgeTint = 0x38bdf8;

    /// <summary>The pure render: chunks + objective markers in, findings + an RGB pixel buffer out.
    /// <paramref name="bridgeable"/> is the set of void columns (no ground of their own) that the map's own
    /// buildable-region wiring opens to bridging from the first tick — see <see cref="BridgeableColumns"/>;
    /// null/empty means the render falls back to ground-and-headroom only.</summary>
    public static Result? Render(IEnumerable<AnvilRegion.Chunk> chunks, IReadOnlyList<Marker> markers,
        IReadOnlySet<(int X, int Z)>? bridgeable = null)
    {
        var ground = new Dictionary<(int X, int Z), bool>();   // value: navigable (has headroom)
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

        var navigable = ground.Where(entry => entry.Value).Select(entry => entry.Key).Concat(bridged).ToList();
        var components = GridComponents.Label(navigable, connectivity: 4);
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
                if (ground.TryGetValue(cell, out var isNavigable))
                {
                    Raster.Set(pixels, blocksWide, col, row, isNavigable ? ComponentRgb(cell) : 0x1c1f26);
                    continue;
                }
                if (bridged.Contains(cell))
                {
                    Raster.Set(pixels, blocksWide, col, row, Raster.Lerp(ComponentRgb(cell), BridgeTint, 0.55));
                    continue;
                }
                Raster.Set(pixels, blocksWide, col, row, 0x0E0E12);
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

        return new Result(pixels, blocksWide, blocksHigh, components.Count, navigable.Count, bridged.Count, markers.Count, isolated);
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
