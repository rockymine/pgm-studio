using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PgmStudio.Vocabulary;

namespace PgmStudio.Domain;

/// <summary>
/// One rule, as a reader meets it. <see cref="Rule"/> is the stable id a finding carries and a client keys on;
/// <see cref="Family"/> groups it by what it is about rather than by which gate asks; <see cref="Owner"/> is
/// where it is stated, which is the file to read next. <see cref="Means"/> and <see cref="Fix"/> are the
/// rule's own two sentences — what it refuses, and what to do about it.
/// </summary>
/// <param name="Evidence">For a layout rule, how well the corpus backs it — <c>corpus</c>, <c>expert</c>,
/// <c>open</c> or <c>guess</c>, in <c>rules.md</c>'s own terms. Null for a gate rule, which is code rather
/// than a measured claim about how a map plays.</param>
/// <param name="Category">What a caller does about a finding citing this rule, from the <c>[Rule]</c>
/// attribute beside the constant. Null for a layout rule, which has no declaration site to carry one, and
/// for a gate rule nothing raises.</param>
/// <param name="Concerns">What the rule is about — one word or several, since a rule concerns a combination
/// a family prefix cannot carry. Empty for a layout rule.</param>
public sealed record RuleDoc(
    string Rule, string Family, string Owner, string Means, string? Fix = null, string? Evidence = null,
    RuleCategory? Category = null, IReadOnlyList<RuleConcern>? Concerns = null)
{
    /// <summary>What the rule is about, never null.</summary>
    public IReadOnlyList<RuleConcern> About => Concerns ?? [];
}

/// <summary>
/// <b>Every rule the studio can cite, in one list.</b> A finding carries an id and a sentence about the one
/// document it was refused over; nothing anywhere answered "what is <c>WL2</c>", and the ids outlive the
/// tasks that added them, so a reader meeting one in a refusal a year later had nowhere to look it up.
///
/// <para><b>Nothing here is written twice.</b> A gate rule's meaning is read out of the docstring beside its
/// own <c>const</c>, through the XML documentation file the compiler emits — so the sentence a caller is
/// shown is the sentence in the source, and there is no catalogue entry to fall out of step with it. Its fix
/// is the <c>&lt;remarks&gt;</c> of the same docstring, and its category and concerns are the
/// <see cref="RuleAttribute"/> beside it, which is why adding a rule prompts for all four at the
/// point the rule is written rather than in a table somewhere else. A layout rule comes out of
/// <c>docs/generator/rules.md</c>, embedded in this assembly and parsed: that document is the rule law and is
/// amended only by its own correction protocol, so copying its statements into C# would have produced a
/// second law that drifts.</para>
///
/// <para><b>Only the layout rules the studio can cite are published.</b> <c>rules.md</c> states 92 and the
/// catalogue answers the <see cref="Raised"/> subset of them, because the question it exists for is <i>what
/// is this finding</i> and a rule nothing raises has no finding to explain. The rest are the generator's
/// law, stated where the law lives and amended by its own correction protocol; publishing them in rows
/// identical to the ones a caller can actually fail on makes every row less informative. Every gate rule is
/// published, including the four <c>WX</c> constants nothing raises — those state how a room frame is
/// derived, and the catalogue is the studio's own answer to what one means.</para>
///
/// <para><b>A layout rule has no fix, and that is not an omission.</b> The gate rules are mechanical — a
/// doorway too short, a document that will not parse — and what to do about one is derivable. The layout
/// rules are claims about how a map is played, which <c>CLAUDE.md</c> says are the author's to state and not
/// this repository's to infer. What is offered instead is the evidence tag <c>rules.md</c> already carries,
/// which says how far to trust each one.</para>
/// </summary>
public static class RuleCatalog
{
    /// <summary>
    /// The layout rules the studio can name in an answer, and so the ones the catalogue publishes. Three
    /// kinds of site cite one: the plan validator's lints, an evaluator term's <c>RuleId</c>, and a
    /// producibility finding's <c>Cites</c>. None of the three is reachable from here — they are all in
    /// <c>Pgm</c>, which is above this — so the set is stated, and <c>RulesEndpointTests</c> holds it to the
    /// source in both directions: an id here that nothing cites fails, and an id cited that is not here
    /// fails too.
    /// </summary>
    public static readonly IReadOnlySet<string> Raised = new HashSet<string>(StringComparer.Ordinal)
    {
        "BZ5", "BZ6", "BZ9", "BZ11",
        "CT1", "CT4", "CT5", "CT8", "CT9", "CT12",
        "EL1",
        "FR4", "FR6", "FR8",
        "G2", "G5", "G8",
        "GO1",
        "LN1", "LN2",
        "SP1", "SP2", "SP8", "SP9",
        "ST1", "ST2", "ST8", "ST9",
        "WL1", "WL2", "WL7", "WL8", "WL9", "WL10", "WL11",
    };

    /// <summary>A rule id: two or three letters, then a number or a single-letter suffix (<c>PC-C</c>).</summary>
    private static readonly Regex IdShape = new(@"^[A-Z]{2,3}(?:-[A-Z]+|[0-9]+)$", RegexOptions.Compiled);

    private static readonly Regex LayoutRule = new(
        @"^- \*\*(?<id>[A-Z]{1,3}(?:-[A-Z]+|[0-9]+))\b", RegexOptions.Compiled);

    private static readonly Regex EvidenceTag = new(@"\[(corpus|expert|open|guess)\]", RegexOptions.Compiled);

    /// <summary>Every rule the given assemblies declare, plus every layout rule, ordered by family then by the
    /// number inside the id — so <c>PL2</c> precedes <c>PL10</c>, which sorting the strings would not.</summary>
    /// <param name="assemblies">The assemblies to read gate rules out of. Named by the caller rather than
    /// discovered, because an assembly nothing has touched yet is not loaded and would be silently missing.</param>
    public static IReadOnlyList<RuleDoc> Read(IEnumerable<Assembly> assemblies)
    {
        var rules = assemblies.SelectMany(GateRules).Concat(LayoutRules()).ToList();
        return [.. rules.OrderBy(rule => rule.Family, StringComparer.Ordinal).ThenBy(Ordinal).ThenBy(rule => rule.Rule, StringComparer.Ordinal)];
    }

    /// <summary>The number inside an id, for ordering. A suffixed id (<c>PC-C</c>) has none and sorts first.</summary>
    private static int Ordinal(RuleDoc rule) =>
        int.TryParse(new string([.. rule.Rule.Where(char.IsDigit)]), out var number) ? number : 0;

    /// <summary>The family an id belongs to: its letters. Grouping by what a rule is about is what keeps the
    /// same objective rule one rule when the compile gate and the export gate both ask it.</summary>
    private static string FamilyOf(string rule) => new([.. rule.TakeWhile(char.IsAsciiLetterUpper)]);

    // ── the gate rules: the constants, and their own docstrings ───────────────────────────────────────────

    /// <summary>Every <c>const string</c> in the assembly whose value is shaped like a rule id, with the
    /// summary and remarks the compiler wrote out beside it. Private and nested declarations are read too —
    /// <c>ExportRules</c> is private inside <c>MapExportComposer</c>, and a rule that is not listed because of
    /// where it happens to be declared is the failure this exists to prevent.</summary>
    private static IEnumerable<RuleDoc> GateRules(Assembly assembly)
    {
        var docs = Documentation(assembly);
        foreach (var type in Types(assembly))
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (!field.IsLiteral || field.FieldType != typeof(string)) continue;
                if (field.GetRawConstantValue() is not string id || !IdShape.IsMatch(id)) continue;

                var (summary, remarks) = docs.GetValueOrDefault($"F:{type.FullName!.Replace('+', '.')}.{field.Name}");
                var classification = field.GetCustomAttribute<RuleAttribute>();
                yield return new RuleDoc(
                    id, FamilyOf(id), $"{type.FullName!.Replace('+', '.')}.{field.Name}",
                    summary ?? "", remarks,
                    Category: classification?.Category, Concerns: classification?.Concerns);
            }
    }

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        // A type that will not load takes the whole sweep with it otherwise, and a catalogue that answers
        // nothing is worse than one missing a family.
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
    }

    /// <summary>The assembly's XML documentation file, as member name → (summary, remarks). Empty when the
    /// file is absent, which leaves the ids listed with no sentence rather than dropping them: an id with no
    /// description still answers "does this rule exist and who owns it".</summary>
    private static Dictionary<string, (string? Summary, string? Remarks)> Documentation(Assembly assembly)
    {
        var path = Path.ChangeExtension(assembly.Location, ".xml");
        if (assembly.Location.Length == 0 || !File.Exists(path)) return [];

        try
        {
            return XDocument.Load(path).Descendants("member")
                .Where(member => member.Attribute("name") is not null)
                .ToDictionary(
                    member => member.Attribute("name")!.Value,
                    member => (Prose(member.Element("summary")), Prose(member.Element("remarks"))));
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
        {
            return [];
        }
    }

    /// <summary>An XML doc element as one line of prose: its text with the markup flattened, a
    /// <c>&lt;see cref&gt;</c> reduced to the name it points at, and every run of whitespace collapsed — the
    /// docstrings are hard-wrapped in the source and would otherwise arrive with the wrapping in them.</summary>
    private static string? Prose(XElement? element)
    {
        if (element is null) return null;
        var text = new StringBuilder();
        foreach (var node in element.DescendantNodes())
        {
            if (node is XText plain) text.Append(plain.Value);
            else if (node is XElement { Name.LocalName: "see" or "seealso" } reference)
                text.Append(Referenced(reference));
        }
        var collapsed = Regex.Replace(text.ToString(), @"\s+", " ").Trim();
        return collapsed.Length == 0 ? null : collapsed;
    }

    /// <summary>What a <c>&lt;see&gt;</c> points at, as the reader would say it: the member or type name
    /// without its namespace and without the <c>T:</c>/<c>F:</c>/<c>M:</c> prefix the compiler adds.</summary>
    private static string Referenced(XElement reference)
    {
        var target = reference.Attribute("cref")?.Value ?? reference.Attribute("langword")?.Value ?? "";
        if (target.Length > 2 && target[1] == ':') target = target[2..];
        if (target.IndexOf('(') is var arguments and > 0) target = target[..arguments];
        return target.Split('.').LastOrDefault() ?? target;
    }

    // ── the layout rules: rules.md itself ─────────────────────────────────────────────────────────────────

    /// <summary>Every <see cref="Raised"/> rule stated in <c>docs/generator/rules.md</c>, which is embedded in
    /// this assembly so the catalogue reads the law rather than a copy of it. A rule runs from its own bullet
    /// to the next one, so the sub-bullets under <c>CT1</c>'s forms stay part of <c>CT1</c>.</summary>
    private static IEnumerable<RuleDoc> LayoutRules()
    {
        using var stream = typeof(RuleCatalog).Assembly.GetManifestResourceStream("layout-rules.md");
        if (stream is null) yield break;
        using var reader = new StreamReader(stream);

        string family = "", section = "", id = "", evidence = "";
        var body = new StringBuilder();

        RuleDoc Finished() => new(
            id, family, $"docs/generator/rules.md — {section}",
            Regex.Replace(body.ToString(), @"\s+", " ").Trim(),
            Evidence: evidence.Length == 0 ? null : evidence);

        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("## "))
            {
                if (Raised.Contains(id)) yield return Finished();
                id = "";
                // "## SP — Spawn" and "## CT — The mid interface … [expert]": the letters before the dash are
                // the family, the rest names the section a reader is being sent to.
                var heading = line[3..].Split('—', 2);
                family = heading[0].Trim();
                section = heading.Length > 1 ? EvidenceTag.Replace(heading[1], "").Trim() : family;
                if (family.Length > 3) { family = ""; section = ""; }   // "Definitions", "Correction protocol"
                continue;
            }
            if (family.Length == 0) continue;

            if (LayoutRule.Match(line) is { Success: true } start)
            {
                if (Raised.Contains(id)) yield return Finished();
                id = start.Groups["id"].Value;
                body.Clear();
                body.Append(Opening(line[start.Length..], out evidence));
                continue;
            }
            if (id.Length > 0) body.Append(' ').Append(line.Trim());
        }
        if (Raised.Contains(id)) yield return Finished();
    }

    /// <summary>What a rule's first line says once its own id line is taken off it: the bold run closes after
    /// the id and its evidence tag, and the tag becomes <see cref="RuleDoc.Evidence"/> rather than staying in
    /// the sentence. A rule that titles itself — <c>- **CT1 — the mid is an interface, not a cut.** There is
    /// no…</c> — keeps its title, because that title is the rule.</summary>
    private static string Opening(string rest, out string evidence)
    {
        evidence = EvidenceTag.Match(rest) is { Success: true } tag ? tag.Groups[1].Value : "";
        var close = rest.IndexOf("**", StringComparison.Ordinal);
        if (close >= 0) rest = rest.Remove(close, 2);
        rest = EvidenceTag.Replace(rest, "", 1);
        return rest.TrimStart().TrimStart('—').TrimStart();
    }
}
