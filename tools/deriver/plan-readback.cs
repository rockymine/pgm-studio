#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// plan-readback: run authored *.plan.json files through the real derivers, per labeled box.
// A `buffer` piece acts as a box overlay: every terrain piece whose rect lies fully inside it is read
// as that box's fill — a box containing a room (wool-room / spawn) goes through the approach
// classifier, a terminal-free box (hub, frontline) through the body classifier. The full evaluator
// readout follows, so one run shows both what the geometry IS and what validation fires on it.
// Usage: dotnet run tools/deriver/plan-readback.cs <plan.json> [<plan.json>…]
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Plan;
using PgmStudio.Pgm.Shapes;

if (args.Length == 0)
{
    Console.WriteLine("usage: dotnet run tools/deriver/plan-readback.cs <plan.json> [<plan.json>…]");
    return;
}

foreach (var path in args)
{
    if (PlanModel.Parse(File.ReadAllText(path)) is not { } plan)
    {
        Console.WriteLine($"{Path.GetFileName(path)}: PARSE FAILED");
        continue;
    }
    Console.WriteLine($"── {plan.Meta?.Name ?? "(unnamed)"} ({Path.GetFileName(path)}) ──");

    var boxes = plan.Pieces.Where(p => p.Role == PlanRoles.Buffer).ToList();
    var terrain = plan.Pieces.Where(p => p.Role != PlanRoles.Buffer).ToList();
    if (boxes.Count == 0)
        Console.WriteLine("  (no buffer box overlays — draw a buffer around each box to get per-box reads)");

    bool Inside(int[] inner, int[] outer) =>
        inner[0] >= outer[0] && inner[1] >= outer[1]
        && inner[0] + inner[2] <= outer[0] + outer[2] && inner[1] + inner[3] <= outer[1] + outer[3];

    foreach (var box in boxes)
    {
        var members = terrain.Where(t => Inside(t.Rect, box.Rect)).ToList();
        if (members.Count == 0) { Console.WriteLine($"  {box.Id,-14} (empty)"); continue; }

        var filled = new HashSet<(int, int)>();
        var roomCells = new HashSet<(int, int)>();
        foreach (var p in members)
            for (var x = p.Rect[0]; x < p.Rect[0] + p.Rect[2]; x++)
                for (var z = p.Rect[1]; z < p.Rect[1] + p.Rect[3]; z++)
                {
                    filled.Add((x, z));
                    if (p.Role is PlanRoles.WoolRoom or PlanRoles.Spawn) roomCells.Add((x, z));
                }

        if (roomCells.Count > 0)
        {
            var read = ShapeClassifier.Classify(filled, roomCells);
            Console.WriteLine($"  {box.Id,-14} approach → {read.Family} (w {read.Width}) · {members.Count} piece(s)");
        }
        else
        {
            var body = ShapeClassifier.ClassifyBody(filled);
            var arms = body.Arms > 0 ? $"({body.Arms} arms)" : "";
            Console.WriteLine($"  {box.Id,-14} body     → {body.Form}{arms} · {members.Count} piece(s)");
        }
    }

    var eval = LayoutEvaluator.Evaluate(plan, EvaluationProfile.Default);
    var fired = eval.Terms.Where(t => t.Violation is not null || t.Distance > 0)
        .Select(t => $"{t.TermId}{(t.Kind == TermKind.Hard ? " [HARD]" : $" +{t.Distance:0.##}")}").ToList();
    Console.WriteLine($"  evaluator      score {eval.Score:0.##} · fired: {(fired.Count == 0 ? "none" : string.Join(", ", fired))}");
    Console.WriteLine();
}
