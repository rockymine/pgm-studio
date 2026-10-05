using System.Text.Json;
using System.Text.RegularExpressions;
using PgmStudio.Vocabulary;

namespace PgmStudio.Domain;

/// <summary>
/// How a document reader's fault is said in a finding: the field it stopped at, as the document spells it, and
/// a message in the shape every finding takes.
///
/// <para>A reader's own fault is a <see cref="DocumentFault"/> or a <see cref="JsonException"/> whose message is
/// a predicate about the field it stopped at ("is not a number", "is empty"), and the message puts that field,
/// or the document, in front of it. System.Text.Json's own faults carry its sentence and its position instead,
/// so they are read for what they are — a value of the wrong type at a field, a kind it does not know, or a
/// body that is not JSON at all — and said that way. Any other exception is the request's own sentence and is
/// said as it is.</para>
/// </summary>
public static partial class JsonFaults
{
    /// <summary>The field <paramref name="fault"/> stopped at, as the document spells it — <c>roof.body</c>,
    /// <c>rects[0]</c> — or null where it is the whole document or no field at all.</summary>
    public static string? Field(Exception fault) => Read(fault, null).Field;

    /// <summary>The <paramref name="rule"/> finding for <paramref name="fault"/>: its field the one the fault
    /// stopped at, under <paramref name="member"/> where the document is one member of the body; its message
    /// that field, or <paramref name="document"/> where it names none, then what is wrong there; and the edit
    /// that makes the field read, where the reader knows one.</summary>
    public static Finding Said(string rule, Exception fault, string? member = null, string document = "the request's body")
    {
        var (path, text, predicate) = Read(fault, null);
        var field = Under(member, path);
        var message = !predicate ? text : field is null ? $"{document} {text}" : $"`{field}` {text}";
        var edit = (fault as DocumentFault)?.Edit is { } change ? change with { Path = Under(member, change.Path)! } : null;
        return new Finding(rule, message, Field: field, Edit: edit);
    }

    private static string? Under(string? member, string? path) => (member, path) switch
    {
        (null or "", _) => path,
        (_, null) => member,
        _ => $"{member}.{path}",
    };

    /// <summary>The field <paramref name="fault"/> stopped at and what is wrong there, as a predicate; a fault
    /// naming a kind outside <paramref name="kinds"/> lists them. <c>Predicate</c> is false where the fault is a
    /// whole sentence of its own rather than something said about a field.</summary>
    public static (string? Field, string Text, bool Predicate) Read(Exception fault, IReadOnlyCollection<string>? kinds)
    {
        switch (fault)
        {
            case DocumentFault document:
                return (document.Field.Length == 0 ? null : document.Field, document.Message, true);
            case JsonException json when !json.Message.Contains(" Path: ", StringComparison.Ordinal)
                                         && !json.Message.Contains("LineNumber", StringComparison.Ordinal):
                return (Relative(json.Path), json.Message, true);
            case JsonException json:
            {
                var field = Relative(json.Path);
                if (UnknownKind().Match(json.Message) is { Success: true } unknown)
                    return (Kind(field), kinds is { Count: > 0 }
                        ? $"is '{unknown.Groups[1].Value}', which is not one of {string.Join(", ", kinds)}"
                        : $"is '{unknown.Groups[1].Value}', which does not exist", true);
                if (json.Message.Contains("must specify a type discriminator", StringComparison.Ordinal))
                    return (Kind(field), "is not stated", true);
                if (ConvertedTo().Match(json.Message) is { Success: true } converted)
                    return (field, field is null ? "is not the document the route reads"
                        : NotA(Wanted().Match(json.InnerException?.Message ?? "") is { Success: true } wanted
                            ? wanted.Groups[1].Value : converted.Groups[1].Value), true);
                return (null, $"is not JSON at line {(json.LineNumber ?? 0) + 1}, "
                    + $"position {(json.BytePositionInLine ?? 0) + 1}", true);
            }
            case NotSupportedException unsupported:
                return (Kind(Relative(Positioned().Match(unsupported.Message) is { Success: true } at ? at.Groups[1].Value : null)),
                    "is not stated, or is not a kind that exists", true);
            default:
                return (null, fault.Message, false);
        }
    }

    /// <summary>System.Text.Json's path as the document's own: <c>$.modes[0]</c> is <c>modes[0]</c>.</summary>
    private static string? Relative(string? path)
    {
        if (string.IsNullOrEmpty(path) || path == "$") return null;
        var trimmed = path.TrimStart('$').TrimStart('.').TrimEnd('.');
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>The <c>kind</c> of the object at <paramref name="field"/>, which is the field a kind fault is
    /// about whether the reader stopped at the object or at the discriminator itself. A fault the polymorphic
    /// reader raises is always about a kind.</summary>
    private static string Kind(string? field) =>
        field is null ? "kind" : field == "kind" || field.EndsWith(".kind", StringComparison.Ordinal) ? field : $"{field}.kind";

    /// <summary>A value that is not the type its field takes, said in the document's words. The type is
    /// the reader's own word for it where the codec's inner fault names one, which it does where the record
    /// is read through its constructor and the outer fault names the record instead.</summary>
    private static string NotA(string type) => (type.StartsWith("System.Nullable`1[", StringComparison.Ordinal)
        ? type["System.Nullable`1[".Length..^1] : type).Replace("System.", "") switch
    {
        "Double" or "Single" or "Decimal" or "number" => "is not a number",
        "Int32" or "Int64" or "Int16" or "Byte" or "UInt32" or "UInt64" => "is not a whole number",
        "Boolean" or "boolean" => "is not true or false",
        "String" or "string" => "is not text",
        var other when other.EndsWith("[]", StringComparison.Ordinal) || other.StartsWith("Collections", StringComparison.Ordinal)
            => "is not a list",
        _ => "is not a value its field takes",
    };

    [GeneratedRegex(@"(?:for an?|as an?) (\w+)\.$")]
    private static partial Regex Wanted();

    [GeneratedRegex(@"could not be converted to ([^\s]+?)\.?(?:\s+Path:|$)")]
    private static partial Regex ConvertedTo();

    [GeneratedRegex(@"Path:\s*(\S+)")]
    private static partial Regex Positioned();

    [GeneratedRegex("unrecognized type discriminator id '([^']*)'")]
    private static partial Regex UnknownKind();
}
