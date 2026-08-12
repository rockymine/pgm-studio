#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
// The same connectivity reading, taken tree by tree, so a world's worst crowns can be named rather than
// averaged away.
using PgmStudio.Minecraft;

var regionDir = args[0];
var withCarpentry = args.Contains("--carpentry");
var byFamily = args.Contains("--families");
var label = Array.IndexOf(args, "--label") is var at && at >= 0 ? args[at + 1] : regionDir;

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
static bool IsCarpentry(int id) => id is 5 or 126 or 53 or 134 or 135 or 136 or 163 or 164;
static string Species(int id, int data) => id is 17 or 18
    ? (data & 3) switch { 0 => "oak", 1 => "spruce", 2 => "birch", _ => "jungle" }
    : (data & 1) == 0 ? "acacia" : "dark oak";

// Flat sheets of ground are named so a tree is never fused to the floor it stands on.
var platform = new HashSet<(int X, int Y, int Z)>();
if (withCarpentry)
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

var member = world.Keys.Where(c => !platform.Contains(c) && (IsLog(world[c].Id) || IsLeaf(world[c].Id)
    || (withCarpentry && IsCarpentry(world[c].Id)))).ToHashSet();
var seen = new HashSet<(int X, int Y, int Z)>();
var trees = new List<List<(int X, int Y, int Z)>>();
foreach (var seed in member)
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
                    if (member.Contains((cell.X + dx, cell.Y + dy, cell.Z + dz))
                        && seen.Add((cell.X + dx, cell.Y + dy, cell.Z + dz)))
                        queue.Enqueue((cell.X + dx, cell.Y + dy, cell.Z + dz));
    }
    // A body needs wood as well as foliage: a drifting blob of leaves is a defect to be counted,
    // not a tree whose crown can be scored.
    if (cluster.Any(c => IsLeaf(world[c].Id)) && cluster.Any(c => IsLog(world[c].Id))) trees.Add(cluster);
}

Console.WriteLine($"╔══ {label} — {trees.Count} bodies carrying foliage ══╗");

var rows = new List<(int Index, string Family, int Leaves, double WoodTouch, double Stranded,
                     int WorstBlob, int Blobs, double Neighbours, string Species)>();
var index = 0;
foreach (var tree in trees.OrderBy(t => t.Min(c => c.Z)).ThenBy(t => t.Min(c => c.X)))
{
    index++;
    var leaves = tree.Where(c => IsLeaf(world[c].Id)).ToHashSet();
    var wood = tree.Where(c => IsLog(world[c].Id) || (withCarpentry && IsCarpentry(world[c].Id))).ToHashSet();
    if (leaves.Count == 0) continue;

    var touching = 0;
    var neighbourTotal = 0;
    foreach (var leaf in leaves)
    {
        var anyWood = false;
        for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    var cell = (leaf.X + dx, leaf.Y + dy, leaf.Z + dz);
                    if (world.ContainsKey(cell)) neighbourTotal++;
                    if (wood.Contains(cell)) anyWood = true;
                }
        if (anyWood) touching++;
    }

    // Foliage that reaches wood through leaves, and the islands that do not.
    var reached = new HashSet<(int X, int Y, int Z)>();
    var frontier = new Queue<(int X, int Y, int Z)>();
    foreach (var leaf in leaves)
        for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                    if (wood.Contains((leaf.X + dx, leaf.Y + dy, leaf.Z + dz)) && reached.Add(leaf))
                        frontier.Enqueue(leaf);
    while (frontier.Count > 0)
    {
        var cell = frontier.Dequeue();
        for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                    if (leaves.Contains((cell.X + dx, cell.Y + dy, cell.Z + dz))
                        && reached.Add((cell.X + dx, cell.Y + dy, cell.Z + dz)))
                        frontier.Enqueue((cell.X + dx, cell.Y + dy, cell.Z + dz));
    }

    var loose = new HashSet<(int X, int Y, int Z)>(leaves.Except(reached));
    var blobSizes = new List<int>();
    while (loose.Count > 0)
    {
        var seed = loose.First();
        loose.Remove(seed);
        var size = 0;
        var queue = new Queue<(int X, int Y, int Z)>([seed]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            size++;
            for (var dy = -1; dy <= 1; dy++)
                for (var dz = -1; dz <= 1; dz++)
                    for (var dx = -1; dx <= 1; dx++)
                        if (loose.Remove((cell.X + dx, cell.Y + dy, cell.Z + dz)))
                            queue.Enqueue((cell.X + dx, cell.Y + dy, cell.Z + dz));
        }
        blobSizes.Add(size);
    }

    var kind = leaves.GroupBy(c => Species(world[c].Id, world[c].Data))
        .OrderByDescending(g => g.Count()).First().Key;
    rows.Add((index, $"z {tree.Min(c => c.Z) / 30}|{tree.Min(c => c.Z)}..{tree.Max(c => c.Z)}", leaves.Count, touching * 100.0 / leaves.Count,
        (leaves.Count - reached.Count) * 100.0 / leaves.Count,
        blobSizes.Count == 0 ? 0 : blobSizes.Max(), blobSizes.Count,
        neighbourTotal / (double)leaves.Count, kind));
}

Console.WriteLine($"\n{"#",4} {"leaves",7} {"touch wood",11} {"stranded",9} {"worst island",13} {"islands",8} {"neigh",6}  species");
foreach (var row in rows.OrderByDescending(r => r.Stranded).ThenByDescending(r => r.WorstBlob).Take(25))
    Console.WriteLine($"{row.Index,4} {row.Leaves,7} {row.WoodTouch,10:0.0}% {row.Stranded,8:0.0}% " +
        $"{row.WorstBlob,13} {row.Blobs,8} {row.Neighbours,6:0.0}  {row.Species}");

Console.WriteLine($"\n  trees with any stranded foliage: {rows.Count(r => r.Stranded > 0)} of {rows.Count} " +
    $"({rows.Count(r => r.Stranded > 0) * 100.0 / rows.Count:0}%)");
Console.WriteLine($"  trees with an island of 5+ blocks: {rows.Count(r => r.WorstBlob >= 5)}");
Console.WriteLine($"  leaves touching wood: median tree {rows.OrderBy(r => r.WoodTouch).ElementAt(rows.Count / 2).WoodTouch:0.0}%");
Console.WriteLine($"  occupied neighbours per leaf: median tree {rows.OrderBy(r => r.Neighbours).ElementAt(rows.Count / 2).Neighbours:0.0}");

if (byFamily)
{
    Console.WriteLine($"\n  ── by set (one 19x19 platform band = one family the author built) ──");
    Console.WriteLine($"  {"set",4} {"trees",6} {"leaves",8} {"touch wood",11} {"stranded",9} {"neigh",6}  species");
    var families = rows.GroupBy(r => r.Family.Split('|')[0]).OrderBy(g => int.Parse(g.Key.Split(' ')[1].Split('|')[0])).ToList();
    var number = 0;
    foreach (var family in families)
    {
        number++;
        Console.WriteLine($"  {number,4} {family.Count(),6} {family.Sum(r => r.Leaves),8} " +
            $"{family.Average(r => r.WoodTouch),10:0.0}% {family.Average(r => r.Stranded),8:0.00}% " +
            $"{family.Average(r => r.Neighbours),6:0.0}  " +
            $"{string.Join("/", family.Select(r => r.Species).Distinct()),-18} z {family.Min(r => int.Parse(r.Family.Split('|')[1].Split("..")[0]))}");
    }
}
