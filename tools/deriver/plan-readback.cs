#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// plan-readback: run authored *.plan.json files through the real derivers, per authored box.
// Each entry of the plan's `boxes` section is read as one group (PlanBoxes.MembersOf — named members, else
// containment): a box holding a room (wool-room / spawn) goes through the approach classifier, a
// terminal-free box (hub, frontline) through the body classifier. The full evaluator readout follows, so
// one run shows both what the geometry IS and what validation fires on it.
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

    if (plan.Boxes.Count == 0)
        Console.WriteLine("  (no boxes — draw a box around each part to get per-box reads)");

    foreach (var box in plan.Boxes)
    {
        var members = PlanBoxes.MembersOf(plan, box);
        if (members.Count == 0) { Console.WriteLine($"  {box.Id,-12} {box.Kind,-9} (empty)"); continue; }

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
            Console.WriteLine($"  {box.Id,-12} {box.Kind,-9} approach → {read.Family} (w {read.Width}) · {members.Count} piece(s)");
        }
        else
        {
            var body = ShapeClassifier.ClassifyBody(filled);
            var arms = body.Arms > 0 ? $"({body.Arms} arms)" : "";
            Console.WriteLine($"  {box.Id,-12} {box.Kind,-9} body     → {body.Form}{arms} · {members.Count} piece(s)");
        }
    }

    var eval = LayoutEvaluator.Evaluate(plan, EvaluationProfile.Default);
    var fired = eval.Terms.Where(t => t.Violation is not null || t.Distance > 0)
        .Select(t => $"{t.TermId}{(t.Kind == TermKind.Hard ? " [HARD]" : $" +{t.Distance:0.##}")}").ToList();
    Console.WriteLine($"  evaluator    score {eval.Score:0.##} · fired: {(fired.Count == 0 ? "none" : string.Join(", ", fired))}");
    Console.WriteLine();
}
