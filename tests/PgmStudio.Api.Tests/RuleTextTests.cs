using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every rule's two texts, as <c>GET /api/rules</c> serves them, held to <c>docs/refusals.md</c>'s "How a rule's
/// two texts are written": one spelling per meaning, so a reader comparing two rules, or an agent turning a fix
/// into an edit, never reads one edit as two.
/// </summary>
[NotInParallel("api-db")]
public sealed class RuleTextTests
{
    private sealed record Row(string Rule, string Means, string? Fix);

    /// <summary>Rules whose text holds an open question for the author, named so the exemption is read rather
    /// than hidden: WL12's fix states its gap without the two widths, which do not fit in 35 words.</summary>
    private static readonly HashSet<string> Flagged = ["WL12"];

    private static async Task<List<Row>> RulesAsync()
    {
        using var client = ApiTestFactory.Shared.CreateClient();
        var resp = await client.GetAsync("/api/rules");
        await Assert.That(resp.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<List<Row>>(new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
    }

    private static IEnumerable<(string Rule, string Field, string Text)> Texts(List<Row> rules) =>
        rules.SelectMany(rule => new[] { (rule.Rule, "means", rule.Means), (rule.Rule, "fix", rule.Fix ?? "") });

    private static IEnumerable<(string Rule, string Sentence)> FixSentences(List<Row> rules) =>
        rules.SelectMany(rule => Regex.Split(rule.Fix ?? "", @"(?<=\.)\s+")
            .Where(sentence => sentence.Length > 0).Select(sentence => (rule.Rule, sentence)));

    private static string Prose(string text) => Regex.Replace(text, "`[^`]*`", "");

    /// <summary>The verbs a fix acts with, one per edit: twelve for a document and four for a request.</summary>
    private static readonly HashSet<string> Verbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Add", "Change", "Delete", "Lengthen", "Level", "Merge", "Move", "Set", "Shorten", "Shrink", "Split", "Widen",
        "Send", "Wait", "Ask", "Report",
    };

    /// <summary>Words that are a second spelling of one of <see cref="Verbs"/>, or a pointing word where the
    /// fixes say "the".</summary>
    private static readonly string[] FixNeverWords =
    [
        "extend", "trim", "remove", "resize", "reshape", "put", "place", "fill", "break", "state", "make", "use",
        "give", "pick", "choose", "rename", "raise", "lower", "retry", "contact", "this", "those", "away", "nearer",
    ];

    /// <summary>Phrases the fixes state one way: a target is "until … is", a size acts on "the `rect`".</summary>
    private static readonly string[] FixNeverPhrases = ["so that", "so its", "to reach", "a `rect`"];

    /// <summary>The words a text may not use: every name the glossary retired, and the hedges that stand where
    /// a number belongs.</summary>
    private static readonly string[] Retired =
        [.. Glossary.Terms.SelectMany(term => term.AlsoCalled), "usual", "usually", "typical", "typically", "too", "enough"];

    /// <summary>The shapes a meaning takes: a limit, a count, an absence or a presence, a contact, a step, a
    /// name that resolves to nothing, two values at once, a duplicate, a right the caller lacks, a service that
    /// did not answer and a fault. A two-sided disagreement is the clause shape <c>X, and Y</c>.</summary>
    private static readonly string[] Shapes =
    [
        "less than", "more than", "fewer than", "not between", "is at least", "has no", "has a", "touch", "overlaps",
        "or more blocks above or below", "runs past", "is not", "is built of", "both", "the same", "holds only",
        "does not exist", "does not have", "may not", "did not answer", "failed to", ", and ",
    ];

    [Test]
    public async Task Every_text_is_at_most_35_words()
    {
        var rules = await RulesAsync();
        var wordy = Texts(rules).Where(text => text.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 35)
            .Select(text => $"{text.Rule} {text.Field}");

        await Assert.That(rules).IsNotEmpty();
        await Assert.That(wordy).IsEmpty();
    }

    [Test]
    public async Task No_text_uses_a_retired_word()
    {
        var found = new List<string>();
        foreach (var (rule, field, text) in Texts(await RulesAsync()))
            foreach (var word in Retired)
                if (!Flagged.Contains(rule)
                    && Regex.IsMatch(Prose(text), $@"(?<![\w-]){Regex.Escape(word)}(?![\w-])", RegexOptions.IgnoreCase))
                    found.Add($"{rule} {field}: {word}");

        await Assert.That(found).IsEmpty();
    }

    /// <summary>Neither text names code, a rule id, an endpoint or a file, and neither uses a dash, an arrow, a
    /// comparison sign, parentheses or a semicolon.</summary>
    [Test]
    public async Task No_text_names_code_or_uses_a_symbol()
    {
        var found = new List<string>();
        foreach (var (rule, field, text) in Texts(await RulesAsync()))
        {
            if (text.IndexOfAny(['—', '–', '≥', '≤', '→', '(', ')', ';', '=']) >= 0) found.Add($"{rule} {field}: a symbol");
            if (Regex.IsMatch(text, @"\b[A-Z]{1,3}\d+\b")) found.Add($"{rule} {field}: a rule id");
            if (Regex.IsMatch(Prose(text), @"\b[A-Z][a-z]+[A-Z]\w*")) found.Add($"{rule} {field}: a code name");
            if (text.Contains("/api", StringComparison.Ordinal) || text.Contains(".cs", StringComparison.Ordinal))
                found.Add($"{rule} {field}: an endpoint or a file");
        }

        await Assert.That(found).IsEmpty();
    }

    /// <summary>An alternative between actions is one sentence, <c>Either X, or Y.</c>: each side opens with its
    /// own verb, and a plain "or" joins nouns only.</summary>
    [Test]
    public async Task Every_alternative_is_either_or()
    {
        var wrong = new List<string>();
        foreach (var (rule, sentence) in FixSentences(await RulesAsync()))
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

    /// <summary>Every action opens with one of <see cref="Verbs"/>, and no fix spells an edit a second way.</summary>
    [Test]
    public async Task Every_action_uses_a_studio_verb()
    {
        var rules = await RulesAsync();
        var wrong = new List<string>();
        foreach (var (rule, sentence) in FixSentences(rules))
        {
            var first = Regex.Match(sentence, @"^(?:Either )?(\w+)").Groups[1].Value;
            if (!Verbs.Contains(first)) wrong.Add($"{rule}: {first}");
            foreach (Match then in Regex.Matches(sentence, @", then (\w+)"))
                if (!Verbs.Contains(then.Groups[1].Value)) wrong.Add($"{rule}: then {then.Groups[1].Value}");
        }
        foreach (var rule in rules)
        {
            var fix = rule.Fix ?? "";
            foreach (var word in FixNeverWords)
                if (Regex.IsMatch(Prose(fix), $@"\b{word}\b", RegexOptions.IgnoreCase)) wrong.Add($"{rule.Rule}: '{word}'");
            foreach (var phrase in FixNeverPhrases)
                if (fix.Contains(phrase, StringComparison.OrdinalIgnoreCase)) wrong.Add($"{rule.Rule}: '{phrase}'");
        }

        await Assert.That(wrong).IsEmpty();
    }

    /// <summary>An entry is added to its list and deleted from it; a change of size acts on a `rect`; a merge
    /// names the one entry it makes; no action hides a second verb behind "by …ing".</summary>
    [Test]
    public async Task Every_edit_names_its_list_or_its_rect()
    {
        var wrong = new List<string>();
        foreach (var rule in await RulesAsync())
        {
            var fix = rule.Fix ?? "";
            foreach (Match add in Regex.Matches(fix, @"\b[Aa]dd\b[^,.]*"))
                if (!add.Value.Contains(" to `", StringComparison.Ordinal) && !add.Value.Contains(" to the `", StringComparison.Ordinal))
                    wrong.Add($"{rule.Rule}: {add.Value}");
            foreach (Match delete in Regex.Matches(fix, @"\b[Dd]elete\b[^,.]*"))
                if (!delete.Value.Contains(" from `", StringComparison.Ordinal) && !delete.Value.Contains(" from the `", StringComparison.Ordinal))
                    wrong.Add($"{rule.Rule}: {delete.Value}");
            foreach (Match resize in Regex.Matches(fix, @"\b(?:[Ww]iden|[Ss]hrink|[Ll]engthen|[Ss]horten) (\S+ \S+)"))
                if (resize.Groups[1].Value is not ("the `rect`" or "its `rect`")) wrong.Add($"{rule.Rule}: {resize.Value}");
            foreach (Match merge in Regex.Matches(fix, @"\b[Mm]erge\b[^,.]*"))
                if (!merge.Value.Contains(" into one entry in `", StringComparison.Ordinal)) wrong.Add($"{rule.Rule}: {merge.Value}");
            foreach (Match by in Regex.Matches(fix, @"\bby \w+ing\b")) wrong.Add($"{rule.Rule}: {by.Value}");
        }

        await Assert.That(wrong).IsEmpty();
    }

    /// <summary>Set takes a value ("to V", "to at least N"); a target reached by degrees is "until … is", after
    /// Change, Move or a resize. A value is in backticks, the finding is "the X the finding names", a field is
    /// one dotted path ("the `spec.form` of a wing", never "the `form` in the `spec`"), and a request is sent
    /// again with its parameter set.</summary>
    [Test]
    public async Task Every_value_and_target_is_spelled_one_way()
    {
        var wrong = new List<string>();
        foreach (var rule in await RulesAsync())
        {
            var fix = rule.Fix ?? "";
            foreach (Match until in Regex.Matches(fix, @"\b[Ss]et\b[^,.]*\buntil\b")) wrong.Add($"{rule.Rule}: {until.Value}");
            foreach (Match bare in Regex.Matches(fix, @"\bto (null|true|false)\b")) wrong.Add($"{rule.Rule}: {bare.Value}");
            foreach (Match more in Regex.Matches(fix, @"\b\d+ or more\b")) wrong.Add($"{rule.Rule}: {more.Value}");
            foreach (Match nested in Regex.Matches(fix, @"`[\w.]+` in the `")) wrong.Add($"{rule.Rule}: {nested.Value} is one dotted path");
            foreach (Match pointing in Regex.Matches(fix, @"\b(that|these) (ground|block|piece|room|shape|wall|edge|side|layer|entry|prop|island|value|number|height|distance)\b"))
                wrong.Add($"{rule.Rule}: '{pointing.Value}' points where the fixes say 'the'");
            foreach (var phrase in new[] { "of the finding", "the warnings", "the finding lists", "[]", "of the request to" })
                if (fix.Contains(phrase, StringComparison.Ordinal)) wrong.Add($"{rule.Rule}: '{phrase}'");
            foreach (Match bound in Regex.Matches(fix, @"\bto at (least|most)\b"))
            {
                var sentence = Regex.Split(fix[..bound.Index], @"\. ").Last();
                var verbs = Regex.Matches(sentence, @"\b\w+\b").Select(word => word.Value).Where(Verbs.Contains).ToList();
                if (verbs.Count == 0 || !verbs[^1].Equals("set", StringComparison.OrdinalIgnoreCase))
                    wrong.Add($"{rule.Rule}: '{bound.Value}' outside a Set");
            }
        }

        await Assert.That(wrong).IsEmpty();
    }

    /// <summary>A meaning takes one of the <see cref="Shapes"/> and says what was made, never why it is refused.
    /// A range is "between X and Y" in either text, a walking distance opens its sentence, and "its own team's"
    /// stands only beside an enemy it is told apart from.</summary>
    [Test]
    public async Task Every_meaning_takes_a_shape_and_states_a_range_one_way()
    {
        var wrong = new List<string>();
        foreach (var rule in await RulesAsync())
        {
            if (!Shapes.Any(shape => rule.Means.Contains(shape, StringComparison.Ordinal))) wrong.Add($"{rule.Rule}: no shape");
            if (rule.Means.Contains("by walking distance", StringComparison.Ordinal)) wrong.Add($"{rule.Rule}: trailing walking distance");
            foreach (var reason in new[] { ", which ", ", but " })
                if (rule.Means.Contains(reason, StringComparison.Ordinal)) wrong.Add($"{rule.Rule}: '{reason.Trim()}' gives a reason");
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
