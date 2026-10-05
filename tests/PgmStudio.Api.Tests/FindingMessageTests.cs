using System.Text;
using System.Text.RegularExpressions;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A finding's message, read out of the source that writes it: one sentence, opening with the thing it is about,
/// saying where and the number it measured against its limit, with no reason, no fix and no symbol, in the
/// glossary's words. The rule a finding cites says why and what to do; the message says only which thing and how
/// far.
/// </summary>
public sealed class FindingMessageTests
{
    /// <summary>Every call that carries a finding's message, and which of its arguments the message is: the
    /// finding itself, written out or target-typed, the helpers that write one (the plan checks' lint, the decorator's decline, the request
    /// refusals, the editors' faults and the evaluator's term scores).</summary>
    private static readonly (string Call, int Argument)[] Carriers =
    [
        ("new Finding", 1), (@"new(?=\(\s*[A-Z]\w*Rules\.)", 1), ("Lint", 1), ("Declined", 3), (@"Refusals\.UnreadableAsync", 2), (@"Refusals\.ConflictAsync", 2),
        (@"EditException\.(?:Unreadable|NoSuchSubject|Conflict|Unresolved|Inapplicable)", 0),
        (@"TermScores\.Violated", 1), (@"TermScores\.Soft", 2),
    ];

    /// <summary>Text a message never holds: a dash or a sign standing for a word, a second sentence, a reason
    /// or a fix in prose, a plural written with brackets, and the phrasings the messages state one way —
    /// "does not exist", "states no", "is already taken", "is not a number", "is not one of".</summary>
    private static readonly string[] Never =
    [
        "—", "–", "×", "≥", "≤", "→", ";", " < ", " > ", ". ", " wants", " should ", " so ", " because ", "(s)",
        "not found", "is required", "already in use", "must be", "invalid", "unknown", "will not", "cannot",
    ];

    /// <summary>What a message's prose, its values and quoted ids set aside, never holds: a fix or a reason in
    /// words, a rule or task id, a name from the code, and words in parentheses.</summary>
    private static readonly (Regex Pattern, string Says)[] Shapes =
    [
        (new(@"\b(instead|please|try|make sure|so that|in order)\b", RegexOptions.IgnoreCase), "a reason or a fix"),
        (new(@"\b[A-Z]{1,3}\d+\b"), "a rule or task id"),
        (new(@"\b[A-Z][a-z]+[A-Z]\w*"), "a name from the code"),
        (new(@"\(\s*[a-zA-Z]"), "words in parentheses"),
    ];

    /// <summary>The words a message may not use: every name the glossary retired in favour of a term.</summary>
    private static readonly string[] Retired = [.. Glossary.Terms.SelectMany(term => term.AlsoCalled)];

    [Test]
    public async Task Every_message_in_the_source_takes_the_one_shape()
    {
        var wrong = new List<string>();
        var count = 0;
        foreach (var path in Directory.EnumerateFiles(Source(), "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var file = Path.GetRelativePath(Source(), path);
            foreach (var (line, text, _) in Messages(File.ReadAllText(path)))
            {
                if (text.Length == 0) continue;
                count++;
                var where = $"{file}:{line} \"{text}\"";
                foreach (var never in Never)
                    if (text.Contains(never, StringComparison.Ordinal)) wrong.Add($"{where}: '{never.Trim()}'");
                if (text.TrimEnd().EndsWith('.')) wrong.Add($"{where}: ends a sentence");
                if (char.IsUpper(text[0])) wrong.Add($"{where}: opens with a capital");
                var prose = Regex.Replace(text, "`[^`]*`|'[^']*'", "");
                foreach (var (pattern, says) in Shapes)
                    if (pattern.IsMatch(prose)) wrong.Add($"{where}: {says}");
                foreach (var word in Retired)
                    if (Regex.IsMatch(prose, $@"(?<![\w-]){Regex.Escape(word)}(?![\w-])", RegexOptions.IgnoreCase))
                        wrong.Add($"{where}: retired word '{word}'");
            }
        }

        await Assert.That(count).IsGreaterThan(350);
        await Assert.That(wrong).IsEmpty();
    }

    /// <summary>A plan check's lint names the thing it is about by its id, as every message the plan checks
    /// write opens with one.</summary>
    [Test]
    public async Task Every_plan_check_names_its_thing()
    {
        var path = Path.Combine(Source(), "PgmStudio.Pgm", "Plan", "PlanValidator.cs");
        var unnamed = Messages(File.ReadAllText(path), "Lint")
            .Where(message => !message.Text.Contains("'{}'", StringComparison.Ordinal) && !NamesThroughAHelper.IsMatch(message.Raw))
            .Select(message => $"PlanValidator.cs:{message.Line} \"{message.Text}\"");

        await Assert.That(unnamed).IsEmpty();
    }

    /// <summary>The helpers that write ids already quoted: a list of them, or a piece that may be the edge.</summary>
    private static readonly Regex NamesThroughAHelper = new(@"\b(Wording\.Ids|Named)\(");

    private static string Source()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("no src/ above the test output"), "src");
    }

    /// <summary>Every carrier call in a source, its line, the literal text of its message, an interpolation hole
    /// read as <c>{}</c>, and the message as written.</summary>
    private static IEnumerable<(int Line, string Text, string Raw)> Messages(string source, string? only = null)
    {
        var found = new List<(int At, int Line, string Text, string Raw)>();
        foreach (var (call, argument) in Carriers.Where(carrier => only is null || carrier.Call == only))
            foreach (Match match in Regex.Matches(source, $@"(?<![\w.]){call}\("))
            {
                var args = Arguments(source, match.Index + match.Length);
                if (args is null || args.Count <= argument) continue;
                found.Add((match.Index, source[..match.Index].Count(c => c == '\n') + 1, Literal(args[argument]), args[argument]));
            }
        return found.OrderBy(entry => entry.At).Select(entry => (entry.Line, entry.Text, entry.Raw));
    }

    /// <summary>The top-level arguments of a call whose parenthesis opened just before <paramref name="at"/>, or
    /// null for a declaration, whose first argument is a parameter list.</summary>
    private static List<string>? Arguments(string source, int at)
    {
        var args = new List<string>();
        var start = at;
        var depth = 0;
        for (var i = at; i < source.Length; i++)
        {
            var c = source[i];
            if (c == '"' || (c is '$' or '@' && i + 1 < source.Length && source[i + 1] is '"' or '$' or '@'))
            {
                i = SkipString(source, i, out _) - 1;
                continue;
            }
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}')
            {
                if (depth == 0) { args.Add(source[start..i]); return Declares.Any(type => args[0].TrimStart().StartsWith(type, StringComparison.Ordinal)) ? null : args; }
                depth--;
            }
            else if (c == ',' && depth == 0) { args.Add(source[start..i]); start = i + 1; }
        }
        return null;
    }

    /// <summary>The first parameter of a declaration, which a call's first argument never starts with.</summary>
    private static readonly string[] Declares = ["string ", "HttpContext ", "ILayoutTerm ", "int "];

    /// <summary>The literal text of the strings an expression joins at its own level.</summary>
    private static string Literal(string expression)
    {
        var text = new StringBuilder();
        var depth = 0;
        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];
            if (c == '"' || (c is '$' or '@' && i + 1 < expression.Length && expression[i + 1] is '"' or '$' or '@'))
            {
                i = SkipString(expression, i, out var literal) - 1;
                if (depth == 0) text.Append(literal);
                continue;
            }
            if (c is '(' or '[') depth++;
            else if (c is ')' or ']') depth--;
        }
        return text.ToString();
    }

    /// <summary>Reads one string literal from <paramref name="at"/>, interpolated or not, and returns the index
    /// after it, with its text, each hole read as <c>{}</c>.</summary>
    private static int SkipString(string source, int at, out string literal)
    {
        var interpolated = false;
        var verbatim = false;
        var i = at;
        while (source[i] != '"') { interpolated |= source[i] == '$'; verbatim |= source[i] == '@'; i++; }
        i++;
        var text = new StringBuilder();
        while (i < source.Length)
        {
            var c = source[i];
            if (!verbatim && c == '\\') { text.Append(source[i + 1]); i += 2; continue; }
            if (c == '"')
            {
                if (verbatim && i + 1 < source.Length && source[i + 1] == '"') { text.Append('"'); i += 2; continue; }
                literal = text.ToString();
                return i + 1;
            }
            if (interpolated && c == '{')
            {
                if (source[i + 1] == '{') { text.Append('{'); i += 2; continue; }
                var depth = 1;
                i++;
                while (depth > 0)
                {
                    if (source[i] == '"' || (source[i] is '$' or '@' && source[i + 1] is '"' or '$' or '@')) { i = SkipString(source, i, out _); continue; }
                    if (source[i] == '{') depth++;
                    else if (source[i] == '}') depth--;
                    i++;
                }
                text.Append("{}");
                continue;
            }
            if (interpolated && c == '}' && source[i + 1] == '}') { text.Append('}'); i += 2; continue; }
            text.Append(c);
            i++;
        }
        literal = text.ToString();
        return i;
    }
}
