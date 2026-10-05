using PgmStudio.Domain;
using PgmStudio.Pgm.Evaluate;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A layout rule states the numbers its term scores against, and those numbers live twice: in the rule's text
/// and in the band the term reads, authored on the term or learned from the seed maps. This is the gate that
/// stops them drifting: every banded term's rule states its band, in the words and rounding the texts use, so
/// re-learning the envelopes fails here until the text says the new numbers.
/// </summary>
public sealed class RuleBandDriftTests
{
    /// <summary>How each banded term's rule states its band: a range where both ends can be broken, the upper
    /// end alone where the lower cannot (a count from zero, a ratio from its ideal of one). The unit and the
    /// rounding are the term's own <see cref="SoftTerm.Unit"/>, written by <see cref="Measures"/>.</summary>
    private static readonly (string Term, string Rule, bool Range)[] Stated =
    [
        ("band-count", "CT1", false),
        ("neutral-stepping-count", "CT4", false),
        ("team-stepping-count", "CT13", false),
        ("isolation-cut-count", "CT5", false),
        ("enclosed-void-count", "CT8", false),
        ("route-interference", "CT14", false),
        ("frontline-count", "FR4", false),
        ("frontline-width", "FR6", false),
        ("fill-ratio", "G8", true),
        ("dead-share", "LN5", false),
        ("lane-width", "LN1", true),
        ("max-chain-length", "LN2", true),
        ("wool-wool-distance", "WL7", true),
        ("spawn-wool-distance", "WL13", true),
        ("spawn-wool-spread", "WL9", false),
        ("spawn-wool-ratio", "WL15", false),
        ("wool-front-distance", "WL10", true),
        ("wool-front-balance", "WL16", false),
        ("wool-front-ratio", "WL17", false),
        ("wool-front-remoteness", "WL18", true),
        ("goal-spawn-ratio", "GO1", true),
        ("own-goal-distance", "GO2", true),
        ("opposing-goal-distance", "GO3", true),
        ("goal-spawn-distance", "GO4", true),
    ];

    private static Band BandOf(SoftTerm term) => term.AuthoredBand ?? SeedEnvelopes.Default[term.Id]!.Value;

    [Test]
    public async Task Every_banded_term_is_stated()
    {
        var banded = LayoutEvaluator.AllTerms.OfType<SoftTerm>()
            .Where(term => term.AuthoredBand is not null || SeedEnvelopes.Default[term.Id] is not null)
            .Select(term => term.Id)
            .Where(id => id != "uncrossed-middle-void");

        await Assert.That(banded.Except(Stated.Select(row => row.Term))).IsEmpty();
    }

    [Test]
    public async Task Every_rule_states_the_band_its_term_scores_against()
    {
        var means = RuleCatalog.Read([typeof(LayoutRules).Assembly]).ToDictionary(rule => rule.Rule, rule => rule.Means);
        var terms = LayoutEvaluator.AllTerms.OfType<SoftTerm>().ToDictionary(term => term.Id);
        var drifted = new List<string>();

        foreach (var (termId, rule, range) in Stated)
        {
            var term = terms[termId];
            var band = BandOf(term);
            var expected = range
                ? Measures.Between(band.Lo, band.Hi, term.Unit)
                : $"more than {Measures.Bound(band.Hi, term.Unit)}";
            if (term.RuleId != rule || !means[rule].Contains(expected, StringComparison.Ordinal))
                drifted.Add($"{rule} ({termId}) should say \"{expected}\": {means[rule]}");
        }

        await Assert.That(drifted).IsEmpty();
    }

    /// <summary>A hole in the mid with no crossing is scored against a band of exactly zero, which the rule
    /// states as "has no" rather than as numbers.</summary>
    [Test]
    public async Task The_zero_band_is_stated_as_none()
    {
        var term = LayoutEvaluator.AllTerms.OfType<SoftTerm>().Single(term => term.Id == "uncrossed-middle-void");

        await Assert.That(BandOf(term)).IsEqualTo(new Band(0, 0));
    }
}
