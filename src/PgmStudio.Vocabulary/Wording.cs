namespace PgmStudio.Vocabulary;

/// <summary>The two pieces of a finding's message every check writes the same way: a count with its noun, and a
/// list of ids.</summary>
public static class Wording
{
    /// <summary>A count and its noun: "1 block", "3 blocks"; <paramref name="plural"/> where the noun does not
    /// take an s.</summary>
    public static string Count(int count, string singular, string? plural = null) =>
        count == 1 ? $"1 {singular}" : $"{count} {plural ?? singular + "s"}";

    /// <summary>Ids as a message names them: "'a'", "'a' and 'b'", "'a', 'b' and 'c'".</summary>
    public static string Ids(IEnumerable<string> ids)
    {
        var quoted = ids.Select(id => $"'{id}'").ToList();
        return quoted.Count <= 1 ? string.Concat(quoted) : $"{string.Join(", ", quoted[..^1])} and {quoted[^1]}";
    }
}
