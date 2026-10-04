using System.Text.RegularExpressions;
using PgmStudio.Vocabulary;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// The layout rules' two texts, held to the format the author approved: each at most 35 words, in the
/// glossary's words and never a name the glossary retired, no "usual" where a number belongs, and a document
/// field marked only in the fix.
/// </summary>
public class LayoutRulesTests
{
    private static readonly IReadOnlyList<RuleDoc> Rules =
        [.. RuleCatalog.Read([typeof(LayoutRules).Assembly]).Where(rule => rule.Owner.StartsWith("PgmStudio.Domain.LayoutRules.", StringComparison.Ordinal))];

    private static IEnumerable<(string Rule, string Field, string Text)> Texts() =>
        Rules.SelectMany(rule => new[] { (rule.Rule, "means", rule.Means), (rule.Rule, "fix", rule.Fix ?? "") });

    /// <summary>The words a text may not use: every name the glossary retired in favour of a term, and the
    /// hedges that stand where a number belongs.</summary>
    private static readonly string[] Retired =
        [.. Glossary.Terms.SelectMany(term => term.AlsoCalled), "usual", "usually", "typical", "typically"];

    [Test]
    public async Task Every_text_is_at_most_35_words()
    {
        var wordy = Texts().Where(text => text.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 35)
            .Select(text => $"{text.Rule} {text.Field}");

        await Assert.That(Rules).IsNotEmpty();
        await Assert.That(wordy).IsEmpty();
    }

    [Test]
    public async Task No_text_uses_a_retired_word()
    {
        var found = new List<string>();
        foreach (var (rule, field, text) in Texts())
        {
            var prose = Regex.Replace(text, "`[^`]*`", "");
            foreach (var word in Retired)
                if (Regex.IsMatch(prose, $@"(?<![\w-]){Regex.Escape(word)}(?![\w-])", RegexOptions.IgnoreCase))
                    found.Add($"{rule} {field}: {word}");
        }

        await Assert.That(found).IsEmpty();
    }

    [Test]
    public async Task Only_a_fix_marks_a_field_and_no_text_uses_a_symbol()
    {
        await Assert.That(Rules.Where(rule => rule.Means.Contains('`')).Select(rule => rule.Rule)).IsEmpty();
        await Assert.That(Texts().Where(text => text.Text.IndexOfAny(['—', '–', '≥', '≤', '→']) >= 0)
            .Select(text => $"{text.Rule} {text.Field}")).IsEmpty();
    }
}
