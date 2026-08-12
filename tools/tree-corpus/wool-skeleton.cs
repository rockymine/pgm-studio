#:project ../../src/PgmStudio.Minecraft/PgmStudio.Minecraft.csproj
// The wool tree read as a skeleton. Each colour is one limb, so the branching the author draws by hand can
// be measured directly: what attaches to what, where along the parent, at what angle, and how fast a limb
// gives up its length.
using PgmStudio.Minecraft;

var regionDir = args.Length > 0 ? args[0] : "tools/tree-corpus/tree-showcase/region";
var wool = new Dictionary<(int X, int Y, int Z), int>();
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
                if (ids[(y << 8) | col] == 35)
                    wool[(chunk.ChunkX * 16 + lx, y, chunk.ChunkZ * 16 + lz)] = data[(y << 8) | col];
        }
}

// One colour can be reused for two limbs that never touch, so a limb is a connected run of one colour.
var limbs = new List<(int Colour, List<(int X, int Y, int Z)> Cells)>();
var pending = new HashSet<(int X, int Y, int Z)>(wool.Keys);
while (pending.Count > 0)
{
    var seed = pending.First();
    pending.Remove(seed);
    var cells = new List<(int X, int Y, int Z)> { seed };
    var queue = new Queue<(int X, int Y, int Z)>([seed]);
    while (queue.Count > 0)
    {
        var cell = queue.Dequeue();
        for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = (cell.X + dx, cell.Y + dy, cell.Z + dz);
                    if (wool.TryGetValue(next, out var colour) && colour == wool[seed] && pending.Remove(next))
                    {
                        cells.Add(next);
                        queue.Enqueue(next);
                    }
                }
    }
    limbs.Add((wool[seed], cells));
}

Console.WriteLine($"{wool.Count} wool blocks → {limbs.Count} limbs in " +
    $"{wool.Values.Distinct().Count()} colours (a colour reused where two limbs never touch)");

// The trunk is the limb standing lowest; every other limb hangs off whichever limb it touches first.
var order = limbs.OrderBy(l => l.Cells.Min(c => c.Y)).ThenByDescending(l => l.Cells.Count).ToList();
var parent = new int[order.Count];
var attach = new (int X, int Y, int Z)[order.Count];
var depth = new int[order.Count];
Array.Fill(parent, -1);

for (var i = 1; i < order.Count; i++)
{
    var best = (Parent: -1, Distance: int.MaxValue, Cell: (0, 0, 0));
    for (var j = 0; j < order.Count; j++)
    {
        if (i == j) continue;
        foreach (var mine in order[i].Cells)
            foreach (var theirs in order[j].Cells)
            {
                var gap = Math.Max(Math.Abs(mine.X - theirs.X),
                          Math.Max(Math.Abs(mine.Y - theirs.Y), Math.Abs(mine.Z - theirs.Z)));
                // Prefer the limb that is touching, then the one whose contact sits lowest — a branch
                // leaves its parent, so its parent is the older wood underneath it.
                if (gap <= 1 && (theirs.Y < best.Distance || best.Parent < 0))
                {
                    var candidate = order[j].Cells.Min(c => c.Y);
                    if (best.Parent < 0 || candidate < best.Distance)
                        best = (j, candidate, mine);
                }
            }
    }
    parent[i] = best.Parent;
    attach[i] = best.Cell;
}
for (var i = 0; i < order.Count; i++)
{
    var walk = i;
    var steps = 0;
    while (parent[walk] >= 0 && steps < 20) { walk = parent[walk]; steps++; }
    depth[i] = steps;
}

string[] names = ["white", "orange", "magenta", "light blue", "yellow", "lime", "pink", "gray",
                  "light gray", "cyan", "purple", "blue", "brown", "green", "red", "black"];

static double Length((int X, int Y, int Z) from, (int X, int Y, int Z) to) =>
    Math.Sqrt(Sq(to.X - from.X) + Sq(to.Y - from.Y) + Sq(to.Z - from.Z));
static double Sq(double v) => v * v;

Console.WriteLine($"\n{"limb",-12} {"blocks",7} {"order",6} {"parent",-12} {"attach y",9} {"reach",6} " +
    $"{"rise",6} {"angle",7}  span");
var reachOf = new double[order.Count];
for (var i = 0; i < order.Count; i++)
{
    var cells = order[i].Cells;
    var root = i == 0 ? cells.OrderBy(c => c.Y).First() : attach[i];
    var tip = cells.OrderByDescending(c => Length(root, c)).First();
    var reach = Length(root, tip);
    reachOf[i] = reach;
    var rise = tip.Y - root.Y;
    // Angle from vertical: 0 is a spire, 90 a limb held straight out.
    var angle = reach < 0.5 ? 0 : Math.Acos(Math.Clamp(rise / reach, -1, 1)) * 180 / Math.PI;
    Console.WriteLine($"{names[order[i].Colour],-12} {cells.Count,7} {depth[i],6} " +
        $"{(parent[i] < 0 ? "— (trunk)" : names[order[parent[i]].Colour]),-12} {(i == 0 ? root.Y : attach[i].Y),9} " +
        $"{reach,6:0.0} {rise,6} {angle,6:0}°  " +
        $"{cells.Max(c => c.X) - cells.Min(c => c.X) + 1}x{cells.Max(c => c.Y) - cells.Min(c => c.Y) + 1}x{cells.Max(c => c.Z) - cells.Min(c => c.Z) + 1}");
}

Console.WriteLine($"\n── the shape of the branching ──");
foreach (var level in Enumerable.Range(0, depth.Max() + 1))
{
    var atLevel = Enumerable.Range(0, order.Count).Where(i => depth[i] == level).ToList();
    if (atLevel.Count == 0) continue;
    Console.WriteLine($"  order {level}: {atLevel.Count,2} limb(s), " +
        $"{atLevel.Average(i => order[i].Cells.Count),5:0.0} blocks each, " +
        $"reach {atLevel.Average(i => reachOf[i]),4:0.0}, " +
        $"held {atLevel.Where(i => depth[i] > 0).DefaultIfEmpty(-1).Average(i => i < 0 ? 0 : Angle(i)),3:0}° off vertical");
    double Angle(int i)
    {
        var tip = order[i].Cells.OrderByDescending(c => Length(attach[i], c)).First();
        var reach = Length(attach[i], tip);
        return reach < 0.5 ? 0 : Math.Acos(Math.Clamp((tip.Y - attach[i].Y) / reach, -1, 1)) * 180 / Math.PI;
    }
}
var children = new int[order.Count];
for (var i = 1; i < order.Count; i++) if (parent[i] >= 0) children[parent[i]]++;
Console.WriteLine($"  branches leaving the trunk: {children[0]}");
Console.WriteLine($"  a limb that carries children carries {children.Where(c => c > 0).Average():0.0} on average");
Console.WriteLine($"  a child keeps {Enumerable.Range(1, order.Count - 1).Where(i => parent[i] >= 0 && reachOf[parent[i]] > 0).Average(i => reachOf[i] / reachOf[parent[i]]):0.00} of its parent's reach");
