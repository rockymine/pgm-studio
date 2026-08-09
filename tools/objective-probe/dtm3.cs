#:project /home/user/pgm-studio/src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
#:project /home/user/pgm-studio/src/PgmStudio.Pgm/PgmStudio.Pgm.csproj

// Ranking, not gating. A confirm-in-UI flow does not need a candidate set that is mostly right; it needs the
// right ones at the top. This measures recall@N per map for a score built from what the taxonomy showed
// actually separates, with symmetry pairing found WITHOUT the XML — the centre is voted for by the candidates
// themselves, so nothing here knows where the objectives are.

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

int maps = 0, totalTrue = 0;
int inTop2 = 0, inTop5 = 0, inTop10 = 0, anywhere = 0;
var candidateCounts = new List<int>();
var ranks = new List<int>();

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

    // ground truth: the declared structure cell sets
    var truth = new List<HashSet<(int X, int Y, int Z)>>();
    foreach (var destroyable in objectives)
    {
        var ids = MaterialIds.Resolve(destroyable.Materials);
        if (ids.Count == 0) continue;
        var cells = new HashSet<(int X, int Y, int Z)>();
        foreach (var box in RegionBoxes.Of(map.Regions, destroyable.RegionId))
            for (var x = box.MinX; x <= box.MaxX; x++)
                for (var y = box.MinY; y <= box.MaxY; y++)
                    for (var z = box.MinZ; z <= box.MaxZ; z++)
                        if (ids.Contains(world.GetValueOrDefault((x, y, z)))) cells.Add((x, y, z));
        if (cells.Count > 0) truth.Add(cells);
    }
    if (truth.Count == 0) continue;
    maps++;

    // ── candidates: per-material clusters that are not buried and not huge ───
    var candidates = new List<(HashSet<(int X, int Y, int Z)> Cells, string Material, int Size,
                               int AirPct, bool Fluid, double Cx, double Cz, int MaxDim)>();
    foreach (var (materialId, materialName) in materials)
    {
        var cells = world.Where(entry => entry.Value == materialId).Select(entry => entry.Key).ToHashSet();
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
            if (blob.Count > 128) continue;                       // a goal is small; terrain is not

            int airFaces = 0, solidFaces = 0;
            var fluid = false;
            foreach (var cell in blob)
                foreach (var face in faces)
                {
                    var next = (cell.X + face.X, cell.Y + face.Y, cell.Z + face.Z);
                    if (blob.Contains(next)) continue;
                    var id = world.GetValueOrDefault(next);
                    if (id == 0) airFaces++;
                    else { solidFaces++; if (fluids.Contains(id)) fluid = true; }
                }
            if (airFaces == 0) continue;                          // fully buried: never a goal

            int maxDim = Math.Max(blob.Max(c => c.X) - blob.Min(c => c.X),
                         Math.Max(blob.Max(c => c.Y) - blob.Min(c => c.Y),
                                  blob.Max(c => c.Z) - blob.Min(c => c.Z))) + 1;
            candidates.Add((blob, materialName, blob.Count,
                            100 * airFaces / (airFaces + solidFaces), fluid,
                            blob.Average(c => (double)c.X), blob.Average(c => (double)c.Z), maxDim));
        }
    }
    if (candidates.Count == 0) continue;
    candidateCounts.Add(candidates.Count);

    // ── the symmetry centre, voted for by the candidates themselves ─────────
    // Every same-material same-size pair proposes the midpoint it would be symmetric about; the true centre
    // is the midpoint the most pairs agree on. No XML is consulted.
    var votes = new Dictionary<(int X, int Z), int>();
    for (var i = 0; i < candidates.Count; i++)
        for (var j = i + 1; j < candidates.Count; j++)
        {
            if (candidates[i].Material != candidates[j].Material) continue;
            if (Math.Abs(candidates[i].Size - candidates[j].Size) > 1) continue;
            var key = ((int)Math.Round(candidates[i].Cx + candidates[j].Cx),
                       (int)Math.Round(candidates[i].Cz + candidates[j].Cz));   // 2× the midpoint, kept integral
            votes[key] = votes.GetValueOrDefault(key) + 1;
        }
    var centre = votes.Count == 0 ? (0, 0) : votes.OrderByDescending(kv => kv.Value).First().Key;

    // ── score and rank ──────────────────────────────────────────────────────
    // Material prior from the measured true:false ratio — emerald is nearly 1:1, obsidian nearly 1:22.
    static double Prior(string material) => material switch
    {
        "emerald" => 3.0, "gold" => 1.5, "endstone" => 1.0, _ => 0.8,
    };
    var scored = candidates.Select(candidate =>
    {
        var paired = candidates.Any(other =>
            !ReferenceEquals(other.Cells, candidate.Cells)
            && other.Material == candidate.Material
            && Math.Abs(other.Size - candidate.Size) <= 1
            && Math.Abs(other.Cx + candidate.Cx - centre.Item1) < 2
            && Math.Abs(other.Cz + candidate.Cz - centre.Item2) < 2);
        var score = Prior(candidate.Material)
                  + (paired ? 4.0 : 0)
                  + candidate.AirPct / 50.0
                  + (candidate.Fluid ? -1.0 : 0)
                  + (candidate.MaxDim <= 5 ? 0.5 : 0);
        return (candidate, score, paired);
    })
    .OrderByDescending(entry => entry.score)
    .ToList();

    foreach (var structure in truth)
    {
        totalTrue++;
        var rank = scored.FindIndex(entry => entry.candidate.Cells.Overlaps(structure));
        if (rank < 0) continue;
        anywhere++;
        ranks.Add(rank + 1);
        if (rank < 2) inTop2++;
        if (rank < 5) inTop5++;
        if (rank < 10) inTop10++;
    }
}

candidateCounts.Sort();
ranks.Sort();
Console.WriteLine($"maps {maps}, declared structures {totalTrue}");
Console.WriteLine($"candidates per map: median {candidateCounts[candidateCounts.Count / 2]}, "
                  + $"p90 {candidateCounts[(int)(candidateCounts.Count * .9)]}, max {candidateCounts[^1]}");
Console.WriteLine();
Console.WriteLine($"  present as a candidate at all : {anywhere,5} / {totalTrue}  ({100.0 * anywhere / totalTrue:F1}%)");
Console.WriteLine($"  ranked in the map's top 2     : {inTop2,5} / {totalTrue}  ({100.0 * inTop2 / totalTrue:F1}%)");
Console.WriteLine($"  ranked in the map's top 5     : {inTop5,5} / {totalTrue}  ({100.0 * inTop5 / totalTrue:F1}%)");
Console.WriteLine($"  ranked in the map's top 10    : {inTop10,5} / {totalTrue}  ({100.0 * inTop10 / totalTrue:F1}%)");
if (ranks.Count > 0)
    Console.WriteLine($"  rank of a found structure: median {ranks[ranks.Count / 2]}, p90 {ranks[(int)(ranks.Count * .9)]}");
