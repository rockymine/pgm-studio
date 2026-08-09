#:project /home/user/pgm-studio/src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
#:project /home/user/pgm-studio/src/PgmStudio.Pgm/PgmStudio.Pgm.csproj

// Why the destroyable false positives are not destroyables — a taxonomy, not a threshold search.
//
// Corrects the earlier probe's central mistake: it flooded clusters across obsidian/emerald/gold/ender stone
// POOLED, so a gold block touching obsidian terrain became one cluster and every incidental block in the
// vocabulary became a candidate. A destroyable is a connected mass of ONE material, so clustering is per
// material here.
//
// Ground truth stays the contract's: the blocks inside a declared region matching its declared material.

using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Pgm;

var roots = Directory.GetDirectories("/home/user/CommunityMaps")
    .Concat(Directory.Exists("/workspace/overcastcommunity/publicmaps")
        ? Directory.GetDirectories("/workspace/overcastcommunity/publicmaps") : [])
    .ToList();

var materials = new Dictionary<int, string> { [49] = "obsidian", [133] = "emerald", [41] = "gold", [121] = "endstone" };
var fluids = new HashSet<int> { 8, 9, 10, 11 };
var faces = new (int X, int Y, int Z)[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };

// One candidate cluster and everything measurable about it locally.
var rows = new List<(string Slug, string Material, bool True, int Size, int W, int H, int D,
                     double Fill, int AirFaces, int SolidFaces, bool Fluid, int Float, int MinY)>();
int maps = 0, declared = 0, resolved = 0;

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
    declared += objectives.Count;
    var slug = Path.GetFileName(mapDir);

    var world = new Dictionary<(int X, int Y, int Z), int>();
    foreach (var chunk in chunks)
        foreach (var block in AnvilRegion.Blocks(chunk))
            world[(block.X, block.Y, block.Z)] = block.Id;

    var trueCells = new HashSet<(int X, int Y, int Z)>();
    foreach (var destroyable in objectives)
    {
        var ids = MaterialIds.Resolve(destroyable.Materials);
        if (ids.Count == 0) continue;
        var found = false;
        foreach (var box in RegionBoxes.Of(map.Regions, destroyable.RegionId))
            for (var x = box.MinX; x <= box.MaxX; x++)
                for (var y = box.MinY; y <= box.MaxY; y++)
                    for (var z = box.MinZ; z <= box.MaxZ; z++)
                        if (ids.Contains(world.GetValueOrDefault((x, y, z)))) { trueCells.Add((x, y, z)); found = true; }
        if (found) resolved++;
    }

    // ── per-material connected clusters ──────────────────────────────────────
    foreach (var (materialId, materialName) in materials)
    {
        var cells = world.Where(entry => entry.Value == materialId).Select(entry => entry.Key).ToHashSet();
        var seen = new HashSet<(int X, int Y, int Z)>();
        foreach (var start in cells)
        {
            if (!seen.Add(start)) continue;
            var blob = new List<(int X, int Y, int Z)> { start };
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
            if (blob.Count > 4000) continue;   // terrain-scale masses: recorded as refused, not measured

            var blobSet = blob.ToHashSet();
            int minX = blob.Min(c => c.X), maxX = blob.Max(c => c.X);
            int minY = blob.Min(c => c.Y), maxY = blob.Max(c => c.Y);
            int minZ = blob.Min(c => c.Z), maxZ = blob.Max(c => c.Z);
            int width = maxX - minX + 1, height = maxY - minY + 1, depth = maxZ - minZ + 1;
            var fill = blob.Count / (double)(width * height * depth);

            int airFaces = 0, solidFaces = 0;
            var touchesFluid = false;
            foreach (var cell in blob)
                foreach (var face in faces)
                {
                    var next = (cell.X + face.X, cell.Y + face.Y, cell.Z + face.Z);
                    if (blobSet.Contains(next)) continue;
                    var id = world.GetValueOrDefault(next);
                    if (id == 0) airFaces++;
                    else { solidFaces++; if (fluids.Contains(id)) touchesFluid = true; }
                }

            var gap = int.MaxValue;
            foreach (var column in blob.GroupBy(cell => (cell.X, cell.Z)))
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

            rows.Add((slug, materialName, blob.Any(trueCells.Contains), blob.Count, width, height, depth,
                      fill, airFaces, solidFaces, touchesFluid, gap == int.MaxValue ? 0 : gap, minY));
        }
    }
}

var trueRows = rows.Where(r => r.True).ToList();
var falseRows = rows.Where(r => !r.True).ToList();

Console.WriteLine($"maps {maps}, declared destroyables {declared}, resolved in-world {resolved}");
Console.WriteLine($"clusters: {rows.Count}  (true {trueRows.Count}, false {falseRows.Count})");
Console.WriteLine();

Console.WriteLine("BY MATERIAL — true vs false clusters:");
foreach (var material in materials.Values)
{
    var t = trueRows.Count(r => r.Material == material);
    var f = falseRows.Count(r => r.Material == material);
    Console.WriteLine($"  {material,-9} true {t,5}   false {f,7}");
}

static void Show(string label, List<int> values)
{
    if (values.Count == 0) { Console.WriteLine($"    {label,-14} none"); return; }
    values.Sort();
    int P(double p) => values[Math.Min(values.Count - 1, (int)(values.Count * p))];
    Console.WriteLine($"    {label,-14} n={values.Count,6}  p10={P(.1),4} median={P(.5),4} p75={P(.75),4} p90={P(.9),4} p99={P(.99),5}");
}

Console.WriteLine();
Console.WriteLine("TRUE structures — what a destroyable actually looks like:");
Show("size", trueRows.Select(r => r.Size).ToList());
Show("bbox max dim", trueRows.Select(r => Math.Max(r.W, Math.Max(r.H, r.D))).ToList());
Show("fill %", trueRows.Select(r => (int)(r.Fill * 100)).ToList());
Show("air-face %", trueRows.Select(r => r.AirFaces + r.SolidFaces == 0 ? 0 : 100 * r.AirFaces / (r.AirFaces + r.SolidFaces)).ToList());
Show("float", trueRows.Select(r => r.Float).ToList());
Console.WriteLine($"    touching fluid: {trueRows.Count(r => r.Fluid)} of {trueRows.Count}");

Console.WriteLine();
Console.WriteLine("FALSE clusters — same measures:");
Show("size", falseRows.Select(r => r.Size).ToList());
Show("bbox max dim", falseRows.Select(r => Math.Max(r.W, Math.Max(r.H, r.D))).ToList());
Show("fill %", falseRows.Select(r => (int)(r.Fill * 100)).ToList());
Show("air-face %", falseRows.Select(r => r.AirFaces + r.SolidFaces == 0 ? 0 : 100 * r.AirFaces / (r.AirFaces + r.SolidFaces)).ToList());
Show("float", falseRows.Select(r => r.Float).ToList());
Console.WriteLine($"    touching fluid: {falseRows.Count(r => r.Fluid)} of {falseRows.Count}");

// ── the taxonomy: why is each false cluster not a destroyable? first matching reason wins ──
Console.WriteLine();
Console.WriteLine("FALSE-POSITIVE TAXONOMY (first reason that applies):");
var reasons = new Dictionary<string, int>();
var survivors = new List<(string Slug, string Material, int Size, double Fill, int AirPct)>();
foreach (var row in falseRows)
{
    var total = row.AirFaces + row.SolidFaces;
    var airPct = total == 0 ? 0 : 100 * row.AirFaces / total;
    var maxDim = Math.Max(row.W, Math.Max(row.H, row.D));
    string reason =
        row.Size > 64 ? "too large (>64 blocks)"
        : maxDim > 8 ? "too sprawling (bbox dim >8)"
        : airPct == 0 ? "fully buried (no air face at all)"
        : row.Fluid ? "submerged / touching fluid"
        : airPct < 25 ? "embedded in terrain (<25% air faces)"
        : row.Fill < 0.5 ? "not solid (fill <50%)"
        : "survives every gate";
    reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
    if (reason == "survives every gate") survivors.Add((row.Slug, row.Material, row.Size, row.Fill, airPct));
}
foreach (var (reason, count) in reasons.OrderByDescending(kv => kv.Value))
    Console.WriteLine($"  {count,7}  {reason}");

// the same gates applied to the true structures — what would be lost
var keptTrue = trueRows.Count(row =>
{
    var total = row.AirFaces + row.SolidFaces;
    var airPct = total == 0 ? 0 : 100 * row.AirFaces / total;
    var maxDim = Math.Max(row.W, Math.Max(row.H, row.D));
    return row.Size <= 64 && maxDim <= 8 && airPct >= 25 && !row.Fluid && row.Fill >= 0.5;
});
Console.WriteLine();
Console.WriteLine($"GATES APPLIED:  true kept {keptTrue}/{trueRows.Count}   false kept {survivors.Count}/{falseRows.Count}");
Console.WriteLine("  surviving false positives by map (top 15):");
foreach (var group in survivors.GroupBy(s => s.Slug).OrderByDescending(g => g.Count()).Take(15))
    Console.WriteLine($"    {group.Count(),5}  {group.Key} ({string.Join(",", group.Select(s => s.Material).Distinct())})");
