#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
// How a leaf is held. For every leaf block the 3x3x3 neighbourhood is read and the leaf is filed by its
// strongest contact — face, edge or corner, against wood or against another leaf — and then by whether a
// chain of leaves reaches wood at all. Run over a hand-built world and a generated one, the two profiles
// are directly comparable.
using PgmStudio.Minecraft;

var regionDir = args[0];
var withCarpentry = args.Contains("--carpentry");
var label = Array.IndexOf(args, "--label") is var at && at >= 0 ? args[at + 1] : Path.GetFileName(Path.GetDirectoryName(regionDir)!);

var world = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
foreach (var chunk in Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks))
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
            for (var y = 0; y < 256; y++)
                if (ids[(y << 8) | col] != 0)
                    world[(chunk.ChunkX * 16 + lx, y, chunk.ChunkZ * 16 + lz)] =
                        (ids[(y << 8) | col], data[(y << 8) | col]);
        }
}

static bool IsLog(int id) => id is 17 or 162;
static bool IsLeaf(int id) => id is 18 or 161;
// Wood a leaf may legitimately hang from: the log itself, and the carpentry some authors branch with.
static bool IsCarpentry(int id) => id is 5 or 126 or 53 or 134 or 135 or 136 or 163 or 164;

var leaves = world.Keys.Where(c => IsLeaf(world[c].Id)).ToHashSet();
var wood = world.Keys.Where(c => IsLog(world[c].Id) || (withCarpentry && IsCarpentry(world[c].Id))).ToHashSet();
Console.WriteLine($"╔══ {label} ══╗");
Console.WriteLine($"  {leaves.Count} leaves, {world.Keys.Count(c => IsLog(world[c].Id))} logs" +
    (withCarpentry ? $", {world.Keys.Count(c => IsCarpentry(world[c].Id))} carpentry blocks counted as wood" : ""));

// ── the ladder: face (one axis), edge (two), corner (three) ────────────────────────────────────────
static int Kind(int dx, int dy, int dz) => Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz);   // 1 face, 2 edge, 3 corner
string[] tierName = ["—", "face", "edge", "corner"];

var ladder = new Dictionary<string, int>();
var cross = new Dictionary<(int Wood, int Leaf), int>();
var neighbourCount = new List<int>();

foreach (var leaf in leaves)
{
    int bestWood = 4, bestLeaf = 4, bestSolid = 4, touching = 0;
    for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0 && dz == 0) continue;
                var cell = (leaf.X + dx, leaf.Y + dy, leaf.Z + dz);
                if (!world.TryGetValue(cell, out var block)) continue;
                var kind = Kind(dx, dy, dz);
                if (wood.Contains(cell)) { bestWood = Math.Min(bestWood, kind); touching++; }
                else if (IsLeaf(block.Id)) { bestLeaf = Math.Min(bestLeaf, kind); touching++; }
                else { bestSolid = Math.Min(bestSolid, kind); touching++; }
            }
    neighbourCount.Add(touching);
    cross[(bestWood, bestLeaf)] = cross.GetValueOrDefault((bestWood, bestLeaf)) + 1;

    var tier = bestWood < 4 ? $"{tierName[bestWood]}-to-wood"
             : bestLeaf < 4 ? $"{tierName[bestLeaf]}-to-leaf"
             : bestSolid < 4 ? "only some other block" : "nothing at all (floating)";
    ladder[tier] = ladder.GetValueOrDefault(tier) + 1;
}

Console.WriteLine("\n  ── strongest contact in the 3x3x3 neighbourhood ──");
string[] order = ["face-to-wood", "edge-to-wood", "corner-to-wood", "face-to-leaf", "edge-to-leaf",
                  "corner-to-leaf", "only some other block", "nothing at all (floating)"];
foreach (var tier in order.Where(ladder.ContainsKey))
    Console.WriteLine($"    {tier,-28} {ladder[tier],7}  {ladder[tier] * 100.0 / leaves.Count,5:0.0}%");

Console.WriteLine("\n  ── how many of the 26 neighbours are occupied ──");
neighbourCount.Sort();
Console.WriteLine($"    min {neighbourCount[0]}, median {neighbourCount[neighbourCount.Count / 2]}, " +
    $"mean {neighbourCount.Average():0.0}, max {neighbourCount[^1]}");
foreach (var band in new[] { (0, 0), (1, 3), (4, 7), (8, 12), (13, 18), (19, 26) })
{
    var hits = neighbourCount.Count(n => n >= band.Item1 && n <= band.Item2);
    Console.WriteLine($"    {band.Item1,2}..{band.Item2,-2} neighbours  {hits,7}  {hits * 100.0 / leaves.Count,5:0.0}%  " +
        new string('█', (int)(hits * 60.0 / leaves.Count)));
}

// ── does a chain of leaves reach wood at all ───────────────────────────────────────────────────────
// Corner steps count: an author who builds diagonally still means the crown to hang together, and the
// question here is whether the foliage is attached at all, not whether vanilla would keep it.
var distance = new Dictionary<(int X, int Y, int Z), int>();
var frontier = new Queue<(int X, int Y, int Z)>();
foreach (var leaf in leaves)
{
    var against = false;
    for (var dy = -1; dy <= 1 && !against; dy++)
        for (var dz = -1; dz <= 1 && !against; dz++)
            for (var dx = -1; dx <= 1 && !against; dx++)
                if ((dx != 0 || dy != 0 || dz != 0) && wood.Contains((leaf.X + dx, leaf.Y + dy, leaf.Z + dz)))
                    against = true;
    if (against) { distance[leaf] = 1; frontier.Enqueue(leaf); }
}
while (frontier.Count > 0)
{
    var cell = frontier.Dequeue();
    for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var next = (cell.X + dx, cell.Y + dy, cell.Z + dz);
                if (leaves.Contains(next) && !distance.ContainsKey(next))
                {
                    distance[next] = distance[cell] + 1;
                    frontier.Enqueue(next);
                }
            }
}

var unreached = leaves.Where(l => !distance.ContainsKey(l)).ToHashSet();
Console.WriteLine($"\n  ── leaves that reach wood through a chain of leaves ──");
Console.WriteLine($"    reach it:      {distance.Count,7}  {distance.Count * 100.0 / leaves.Count,5:0.00}%");
Console.WriteLine($"    never reach it:{unreached.Count,7}  {unreached.Count * 100.0 / leaves.Count,5:0.00}%");
if (distance.Count > 0)
{
    var hops = distance.Values.OrderBy(v => v).ToList();
    Console.WriteLine($"    leaf-steps from wood: median {hops[hops.Count / 2]}, " +
        $"90th {hops[(int)(hops.Count * 0.9)]}, max {hops[^1]}");
}

// The stranded foliage, grouped into the blobs it forms, since one drifting island reads very
// differently from a thousand single leaves.
var pending = new HashSet<(int X, int Y, int Z)>(unreached);
var blobs = new List<int>();
while (pending.Count > 0)
{
    var seed = pending.First();
    pending.Remove(seed);
    var size = 0;
    var queue = new Queue<(int X, int Y, int Z)>([seed]);
    while (queue.Count > 0)
    {
        var cell = queue.Dequeue();
        size++;
        for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                    if (pending.Remove((cell.X + dx, cell.Y + dy, cell.Z + dz)))
                        queue.Enqueue((cell.X + dx, cell.Y + dy, cell.Z + dz));
    }
    blobs.Add(size);
}
blobs.Sort();
if (blobs.Count > 0)
    Console.WriteLine($"    stranded in {blobs.Count} separate blob(s): " +
        $"median {blobs[blobs.Count / 2]} blocks, largest {blobs[^1]}, " +
        $"{blobs.Count(b => b == 1)} of them a single leaf alone in the air");

// ── what vanilla would keep: face steps only, four of them, from a log ──────────────────────────────
var decayLive = new HashSet<(int X, int Y, int Z)>();
var decayFrontier = new Queue<((int X, int Y, int Z) Cell, int Step)>();
foreach (var leaf in leaves)
    foreach (var step in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
        if (world.TryGetValue((leaf.X + step.Item1, leaf.Y + step.Item2, leaf.Z + step.Item3), out var b)
            && IsLog(b.Id) && decayLive.Add(leaf))
        { decayFrontier.Enqueue((leaf, 1)); break; }
while (decayFrontier.Count > 0)
{
    var (cell, step) = decayFrontier.Dequeue();
    if (step >= 4) continue;
    foreach (var move in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
    {
        var next = (cell.X + move.Item1, cell.Y + move.Item2, cell.Z + move.Item3);
        if (leaves.Contains(next) && decayLive.Add(next)) decayFrontier.Enqueue((next, step + 1));
    }
}
Console.WriteLine($"\n  ── vanilla decay check (face steps only, within 4 of a log) ──");
Console.WriteLine($"    would survive: {decayLive.Count,7}  {decayLive.Count * 100.0 / leaves.Count,5:0.0}%   " +
    $"(the rest are held up only by the no-decay flag)");
