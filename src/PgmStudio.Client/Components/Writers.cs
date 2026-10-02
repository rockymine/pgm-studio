namespace PgmStudio.Client.Components;

/// <summary>Who wrote something the studio keeps — a change to a board, a message in a note's thread — in the
/// words every column names them in. A token writes as an agent and is named by its label; the person it acts
/// for is who issued it, and is said only beside it.</summary>
public static class Writers
{
    /// <summary>The name a writer goes by: the token's label where a token wrote it, else the person.</summary>
    public static string Name(string? person, string? token) => token ?? person ?? "unsigned";

    /// <summary>The whole of who wrote it, for a hover: the agent and whose token it wrote with, or the person.</summary>
    public static string Describe(string? person, string? token) =>
        token is null ? person ?? "unsigned"
        : $"{token} — an agent, writing with {(person is null ? "an unsigned token" : $"{person}'s token")}";
}
