#:project ../../src/PgmStudio.Geom/PgmStudio.Geom.csproj
// The grown tree scored on every measure the hand-built corpus supplies, in one run. This is the reading
// behind the "what the grower does with it" table in docs/world-export/tree-corpus.md, and the one to take
// before and after a change to TreeSkeleton, TreeCrown or SweptVolume — each figure is printed beside what
// the corpus measures for the same thing, so a change that improves one and wrecks another is visible in
// the same glance.
//
// The tree is grown and foliated exactly as the dressing pass does it: limbs swept into wood, clusters
// placed on the tips, and the crown filtered to what the tree actually holds.
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;

double[] heights = [6, 9, 12, 16, 20, 26, 32, 40];
double[] leafSizes = [0.3, 0.6, 1.0];
const uint seedCount = 8;
var byHeight = args.Contains("--by-height");

var touch = new List<double>();
var reached = new List<double>();
var occupied = new List<double>();
var enclosed = new List<double>();
var islands = new List<int>();
var worstIsland = 0;
var woodNeighbours = new List<double>();
var woodPieces = new List<int>();
var limbAngle = new List<double>();
var limbReach = new List<double>();
var tipCount = new List<double>();
var leafCount = new List<double>();
var woodCount = new List<double>();
var perHeight = new List<(double Height, double Tips, double Wood, double Leaves, double Touch, double Neighbours)>();

foreach (var height in heights)
{
    var band = (Tips: 0.0, Wood: 0.0, Leaves: 0.0, Touch: 0.0, Neighbours: 0.0, Trees: 0);
    foreach (var leafSize in leafSizes)
        for (uint seed = 1; seed <= seedCount; seed++)
        {
            // The knobs a placed tree ships with: leader and flow are TreeProp's defaults, the rest
            // TreeShape's own, so this tracks the product rather than a tuning run.
            var shape = new TreeShape(Height: height, Leader: 0.55, Flow: 0.45);
            var tree = TreeSkeleton.Grow(shape, seed);

            var wood = new HashSet<(int X, int Y, int Z)>();
            foreach (var limb in tree.Limbs)
                foreach (var cell in SweptVolume.Sweep(limb.Path, limb.StartRadius, limb.EndRadius))
                    wood.Add(cell);

            var clusters = TreeCrown.Clusters(tree.Tips, leafSize, shape.Size, seed);
            var (min, max) = TreeCrown.Bounds(clusters);
            var drawn = new List<(int X, int Y, int Z)>();
            for (var y = (int)Math.Floor(min.Y); y <= (int)Math.Ceiling(max.Y); y++)
                for (var z = (int)Math.Floor(min.Z); z <= (int)Math.Ceiling(max.Z); z++)
                    for (var x = (int)Math.Floor(min.X); x <= (int)Math.Ceiling(max.X); x++)
                        if (!wood.Contains((x, y, z))
                            && TreeCrown.OwnerAt(clusters, new Vec3(x, y, z), seed) is not null)
                            drawn.Add((x, y, z));
            var leaves = TreeCrown.Rooted(drawn, wood);
            if (leaves.Count == 0) continue;

            tipCount.Add(tree.Tips.Count);
            leafCount.Add(leaves.Count);
            woodCount.Add(wood.Count);

            // ── how a leaf is held ──────────────────────────────────────────────────────────────────
            var touching = 0;
            var neighbourTotal = 0L;
            var shut = 0;
            foreach (var leaf in leaves)
            {
                var anyWood = false;
                var faces = 0;
                foreach (var (cell, tier) in Around(leaf))
                {
                    if (leaves.Contains(cell) || wood.Contains(cell)) neighbourTotal++;
                    if (wood.Contains(cell)) anyWood = true;
                    if (tier == 1 && leaves.Contains(cell)) faces++;
                }
                if (anyWood) touching++;
                if (faces == 6) shut++;
            }
            touch.Add(touching * 100.0 / leaves.Count);
            occupied.Add(neighbourTotal / (double)leaves.Count);
            enclosed.Add(shut * 100.0 / leaves.Count);

            // ── what the tree holds, and what it does not ───────────────────────────────────────────
            // Rooted already dropped the unreachable, so this reports zero unless it is broken — which is
            // the point of measuring it rather than trusting it.
            var held = TreeCrown.Rooted(leaves, wood);
            reached.Add(held.Count * 100.0 / leaves.Count);
            var loose = new HashSet<(int X, int Y, int Z)>(leaves.Except(held));
            var count = 0;
            while (loose.Count > 0)
            {
                var start = loose.First();
                loose.Remove(start);
                var size = 1;
                var queue = new Queue<(int X, int Y, int Z)>([start]);
                while (queue.Count > 0)
                    foreach (var (cell, _) in Around(queue.Dequeue()))
                        if (loose.Remove(cell)) { size++; queue.Enqueue(cell); }
                count++;
                worstIsland = Math.Max(worstIsland, size);
            }
            islands.Add(count);

            // ── the wood on its own ─────────────────────────────────────────────────────────────────
            var total = 0L;
            foreach (var cell in wood)
                foreach (var (next, _) in Around(cell))
                    if (wood.Contains(next)) total++;
            woodNeighbours.Add(total / (double)wood.Count);
            woodPieces.Add(Pieces(wood));

            // ── the limbs, chord from where each left its parent to its far end ─────────────────────
            var trunk = tree.Limbs[0];
            var trunkReach = Length(trunk.Path[0], trunk.Path[^1]);
            foreach (var limb in tree.Limbs.Where(limb => limb.Level == 1))
            {
                var span = Length(limb.Path[0], limb.Path[^1]);
                if (span < 0.5) continue;
                limbAngle.Add(Math.Acos(Math.Clamp((limb.Path[^1].Y - limb.Path[0].Y) / span, -1, 1)) * 180 / Math.PI);
                if (trunkReach > 0.5) limbReach.Add(span / trunkReach);
            }

            band = (band.Tips + tree.Tips.Count, band.Wood + wood.Count, band.Leaves + leaves.Count,
                    band.Touch + touching * 100.0 / leaves.Count,
                    band.Neighbours + neighbourTotal / (double)leaves.Count, band.Trees + 1);
        }
    if (band.Trees > 0)
        perHeight.Add((height, band.Tips / band.Trees, band.Wood / band.Trees, band.Leaves / band.Trees,
                       band.Touch / band.Trees, band.Neighbours / band.Trees));
}

Console.WriteLine($"{heights.Length * leafSizes.Length * seedCount} trees over {heights.Length} heights x " +
    $"{leafSizes.Length} leaf sizes x {seedCount} seeds ({leafCount.Sum():0} leaves, {woodCount.Sum():0} wood)");
Console.WriteLine($"\n  {"measure",-36} {"grown",8}   hand-built");
Row("leaves reaching wood, median tree", $"{Median(reached):0.0}%", "99.95%");
Row("leaves touching wood, median tree", $"{Median(touch):0.0}%", "30.3%");
Row("occupied neighbours per leaf", $"{Median(occupied):0.0}", "6.2");
Row("leaves enclosed on all six faces", $"{Median(enclosed):0.0}%", "1.7%");
Row("crown islands per tree", $"{Median(islands.Select(i => (double)i)):0.0}", "0");
Row("worst stranded island", $"{worstIsland}", "2 blocks");
Row("trees carrying a stranded leaf", $"{islands.Count(i => i > 0) * 100.0 / islands.Count:0}%", "0%");
Row("wood neighbours per block", $"{Median(woodNeighbours):0.0}", "6.3");
Row("trees whose wood is in one piece", $"{woodPieces.Count(p => p == 1) * 100.0 / woodPieces.Count:0}%", "96%");
Row("first-order limb, off vertical", $"{limbAngle.Average():0}°", "59°");
Row("first-order limb, reach vs trunk", $"{limbReach.Average():0.00}", "0.40");
Row("tips per tree, median", $"{Median(tipCount):0}", "31 on the wool tree");
Row("leaves per block of wood", $"{leafCount.Sum() / woodCount.Sum():0.0}", "2.7-13.4 on its large trees");

if (byHeight)
{
    Console.WriteLine($"\n  ── by height, so a density that climbs with size is visible ──");
    Console.WriteLine($"  {"height",7} {"tips",6} {"wood",7} {"leaves",8} {"touch",7} {"neigh",6}");
    foreach (var band in perHeight)
        Console.WriteLine($"  {band.Height,7:0} {band.Tips,6:0} {band.Wood,7:0} {band.Leaves,8:0} " +
            $"{band.Touch,6:0.0}% {band.Neighbours,6:0.0}");
}

void Row(string name, string grown, string handBuilt) =>
    Console.WriteLine($"  {name,-36} {grown,8}   {handBuilt}");

// Every neighbour in the 3x3x3, with the ladder tier it sits at — 1 face, 2 edge, 3 corner.
static IEnumerable<((int X, int Y, int Z) Cell, int Tier)> Around((int X, int Y, int Z) cell)
{
    for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
            for (var dx = -1; dx <= 1; dx++)
                if (dx != 0 || dy != 0 || dz != 0)
                    yield return ((cell.X + dx, cell.Y + dy, cell.Z + dz),
                                  Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz));
}

static double Length(Vec3 from, Vec3 to)
{
    var span = to - from;
    return Math.Sqrt(span.X * span.X + span.Y * span.Y + span.Z * span.Z);
}

static int Pieces(HashSet<(int X, int Y, int Z)> cells)
{
    var pending = new HashSet<(int X, int Y, int Z)>(cells);
    var count = 0;
    while (pending.Count > 0)
    {
        var start = pending.First();
        pending.Remove(start);
        var queue = new Queue<(int X, int Y, int Z)>([start]);
        while (queue.Count > 0)
            foreach (var (cell, _) in Around(queue.Dequeue()))
                if (pending.Remove(cell)) queue.Enqueue(cell);
        count++;
    }
    return count;
}

static double Median(IEnumerable<double> values)
{
    var sorted = values.OrderBy(value => value).ToList();
    return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
}
