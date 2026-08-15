namespace PgmStudio.Contracts;

/// <summary>
/// One thing wrong with something an author wrote, as it crosses the API — the wire mirror of the studio's
/// <c>Finding</c>, and the only finding shape a client has to know.
///
/// <para><b>Every gate answers in this shape.</b> A plan refused for its structure, a house style refused for
/// its blocks, a building refused for how its wings meet, a map refused at export for its gamemode: all of
/// them land here, so a panel rendering a refusal never has to know which gate produced it before it can read
/// one. <see cref="Rule"/> is the stable id a panel styles off and an agent branches on; <see cref="Message"/>
/// carries the measured numbers; <see cref="Severity"/> is <c>refusal</c> or <c>complaint</c>, and a complaint
/// rides along with work that succeeded.</para>
///
/// <para><see cref="Field"/> names the document field at fault where one is nameable, and
/// <see cref="Subjects"/> the ids the fault indicts — pieces, zones, props, wings — which the canvas
/// highlights on click. <see cref="Cites"/> is what to look up next where the rule id is not it: the layout
/// rule the fault falls under, or the open task that would resolve it. Any of the three may be absent,
/// because different gates know different things about what they are refusing.</para>
/// </summary>
public sealed record FindingDto(
    string Rule,
    string Message,
    string Severity = "refusal",
    string? Field = null,
    IReadOnlyList<string>? Subjects = null,
    string? Cites = null)
{
    /// <summary>The implicated ids, never null.</summary>
    public IReadOnlyList<string> SubjectIds => Subjects ?? [];

    /// <summary>Whether this one stopped the work.</summary>
    public bool Refuses => Severity == "refusal";
}

/// <summary>
/// A refusal, as every gate in the studio answers it: which gate said no, one line for a caller that wants a
/// sentence, and the findings themselves for one that wants to act.
///
/// <para><see cref="Error"/> is a short label naming the gate — <c>"objective placement"</c>, <c>"invalid house
/// style"</c> — and never the fault itself, which is what the findings are for. It exists so a client can show
/// something useful before it has looked at a single finding.</para>
/// </summary>
public sealed record RefusalDto(string Error, string Message, IReadOnlyList<FindingDto> Findings);
