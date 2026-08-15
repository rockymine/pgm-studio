#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
// The tree with its foliage disregarded: the trunk and branch network on its own. Every piece of wood is
// filed by its strongest join to the next — face, edge or corner, the same ladder a leaf is read on — the
// network is decomposed into a stem and the limbs that leave it, and each limb is measured for where it
// attaches, how far it reaches and at what angle. The wool tree, whose limbs are colour-labelled, is the
// control: the decomposition is run against a skeleton already known by hand.
using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;

var regionDir = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : "tools/tree-corpus/tree-showcase/region";
var logsOnly = args.Contains("--logs-only");          // drop the carpentry some families branch with
var grower = args.Contains("--grower");               // read the generator's own wood instead of a world
var perTree = args.Contains("--per-tree");
var byFamily = args.Contains("--families");
var sketchWanted = Array.IndexOf(args, "--sketch") is var sketchAt && sketchAt >= 0
    ? (args.Length > sketchAt + 1 && int.TryParse(args[sketchAt + 1], out var only) ? only : 0)
    : -1;

var reading = new List<TreeReading>();
if (grower) ReadGrower(); else ReadWorld();

void ReadWorld()
{
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

// The platforms are named as what they are — broad flat sheets of one course — so a trunk is never fused
// to the floor it stands on, and the plank floor is never read as a branch.
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

// Tree bodies are clustered with their foliage, so a tree is the same body here as in every other reading
// of this world; the wood is then taken out of each one.
var member = world.Keys.Where(c => !platform.Contains(c) && (IsLog(world[c].Id) || IsLeaf(world[c].Id)
    || world[c].Id == 35 || IsCarpentry(world[c].Id))).ToHashSet();
var seen = new HashSet<(int X, int Y, int Z)>();
var bodies = new List<List<(int X, int Y, int Z)>>();
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
    bodies.Add(cluster);
}
var trees = bodies.Where(c => c.Any(p => IsLog(world[p].Id) || world[p].Id == 35))
    .OrderBy(c => c.Min(p => p.Z) / 30).ThenBy(c => c.Min(p => p.X)).ToList();

bool IsWood((int X, int Y, int Z) cell) => world.TryGetValue(cell, out var block)
    && (IsLog(block.Id) || block.Id == 35 || (!logsOnly && IsCarpentry(block.Id)));

Console.WriteLine($"╔══ the wood network of {Path.GetFileName(Path.GetDirectoryName(regionDir)!)}, foliage disregarded ══╗");
Console.WriteLine($"  {trees.Count} tree bodies; " +
    $"{trees.Sum(t => t.Count(IsWood))} wood blocks " +
    $"({trees.Sum(t => t.Count(c => IsLog(world[c].Id)))} log, " +
    $"{trees.Sum(t => t.Count(c => world[c].Id == 35))} wool, " +
    $"{trees.Sum(t => t.Count(c => IsCarpentry(world[c].Id)))} carpentry" +
    (logsOnly ? ", not counted as wood" : "") + ")");

foreach (var (body, at) in trees.Select((body, at) => (body, at)))
{
    var wood = body.Where(IsWood).ToHashSet();
    if (wood.Count == 0) continue;
    var species = body.Any(c => IsLog(world[c].Id))
        ? body.Where(c => IsLog(world[c].Id)).GroupBy(c => Species(world[c].Id, world[c].Data))
            .OrderByDescending(g => g.Count()).First().Key
        : "wool";
    var foot = wood.OrderBy(c => c.Y).First();
    var under = world.GetValueOrDefault((foot.X, foot.Y - 1, foot.Z), (0, 0));
    reading.Add(Read(at + 1, body.Min(c => c.Z) / 30, species, wood,
        under.Item1 != 0 && !IsLog(under.Item1) && !IsLeaf(under.Item1) && under.Item1 != 35));
}
}

// The generator's own wood, swept from the limb splines exactly as the dressing pass emits it, so the
// same reading can be taken of it. Every tree is grown in its own frame, which is what keeps the sweep
// from reading as one thicket.
void ReadGrower()
{
    double[] heights = [6, 9, 12, 16, 20, 26, 32, 40];
    const uint seedCount = 12;
    Console.WriteLine($"╔══ the wood network of the grown tree, foliage disregarded ══╗");
    var index = 0;
    foreach (var height in heights)
        for (uint seed = 1; seed <= seedCount; seed++)
        {
            // The knobs a placed tree ships with: leader and flow are TreeProp's defaults, and everything
            // else is TreeShape's own, so the harness tracks the product rather than a tuning run.
            var shape = new TreeShape(Height: height, Leader: 0.55, Flow: 0.45);
            var wood = new HashSet<(int X, int Y, int Z)>();
            foreach (var limb in TreeSkeleton.Grow(shape, seed).Limbs)
                foreach (var cell in SweptVolume.Sweep(limb.Path, limb.StartRadius, limb.EndRadius))
                    wood.Add(cell);
            reading.Add(Read(++index, (int)height, $"grown h{height:0}", wood, true));
        }
    Console.WriteLine($"  {reading.Count} trees over {heights.Length} heights x {seedCount} seeds; " +
        $"{reading.Sum(t => t.Wood.Count)} wood blocks");
}

// ── the ladder: face (one axis apart), edge (two), corner (three) ───────────────────────────────────
static int Kind(int dx, int dy, int dz) => Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz);
static double Step(int kind) => kind == 1 ? 1 : kind == 2 ? 1.41421356 : 1.73205081;
static double Distance((int X, int Y, int Z) from, (int X, int Y, int Z) to) =>
    Math.Sqrt(Sq(to.X - from.X) + Sq(to.Y - from.Y) + Sq(to.Z - from.Z));
static double Sq(double value) => value * value;

static TreeReading Read(int index, int band, string kindOfWood, HashSet<(int X, int Y, int Z)> wood, bool grounded)
{
    // Every block of wood filed by the strongest join it has to another, and by how many joins of each
    // kind it carries — the girth measure that thins as a limb runs out.
    var strongest = new Dictionary<(int X, int Y, int Z), int>();
    var degree = new Dictionary<(int X, int Y, int Z), (int Face, int Edge, int Corner)>();
    var links = new int[4];
    foreach (var cell in wood)
    {
        var best = 4;
        var (face, edge, corner) = (0, 0, 0);
        for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    if (!wood.Contains((cell.X + dx, cell.Y + dy, cell.Z + dz))) continue;
                    var kind = Kind(dx, dy, dz);
                    best = Math.Min(best, kind);
                    if (kind == 1) face++; else if (kind == 2) edge++; else corner++;
                    links[kind]++;
                }
        strongest[cell] = best;
        degree[cell] = (face, edge, corner);
    }
    for (var kind = 1; kind <= 3; kind++) links[kind] /= 2;      // each join counted from both ends

    // Whether the network holds together at all, and whether it holds together without the diagonals.
    var pieces = Components(wood, 3);
    var facePieces = Components(wood, 1);

    // ── the skeleton ────────────────────────────────────────────────────────────────────────────────
    // Rooted at the lowest wood, a shortest-path tree over the network gives every block a parent; the
    // stem is the chain that carries the most wood at every fork, and a limb is a chain leaving it.
    var root = wood.OrderBy(c => c.Y).ThenBy(c => Distance(c, Centre(wood))).First();
    var (parent, order) = ShortestPathTree(wood, root);
    var carried = new Dictionary<(int X, int Y, int Z), int>();
    foreach (var cell in wood) carried[cell] = 1;
    foreach (var cell in Enumerable.Reverse(order))
        if (parent.TryGetValue(cell, out var up) && up != cell) carried[up] += carried[cell];

    var children = new Dictionary<(int X, int Y, int Z), List<(int X, int Y, int Z)>>();
    foreach (var cell in wood)
    {
        if (!parent.TryGetValue(cell, out var up) || up == cell) continue;
        if (!children.TryGetValue(up, out var hanging)) children[up] = hanging = [];
        hanging.Add(cell);
    }

    List<(int X, int Y, int Z)> Chain((int X, int Y, int Z) from)
    {
        var chain = new List<(int X, int Y, int Z)> { from };
        var cell = from;
        while (children.TryGetValue(cell, out var next) && next.Count > 0)
        {
            cell = next.OrderByDescending(c => carried[c]).ThenByDescending(c => c.Y).First();
            chain.Add(cell);
        }
        return chain;
    }

    var limbs = new List<Limb>();
    var stem = Chain(root);
    limbs.Add(new Limb(0, -1, [.. stem], [], root));
    var absorbed = (Chains: 0, Cells: 0);
    var frontier = new Queue<((int X, int Y, int Z) Start, (int X, int Y, int Z) From, int Host)>();
    Follow(stem, 0);
    while (frontier.Count > 0)
    {
        var (start, from, hostIndex) = frontier.Dequeue();
        var host = limbs[hostIndex];
        var chain = Chain(start);
        // A chain that never gets clear of the limb it leaves is a doubling of that limb — the second
        // column of a two-wide bole, or a knob on its side — not a branch off it. It is absorbed into
        // its host, so that what leaves it further out is still measured against the limb it really
        // leaves rather than against a piece of the same wood.
        if (chain.All(c => host.Cells.Any(h => Distance(c, h) <= 1.75)))
        {
            absorbed = (absorbed.Chains + 1, absorbed.Cells + chain.Count);
            host.Doubled.AddRange(chain);
            Follow(chain, hostIndex);
            continue;
        }
        limbs.Add(new Limb(host.Order + 1, hostIndex, [.. chain], [], from));
        Follow(chain, limbs.Count - 1);
    }

    // Everything hanging off a chain other than the chain's own continuation starts a chain of its own.
    void Follow(List<(int X, int Y, int Z)> chain, int hostIndex)
    {
        var onChain = chain.ToHashSet();
        foreach (var cell in chain)
            foreach (var child in children.GetValueOrDefault(cell, []))
                if (!onChain.Contains(child)) frontier.Enqueue((child, cell, hostIndex));
    }

    // ── the stem, on its own ────────────────────────────────────────────────────────────────────────
    var top = stem[^1];
    var rise = top.Y - root.Y;
    var chord = Distance(root, top);
    var lean = chord < 0.5 ? 0 : Math.Acos(Math.Clamp(rise / chord, -1, 1)) * 180 / Math.PI;
    var drift = Math.Sqrt(Sq(top.X - root.X) + Sq(top.Z - root.Z));

    // The bole is the stem below the crown: the run up to the course where the first branch of reach 3
    // or more leaves it. The reach threshold is what keeps a flare at the foot or a knob on the side
    // from being read as the crown starting there; taking the course rather than a position along the
    // spine is what keeps a thick bole, whose branches leave from wood beside the spine, readable. Lean
    // measured on the bole rather than the whole stem is what keeps a tree from reading as angled
    // merely because the wood carrying the most blocks turns off at the top.
    var crownBase = limbs.Where(l => l.Order == 1 && l.Reach >= 3)
        .Select(l => l.From.Y).DefaultIfEmpty(top.Y).Min();
    var boleTop = stem.Where(c => c.Y <= crownBase).OrderByDescending(c => c.Y).FirstOrDefault(stem[0]);
    var boleChord = Distance(root, boleTop);
    var boleLean = boleChord < 0.5 ? 0 : Math.Acos(Math.Clamp((boleTop.Y - root.Y) / boleChord, -1, 1)) * 180 / Math.PI;
    // How far the stem climbs of everything standing above the root — the leader knob, measured.
    var leader = wood.Max(c => c.Y) - root.Y is var span && span > 0 ? (top.Y - root.Y) / (double)span : 1;
    var travel = 0.0;
    var steps = new int[4];
    var flat = 0;
    for (var i = 1; i < stem.Count; i++)
    {
        travel += Math.Sqrt(Sq(stem[i].X - stem[i - 1].X) + Sq(stem[i].Z - stem[i - 1].Z));
        var kind = Kind(stem[i].X - stem[i - 1].X, stem[i].Y - stem[i - 1].Y, stem[i].Z - stem[i - 1].Z);
        steps[kind]++;
        if (stem[i].Y == stem[i - 1].Y) flat++;
    }
    // Girth is read on the stem: wood standing within a block of the stem's own course.
    var girth = stem.Take(Math.Max(1, stem.Count / 2)).Average(cell =>
        wood.Count(c => c.Y == cell.Y && Math.Abs(c.X - cell.X) <= 1 && Math.Abs(c.Z - cell.Z) <= 1));

    // How far out each block of wood stands from the stem, counted in steps through the network — the
    // axis the thinning is read on, and the one measure here that owes nothing to the limb decomposition.
    var fromStem = new Dictionary<(int X, int Y, int Z), int>();
    var walk = new Queue<(int X, int Y, int Z)>();
    foreach (var cell in limbs[0].Cells) { fromStem[cell] = 0; walk.Enqueue(cell); }
    while (walk.Count > 0)
    {
        var cell = walk.Dequeue();
        for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = (cell.X + dx, cell.Y + dy, cell.Z + dz);
                    if (!wood.Contains(next) || fromStem.ContainsKey(next)) continue;
                    fromStem[next] = fromStem[cell] + 1;
                    walk.Enqueue(next);
                }
    }

    return new TreeReading(index, band, kindOfWood, wood, strongest, degree, links,
        pieces, facePieces, limbs, fromStem, absorbed.Chains, absorbed.Cells, stem, rise, lean,
        boleTop.Y - root.Y, boleLean, leader, drift, travel, steps, flat, girth, grounded);
}

static (int X, int Y, int Z) Centre(HashSet<(int X, int Y, int Z)> cells) =>
    ((int)cells.Average(c => c.X), (int)cells.Average(c => c.Y), (int)cells.Average(c => c.Z));

// Connected pieces of a cell set, joined at the given ladder tier or tighter (1 face-only, 3 anything).
static List<int> Components(HashSet<(int X, int Y, int Z)> cells, int tier)
{
    var pending = new HashSet<(int X, int Y, int Z)>(cells);
    var sizes = new List<int>();
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
                        if (Kind(dx, dy, dz) is > 0 and var kind && kind <= tier
                            && pending.Remove((cell.X + dx, cell.Y + dy, cell.Z + dz)))
                            queue.Enqueue((cell.X + dx, cell.Y + dy, cell.Z + dz));
        }
        sizes.Add(size);
    }
    return [.. sizes.OrderByDescending(s => s)];
}

// Dijkstra over the network with a face step costing 1, an edge √2 and a corner √3, so the tree of
// parents follows the wood the way it was built rather than the way a queue happens to drain.
static (Dictionary<(int X, int Y, int Z), (int X, int Y, int Z)> Parent, List<(int X, int Y, int Z)> Order)
    ShortestPathTree(HashSet<(int X, int Y, int Z)> wood, (int X, int Y, int Z) root)
{
    var best = new Dictionary<(int X, int Y, int Z), double> { [root] = 0 };
    var parent = new Dictionary<(int X, int Y, int Z), (int X, int Y, int Z)> { [root] = root };
    var order = new List<(int X, int Y, int Z)>();
    var settled = new HashSet<(int X, int Y, int Z)>();
    var queue = new PriorityQueue<(int X, int Y, int Z), double>();
    queue.Enqueue(root, 0);
    while (queue.TryDequeue(out var cell, out var cost))
    {
        if (!settled.Add(cell)) continue;
        order.Add(cell);
        for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    var next = (cell.X + dx, cell.Y + dy, cell.Z + dz);
                    if (!wood.Contains(next) || settled.Contains(next)) continue;
                    var through = cost + Step(Kind(dx, dy, dz));
                    if (best.TryGetValue(next, out var known) && known <= through) continue;
                    best[next] = through;
                    parent[next] = cell;
                    queue.Enqueue(next, through);
                }
    }
    return (parent, order);
}

// ── what the corpus says ────────────────────────────────────────────────────────────────────────────
string[] tierName = ["—", "face", "edge", "corner"];
var allWood = reading.Sum(t => t.Wood.Count);

Console.WriteLine($"\n── every block of wood, by its strongest join to the next ──");
Console.WriteLine($"  {"strongest join",-16} {"blocks",8} {"share",7}");
for (var kind = 1; kind <= 4; kind++)
{
    var count = reading.Sum(t => t.Strongest.Values.Count(s => s == kind));
    Console.WriteLine($"  {(kind == 4 ? "nothing at all" : tierName[kind] + "-to-wood"),-16} {count,8} {count * 100.0 / allWood,6:0.0}%");
}
var joins = Enumerable.Range(1, 3).Select(k => reading.Sum(t => t.Links[k])).ToArray();
Console.WriteLine($"  joins in the network: {joins.Sum()} — {joins[0]} face, {joins[1]} edge, {joins[2]} corner " +
    $"({joins[0] * 100.0 / joins.Sum():0.0}% / {joins[1] * 100.0 / joins.Sum():0.0}% / {joins[2] * 100.0 / joins.Sum():0.0}%)");
Console.WriteLine($"  wood neighbours per block: {reading.Sum(t => t.Degree.Values.Sum(d => d.Face + d.Edge + d.Corner)) / (double)allWood:0.0} " +
    $"(face {reading.Sum(t => t.Degree.Values.Sum(d => d.Face)) / (double)allWood:0.0}, " +
    $"edge {reading.Sum(t => t.Degree.Values.Sum(d => d.Edge)) / (double)allWood:0.0}, " +
    $"corner {reading.Sum(t => t.Degree.Values.Sum(d => d.Corner)) / (double)allWood:0.0})");

foreach (var lone in reading.Where(t => t.Strongest.Values.Any(s => s == 4)))
    Console.WriteLine($"  wood touching no other wood: tree {lone.Index} (set {lone.Band}, {lone.Timber}) — " +
        $"{string.Join(", ", lone.Strongest.Where(pair => pair.Value == 4).Select(pair => $"{pair.Key.X},{pair.Key.Y},{pair.Key.Z}"))}");

Console.WriteLine($"\n── does the wood hold together on its own ──");
Console.WriteLine($"  trees whose wood is one network: {reading.Count(t => t.Pieces.Count == 1)} of {reading.Count}");
foreach (var tree in reading.Where(t => t.Pieces.Count > 1).OrderByDescending(t => t.Pieces.Count))
    Console.WriteLine($"    tree {tree.Index,2} (set {tree.Band}): {tree.Pieces.Count} pieces — " +
        $"{string.Join(" + ", tree.Pieces.Take(6))}{(tree.Pieces.Count > 6 ? " + …" : "")}");
Console.WriteLine($"  trees whose wood is one network on face joins alone: " +
    $"{reading.Count(t => t.FacePieces.Count == 1)} of {reading.Count}");
Console.WriteLine($"  pieces a face-only reading breaks a tree into: median {Median(reading.Select(t => (double)t.FacePieces.Count))
    :0}, worst {reading.Max(t => t.FacePieces.Count)} " +
    $"(tree {reading.OrderByDescending(t => t.FacePieces.Count).First().Index})");

Console.WriteLine($"\n── the grip loosens outward: wood by branch order ──");
Console.WriteLine($"  {"order",6} {"limbs",6} {"blocks",7} {"neighbours",11} {"face",6} {"edge",6} {"corner",7} {"girth",6} {"reach",6} {"angle",6}");
var maxOrder = reading.SelectMany(t => t.Limbs).Max(l => l.Order);
for (var level = 0; level <= maxOrder; level++)
{
    var limbs = reading.SelectMany(t => t.Limbs.Where(l => l.Order == level)).ToList();
    if (limbs.Count == 0) continue;
    var cells = reading.SelectMany(t => t.Limbs.Where(l => l.Order == level)
        .SelectMany(l => l.Cells.Select(c => (Tree: t, Cell: c)))).ToList();
    Console.WriteLine($"  {level,6} {limbs.Count,6} {cells.Count,7} " +
        $"{cells.Average(c => c.Tree.Degree[c.Cell].Face + c.Tree.Degree[c.Cell].Edge + c.Tree.Degree[c.Cell].Corner),11:0.0} " +
        $"{cells.Count(c => c.Tree.Strongest[c.Cell] == 1) * 100.0 / cells.Count,5:0}% " +
        $"{cells.Count(c => c.Tree.Strongest[c.Cell] == 2) * 100.0 / cells.Count,5:0}% " +
        $"{cells.Count(c => c.Tree.Strongest[c.Cell] == 3) * 100.0 / cells.Count,6:0}% " +
        $"{cells.Average(c => c.Tree.Wood.Count(w => w.Y == c.Cell.Y && Math.Abs(w.X - c.Cell.X) <= 1 && Math.Abs(w.Z - c.Cell.Z) <= 1)),6:0.0} " +
        $"{limbs.Average(l => l.Reach),6:0.0} {limbs.Average(l => l.Angle),5:0}°");
}
Console.WriteLine($"  chains absorbed as a doubling of the limb they leave (a bole's second column, a knob): " +
    $"{reading.Sum(t => t.Doublings)}, {reading.Sum(t => t.DoubledBlocks)} blocks");
Console.WriteLine($"  wood the network never reaches from the root: " +
    $"{reading.Sum(t => t.Wood.Count - t.Limbs.Sum(l => l.Blocks))} blocks");

Console.WriteLine($"\n── the same thinning read off the network alone: wood by its steps from the stem ──");
Console.WriteLine($"  {"steps out",10} {"blocks",8} {"neighbours",11} {"face",6} {"edge",6} {"corner",7} {"girth",6} {"ends",6}");
foreach (var band in new (string Name, int Low, int High)[] { ("on the stem", 0, 0), ("1-2", 1, 2),
                             ("3-5", 3, 5), ("6-9", 6, 9), ("10 or more", 10, int.MaxValue) })
{
    var cells = reading.SelectMany(t => t.Wood.Where(c => t.FromStem.TryGetValue(c, out var steps)
        && steps >= band.Low && steps <= band.High).Select(c => (Tree: t, Cell: c))).ToList();
    if (cells.Count == 0) continue;
    Console.WriteLine($"  {band.Name,10} {cells.Count,8} " +
        $"{cells.Average(c => c.Tree.Degree[c.Cell].Face + c.Tree.Degree[c.Cell].Edge + c.Tree.Degree[c.Cell].Corner),11:0.0} " +
        $"{cells.Count(c => c.Tree.Strongest[c.Cell] == 1) * 100.0 / cells.Count,5:0}% " +
        $"{cells.Count(c => c.Tree.Strongest[c.Cell] == 2) * 100.0 / cells.Count,5:0}% " +
        $"{cells.Count(c => c.Tree.Strongest[c.Cell] == 3) * 100.0 / cells.Count,6:0}% " +
        $"{cells.Average(c => c.Tree.Wood.Count(w => w.Y == c.Cell.Y && Math.Abs(w.X - c.Cell.X) <= 1 && Math.Abs(w.Z - c.Cell.Z) <= 1)),6:0.0} " +
        $"{cells.Count(c => c.Tree.Degree[c.Cell].Face + c.Tree.Degree[c.Cell].Edge + c.Tree.Degree[c.Cell].Corner <= 1) * 100.0 / cells.Count,5:0}%");
}

Console.WriteLine($"\n── how a limb sits on its parent ──");
var held = reading.SelectMany(t => t.Limbs.Where(l => l.Order > 0)
    .Select(l => (Limb: l, Host: t.Limbs[l.Host]))).ToList();
foreach (var level in held.GroupBy(h => h.Limb.Order).OrderBy(g => g.Key))
    Console.WriteLine($"  order {level.Key}: {level.Count(),4} limbs, " +
        $"reach {level.Average(h => h.Limb.Reach),4:0.0} — {level.Average(h => h.Host.Reach < 0.5 ? 0 : h.Limb.Reach / h.Host.Reach),4:0.00} of its parent's, " +
        $"leaving it {level.Average(h => h.Host.Along(h.Limb.From)),4:0.00} of the way along, " +
        $"at {level.Average(h => h.Limb.Angle),3:0}° off vertical");
Console.WriteLine($"  the fan, median tree: {string.Join(" → ", Enumerable.Range(0, maxOrder + 1)
    .Select(level => $"{Median(reading.Select(t => (double)t.Limbs.Count(l => l.Order == level))):0}"))}");

Console.WriteLine($"\n── the stem ──");
Console.WriteLine($"  {"lean of the bole",-26} {"trees",6} {"median rise",12} {"girth",6} {"drift",6} {"whole stem",11}");
foreach (var band in reading.GroupBy(t => t.BoleLean < 8 ? "upright (under 8°)"
             : t.BoleLean < 25 ? "leaning (8-25°)" : "angled (over 25°)")
             .OrderBy(g => g.Average(t => t.BoleLean)))
    Console.WriteLine($"  {band.Key,-26} {band.Count(),6} {Median(band.Select(t => (double)t.BoleRise)),12:0} " +
        $"{band.Average(t => t.Girth),6:0.0} {band.Average(t => t.Drift),6:0.0} {band.Average(t => t.Lean),10:0}°");
Console.WriteLine($"  the bole carries {Median(reading.Select(t => t.BoleRise / (double)Math.Max(1, t.Rise))):0.00} " +
    $"of the stem's rise before the first branch leaves it (median tree)");
Console.WriteLine($"  the stem climbs {Median(reading.Select(t => t.Leader)):0.00} of the wood standing over the root " +
    $"(median tree); it reaches the top in {reading.Count(t => t.Leader > 0.98)} of {reading.Count}");
var stemSteps = Enumerable.Range(1, 3).Select(k => reading.Sum(t => t.Steps[k])).ToArray();
Console.WriteLine($"  steps up a stem: {stemSteps.Sum()} — {stemSteps[0]} face, {stemSteps[1]} edge, {stemSteps[2]} corner " +
    $"({stemSteps[0] * 100.0 / stemSteps.Sum():0.0}% / {stemSteps[1] * 100.0 / stemSteps.Sum():0.0}% / {stemSteps[2] * 100.0 / stemSteps.Sum():0.0}%)");
Console.WriteLine($"  stems that never step sideways at all: {reading.Count(t => t.Steps[2] + t.Steps[3] == 0)} of {reading.Count}");
Console.WriteLine($"  a stem that does lean carries it one way: drift over travel " +
    $"{reading.Where(t => t.Travel > 0).Average(t => t.Drift / t.Travel):0.00} " +
    $"(1.00 is a straight lean, low is a wander or a spiral)");
Console.WriteLine($"  stems with a horizontal step (the wood turns and runs flat): " +
    $"{reading.Count(t => t.Flat > 0)} of {reading.Count}");
Console.WriteLine($"  trees whose lowest wood rests on nothing: {reading.Count(t => !t.Grounded)}");

// The wool tree is the control: its limbs are colour-labelled, so the decomposition can be checked
// against a skeleton already read by hand (`wool-skeleton.cs`) before it is trusted on the other 74.
foreach (var control in reading.Where(t => t.Timber == "wool"))
{
    Console.WriteLine($"\n── the wool tree, where the skeleton is known by hand ──");
    Console.WriteLine($"  tree {control.Index}: {control.Wood.Count} blocks, " +
        $"{control.Limbs.Count} limbs in {control.Limbs.Max(l => l.Order) + 1} orders, " +
        $"fan {string.Join(" → ", Enumerable.Range(0, control.Limbs.Max(l => l.Order) + 1)
            .Select(level => control.Limbs.Count(l => l.Order == level)))}");
    Console.WriteLine($"  {"order",6} {"limbs",6} {"blocks each",12} {"reach",6} {"angle",6} {"of its parent's reach",22}");
    foreach (var level in control.Limbs.GroupBy(l => l.Order).OrderBy(g => g.Key))
        Console.WriteLine($"  {level.Key,6} {level.Count(),6} {level.Average(l => l.Blocks),12:0.0} " +
            $"{level.Average(l => l.Reach),6:0.0} {level.Average(l => l.Angle),5:0}° " +
            $"{(level.Key == 0 ? 0 : level.Average(l => control.Limbs[l.Host].Reach < 0.5 ? 0 : l.Reach / control.Limbs[l.Host].Reach)),21:0.00}");
    Console.WriteLine($"  the primaries leave the stem at y {string.Join(", ", control.Limbs
        .Where(l => l.Order == 1).Select(l => l.From.Y).OrderBy(y => y))}");
}

if (byFamily)
{
    Console.WriteLine($"\n── by set (one 19x19 platform band = one family the author built) ──");
    Console.WriteLine($"  {"set",4} {"trees",6} {"wood",6} {"face",6} {"edge",6} {"corner",7} {"neigh",6} {"orders",7} {"limbs",6} {"lean",6} {"girth",6}  wood");
    var number = 0;
    foreach (var family in reading.GroupBy(t => t.Band).OrderBy(g => g.Key))
    {
        number++;
        var cells = family.SelectMany(t => t.Wood.Select(c => (Tree: t, Cell: c))).ToList();
        Console.WriteLine($"  {number,4} {family.Count(),6} {cells.Count,6} " +
            $"{cells.Count(c => c.Tree.Strongest[c.Cell] == 1) * 100.0 / cells.Count,5:0}% " +
            $"{cells.Count(c => c.Tree.Strongest[c.Cell] == 2) * 100.0 / cells.Count,5:0}% " +
            $"{cells.Count(c => c.Tree.Strongest[c.Cell] == 3) * 100.0 / cells.Count,6:0}% " +
            $"{cells.Average(c => c.Tree.Degree[c.Cell].Face + c.Tree.Degree[c.Cell].Edge + c.Tree.Degree[c.Cell].Corner),6:0.0} " +
            $"{family.Average(t => t.Limbs.Max(l => l.Order)),7:0.0} " +
            $"{family.Average(t => t.Limbs.Count(l => l.Order > 0)),6:0.0} " +
            $"{family.Average(t => t.BoleLean),5:0}° {family.Average(t => t.Girth),6:0.0}  " +
            $"{string.Join("/", family.Select(t => t.Timber).Distinct())}");
    }
}

if (perTree)
{
    Console.WriteLine($"\n── every tree ──");
    Console.WriteLine($"  {"#",3} {"set",4} {"wood",5} {"face",5} {"edge",5} {"corner",6} {"neigh",6} {"pieces",7} {"orders",7} {"limbs",6} " +
        $"{"rise",5} {"bole",5} {"lean",5} {"girth",6}  wood");
    foreach (var tree in reading)
        Console.WriteLine($"  {tree.Index,3} {tree.Band,4} {tree.Wood.Count,5} " +
            $"{tree.Strongest.Values.Count(s => s == 1) * 100.0 / tree.Wood.Count,4:0}% " +
            $"{tree.Strongest.Values.Count(s => s == 2) * 100.0 / tree.Wood.Count,4:0}% " +
            $"{tree.Strongest.Values.Count(s => s == 3) * 100.0 / tree.Wood.Count,5:0}% " +
            $"{tree.Degree.Values.Average(d => d.Face + d.Edge + d.Corner),6:0.0} {tree.Pieces.Count,7} " +
            $"{tree.Limbs.Max(l => l.Order),7} {tree.Limbs.Count(l => l.Order > 0),6} " +
            $"{tree.Rise,5} {tree.BoleRise,5} {tree.BoleLean,4:0}° {tree.Girth,6:0.0}  {tree.Timber}");
}

if (sketchWanted >= 0)
{
    Console.WriteLine($"\n── the network drawn, foliage disregarded ──");
    Console.WriteLine($"  # is the stem, o a limb off it, + a limb further out, · wood doubling a limb it never leaves,");
    Console.WriteLine($"  ? wood the root never reaches. The network is projected onto its widest face.");
    foreach (var tree in reading.Where(t => sketchWanted == 0 || t.Index == sketchWanted))
    {
        var owner = new Dictionary<(int X, int Y, int Z), char>();
        foreach (var limb in tree.Limbs)
        {
            foreach (var cell in limb.Spine) owner[cell] = limb.Order == 0 ? '#' : limb.Order == 1 ? 'o' : '+';
            foreach (var cell in limb.Doubled) owner[cell] = '·';
        }
        var alongX = tree.Wood.Max(c => c.X) - tree.Wood.Min(c => c.X) >= tree.Wood.Max(c => c.Z) - tree.Wood.Min(c => c.Z);
        Console.WriteLine($"\n  tree {tree.Index} (set {tree.Band}, {tree.Timber}, {tree.Wood.Count} wood, " +
            $"{tree.Limbs.Count(l => l.Order > 0)} limbs, bole lean {tree.BoleLean:0}°) looking along {(alongX ? "z" : "x")}");
        for (var y = tree.Wood.Max(c => c.Y); y >= tree.Wood.Min(c => c.Y); y--)
        {
            var line = new System.Text.StringBuilder("  ");
            for (var across = alongX ? tree.Wood.Min(c => c.X) : tree.Wood.Min(c => c.Z);
                 across <= (alongX ? tree.Wood.Max(c => c.X) : tree.Wood.Max(c => c.Z)); across++)
            {
                var column = tree.Wood.Where(c => c.Y == y && (alongX ? c.X : c.Z) == across).ToList();
                line.Append(column.Count == 0 ? ' ' : column.Select(c => owner.GetValueOrDefault(c, '?'))
                    .OrderBy(ch => ch == '#' ? 0 : ch == 'o' ? 1 : ch == '+' ? 2 : 3).First());
            }
            Console.WriteLine(line.ToString());
        }
    }
}

static double Median(IEnumerable<double> values)
{
    var sorted = values.OrderBy(v => v).ToList();
    return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
}

// A limb is a chain of wood running from where it left its host to its far end — its spine — plus any
// chain that hung off it without ever getting clear of it, which is the same limb doubled rather than a
// branch on it.
sealed record Limb(int Order, int Host, List<(int X, int Y, int Z)> Spine,
                   List<(int X, int Y, int Z)> Doubled, (int X, int Y, int Z) From)
{
    public IEnumerable<(int X, int Y, int Z)> Cells => Spine.Concat(Doubled);
    public int Blocks => Spine.Count + Doubled.Count;
    /// <summary>The limb's far end: the block of it standing furthest from where it left its parent,
    /// which is what a limb's reach and angle are read against.</summary>
    public (int X, int Y, int Z) Tip => Spine.OrderByDescending(c =>
        Sq2(c.X - From.X) + Sq2(c.Y - From.Y) + Sq2(c.Z - From.Z)).First();
    public double Reach => Math.Sqrt(Sq2(Tip.X - From.X) + Sq2(Tip.Y - From.Y) + Sq2(Tip.Z - From.Z));
    public double Angle => Reach < 0.5 ? 0
        : Math.Acos(Math.Clamp((Tip.Y - From.Y) / Reach, -1, 1)) * 180 / Math.PI;
    /// <summary>How far along this limb another one leaves it, 0 at its foot and 1 at its tip.</summary>
    public double Along((int X, int Y, int Z) cell)
    {
        var at = Spine.IndexOf(cell);
        return Spine.Count < 2 || at < 0 ? 0 : at / (double)(Spine.Count - 1);
    }
    private static double Sq2(double value) => value * value;
}

sealed record TreeReading(int Index, int Band, string Timber, HashSet<(int X, int Y, int Z)> Wood,
    Dictionary<(int X, int Y, int Z), int> Strongest,
    Dictionary<(int X, int Y, int Z), (int Face, int Edge, int Corner)> Degree,
    int[] Links, List<int> Pieces, List<int> FacePieces, List<Limb> Limbs,
    Dictionary<(int X, int Y, int Z), int> FromStem, int Doublings, int DoubledBlocks,
    List<(int X, int Y, int Z)> Stem, int Rise, double Lean, int BoleRise, double BoleLean, double Leader,
    double Drift, double Travel, int[] Steps, int Flat, double Girth, bool Grounded);
