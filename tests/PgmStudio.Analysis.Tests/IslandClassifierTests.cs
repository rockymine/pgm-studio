using PgmStudio.Analysis.Footprint;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// Size-based island role bucketing + the under-split heuristic. Reference sizes mirror the corpus:
/// two ~equal team islands, gameplay-sized neutral mids, and sub-gameplay specks/fragments.
/// </summary>
public sealed class IslandClassifierTests
{
    [Test]
    [Arguments(6230, 6230, IslandRole.Major)]   // a co-equal team island
    [Arguments(1545, 7152, IslandRole.Neutral)] // ~21% of the largest → contested-middle piece
    [Arguments(64, 7152, IslandRole.Neutral)]   // exactly at the gameplay floor → still a piece
    [Arguments(63, 7152, IslandRole.Small)]     // just below gameplay size → a speck
    [Arguments(37, 6386, IslandRole.Small)]     // detection fragment
    public async Task Classify_bucketsBySizeRelativeToLargest(int count, int largest, IslandRole expected)
    {
        await Assert.That(IslandClassifier.Classify(count, largest)).IsEqualTo(expected);
    }

    [Test]
    public async Task Classify_emptyLargest_isSmall()
    {
        await Assert.That(IslandClassifier.Classify(10, 0)).IsEqualTo(IslandRole.Small);
    }

    [Test]
    public async Task Classify_overList_twoTeamsPlusMidsAndSpecks()
    {
        // green_gem-shaped: two co-equal team islands + two gameplay neutrals.
        var islands = MakeIslands(7152, 7152, 1545, 1545, 40);
        var roles = IslandClassifier.Classify(islands);

        await Assert.That(roles.Count(r => r == IslandRole.Major)).IsEqualTo(2);
        await Assert.That(roles.Count(r => r == IslandRole.Neutral)).IsEqualTo(2);
        await Assert.That(roles.Count(r => r == IslandRole.Small)).IsEqualTo(1);
        await Assert.That(IslandClassifier.MajorCount(islands)).IsEqualTo(2);
    }

    [Test]
    public async Task LooksUnderSplit_oneMajorOnTwoTeamMap_isSuspect()
    {
        // abstract-shaped: one map-spanning island + two specks, on a 2-team map.
        var islands = MakeIslands(4937, 49, 49);
        await Assert.That(IslandClassifier.MajorCount(islands)).IsEqualTo(1);
        await Assert.That(IslandClassifier.LooksUnderSplit(1, teamCount: 2)).IsTrue();
    }

    [Test]
    public async Task LooksUnderSplit_twoMajorsOnTwoTeamMap_isHealthy()
    {
        await Assert.That(IslandClassifier.LooksUnderSplit(2, teamCount: 2)).IsFalse();
    }

    [Test]
    public async Task LooksUnderSplit_ignoresSingleTeamMaps()
    {
        await Assert.That(IslandClassifier.LooksUnderSplit(1, teamCount: 1)).IsFalse();
        await Assert.That(IslandClassifier.LooksUnderSplit(1, teamCount: 0)).IsFalse();
    }

    private static IReadOnlyList<IslandDetector.Island> MakeIslands(params int[] counts) =>
        counts.Select((c, i) => new IslandDetector.Island(i + 1, c, (0, 0, 1, 1),
            new NetTopologySuite.Geometries.GeometryFactory().ToGeometry(
                new NetTopologySuite.Geometries.Envelope(0, 1, 0, 1)))).ToList();
}
