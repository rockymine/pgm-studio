#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj

// ISOLATION is the signal the earlier passes missed. The neighbourhood dump showed that 89% of declared
// destroyables have ZERO blocks of their own material within 10 blocks — they are the only thing made of that
// stuff anywhere near. Decoration is the opposite: a material used architecturally appears again and again.
//
// This scores that against the false positives, alongside elevation above the local terrain (declared
// structures sit a median of 5 blocks above it), with no size cap and no air-face requirement — the two
// filters that were throwing away a third of the truth.

using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Pgm;

var roots = Directory.GetDirectories("/media/sf_repos/CommunityMaps")
    .Concat(Directory.Exists("/workspace/overcastcommunity/publicmaps")
        ? Directory.GetDirectories("/workspace/overcastcommunity/publicmaps") : [])
    .ToList();
// Every material the corpus actually declares a destroyable in — the "closed four" is not closed.
// The four that carry 84% of declared destroyables. Wool, stained clay and stained glass carry another 8%
// between them and are ruinous as candidate materials — a CTW map is made of wool, so admitting it produced
// 439,440 clusters against 15,480 for the four. A material a map is built from cannot mark a goal in it.
var materials = new Dictionary<int, string>
{
    [49] = "obsidian", [133] = "emerald", [41] = "gold", [121] = "endstone",
};
var faces = new (int X, int Y, int Z)[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };
const int Reach = 10;

var rows = new List<(bool True, string Material, int Size, int SameNearby, int AboveSurface, int AirPct)>();
int maps = 0, truths = 0;

foreach (var mapDir in roots.SelectMany(Directory.GetDirectories).OrderBy(d => d, StringComparer.Ordinal))
{
    var xmlPath = Path.Combine(mapDir, "map.xml");
    var regionDir = Path.Combine(mapDir, "region");
    if (!File.Exists(xmlPath) || !Directory.Exists(regionDir)) continue;

    MapXml map;
    try { map = MapParser.Parse(xmlPath); }
    catch (UnsupportedMapException) { continue; }
    var objectives = map.Destroyables.Where(d => d.IsObjective).ToList();
    if (objectives.Count == 0) continue;

    List<AnvilRegion.Chunk> chunks;
    try { chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks).ToList(); }
    catch { continue; }
    if (chunks.Count == 0) continue;
    maps++;

    var world = new Dictionary<(int X, int Y, int Z), int>();
    foreach (var chunk in chunks)
        foreach (var block in AnvilRegion.Blocks(chunk))
            world[(block.X, block.Y, block.Z)] = block.Id;

    var trueCells = new HashSet<(int X, int Y, int Z)>();
    foreach (var destroyable in objectives)
    {
        var ids = MaterialIds.Resolve(destroyable.Materials);
        if (ids.Count == 0) continue;
        var any = false;
        foreach (var box in RegionBoxes.Of(map.Regions, destroyable.RegionId))
            for (var x = box.MinX; x <= box.MaxX; x++)
                for (var y = box.MinY; y <= box.MaxY; y++)
                    for (var z = box.MinZ; z <= box.MaxZ; z++)
                        if (ids.Contains(world.GetValueOrDefault((x, y, z)))) { trueCells.Add((x, y, z)); any = true; }
        if (any) truths++;
    }

    // per-material cells, kept for the isolation query
    var byMaterial = new Dictionary<int, HashSet<(int X, int Y, int Z)>>();
    foreach (var (id, _) in materials)
        byMaterial[id] = world.Where(entry => entry.Value == id).Select(entry => entry.Key).ToHashSet();

    foreach (var (materialId, materialName) in materials)
    {
        var cells = byMaterial[materialId];
        var seen = new HashSet<(int X, int Y, int Z)>();
        foreach (var start in cells)
        {
            if (!seen.Add(start)) continue;
            var blob = new HashSet<(int X, int Y, int Z)> { start };
            var queue = new Queue<(int X, int Y, int Z)>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var face in faces)
                {
                    var next = (cell.X + face.X, cell.Y + face.Y, cell.Z + face.Z);
                    if (cells.Contains(next) && seen.Add(next)) { blob.Add(next); queue.Enqueue(next); }
                }
            }

            int minX = blob.Min(c => c.X), maxX = blob.Max(c => c.X);
            int minY = blob.Min(c => c.Y), maxY = blob.Max(c => c.Y);
            int minZ = blob.Min(c => c.Z), maxZ = blob.Max(c => c.Z);

            // ISOLATION: blocks of the same material within Reach that are not part of this cluster
            var sameNearby = 0;
            foreach (var cell in cells)
            {
                if (blob.Contains(cell)) continue;
                if (cell.X >= minX - Reach && cell.X <= maxX + Reach
                    && cell.Y >= minY - Reach && cell.Y <= maxY + Reach
                    && cell.Z >= minZ - Reach && cell.Z <= maxZ + Reach) sameNearby++;
                if (sameNearby > 64) break;
            }

            // elevation above the surrounding terrain, read from the ring around the cluster
            var heights = new List<int>();
            for (var x = minX - Reach; x <= maxX + Reach; x += 2)
                for (var z = minZ - Reach; z <= maxZ + Reach; z += 2)
                {
                    if (x >= minX && x <= maxX && z >= minZ && z <= maxZ) continue;
                    for (var y = maxY + Reach; y >= 0; y--)
                        if (world.ContainsKey((x, y, z))) { heights.Add(y); break; }
                }
            heights.Sort();
            var above = heights.Count == 0 ? 0 : minY - heights[heights.Count / 2];

            int airFaces = 0, solidFaces = 0;
            foreach (var cell in blob)
                foreach (var face in faces)
                {
                    var next = (cell.X + face.X, cell.Y + face.Y, cell.Z + face.Z);
                    if (blob.Contains(next)) continue;
                    if (world.ContainsKey(next)) solidFaces++; else airFaces++;
                }

            rows.Add((blob.Overlaps(trueCells), materialName, blob.Count, sameNearby, above,
                      airFaces + solidFaces == 0 ? 0 : 100 * airFaces / (airFaces + solidFaces)));
        }
    }
}

var trues = rows.Where(r => r.True).ToList();
var falses = rows.Where(r => !r.True).ToList();
Console.WriteLine($"maps {maps}, declared structures resolved {truths}");
Console.WriteLine($"clusters {rows.Count}: true {trues.Count}, false {falses.Count}");

static void Split(string label, List<(bool True, string Material, int Size, int SameNearby, int AboveSurface, int AirPct)> set,
                  Func<(bool, string, int, int, int, int), int> pick)
{
    var values = set.Select(row => pick((row.True, row.Material, row.Size, row.SameNearby, row.AboveSurface, row.AirPct))).OrderBy(v => v).ToList();
    if (values.Count == 0) { Console.WriteLine($"    {label,-10} none"); return; }
    int P(double q) => values[Math.Min(values.Count - 1, (int)(values.Count * q))];
    Console.WriteLine($"    {label,-10} p10={P(.1),5} p25={P(.25),5} med={P(.5),5} p75={P(.75),5} p90={P(.9),5}");
}

Console.WriteLine("\nISOLATION — same-material blocks within 10 (capped at 65):");
Split("true", trues, row => row.Item4);
Split("false", falses, row => row.Item4);
Console.WriteLine($"    zero nearby:  true {trues.Count(r => r.SameNearby == 0)}/{trues.Count}"
                  + $"   false {falses.Count(r => r.SameNearby == 0)}/{falses.Count}");

Console.WriteLine("\nELEVATION above local terrain:");
Split("true", trues, row => row.Item5);
Split("false", falses, row => row.Item5);

Console.WriteLine("\nGATE GRID — isolation × elevation, no size cap, no air-face requirement:");
Console.WriteLine("   maxSame  minAbove |  true kept          false kept   precision");
foreach (var maxSame in new[] { 0, 2, 8, 64 })
    foreach (var minAbove in new[] { -99, 0, 2, 4 })
    {
        var t = trues.Count(r => r.SameNearby <= maxSame && r.AboveSurface >= minAbove);
        var f = falses.Count(r => r.SameNearby <= maxSame && r.AboveSurface >= minAbove);
        Console.WriteLine($"   {maxSame,7}  {minAbove,8} |  {t,5}/{trues.Count,-5}      {f,7}      {(t + f == 0 ? 0 : 100.0 * t / (t + f)),5:F1}%");
    }
