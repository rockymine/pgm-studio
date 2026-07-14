using PgmStudio.Pgm.Evaluate;

namespace PgmStudio.Pgm.Compose;

/// <summary>One rejected compose attempt, reproducible from its seed: the request, the attempt index, the stage
/// that rejected it, and the term/rule that fired with the pieces it indicted. Re-composing with the same seed
/// reproduces the failed layout exactly, and the accumulated log doubles as a frequency report of which rule
/// kills the most attempts.</summary>
public sealed record RejectRecord(
    ulong Seed, int Players, int Teams, string Symmetry, int Attempt, string Stage,
    string TermId, string RuleId, IReadOnlyList<string> Subjects);

/// <summary>An optional sink the composer appends a <see cref="RejectRecord"/> to whenever the acceptance gate
/// rejects an attempt. Null by default (the pure compose path emits nothing); a tool passes a JSONL writer to
/// capture the reject stream.</summary>
public interface IComposeRejectSink
{
    void Reject(RejectRecord record);
}
