using PgmStudio.Pgm.Compose;
using PgmStudio.Pgm.Derive;
using PgmStudio.Pgm.Shapes;

namespace PgmStudio.Pgm.Tests.Derive;

/// <summary>The structural read of a composed unit: wool approach families (sorted), the hub compound form, and
/// the frontline form (or none). Reads uniformly off the labeled unit, matches the wool count, is
/// deterministic per seed, and serializes to a stable canonical bucket key.</summary>
public sealed class StructureSummaryTests
{
    [Test]
    public async Task Derive_reads_wools_hub_and_frontline_across_seeds()
    {
        foreach (var players in new[] { 6, 8, 12, 20 })
            for (ulong seed = 0; seed < 8; seed++)
            {
                var stages = Composer.ComposeStages(new ComposeRequest(players, seed: seed));
                var s = StructureSummary.Derive(stages.Unit);

                // one family per wool box == the plan's wool count
                await Assert.That(s.Wools.Count).IsEqualTo(stages.Plan.Placements.Wools.Count)
                    .Because($"{players}p seed {seed}");
                // sorted (canonical order)
                await Assert.That(s.Wools.SequenceEqual(s.Wools.OrderBy(f => f))).IsTrue();
                // a unit always has a hub
                await Assert.That(s.Hub).IsNotNull();
            }
    }

    [Test]
    public async Task Derive_is_deterministic_and_canonical_key_is_stable()
    {
        var a = StructureSummary.Derive(Composer.ComposeStages(new ComposeRequest(12, seed: 3)).Unit);
        var b = StructureSummary.Derive(Composer.ComposeStages(new ComposeRequest(12, seed: 3)).Unit);
        await Assert.That(a.Canonical()).IsEqualTo(b.Canonical());
        await Assert.That(a.Canonical()).StartsWith("wools:");
        await Assert.That(a.Canonical()).Contains("|hub:");
        await Assert.That(a.Canonical()).Contains("|front:");
    }

    [Test]
    public async Task Form_tokens_map_the_body_vocabulary()
    {
        await Assert.That(StructureNames.Form(null)).IsEqualTo("none");
        await Assert.That(StructureNames.Form(CompoundForm.I)).IsEqualTo("i");
        await Assert.That(StructureNames.Form(CompoundForm.L)).IsEqualTo("l");
        await Assert.That(StructureNames.Form(CompoundForm.U)).IsEqualTo("u");
        await Assert.That(StructureNames.Form(CompoundForm.Donut)).IsEqualTo("donut");
        await Assert.That(StructureNames.Form(CompoundForm.DoubleHole)).IsEqualTo("double-hole");
    }
}
