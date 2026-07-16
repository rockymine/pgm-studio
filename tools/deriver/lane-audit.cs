#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Wool-lane TRAINING harness (docs/contracts/layout-evaluator.md §5.4, derive-then-override). Runs the deriver's
// WoolLaneShape classifier on every labelled example in tools/deriver/lanes/ and diffs it against the author's
// intended label in labels.json. Agreements confirm the classifier; disagreements are the fix list — either the
// classifier is wrong, or the vocabulary needs a new term. Add examples freely (any label string).
using System.Text.Json;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

var dir = Path.Combine("tools", "deriver", "lanes");
var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(Path.Combine(dir, "labels.json")))!;
var labels = raw.Where(kv => kv.Value.ValueKind == JsonValueKind.String).ToDictionary(kv => kv.Key, kv => kv.Value.GetString()!);

int match = 0, mismatch = 0, unlabeled = 0;
var misses = new List<string>();
Console.WriteLine($"{"example",-24} {"wool",-10} {"author",-10} {"deriver",-12} status");
Console.WriteLine(new string('-', 66));
foreach (var path in Directory.EnumerateFiles(dir, "*.plan.json").OrderBy(p => p, StringComparer.Ordinal))
{
    var name = Path.GetFileName(path);
    name = name[..^".plan.json".Length];
    var plan = PlanModel.Parse(File.ReadAllText(path));
    if (plan is null) { Console.WriteLine($"{name,-24} (unparseable)"); continue; }
    var wools = plan.Placements.Wools;
    if (wools.Count == 0) { Console.WriteLine($"{name,-24} (no wool)"); continue; }
    var author = labels.GetValueOrDefault(name);
    foreach (var w in wools)
    {
        var (read, width) = ShapeClassifier.ClassifyOpen(plan, w.Piece);
        var shape = ShapeClassifier.LaneName(read);
        var derived = width > 0 ? $"{shape}·w{width}" : shape;
        string status;
        if (author is null) { status = "— (unlabeled)"; unlabeled++; }
        else if (string.Equals(author, shape, StringComparison.OrdinalIgnoreCase)) { status = "OK"; match++; }
        else { status = "MISMATCH"; mismatch++; misses.Add($"{name}/{w.Piece}: author={author} deriver={shape}"); }
        Console.WriteLine($"{name,-24} {w.Piece,-10} {author ?? "—",-10} {derived,-12} {status}");
    }
}
Console.WriteLine(new string('-', 66));
Console.WriteLine($"{match} OK · {mismatch} MISMATCH · {unlabeled} unlabeled");
foreach (var m in misses) Console.WriteLine($"  FIX  {m}");
