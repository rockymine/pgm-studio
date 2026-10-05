using System.Text.Json;
using PgmStudio.Vocabulary;

namespace PgmStudio.Domain;

/// <summary>
/// A posted document that will not parse, naming the field it broke on, with a message that is a predicate
/// about that field ("is stated as null"); <see cref="JsonFaults"/> puts the two together for a finding.
///
/// <para>A reader stops at the first thing that makes producing a value impossible rather than collecting
/// everything wrong, so it throws rather than answering findings. It derives from <see cref="JsonException"/>
/// so every caller that catches a reader's fault catches this one too.</para>
/// </summary>
public sealed class DocumentFault(string field, string message, DocumentEdit? edit = null) : JsonException(message)
{
    /// <summary>The document field the fault is about, in the document's own words — dotted where it is
    /// nested, so <c>roof.gableWindows</c> rather than a <c>gableWindows</c> an author cannot find.</summary>
    public string Field { get; } = field;

    /// <summary>The change that makes the field read, where the reader knows one.</summary>
    public DocumentEdit? Edit { get; } = edit;
}
