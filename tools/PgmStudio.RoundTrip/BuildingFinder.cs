using PgmStudio.Minecraft;

namespace PgmStudio.RoundTrip;

/// <summary>
/// Buildings found from their roofs. A roof is the one part of a building that is always visible from above,
/// always made of a material the terrain never uses, and always continuous — walls are hidden under it,
/// floors are hidden inside it, and a footprint drawn from anything else has to guess where a structure ends.
///
/// <para><b>A roof has a room under it.</b> Clearance over the terrain is the first gate — plank laid on the
/// ground is decking — but clearance alone passes a floor raised on a slope. What no floor has is emptiness
/// beneath: follow each column's solid run downward and a floor reaches the terrain almost everywhere, while
/// a building grounds only where its walls are and is hollow between them. The share of grounded columns is
/// therefore bounded <b>above</b> as well as below, and the two populations do not overlap — every house on
/// the map this was built for grounds between 15% and 31% of its roof, every plank floor between 93% and
/// 100%.</para>
///
/// <para><b>Two measures are reported and neither gates.</b> The share of roof columns with a solid run to the
/// terrain separates what stands from what hangs, and the count of footprint corners carrying a vertical log
/// says how far a candidate fits a framing convention. Alone, each is ambiguous: a hollow shaft head grounds
/// nowhere and so does a floating objective marker. <b>Together they separate cleanly</b> — the shaft head is
/// framed at all four corners because it is built like the houses around it, and the marker is framed at none
/// because it is not a building at all. A caller who knows the map reads the pair; the tool states them and
/// classifies without discarding, so a wrong call stays visible instead of becoming a missing row.</para>
/// </summary>
internal static class BuildingFinder
{
    /// <summary>Plant cover and small furniture, stepped past so a roof under a snow layer is still a roof.
    /// Glass is included: it is the usual way to draw smoke over a chimney, and a roof read at the smoke stops
    /// at a column of glass rather than at the roof it rises from. The cost is that a genuine glass roof is
    /// seen through, which is the right trade when glass over a building is far more often weather than
    /// shelter.</summary>
    private static readonly HashSet<int> Cover =
    [
        0, 6, 18, 20, 30, 31, 32, 37, 38, 39, 40, 50, 51, 55, 59, 63, 66, 68, 69, 70, 71, 72, 75, 76, 77, 78,
        83, 95, 102, 104, 105, 106, 111, 115, 141, 142, 143, 157, 160, 161, 175,
    ];

    /// <summary>A slab's high data bit says which half of its block it fills, not what it is made of, so a
    /// roof laid in upside-down slabs is the same roof as one laid in upright ones. Matching without masking
    /// it splits a roof down the middle and loses whichever half the author chose to hang.</summary>
    /// <summary>Grounded share at or above which a component is floored rather than roofed. Set far above
    /// any real roof: the gap between the two populations is 31% to 93%, so the exact value does not matter
    /// and a reading anywhere inside that gap says the same thing.</summary>
    private const double SolidBeneath = 0.8;

    private static int MaterialBits(int id, int data) => id is 43 or 44 or 125 or 126 ? data & 7 : data;

    /// <summary>Terrain: what the ground itself is made of, so a structure's own blocks are stepped past to
    /// find the surface it was placed on.</summary>
    /// <summary>Terrain: what the ground itself is made of. Cobblestone and stone brick are included because
    /// a foundation is ground as far as a roof is concerned — a building set on a plinth still stands on the
    /// map, and treating its plinth as structure would measure the roof's clearance from the wrong datum.</summary>
    private static bool IsTerrain(int id) => id is 1 or 2 or 3 or 4 or 12 or 13 or 24 or 48 or 60 or 62 or 79
        or 80 or 82 or 87 or 88 or 98 or 110 or 121 or 129 or 153 or 155 or 159 or 172 or 174 or 179;

    private sealed record Column(int RoofId, int RoofData, int RoofY, int SolidBottom, int GroundY, bool HasLog,
                                 bool WalledAtBase);

    private sealed record Candidate(int MinX, int MaxX, int MinZ, int MaxZ, int Footprint, int RoofLow,
                                    int RoofHigh, int GroundY, double Grounded, int Corners, double Walled,
                                    double Rim, string Materials)
    {
        /// <summary>What the measures say together. The rim decides: a house roof is laid as a fill enclosed
        /// by a border of a second wood, and a cap that is one material throughout was never a house roof
        /// however well it is framed or walled. Nothing is dropped on this — it is a reading offered next to
        /// the numbers it came from.</summary>
        public string Reading => Corners < 3 && Grounded < 0.05 ? "hangs, unframed — not a building"
            : Rim >= 0.1 ? "fill inside a rim — a house roof"
            : "one material throughout — a cap, not a house roof";
    }

    public static int Run(string regionDir, string outPng, int scale, IReadOnlyList<(int Id, int Data)> roofSpec,
                          IReadOnlyList<(int Id, int Data)> rimSpec, int minimumArea, int minimumHeight,
                          int minimumSide)
    {
        if (!Directory.Exists(regionDir)) { Console.Error.WriteLine($"no region dir: {regionDir}"); return 1; }
        if (roofSpec.Count == 0) { Console.Error.WriteLine("--buildings needs --roof <id[:data],...>"); return 1; }
        var mcas = Directory.GetFiles(regionDir, "*.mca");
        if (mcas.Length == 0) { Console.Error.WriteLine($"no region files in {regionDir}"); return 1; }

        var exact = new HashSet<(int Id, int Data)>(roofSpec.Where(entry => entry.Data >= 0));
        var anyData = new HashSet<int>(roofSpec.Where(entry => entry.Data < 0).Select(entry => entry.Id));
        bool IsRoof(int id, int data) => anyData.Contains(id) || exact.Contains((id, MaterialBits(id, data)));

        // The rim is the second wood a roof is bordered with. Its presence is what tells a laid roof from a
        // cap: a house roof is a fill enclosed by a border, and a cap is one material all the way across.
        var rimExact = new HashSet<(int Id, int Data)>(rimSpec.Where(entry => entry.Data >= 0));
        var rimAny = new HashSet<int>(rimSpec.Where(entry => entry.Data < 0).Select(entry => entry.Id));
        bool IsRim(int id, int data) => rimAny.Contains(id) || rimExact.Contains((id, MaterialBits(id, data)));

        var columns = new Dictionary<(int X, int Z), Column>();
        var terrain = new Dictionary<(int X, int Z), int>();
        foreach (var mca in mcas)
            foreach (var chunk in AnvilRegion.ReadChunks(mca))
                Scan(chunk, IsRoof, columns, terrain);
        if (terrain.Count == 0) { Console.Error.WriteLine("no columns decoded"); return 1; }

        var roofCells = new HashSet<(int X, int Z)>(columns.Keys);
        var standing = new List<Candidate>();
        var floating = new List<Candidate>();
        var claimed = new Dictionary<(int X, int Z), int>();

        var floors = 0;
        var pending = new HashSet<(int X, int Z)>(roofCells);
        while (pending.Count > 0)
        {
            var component = Flood(pending.First(), pending);
            if (component.Count < minimumArea) continue;

            var grounded = component.Count(cell =>
                columns[cell].SolidBottom <= columns[cell].GroundY + 1) / (double)component.Count;

            int minX = component.Min(cell => cell.X), maxX = component.Max(cell => cell.X);
            int minZ = component.Min(cell => cell.Z), maxZ = component.Max(cell => cell.Z);
            var roofs = component.Select(cell => columns[cell].RoofY).OrderBy(y => y).ToList();
            // The ground to measure against is the terrain directly under the roof, not a ring outside it.
            // On a slope the ring sits well below the footprint, which lets plank laid flat into a hillside
            // as texture read as a roof standing several blocks clear of nothing.
            var below = component.Where(terrain.ContainsKey).Select(cell => terrain[cell]).OrderBy(y => y).ToList();
            var materials = component.GroupBy(cell => BlockPalette.Name(columns[cell].RoofId, columns[cell].RoofData))
                .OrderByDescending(group => group.Count()).Take(2)
                .Select(group => $"{group.Key} {group.Count() * 100 / component.Count}%");

            var candidate = new Candidate(minX, maxX, minZ, maxZ, component.Count, roofs[0], roofs[^1],
                below.Count > 0 ? below[below.Count / 2] : roofs[0], grounded,
                CornerStems(minX, maxX, minZ, maxZ, columns),
                component.Count(cell => columns[cell].WalledAtBase) / (double)component.Count,
                rimSpec.Count == 0 ? 1.0
                    : component.Count(cell => IsRim(columns[cell].RoofId, columns[cell].RoofData)) / (double)component.Count,
                string.Join(", ", materials));

            // Clearance over the terrain beneath, measured at the ridge rather than the eave: a pitched roof
            // comes down to meet its walls, so its lowest course sits as close to the ground as decking does
            // and testing there discards the houses along with the decking.
            if (candidate.RoofHigh - candidate.GroundY < minimumHeight) continue;

            // Solid under nearly every column is a floor, however high the slope beneath carries it. A
            // building is hollow between its walls, so a roof cannot ground everywhere and remain a roof.
            if (grounded >= SolidBeneath) { floors++; continue; }
            // The smallest thing a map builds is still a room. Below that a component is a cap, a marker or
            // a fragment of decking, and no measure taken on it means anything.
            if (maxX - minX + 1 < minimumSide || maxZ - minZ + 1 < minimumSide) continue;

            foreach (var cell in component) claimed[cell] = standing.Count;
            standing.Add(candidate);
        }

        Report(standing, floating, floors);
        _ = floating;
        Draw(outPng, scale, terrain, claimed);
        return 0;
    }

    private static void Scan(AnvilRegion.Chunk chunk, Func<int, int, bool> isRoof,
                             Dictionary<(int X, int Z), Column> columns, Dictionary<(int X, int Z), int> terrain)
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

                var groundY = 0;
                for (var y = 255; y >= 0; y--)
                    if (IsTerrain(ids[(y << 8) | col])) { groundY = y; break; }
                terrain[cell] = groundY;

                var roofY = -1;
                for (var y = 255; y >= 0; y--)
                {
                    var id = ids[(y << 8) | col];
                    if (Cover.Contains(id)) continue;
                    if (isRoof(id, data[(y << 8) | col])) roofY = y;
                    break;
                }
                if (roofY < 0) continue;

                // How far the solid stack under this roof reaches. A wall carries it to the terrain; a
                // hollow interior stops one block down; a structure in the air stops far above the ground.
                var bottom = roofY;
                while (bottom > 0 && ids[((bottom - 1) << 8) | col] != 0 &&
                       !Cover.Contains(ids[((bottom - 1) << 8) | col])) bottom--;

                var hasLog = false;
                for (var y = groundY; y <= roofY && !hasLog; y++) hasLog = ids[(y << 8) | col] is 17 or 162;

                // Anything solid standing in the first courses above the ground. A house fills the span
                // between its corner posts with wall; a shaft head carries the same posts over open air.
                var walled = false;
                for (var y = groundY + 1; y <= Math.Min(groundY + 3, roofY - 1) && !walled; y++)
                {
                    var here = ids[(y << 8) | col];
                    walled = here != 0 && !Cover.Contains(here);
                }

                columns[cell] = new Column(ids[(roofY << 8) | col], data[(roofY << 8) | col], roofY, bottom,
                    groundY, hasLog, walled);
            }
    }

    private static List<(int X, int Z)> Flood((int X, int Z) seed, HashSet<(int X, int Z)> pending)
    {
        var component = new List<(int X, int Z)>();
        pending.Remove(seed);
        var queue = new Queue<(int X, int Z)>([seed]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            component.Add(cell);
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dz != 0) && pending.Remove((cell.X + dx, cell.Z + dz)))
                        queue.Enqueue((cell.X + dx, cell.Z + dz));
        }
        return component;
    }

    /// <summary>How many of the four footprint corners carry a vertical log within a block or two. Reported
    /// as a fit to one map's framing convention, never used to accept or reject.</summary>
    private static int CornerStems(int minX, int maxX, int minZ, int maxZ, Dictionary<(int X, int Z), Column> columns)
    {
        var found = 0;
        foreach (var (cornerX, cornerZ) in new[] { (minX, minZ), (minX, maxZ), (maxX, minZ), (maxX, maxZ) })
        {
            var near = false;
            for (var dx = -2; dx <= 2 && !near; dx++)
                for (var dz = -2; dz <= 2 && !near; dz++)
                    near = columns.TryGetValue((cornerX + dx, cornerZ + dz), out var column) && column.HasLog;
            if (near) found++;
        }
        return found;
    }

    private static void Report(List<Candidate> standing, List<Candidate> floating, int floors)
    {
        Console.WriteLine($"{standing.Count} roof component(s) standing clear of the terrain " +
            $"({floors} more were solid beneath — floors, not roofs)\n");
        foreach (var group in standing.GroupBy(item => item.Reading).OrderByDescending(group => group.Count()))
            Console.WriteLine($"  {group.Count(),3}  {group.Key}");
        Console.WriteLine();
        Console.WriteLine($"{"x range",-14} {"z range",-14} {"roof",6} {"size",8} {"y",10} {"tall",5} {"gnd",6} {"cnr",4} {"wall",6} {"rim",5}  materials");
        foreach (var building in standing.OrderByDescending(building => building.Footprint))
            Console.WriteLine(Line(building));

        var footprints = standing.GroupBy(building =>
            $"{building.MaxX - building.MinX + 1}x{building.MaxZ - building.MinZ + 1}")
            .OrderByDescending(group => group.Count());
        Console.WriteLine("\nfootprint sizes: " + string.Join(", ", footprints.Select(group => $"{group.Key} x{group.Count()}")));
        Console.WriteLine($"corner stems: {standing.Count(building => building.Corners == 4)} of {standing.Count} " +
            $"carry a log at all four corners");

        foreach (var group in standing.Where(item => item.Grounded < 0.05)
                     .GroupBy(item => item.Reading).OrderByDescending(group => group.Count()))
        {
            Console.WriteLine($"\n{group.Count()} — {group.Key}:");
            foreach (var item in group.OrderByDescending(item => item.Footprint)) Console.WriteLine(Line(item));
        }
    }

    private static string Line(Candidate item) =>
        $"{$"{item.MinX}..{item.MaxX}",-14} {$"{item.MinZ}..{item.MaxZ}",-14} {item.Footprint,6} " +
        $"{$"{item.MaxX - item.MinX + 1}x{item.MaxZ - item.MinZ + 1}",8} {$"{item.RoofLow}..{item.RoofHigh}",10} " +
        $"{item.RoofHigh - item.GroundY,5} {item.Grounded,5:0%} {item.Corners,4} {item.Walled,6:0%} {item.Rim,5:0%}  {item.Materials}";

    private static void Draw(string outPng, int scale, Dictionary<(int X, int Z), int> terrain,
                             Dictionary<(int X, int Z), int> claimed)
    {
        int minX = terrain.Keys.Min(cell => cell.X), maxX = terrain.Keys.Max(cell => cell.X);
        int minZ = terrain.Keys.Min(cell => cell.Z), maxZ = terrain.Keys.Max(cell => cell.Z);
        int blocksWide = maxX - minX + 1, blocksHigh = maxZ - minZ + 1;
        int lowest = terrain.Values.Min(), highest = terrain.Values.Max();
        var span = Math.Max(1, highest - lowest);

        int[] accents = [0xFF7A1F, 0x35D6C4, 0xFFD400, 0xFF4FA3, 0x7CFF4F, 0x9B7BFF];
        const int Rejected = 0xFF2020;

        var pixels = new byte[blocksWide * blocksHigh * 3];
        for (var row = 0; row < blocksHigh; row++)
            for (var col = 0; col < blocksWide; col++)
            {
                var cell = (minX + col, minZ + row);
                if (!terrain.TryGetValue(cell, out var height)) { Raster.Set(pixels, blocksWide, col, row, 0x0E0E12); continue; }
                Raster.Set(pixels, blocksWide, col, row, Raster.Lerp(0x212429, 0x82868E, (height - lowest) / (double)span));

                if (!claimed.TryGetValue(cell, out var index)) continue;
                var accent = index < 0 ? Rejected : accents[index % accents.Length];
                var onEdge = Enumerable.Range(0, 4)
                    .Select(side => (cell.Item1 + (side == 0 ? 1 : side == 1 ? -1 : 0), cell.Item2 + (side == 2 ? 1 : side == 3 ? -1 : 0)))
                    .Any(neighbour => !claimed.TryGetValue(neighbour, out var other) || other != index);
                Raster.Over(pixels, blocksWide, col, row, accent, onEdge ? 1.0 : 0.6);
            }

        var scaled = Raster.Upscale(pixels, blocksWide, blocksHigh, scale);
        PngWriter.Write(outPng, blocksWide * scale, blocksHigh * scale, scaled);
        Console.WriteLine($"\n  wrote {outPng} ({blocksWide * scale}x{blocksHigh * scale} px, {scale} px/block); " +
            $"rejected structures drawn in red");
    }
}
