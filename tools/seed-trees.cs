#:project ../src/PgmStudio.Api/PgmStudio.Api.csproj
// A file-based app turns reflection-based JSON off by default, and a recipe's body is serialized that way.
#:property JsonSerializerIsReflectionEnabledByDefault=true
// It runs under `dotnet run` and is never published ahead-of-time, so the AOT analysers have nothing to guard.
#:property PublishAot=false
// seed-trees: cut every hand-built tree out of a world and file each one in the tree library as a copied recipe.
//
//   dotnet run tools/seed-trees.cs <worldDir> [name] [--builder=<name>] [--wool] [--dry] [connection string]
//
// <worldDir> holds region/*.mca — a showcase world where every tree stands on its own, clear of every other,
// so a connected-component pass over the tree blocks finds each trunk with its branches and leaves. A hand-built
// crown also carries a tip or two clear of its own wood, which no 26-connected step reaches; each joins the
// standing tree whose blocks come nearest it, within four blocks. A tree is logs, leaves, and the carpentry an author
// builds and branches with — planks, wooden slabs, wooden stairs, fences and vines; --wool counts wool too, for
// a corpus that builds a tree out of it. A plank is tree above the world's lowest course and platform on it, which
// is where a showcase lays its platforms. Each tree is normalised to its foot — its lowest wood nearest the
// trunk, a plank where the trunk stands on one, or the lowest block where there is no log — and stored as
// [x, y, z, id, data] rows, with the cut recorded beside them: the world directory, the foot's world coordinates,
// the time of the run and, with --builder, who built the world's trees — a map a copied tree stands on credits
// them as a contributor for its trees.
// The cut is what makes a row `copied`; the library refuses that form to any save without one (DR-COPY).
// A body counts as a tree when it rests on something: a solid block that is not tree material within two
// courses under its foot. A piece with no tree within reach is a fragment, and is reported rather than filed.
//
// Rows are named <name>-r<row>-<n>: trees are sorted into rows by the z they stand at (a new row opens where the
// gap between one foot and the next is over 20 blocks) and numbered along x inside the row, so a re-run over
// the same world names the same trees, and a row keyed on that name is updated rather than duplicated.
// A row opens for a wool tree as well, whether or not --wool files it, so the numbering is the world's rows
// rather than the run's and one flag does not renumber every row behind it.
// The connection string falls back to PGM_STUDIO_DB, then to the local dev database.
using System.Text.Json;
using PgmStudio.Data;
using PgmStudio.Data.Schema;
using PgmStudio.Data.Theme;
using PgmStudio.Migrations;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

var positional = args.Where(arg => !arg.StartsWith("--")).ToList();
if (positional.Count == 0)
{
    Console.Error.WriteLine("usage: dotnet run tools/seed-trees.cs <worldDir> [name] [--builder=<name>] [--wool] [--dry] [connection]");
    return 2;
}
var worldDir = positional[0];
var name = positional.Count > 1 ? positional[1] : Path.GetFileName(Path.GetFullPath(worldDir).TrimEnd('/'));
var connection = positional.Count > 2 ? positional[2]
    : Environment.GetEnvironmentVariable("PGM_STUDIO_DB")
    ?? "Server=localhost;Database=pgm_studio;User ID=pgm;Password=pgm_dev_pw;";
var withWool = args.Contains("--wool");
var builder = args.Where(arg => arg.StartsWith("--builder=")).Select(arg => arg["--builder=".Length..].Trim())
    .LastOrDefault(named => named.Length > 0);
var dry = args.Contains("--dry");

// ── the world's tree blocks ─────────────────────────────────────────────────────────────────────────
var regionDir = Directory.Exists(Path.Combine(worldDir, "region")) ? Path.Combine(worldDir, "region") : worldDir;
var body = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
var wool = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
var solid = new HashSet<(int X, int Y, int Z)>();
var world = Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks)
    .SelectMany(chunk => AnvilRegion.Blocks(chunk)).ToList();
// The course the platforms are laid on: the world's lowest. A plank on it is platform; a plank over it is tree.
var floor = world.Min(block => block.Y);
foreach (var block in world)
{
    if (IsTreeBlock(block.Id, withWool) || block.Id == Blocks.Planks && block.Y > floor)
        body[(block.X, block.Y, block.Z)] = (block.Id, block.Data);
    else
    {
        solid.Add((block.X, block.Y, block.Z));
        // Wool the run does not file is ground, and is kept apart so a wool tree can still open its row.
        if (block.Id == Blocks.Wool) wool[(block.X, block.Y, block.Z)] = (block.Id, block.Data);
    }
}
Console.WriteLine($"{name}: {body.Count} tree blocks in {regionDir}");

// ── one tree per connected component ────────────────────────────────────────────────────────────────
List<List<(int X, int Y, int Z)>> Bodies(Dictionary<(int X, int Y, int Z), (int Id, int Data)> blocks)
{
    var seen = new HashSet<(int X, int Y, int Z)>();
    var found = new List<List<(int X, int Y, int Z)>>();
    foreach (var start in blocks.Keys)
    {
        if (!seen.Add(start)) continue;
        var cells = new List<(int X, int Y, int Z)>();
        var queue = new Queue<(int X, int Y, int Z)>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            cells.Add(cell);
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
            {
                var next = (cell.X + dx, cell.Y + dy, cell.Z + dz);
                if (blocks.ContainsKey(next) && seen.Add(next)) queue.Enqueue(next);
            }
        }
        found.Add(cells);
    }
    return found;
}

// ── standing trees, and the crown pieces that hang clear of them ────────────────────────────────────
// A tree stands when it holds a log (or is big enough to be one) and rests on something. Everything else a
// pass finds is a piece of crown a hand-built tree carries clear of its own wood — a tip no 26-connected step
// reaches — and it belongs to the standing tree whose blocks come nearest it. A piece
// with no tree within reach is a fragment, reported rather than filed.
// A tree's foot is its lowest wood — a log, or a plank a trunk stands on — nearest the trunk's own column.
(int X, int Y, int Z) FootOf(List<(int X, int Y, int Z)> cells, Dictionary<(int X, int Y, int Z), (int Id, int Data)> blocks)
{
    var logs = cells.Where(cell => IsLog(blocks[cell].Id)).ToList();
    if (logs.Count == 0) return cells.MinBy(cell => (cell.Y, cell.X, cell.Z));
    var trunk = logs.MinBy(cell => (cell.Y, cell.X, cell.Z));
    var wood = cells.Where(cell => IsLog(blocks[cell].Id) || blocks[cell].Id == Blocks.Planks).ToList();
    var lowest = wood.Min(cell => cell.Y);
    return wood.Where(cell => cell.Y == lowest)
        .MinBy(cell => (Math.Abs(cell.X - trunk.X) + Math.Abs(cell.Z - trunk.Z), cell.X, cell.Z));
}
bool Rests((int X, int Y, int Z) foot) =>
    solid.Contains((foot.X, foot.Y - 1, foot.Z)) || solid.Contains((foot.X, foot.Y - 2, foot.Z));

const int CrownReach = 4;
var standing = new List<(List<(int X, int Y, int Z)> Cells, (int X, int Y, int Z) Foot)>();
var hanging = new List<List<(int X, int Y, int Z)>>();
foreach (var cells in Bodies(body))
{
    var foot = FootOf(cells, body);
    if ((cells.Any(cell => IsLog(body[cell].Id)) || cells.Count >= 20) && Rests(foot)) standing.Add((cells, foot));
    else hanging.Add(cells);
}
foreach (var piece in hanging)
{
    var nearest = standing
        .Select(tree => (Tree: tree, Gap: Gap(piece, tree.Cells)))
        .Where(tree => tree.Gap <= CrownReach)
        .OrderBy(tree => tree.Gap)
        .FirstOrDefault();
    if (nearest.Tree.Cells is null)
    {
        var at = piece.MinBy(cell => (cell.Y, cell.X, cell.Z));
        Console.WriteLine($"  {piece.Count,3} block(s) from ({at.X},{at.Y},{at.Z}) reach no tree and are left as a fragment");
        continue;
    }
    nearest.Tree.Cells.AddRange(piece);
    var low = piece.MinBy(cell => (cell.Y, cell.X, cell.Z));
    Console.WriteLine($"  {piece.Count,3} block(s) of crown from ({low.X},{low.Y},{low.Z}) join the tree at " +
                      $"({nearest.Tree.Foot.X},{nearest.Tree.Foot.Y},{nearest.Tree.Foot.Z}), {nearest.Gap} block(s) clear of it");
}

var woolTrees = Bodies(wool).Where(cells => cells.Count >= 20)
    .Select(cells => (Cells: cells, Foot: FootOf(cells, wool)))
    .Where(tree => Rests(tree.Foot));

// ── rows by z, numbered along x ─────────────────────────────────────────────────────────────────────
var cut = standing.Select(tree => (tree.Foot, tree.Cells, Filed: true))
    .Concat(woolTrees.Select(tree => (tree.Foot, tree.Cells, Filed: false)))
    .OrderBy(tree => tree.Foot.Z).ThenBy(tree => tree.Foot.X).ToList();
var unfiled = cut.Count(tree => !tree.Filed);
if (unfiled > 0) Console.WriteLine($"  {unfiled} wool tree(s) hold a row of their own; --wool files them");

var row = 0; var last = int.MinValue; var index = 0;
var named = new List<(string Name, (int X, int Y, int Z) Foot, int[][] Body)>();
foreach (var tree in cut)
{
    if (tree.Foot.Z - last > 20) { row++; index = 0; }
    last = tree.Foot.Z;
    if (!tree.Filed) continue;
    index++;
    var rowsOfTree = tree.Cells
        .OrderBy(cell => cell.Y).ThenBy(cell => cell.Z).ThenBy(cell => cell.X)
        .Select(cell => new[] { cell.X - tree.Foot.X, cell.Y - tree.Foot.Y, cell.Z - tree.Foot.Z, body[cell].Id, body[cell].Data })
        .ToArray();
    named.Add(($"{name}-r{row}-{index}", tree.Foot, rowsOfTree));
}
// Numbered along x within a row: the sort above ran along z first, so renumber each row by x.
named = named
    .GroupBy(tree => tree.Name[..tree.Name.LastIndexOf('-')])
    .SelectMany(group => group.OrderBy(tree => tree.Foot.X).Select((tree, at) => ($"{group.Key}-{at + 1}", tree.Foot, tree.Body)))
    .ToList();

foreach (var (treeName, foot, blocks) in named)
{
    var height = blocks.Max(cell => cell[1]) - blocks.Min(cell => cell[1]) + 1;
    var logs = blocks.Count(cell => IsLog(cell[3]));
    var leaves = blocks.Count(cell => cell[3] is Blocks.Leaves or Blocks.Leaves2);
    Console.WriteLine($"  {treeName,-24} foot ({foot.X,4},{foot.Y,3},{foot.Z,5})  {blocks.Length,4} blocks  {height,2} tall  {logs,3} logs  {leaves,4} leaves");
}
if (dry) return 0;

// ── into the library, by name ───────────────────────────────────────────────────────────────────────
var state = SchemaMigrator.GetSchemaState(connection);
if (state.Pending.Count > 0)
{
    Console.WriteLine($"applying {state.Pending.Count} pending migration(s) …");
    SchemaMigrator.MigrateUp(connection);
}
await using var db = new PgmDb(PgmDataOptions.ForConnectionString(connection));
var store = new PropStyleStore(db);
var existing = (await store.ListTreesAsync()).ToDictionary(r => r.Name, r => r, StringComparer.Ordinal);
int added = 0, updated = 0;
var cutFrom = Path.GetFullPath(worldDir).TrimEnd('/');
var cutAt = DateTime.UtcNow;
foreach (var (treeName, foot, blocks) in named)
{
    var stored = new TreeStyleRow
    {
        Name = treeName,
        Form = "copied",
        Height = blocks.Max(cell => cell[1]) - blocks.Min(cell => cell[1]) + 1,
        Body = JsonSerializer.Serialize(blocks),
        CutWorld = cutFrom,
        CutX = foot.X,
        CutY = foot.Y,
        CutZ = foot.Z,
        CutAt = cutAt,
        CutBuilder = builder,
    };
    if (existing.TryGetValue(treeName, out var have))
    {
        await store.UpdateTreeAsync(have.Id, stored);
        updated++;
    }
    else
    {
        await store.CreateTreeAsync(stored);
        added++;
    }
}
Console.WriteLine($"\n{added} added, {updated} updated — {named.Count} copied trees under '{name}-r*'"
    + (builder is null ? ", built by nobody named" : $", built by {builder}"));
return 0;

static bool IsLog(int id) => id is Blocks.Log or Blocks.Log2;

// The widest gap a piece of crown may hang clear of its tree across: the blocks between the piece and the
// tree's nearest block, counted the way a 26-connected step counts them.
static int Gap(List<(int X, int Y, int Z)> piece, List<(int X, int Y, int Z)> tree)
{
    var gap = int.MaxValue;
    foreach (var a in piece)
        foreach (var b in tree)
            gap = Math.Min(gap, Math.Max(Math.Abs(a.X - b.X), Math.Max(Math.Abs(a.Y - b.Y), Math.Abs(a.Z - b.Z))) - 1);
    return gap;
}

static bool IsTreeBlock(int id, bool withWool) =>
    id is Blocks.Log or Blocks.Log2 or Blocks.Leaves or Blocks.Leaves2
    || id is 125 or 126                       // wooden double slab, wooden slab
    || id is 85 or 106                        // fence, vine
    || BlockFamilies.IsStair(id) && id is 53 or 134 or 135 or 136 or 163 or 164
    || withWool && id == Blocks.Wool;
