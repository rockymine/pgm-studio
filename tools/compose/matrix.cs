#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

var cases = new List<(int p, int t, string s, ulong seed)>();
foreach (var p in new[]{5,12,16,20,30})
    foreach (var seed in new ulong[]{1,7,13})
        cases.Add((p,2,"rot_180",seed));
foreach (var seed in new ulong[]{1,7,13}) cases.Add((5,4,"rot_90",seed));
cases.Add((10,4,"rot_90",1UL));
cases.Add((16,4,"rot_90",5UL));
cases.Add((20,4,"rot_90",9UL));
cases.Add((12,2,"mirror_x",2UL));
cases.Add((10,2,"mirror_z",2UL));

int ok=0, fail=0;
Console.WriteLine($"{"case",-24} {"pcs",4} {"zon",4} {"stn",4} {"errs",5} {"holes",6} {"cut",4}  threw");
foreach (var (p,t,s,seed) in cases)
{
    var name = $"p{p} t{t} {s} s{seed}";
    try {
        var plan = Composer.Compose(new ComposeRequest(p,t,s,seed,5));
        var errs = PlanValidator.Validate(plan).Count(f => f.Severity == PlanSeverity.Error);
        var stones = plan.Pieces.Count(pc => pc.Id.StartsWith("stone"));
        var holes = ClosureAnalysis.HoleSizes(plan).Count;
        var cut = plan.Zones.Any(z => z.Id == "bridge-a");
        Console.WriteLine($"{name,-24} {plan.Pieces.Count,4} {plan.Zones.Count,4} {stones,4} {errs,5} {holes,6} {cut,4}  no");
        ok++;
    }
    catch (Exception ex) {
        Console.WriteLine($"{name,-24} {"-",4} {"-",4} {"-",4} {"-",5} {"-",6} {"-",4}  YES {ex.GetType().Name}");
        fail++;
    }
}
Console.WriteLine($"=== composed={ok} threw={fail} (of {cases.Count})");
