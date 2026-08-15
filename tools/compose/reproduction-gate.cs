#:project ../../src/PgmStudio.Pgm/PgmStudio.Pgm.csproj
#:property JsonSerializerIsReflectionEnabledByDefault=true
using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Plan;

// The reproduction gate: compose a board sweep and check that every composed board reads back as producible —
// no box the emitters cannot reproduce, no unit-level rule violated. Also reports how many hubs come out with a
// widened ring wall, read off the tuple the producibility search names.
int composed = 0, threw = 0, boards = 0;
int hubs = 0, ringed = 0, widened = 0;
var findings = new List<string>();

foreach (var players in new[] { 6, 8, 12, 16, 20, 30 })
    foreach (var sym in new[] { "rot_180", "mirror_z" })
        for (ulong seed = 1; seed <= 34; seed++)
        {
            boards++;
            PlanModel plan;
            ComposedStages stages;
            try { stages = Composer.ComposeStages(new ComposeRequest(players, 2, sym, seed, 5)); }
            catch (Exception ex) { threw++; findings.Add($"THREW p{players} {sym} s{seed}: {ex.GetType().Name} {ex.Message}"); continue; }
            composed++;
            plan = stages.Plan;
            PlanBoxAnnotation.Apply(plan, stages.Unit);          // the boxes producibility reads

            var read = Producibility.ReadPlan(plan);
            // the producibility search names the tuple that reproduced each box, so a widened ring is whichever
            // hub only reproduces at a non-uniform wall vector — read off the answer rather than re-measured
            foreach (var b in read.Boxes.Where(b => b.Kind == PlanBoxKinds.Hub))
            {
                hubs++;
                if (b.Identity.StartsWith("Ring") || b.Identity.StartsWith("P")
                    || b.Identity.StartsWith("G") || b.Identity.StartsWith("DoubleHole")) ringed++;
                if (b.Producible?.Label.Contains("walls") == true) widened++;
            }

            foreach (var b in read.Boxes.Where(b => !b.IsProducible && b.Kind != PlanBoxKinds.Mid))
                findings.Add($"p{players} {sym} s{seed} {b.BoxId} ({b.Kind}/{b.Identity}) "
                    + $"nearest {b.Nearest?.Label} {b.Nearest?.DifferingCells} cells"
                    + (b.Findings.Count == 0 ? "" : " | " + string.Join("; ", b.Findings.Select(f => $"{f.Rule}[{f.Cites}]"))));
            foreach (var f in read.Unit)
                findings.Add($"p{players} {sym} s{seed} UNIT {f.Rule}[{f.Cites}] {f.Message}");
        }

Console.WriteLine($"composed {composed}/{boards}  threw {threw}");
Console.WriteLine($"hubs {hubs}  ring-bodied {ringed}  reproduced at widened walls {widened} "
    + $"({100.0 * widened / Math.Max(1, hubs):F1}% of hubs, {100.0 * widened / Math.Max(1, ringed):F1}% of ring-bodied)");
Console.WriteLine($"producibility findings: {findings.Count}");
foreach (var f in findings.Take(25)) Console.WriteLine("  " + f);
