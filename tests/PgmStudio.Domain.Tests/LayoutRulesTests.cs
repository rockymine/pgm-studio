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

    /// <summary>The verbs a fix may act with, one per edit: a synonym for one of these is the same edit
    /// spelled two ways, which is what a reader comparing two fixes stumbles on.</summary>
    private static readonly HashSet<string> Verbs = new(StringComparer.OrdinalIgnoreCase)
        { "Add", "Change", "Delete", "Lengthen", "Level", "Merge", "Move", "Set", "Shorten", "Shrink", "Split", "Widen" };

    /// <summary>Words that are a second spelling of one of <see cref="Verbs"/>, or of a phrase the fixes state
    /// one way: <c>until … is at least</c>, <c>the</c> rather than a pointing word, <c>farther from</c>.</summary>
    private static readonly string[] FixNever =
        ["extend", "trim", "remove", "resize", "reshape", "put", "so that", "so its", "to reach", "to at least", " this ",
         " those ", "away", "nearer", "a `rect`"];

    private static IEnumerable<(string Rule, string Sentence)> FixSentences() =>
        Rules.SelectMany(rule => Regex.Split(rule.Fix ?? "", @"(?<=\.)\s+")
            .Where(sentence => sentence.Length > 0).Select(sentence => (rule.Rule, sentence)));

    /// <summary>An alternative between actions is one sentence, <c>Either X, or Y.</c>: a sentence that opens
    /// with Either offers one, and an action after <c>, or</c> belongs to one.</summary>
    [Test]
    public async Task Every_alternative_is_either_or()
    {
        var wrong = new List<string>();
        foreach (var (rule, sentence) in FixSentences())
        {
            var either = sentence.StartsWith("Either ", StringComparison.Ordinal);
            if (either && !sentence.Contains(", or ", StringComparison.Ordinal)) wrong.Add($"{rule}: Either with no ', or'");
            var orAction = Regex.Matches(sentence, @", or (\w+)").Any(match => Verbs.Contains(match.Groups[1].Value));
            if (orAction && !either) wrong.Add($"{rule}: an action after ', or' outside Either");
            if (Regex.IsMatch(sentence, @"^\w+ or \w+ ")) wrong.Add($"{rule}: two verbs joined by or");
            if (!either && Regex.IsMatch(sentence, @", or (a|an|the) [^,]* in `")) wrong.Add($"{rule}: a second action with its verb left out");
        }

        await Assert.That(wrong).IsEmpty();
    }

    /// <summary>Every action a fix names opens with one of <see cref="Verbs"/>: the first word of a sentence,
    /// the word after Either, and the word after <c>, or</c> or <c>, then</c> where an action stands.</summary>
    [Test]
    public async Task Every_action_uses_a_studio_verb()
    {
        var wrong = new List<string>();
        foreach (var (rule, sentence) in FixSentences())
        {
            var first = Regex.Match(sentence, @"^(?:Either )?(\w+)").Groups[1].Value;
            if (!Verbs.Contains(first)) wrong.Add($"{rule}: {first}");
            foreach (Match then in Regex.Matches(sentence, @", then (\w+)"))
                if (!Verbs.Contains(then.Groups[1].Value)) wrong.Add($"{rule}: then {then.Groups[1].Value}");
        }
        foreach (var rule in Rules)
            foreach (var never in FixNever)
                if ((rule.Fix ?? "").Contains(never, StringComparison.OrdinalIgnoreCase)) wrong.Add($"{rule.Rule}: '{never.Trim()}'");

        await Assert.That(wrong).IsEmpty();
    }

    /// <summary>An entry is added to its list and deleted from it, spelled <c>Add X to `list`</c> and
    /// <c>Delete X from `list`</c>, so an agent reads which list to edit from the same place in every fix.</summary>
    [Test]
    public async Task Every_add_names_its_list_with_to_and_every_delete_with_from()
    {
        var wrong = new List<string>();
        foreach (var rule in Rules)
        {
            var fix = rule.Fix ?? "";
            foreach (Match add in Regex.Matches(fix, @"\b[Aa]dd\b[^,.]*"))
                if (!add.Value.Contains(" to `", StringComparison.Ordinal)) wrong.Add($"{rule.Rule}: {add.Value}");
            foreach (Match delete in Regex.Matches(fix, @"\b[Dd]elete\b[^,.]*"))
                if (!delete.Value.Contains(" from `", StringComparison.Ordinal)) wrong.Add($"{rule.Rule}: {delete.Value}");
        }

        await Assert.That(wrong).IsEmpty();
    }

    /// <summary>A change of size acts on a `rect`, a merge names the one entry it makes, and no action hides a
    /// second verb behind <c>by …ing</c>: the edit an agent makes is read off the verb and its object alone.</summary>
    [Test]
    public async Task Every_resize_names_its_rect_and_every_merge_its_entry()
    {
        var wrong = new List<string>();
        foreach (var rule in Rules)
        {
            var fix = rule.Fix ?? "";
            foreach (Match resize in Regex.Matches(fix, @"\b(?:[Ww]iden|[Ss]hrink|[Ll]engthen|[Ss]horten) (\S+ \S+)"))
                if (resize.Groups[1].Value is not ("the `rect`" or "its `rect`")) wrong.Add($"{rule.Rule}: {resize.Value}");
            foreach (Match merge in Regex.Matches(fix, @"\b[Mm]erge\b[^,.]*"))
                if (!merge.Value.Contains(" into one entry in `", StringComparison.Ordinal)) wrong.Add($"{rule.Rule}: {merge.Value}");
            foreach (Match by in Regex.Matches(fix, @"\bby \w+ing\b")) wrong.Add($"{rule.Rule}: {by.Value}");
        }

        await Assert.That(wrong).IsEmpty();
    }

    /// <summary>A meaning takes one of the shapes: a limit (less than, more than, not between), a count, an
    /// absence, a touch, an overlap or a step. A range is "between X and Y", never "X to Y", in either text, a
    /// walking distance opens its sentence rather than trailing it, and "its own team's" stands only beside an
    /// enemy it is told apart from.</summary>
    [Test]
    public async Task Every_meaning_takes_a_shape_and_states_a_range_one_way()
    {
        string[] shapes = ["less than", "more than", "not between", "has no", "touch", "overlaps", "or more blocks above or below", "runs past", "is not"];
        var wrong = new List<string>();
        foreach (var rule in Rules)
        {
            if (!shapes.Any(shape => rule.Means.Contains(shape, StringComparison.Ordinal))) wrong.Add($"{rule.Rule}: no shape");
            if (rule.Means.Contains("by walking distance", StringComparison.Ordinal)) wrong.Add($"{rule.Rule}: trailing walking distance");
            foreach (var text in new[] { rule.Means, rule.Fix ?? "" })
            {
                if (Regex.IsMatch(text, @"\b\d+(\.\d+)? to \d")) wrong.Add($"{rule.Rule}: 'X to Y'");
                if (text.Contains("its own team's", StringComparison.Ordinal) && !text.Contains("enem", StringComparison.Ordinal))
                    wrong.Add($"{rule.Rule}: 'its own team's' with no enemy beside it");
            }
        }

        await Assert.That(wrong).IsEmpty();
    }
}
