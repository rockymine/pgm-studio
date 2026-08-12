#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
// Ground truth for the tree-showcase world. A tree core is a connected lump of log/leaf/wool; the
// carpentry (planks, slabs, stairs) is attached afterwards and only above the trunk's own base, so the
// platform a tree stands on is never mistaken for part of it.
using PgmStudio.Minecraft;

var regionDir = args.Length > 0 ? args[0] : "tools/tree-corpus/tree-showcase/region";
var chunks = Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks).ToList();

var world = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
foreach (var chunk in chunks)
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
            {
                var id = ids[(y << 8) | col];
                if (id != 0) world[(chunk.ChunkX * 16 + lx, y, chunk.ChunkZ * 16 + lz)] =
                    (id, data[(y << 8) | col]);
            }
        }
}

static bool IsLog(int id) => id is 17 or 162;
static bool IsLeaf(int id) => id is 18 or 161;
static bool IsCarpentry(int id) => id is 5 or 126 or 53 or 134 or 135 or 136 or 163 or 164;

static string WoodSpecies(int id, int data) => id is 17 or 18
    ? (data & 3) switch { 0 => "oak", 1 => "spruce", 2 => "birch", _ => "jungle" }
    : (data & 1) == 0 ? "acacia" : "dark oak";
static string PlankSpecies(int data) => (data & 7) switch
{
    0 => "oak", 1 => "spruce", 2 => "birch", 3 => "jungle", 4 => "acacia", _ => "dark oak"
};
string[] WoolNames =
[
    "white", "orange", "magenta", "light blue", "yellow", "lime", "pink", "grey",
    "light grey", "cyan", "purple", "blue", "brown", "green", "red", "black"
];

// The platforms, found as what they are: broad flat sheets of one course. Planks are structural in
// these trees — several build their branches out of slabs — so carpentry cannot simply be excluded from
// the clustering; the floor it would fuse a tree to has to be named instead.
var platform = new HashSet<(int X, int Y, int Z)>();
foreach (var course in world.Keys.Where(c => world[c].Id is 5 or 2 or 3).GroupBy(c => c.Y))
{
    var pending = course.ToHashSet();
    while (pending.Count > 0)
    {
        var seed = pending.First();
        pending.Remove(seed);
        var sheet = new List<(int X, int Y, int Z)> { seed };
        var queue = new Queue<(int X, int Y, int Z)>([seed]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var step in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                if (pending.Remove((cell.X + step.Item1, cell.Y, cell.Z + step.Item2)))
                {
                    sheet.Add((cell.X + step.Item1, cell.Y, cell.Z + step.Item2));
                    queue.Enqueue((cell.X + step.Item1, cell.Y, cell.Z + step.Item2));
                }
        }
        if (sheet.Count >= 100) foreach (var cell in sheet) platform.Add(cell);
    }
}
Console.WriteLine($"platform sheets: {platform.Count} blocks over " +
    $"{platform.GroupBy(c => c.Y).Count()} course(s) — {string.Join(", ", platform.GroupBy(c => c.Y).OrderBy(g => g.Key).Select(g => $"y{g.Key} {g.Count()}"))}");

var core = world.Keys.Where(c => !platform.Contains(c) &&
    (IsLog(world[c].Id) || IsLeaf(world[c].Id) || world[c].Id == 35 || IsCarpentry(world[c].Id))).ToHashSet();
var seen = new HashSet<(int X, int Y, int Z)>();
var clusters = new List<List<(int X, int Y, int Z)>>();
foreach (var seed in core)
{
    if (!seen.Add(seed)) continue;
    var cluster = new List<(int X, int Y, int Z)>();
    var queue = new Queue<(int X, int Y, int Z)>([seed]);
    while (queue.Count > 0)
    {
        var cell = queue.Dequeue();
        cluster.Add(cell);
        for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                    if (core.Contains((cell.X + dx, cell.Y + dy, cell.Z + dz))
                        && seen.Add((cell.X + dx, cell.Y + dy, cell.Z + dz)))
                        queue.Enqueue((cell.X + dx, cell.Y + dy, cell.Z + dz));
    }
    clusters.Add(cluster);
}

// A cluster is a tree when it has a trunk or is built of wool; anything else is a loose leaf or two.
var trees = clusters.Where(c => c.Any(p => IsLog(world[p].Id) || world[p].Id == 35)).ToList();
var strays = clusters.Except(trees).ToList();
trees = [.. trees.OrderBy(c => c.Min(p => p.Z) / 30).ThenBy(c => c.Min(p => p.X))];

Console.WriteLine($"tree bodies {trees.Count}; loose leaf fragments {strays.Count} " +
    $"({strays.Sum(c => c.Count)} blocks)");

Console.WriteLine($"\n{"#",3} {"row",4} {"x",10} {"z",10} {"h",4} {"logs",5} {"leaf",5}  {"trunk wood",-22} {"canopy",-34} carpentry");
var index = 0;
var rowKeys = trees.Select(c => c.Min(p => p.Z) / 30).Distinct().OrderBy(k => k).ToList();
foreach (var tree in trees)
{
    index++;
    var logs = tree.Where(p => IsLog(world[p].Id)).ToList();
    var leaves = tree.Where(p => IsLeaf(world[p].Id)).ToList();
    var wool = tree.Where(p => world[p].Id == 35).ToList();
    var baseY = logs.Count > 0 ? logs.Min(p => p.Y) : tree.Min(p => p.Y);
    var wood = tree.Where(p => IsCarpentry(world[p].Id)).ToList();

    string Tally(IEnumerable<string> names) => string.Join(", ", names.GroupBy(n => n)
        .OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"));

    var trunkText = wool.Count > 0 && logs.Count == 0
        ? Tally(wool.Select(p => WoolNames[world[p].Data & 15]))
        : Tally(logs.Select(p => WoodSpecies(world[p].Id, world[p].Data)));
    var barkOff = logs.Count(p => (world[p].Data >> 2 & 3) != 3);
    var canopyText = leaves.Count == 0 ? "—" : Tally(leaves.Select(p => WoodSpecies(world[p].Id, world[p].Data)));
    var carpentryText = wood.Count == 0 ? "" : Tally(wood.Select(p =>
        $"{PlankSpecies(world[p].Data)} {(world[p].Id == 5 ? "plank" : world[p].Id == 126 ? "slab" : "stair")}"));

    Console.WriteLine($"{index,3} {rowKeys.IndexOf(tree.Min(p => p.Z) / 30) + 1,4} " +
        $"{$"{tree.Min(p => p.X)}..{tree.Max(p => p.X)}",10} {$"{tree.Min(p => p.Z)}..{tree.Max(p => p.Z)}",10} " +
        $"{tree.Max(p => p.Y) - baseY + 1,4} {logs.Count,5} {leaves.Count,5}  " +
        $"{trunkText + (barkOff > 0 ? $" [{barkOff} not all-bark]" : ""),-22} {canopyText,-34} {carpentryText}");
}

Console.WriteLine($"\n=== rows ===");
foreach (var row in trees.GroupBy(c => c.Min(p => p.Z) / 30).OrderBy(g => g.Key))
    Console.WriteLine($"  row {rowKeys.IndexOf(row.Key) + 1,2}: {row.Count(),2} trees, z {row.Min(c => c.Min(p => p.Z))}..{row.Max(c => c.Max(p => p.Z))}");

Console.WriteLine($"\n=== species pairing (trunk wood → canopy leaf) ===");
foreach (var group in trees.Where(t => t.Any(p => IsLog(world[p].Id))).GroupBy(t =>
{
    var logs = t.Where(p => IsLog(world[p].Id)).ToList();
    var leaves = t.Where(p => IsLeaf(world[p].Id)).ToList();
    var trunk = logs.GroupBy(p => WoodSpecies(world[p].Id, world[p].Data))
        .OrderByDescending(g => g.Count()).First().Key;
    var canopy = leaves.Count == 0 ? "none" : leaves.GroupBy(p => WoodSpecies(world[p].Id, world[p].Data))
        .OrderByDescending(g => g.Count()).First().Key;
    var mixed = leaves.Select(p => WoodSpecies(world[p].Id, world[p].Data)).Distinct().Count() > 1;
    return (trunk, canopy, mixed);
}).OrderByDescending(g => g.Count()))
    Console.WriteLine($"  {group.Key.trunk,-10} → {group.Key.canopy,-10} {group.Count(),3} trees" +
        (group.Key.mixed ? "  (canopy mixes more than one leaf type)" : ""));

// ── Why the flora tool sees fewer: replay its stem test tree by tree ────────────────────────────────
bool AllBark((int Id, int Data) block) => (block.Data >> 2 & 3) == 3;
Console.WriteLine("\n=== flora's stem test, replayed per tree ===");
var missed = new List<int>();
var stemTally = new List<int>();
index = 0;
foreach (var tree in trees)
{
    index++;
    var logs = tree.Where(p => IsLog(world[p].Id)).ToHashSet();
    var stems = 0;
    var footings = 0;
    var brokenBySpecies = 0;
    var tooShort = 0;
    foreach (var cell in logs)
    {
        var under = world.GetValueOrDefault((cell.X, cell.Y - 1, cell.Z), (0, 0));
        if (under.Item1 == 0 || IsLog(under.Item1) || IsLeaf(under.Item1)) continue;
        footings++;
        if (!AllBark(world[cell])) continue;
        var species = WoodSpecies(world[cell].Id, world[cell].Data);
        var run = 0;
        while (world.TryGetValue((cell.X, cell.Y + run, cell.Z), out var above) && IsLog(above.Id)
               && AllBark(above) && WoodSpecies(above.Id, above.Data) == species) run++;
        var wholeColumn = 0;
        while (world.TryGetValue((cell.X, cell.Y + wholeColumn, cell.Z), out var above) && IsLog(above.Id))
            wholeColumn++;
        if (run >= 3) stems++;
        else if (wholeColumn >= 3) brokenBySpecies++;
        else tooShort++;
    }
    stemTally.Add(stems);
    if (stems == 0)
    {
        missed.Add(index);
        Console.WriteLine($"  tree {index,2} (row {rowKeys.IndexOf(tree.Min(p => p.Z) / 30) + 1}, " +
            $"x {tree.Min(p => p.X)}..{tree.Max(p => p.X)}): {logs.Count} logs, {footings} rest on something, " +
            $"{brokenBySpecies} of those top a 3+ log column that changes species, {tooShort} shorter than 3");
    }
}
Console.WriteLine($"\n  trees flora's stem test can reach: {trees.Count - missed.Count} of {trees.Count}" +
    $" ({stemTally.Sum()} stems total)");
Console.WriteLine($"  trees it cannot: {missed.Count} — {string.Join(", ", missed)}");

// What a tree of a given height costs in blocks. The finding this answers is that it costs almost the same
// whatever its height: an author adds crown as a tree grows, not timber, where a generator that scales its
// trunk radius and its branch count with height spends six times the wood on a tall one.
Console.WriteLine("\n=== wood and foliage by height ===");
Console.WriteLine($"  {"height",-10} {"trees",6} {"wood",7} {"leaves",8} {"leaf/wood",10}");
foreach (var band in new (string Name, int Low, int High)[]
         { ("5-9", 5, 9), ("10-13", 10, 13), ("14-17", 14, 17), ("18-23", 18, 23), ("24 or more", 24, 999) })
{
    var inBand = trees.Select(tree => (
            Height: tree.Max(p => p.Y) - (tree.Any(p => IsLog(world[p].Id))
                ? tree.Where(p => IsLog(world[p].Id)).Min(p => p.Y) : tree.Min(p => p.Y)) + 1,
            Wood: tree.Count(p => IsLog(world[p].Id) || world[p].Id == 35),
            Leaves: tree.Count(p => IsLeaf(world[p].Id))))
        .Where(t => t.Height >= band.Low && t.Height <= band.High && t.Leaves > 0).ToList();
    if (inBand.Count == 0) continue;
    Console.WriteLine($"  {band.Name,-10} {inBand.Count,6} {inBand.Average(t => t.Wood),7:0} " +
        $"{inBand.Average(t => t.Leaves),8:0} {inBand.Sum(t => t.Leaves) / (double)inBand.Sum(t => t.Wood),10:0.0}");
}

// How straight the trunks are: the tallest stack of logs at any one (x,z), rooted or not.
Console.WriteLine("\n=== trunk straightness (tallest log column anywhere in the tree) ===");
var straight = new List<(int Tree, int Row, int Tallest, int Height, bool Grounded)>();
index = 0;
foreach (var tree in trees)
{
    index++;
    var logs = tree.Where(p => IsLog(world[p].Id)).ToHashSet();
    if (logs.Count == 0) continue;
    var tallest = logs.Max(cell =>
    {
        if (logs.Contains((cell.X, cell.Y - 1, cell.Z))) return 0;
        var run = 0;
        while (logs.Contains((cell.X, cell.Y + run, cell.Z))) run++;
        return run;
    });
    var grounded = logs.Any(cell => world.TryGetValue((cell.X, cell.Y - 1, cell.Z), out var under)
        && !IsLog(under.Id) && !IsLeaf(under.Id));
    straight.Add((index, rowKeys.IndexOf(tree.Min(p => p.Z) / 30) + 1, tallest,
        tree.Max(p => p.Y) - logs.Min(p => p.Y) + 1, grounded));
}
foreach (var band in straight.GroupBy(t => t.Tallest <= 2 ? "1-2 (fully leaning)"
             : t.Tallest <= 4 ? "3-4" : t.Tallest <= 7 ? "5-7" : "8+ (a straight bole)")
             .OrderBy(g => g.Key))
    Console.WriteLine($"  tallest column {band.Key,-22} {band.Count(),3} trees  " +
        $"(median tree height {band.OrderBy(t => t.Height).ElementAt(band.Count() / 2).Height})");
Console.WriteLine($"  trees with no log resting on anything (hovering): " +
    $"{straight.Count(t => !t.Grounded)} — {string.Join(", ", straight.Where(t => !t.Grounded).Select(t => $"#{t.Tree}(row {t.Row})"))}");
