using PgmStudio.Domain;
using PgmStudio.Geom.Render;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

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
/// <para><b>"Built" alone cannot separate a building from the ground it stands on</b>, because it is a
/// yes/no over every material at once: any two neighbouring built columns would join, whatever either was
/// built <i>of</i>. A roof's edge touching the plaza it stands over fuses to the plaza even in two different
/// materials, and a themed map that paints its terrain in the palette it builds with — a stone-brick cottage
/// on a stone-brick town square, a clay cage on a clay field — fuses doubly, since the two are not just both
/// built but built alike. Absent a recorded extent, what separates them is not material but the step between
/// them: a wall or a roof stands several blocks over the ground it grows from, while a plaza's own
/// surface is flat. The flood therefore joins two
/// neighbouring built columns only when their tops are within <c>maximumStep</c> of each other, the same
/// discipline <c>--buildings</c> already applies to a roof — a pitch or a storey rises a block or two at a
/// time, while a painted floor beside a wall sits three or more below the wall it touches. That step test is
/// a geometric improvement over material alone, not a full fix: a roof laid flush with the plaza's own
/// paving height defeats it, since nothing steps between two flat surfaces of one material.</para>
///
/// <para><b>A recorded <see cref="WorldProvenance"/> closes that residual gap, and is preferred
/// whenever one is given.</b> A built map knows which columns a stamp claimed at the moment it claimed them,
/// so "built" stops being read off the block on top and becomes a lookup: a column is a candidate only when
/// the build itself recorded it as <see cref="ProvenancePass.Structure"/>, whatever it is made of and
/// however level it sits against the paving beside it. A stamped building's extent is then recorded rather
/// than flooded for, so a roof flush with its own plaza cannot fuse with it — the plaza was never a
/// candidate at all — and the step test (which can still fragment one tall roof into two components by
/// height alone) is dropped in favour of the recorded extent. A world with no provenance — a scanned map, or
/// one built before this recording existed — keeps the step test, since material is the only signal it has.</para>
///
/// <para><b>With provenance, a candidate column is grouped rather than flooded.</b> Adjacency alone cannot
/// tell two buildings that genuinely touch — a terrace, a shared wall — apart, so each stamp's claim carries
/// an owner and the candidates are partitioned by it directly: every column a given owner claimed is one
/// finding, whether or not it happens to neighbour a column another owner claimed. Two houses that stand
/// wall to wall read as two structures for exactly the reason a flood could never give — the columns were
/// never one claim to begin with. A column whose claim carries no id groups with every other column that also
/// carries none, which is the degraded reading for an unidentified claim rather than a case this reader tries
/// to recover from. Where an id <em>is</em> carried it names the unit and the orbit image separately, so a
/// building and its own mirror are one identity seen twice and are coloured as one thing.</para>
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

    /// <summary>An accent slot per finding, equal for findings that share a <see cref="StampId.Identity"/>. Slots are
    /// handed out in the identities' own sorted order rather than in discovery order, so the same board draws
    /// the same colours twice and a mirrored pair matches whichever half is found first. A finding with no
    /// identity at all — every one of them on a world with no provenance to read — keeps a slot of its own,
    /// because nothing there says which two findings are one thing.</summary>
    private static int[] AccentSlots(IReadOnlyList<(string Kind, string Unit)?> identities)
    {
        var order = identities.OfType<(string Kind, string Unit)>().Distinct()
                              .OrderBy(identity => identity.Kind, StringComparer.Ordinal)
                              .ThenBy(identity => identity.Unit, StringComparer.Ordinal)
                              .Select((identity, slot) => (identity, slot))
                              .ToDictionary(entry => entry.identity, entry => entry.slot);

        var slots = new int[identities.Count];
        var next = order.Count;
        for (var i = 0; i < identities.Count; i++)
            slots[i] = identities[i] is { } identity ? order[identity] : next++;
        return slots;
    }

    public sealed record Result(byte[] Pixels, int BlocksWide, int BlocksHigh, List<Structure> Structures);

    /// <summary>Reads a built region directory from disk. Picks up <see cref="WorldProvenanceFile"/>'s
    /// sidecar automatically when the region carries one; a region with none falls back to the step-tested
    /// material reading, exactly as a world carrying no record does.</summary>
    public static int Run(string regionDir, string outPng, int scale, int minimumArea, int maximumStep = DefaultMaximumStep)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }
        return Emit(mcas.SelectMany(AnvilRegion.ReadChunks), outPng, scale, minimumArea, maximumStep,
            WorldProvenanceFile.TryRead(regionDir)) is null ? 1 : 0;
    }

    /// <summary>The finished structure census as bytes, for a caller that wants the image rather than a
    /// file. Null where the world decodes to no column.</summary>
    public static byte[]? Png(VoxelWorld world, int scale, int minimumArea,
        int maximumStep = DefaultMaximumStep, WorldProvenance? provenance = null)
        => Emit(AnvilRegion.FromWorld(world), null, scale, minimumArea, maximumStep, provenance);

    public static int Run(VoxelWorld world, string outPng, int scale, int minimumArea,
        int maximumStep = DefaultMaximumStep, WorldProvenance? provenance = null)
        => Emit(AnvilRegion.FromWorld(world), outPng, scale, minimumArea, maximumStep, provenance) is null ? 1 : 0;

    private static byte[]? Emit(IEnumerable<AnvilRegion.Chunk> chunks, string? outPng, int scale, int minimumArea,
        int maximumStep, WorldProvenance? provenance)
    {
        var result = Render(chunks, minimumArea, maximumStep, provenance);
        if (result is null) { if (outPng is not null) Console.Error.WriteLine("no columns decoded"); return null; }

        Report(result.Structures);
        var scaled = Raster.Upscale(result.Pixels, result.BlocksWide, result.BlocksHigh, scale);
        List<Legend.Entry> entries =
        [
            new("NATURAL GROUND (SHADED BY HEIGHT)", 0x5B5E66),
            new("STRUCTURE (ONE ACCENT PER IDENTITY - A MIRRORED PAIR SHARES ONE)", 0xFF7A1F),
            new("VOID", 0x0E0E12),
        ];
        // Whether "structure" was read off a recorded extent or off material + step is exactly the fact
        // capabilities.md's renderer section warns an image cannot carry by colour alone — so it goes
        // into the scale line every render already bakes onto the picture.
        var extentReading = provenance is not null ? "recorded provenance" : $"material + step (max {maximumStep})";
        var withLegend = Legend.AppendBelow(scaled, result.BlocksWide * scale, result.BlocksHigh * scale, entries,
            out var legendHeight,
            scaleLabel: $"SCALE: 1 BLOCK = {scale} PX - {result.BlocksWide} X {result.BlocksHigh} BLOCKS" +
                        $"  -  STRUCTURE EXTENT: {extentReading.ToUpperInvariant()}");
        var png = PngWriter.Encode(result.BlocksWide * scale, legendHeight, withLegend);
        if (outPng is null) return png;
        File.WriteAllBytes(outPng, png);
        Console.WriteLine($"  wrote {outPng} ({result.BlocksWide * scale}x{legendHeight} px, {scale} px/block), " +
            $"{result.Structures.Count} structure(s) over the terrain, extent: {extentReading}");
        return png;
    }

    /// <summary>The pure render: chunks in, findings + an RGB pixel buffer out. No file or console I/O.
    /// <paramref name="provenance"/> non-null makes a column's candidacy a recorded fact rather than a
    /// material + step guess (see the class remarks); <paramref name="maximumStep"/> is then unused.</summary>
    public static Result? Render(IEnumerable<AnvilRegion.Chunk> chunks, int minimumArea,
        int maximumStep = DefaultMaximumStep, WorldProvenance? provenance = null)
    {
        var topId = new Dictionary<(int X, int Z), int>();
        var topData = new Dictionary<(int X, int Z), int>();
        var topY = new Dictionary<(int X, int Z), int>();
        var baseY = new Dictionary<(int X, int Z), int>();
        var naturalY = new Dictionary<(int X, int Z), int>();
        foreach (var chunk in chunks) Scan(chunk, topId, topData, topY, baseY, naturalY);
        if (topY.Count == 0) return null;

        var builtCells = provenance is null
            ? new HashSet<(int X, int Z)>(topId.Where(entry => BlockRoles.IsBuilt(entry.Value)).Select(entry => entry.Key))
            : new HashSet<(int X, int Z)>(topId.Keys.Where(cell => provenance.PassAt(cell.X, cell.Z) == ProvenancePass.Structure));
        var structures = new List<Structure>();
        var claimed = new Dictionary<(int X, int Z), int>();

        // Provenance answers "whose claim is this" directly, so the candidates are partitioned by owner
        // rather than flooded for adjacency; absent provenance, adjacency plus the step test is still the
        // only signal there is.
        var components = provenance is null
            ? Flood(builtCells, topY, maximumStep).Select(cells => (Owner: (StampId?)null, Cells: cells))
            : builtCells.GroupBy(cell => provenance.OwnerAt(cell.X, cell.Z))
                .Select(group => (Owner: group.Key, Cells: (IReadOnlyList<(int X, int Z)>)[.. group]));

        // What each finding is, as opposed to which image of it this one is — the key the accent is chosen
        // by, so a structure and its mirror come out the same colour and a genuinely unpaired one stands out.
        var identityOf = new List<(string Kind, string Unit)?>();

        foreach (var (owner, component) in components)
        {
            if (component.Count < minimumArea) continue;

            var index = structures.Count;
            foreach (var cell in component) claimed[cell] = index;
            identityOf.Add(owner?.Identity);

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

        var pixels = Draw(topY, naturalY, claimed, AccentSlots(identityOf), out var blocksWide, out var blocksHigh);
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

    /// <summary>Every connected component of built columns, material-and-step read: no provenance to name
    /// which stamp a column belongs to, so adjacency is the only signal there is.</summary>
    private static IEnumerable<IReadOnlyList<(int X, int Z)>> Flood(
        HashSet<(int X, int Z)> builtCells, Dictionary<(int X, int Z), int> topY, int maximumStep)
    {
        var pending = new HashSet<(int X, int Z)>(builtCells);
        while (pending.Count > 0)
        {
            var seed = pending.First();
            yield return FloodOne(seed, pending, topY, maximumStep);
        }
    }

    /// <summary>One connected component of built columns, 8-neighbour so a diagonal corner still joins — but
    /// only across a step of <paramref name="maximumStep"/> or less, so a wall standing over a painted plaza
    /// of the same material breaks the flood instead of fusing the building to the ground it grows from.</summary>
    private static List<(int X, int Z)> FloodOne((int X, int Z) seed, HashSet<(int X, int Z)> pending,
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
    private static HashSet<(int X, int Z)> Ring(IReadOnlyList<(int X, int Z)> component)
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
                               Dictionary<(int X, int Z), int> claimed, int[] accentSlots,
                               out int blocksWide, out int blocksHigh)
    {
        int minX = topY.Keys.Min(cell => cell.X), maxX = topY.Keys.Max(cell => cell.X);
        int minZ = topY.Keys.Min(cell => cell.Z), maxZ = topY.Keys.Max(cell => cell.Z);
        blocksWide = maxX - minX + 1; blocksHigh = maxZ - minZ + 1;

        var terrain = naturalY.Values.ToList();
        int lowest = terrain.Count > 0 ? terrain.Min() : 0, highest = terrain.Count > 0 ? terrain.Max() : 0;
        var span = Math.Max(1, highest - lowest);

        // One accent per identity rather than per finding, so a structure and its mirror image come out the
        // same colour and an unpaired one is the thing that stands out — which is the single question these
        // pictures are looked at to answer on a mirrored board.
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
                Raster.Over(pixels, blocksWide, col, row, accents[accentSlots[index] % accents.Length], onEdge ? 1.0 : 0.62);
            }
        return pixels;
    }
}
