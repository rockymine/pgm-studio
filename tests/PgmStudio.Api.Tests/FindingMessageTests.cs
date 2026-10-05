using System.Text;
using System.Text.RegularExpressions;

namespace PgmStudio.Api.Tests;

/// <summary>
/// A finding's message, read out of the source that writes it: one sentence, opening with the thing it is about
/// by kind and id, saying where and the number it measured against its limit, with no reason, no fix and no
/// symbol. The rule a finding cites says why and what to do; the message says only which thing and how far.
/// </summary>
public sealed class FindingMessageTests
{
    /// <summary>Where a message is written, and which argument of the call is the message: the plan checks' lint,
    /// the decorator's decline, whose argument is the part after the prop's kind and id, and the producibility
    /// read's findings.</summary>
    private static readonly (string File, string Call, int Argument, bool NamesItsThing)[] Writers =
    [
        ("PgmStudio.Pgm/Plan/PlanValidator.cs", "Lint", 1, true),
        ("PgmStudio.Minecraft/Dressing/Decorator.cs", "Declined", 3, false),
        ("PgmStudio.Pgm/Compose/Producibility.cs", "Finding", 1, false),
    ];

    /// <summary>Text a message never holds: a dash or a sign standing for a word, a second sentence, a reason
    /// or a fix in prose.</summary>
    private static readonly string[] Never = ["—", "–", "×", "≥", "≤", "→", " < ", " > ", ". ", " wants", " should ", " so "];

    [Test]
    public async Task Every_message_a_helper_writes_takes_the_one_shape()
    {
        var wrong = new List<string>();
        var count = 0;
        foreach (var (file, call, argument, namesItsThing) in Writers)
        {
            var path = Path.Combine(Source(), file);
            foreach (var (line, text, raw) in Messages(File.ReadAllText(path), call, argument))
            {
                count++;
                var where = $"{file}:{line} \"{text}\"";
                foreach (var never in Never)
                    if (text.Contains(never, StringComparison.Ordinal)) wrong.Add($"{where}: '{never.Trim()}'");
                if (text.EndsWith('.')) wrong.Add($"{where}: ends a sentence");
                if (text.Length > 0 && char.IsUpper(text[0])) wrong.Add($"{where}: opens with a capital");
                if (namesItsThing && !text.Contains("'{}'", StringComparison.Ordinal) && !NamesThroughAHelper.IsMatch(raw))
                    wrong.Add($"{where}: names no id");
            }
        }

        await Assert.That(count).IsGreaterThan(20);
        await Assert.That(wrong).IsEmpty();
    }

    /// <summary>The helpers that write ids already quoted: a list of them, or a piece that may be the edge.</summary>
    private static readonly Regex NamesThroughAHelper = new(@"\b(Quoted|Named)\(");

    private static string Source()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("no src/ above the test output"), "src");
    }

    /// <summary>Every call of <paramref name="call"/>, its line, the literal text of one argument, an
    /// interpolation hole read as <c>{}</c>, and the argument as written.</summary>
    private static IEnumerable<(int Line, string Text, string Raw)> Messages(string source, string call, int argument)
    {
        foreach (Match match in Regex.Matches(source, $@"(?<![\w.]){call}\("))
        {
            var args = Arguments(source, match.Index + match.Length);
            if (args is null || args.Count <= argument) continue;
            yield return (source[..match.Index].Count(c => c == '\n') + 1, Literal(args[argument]), args[argument]);
        }
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
                if (depth == 0) { args.Add(source[start..i]); return args[0].TrimStart().StartsWith("string ", StringComparison.Ordinal) ? null : args; }
                depth--;
            }
            else if (c == ',' && depth == 0) { args.Add(source[start..i]); start = i + 1; }
        }
        return null;
    }

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
