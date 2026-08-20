
using PgmStudio.Vocabulary;

namespace PgmStudio.Contracts;

/// <summary>
/// A refusal, as every gate in the studio answers it: which gate said no, one line for a caller that wants a
/// sentence, and the findings themselves for one that wants to act.
///
/// <para><see cref="Error"/> is a short label naming the gate — <c>"objective placement"</c>, <c>"invalid house
/// style"</c> — and never the fault itself, which is what the findings are for. It exists so a client can show
/// something useful before it has looked at a single finding.</para>
///
/// <para>The findings are <see cref="Finding"/> itself, the shape the gate raised them in. A gate below
/// <c>Api</c> hands its findings up and this layer renders the envelope, so what a caller reads never depends
/// on how deep the refusal was raised.</para>
/// </summary>
public sealed record RefusalDto(string Error, string Message, IReadOnlyList<Finding> Findings);
