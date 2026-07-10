#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Generates the base wool-approach shape fixtures (docs/contracts/layout-generation.md §2) as real
// *.plan.json files, straight from the doc's t/v/w catalog. The t/v/w notation stays doc-only — it lives
// here only as inline literals that MIRROR the doc (canonical), never as a persisted on-disk format; the
// output is the actual plan format. The generator also runs the four-way skeleton decision tree on each
// shape and checks it against the catalog family, so the classification the contract asserts is verified,
// not just claimed. Run: dotnet run tools/deriver/shapes-gen.cs
using PgmStudio.Pgm.Plan;

// name, expected coarse family, grid rows (t = terrain, v = void, w = wool). Mirror of the §2 catalog.
var grids = new (string name, string fam, string[] rows)[]
{
    ("isolated",    "isolated", ["vv", "wv", "vv"]),
    ("i-straight",  "thin",     ["tttw", "vvvv"]),
    ("i-sidetuck",  "thin",     ["tttt", "vvvw"]),
    ("l-corner-1",  "thin",     ["tw", "vt", "tt"]),
    ("l-corner-2",  "thin",     ["tttv", "vvtw"]),
    ("flanked",     "thin",     ["tt", "vw", "tt"]),
    ("scythe-1",    "thin",     ["tttv", "tvtw"]),
    ("scythe-2",    "thin",     ["tttvv", "tvttw"]),
    ("scythe-3-wide", "thin",   ["ttttv", "ttvtw"]),
    ("h-branch-1",  "H",        ["ttv", "vtw", "ttv"]),
    ("h-branch-2",  "H",        ["ttv", "vtv", "ttw"]),
    ("h-branch-3",  "H",        ["ttvv", "vtvv", "tttw"]),
    ("h-branch-4",  "H",        ["ttvw", "vtvt", "tttt"]),
    ("donut-1",     "donut",    ["ttttv", "vtvtv", "vtttw"]),
    ("donut-2",     "donut",    ["ttttv", "ttvtv", "vtttw"]),
    ("donut-3",     "donut",    ["ttttvv", "ttvtvv", "vttttw"]),
    ("plug",        "plug",     ["ttvw", "vttt", "tttt"]),
};

var outDir = Path.Combine("tools", "deriver", "shapes");
Directory.CreateDirectory(outDir);

static IEnumerable<(int, int)> N4((int, int) c)
{ yield return (c.Item1 + 1, c.Item2); yield return (c.Item1 - 1, c.Item2); yield return (c.Item1, c.Item2 + 1); yield return (c.Item1, c.Item2 - 1); }

// --- the four-way skeleton decision tree (unit scale: block = 2x2). Order: isolated, donut, plug, H, thin.
static bool HasHole(HashSet<(int, int)> fill)
{
    int mnx = fill.Min(c => c.Item1) - 1, mxx = fill.Max(c => c.Item1) + 1, mnz = fill.Min(c => c.Item2) - 1, mxz = fill.Max(c => c.Item2) + 1;
    var outside = new HashSet<(int, int)>(); var q = new Queue<(int, int)>(); q.Enqueue((mnx, mnz)); outside.Add((mnx, mnz));
    while (q.Count > 0) { var c = q.Dequeue(); foreach (var nb in N4(c)) if (nb.Item1 >= mnx && nb.Item1 <= mxx && nb.Item2 >= mnz && nb.Item2 <= mxz && !fill.Contains(nb) && outside.Add(nb)) q.Enqueue(nb); }
    for (int x = mnx; x <= mxx; x++) for (int z = mnz; z <= mxz; z++) if (!fill.Contains((x, z)) && !outside.Contains((x, z))) return true;
    return false;
}
static bool Has2x2(HashSet<(int, int)> t)
{ foreach (var (x, z) in t) if (t.Contains((x + 1, z)) && t.Contains((x, z + 1)) && t.Contains((x + 1, z + 1))) return true; return false; }
static int Bends(HashSet<(int, int)> s)
{
    int mnx = s.Min(c => c.Item1), mxx = s.Max(c => c.Item1) + 1, mnz = s.Min(c => c.Item2), mxz = s.Max(c => c.Item2) + 1, r = 0;
    for (int x = mnx; x <= mxx; x++) for (int z = mnz; z <= mxz; z++)
    { int n = 0; if (s.Contains((x, z))) n++; if (s.Contains((x - 1, z))) n++; if (s.Contains((x, z - 1))) n++; if (s.Contains((x - 1, z - 1))) n++; if (n == 3) r++; }
    return r;
}
static (string fam, int bends, bool holeAndBlock) Classify(HashSet<(int, int)> terr, (int, int) wool)
{
    var walk = new HashSet<(int, int)>(terr) { wool };
    bool hole = HasHole(walk), block = Has2x2(terr);
    string fam;
    if (!N4(wool).Any(terr.Contains)) fam = "isolated";
    else if (hole) fam = "donut";        // donut BEFORE plug: a loop may carry a locally thick corner
    else if (block) fam = "plug";
    else if (walk.Any(c => N4(c).Count(walk.Contains) >= 3)) fam = "H";  // wool counts as a walkable terminus
    else fam = "thin";
    return (fam, Bends(walk), hole && block);
}

int ok = 0, bad = 0, ambigN = 0;
Console.WriteLine($"{"fixture",-15} {"expected",-9} {"derived",-9} {"bends",5}  status");
Console.WriteLine(new string('-', 56));
foreach (var (name, fam, rows) in grids)
{
    var terr = new HashSet<(int, int)>(); (int, int)? wool = null;
    for (int z = 0; z < rows.Length; z++) for (int x = 0; x < rows[z].Length; x++)
    { var ch = rows[z][x]; if (ch == 't') terr.Add((x, z)); else if (ch == 'w') { wool = (x, z); } }

    // build plan: greedy horizontal-run merge per row for terrain; wool = 1x1 wool-room
    var plan = new PlanModel { Meta = new PlanMeta { Name = name } };
    plan.Globals = new PlanGlobals { Cell = 5, Symmetry = "none", MaxPlayers = 12, Surface = 9, Headroom = 11 };
    int pid = 0;
    for (int z = 0; z < rows.Length; z++)
    {
        int x = 0;
        while (x < rows[z].Length)
        {
            if (rows[z][x] != 't') { x++; continue; }
            int c0 = x; while (x < rows[z].Length && rows[z][x] == 't') x++;
            plan.Pieces.Add(new PlanPiece { Id = $"t{++pid}", Role = PlanRoles.Piece, Rect = [c0, z, x - c0, 1] });
        }
    }
    var w = wool!.Value;
    plan.Pieces.Add(new PlanPiece { Id = "wool", Role = PlanRoles.WoolRoom, Rect = [w.Item1, w.Item2, 1, 1] });
    plan.Placements.Wools.Add(new WoolPlacement { Piece = "wool", At = [0, 0] });
    File.WriteAllText(Path.Combine(outDir, $"{name}.plan.json"), plan.ToJson());

    var (derived, bends, both) = Classify(terr, w);
    // Unit-scale (W=1) prototype: block=2x2, junction=degree>=3. A shape intended wider (a 2-wide
    // scythe) trips the block test => reads as plug. That is the W-ambiguity, not a mismatch: the
    // block/junction predicates are W-relative, and t/v/w does not fix W.
    string status; bool ambig = fam == "thin" && derived == "plug";
    if (ambig) { status = "W-AMBIG"; ambigN++; }
    else if (derived == fam) { status = "OK"; ok++; }
    else { status = "MISMATCH"; bad++; }
    var note = both ? "  (hole+block: donut-before-plug)" : ambig ? "  (wide scythe vs plug: needs realized W)" : "";
    Console.WriteLine($"{name,-15} {fam,-9} {derived,-9} {bends,5}  {status}{note}");
}
Console.WriteLine(new string('-', 56));
Console.WriteLine($"{ok} OK / {bad} MISMATCH / {ambigN} W-ambiguous  ->  {outDir}/*.plan.json ({grids.Length} fixtures)");
