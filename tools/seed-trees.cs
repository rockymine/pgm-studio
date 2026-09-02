#:project ../src/PgmStudio.Api/PgmStudio.Api.csproj
// A file-based app turns reflection-based JSON off by default, and a recipe's body is serialized that way.
#:property JsonSerializerIsReflectionEnabledByDefault=true
// seed-trees: cut every hand-built tree out of a world and file each one in the tree library as a copied recipe.
//
//   dotnet run tools/seed-trees.cs <worldDir> [name] [--wool] [--dry] [connection string]
//
// <worldDir> holds region/*.mca — a showcase world where every tree stands on its own, clear of every other,
// so a plain connected-component pass over the tree blocks assigns every branch and leaf to one trunk with no
// arbitration. A tree is logs, leaves, and the carpentry an author branches with — wooden slabs, wooden stairs,
// fences and vines; --wool counts wool too, for a corpus that builds a tree out of it. Each tree is normalised to
// its foot — the lowest log, or the lowest block where there is none — and stored as [x, y, z, id, data] rows.
// A body counts as a tree when it rests on something: a solid block that is not tree material within two
// courses under its foot. A leaf cloud or a stray log hanging in the air is a fragment of a tree that broke,
// and is reported rather than filed.
//
// Rows are named <name>-r<row>-<n>: trees are sorted into rows by the z they stand at (a new row opens where the
// gap between one foot and the next is over 20 blocks) and numbered along x inside the row, so a re-run over
// the same world names the same trees, and a row keyed on that name is updated rather than duplicated.
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
    Console.Error.WriteLine("usage: dotnet run tools/seed-trees.cs <worldDir> [name] [--wool] [--dry] [connection]");
    return 2;
}
var worldDir = positional[0];
var name = positional.Count > 1 ? positional[1] : Path.GetFileName(Path.GetFullPath(worldDir).TrimEnd('/'));
var connection = positional.Count > 2 ? positional[2]
    : Environment.GetEnvironmentVariable("PGM_STUDIO_DB")
    ?? "Server=localhost;Database=pgm_studio;User ID=pgm;Password=pgm_dev_pw;";
var withWool = args.Contains("--wool");
var dry = args.Contains("--dry");

// ── the world's tree blocks ─────────────────────────────────────────────────────────────────────────
var regionDir = Directory.Exists(Path.Combine(worldDir, "region")) ? Path.Combine(worldDir, "region") : worldDir;
var body = new Dictionary<(int X, int Y, int Z), (int Id, int Data)>();
var solid = new HashSet<(int X, int Y, int Z)>();
foreach (var chunk in Directory.GetFiles(regionDir, "*.mca").SelectMany(AnvilRegion.ReadChunks))
    foreach (var block in AnvilRegion.Blocks(chunk))
    {
        if (IsTreeBlock(block.Id, withWool)) body[(block.X, block.Y, block.Z)] = (block.Id, block.Data);
        else solid.Add((block.X, block.Y, block.Z));
    }
Console.WriteLine($"{name}: {body.Count} tree blocks in {regionDir}");

// ── one tree per connected component ────────────────────────────────────────────────────────────────
var seen = new HashSet<(int X, int Y, int Z)>();
var trees = new List<List<(int X, int Y, int Z)>>();
foreach (var start in body.Keys)
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
            if (body.ContainsKey(next) && seen.Add(next)) queue.Enqueue(next);
        }
    }
    var logs = cells.Count(cell => IsLog(body[cell].Id));
    if (logs > 0 || cells.Count >= 20) trees.Add(cells);
}

// ── rows by z, numbered along x ─────────────────────────────────────────────────────────────────────
var fragments = 0;
var cut = trees.Select(cells =>
{
    var wood = cells.Where(cell => IsLog(body[cell].Id)).ToList();
    var foot = (wood.Count > 0 ? wood : cells).MinBy(cell => (cell.Y, cell.X, cell.Z));
    return (Foot: foot, Cells: cells);
}).Where(tree =>
{
    var rests = solid.Contains((tree.Foot.X, tree.Foot.Y - 1, tree.Foot.Z))
                || solid.Contains((tree.Foot.X, tree.Foot.Y - 2, tree.Foot.Z));
    if (!rests) fragments++;
    return rests;
}).OrderBy(tree => tree.Foot.Z).ThenBy(tree => tree.Foot.X).ToList();
if (fragments > 0) Console.WriteLine($"  {fragments} body(ies) hang in the air and are left as fragments");

var rows = new List<List<(int X, int Y, int Z)>>();
var row = 0; var last = int.MinValue; var index = 0;
var named = new List<(string Name, (int X, int Y, int Z) Foot, int[][] Body)>();
foreach (var tree in cut)
{
    if (tree.Foot.Z - last > 20) { row++; index = 0; }
    last = tree.Foot.Z;
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
foreach (var (treeName, _, blocks) in named)
{
    var stored = new TreeStyleRow
    {
        Name = treeName,
        Form = "copied",
        Height = blocks.Max(cell => cell[1]) - blocks.Min(cell => cell[1]) + 1,
        Body = JsonSerializer.Serialize(blocks),
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
Console.WriteLine($"\n{added} added, {updated} updated — {named.Count} copied trees under '{name}-r*'");
return 0;

static bool IsLog(int id) => id is Blocks.Log or Blocks.Log2;

static bool IsTreeBlock(int id, bool withWool) =>
    id is Blocks.Log or Blocks.Log2 or Blocks.Leaves or Blocks.Leaves2
    || id is 125 or 126                       // wooden double slab, wooden slab
    || id is 85 or 106                        // fence, vine
    || BlockFamilies.IsStair(id) && id is 53 or 134 or 135 or 136 or 163 or 164
    || withWool && id == Blocks.Wool;
