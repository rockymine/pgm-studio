using System.Globalization;
using PgmStudio.Domain;
using PgmStudio.Pgm.Evaluate;
using PgmStudio.Pgm.Evaluate.Terms;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A number that exists as a rule's text and as the band a scorer reads is two copies of one ruling, and
/// nothing structural stops them drifting. This is the gate that stops it: each goal rule's text states the
/// band its term actually scores with, in the words the text uses.
/// </summary>
public sealed class RuleBandDriftTests
{
    private static async Task StatesBand(string rule, ILayoutTerm term, string format)
    {
        var band = ((SoftTerm)term).AuthoredBand!.Value;
        var means = RuleCatalog.Read([typeof(LayoutRules).Assembly]).Single(entry => entry.Rule == rule).Means;

        await Assert.That(term.RuleId).IsEqualTo(rule);
        await Assert.That(means)
            .Contains($"less than {band.Lo.ToString(format, CultureInfo.InvariantCulture)} or more than {band.Hi.ToString(format, CultureInfo.InvariantCulture)}");
    }

    [Test]
    public Task GO1s_text_states_the_band_the_term_scores_with() =>
        StatesBand(LayoutRules.GoalSpawnRatio, new GoalSpawnRatio(), "0.##");

    [Test]
    public Task GO2s_text_states_the_band_the_term_scores_with() =>
        StatesBand(LayoutRules.OwnGoalSpacing, new OwnGoalDistance(), "0");

    [Test]
    public Task GO3s_text_states_the_band_the_term_scores_with() =>
        StatesBand(LayoutRules.OpposingGoalSpacing, new OpposingGoalDistance(), "0");

    [Test]
    public Task GO4s_text_states_the_band_the_term_scores_with() =>
        StatesBand(LayoutRules.GoalSpawnDistance, new GoalSpawnDistance(), "0");
}
