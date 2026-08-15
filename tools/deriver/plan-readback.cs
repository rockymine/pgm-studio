#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
// plan-readback: run authored *.plan.json files through the real derivers, per authored box, and ask of each
// whether the composer could have produced it.
// Each entry of the plan's `boxes` section is read as one group (PlanBoxes.MembersOf — named members, else
// containment). Per box: what the derivers READ it as, then the producibility verdict (Producibility — the
// declared parameter menus emitted by the real emitters and compared) with its directed findings. The
// evaluator readout follows, so one run shows the gap between "legal to author" and "buildable by the
// machine".
// Usage: dotnet run tools/deriver/plan-readback.cs <plan.json> [<plan.json>…]
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Plan;

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

    var read = Producibility.ReadPlan(plan);
    foreach (var b in read.Boxes)
    {
        var verdict = b.Producible is { } p ? $"PRODUCIBLE as {p.Label} (cw {p.Cw})" : "NOT PRODUCIBLE";
        Console.WriteLine($"  {b.BoxId,-12} {b.Kind,-9} reads {b.Identity,-18} {verdict}");
        if (b.Nearest is { } n)
            Console.WriteLine($"    nearest    {n.Label} (cw {n.Cw}) — {n.DifferingCells} cell(s) differ " +
                              $"({n.Extra.Count} extra, {n.Missing.Count} missing)");
        foreach (var f in b.Findings)
            Console.WriteLine($"    {f.Rule}{(f.Cites is null ? "" : $" [{f.Cites}]")}: {f.Message}");
    }
    // the unit-level rules — properties of the arrangement, reported alongside the per-box reads (both matter:
    // a box can be unbuildable on its own geometry AND the unit unbuildable in how it is arranged)
    foreach (var f in read.Unit)
        Console.WriteLine($"  [unit] {f.Rule}{(f.Cites is null ? "" : $" [{f.Cites}]")}: {f.Message}");

    var eval = LayoutEvaluator.Evaluate(plan, EvaluationProfile.Default);
    var fired = eval.Terms.Where(t => t.Violation is not null || t.Distance > 0)
        .Select(t => $"{t.TermId}{(t.Kind == TermKind.Hard ? " [HARD]" : $" +{t.Distance:0.##}")}").ToList();
    Console.WriteLine($"  evaluator    score {eval.Score:0.##} · fired: {(fired.Count == 0 ? "none" : string.Join(", ", fired))}");
    Console.WriteLine();
}
