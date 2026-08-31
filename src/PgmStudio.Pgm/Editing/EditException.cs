using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Editing;

/// <summary>
/// An edit the document will not take, carrying the <see cref="Vocabulary.Finding"/> the endpoint answers with.
///
/// <para>An edit is refused for eight reasons: six the request owns (<see cref="RequestRules"/>) and two the
/// document does (<see cref="EditRules"/>). What travels is the finding and the status that states it, so
/// <c>WriteSupport.RunEditAsync</c> answers the studio's one refusal envelope with no knowledge of which
/// editor raised it.</para>
///
/// <para><see cref="Error"/> is the gate's short label, for a client with something to show before it has
/// read a finding. It names the fault family rather than the endpoint, because one editor stands behind all
/// thirty-six write routes and naming the route says nothing a caller does not already know.</para>
/// </summary>
public sealed class EditException(int status, string error, Finding finding) : Exception(finding.Message)
{
    /// <summary>The HTTP status the fault answers at — a fact about the request rather than about the
    /// fault, which is why it is stated at the throw site and not derived from the rule.</summary>
    public int Status { get; } = status;

    /// <summary>The refusal envelope's <c>error</c>: the gate's short label, never the fault itself.</summary>
    public string Error { get; } = error;

    /// <summary>What is wrong, in the one shape every gate in the studio answers in.</summary>
    public Finding Finding { get; } = finding;

    /// <summary>The request could not be acted on: a required field absent, a value outside a closed set, a
    /// number that is not one. <c>RQ1</c> at 400, with <paramref name="field"/> naming the parameter where it
    /// has a name, so a client can put the sentence beside the input rather than at the top of the page.</summary>
    public static EditException Unreadable(string message, string? field = null) =>
        new(400, "invalid edit", new Finding(RequestRules.Unreadable, message, Field: field));

    /// <summary>The edit names a subject the document does not have — a region, a team, a wool, a monument, an
    /// apply-rule, a filter, a spawn. <c>RQ4</c> at 404, with <paramref name="missing"/> naming the ids where
    /// one edit can name several, so a caller is not left splitting them back out of the sentence.</summary>
    public static EditException NoSuchSubject(string message, IReadOnlyList<string>? missing = null) =>
        new(404, "no such subject", new Finding(RequestRules.NoSuchSubject, message, Subjects: missing));

    /// <summary>The edit is well-formed and conflicts with what the document already holds — an id in use, a
    /// row something still references. <c>RQ5</c> at 409, with <paramref name="holding"/> naming what is in
    /// the way so a caller can act rather than guess.</summary>
    public static EditException Conflict(string message, IReadOnlyList<string>? holding = null) =>
        new(409, "conflicting edit", new Finding(RequestRules.Conflict, message, Subjects: holding));

    /// <summary>A reference the document cannot resolve. <c>ED1</c> at 400.</summary>
    public static EditException Unresolved(string message, string? field = null) =>
        new(400, "unresolved reference", new Finding(EditRules.UnresolvedReference, message, Field: field));

    /// <summary>An edit the document is not in a state to take. <c>ED2</c> at 400.</summary>
    public static EditException Inapplicable(string message) =>
        new(400, "edit not applicable", new Finding(EditRules.Inapplicable, message));
}
