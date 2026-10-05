using PgmStudio.Vocabulary;

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
/// the only place that knows one: the document editors behind the entity write routes raise <c>RQ1</c>, <c>RQ4</c> and
/// <c>RQ5</c> from inside <c>Pgm.Editing</c>. A rule id has one home — a second <c>const</c> aliasing one that
/// exists is two rules, which <c>docs/refusals.md</c> names under <i>Adding one</i> — so it lives in the
/// lowest project every caller can already reach.</para>
/// </summary>
public static class RequestRules
{
    /// <summary>A request's body or parameter is not in the form the route reads.</summary>
    /// <remarks>Send the request again with the field the finding names set to a value of the form the route
    /// reads.</remarks>
    [Rule(RuleCategory.Malformed, RuleConcern.Request)]
    public const string Unreadable = "RQ1";

    /// <summary>The studio failed to answer a request.</summary>
    /// <remarks>Report the error with the <c>error</c> and <c>message</c> of the response.</remarks>
    [Rule(RuleCategory.Internal, RuleConcern.Studio)]
    public const string Unhandled = "RQ2";

    /// <summary>A request's document names a field that the document of its tool does not have.</summary>
    /// <remarks>Change the field the finding names to a field the document of the tool has.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Request)]
    public const string Unread = "RQ3";

    /// <summary>The path of a request names a map, a library entry or a document that the studio does not
    /// have.</summary>
    /// <remarks>Either send the request again with the <c>slug</c> or <c>id</c> in its path set to one the studio
    /// has, or send the request that writes the document first, then send the request again.</remarks>
    [Rule(RuleCategory.Unknown, RuleConcern.Request)]
    public const string NoSuchSubject = "RQ4";

    /// <summary>A request expects a map or library entry to be in a state, and the studio holds it in
    /// another.</summary>
    /// <remarks>Either send the request again with another <c>name</c> or <c>slug</c>, or send it with the
    /// <c>If-Match</c> the finding names, or change the entries the finding names until nothing binds the
    /// entry.</remarks>
    [Rule(RuleCategory.Conflict, RuleConcern.Request)]
    public const string Conflict = "RQ5";

    /// <summary>The studio failed to read a document it stored.</summary>
    /// <remarks>Send the request that writes the document again, then report the error with the <c>error</c> and
    /// <c>message</c> of the response if the request is refused again.</remarks>
    [Rule(RuleCategory.Internal, RuleConcern.Request, RuleConcern.Studio)]
    public const string StoredUnreadable = "RQ6";

    /// <summary>A request signed in as nobody on the whitelist may not write, build a view or read a
    /// note.</summary>
    /// <remarks>Either send the request again signed in as a person on the whitelist, or ask an admin to invite the
    /// person.</remarks>
    [Rule(RuleCategory.Forbidden, RuleConcern.Request)]
    public const string SignedOut = "RQ7";

    /// <summary>The person a request is signed in as may not do what the request asks.</summary>
    /// <remarks>Either ask the map's owner to credit the person as an author, or ask an admin to send the
    /// request.</remarks>
    [Rule(RuleCategory.Forbidden, RuleConcern.Request)]
    public const string NotPermitted = "RQ8";

    /// <summary>The studio has no Discord application.</summary>
    /// <remarks>Ask the person who runs the studio to set <c>Discord:ClientId</c> and
    /// <c>Discord:ClientSecret</c>.</remarks>
    [Rule(RuleCategory.Unavailable, RuleConcern.Studio)]
    public const string SignInUnavailable = "RQ9";

    /// <summary>The studio has no Minecraft block textures.</summary>
    /// <remarks>Either ask the person who runs the studio to set <c>Textures:Jar</c> to a 1.8.9 client jar, or ask
    /// them to set <c>Textures:AcceptMojangEula</c> to <c>true</c>.</remarks>
    [Rule(RuleCategory.Unavailable, RuleConcern.Studio)]
    public const string TexturesUnavailable = "RQ10";

    /// <summary>The build queue did not answer a request with a turn within 60 seconds.</summary>
    /// <remarks>Wait 10 seconds, then send the request again.</remarks>
    [Rule(RuleCategory.Unavailable, RuleConcern.Request, RuleConcern.Studio)]
    public const string Busy = "RQ11";

    /// <summary>The studio has no agent to hand notes to, or the service of the agent did not take them.</summary>
    /// <remarks>Ask the person who runs the studio to set <c>Notes:Agent:Fire</c> and
    /// <c>Notes:Agent:Token</c>.</remarks>
    [Rule(RuleCategory.Unavailable, RuleConcern.Studio)]
    public const string AgentUnavailable = "RQ12";
}
