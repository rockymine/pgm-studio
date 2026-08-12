#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
// The silhouette of a crown, course by course: how much foliage stands at each height and how far it
// reaches from the stem. A conifer widens downward in steps; a flat-crowned acacia carries everything in a
// plate at the top. Both are visible in the per-course profile and in nothing measured before it.
using PgmStudio.Minecraft;

var regionDir = args.Length > 0 ? args[0] : "tools/tree-corpus/tree-showcase/region";
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

var platform = new HashSet<(int X, int Y, int Z)>();
foreach (var course in world.Keys.Where(c => world[c].Id is 5 or 2 or 3).GroupBy(c => c.Y))
{
    var pendingCourse = course.ToHashSet();
    while (pendingCourse.Count > 0)
    {
        var seed = pendingCourse.First();
        pendingCourse.Remove(seed);
        var sheet = new List<(int X, int Y, int Z)> { seed };
        var queue = new Queue<(int X, int Y, int Z)>([seed]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var step in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                if (pendingCourse.Remove((cell.X + step.Item1, cell.Y, cell.Z + step.Item2)))
                {
                    sheet.Add((cell.X + step.Item1, cell.Y, cell.Z + step.Item2));
                    queue.Enqueue((cell.X + step.Item1, cell.Y, cell.Z + step.Item2));
                }
        }
        if (sheet.Count >= 100) foreach (var cell in sheet) platform.Add(cell);
    }
}

var member = world.Keys.Where(c => !platform.Contains(c) &&
    (IsLog(world[c].Id) || IsLeaf(world[c].Id) || IsCarpentry(world[c].Id))).ToHashSet();
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
    if (cluster.Any(c => IsLeaf(world[c].Id)) && cluster.Any(c => IsLog(world[c].Id))) trees.Add(cluster);
}

static string Species(int id, int data) => id is 17 or 18
    ? (data & 3) switch { 0 => "oak", 1 => "spruce", 2 => "birch", _ => "jungle" }
    : (data & 1) == 0 ? "acacia" : "dark oak";

var byFamily = trees.GroupBy(t => t.Min(c => c.Z) / 30).OrderBy(g => g.Key).ToList();
var setNumber = 0;
foreach (var family in byFamily)
{
    setNumber++;
    var members = family.ToList();

    // Every tree's profile is taken from its own base so a family can be averaged without smearing.
    var courses = new List<double[]>();       // leaves per course
    var reaches = new List<double[]>();       // how far the widest leaf stands from the stem, per course
    var tallest = members.Max(t => t.Max(c => c.Y) - t.Where(c => IsLog(world[c].Id)).Min(c => c.Y) + 1);
    foreach (var tree in members)
    {
        var baseY = tree.Where(c => IsLog(world[c].Id)).Min(c => c.Y);
        var leaves = tree.Where(c => IsLeaf(world[c].Id)).ToList();
        var stemX = tree.Where(c => IsLog(world[c].Id)).Average(c => (double)c.X);
        var stemZ = tree.Where(c => IsLog(world[c].Id)).Average(c => (double)c.Z);
        var count = new double[tallest];
        var reach = new double[tallest];
        foreach (var leaf in leaves)
        {
            var level = leaf.Y - baseY;
            if (level < 0 || level >= tallest) continue;
            count[level]++;
            reach[level] = Math.Max(reach[level],
                Math.Sqrt((leaf.X - stemX) * (leaf.X - stemX) + (leaf.Z - stemZ) * (leaf.Z - stemZ)));
        }
        courses.Add(count);
        reaches.Add(reach);
    }

    var mean = new double[tallest];
    var meanReach = new double[tallest];
    for (var level = 0; level < tallest; level++)
    {
        mean[level] = courses.Average(c => c[level]);
        meanReach[level] = reaches.Where(r => r[level] > 0).Select(r => r[level]).DefaultIfEmpty(0).Average();
    }

    var leafKind = members.SelectMany(t => t.Where(c => IsLeaf(world[c].Id)))
        .GroupBy(c => Species(world[c].Id, world[c].Data)).OrderByDescending(g => g.Count()).First().Key;
    var logKind = members.SelectMany(t => t.Where(c => IsLog(world[c].Id)))
        .GroupBy(c => Species(world[c].Id, world[c].Data)).OrderByDescending(g => g.Count()).First().Key;

    // Where the crown carries its bulk, and how sharply it gives up width on the way to the top.
    var crownLevels = Enumerable.Range(0, tallest).Where(level => mean[level] > 0).ToList();
    var lowest = crownLevels.Min();
    var top = crownLevels.Max();
    var widest = crownLevels.OrderByDescending(level => mean[level]).First();
    var crownHeight = top - lowest + 1;
    var widestAt = (widest - lowest) / (double)Math.Max(1, crownHeight - 1);
    var half = lowest + crownHeight / 2;
    var lowerHalf = Enumerable.Range(lowest, half - lowest + 1).Sum(level => mean[level]);
    var upperHalf = Enumerable.Range(half, top - half + 1).Sum(level => mean[level]);

    // Whorls: a course that carries more foliage than the one above and the one below it. A conifer built
    // in tiers has several; a single blob has one.
    var whorls = crownLevels.Where(level => level > lowest && level < top
        && mean[level] > mean[level - 1] && mean[level] >= mean[level + 1]).ToList();

    Console.WriteLine($"\n══ set {setNumber} (z {members.Min(t => t.Min(c => c.Z))}) — {members.Count} trees, " +
        $"{logKind} log under {leafKind} leaves ══");
    Console.WriteLine($"   crown {crownHeight} courses tall, widest at {widestAt * 100:0}% of crown height, " +
        $"{lowerHalf / Math.Max(1, lowerHalf + upperHalf) * 100:0}% of foliage in the lower half, " +
        $"{whorls.Count} tier(s)");

    var peak = mean.Max();
    for (var level = top; level >= lowest; level--)
    {
        var bar = (int)Math.Round(mean[level] / peak * 44);
        Console.WriteLine($"   +{level,2}  {mean[level],6:0.0} {meanReach[level],5:0.0}  {new string('█', bar)}");
    }

    // Each tree resampled onto its own crown height before averaging, so a family holding a 19-block tree
    // and a 35-block one is not read as a tapering crown merely because the short ones stop early.
    const int Bands = 10;
    var share = new double[Bands];
    var bandReach = new double[Bands];
    foreach (var tree in members)
    {
        var baseY = tree.Where(c => IsLog(world[c].Id)).Min(c => c.Y);
        var stemX = tree.Where(c => IsLog(world[c].Id)).Average(c => (double)c.X);
        var stemZ = tree.Where(c => IsLog(world[c].Id)).Average(c => (double)c.Z);
        var leaves = tree.Where(c => IsLeaf(world[c].Id)).ToList();
        int leafLow = leaves.Min(c => c.Y), leafHigh = leaves.Max(c => c.Y);
        var ownHeight = Math.Max(1, leafHigh - leafLow);
        var mine = new double[Bands];
        var reachIn = new double[Bands];
        foreach (var leaf in leaves)
        {
            var band = Math.Min(Bands - 1, (leaf.Y - leafLow) * Bands / (ownHeight + 1));
            mine[band]++;
            reachIn[band] = Math.Max(reachIn[band],
                Math.Sqrt((leaf.X - stemX) * (leaf.X - stemX) + (leaf.Z - stemZ) * (leaf.Z - stemZ)));
        }
        for (var band = 0; band < Bands; band++)
        {
            share[band] += mine[band] / leaves.Count / members.Count;
            bandReach[band] += reachIn[band] / members.Count;
        }
    }
    var widestBand = Array.IndexOf(share, share.Max());
    Console.WriteLine($"   normalised: widest tenth is #{widestBand + 1} of 10 from the base, " +
        $"{share.Take(5).Sum() * 100:0}% of foliage in the lower half, " +
        $"reach {bandReach[0]:0.0} at the base against {bandReach[^1]:0.0} at the top " +
        $"(ratio {bandReach[0] / Math.Max(0.1, bandReach[^1]):0.0}x)");
    for (var band = Bands - 1; band >= 0; band--)
        Console.WriteLine($"        {band + 1,2}/10 {share[band] * 100,5:0.0}% {bandReach[band],5:0.0}  " +
            $"{new string('▓', (int)Math.Round(share[band] / share.Max() * 40))}");
}
