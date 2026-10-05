using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PgmStudio.Vocabulary;

namespace PgmStudio.Domain;

/// <summary> One rule, as a reader meets it. <see cref="Rule"/> is the stable id a finding carries and a client
/// keys on; <see cref="Family"/> groups it by what it is about rather than by which gate asks; <see
/// cref="Owner"/> is the constant that declares it. <see cref="Means"/> and <see cref="Fix"/> are the rule's own
/// two texts: what is wrong, and what to do about it.
/// <para><b>Category</b> — What a caller does about a finding citing this rule, from the <c>[Rule]</c> attribute
/// beside the constant. Null for a rule nothing raises.</para>
/// <para><b>Concerns</b> — What the rule is about — one word or several, since a rule concerns a combination a
/// family prefix cannot carry.</para></summary>
public sealed record RuleDoc(
    string Rule, string Family, string Owner, string Means, string? Fix = null,
    RuleCategory? Category = null, IReadOnlyList<RuleConcern>? Concerns = null)
{
    /// <summary>What the rule is about, never null.</summary>
    public IReadOnlyList<RuleConcern> About => Concerns ?? [];
}

/// <summary>
/// <b>Every rule the studio can cite, in one list, read from one place: the constants that declare them.</b>
/// A rule's meaning is the <c>&lt;summary&gt;</c> of the docstring beside its own <c>const</c> and its fix is
/// the <c>&lt;remarks&gt;</c>, read through the XML documentation file the compiler emits, so the text a
/// caller is shown is the text in the source. Its category and concerns are the <see cref="RuleAttribute"/>
/// beside it. The layout rules are <see cref="LayoutRules"/>; every other family is declared beside the gate
/// that raises it.
/// </summary>
public static class RuleCatalog
{
    /// <summary>A rule id: one to three letters, then a number or a single-letter suffix (<c>PC-C</c>). A
    /// one-letter id (<c>G2</c>) is read only off a constant carrying <see cref="RuleAttribute"/>, since a
    /// short string constant is otherwise too common to mean a rule.</summary>
    private static readonly Regex IdShape = new(@"^[A-Z]{1,3}(?:-[A-Z]+|[0-9]+)$", RegexOptions.Compiled);

    /// <summary>Every rule the given assemblies declare, ordered by family then by the number inside the id — so
    /// <c>PL2</c> precedes <c>PL10</c>, which sorting the strings would not.
    /// <para><b>assemblies</b> — The assemblies to read rules out of. Named by the caller rather than
    /// discovered, because an assembly nothing has touched yet is not loaded and would be silently
    /// missing.</para></summary>
    public static IReadOnlyList<RuleDoc> Read(IEnumerable<Assembly> assemblies)
    {
        var rules = assemblies.SelectMany(Declared).ToList();
        return [.. rules.OrderBy(rule => rule.Family, StringComparer.Ordinal).ThenBy(Ordinal).ThenBy(rule => rule.Rule, StringComparer.Ordinal)];
    }

    /// <summary>The number inside an id, for ordering. A suffixed id (<c>PC-C</c>) has none and sorts first.</summary>
    private static int Ordinal(RuleDoc rule) =>
        int.TryParse(new string([.. rule.Rule.Where(char.IsDigit)]), out var number) ? number : 0;

    /// <summary>The family an id belongs to: its letters. Grouping by what a rule is about is what keeps the
    /// same objective rule one rule when the compile gate and the export gate both ask it.</summary>
    private static string FamilyOf(string rule) => new([.. rule.TakeWhile(char.IsAsciiLetterUpper)]);

    // ── the constants, and their own docstrings ──────────────────────────────────────────────────────────

    /// <summary>Every <c>const string</c> in the assembly whose value is shaped like a rule id, with the
    /// summary and remarks the compiler wrote out beside it. Private and nested declarations are read too —
    /// <c>ExportRules</c> is private inside <c>MapExportComposer</c>, and a rule that is not listed because of
    /// where it happens to be declared is the failure this exists to prevent.</summary>
    private static IEnumerable<RuleDoc> Declared(Assembly assembly)
    {
        var docs = Documentation(assembly);
        foreach (var type in Types(assembly))
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (!field.IsLiteral || field.FieldType != typeof(string)) continue;
                if (field.GetRawConstantValue() is not string id || !IdShape.IsMatch(id)) continue;
                var classification = field.GetCustomAttribute<RuleAttribute>();
                if (classification is null && FamilyOf(id).Length < 2) continue;

                var (summary, remarks) = docs.GetValueOrDefault($"F:{type.FullName!.Replace('+', '.')}.{field.Name}");
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
                    member => (Prose(member.Element("summary")), Prose(member.Element("remarks"), fields: true)));
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
        {
            return [];
        }
    }

    /// <summary>An XML doc element as one line of prose: its text with the markup flattened, a
    /// <c>&lt;see cref&gt;</c> reduced to the name it points at, and every run of whitespace collapsed — the
    /// docstrings are hard-wrapped in the source and would otherwise arrive with the wrapping in them. With
    /// <paramref name="fields"/>, a <c>&lt;c&gt;</c> is a document field and keeps its backticks, which a fix
    /// alone may carry.</summary>
    private static string? Prose(XElement? element, bool fields = false)
    {
        if (element is null) return null;
        var text = new StringBuilder();
        foreach (var node in element.DescendantNodes())
        {
            if (node is XText plain)
                text.Append(fields && plain.Parent is { Name.LocalName: "c" } ? $"`{plain.Value}`" : plain.Value);
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
}
