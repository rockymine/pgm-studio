using System.Text.RegularExpressions;
using PgmStudio.Client.Components;
using PgmStudio.Vocabulary;
using static PgmStudio.Api.Tests.ClientText;

namespace PgmStudio.Api.Tests;

/// <summary>
/// Every text the client shows, held to <c>docs/client/writing-for-the-ui.md</c>: each channel's form and length, one
/// spelling per meaning, one explanation per label, and the glossary's words. The counterpart of
/// <see cref="RuleTextTests"/> for the screens.
/// </summary>
public sealed class ClientTextTests
{
    /// <summary>Labels that explain themselves, so a "?" on one only repeats it.</summary>
    private static readonly HashSet<string> TrivialHelp = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name", "Seed", "Mode", "Height", "Width", "Depth", "Material", "Count", "Size", "Length", "Delete", "Team",
        "Colour", "Kind", "Shape", "Title", "Slug", "Author", "Authors", "Version",
    };

    /// <summary>Acronyms and proper names a text may write in capitals or capitalize mid-sentence.</summary>
    private static readonly HashSet<string> Proper = new(StringComparer.Ordinal)
    {
        "PGM", "API", "ZIP", "XML", "JSON", "ID", "URL", "OK", "PNG", "SVG", "WebGL", "Minecraft", "Discord", "GitHub",
        "Mojang", "Overcast", "Esc", "Shift", "Alt", "Ctrl", "Enter", "Tab", "Space", "Delete", "Backspace", "X", "Y", "Z",
        "King", "Hill", "Flag", "Capture", "Wool", "Destroy", "Core", "Monument", "Team", "Deathmatch", "Blitz", "Rage",
        "Scorebox", "Plains", "Nether", "End", "Overworld",
        "Plan", "Sketch", "Configure", "Library", "Generator", "Glossary", "Rules", "Catalog",
    };

    /// <summary>The action each icon names, as the tooltip's first word, wherever the icon is a button.</summary>
    private static readonly Dictionary<string, string[]> IconVerbs = new(StringComparer.Ordinal)
    {
        ["x"] = ["Close"],
        ["trash-2"] = ["Delete"],
        ["trash"] = ["Delete"],
        ["eye"] = ["Switch", "Show", "Hide"],
        ["eye-off"] = ["Switch", "Show", "Hide"],
        ["chevron-up"] = ["Move"],
        ["chevron-down"] = ["Move"],
        ["plus"] = ["Add"],
        ["copy"] = ["Duplicate", "Copy"],
        ["pencil"] = ["Rename", "Edit"],
        ["hand"] = ["Pan"],
        ["rotate-cw"] = ["Turn"],
        ["rotate-ccw"] = ["Turn"],
        ["scan"] = ["Zoom"],
    };

    /// <summary>What a "New" button may start: a document of its own. An entry added to a list is "Add a …".</summary>
    private static readonly HashSet<string> Documents = new(StringComparer.OrdinalIgnoreCase)
        { "map", "note", "token", "plan", "sketch", "{x}" };

    /// <summary>Uncountable nouns an empty list names in the singular.</summary>
    private static readonly HashSet<string> MassNouns = new(StringComparer.OrdinalIgnoreCase)
        { "XML", "protection", "selection", "{x}" };

    private static readonly Channel[] Short =
        [Channel.Label, Channel.Unit, Channel.Placeholder, Channel.Tooltip, Channel.SectionTitle, Channel.Option, Channel.Button];

    private static readonly Channel[] Sentences =
        [Channel.Help, Channel.Note, Channel.Readout, Channel.Description, Channel.EmptyState, Channel.OptionNote, Channel.Failure];

    private static IEnumerable<Text> Of(params Channel[] channels) => All.Where(text => channels.Contains(text.Channel));

    private static int Words(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private static string Prose(string text) => Regex.Replace(text, @"\{x\}", "");

    private static async Task Holds(IEnumerable<string> wrong, string rule)
    {
        var found = wrong.Distinct().ToList();
        await Assert.That(found).IsEmpty()
            .Because($"{found.Count} text(s) break \"{rule}\" (docs/client/writing-for-the-ui.md):{Environment.NewLine}  "
                     + string.Join($"{Environment.NewLine}  ", found));
    }

    /// <summary>A guard on the harness: a reader that matched nothing would pass every test below.</summary>
    [Test]
    public async Task The_reader_finds_every_channel()
    {
        foreach (var channel in new[] { Channel.Label, Channel.Tooltip, Channel.SectionTitle, Channel.Button, Channel.Option })
            await Assert.That(Of(channel).Count()).IsGreaterThan(40).Because($"{channel} texts were not read");
    }

    /// <summary>A channel has one mechanism: the classes a component renders are never written by hand.</summary>
    [Test]
    public async Task Every_channel_has_one_mechanism() =>
        await Holds(Of(Channel.RawClass).Select(text => $"{text.Where}: class {text.Words} outside its component"),
            "a channel has one mechanism");

    [Test]
    public async Task Every_label_is_a_name()
    {
        var wrong = new List<string>();
        foreach (var text in Of(Channel.Label, Channel.SectionTitle).Where(text => !text.Words.Contains("{x}")))
        {
            var limit = text.Channel == Channel.Label ? 4 : 3;
            if (Words(text.Words) > limit) wrong.Add($"{text.Where}: '{text.Words}' is over {limit} words");
            if (Regex.IsMatch(text.Words, @"[.:;?!]$")) wrong.Add($"{text.Where}: '{text.Words}' ends in punctuation");
            if (text.Words.Contains('(')) wrong.Add($"{text.Where}: '{text.Words}' carries a parenthesis; a unit is Unit");
            if (Regex.IsMatch(text.Words, @"^(Where|Which|How|What|Why|When|Whether)\b"))
                wrong.Add($"{text.Where}: '{text.Words}' is a question");
        }
        await Holds(wrong, "a label names; it never explains");
    }

    [Test]
    public async Task Every_unit_is_one_of_the_units() =>
        await Holds(Of(Channel.Unit).Where(text => !FieldUnits.All.Contains(text.Words))
            .Select(text => $"{text.Where}: '{text.Words}'"), "a unit is one word from one list");

    [Test]
    public async Task Every_help_defines_what_the_label_cannot()
    {
        var wrong = new List<string>();
        foreach (var text in Of(Channel.Help))
        {
            if (Words(text.Words) > 40) wrong.Add($"{text.Where}: help on '{text.Owner}' is {Words(text.Words)} words");
            var sentences = Regex.Split(text.Words.Trim(), @"(?<=[.!?])\s+").Where(sentence => sentence.Length > 0).ToList();
            if (sentences.Count > 3) wrong.Add($"{text.Where}: help on '{text.Owner}' is {sentences.Count} sentences");
            if (!text.Words.TrimEnd().EndsWith('.')) wrong.Add($"{text.Where}: help on '{text.Owner}' does not end with a stop");
            if (TrivialHelp.Contains(text.Owner)) wrong.Add($"{text.Where}: '{text.Owner}' explains itself and takes no help");
            if (HelpNote.Defined(text.Owner) is not null)
                wrong.Add($"{text.Where}: '{text.Owner}' is a glossary term; say Term=\"{text.Owner.ToLowerInvariant()}\"");
            var first = Regex.Match(text.Words, @"^\w+").Value;
            if (first.Length > 0 && string.Equals(first, text.Owner.Split(' ')[0], StringComparison.OrdinalIgnoreCase))
                wrong.Add($"{text.Where}: help on '{text.Owner}' opens with its own label");
        }
        await Holds(wrong, "help defines; it never restates the label");
    }

    [Test]
    public async Task Every_term_is_in_the_glossary() =>
        await Holds(Of(Channel.Term).Where(text => HelpNote.Defined(text.Words) is null)
            .Select(text => $"{text.Where}: '{text.Words}'"), "a term is defined once, in the glossary");

    [Test]
    public async Task Every_sentence_channel_is_sentences_of_its_length()
    {
        var limits = new Dictionary<Channel, int>
        {
            [Channel.Note] = 20, [Channel.Readout] = 25, [Channel.Description] = 25, [Channel.EmptyState] = 20,
            [Channel.OptionNote] = 12, [Channel.Failure] = 20,
        };
        var wrong = new List<string>();
        foreach (var text in Of([.. limits.Keys]))
        {
            if (Words(text.Words) > limits[text.Channel])
                wrong.Add($"{text.Where}: {text.Channel} '{text.Words}' is over {limits[text.Channel]} words");
            var compact = text.Channel == Channel.Readout && text.Words.Contains(" · ", StringComparison.Ordinal);
            if (!compact && (!Regex.IsMatch(text.Words.TrimEnd(), @"[.!?]$|\{x\}$") || !char.IsUpper(text.Words.TrimStart('{', 'x', '}', ' ').FirstOrDefault('A'))))
                wrong.Add($"{text.Where}: {text.Channel} '{text.Words}' is not a sentence");
        }
        await Holds(wrong, "a note, readout, description and empty state are sentences");
    }

    [Test]
    public async Task Every_readout_is_computed() =>
        await Holds(Of(Channel.Readout).Where(text => !text.Words.Contains("{x}"))
            .Select(text => $"{text.Where}: '{text.Words}' computes nothing"), "a readout is computed");

    [Test]
    public async Task Every_empty_state_says_no() =>
        await Holds(Of(Channel.EmptyState).Where(text => !text.Words.StartsWith("No ", StringComparison.Ordinal))
            .Select(text => $"{text.Where}: '{text.Words}'"), "an empty state opens with No");

    [Test]
    public async Task Every_placeholder_names_the_unset_value()
    {
        var wrong = new List<string>();
        foreach (var text in Of(Channel.Placeholder).Where(text => !text.Words.Contains("{x}")))
        {
            if (Words(text.Words) > 3) wrong.Add($"{text.Where}: '{text.Words}' is over 3 words");
            if (Regex.IsMatch(text.Words, @"[.:;(]|^e\.g|^None\b|^Enter\b|^Type\b")) wrong.Add($"{text.Where}: '{text.Words}' instructs");
        }
        await Holds(wrong, "a placeholder names the unset value");
    }

    [Test]
    public async Task Every_tooltip_names_an_action()
    {
        var wrong = new List<string>();
        foreach (var text in Of(Channel.Tooltip).Where(text => text.Words != "{x}"))
        {
            var words = Regex.Replace(text.Words, @"\s*\((Esc|Shift|Alt|Ctrl|[A-Z]|[a-z]|\d)[^)]*\)$", "");
            if (Words(words) > 6) wrong.Add($"{text.Where}: '{text.Words}' is over 6 words");
            if (Regex.IsMatch(words, @"[.:;]$|: ")) wrong.Add($"{text.Where}: '{text.Words}' is a sentence");
        }
        foreach (var group in Of(Channel.Tooltip).Where(text => text.Owner.StartsWith("icon ", StringComparison.Ordinal)))
        {
            var icon = group.Owner[5..];
            if (!IconVerbs.TryGetValue(icon, out var verbs) || group.Words.StartsWith('{')) continue;
            if (!verbs.Any(verb => group.Words.StartsWith(verb, StringComparison.Ordinal)))
                wrong.Add($"{group.Where}: icon {icon} is '{group.Words}', where it is {string.Join(" or ", verbs)}");
        }
        await Holds(wrong, "a tooltip names an action that has no visible word");
    }

    [Test]
    public async Task Every_option_is_a_name()
    {
        var wrong = new List<string>();
        foreach (var text in Of(Channel.Option).Where(text => !text.Words.Contains("{x}")))
        {
            if (Words(text.Words) > 3) wrong.Add($"{text.Where}: '{text.Words}' is over 3 words");
            if (Regex.IsMatch(text.Words, @"[.:;(]")) wrong.Add($"{text.Where}: '{text.Words}' carries punctuation");
        }
        await Holds(wrong, "an option is a name, and its sentence is its note");
    }

    [Test]
    public async Task Every_button_is_a_verb()
    {
        var wrong = new List<string>();
        foreach (var text in Of(Channel.Button).Where(text => !text.Words.Contains("{x}")))
        {
            if (Words(text.Words) > 4) wrong.Add($"{text.Where}: '{text.Words}' is over 4 words");
            if (Regex.IsMatch(text.Words, @"[.:;]$")) wrong.Add($"{text.Where}: '{text.Words}' ends in punctuation");
        }
        foreach (var text in Of(Channel.Button, Channel.Tooltip).Where(text => Regex.IsMatch(text.Words, @"^Remove\b")))
            wrong.Add($"{text.Where}: '{text.Words}' — deleting an entry is Delete");
        await Holds(wrong, "a button is a verb, and deleting is Delete");
    }

    [Test]
    public async Task Every_failure_says_what_to_do() =>
        await Holds(Of(Channel.Failure)
            .Where(text => !Regex.IsMatch(text.Words, @"^Couldn't [^.]+\. ") || text.Words.Contains("HTTP", StringComparison.Ordinal))
            .Select(text => $"{text.Where}: '{text.Words}'"), "Couldn't … . and what to do");

    /// <summary>A label means one thing: every field of that label carries one unit, one help and one note.</summary>
    [Test]
    public async Task Every_label_is_explained_one_way()
    {
        var wrong = new List<string>();
        foreach (var channel in new[] { Channel.Unit, Channel.Help, Channel.Term })
            foreach (var label in Of(channel).Where(text => text.Owner != text.Tag && text.Owner is not ("argument" or "row" or "constant")
                         && !text.Owner.Contains('@') && !text.Words.Contains("{x}"))
                         .GroupBy(text => text.Owner, StringComparer.OrdinalIgnoreCase))
            {
                var wordings = label.Select(text => text.Words).Distinct().ToList();
                if (wordings.Count > 1)
                    wrong.Add($"'{label.Key}' has {wordings.Count} {channel}s: {string.Join(" | ", label.Select(text => $"{text.Where} '{text.Words}'"))}");
            }
        await Holds(wrong, "one label, one meaning, one explanation");
    }

    /// <summary>Every text in sentence case: nothing in capitals but an acronym or a proper name.</summary>
    [Test]
    public async Task Every_text_is_in_sentence_case()
    {
        var wrong = new List<string>();
        foreach (var text in All.Where(text => text.Channel != Channel.RawClass))
        {
            var prose = Prose(text.Words);
            foreach (Match word in Regex.Matches(prose, @"\b[A-Z]{2,}\b"))
                if (!Proper.Contains(word.Value)) wrong.Add($"{text.Where}: '{word.Value}' in capitals");
            if (Short.Contains(text.Channel) && text.Channel != Channel.Unit)
                foreach (Match word in Regex.Matches(prose, @"(?<=\w\s)[A-Z][a-z]+\b"))
                    if (!Proper.Contains(word.Value)) wrong.Add($"{text.Where}: '{text.Words}' capitalizes '{word.Value}'");
            if (prose.IndexOfAny(['—', '–', '→', '≥', '≤', ';', '=', '’', '‘', '“', '”']) >= 0)
                wrong.Add($"{text.Where}: '{text.Words}' carries a symbol");
        }
        await Holds(wrong, "sentence case, plain characters");
    }

    /// <summary>The glossary's words: a name the glossary has retired is never shown, nor its plural. A term that
    /// contains a retired word ("team deathmatch") is read as the term.</summary>
    [Test]
    public async Task No_text_uses_a_retired_word()
    {
        var wrong = new List<string>();
        foreach (var text in All.Where(text => text.Channel is not (Channel.RawClass or Channel.Term)))
            foreach (var (name, term) in Retired(Prose(text.Words)))
                wrong.Add($"{text.Where}: '{text.Words}' says '{name}', which the glossary calls '{term}'");
        await Holds(wrong, "one spelling per meaning");
    }

    private static readonly List<(string Name, string Term)> RetiredNames = [.. Glossary.Terms
        .SelectMany(term => term.AlsoCalled.Select(name => (name, term.Term))).OrderByDescending(pair => pair.name.Length)];

    /// <summary>A term holding a retired name as a word ("team deathmatch" holds "deathmatch") is read as the term.</summary>
    private static readonly List<string> TermsHoldingARetiredName = [.. Glossary.Terms.Select(term => term.Term)
        .Where(term => RetiredNames.Any(pair => !pair.Name.Equals(term, StringComparison.OrdinalIgnoreCase)
            && Regex.IsMatch(term, $@"(?<![\w-]){Regex.Escape(pair.Name)}(?![\w-])", RegexOptions.IgnoreCase)))
        .OrderByDescending(term => term.Length)];

    /// <summary>Every retired name a text uses, singular or plural, with the term the glossary calls it.</summary>
    private static IEnumerable<(string Name, string Term)> Retired(string prose)
    {
        foreach (var term in TermsHoldingARetiredName)
            prose = Regex.Replace(prose, $@"(?<![\w-]){Regex.Escape(term)}(?![\w-])", " ", RegexOptions.IgnoreCase);
        foreach (var (name, term) in RetiredNames)
            if (Regex.IsMatch(prose, $@"(?<![\w-]){Regex.Escape(name)}(s|es)?(?![\w-])", RegexOptions.IgnoreCase))
                yield return (name, term);
    }

    /// <summary>The glossary holds itself to the same: no definition uses a name it has retired.</summary>
    [Test]
    public async Task No_definition_uses_a_retired_word()
    {
        var wrong = Glossary.Terms.SelectMany(entry => Retired(entry.Definition)
            .Select(pair => $"{entry.Term}: says '{pair.Name}', which the glossary calls '{pair.Term}'"));
        await Holds(wrong, "the glossary uses its own words");
    }

    /// <summary>One form per situation (writing-for-the-ui.md, "One form per situation"): an empty list in the plural
    /// with "yet", an empty selection "selected", making a document "Create", a failure naming "the" thing, a failed
    /// load ending the one way, an unreachable studio said one way.</summary>
    [Test]
    public async Task Every_situation_is_said_one_way()
    {
        var wrong = new List<string>();
        foreach (var text in Of(Channel.EmptyState))
        {
            var empty = System.Text.RegularExpressions.Regex.Match(text.Words, @"^No (\S+)(?: [a-z]+)*? (yet|selected|picked|chosen)\b");
            if (empty.Success && empty.Groups[2].Value is "picked" or "chosen")
                wrong.Add($"{text.Where}: '{text.Words}' — an empty selection is 'No … selected.'");
            // The head noun is the last word before the first preposition or "yet": "No players on the whitelist yet".
            var list = System.Text.RegularExpressions.Regex.Match(text.Words,
                @"^No ((?:\S+ )*?)(\S+) (?:on|in|to|for|of|at|with|yet)\b(?=.*\byet\b)");
            if (list.Success && !list.Groups[2].Value.EndsWith('s') && !MassNouns.Contains(list.Groups[2].Value))
                wrong.Add($"{text.Where}: '{text.Words}' — an empty list names it in the plural");
        }
        foreach (var text in Of(Channel.EmptyState, Channel.Description, Channel.Note, Channel.Help))
            if (System.Text.RegularExpressions.Regex.IsMatch(text.Words, @"\b(Make|Build|Start|Issue) one\b"))
                wrong.Add($"{text.Where}: '{text.Words}' — making a document is 'Create one'");
        foreach (var text in Of(Channel.Failure))
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(text.Words, @"^Couldn't \w+ (?:a copy of )?(this|that)\b"))
                wrong.Add($"{text.Where}: '{text.Words}' — a failure names 'the' thing");
            if (text.Words.StartsWith("Couldn't load ", StringComparison.Ordinal)
                && !text.Words.EndsWith(". Reload the page to try again.", StringComparison.Ordinal))
                wrong.Add($"{text.Where}: '{text.Words}' — a failed load ends 'Reload the page to try again.'");
            if (text.Words.StartsWith("Couldn't reach the studio", StringComparison.Ordinal)
                && text.Words != "Couldn't reach the studio. Check your connection and try again.")
                wrong.Add($"{text.Where}: '{text.Words}' — an unreachable studio is said one way");
        }
        foreach (var text in Of(Channel.Button).Where(text => !text.Words.Contains("{x}") || text.Words.StartsWith("New ")))
        {
            if (text.Words == "Add") wrong.Add($"{text.Where}: 'Add' names what it adds");
            var made = System.Text.RegularExpressions.Regex.Match(text.Words, @"^New (.+)$");
            if (made.Success && !Documents.Contains(made.Groups[1].Value))
                wrong.Add($"{text.Where}: '{text.Words}' — an entry in a list is 'Add a {made.Groups[1].Value}'");
        }
        foreach (var text in Of(Channel.Tooltip).Where(text => text.Owner is "icon trash-2" or "icon trash" or "InspectorDelete"))
            if (!text.Words.StartsWith("Delete this ", StringComparison.Ordinal))
                wrong.Add($"{text.Where}: '{text.Words}' — a delete names its thing: 'Delete this …'");
        foreach (var text in Of(Channel.Note))
            if (System.Text.RegularExpressions.Regex.IsMatch(text.Words, @"(?i)\b(automatically|works it out|left empty)\b"))
                wrong.Add($"{text.Where}: '{text.Words}' — what empty means is 'Auto: …'");
        foreach (var text in Of(Channel.Placeholder))
            if (System.Text.RegularExpressions.Regex.IsMatch(text.Words, @"^\d|…$|\.\.\.$"))
                wrong.Add($"{text.Where}: '{text.Words}' — a placeholder is no number, example or ellipsis");
        foreach (var text in Of(Channel.Description, Channel.EmptyState, Channel.Note))
            if (System.Text.RegularExpressions.Regex.IsMatch(text.Words, @"\bDraw (a|one) (box|rectangle)\b|\bDraw one\b.*rectangle"))
                wrong.Add($"{text.Where}: '{text.Words}' — a rectangle is dragged: 'Drag a rectangle'");
        foreach (var text in All.Where(text => System.Text.RegularExpressions.Regex.IsMatch(text.Words, @"(\{x\}|\d+) courses?\b")))
            wrong.Add($"{text.Where}: '{text.Words}' — a height is counted in blocks, as its field says");
        foreach (var option in Of(Channel.OptionNote).Where(text => !text.Words.Contains("{x}")).GroupBy(text => text.Owner))
            if (option.Select(text => text.Words).Distinct().Count() > 1)
                wrong.Add($"option '{option.Key}' has {option.Select(text => text.Words).Distinct().Count()} notes: "
                          + string.Join(" | ", option.Select(text => $"{text.Where} '{text.Words}'")));
        await Holds(wrong, "one form per situation");
    }
}
