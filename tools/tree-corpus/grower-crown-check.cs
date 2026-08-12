#:project ../../src/PgmStudio.Geom/PgmStudio.Geom.csproj
// Runs the real grower over a sweep of the shapes the inspector offers and measures the same thing the
// corpus was measured on: how much of a crown hangs off wood, and how much of it floats free.
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;

var heights = new double[] { 6, 9, 12, 16, 20, 26, 32, 40 };
var leafSizes = new double[] { 0.2, 0.4, 0.6, 0.8, 1.0 };
uint seedCount = 12;

Console.WriteLine($"{"height",7} {"leafSize",9} {"trees",6} {"leaves",8} {"touch wood",11} {"stranded",9} " +
    $"{"islands/tree",12} {"worst",6} {"tips",5} {"tips detached",14}");

var overall = (Leaves: 0, Stranded: 0, Touch: 0, Islands: 0, Tips: 0, Detached: 0);
foreach (var height in heights)
{
    foreach (var leafSize in leafSizes)
    {
        var totals = (Leaves: 0, Stranded: 0, Touch: 0, Islands: 0, Worst: 0, Tips: 0, Detached: 0);
        for (uint seed = 1; seed <= seedCount; seed++)
        {
            var shape = new TreeShape(Height: height, Stems: 1, Levels: 2,
                BranchAngle: 0.55, Flow: 0.45, Leader: 0.55);
            var grown = TreeSkeleton.Grow(shape, seed);

            var wood = new HashSet<(int X, int Y, int Z)>();
            foreach (var limb in grown.Limbs)
                foreach (var cell in SweptVolume.Sweep(limb.Path, limb.StartRadius, limb.EndRadius))
                    wood.Add(cell);

            var clusters = TreeCrown.Clusters(grown.Tips, leafSize, shape.Size, seed);
            var (min, max) = TreeCrown.Bounds(clusters);
            var leaves = new HashSet<(int X, int Y, int Z)>();
            var ownerOf = new Dictionary<(int X, int Y, int Z), int>();
            for (var y = (int)Math.Floor(min.Y); y <= (int)Math.Ceiling(max.Y); y++)
                for (var z = (int)Math.Floor(min.Z); z <= (int)Math.Ceiling(max.Z); z++)
                    for (var x = (int)Math.Floor(min.X); x <= (int)Math.Ceiling(max.X); x++)
                        if (TreeCrown.OwnerAt(clusters, new Vec3(x, y, z), seed) is int owner)
                        {
                            if (wood.Contains((x, y, z))) continue;   // wood wins the cell, as the builder does
                            leaves.Add((x, y, z));
                            ownerOf[(x, y, z)] = owner;
                        }
            if (leaves.Count == 0) continue;

            // Reachability, exactly as measured on the corpus: 26-connected leaves back to any wood.
            var reached = new HashSet<(int X, int Y, int Z)>();
            var frontier = new Queue<(int X, int Y, int Z)>();
            var touching = 0;
            foreach (var leaf in leaves)
            {
                var anyWood = false;
                for (var dy = -1; dy <= 1; dy++)
                    for (var dz = -1; dz <= 1; dz++)
                        for (var dx = -1; dx <= 1; dx++)
                            if ((dx != 0 || dy != 0 || dz != 0) && wood.Contains((leaf.X + dx, leaf.Y + dy, leaf.Z + dz)))
                                anyWood = true;
                if (anyWood) { touching++; if (reached.Add(leaf)) frontier.Enqueue(leaf); }
            }
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
            var stranded = loose.Count;
            var islands = 0;
            var worst = 0;
            while (loose.Count > 0)
            {
                var seedCell = loose.First();
                loose.Remove(seedCell);
                var size = 0;
                var queue = new Queue<(int X, int Y, int Z)>([seedCell]);
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
                islands++;
                worst = Math.Max(worst, size);
            }

            // A cluster is detached when not one of its own cells touches wood — the whole lobe floats.
            var detached = 0;
            for (var i = 0; i < clusters.Count; i++)
            {
                var mine = leaves.Where(l => ownerOf[l] == i).ToList();
                if (mine.Count == 0) continue;
                var held = mine.Any(leaf =>
                {
                    for (var dy = -1; dy <= 1; dy++)
                        for (var dz = -1; dz <= 1; dz++)
                            for (var dx = -1; dx <= 1; dx++)
                                if (wood.Contains((leaf.X + dx, leaf.Y + dy, leaf.Z + dz))) return true;
                    return false;
                });
                if (!held) detached++;
            }

            totals.Leaves += leaves.Count;
            totals.Stranded += stranded;
            totals.Touch += touching;
            totals.Islands += islands;
            totals.Worst = Math.Max(totals.Worst, worst);
            totals.Tips += clusters.Count;
            totals.Detached += detached;
        }
        if (totals.Leaves == 0) continue;
        Console.WriteLine($"{height,7} {leafSize,9:0.0} {seedCount,6} {totals.Leaves,8} " +
            $"{totals.Touch * 100.0 / totals.Leaves,10:0.0}% {totals.Stranded * 100.0 / totals.Leaves,8:0.0}% " +
            $"{totals.Islands / (double)seedCount,12:0.0} {totals.Worst,6} {totals.Tips / (double)seedCount,5:0.0} " +
            $"{totals.Detached * 100.0 / Math.Max(1, totals.Tips),13:0.0}%");
        overall.Leaves += totals.Leaves; overall.Stranded += totals.Stranded; overall.Touch += totals.Touch;
        overall.Islands += totals.Islands; overall.Tips += totals.Tips; overall.Detached += totals.Detached;
    }
}

Console.WriteLine($"\nover the whole sweep: {overall.Leaves} leaves — " +
    $"{overall.Touch * 100.0 / overall.Leaves:0.0}% touch wood, {overall.Stranded * 100.0 / overall.Leaves:0.0}% stranded, " +
    $"{overall.Detached} of {overall.Tips} crown clusters ({overall.Detached * 100.0 / overall.Tips:0.0}%) never touch their own branch");
