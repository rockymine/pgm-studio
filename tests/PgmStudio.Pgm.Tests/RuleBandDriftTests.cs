using PgmStudio.Domain;
using PgmStudio.Pgm.Evaluate.Terms;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A number that exists as a rule's prose and as the constant a scorer reads is two copies of one ruling,
/// and nothing structural stops them drifting — rules.md is law amended by hand, the term is code. This is
/// the gate that stops it: the band GO1's text states must be the band <see cref="GoalSpawnRatio"/> actually
/// scores with, byte for byte in the format the text uses.
/// </summary>
public sealed class RuleBandDriftTests
{
    [Test]
    public async Task GO1s_prose_states_the_band_the_term_scores_with()
    {
        var band = new GoalSpawnRatio().AuthoredBand!.Value;
        var go1 = RuleCatalog.Read([]).Single(rule => rule.Rule == "GO1");

        await Assert.That(go1.Means).Contains($"[{band.Lo:0.0}, {band.Hi:0.0}]");
        // and the prose still names the term that enforces it, so a reader lands on the right code
        await Assert.That(go1.Means).Contains("goal-spawn-ratio");
    }

    [Test]
    public async Task GO4s_prose_states_the_band_the_term_scores_with()
    {
        var band = new GoalSpawnDistance().AuthoredBand!.Value;
        var go4 = RuleCatalog.Read([]).Single(rule => rule.Rule == "GO4");

        await Assert.That(go4.Means).Contains($"[{band.Lo:0}, {band.Hi:0}]");
        await Assert.That(go4.Means).Contains("goal-spawn-distance");
    }
}
