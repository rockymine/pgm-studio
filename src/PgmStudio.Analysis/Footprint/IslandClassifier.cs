namespace PgmStudio.Analysis.Footprint;

/// <summary>
/// Coarse gameplay role of a detected island, by size relative to the largest landmass:
/// <list type="bullet">
/// <item><see cref="Major"/> — a large landmass (a team island that holds spawn + wools, or a
/// gameplay-defining central mass).</item>
/// <item><see cref="Neutral"/> — a gameplay-sized contested-middle island (a stepping-stone / mid piece).</item>
/// <item><see cref="Small"/> — below gameplay size: decorative specks or detection fragments.</item>
/// </list>
/// </summary>
public enum IslandRole { Major, Neutral, Small }

/// <summary>
/// Size-bucketing of detected islands into gameplay roles, separating the large team islands (which hold
/// spawn/wools and need dissection into lanes) from the neutral contested-middle pieces (stepping-stones /
/// mid) and sub-gameplay specks. Thresholds are corpus-derived: gameplay-sized neutrals run ~64–1023 blocks
/// and only ~13% exceed a quarter of a team island, so a landmass at least <see cref="MajorFraction"/> of the
/// largest reads as a major island, one at least <see cref="MinGameplaySize"/> blocks but smaller is a
/// neutral, and anything below that is a speck.
/// </summary>
public static class IslandClassifier
{
    public const int MinGameplaySize = 64;
    public const double MajorFraction = 0.25;

    /// <summary>Role of one island given the largest island's block count.</summary>
    public static IslandRole Classify(int blockCount, int largestBlockCount,
        int minGameplaySize = MinGameplaySize, double majorFraction = MajorFraction)
    {
        if (largestBlockCount <= 0) return IslandRole.Small;
        if (blockCount >= majorFraction * largestBlockCount) return IslandRole.Major;
        return blockCount >= minGameplaySize ? IslandRole.Neutral : IslandRole.Small;
    }

    /// <summary>Roles for every island (ordered as given). Uses the max block count as the reference.</summary>
    public static IReadOnlyList<IslandRole> Classify(IReadOnlyList<IslandDetector.Island> islands,
        int minGameplaySize = MinGameplaySize, double majorFraction = MajorFraction)
    {
        if (islands.Count == 0) return [];
        var largest = islands.Max(i => i.BlockCount);
        return islands.Select(i => Classify(i.BlockCount, largest, minGameplaySize, majorFraction)).ToList();
    }

    /// <summary>Count of <see cref="IslandRole.Major"/> islands.</summary>
    public static int MajorCount(IReadOnlyList<IslandDetector.Island> islands) =>
        Classify(islands).Count(r => r == IslandRole.Major);

    /// <summary>
    /// Heuristic that a map's island detection looks <b>under-split</b> (teams merged into one landmass):
    /// a symmetric N-team map should resolve into N comparable major islands, so fewer majors than teams
    /// means a team boundary was bridged (e.g. a block-36 floor read as one mass). Returns false for the
    /// degenerate single-team / no-team case where there is nothing to split.
    /// </summary>
    public static bool LooksUnderSplit(int majorCount, int teamCount) =>
        teamCount >= 2 && majorCount < teamCount;
}
