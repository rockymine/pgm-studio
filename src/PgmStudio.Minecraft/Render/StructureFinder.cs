using PgmStudio.Geom.Render;

namespace PgmStudio.Minecraft.Render;

/// <summary>
/// The built structures standing on a world, found and drawn over the terrain they sit on.
///
/// <para><b>A building is recognised by its material, not by its height.</b> Elevation alone cannot separate
/// a hut from the boulder beside it — both are flat-topped lumps standing over their surroundings — and a
/// long hall on level ground has no elevation signature at all. What every building does have is a surface a
/// world does not generate: planks, stairs, slabs, brick, glass, wool. Each column is classified by the block
/// on top of it once vegetation is stripped, the built ones are joined into connected components, and each
/// component is one candidate structure.</para>
///
/// <para><b>Height is then measured against the ground the structure hides.</b> The natural surface under a
/// building is not visible, so the comparison is with a ring of natural columns just outside the component's
/// own footprint; a structure's height is its roof over that ring's median. This is what separates a building
/// from a path or a plaza, which are built surfaces with no height at all, and it is reported rather than
/// used as a filter — a paved road is a real find, it is simply not a building.</para>
///
/// <para><b>"Built" used to be the only test the flood applied, and it is a yes/no over every material at
/// once.</b> Any two neighbouring built columns joined, whatever either was built <i>of</i> — a roof's edge
/// touching the plaza it stands over fuses to the plaza even in two different materials, and a themed map
/// that paints its terrain in the palette it builds with — a stone-brick cottage on a stone-brick town
/// square, a clay cage on a clay field — fuses doubly, since the two are not just both built but built
/// alike. What separates a building from the ground either way is not material but the step between them: a
/// wall or a roof stands several blocks over the ground it grows from, while a plaza's own surface is flat.
/// The flood therefore joins two
/// neighbouring built columns only when their tops are within <c>maximumStep</c> of each other, the same
/// discipline <c>--buildings</c> already applies to a roof — a pitch or a storey rises a block or two at a
/// time, while a painted floor beside a wall sits three or more below the wall it touches. This is a
/// geometric improvement, not a full one: an unroofed courtyard or unwalled porch at the ground's own
/// height still reads as ground, and two wings of one building separated by more than the step still read
/// as two structures. A full finder would want the enclosed volume a room stamps, which nothing records
/// after the fact — see <c>docs/tools/capabilities.md</c>'s renderer section for what this stops short of.</para>
///
/// <para>The natural ground a component is measured against is read the same way — <c>naturalY</c> looks
/// past the paint to the terrain underneath at every column, built or not, so a ring sampled around a
/// component still finds real ground even where the whole map wears one material.</para>
///
/// <para>The render puts the findings over a desaturated height profile, because a structure's placement only
/// means something against the terrain it was placed on. Nothing here reads what a material means (no
/// per-theme roof/path spec, unlike <c>--buildings</c>/<c>--flora</c>), which is what makes it the stage
/// image a generator can always ask for: it needs no knowledge of which theme built the world.</para>
/// </summary>
public static class StructureFinder
{
    /// <summary>How far one column's top may step from its neighbour's and still belong to the same
    /// structure — the same tolerance <c>--buildings</c> gives a roof pitch. Set low enough that a wall
    /// standing over a painted plaza breaks the flood; a caller reading a taller building can widen it.</summary>
    public const int DefaultMaximumStep = 4;

    /// <summary>What a structure's outline may be read through: nothing, and everything standing on the
    /// ground rather than being it.</summary>
    private static bool Skin(int id) => id == 0 || BlockRoles.SeenThrough(id);

    /// <summary>Natural ground: everything the terrain itself is made of, so a built column's own blocks and
    /// the liquids over them are stepped past to reach it.</summary>
    private static bool IsNaturalGround(int id) => BlockRoles.IsNaturalGround(id);

    public sealed record Structure(int MinX, int MaxX, int MinZ, int MaxZ, int Area, int RoofLow, int RoofHigh,
                                    int GroundAround, int GroundSpread, int BaseOffset, string Materials);

    public sealed record Result(byte[] Pixels, int BlocksWide, int BlocksHigh, List<Structure> Structures);

    public static int Run(string regionDir, string outPng, int scale, int minimumArea, int maximumStep = DefaultMaximumStep)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }
        return Emit(mcas.SelectMany(AnvilRegion.ReadChunks), outPng, scale, minimumArea, maximumStep);
    }

    /// <summary>Renders a world still held in memory, via <see cref="AnvilRegion.FromWorld"/>.</summary>
    public static int Run(VoxelWorld world, string outPng, int scale, int minimumArea, int maximumStep = DefaultMaximumStep)
        => Emit(AnvilRegion.FromWorld(world), outPng, scale, minimumArea, maximumStep);

    private static int Emit(IEnumerable<AnvilRegion.Chunk> chunks, string outPng, int scale, int minimumArea, int maximumStep)
    {
        var result = Render(chunks, minimumArea, maximumStep);
        if (result is null) { Console.Error.WriteLine("no columns decoded"); return 1; }

        Report(result.Structures);
        var scaled = Raster.Upscale(result.Pixels, result.BlocksWide, result.BlocksHigh, scale);
        PngWriter.Write(outPng, result.BlocksWide * scale, result.BlocksHigh * scale, scaled);
        Console.WriteLine($"  wrote {outPng} ({result.BlocksWide * scale}x{result.BlocksHigh * scale} px, {scale} px/block), " +
            $"{result.Structures.Count} structure(s) over the terrain");
        return 0;
    }

    /// <summary>The pure render: chunks in, findings + an RGB pixel buffer out. No file or console I/O.</summary>
    public static Result? Render(IEnumerable<AnvilRegion.Chunk> chunks, int minimumArea, int maximumStep = DefaultMaximumStep)
    {
        var topId = new Dictionary<(int X, int Z), int>();
        var topData = new Dictionary<(int X, int Z), int>();
        var topY = new Dictionary<(int X, int Z), int>();
        var baseY = new Dictionary<(int X, int Z), int>();
        var naturalY = new Dictionary<(int X, int Z), int>();
        foreach (var chunk in chunks) Scan(chunk, topId, topData, topY, baseY, naturalY);
        if (topY.Count == 0) return null;

        var builtCells = new HashSet<(int X, int Z)>(topId.Where(entry => BlockRoles.IsBuilt(entry.Value)).Select(entry => entry.Key));
        var structures = new List<Structure>();
        var claimed = new Dictionary<(int X, int Z), int>();

        var pending = new HashSet<(int X, int Z)>(builtCells);
        while (pending.Count > 0)
        {
            var seed = pending.First();
            var component = Flood(seed, pending, topY, maximumStep);
            if (component.Count < minimumArea) continue;

            var index = structures.Count;
            foreach (var cell in component) claimed[cell] = index;

            var roofs = component.Select(cell => topY[cell]).OrderBy(y => y).ToList();
            var bases = component.Select(cell => baseY[cell]).OrderBy(y => y).ToList();
            var ring = Ring(component).Where(naturalY.ContainsKey).Select(cell => naturalY[cell]).OrderBy(y => y).ToList();
            var materials = component.GroupBy(cell => BlockPalette.Name(topId[cell], topData[cell]))
                .OrderByDescending(group => group.Count()).Take(3)
                .Select(group => $"{group.Key} {group.Count() * 100 / component.Count}%");

            var groundLevel = ring.Count > 0 ? ring[ring.Count / 2] : roofs[0];
            // How uneven the ground it was placed on is, ignoring the tails so one boulder in the ring
            // does not read as a slope.
            var spread = ring.Count > 4 ? ring[(int)(ring.Count * 0.9)] - ring[(int)(ring.Count * 0.1)] : 0;

            structures.Add(new Structure(
                component.Min(cell => cell.X), component.Max(cell => cell.X),
                component.Min(cell => cell.Z), component.Max(cell => cell.Z),
                component.Count, roofs[0], roofs[^1],
                groundLevel, spread, bases[bases.Count / 2] - groundLevel, string.Join(", ", materials)));
        }

        var pixels = Draw(topY, naturalY, claimed, out var blocksWide, out var blocksHigh);
        return new Result(pixels, blocksWide, blocksHigh, structures);
    }

    private static void Scan(AnvilRegion.Chunk chunk, Dictionary<(int X, int Z), int> topId,
                             Dictionary<(int X, int Z), int> topData, Dictionary<(int X, int Z), int> topY,
                             Dictionary<(int X, int Z), int> baseY, Dictionary<(int X, int Z), int> naturalY)
    {
        var ids = new ushort[256 * 256];
        var data = new byte[256 * 256];
        foreach (var section in AnvilRegion.Sections(chunk))
        {
            var yStart = section.SectionY * 16;
            if (yStart is < 0 or >= 256) continue;
            Array.Copy(section.Ids, 0, ids, yStart * 256, 4096);
            Array.Copy(section.Data, 0, data, yStart * 256, 4096);
        }

        for (var lz = 0; lz < 16; lz++)
            for (var lx = 0; lx < 16; lx++)
            {
                var col = (lz << 4) | lx;
                var cell = (chunk.ChunkX * 16 + lx, chunk.ChunkZ * 16 + lz);
                var haveTop = false;
                for (var y = 255; y >= 0; y--)
                {
                    var id = ids[(y << 8) | col];
                    if (Skin(id)) continue;
                    if (!haveTop)
                    {
                        topId[cell] = id;
                        topData[cell] = data[(y << 8) | col];
                        topY[cell] = y;
                        // The base is where the unbroken run of built blocks under the top ends, which is
                        // the level the structure was actually seated at.
                        baseY[cell] = y;
                        for (var below = y; below >= 0 && (BlockRoles.IsBuilt(ids[(below << 8) | col]) || Skin(ids[(below << 8) | col])); below--)
                            baseY[cell] = below;
                        haveTop = true;
                    }
                    if (!IsNaturalGround(id)) continue;
                    naturalY[cell] = y;
                    break;
                }
            }
    }

    /// <summary>One connected component of built columns, 8-neighbour so a diagonal corner still joins — but
    /// only across a step of <paramref name="maximumStep"/> or less, so a wall standing over a painted plaza
    /// of the same material breaks the flood instead of fusing the building to the ground it grows from.</summary>
    private static List<(int X, int Z)> Flood((int X, int Z) seed, HashSet<(int X, int Z)> pending,
                                               Dictionary<(int X, int Z), int> topY, int maximumStep)
    {
        var component = new List<(int X, int Z)>();
        var queue = new Queue<(int X, int Z)>([seed]);
        pending.Remove(seed);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            component.Add(cell);
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var next = (cell.X + dx, cell.Z + dz);
                    if (!pending.Contains(next)) continue;
                    if (Math.Abs(topY[next] - topY[cell]) > maximumStep) continue;
                    pending.Remove(next);
                    queue.Enqueue(next);
                }
        }
        return component;
    }

    /// <summary>Columns just outside a component — the ground it stands on, which its own roof hides. Sampled
    /// through <c>naturalY</c> rather than filtered by material, so a component still finds real ground when
    /// the whole map, floor included, wears one paint.</summary>
    private static HashSet<(int X, int Z)> Ring(List<(int X, int Z)> component)
    {
        var inside = new HashSet<(int X, int Z)>(component);
        var ring = new HashSet<(int X, int Z)>();
        foreach (var cell in component)
            for (var dz = -3; dz <= 3; dz++)
                for (var dx = -3; dx <= 3; dx++)
                {
                    var next = (cell.X + dx, cell.Z + dz);
                    if (!inside.Contains(next)) ring.Add(next);
                }
        return ring;
    }

    private static void Report(List<Structure> structures)
    {
        Console.WriteLine($"{structures.Count} built structure(s) of the minimum size\n");
        Console.WriteLine($"{"x range",-14} {"z range",-14} {"area",6} {"roof y",10} {"ground",7} {"tall",5} {"rough",6} {"seat",5}  materials");
        foreach (var structure in structures.OrderByDescending(structure => structure.Area))
            Console.WriteLine($"{$"{structure.MinX}..{structure.MaxX}",-14} {$"{structure.MinZ}..{structure.MaxZ}",-14} " +
                $"{structure.Area,6} {$"{structure.RoofLow}..{structure.RoofHigh}",10} {structure.GroundAround,7} " +
                $"{structure.RoofHigh - structure.GroundAround,5} {structure.GroundSpread,6} {structure.BaseOffset,5:+0;-0;0}  {structure.Materials}");

        var tall = structures.Where(structure => structure.RoofHigh - structure.GroundAround >= 3).ToList();
        Console.WriteLine($"\n{tall.Count} stand 3+ blocks over the ground around them; " +
            $"{structures.Count - tall.Count} are flat — paths, floors, plazas.");

        // "rough" is how uneven the ground around a structure is; "seat" is where its lowest built block
        // sits against that ground — 0 is flush, positive floats above it, negative is dug in. A structure
        // dropped onto terrain nobody levelled shows up as rough ground with a non-zero seat.
        if (tall.Count == 0) return;
        var perched = tall.Where(structure => structure.GroundSpread >= 3 && structure.BaseOffset > 0).ToList();
        var levelled = tall.Count(structure => structure.GroundSpread <= 1);
        Console.WriteLine($"of those {tall.Count}: {levelled} sit on ground levelled to within 1 block, " +
            $"{perched.Count} stand on ground uneven by 3+ with their base above it");
        foreach (var structure in perched.OrderByDescending(structure => structure.GroundSpread).Take(8))
            Console.WriteLine($"    x {structure.MinX}..{structure.MaxX} z {structure.MinZ}..{structure.MaxZ}: " +
                $"ground uneven by {structure.GroundSpread}, base {structure.BaseOffset:+0;-0;0} over it");
    }

    /// <summary>Findings over a desaturated height profile: a structure's placement only means something
    /// against the terrain it was placed on.</summary>
    private static byte[] Draw(Dictionary<(int X, int Z), int> topY, Dictionary<(int X, int Z), int> naturalY,
                               Dictionary<(int X, int Z), int> claimed, out int blocksWide, out int blocksHigh)
    {
        int minX = topY.Keys.Min(cell => cell.X), maxX = topY.Keys.Max(cell => cell.X);
        int minZ = topY.Keys.Min(cell => cell.Z), maxZ = topY.Keys.Max(cell => cell.Z);
        blocksWide = maxX - minX + 1; blocksHigh = maxZ - minZ + 1;

        var terrain = naturalY.Values.ToList();
        int lowest = terrain.Count > 0 ? terrain.Min() : 0, highest = terrain.Count > 0 ? terrain.Max() : 0;
        var span = Math.Max(1, highest - lowest);

        // Structures far enough apart never share a colour, and the cycle is short enough to stay readable.
        int[] accents = [0xFF7A1F, 0x35D6C4, 0xFFD400, 0xFF4FA3, 0x7CFF4F, 0x9B7BFF];

        var pixels = new byte[blocksWide * blocksHigh * 3];
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                var cell = (minX + col, minZ + row);
                if (!naturalY.TryGetValue(cell, out var ground)) { Raster.Set(pixels, blocksWide, col, row, 0x0E0E12); continue; }
                var shade = Raster.Lerp(0x23252B, 0x8E9199, (ground - lowest) / (double)span);
                Raster.Set(pixels, blocksWide, col, row, shade);

                if (!claimed.TryGetValue(cell, out var index)) continue;
                var onEdge = Enumerable.Range(0, 4)
                    .Select(side => (cell.Item1 + (side == 0 ? 1 : side == 1 ? -1 : 0), cell.Item2 + (side == 2 ? 1 : side == 3 ? -1 : 0)))
                    .Any(neighbour => !claimed.TryGetValue(neighbour, out var other) || other != index);
                Raster.Over(pixels, blocksWide, col, row, accents[index % accents.Length], onEdge ? 1.0 : 0.62);
            }
        return pixels;
    }
}
