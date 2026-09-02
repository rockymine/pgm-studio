using PgmStudio.Domain;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Export.Tests;

/// <summary>
/// <see cref="RouteRead"/>: a drawn route walked down its own centreline, over a built board. What is asserted
/// is what the read exists to say and a column read cannot — that the walk follows the road, that a station
/// knows whether the paving reached it, and that a step in the ground under the road is named where it is.
/// </summary>
public sealed class RouteReadTests
{
    // Two plates meeting at x=0, the east one two courses higher, so a road laid straight through has one
    // rise in it at a cell the read has to name. A shape's `max_x` is the far edge rather than the last cell,
    // so these abut without a gap between them.
    private const string Stepped =
        """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers":[{"id":"ground","base_y":0,"layout":{"shapes":[
            {"id":"a","type":"rectangle","operation":"add","min_x":-30,"min_z":-30,"max_x":0,"max_z":30,"base_height":8},
            {"id":"b","type":"rectangle","operation":"add","min_x":0,"min_z":-30,"max_x":30,"max_z":30,"base_height":10}],
          "groups":[]}}],
         "dressing":{"props":[
            {"kind":"stroke","id":"lane","points":[[-20,0],[20,0]],"radius":2,"seed":5,"route":true,
             "pave":{"kind":"solid","id":4}}]}}
        """;

    private static MapIntent Intent() => new()
    {
        Teams = [new TeamDef { Id = "red", Color = "red" }, new TeamDef { Id = "blue", Color = "blue" }],
    };

    private static RouteRead.Walked Walk(string layout, string id = "lane", int image = 0) =>
        RouteRead.Of(WorldBuilder.Build(layout, Intent()), layout, id, image)!;

    [Test]
    public async Task The_walk_follows_the_strokes_own_line_end_to_end()
    {
        var walked = Walk(Stepped);

        await Assert.That(walked.Id).IsEqualTo("lane");
        await Assert.That(walked.Route).IsTrue();
        await Assert.That(walked.Stations[0].X).IsEqualTo(-20);
        await Assert.That(walked.Stations[^1].X).IsEqualTo(20);
        await Assert.That(walked.Stations.All(station => station.Z == 0)).IsTrue();
        // Every block of the line, each once and in order: a station per cell from end to end.
        await Assert.That(walked.Stations.Count).IsEqualTo(41);
        await Assert.That(walked.Stations.Select(station => station.X).Distinct().Count())
            .IsEqualTo(walked.Stations.Count);
    }

    [Test]
    public async Task A_step_in_the_ground_under_the_road_is_named_where_it_is()
    {
        var walked = Walk(Stepped);

        // The plate east of x=0 stands two blocks higher, so the road crosses one scramble and no drop.
        await Assert.That(walked.WorstStep).IsEqualTo(2);
        await Assert.That(walked.Rises).IsEqualTo(1);
        await Assert.That(walked.Falls).IsEqualTo(0);
        await Assert.That(walked.Events.Single()).IsEqualTo("scramble +2 at (0, 0)");
    }

    [Test]
    public async Task A_station_says_whether_the_paving_reached_it_and_what_it_is_made_of()
    {
        var walked = Walk(Stepped);

        await Assert.That(walked.Paved).IsGreaterThan(0);
        await Assert.That(walked.Materials).IsNotEmpty();
        await Assert.That(walked.Materials[0].Material).Contains("4:0 Cobblestone");   // what it paves with
        await Assert.That(walked.Materials.Sum(run => run.Cells)).IsEqualTo(walked.Paved);
        await Assert.That(walked.MaterialRuns).IsGreaterThanOrEqualTo(1);
        await Assert.That(walked.Stations.Count(station => station.Paved)
                          + walked.Gaps.Sum(gap => gap.Cells)).IsEqualTo(walked.Stations.Count);
    }

    [Test]
    public async Task A_road_the_pass_never_paved_is_all_gap()
    {
        // The same line drawn over nothing: the board's ground stops at x 30, and past it there is no column
        // to pave, so the walk still answers and every station of it is unpaved.
        const string OffTheBoard =
            """
            {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
             "layers":[{"id":"ground","base_y":0,"layout":{"shapes":[
                {"id":"a","type":"rectangle","operation":"add","min_x":-30,"min_z":-30,"max_x":0,"max_z":30,"base_height":8}],
              "groups":[]}}],
             "dressing":{"props":[
                {"kind":"stroke","id":"lane","points":[[60,60],[80,60]],"radius":2,"seed":5,"route":true,
                 "pave":{"kind":"solid","id":4}}]}}
            """;

        var walked = Walk(OffTheBoard);

        await Assert.That(walked.Paved).IsEqualTo(0);
        await Assert.That(walked.Gaps.Single().Cells).IsEqualTo(walked.Stations.Count);
        await Assert.That(walked.Stations.All(station => station.Word == "void")).IsTrue();
        await Assert.That(RouteRead.Render(walked)).Contains("materials: (none paved)");
    }

    [Test]
    public async Task Each_image_of_a_mirrored_road_is_walked_on_its_own_ground()
    {
        // The same stepped board mirrored, with the lane off the centre line so its image is a second road
        // rather than itself: the two are congruent and each answers for the ground it stands on.
        var mirrored = Stepped
            .Replace("\"mirror_mode\":\"none\"", "\"mirror_mode\":\"rot_180\"")
            .Replace("[[-20,0],[20,0]]", "[[-20,-10],[20,-10]]");

        var first = Walk(mirrored);
        var second = Walk(mirrored, image: 1);

        await Assert.That(first.Images).IsEqualTo(2);
        await Assert.That(second.Image).IsEqualTo(1);
        await Assert.That(second.Stations.Count).IsEqualTo(first.Stations.Count);
        // A stroke is fanned as the outline it is — its centreline turned point by point, not cell by cell
        // — so the image of the line from (-20, -10) is the line from (20, 10) about a centre of (0, 0).
        await Assert.That((second.Stations[0].X, second.Stations[0].Z)).IsEqualTo((20, 10));
        await Assert.That(second.Stations.Select(station => (station.X, station.Z))
            .Intersect(first.Stations.Select(station => (station.X, station.Z)))).IsEmpty();
    }

    [Test]
    public async Task A_stroke_the_document_does_not_carry_has_no_road_to_walk()
    {
        await Assert.That(RouteRead.Of(WorldBuilder.Build(Stepped, Intent()), Stepped, "nowhere", 0)).IsNull();
    }
}
