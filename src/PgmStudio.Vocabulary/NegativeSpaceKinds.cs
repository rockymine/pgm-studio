namespace PgmStudio.Vocabulary;

/// <summary>
/// How much a body encloses a connected patch of empty ground beside it, by how many of the four axis
/// directions wall it: a <see cref="Hole"/> is enclosed outright, a <see cref="Bay"/> is walled from three
/// and open toward one, a <see cref="Notch"/> from two, and <see cref="Open"/> from at most one — plain
/// outside space along a flat side, which is not a feature of the shape. The escalation
/// notch → bay → hole is the wall count 2 → 3 → 4.
///
/// <para>The same four words name the space a composed body publishes as a vacancy and the space an
/// authored plan leaves between two of its pieces, because it is one classification read off a cell set
/// either way. Three parties spell them — the shape emitter that offers a vacancy, the lint that measures
/// how narrow a space is before a goal stands beside it, and the wire that answers both.</para>
/// </summary>
public static class NegativeSpaceKinds
{
    /// <summary>Walled from at most one direction: outside space along a flat side, and no feature.</summary>
    public const string Open = "open";

    /// <summary>Walled from two directions — the corner an L wraps.</summary>
    public const string Notch = "notch";

    /// <summary>Walled from three, open toward one: a recess with a single mouth.</summary>
    public const string Bay = "bay";

    /// <summary>Enclosed outright — the ring's void, reached only by crossing something.</summary>
    public const string Hole = "hole";

    /// <summary>The four, in escalation order.</summary>
    public static readonly string[] All = [Open, Notch, Bay, Hole];

    /// <summary>The kind a space of this wall count is, given whether the body encloses it outright. An
    /// enclosed space is a hole whatever the count says, a wall being a wall in every direction at once.</summary>
    public static string Of(int wallDirections, bool enclosed) =>
        enclosed ? Hole : wallDirections >= 3 ? Bay : wallDirections == 2 ? Notch : Open;

    /// <summary>The kind's place in the escalation, so an ordering over kinds runs open → notch → bay →
    /// hole rather than alphabetically. A word nothing matches ranks with <see cref="Open"/>.</summary>
    public static int Rank(string? kind) => Math.Max(0, Array.IndexOf(All, Canonical(kind)));

    /// <summary>What a kind is, in one sentence — what a reader meets under the word.</summary>
    public static string Describe(string? kind) => Canonical(kind) switch
    {
        Notch => "Walled from two directions — the corner an L wraps",
        Bay => "Walled from three and open toward one: a recess with a single mouth",
        Hole => "Enclosed outright — the ring's void, reached only by crossing something",
        _ => "Walled from at most one direction: outside space along a flat side",
    };

    /// <summary>The word, or <see cref="Open"/> for one that is not a kind — the absence of a feature, and
    /// what a space nothing encloses is.</summary>
    public static string Canonical(string? kind) => All.Contains(kind) ? kind! : Open;
}
