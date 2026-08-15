#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj

// Measures how discriminating the two objective signatures are, BEFORE any classifier exists.
//
//   core        = lava enclosed by obsidian
//   destroyable = a small isolated cluster in the objective material vocabulary
//
// Ground truth is the map's own XML: every declared <destroyable>/<core> resolves to a region, and the blocks
// matching its material inside that region are the real structure. The question this answers is not "can a
// signature find them" — §6 of the contract already measured that they look like this — but "how much ELSE in
// the world looks the same", which is the only thing that decides whether a detector is usable.

using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Suggest;
using PgmStudio.Pgm;

var roots = Directory.GetDirectories("/media/sf_repos/CommunityMaps")
    .Concat(Directory.Exists("/workspace/overcastcommunity/publicmaps")
        ? Directory.GetDirectories("/workspace/overcastcommunity/publicmaps") : [])
    .ToList();

// The closed material vocabulary the contract names for destroyables, plus obsidian for cores.
var objectiveIds = new Dictionary<int, string>
{
    [49] = "obsidian", [133] = "emerald", [41] = "gold", [121] = "ender stone",
};
const int Lava = 10, StationaryLava = 11, Obsidian = 49;

int mapsWithObjectives = 0, declaredDestroyables = 0, declaredCores = 0;
int coreCandidates = 0, coreHits = 0;
var clusterSizes = new List<int>();
var trueSizes = new List<int>();
var falseBig = new SortedDictionary<int, int>();
var trueGap = new List<int>(); var falseGap = new List<int>();
var trueExposure = new List<int>(); var falseExposure = new List<int>();
var trueFeat = new List<(int Size,int Gap,int Exp)>(); var falseFeat = new List<(int Size,int Gap,int Exp)>();
int destroyableCandidates = 0, destroyableHits = 0, resolvedStructures = 0;

foreach (var mapDir in roots.SelectMany(Directory.GetDirectories).OrderBy(d => d, StringComparer.Ordinal))
{
    var xmlPath = Path.Combine(mapDir, "map.xml");
    var regionDir = Path.Combine(mapDir, "region");
    if (!File.Exists(xmlPath) || !Directory.Exists(regionDir)) continue;

    MapXml map;
    try { map = MapParser.Parse(xmlPath); }
    catch (UnsupportedMapException) { continue; }
    var objectives = map.Destroyables.Where(d => d.IsObjective).ToList();
    if (objectives.Count == 0 && map.Cores.Count == 0) continue;

    List<AnvilRegion.Chunk> chunks;
    try { chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks).ToList(); }
    catch { continue; }
    if (chunks.Count == 0) continue;

    mapsWithObjectives++;
    declaredDestroyables += objectives.Count;
    declaredCores += map.Cores.Count;
    var slug = Path.GetFileName(mapDir);

    // ── the world, as a sparse id lookup ─────────────────────────────────────
    var world = new Dictionary<(int, int, int), int>();
    foreach (var chunk in chunks)
        foreach (var block in AnvilRegion.Blocks(chunk))
            world[(block.X, block.Y, block.Z)] = block.Id;

    // ── ground truth: the declared boxes ─────────────────────────────────────
    var trueCoreBoxes = map.Cores
        .Select(core => RegionBoxes.Enclosing(map.Regions, core.RegionId))
        .OfType<BlockBox>().ToList();
    // Ground truth the way the contract measures a structure: the blocks INSIDE the declared region that match
    // its declared material. The region itself is a loose human box (OB12) and routinely encloses decoration,
    // so labelling by the box makes every nearby gold block a true objective and the measurement meaningless.
    var trueCells = new HashSet<(int, int, int)>();
    var trueStructures = new List<HashSet<(int, int, int)>>();
    foreach (var destroyable in objectives)
    {
        var ids = MaterialIds.Resolve(destroyable.Materials);
        if (ids.Count == 0) continue;
        var cells = new HashSet<(int, int, int)>();
        foreach (var box in RegionBoxes.Of(map.Regions, destroyable.RegionId))
            for (var x = box.MinX; x <= box.MaxX; x++)
                for (var y = box.MinY; y <= box.MaxY; y++)
                    for (var z = box.MinZ; z <= box.MaxZ; z++)
                        if (ids.Contains(world.GetValueOrDefault((x, y, z)))) cells.Add((x, y, z));
        if (cells.Count == 0) continue;
        trueStructures.Add(cells);
        foreach (var cell in cells) trueCells.Add(cell);
    }
    resolvedStructures += trueStructures.Count;

    static bool Touches(BlockBox box, (int X, int Y, int Z) cell) =>
        cell.X >= box.MinX - 1 && cell.X <= box.MaxX + 1
        && cell.Y >= box.MinY - 1 && cell.Y <= box.MaxY + 1
        && cell.Z >= box.MinZ - 1 && cell.Z <= box.MaxZ + 1;

    // ── core signature: the shipped detector, so the measurement cannot drift from what runs ──
    foreach (var suggestion in CoreSuggester.Gather(world))
    {
        coreCandidates++;
        var casing = suggestion.Casing;
        var hit = trueCoreBoxes.Any(box =>
            casing.MaxX >= box.MinX - 1 && casing.MinX <= box.MaxX + 1
            && casing.MaxY >= box.MinY - 1 && casing.MinY <= box.MaxY + 1
            && casing.MaxZ >= box.MinZ - 1 && casing.MinZ <= box.MaxZ + 1);
        if (hit) coreHits++;
        else Console.WriteLine($"  FP core   {slug,-26} lava={suggestion.LavaBlocks,4} shell={suggestion.Shell} at {casing}");
    }

    // ── destroyable signature: connected clusters of objective materials ─────
    var objectiveCells = world.Where(kv => objectiveIds.ContainsKey(kv.Value)).Select(kv => kv.Key).ToHashSet();
    var seen = new HashSet<(int, int, int)>();
    foreach (var start in objectiveCells)
    {
        if (!seen.Add(start)) continue;
        var blob = new List<(int X, int Y, int Z)> { start };
        var queue = new Queue<(int X, int Y, int Z)>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var (x, y, z) = queue.Dequeue();
            foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
            {
                var next = (x + dx, y + dy, z + dz);
                if (objectiveCells.Contains(next) && seen.Add(next)) { blob.Add(next); queue.Enqueue(next); }
            }
        }
        // a core's obsidian casing is not a destroyable — drop clusters that wrap lava
        if (blob.Any(cell => new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) }
                .Any(d => world.GetValueOrDefault((cell.X + d.Item1, cell.Y + d.Item2, cell.Z + d.Item3)) is Lava or StationaryLava)))
            continue;

        destroyableCandidates++;
        clusterSizes.Add(blob.Count);

        // FLOAT: the air gap directly beneath the cluster. Per footprint column, count consecutive air below
        // the cluster's lowest block there; the cluster's float is the SMALLEST such gap, because a structure
        // resting on anything is not floating.
        var footprint = blob.GroupBy(cell => (cell.X, cell.Z));
        var gap = int.MaxValue;
        foreach (var column in footprint)
        {
            var lowest = column.Min(cell => cell.Y);
            var air = 0;
            for (var y = lowest - 1; y >= 0 && air < 32; y--)
            {
                if (world.ContainsKey((column.Key.X, y, column.Key.Z))) break;
                air++;
            }
            gap = Math.Min(gap, air);
        }
        if (gap == int.MaxValue) gap = 0;

        // EXPOSURE: the fraction of the cluster's faces that open onto air — how isolated it is.
        int faces = 0, airFaces = 0;
        foreach (var cell in blob)
            foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
            {
                var next = (cell.X + dx, cell.Y + dy, cell.Z + dz);
                if (blob.Contains(next)) continue;
                faces++;
                if (!world.ContainsKey(next)) airFaces++;
            }
        var exposure = faces == 0 ? 0 : (int)Math.Round(100.0 * airFaces / faces);

        var isTrue = blob.Any(trueCells.Contains);
        if (isTrue)
        {
            destroyableHits++;
            trueSizes.Add(blob.Count);
            trueGap.Add(gap);
            trueExposure.Add(exposure);
            trueFeat.Add((blob.Count, gap, exposure));
        }
        else
        {
            falseBig[Math.Min(blob.Count, 200)] = falseBig.GetValueOrDefault(Math.Min(blob.Count, 200)) + 1;
            falseGap.Add(gap);
            falseExposure.Add(exposure);
            falseFeat.Add((blob.Count, gap, exposure));
        }
    }
}

Console.WriteLine();
Console.WriteLine($"maps with a declared objective + world: {mapsWithObjectives}");
Console.WriteLine($"declared: {declaredDestroyables} destroyable(s), {declaredCores} core(s)");
Console.WriteLine();
Console.WriteLine($"CORE signature (lava fully enclosed by obsidian):");
Console.WriteLine($"  candidates {coreCandidates}, of which touch a declared core: {coreHits}");
Console.WriteLine();
Console.WriteLine($"DESTROYABLE signature (connected cluster of obsidian/emerald/gold/ender stone, not a core casing):");
Console.WriteLine($"  candidates {destroyableCandidates}, overlapping a declared structure: {destroyableHits}");
Console.WriteLine($"  declared structures that resolved in-world: {resolvedStructures}");
if (trueSizes.Count > 0)
{
    trueSizes.Sort();
    Console.WriteLine($"  TRUE cluster size: min {trueSizes[0]}, median {trueSizes[trueSizes.Count / 2]}, "
                      + $"p90 {trueSizes[(int)(trueSizes.Count * 0.9)]}, max {trueSizes[^1]}");
}
Console.WriteLine("  FALSE cluster size histogram (size: count), capped at 200:");
foreach (var (size, count) in falseBig.Take(30)) Console.WriteLine($"    {size,4}: {count}");
Console.WriteLine($"  false clusters of size <= 8: {falseBig.Where(kv => kv.Key <= 8).Sum(kv => kv.Value)}");
Console.WriteLine($"  false clusters of size  > 8: {falseBig.Where(kv => kv.Key > 8).Sum(kv => kv.Value)}");

static void Distribution(string label, List<int> values)
{
    if (values.Count == 0) { Console.WriteLine($"  {label}: none"); return; }
    values.Sort();
    int Pct(double p) => values[Math.Min(values.Count - 1, (int)(values.Count * p))];
    Console.WriteLine($"  {label,-26} n={values.Count,6}  p10={Pct(0.1),3} p25={Pct(0.25),3} median={Pct(0.5),3} p75={Pct(0.75),3} p90={Pct(0.9),3}");
}

Console.WriteLine();
Console.WriteLine("FLOAT — air gap directly beneath the cluster (smallest across its footprint):");
Distribution("true destroyables", trueGap);
Distribution("false clusters", falseGap);
Console.WriteLine();
Console.WriteLine("EXPOSURE — % of cluster faces opening onto air:");
Distribution("true destroyables", trueExposure);
Distribution("false clusters", falseExposure);
Console.WriteLine();
{
    var survivingTrue = trueGap.Count(g => g >= 1);
    var survivingFalse = falseGap.Count(g => g >= 1);
    Console.WriteLine($"gate `float >= 1`:  true kept {survivingTrue}/{trueGap.Count}, false kept {survivingFalse}/{falseGap.Count}");
    var t2 = trueGap.Count(g => g >= 2); var f2 = falseGap.Count(g => g >= 2);
    Console.WriteLine($"gate `float >= 2`:  true kept {t2}/{trueGap.Count}, false kept {f2}/{falseGap.Count}");
    var t3 = trueGap.Count(g => g >= 3); var f3 = falseGap.Count(g => g >= 3);
    Console.WriteLine($"gate `float >= 3`:  true kept {t3}/{trueGap.Count}, false kept {f3}/{falseGap.Count}");
}

Console.WriteLine();
Console.WriteLine("GATE GRID — recall of declared structures vs false positives kept:");
Console.WriteLine("  maxSize  minExp  |  true kept        false kept");
foreach (var maxSize in new[] { 8, 16, 32, 64, 128 })
    foreach (var minExp in new[] { 0, 25, 40, 55, 70 })
    {
        var t = trueFeat.Count(f => f.Size <= maxSize && f.Exp >= minExp);
        var f2 = falseFeat.Count(f => f.Size <= maxSize && f.Exp >= minExp);
        Console.WriteLine($"  {maxSize,7}  {minExp,6}  |  {t,5}/{trueFeat.Count,-5}      {f2,6}");
    }
