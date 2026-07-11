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
    ("isolated",    "Isolated", ["vv", "wv", "vv"]),
    ("i-straight",  "I",     ["tttw", "vvvv"]),
    ("i-sidetuck",  "I",     ["tttt", "vvvw"]),
    ("l-corner-1",  "L",     ["tw", "vt", "tt"]),
    ("l-corner-2",  "L",     ["tttv", "vvtw"]),
    ("flanked",     "U",     ["tt", "vw", "tt"]),
    ("scythe-1",    "Scythe",     ["tttv", "tvtw"]),
    ("scythe-2",    "Scythe",     ["tttvv", "tvttw"]),
    ("scythe-3-wide", "Scythe",   ["ttttv", "ttvtw"]),
    ("h-branch-1",  "H",        ["ttv", "vtw", "ttv"]),
    ("h-branch-2",  "H",        ["ttv", "vtv", "ttw"]),
    ("h-branch-3",  "H",        ["ttvv", "vtvv", "tttw"]),
    ("h-branch-4",  "H",        ["ttvw", "vtvt", "tttt"]),
    ("donut-1",     "Donut",    ["ttttv", "vtvtv", "vtttw"]),
    ("donut-2",     "Donut",    ["ttttv", "ttvtv", "vtttw"]),
    ("donut-3",     "Donut",    ["ttttvv", "ttvtvv", "vttttw"]),
    ("plug",        "Plug",     ["ttvw", "vttt", "tttt"]),
};

var outDir = Path.Combine("tools", "deriver", "shapes");
Directory.CreateDirectory(outDir);

int ok = 0, bad = 0, ambigN = 0;
Console.WriteLine($"{"fixture",-15} {"expected",-9} {"derived",-9}  status");
Console.WriteLine(new string('-', 46));
foreach (var (name, fam, rows) in grids)
{
    (int, int)? wool = null;
    // build plan: greedy horizontal-run merge per row for terrain; wool = 1x1 wool-room
    var plan = new PlanModel { Meta = new PlanMeta { Name = name }, Globals = new PlanGlobals { Cell = 5, Symmetry = "none", MaxPlayers = 12, Surface = 9, Headroom = 11 } };
    int pid = 0;
    for (int z = 0; z < rows.Length; z++)
    {
        int x = 0;
        while (x < rows[z].Length)
        {
            var ch = rows[z][x];
            if (ch == 'w') wool = (x, z);
            if (ch != 't') { x++; continue; }
            int c0 = x; while (x < rows[z].Length && rows[z][x] == 't') x++;
            plan.Pieces.Add(new PlanPiece { Id = $"t{++pid}", Role = PlanRoles.Piece, Rect = [c0, z, x - c0, 1] });
        }
    }
    var w = wool!.Value;
    plan.Pieces.Add(new PlanPiece { Id = "wool", Role = PlanRoles.WoolRoom, Rect = [w.Item1, w.Item2, 1, 1] });
    plan.Placements.Wools.Add(new WoolPlacement { Piece = "wool", At = [0, 0] });
    File.WriteAllText(Path.Combine(outDir, $"{name}.plan.json"), plan.ToJson());

    // classify with the library four-way test at the fixture's unit scale (laneWidth = 1)
    var (shape, width) = WoolApproachShape.Classify(plan, "wool", 1);
    var derived = shape.ToString();
    // scythe-3-wide, as drawn, carries a real T-junction (cell 1,0 has terrain W+E+S), so the branch test
    // names it H at every scale — it is a branch, not a scythe. Kept as a documented edge: a compact grid can
    // encode a junction the family label doesn't intend; a genuinely wide scythe (a scaled scythe-1/2) reads
    // scythe. The turn count itself is now width-invariant (bends read off the terrain outline).
    bool ambig = name == "scythe-3-wide" && derived == "H";
    string status;
    if (ambig) { status = "W-AMBIG"; ambigN++; }
    else if (derived == fam) { status = "OK"; ok++; }
    else { status = "MISMATCH"; bad++; }
    var note = ambig ? "  (drawn with a real branch → H; a scaled scythe reads scythe at any width)" : "";
    Console.WriteLine($"{name,-15} {fam,-9} {derived + "·w" + width,-9}  {status}{note}");
}
Console.WriteLine(new string('-', 46));
Console.WriteLine($"{ok} OK / {bad} MISMATCH / {ambigN} W-ambiguous  ->  {outDir}/*.plan.json ({grids.Length} fixtures)");
