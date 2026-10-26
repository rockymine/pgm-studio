using System.Text.RegularExpressions;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every text the client shows a person, read out of its source and sorted into the channel
/// <c>docs/client/writing-for-the-ui.md</c> names for it. A text is read where it is written: a component's attribute
/// (<c>Label</c>, <c>Unit</c>, <c>Help</c>, <c>Note</c>, <c>Title</c>, <c>placeholder</c>, …), a named argument of
/// the same name, the content of a <c>Button</c>, <c>EmptyState</c> or <c>Readout</c>, a <c>SelectOption</c>, or a
/// failure sentence. A value computed in code is read as its string literals, each one a text of the channel it
/// lands in, with every interpolation reduced to <c>{x}</c>.
/// </summary>
internal static class ClientText
{
    public enum Channel
    {
        Label, Unit, Help, Term, Note, Readout, Placeholder, Tooltip, SectionTitle, Description, EmptyState,
        Option, OptionNote, Button, Failure, Reason, RawClass,
    }

    /// <summary>One text: where it is written, its channel, what it belongs to (the field's label, the button's
    /// icon), and the words, with every computed part as <c>{x}</c>.</summary>
    public sealed record Text(string Where, Channel Channel, string Owner, string Words, string Tag);

    /// <summary>Components whose <c>Title</c> is a heading rather than a tooltip.</summary>
    private static readonly HashSet<string> TitledHeadings =
        ["Section", "SectionHeader", "FilterPanel", "SideDrawer", "PageHeading", "Inspector", "Modal"];

    /// <summary>Components whose <c>Label</c> is a field's name; elsewhere a <c>Label</c> is a button's words.</summary>
    private static readonly HashSet<string> LabelledFields = ["Field", "DetailHeader", "SeedField", "RangeField", "FilterGroup"];

    /// <summary>The classes only a component may render, so a channel has one mechanism.</summary>
    private static readonly string[] OwnedClasses =
        ["section-desc", "inspector-readout", "field-note", "field-hint", "help-note-body", "list-empty", "empty-state",
         "panel-empty-msg", "rules-empty", "landing-empty"];

    public static IReadOnlyList<Text> All { get; } = Read();

    private static List<Text> Read()
    {
        var texts = new List<Text>();
        var client = Path.Combine(Root(), "src", "PgmStudio.Client");
        foreach (var file in Directory.EnumerateFiles(client, "*.*", SearchOption.AllDirectories)
                     .Where(path => path.EndsWith(".razor", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal))
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .Where(path => !Path.GetFileName(path).StartsWith("Design.razor", StringComparison.Ordinal)))
        {
            var source = File.ReadAllText(file);
            var name = Path.GetRelativePath(client, file).Replace('\\', '/');
            var inComponents = name.StartsWith("Components/", StringComparison.Ordinal);
            if (file.EndsWith(".razor", StringComparison.Ordinal)) ReadMarkup(source, name, inComponents, texts);
            ReadCode(source, name, texts);
            if (name == "Components/Data/StudioAccess.cs")
                foreach (Match reason in Regex.Matches(source, @"""([A-Z][^""]*\.)"""))
                    texts.Add(new($"{name}:{Line(source, reason.Index)}", Channel.Reason, "reason", reason.Groups[1].Value, "code"));
        }
        var scripts = Path.Combine(client, "wwwroot", "js");
        foreach (var file in Directory.EnumerateFiles(scripts, "*.js", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}vendor{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            ReadFailures(File.ReadAllText(file), Path.GetRelativePath(client, file).Replace('\\', '/'), texts);
        return texts;
    }

    private static void ReadMarkup(string source, string file, bool inComponents, List<Text> texts)
    {
        foreach (Match open in Regex.Matches(source, @"<([A-Za-z][\w.]*)(?=[\s/>])"))
        {
            var tag = open.Groups[1].Value;
            var attributes = Attributes(source, open.Index + open.Length, out var end);
            var where = $"{file}:{Line(source, open.Index)}";
            attributes.TryGetValue("Label", out var label);
            attributes.TryGetValue("Icon", out var icon);
            var heading = TitledHeadings.Contains(tag) && attributes.TryGetValue("Title", out var title) ? title : null;
            var owner = label ?? heading ?? (icon is null ? tag : $"icon {icon}");
            foreach (var (name, value) in attributes)
            {
                Channel? channel = name switch
                {
                    "Label" when LabelledFields.Contains(tag) => Channel.Label,
                    "Label" => Channel.Button,
                    "Unit" => Channel.Unit,
                    "Help" => Channel.Help,
                    "Term" => Channel.Term,
                    "Note" => Channel.Note,
                    "Description" => Channel.Description,
                    "Title" when TitledHeadings.Contains(tag) => Channel.SectionTitle,
                    "Title" when tag == "NavButton" => Channel.Button,
                    "Title" or "title" or "data-tip" => Channel.Tooltip,
                    "placeholder" or "Placeholder" or "Unbound" => Channel.Placeholder,
                    _ => null,
                };
                if (channel is { } known)
                    foreach (var literal in Literals(value)) texts.Add(new(where, known, owner, literal, tag));
                if (name == "class" && !inComponents)
                    foreach (var owned in OwnedClasses.Where(owned => Regex.IsMatch(value, $@"(^|\s){owned}(\s|$)")))
                        texts.Add(new(where, Channel.RawClass, owner, owned, tag));
            }
            if (source[..end].EndsWith("/>", StringComparison.Ordinal)) continue;
            Channel? content = tag switch
            {
                "Button" => Channel.Button,
                "EmptyState" => Channel.EmptyState,
                "Readout" => Channel.Readout,
                _ => null,
            };
            if (content is null) continue;
            var close = source.IndexOf($"</{tag}>", end, StringComparison.Ordinal);
            if (close < 0) continue;
            var words = Flatten(source[end..close]);
            if (words.Length > 0) texts.Add(new(where, content.Value, owner, words, tag));
        }
    }

    private static void ReadCode(string source, string file, List<Text> texts)
    {
        var rows = new List<(int Start, int End, string Label)>();
        foreach (Match row in Regex.Matches(source, @"\b\w+Row\(\s*""([^""]+)"""))
        {
            var depth = 0;
            var end = row.Index;
            for (var index = source.IndexOf('(', row.Index); index < source.Length; index++)
            {
                if (source[index] == '"') { index = SkipString(source, index); continue; }
                if (source[index] == '(') depth++;
                else if (source[index] == ')' && --depth == 0) { end = index; break; }
            }
            rows.Add((row.Index, end, row.Groups[1].Value));
            texts.Add(new($"{file}:{Line(source, row.Index)}", Channel.Label, row.Groups[1].Value, row.Groups[1].Value, "row"));
        }
        foreach (Match named in Regex.Matches(source, @"\b(label|unit|help|term|note|empty|title):\s*(""(?:[^""\\]|\\.)*""|\$""(?:[^""\\]|\\.)*"")"))
        {
            var channel = named.Groups[1].Value switch
            {
                "label" => Channel.Label,
                "unit" => Channel.Unit,
                "help" => Channel.Help,
                "term" => Channel.Term,
                "note" or "empty" => Channel.Note,
                _ => Channel.Tooltip,
            };
            var owner = rows.FirstOrDefault(row => named.Index > row.Start && named.Index < row.End).Label ?? "argument";
            texts.Add(new($"{file}:{Line(source, named.Index)}", channel, owner, Unquote(named.Groups[2].Value), "code"));
        }
        foreach (Match option in Regex.Matches(source, @"new(?: SelectOption)?\(\s*[^,()""]*(?:""[^""]*"")?[^,()]*,\s*(\$?""(?:[^""\\]|\\.)*"")(?:,\s*(\$?""(?:[^""\\]|\\.)*""))?\s*\)"))
        {
            if (!source[Math.Max(0, option.Index - 200)..option.Index].Contains("SelectOption", StringComparison.Ordinal)
                && !option.Value.StartsWith("new SelectOption", StringComparison.Ordinal)) continue;
            var where = $"{file}:{Line(source, option.Index)}";
            var label = Unquote(option.Groups[1].Value);
            texts.Add(new(where, Channel.Option, label, label, "option"));
            if (option.Groups[2].Success) texts.Add(new(where, Channel.OptionNote, label, Unquote(option.Groups[2].Value), "option"));
        }
        foreach (Match constant in Regex.Matches(source, @"\bconst string (\w*?)(Note|Help|Label|Title|Description|Tooltip)\s*=\s*(""(?:[^""\\]|\\.)*"")"))
        {
            var channel = constant.Groups[2].Value switch
            {
                "Note" => Channel.Note,
                "Help" => Channel.Help,
                "Label" => Channel.Label,
                "Description" => Channel.Description,
                _ => Channel.Tooltip,
            };
            texts.Add(new($"{file}:{Line(source, constant.Index)}", channel, "constant", Unquote(constant.Groups[3].Value), "code"));
        }
        ReadFailures(source, file, texts);
    }

    /// <summary>Every failure sentence a file writes, in C# or in a script: a literal opening with what failed.</summary>
    private static void ReadFailures(string source, string file, List<Text> texts)
    {
        foreach (Match failure in Regex.Matches(source, @"\$?""((?:Couldn't|Could not|Can't|Cannot|Failed|Unable)\b(?:[^""\\]|\\.)*)""|`((?:Couldn't|Could not|Can't|Cannot|Failed|Unable)\b[^`]*)`"))
        {
            var words = failure.Value.StartsWith('`')
                ? System.Text.RegularExpressions.Regex.Replace(failure.Groups[2].Value, @"\$\{[^}]*\}", "{x}")
                : Unquote(failure.Value);
            texts.Add(new($"{file}:{Line(source, failure.Index)}", Channel.Failure, "failure", words, "code"));
        }
    }

    /// <summary>The attributes of a start tag, by name, from <paramref name="at"/> to the tag's end. A value is
    /// read whole even where a Razor expression nests quotes inside it.</summary>
    private static Dictionary<string, string> Attributes(string source, int at, out int end)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = at;
        while (index < source.Length)
        {
            while (index < source.Length && char.IsWhiteSpace(source[index])) index++;
            if (index >= source.Length) break;
            if (source[index] == '>') { index++; break; }
            if (source[index] == '/' && index + 1 < source.Length && source[index + 1] == '>') { index += 2; break; }
            var nameStart = index;
            while (index < source.Length && !char.IsWhiteSpace(source[index]) && source[index] is not ('=' or '>' or '/')) index++;
            var name = source[nameStart..index];
            if (index < source.Length && source[index] == '=')
            {
                index++;
                if (index < source.Length && source[index] is '"' or '\'')
                {
                    var quote = source[index];
                    index++;
                    var valueStart = index;
                    if (source.AsSpan(index).StartsWith("@("))
                    {
                        var depth = 0;
                        for (index++; index < source.Length; index++)
                        {
                            if (source[index] == '"' && depth > 0) { index = SkipString(source, index); continue; }
                            if (source[index] == '(') depth++;
                            else if (source[index] == ')' && --depth == 0) { index++; break; }
                        }
                    }
                    while (index < source.Length && source[index] != quote) index++;
                    attributes[name] = source[valueStart..index];
                    index++;
                }
                else
                {
                    var valueStart = index;
                    while (index < source.Length && !char.IsWhiteSpace(source[index]) && source[index] != '>') index++;
                    attributes[name] = source[valueStart..index];
                }
            }
            else if (name.Length == 0) index++;
        }
        end = index;
        return attributes;
    }

    private static int SkipString(string source, int quote)
    {
        var index = quote + 1;
        while (index < source.Length && source[index] != '"') index += source[index] == '\\' ? 2 : 1;
        return index;
    }

    /// <summary>The texts an attribute value holds: itself where it is plain words, or each string literal
    /// inside a Razor expression; an expression with no literal is not a text.</summary>
    private static IEnumerable<string> Literals(string value)
    {
        if (!value.Contains('@')) { if (value.Length > 0) yield return value; yield break; }
        if (!value.StartsWith("@(", StringComparison.Ordinal) && !value.StartsWith('@'))
        {
            yield return Regex.Replace(value, @"@\([^)]*\)|@[\w.]+(\([^)]*\))?", "{x}");
            yield break;
        }
        foreach (Match literal in Regex.Matches(value, @"\$?""(?:[^""\\]|\\.)*"""))
        {
            var words = Unquote(literal.Value);
            if (words.Length > 0 && Regex.IsMatch(words, @"[A-Za-z]{2}")) yield return words;
        }
    }

    /// <summary>A C# string literal's words, every interpolation as <c>{x}</c>.</summary>
    private static string Unquote(string literal)
    {
        var interpolated = literal.StartsWith('$');
        var body = literal[(interpolated ? 2 : 1)..^1].Replace("\\\"", "\"");
        return interpolated ? Regex.Replace(body, @"\{[^{}]*(\{[^{}]*\}[^{}]*)*\}", "{x}") : body;
    }

    /// <summary>Markup content as the words it shows: tags dropped, every Razor expression as <c>{x}</c>.</summary>
    private static string Flatten(string body)
    {
        const string Computed = "\u0001";
        var words = Regex.Replace(body, @"<(?:[^>""']|""[^""]*""|'[^']*')*>", " ");
        words = Regex.Replace(words, @"@\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)", Computed);
        words = Regex.Replace(words, @"@(if|foreach|for|while)\s*\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)\s*\{|\belse\s*\{|\}", " ");
        words = Regex.Replace(words, @"@[\w.]+(?:\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\))?", Computed);
        return Regex.Replace(words, @"\s+", " ").Trim().Replace(Computed, "{x}");
    }

    private static int Line(string source, int index) => source.AsSpan(0, index).Count('\n') + 1;

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "PgmStudio.Client")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("src/PgmStudio.Client not found above the test binaries");
    }
}
