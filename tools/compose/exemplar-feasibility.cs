#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

// Read every authored exemplar plan back through the producibility engine and print each box's verdict.
foreach (var path in Directory.GetFiles("tools/seeds", "*.plan.json").OrderBy(p => p))
{
    var plan = PlanModel.Parse(File.ReadAllText(path));
    if (plan is null) continue;
    if (plan.Boxes.Count == 0) continue;
    Console.WriteLine($"=== {Path.GetFileName(path)}");
    var read = Producibility.ReadPlan(plan);
    foreach (var b in read.Boxes)
        Console.WriteLine($"  {b.BoxId,-12} {b.Kind,-10} {b.Identity,-22} "
            + (b.Producible is { } p ? $"OK  {p.Label}"
               : $"NO  nearest {b.Nearest?.Label} ({b.Nearest?.DifferingCells} cells)")
            + (b.Findings.Count == 0 ? "" : "  | " + string.Join("; ", b.Findings.Select(f => $"{f.Code}[{f.Cites}]"))));
    foreach (var f in read.Unit) Console.WriteLine($"  UNIT {f.Code}[{f.Cites}] {f.Detail}");
}
