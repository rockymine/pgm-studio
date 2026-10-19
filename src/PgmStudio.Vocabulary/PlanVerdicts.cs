namespace PgmStudio.Vocabulary;

/// <summary>
/// Where a map's places stand with its author: drawn and waiting to be read, sent back with changes asked for,
/// or approved to build on. The places document states one, the Places step sets it, and an agent reads it before
/// it builds; nothing the studio stores is refused for it.
/// </summary>
public static class PlanVerdicts
{
    /// <summary>Drawn, and waiting for the author to read it.</summary>
    public const string Proposed = "proposed";

    /// <summary>Read, and sent back with changes asked for in its notes.</summary>
    public const string Changes = "changes";

    /// <summary>Approved: the board may be built on it.</summary>
    public const string Approved = "approved";

    /// <summary>Every verdict, in the order a plan meets them.</summary>
    public static readonly string[] All = [Proposed, Changes, Approved];
}
