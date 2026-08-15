#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj

// The decisive question for DTM detection: is a declared destroyable a STANDALONE connected mass, or is it
// fused into a larger structure of the same material?
//
// A detector sees connected clusters. If the declared blocks are the whole cluster, the structure is
// detectable in principle. If the cluster continues into a wall, a pedestal or a build, no local rule can
// carve the objective back out of it — that is a ceiling on recall, not a threshold to tune.
//
// This also resolves DT3's "destroyables float 3-5 blocks" against the measured median of 0, by measuring
// float on the DECLARED cells rather than on whatever cluster contains them.

using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Pgm;

var roots = Directory.GetDirectories("/media/sf_repos/CommunityMaps")
    .Concat(Directory.Exists("/workspace/overcastcommunity/publicmaps")
        ? Directory.GetDirectories("/workspace/overcastcommunity/publicmaps") : [])
    .ToList();
var faces = new (int X, int Y, int Z)[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };

var standalone = new List<int>();          // structure size where cluster == structure
var fusedRatio = new List<int>();          // cluster/structure size % where it is bigger
var structFloat = new List<int>();
var structSize = new List<int>();
int structures = 0, exact = 0, fused = 0;
var byMaterial = new Dictionary<string, (int Exact, int Fused)>();
// symmetry: does the structure have a rot_180 partner of the same material and size?
int symChecked = 0, symPaired = 0;

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

    var world = new Dictionary<(int X, int Y, int Z), int>();
    foreach (var chunk in chunks)
        foreach (var block in AnvilRegion.Blocks(chunk))
            world[(block.X, block.Y, block.Z)] = block.Id;

    var centres = new List<((double X, double Z) C, string M, int Size)>();

    foreach (var destroyable in objectives)
    {
        var ids = MaterialIds.Resolve(destroyable.Materials);
        if (ids.Count != 1) continue;                       // one material is the common case; keep it clean
        var materialId = ids.Single();
        var name = materialId switch { 49 => "obsidian", 133 => "emerald", 41 => "gold", 121 => "endstone", _ => materialId.ToString() };

        var cells = new HashSet<(int X, int Y, int Z)>();
        foreach (var box in RegionBoxes.Of(map.Regions, destroyable.RegionId))
            for (var x = box.MinX; x <= box.MaxX; x++)
                for (var y = box.MinY; y <= box.MaxY; y++)
                    for (var z = box.MinZ; z <= box.MaxZ; z++)
                        if (world.GetValueOrDefault((x, y, z)) == materialId) cells.Add((x, y, z));
        if (cells.Count == 0) continue;
        structures++;

        // the connected cluster of that material containing the structure
        var cluster = new HashSet<(int X, int Y, int Z)>();
        var queue = new Queue<(int X, int Y, int Z)>(cells);
        foreach (var cell in cells) cluster.Add(cell);
        while (queue.Count > 0 && cluster.Count < 20000)
        {
            var cell = queue.Dequeue();
            foreach (var face in faces)
            {
                var next = (cell.X + face.X, cell.Y + face.Y, cell.Z + face.Z);
                if (world.GetValueOrDefault(next) == materialId && cluster.Add(next)) queue.Enqueue(next);
            }
        }

        var slot = byMaterial.GetValueOrDefault(name);
        if (cluster.Count == cells.Count)
        {
            exact++; standalone.Add(cells.Count);
            byMaterial[name] = (slot.Exact + 1, slot.Fused);
        }
        else
        {
            fused++; fusedRatio.Add((int)(100.0 * cluster.Count / cells.Count));
            byMaterial[name] = (slot.Exact, slot.Fused + 1);
        }

        // float, measured on the DECLARED structure
        var gap = int.MaxValue;
        foreach (var column in cells.GroupBy(cell => (cell.X, cell.Z)))
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
        structFloat.Add(gap == int.MaxValue ? 0 : gap);
        structSize.Add(cells.Count);
        centres.Add(((cells.Average(c => (double)c.X), cells.Average(c => (double)c.Z)), name, cells.Count));
    }

    // symmetry: every structure should have a partner mirrored about the objectives' own centre
    if (centres.Count >= 2)
    {
        var cx = centres.Average(c => c.C.X);
        var cz = centres.Average(c => c.C.Z);
        foreach (var (centre, material, size) in centres)
        {
            symChecked++;
            var mirrored = (2 * cx - centre.X, 2 * cz - centre.Z);
            if (centres.Any(other => other.M == material && Math.Abs(other.Size - size) <= 2
                                     && Math.Abs(other.C.X - mirrored.Item1) < 3
                                     && Math.Abs(other.C.Z - mirrored.Item2) < 3)) symPaired++;
        }
    }
}

static void Show(string label, List<int> values)
{
    if (values.Count == 0) { Console.WriteLine($"  {label,-20} none"); return; }
    values.Sort();
    int P(double p) => values[Math.Min(values.Count - 1, (int)(values.Count * p))];
    Console.WriteLine($"  {label,-20} n={values.Count,5}  p10={P(.1),4} median={P(.5),4} p75={P(.75),4} p90={P(.9),4} max={values[^1],6}");
}

Console.WriteLine($"declared structures resolved (single material): {structures}");
Console.WriteLine($"  STANDALONE (cluster == structure): {exact}  ({100.0 * exact / Math.Max(1, structures):F0}%)");
Console.WriteLine($"  FUSED into a larger mass:          {fused}  ({100.0 * fused / Math.Max(1, structures):F0}%)");
Console.WriteLine();
foreach (var (material, counts) in byMaterial.OrderByDescending(kv => kv.Value.Exact + kv.Value.Fused))
    Console.WriteLine($"    {material,-9} standalone {counts.Exact,4}   fused {counts.Fused,4}"
                      + $"   ({100.0 * counts.Exact / Math.Max(1, counts.Exact + counts.Fused):F0}% detectable)");
Console.WriteLine();
Show("standalone size", standalone);
Show("fused cluster/struct %", fusedRatio);
Console.WriteLine();
Console.WriteLine("FLOAT measured on the DECLARED structure (not the containing cluster):");
Show("float", structFloat);
Console.WriteLine($"  resting on something (float 0): {structFloat.Count(f => f == 0)} of {structFloat.Count}");
Console.WriteLine($"  floating 3-5 (DT3's claim):     {structFloat.Count(f => f >= 3 && f <= 5)} of {structFloat.Count}");
Console.WriteLine();
Console.WriteLine($"SYMMETRY: {symPaired} of {symChecked} declared structures have a same-material,");
Console.WriteLine($"          same-size partner at the rot_180 image of the objective set's centre.");
