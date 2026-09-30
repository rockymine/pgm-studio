using PgmStudio.Analysis.Playability;
using PgmStudio.Analysis.Region;
using PgmStudio.Pgm;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// <c>EZ2</c>: void between standing ground and a build zone that nobody may build across, where the plan put
/// ground. The board is one island the plan draws out to a build zone at x 10..14; what varies is where the
/// sketch left the coast and what the plan said.
/// </summary>
public sealed class BuildZoneGapTests
{
    private static Dictionary<string, object?> Doc(int zoneFrom) => Serializer.ToDict(MapParser.ParseXmlString($$"""
        <?xml version="1.0"?>
        <map proto="1.4.0">
          <name>b</name><version>1</version><objective>o</objective>
          <filters><not id="no-void"><void/></not></filters>
          <regions>
            <rectangle id="build-area" min="{{zoneFrom}},0" max="{{zoneFrom + 5}},8"/>
            <negative id="not-build-area"><region id="build-area"/></negative>
            <apply region="not-build-area" block-place="no-void"/>
          </regions>
        </map>
        """));

    /// <summary>Ground over x 0..<paramref name="coast"/>, z 0..7.</summary>
    private static HashSet<(int, int)> GroundTo(int coast)
    {
        var cells = new HashSet<(int, int)>();
        for (var x = 0; x <= coast; x++) for (var z = 0; z < 8; z++) cells.Add((x, z));
        return cells;
    }

    private static IReadOnlyList<PgmStudio.Vocabulary.Finding> Gaps(int coast, int? plannedTo, int zoneFrom = 10)
    {
        var zones = Editability.Compute(Doc(zoneFrom), GroundTo(coast), (-4, -4, 24, 12));
        return BuildZoneGap.Check(zones, plannedTo is { } planned ? GroundTo(planned) : null);
    }

    [Test]
    public async Task A_coast_pulled_back_from_the_zone_the_plan_drew_it_to_is_a_gap()
    {
        var gaps = Gaps(coast: 6, plannedTo: 9);

        await Assert.That(gaps.Count).IsEqualTo(1);
        await Assert.That(gaps[0].Rule).IsEqualTo(EditZoneRules.BuildZoneGap);
        await Assert.That(gaps[0].Message).Contains("24 void column(s) in x 7..9, z 0..7").And.Contains("up to 3 wide");
    }

    [Test]
    public async Task A_coast_that_meets_the_zone_leaves_no_gap()
        => await Assert.That(Gaps(coast: 9, plannedTo: 9)).IsEmpty();

    [Test]
    public async Task Void_the_plan_left_between_a_piece_and_a_zone_is_the_composed_board()
        => await Assert.That(Gaps(coast: 6, plannedTo: 6)).IsEmpty();

    [Test]
    public async Task A_board_built_without_a_plan_has_nothing_to_compare_against()
        => await Assert.That(Gaps(coast: 6, plannedTo: null)).IsEmpty();

    [Test]
    public async Task Void_wider_than_the_reach_is_a_crossing_and_not_a_strip()
    {
        // The zone's first column is eleven steps out from the coast, one past the reach.
        await Assert.That(Gaps(coast: 6, plannedTo: 16, zoneFrom: 17)).IsEmpty();
        // Ten steps out is still in reach.
        await Assert.That(Gaps(coast: 6, plannedTo: 16, zoneFrom: 16)).IsNotEmpty();
    }
}
