#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// dress-map: compose a real (large, symmetric) board with the generator and emit its terrain +
// symmetry + objectives as JSON, for the G162 symmetry/fairness prototype to dress in the browser.
// Run from the repo root: dotnet run tools/decorate/dress-map.cs  →  tools/decorate/map.json
using System.Globalization;
using System.Text;
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;
using PgmStudio.Geom;
using Sym = PgmStudio.Geom.Symmetry;

int players = args.Length > 0 ? int.Parse(args[0]) : 24;
ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 7;
var request = new ComposeRequest(playersPerTeam: players, teams: 2, symmetry: "rot_180", seed: seed);
var stages = Composer.ComposeStages(request);
var env = stages.Envelope;
var unit = stages.Unit;
var order = Sym.Order(env.Symmetry);
var axes = Sym.OrbitAxes(env.Symmetry);

// tag priority: spawn(3) > wool room(2) > mid band(1) > land(0)
var grid = new Dictionary<(int, int), int>();
void Put(int x, int z, int tag) { if (!grid.TryGetValue((x, z), out var t) || tag > t) grid[(x, z)] = tag; }
CellRect Fan(CellRect r, int k)
{
    if (k == 0) return r;
    (double x, double z)[] c = [(r.X, r.Z), (r.X, r.MaxZ), (r.MaxX, r.Z), (r.MaxX, r.MaxZ)];
    var p = c.Select(q => Sym.Apply(q.x, q.z, axes[k - 1], 0, 0)).ToList();
    int x1 = (int)Math.Round(p.Min(q => q.X)), z1 = (int)Math.Round(p.Min(q => q.Z));
    return new CellRect(x1, z1, (int)Math.Round(p.Max(q => q.X)) - x1, (int)Math.Round(p.Max(q => q.Z)) - z1);
}
void Stamp(CellRect r, int tag) { for (var k = 0; k < order; k++) { var f = Fan(r, k); for (var x = f.X; x < f.MaxX; x++) for (var z = f.Z; z < f.MaxZ; z++) Put(x, z, tag); } }

foreach (var p in unit.Pieces) Stamp(p.Rect, 0);                                             // land
Stamp(stages.Mid.BandRect, 1);                                                                // mid band
foreach (var p in unit.Pieces.Where(p => p.Box?.Kind == BoxKind.Wool)) Stamp(p.Rect, 2);      // wool rooms
foreach (var p in unit.Pieces.Where(p => p.Role == PlanRoles.Spawn)) Stamp(p.Rect, 3);        // spawn rooms

// markers (board coords), fanned across the orbit
(double X, double Z) Dir(string facing) => facing switch { "north" => (0, -1), "south" => (0, 1), "east" => (1, 0), "west" => (-1, 0), _ => (0, 0) };
var spawns = new List<(double, double, double, double)>();
var sp = unit.Pieces.First(p => p.Id == unit.Spawn.Piece);
double sx = sp.Rect.X + unit.Spawn.At[0], sz = sp.Rect.Z + unit.Spawn.At[1];
var (fx, fz) = Dir(unit.Spawn.Facing);
for (var k = 0; k < order; k++)
{
    var (mx, mz) = k == 0 ? (sx, sz) : Sym.Apply(sx, sz, axes[k - 1], 0, 0);
    var (dx, dz) = k == 0 ? (fx, fz) : Sym.Apply(fx, fz, axes[k - 1], 0, 0);
    spawns.Add((mx, mz, dx, dz));
}
var wools = new List<(double, double)>();
foreach (var w in unit.Wools)
{
    var pc = unit.Pieces.First(p => p.Id == w.Piece);
    double wx = pc.Rect.X + w.At[0], wz = pc.Rect.Z + w.At[1];
    for (var k = 0; k < order; k++) { var (mx, mz) = k == 0 ? (wx, wz) : Sym.Apply(wx, wz, axes[k - 1], 0, 0); wools.Add((mx, mz)); }
}

int minX = grid.Keys.Min(c => c.Item1) - 3, minZ = grid.Keys.Min(c => c.Item2) - 3;
int maxX = grid.Keys.Max(c => c.Item1) + 4, maxZ = grid.Keys.Max(c => c.Item2) + 4;
int gw = maxX - minX, gh = maxZ - minZ;
string F(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);

var sb = new StringBuilder();
sb.Append("{\n");
sb.Append($"\"sym\":\"{env.Symmetry}\",\"cell\":{env.Cell},\"minX\":{minX},\"minZ\":{minZ},\"w\":{gw},\"h\":{gh},\n");
sb.Append("\"grid\":[");
for (var z = minZ; z < maxZ; z++)
{
    var row = new StringBuilder();
    for (var x = minX; x < maxX; x++)
        row.Append(grid.TryGetValue((x, z), out var t) ? (t == 3 ? 'S' : t == 2 ? 'W' : t == 1 ? 'B' : 'L') : '.');
    sb.Append(z > minZ ? "," : "").Append('"').Append(row).Append('"');
}
sb.Append("],\n");
sb.Append("\"spawns\":[").Append(string.Join(",", spawns.Select(s => $"[{F(s.Item1)},{F(s.Item2)},{F(s.Item3)},{F(s.Item4)}]"))).Append("],\n");
sb.Append("\"wools\":[").Append(string.Join(",", wools.Select(s => $"[{F(s.Item1)},{F(s.Item2)}]"))).Append("]\n");
sb.Append("}\n");
File.WriteAllText("tools/decorate/map.json", sb.ToString());
Console.WriteLine($"sym={env.Symmetry} order={order} grid={gw}x{gh} pieces={unit.Pieces.Count} wools={unit.Wools.Count} spawns={spawns.Count}");
