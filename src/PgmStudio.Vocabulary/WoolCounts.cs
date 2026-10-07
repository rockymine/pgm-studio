namespace PgmStudio.Vocabulary;

/// <summary>
/// How many wool rooms one team's unit carries. The composer draws a count inside this range, and the
/// Generator page offers one chip per count, so both read the same bounds.
/// </summary>
public static class WoolCounts
{
    /// <summary>The fewest wools a unit carries.</summary>
    public const int Min = 1;

    /// <summary>The most wools a unit carries.</summary>
    public const int Max = 3;

    /// <summary>Every count, fewest first.</summary>
    public static readonly int[] All = [.. Enumerable.Range(Min, Max - Min + 1)];
}
