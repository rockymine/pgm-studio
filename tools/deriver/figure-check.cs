#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
// Figure check for the shape model's ASCII catalogs: every labelled grid drawn in docs/generator/model.md §5
// is parsed back out of the document and pushed through the real classifier that names that kind of thing.
// A body figure must read as its Compound (ClassifyBody), a family figure as its ShapeFamily
// (ShapeClassifier.Classify), a negative-space figure as its NegativeSpaceKind (BodyEdges.Classify).
//
// The figures are READ FROM THE DOC, never duplicated here, so this cannot drift from what a reader sees —
// the same reason tools/compose/showcase.cs renders the real generator. Three of the figures were wrong when
// this was first run (a three-armed spine whose baseline stopped a cell short of its third arm read as two
// arms; a Z that doubled back read as a Scythe; an L whose wool was a cut cell read as a Clamp), and none of
// it was catchable by eye.
//
// Grammar it expects inside a fenced block: a label line naming one or more figures across the line, optional
// subtitle lines, then the grid rows, then optional caption lines. A grid row is a line made only of the
// figure alphabet (t = terrain, . = void, w = wool); anything else is prose and is skipped. Figures sitting
// side by side are split at the label line's token columns.
//
// Run: dotnet run tools/deriver/figure-check.cs
// Exits non-zero naming every figure that does not classify as drawn.
using PgmStudio.Pgm.Shapes;

var docPath = Path.Combine(AppContext.BaseDirectory, "../../../../../docs/generator/model.md");
if (!File.Exists(docPath)) docPath = "docs/generator/model.md";
if (!File.Exists(docPath)) { Console.Error.WriteLine("cannot find docs/generator/model.md"); return 2; }

var all = File.ReadAllLines(docPath);

// §5 only — from its heading to the next top-level heading.
var start = Array.FindIndex(all, l => l.StartsWith("## 5."));
if (start < 0) { Console.Error.WriteLine("no '## 5.' heading in model.md"); return 2; }
var end = Array.FindIndex(all, start + 1, l => l.StartsWith("## "));
if (end < 0) end = all.Length;

static bool IsGridRow(string line) =>
    line.Trim().Length > 0 && line.All(c => c is 't' or '.' or 'w' or ' ') && line.Any(c => c is 't' or 'w');

// Figures sit side by side separated by runs of two or more spaces, so a label line splits into one group
// per column. The group's first word is the claim (Rectangle, SpineArms(3), I, notch); the rest of the group
// is descriptive ("notch — 2 walls") and ignored. Splitting on single spaces would read every word of a
// multi-word label as its own column and truncate the grid beneath it.
static List<(int Col, string Label)> Labels(string line)
{
    var found = new List<(int, string)>();
    for (var i = 0; i < line.Length;)
    {
        while (i < line.Length && line[i] == ' ') i++;
        if (i >= line.Length) break;
        var groupStart = i;
        while (i < line.Length && !(line[i] == ' ' && i + 1 < line.Length && line[i + 1] == ' ')) i++;
        var group = line[groupStart..Math.Min(i, line.Length)].Trim();
        var word = group.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (word.Length > 0 && char.IsLetter(word[0])) found.Add((groupStart, word));
    }
    return found;
}

var figures = new List<(string Label, List<string> Rows)>();
var inFence = false;
List<(int Col, string Label)>? cols = null;
List<List<string>>? rows = null;

void Flush()
{
    if (cols is null || rows is null) return;
    for (var c = 0; c < cols.Count; c++)
        if (rows[c].Count > 0) figures.Add((cols[c].Label, rows[c]));
    cols = null; rows = null;
}

for (var i = start; i < end; i++)
{
    var line = all[i];
    if (line.StartsWith("```")) { Flush(); inFence = !inFence; continue; }
    if (!inFence) continue;

    if (IsGridRow(line))
    {
        if (cols is null) continue;                       // a grid with no label above it — nothing to claim
        for (var c = 0; c < cols.Count; c++)
        {
            var from = cols[c].Col;
            var to = c + 1 < cols.Count ? Math.Min(cols[c + 1].Col, line.Length) : line.Length;
            if (from >= line.Length) continue;
            var cell = line[from..to].Trim();
            if (cell.Length > 0) rows![c].Add(cell);
        }
        continue;
    }

    if (line.Trim().Length == 0) continue;                 // blank lines separate label from grid

    // Prose closes any figure already collected. The same line is then re-read as a possible new label row,
    // because one fence often stacks a second rank of figures under the first — dropping it as a caption
    // silently skipped four of the nine families, which looked like a shorter run rather than an error.
    if (rows is not null && rows.Any(r => r.Count > 0)) Flush();
    var labels = Labels(line);
    if (labels.Count == 0) continue;
    if (cols is null) { cols = labels; rows = labels.Select(_ => new List<string>()).ToList(); }
}
Flush();

static IReadOnlySet<(int, int)> Cells(List<string> rows, params char[] want)
{
    var set = new HashSet<(int, int)>();
    for (var z = 0; z < rows.Count; z++)
        for (var x = 0; x < rows[z].Length; x++)
            if (want.Contains(rows[z][x])) set.Add((x, z));
    return set;
}

var failures = 0;
var checkedCount = 0;

foreach (var (label, grid) in figures)
{
    var name = label;
    var arms = 0;
    var paren = name.IndexOf('(');
    if (paren > 0) { int.TryParse(name[(paren + 1)..].TrimEnd(')'), out arms); name = name[..paren]; }

    var hasWool = grid.Any(r => r.Contains('w'));
    string claim, read;
    bool ok;
    try
    {
        if (hasWool && Enum.TryParse<ShapeFamily>(name, ignoreCase: true, out var family))
        {
            var filled = Cells(grid, 't', 'w');
            var wool = Cells(grid, 'w');
            var (got, _) = ShapeClassifier.Classify(filled, wool);
            (claim, read, ok) = (family.ToString(), got.ToString(), got == family);
        }
        else if (Enum.TryParse<Compound>(name, ignoreCase: true, out var compound))
        {
            var got = ShapeClassifier.ClassifyBody(Cells(grid, 't'));
            var armsOk = compound != Compound.SpineArms || got.Arms == arms;
            claim = compound + (arms > 0 ? $"({arms})" : "");
            read = got.Form + (got.Form == Compound.SpineArms ? $"({got.Arms})" : "");
            ok = got.Form == compound && armsOk;
        }
        else if (Enum.TryParse<NegativeSpaceKind>(name, ignoreCase: true, out var kind))
        {
            // the drawn body's negative spaces must include one of the claimed class
            var spaces = BodyEdges.Classify(Cells(grid, 't')).Spaces;
            var kinds = spaces.Select(s => s.Kind).ToList();
            claim = kind.ToString();
            read = kinds.Count == 0 ? "(no space)" : string.Join("/", kinds.Distinct());
            ok = kinds.Contains(kind);
        }
        else continue;                                     // not a figure this tool knows how to judge
    }
    catch (Exception e) { claim = name; read = "threw: " + e.Message; ok = false; }

    checkedCount++;
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "  ok" : "FAIL")}  {claim,-16} -> {read}");
}

Console.WriteLine();
Console.WriteLine(failures == 0
    ? $"all {checkedCount} figures in model.md §5 classify as drawn"
    : $"{failures} of {checkedCount} figures do not classify as drawn");
return failures == 0 ? 0 : 1;
