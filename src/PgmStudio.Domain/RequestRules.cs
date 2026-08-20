
namespace PgmStudio.Domain;

/// <summary>
/// The rules a request itself fires, as opposed to a gate reading a document it understood.
///
/// <para>Every other family in <c>docs/refusals.md</c> is owned by the gate that asks it. These are owned by
/// the request, not by the map: a document that could not be read at all, a subject the route names and the
/// studio does not have, a request that conflicts with what is stored, a stored document the studio cannot
/// read back, and a fault that is the studio's own.</para>
///
/// <para>They sit beside <see cref="Finding"/> rather than at the API boundary, because the boundary is not
/// the only place that knows one: the document editors behind the Edit tool raise <c>RQ1</c>, <c>RQ4</c> and
/// <c>RQ5</c> from inside <c>Pgm.Editing</c>. A rule id has one home — a second <c>const</c> aliasing one that
/// exists is two rules, which <c>docs/refusals.md</c> names under <i>Adding one</i> — so it lives in the
/// lowest project every caller can already reach.</para>
/// </summary>
public static class RequestRules
{
    /// <summary>A posted document could not be read — absent, empty, malformed, or naming a kind that does not
    /// exist. The caller's to fix, so it answers 400.</summary>
    /// <remarks>Post a body. If one was posted, the finding's <c>field</c> names where the reader stopped — usually a <c>kind</c> that is not one of the names, or a property stated as <c>null</c> where the record cannot hold one.</remarks>
    public const string Unreadable = "RQ1";

    /// <summary><b>Not the caller's fault.</b> Something escaped an endpoint that no gate refused, which is a
    /// defect in the studio; it answers 500 and stays a 500, because dressing a bug as a bad request sends the
    /// author looking for a mistake they did not make. What it buys is that the caller gets the one envelope
    /// every other refusal arrives in rather than a .NET stack trace, and the trace goes to the log where it
    /// belongs.</summary>
    /// <remarks>Nothing an author can do: this is a defect in the studio, and seeing one is a bug report rather than an authoring problem. The stack trace is in the server log.</remarks>
    public const string Unhandled = "RQ2";

    /// <summary><b>A complaint, never a refusal.</b> The document carried a property the reader had nowhere to
    /// keep — a typo, or a field from a document this is not. It rides on the success response under
    /// <c>warnings</c> because the work did succeed and the rest of the document was read; refusing it would
    /// refuse every snapshot written before the last shape change, since a stored document legitimately holds
    /// retired names an upgrade carries forward. See <see cref="Domain.DocumentShape"/>.</summary>
    /// <remarks>Check the spelling of the field the finding names against the document shape the tool's document names. The work succeeded without it, so whatever that field was meant to say was not said.</remarks>
    public const string Unread = "RQ3";

    /// <summary>The route names a subject the studio does not have — a slug no map is stored under, an id no
    /// library row carries, an artifact a map has not produced yet. It answers <b>404</b> with a body, because
    /// an empty one cannot say whether the identifier was wrong or the route was, and only one of those is
    /// something the caller can correct.</summary>
    /// <remarks>Check the identifier in the path against what the studio holds — <c>GET /api/maps</c> lists the maps, and each library has its own list route. Where the subject is an artifact rather than a row, the stage that produces it has not been run for this map.</remarks>
    public const string NoSuchSubject = "RQ4";

    /// <summary>The request is well-formed and conflicts with what is stored: a name already taken, or a row
    /// something still binds. It answers <b>409</b>, and the finding names what is in the way — the maps or
    /// styles still referencing it, so the caller can act rather than guess.</summary>
    /// <remarks>Nothing is wrong with the request itself. Either choose another name, or release what still holds the thing being removed — the finding's subjects name them.</remarks>
    public const string Conflict = "RQ5";

    /// <summary>A document the <b>studio</b> stored will not read back — a plan row, an artifact, a snapshot
    /// written under a shape no upgrade claims. It answers <b>422</b> rather than 500 because it is data
    /// rather than a defect and the author clears it by writing the document again; and deliberately not
    /// <c>RQ1</c>, which would blame the request that merely asked to read it and send the author looking at
    /// what they just posted.</summary>
    /// <remarks>The stored copy is the problem, not what was just posted: open the tool that writes it and save it again, which replaces the unreadable copy with one in the current shape.</remarks>
    public const string StoredUnreadable = "RQ6";
}
