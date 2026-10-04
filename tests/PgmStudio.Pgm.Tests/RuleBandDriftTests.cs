using System.Globalization;
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
    private enum Unit { Blocks, Cells, Percent, Times, Count }

    /// <summary>How each banded term's rule states its band: a range where both ends can be broken, the upper
    /// end alone where the lower cannot (a count from zero, a ratio from its ideal of one).</summary>
    private static readonly (string Term, string Rule, Unit Unit, bool Range)[] Stated =
    [
        ("band-count", "CT1", Unit.Count, false),
        ("neutral-stepping-count", "CT4", Unit.Count, false),
        ("team-stepping-count", "CT13", Unit.Count, false),
        ("isolation-cut-count", "CT5", Unit.Count, false),
        ("enclosed-void-count", "CT8", Unit.Count, false),
        ("route-interference", "CT14", Unit.Percent, false),
        ("frontline-count", "FR4", Unit.Count, false),
        ("frontline-width", "FR6", Unit.Cells, false),
        ("fill-ratio", "G8", Unit.Percent, true),
        ("dead-share", "LN5", Unit.Percent, false),
        ("lane-width", "LN1", Unit.Blocks, true),
        ("max-chain-length", "LN2", Unit.Blocks, true),
        ("wool-wool-distance", "WL7", Unit.Blocks, true),
        ("spawn-wool-distance", "WL13", Unit.Blocks, true),
        ("spawn-wool-spread", "WL9", Unit.Blocks, false),
        ("spawn-wool-ratio", "WL15", Unit.Times, false),
        ("wool-front-distance", "WL10", Unit.Blocks, true),
        ("wool-front-balance", "WL16", Unit.Blocks, false),
        ("wool-front-ratio", "WL17", Unit.Times, false),
        ("wool-front-remoteness", "WL18", Unit.Blocks, true),
        ("goal-spawn-ratio", "GO1", Unit.Times, true),
        ("own-goal-distance", "GO2", Unit.Blocks, true),
        ("opposing-goal-distance", "GO3", Unit.Blocks, true),
        ("goal-spawn-distance", "GO4", Unit.Blocks, true),
    ];

    private static string Number(double value, Unit unit) => unit switch
    {
        Unit.Percent => (Math.Round(value * 200) / 2).ToString("0.#", CultureInfo.InvariantCulture),
        Unit.Times => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture),
        Unit.Count => Math.Floor(value).ToString("0", CultureInfo.InvariantCulture),
        _ => Math.Round(value).ToString("0", CultureInfo.InvariantCulture),
    };

    private static string Word(Unit unit) => unit switch
    {
        Unit.Blocks => " blocks",
        Unit.Cells => " cells",
        Unit.Percent => " percent",
        Unit.Times => " times",
        _ => "",
    };

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

        foreach (var (termId, rule, unit, range) in Stated)
        {
            var term = terms[termId];
            var band = BandOf(term);
            var expected = range
                ? $"between {Number(band.Lo, unit)} and {Number(band.Hi, unit)}{Word(unit)}"
                : $"more than {Number(band.Hi, unit)}{Word(unit)}";
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
