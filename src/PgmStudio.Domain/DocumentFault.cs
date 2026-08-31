using System.Text.Json;

namespace PgmStudio.Domain;

/// <summary>
/// A posted document that will not parse, naming the field it broke on.
///
/// <para>A reader cannot answer <see cref="Vocabulary.Findings"/> — it stops at the first thing that makes producing a
/// value impossible rather than collecting everything wrong — so it throws, and <c>docs/refusals.md</c> says
/// the exception carries its finding so the gate above answers in the one shape anyway. That held for a
/// dressing document (<c>DR-DOC</c>) and nowhere else: every other reader threw a bare
/// <see cref="JsonException"/>, so a refusal arrived as a sentence with no field on it and a canvas had
/// nothing to highlight.</para>
///
/// <para>It derives from <see cref="JsonException"/> deliberately. Thirteen call sites already catch that,
/// and a new type they did not know about would have walked straight past them into the unhandled backstop —
/// turning a 400 an author can read into a 500 that says the studio is at fault.</para>
/// </summary>
public sealed class DocumentFault(string field, string message) : JsonException(message)
{
    /// <summary>The document field the fault is about, in the document's own words — dotted where it is
    /// nested, so <c>roof.gableWindows</c> rather than a <c>gableWindows</c> an author cannot find.</summary>
    public string Field { get; } = field;
}
