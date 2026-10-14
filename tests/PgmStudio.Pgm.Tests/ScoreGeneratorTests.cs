using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The <c>&lt;score&gt;</c> a board ends at. Without one PGM builds no score module and every rate a point or a
/// flag names pays nothing, all match, silently — so a board whose objectives pay must carry one.
/// </summary>
public sealed class ScoreGeneratorTests
{
    private static readonly ControlPointIntent Point = new()
    {
        Name = "Middle", Anchor = new Pt(0, 0, 0), Size = 7,
        PadBox = new BlockBox(-3, 63, -3, 3, 63, 3), CaptureBox = new BlockBox(-3, 63, -3, 3, 65, 3),
    };

    private static readonly FlagIntent Flag = new() { Posts = [new FlagPostIntent { At = new Pt(0.5, 10, 0.5) }] };

    private static Dict Generate(MapIntent intent)
    {
        var doc = new Dict();
        ScoreGenerator.Apply(doc, intent);
        return doc;
    }

    [Test]
    public async Task A_board_with_a_scoring_point_gets_the_corpus_score_limit()
    {
        var score = (Dict)Generate(new MapIntent { ControlPoints = [Point] })["score"]!;
        await Assert.That(score["limit"]).IsEqualTo((long)ObjectiveDefaults.ControlPointScoreLimit);
    }

    [Test]
    public async Task A_board_with_a_flag_that_pays_gets_the_king_of_the_flag_limit()
    {
        var score = (Dict)Generate(new MapIntent { Flags = [Flag] })["score"]!;
        await Assert.That(score["limit"]).IsEqualTo((long)ObjectiveDefaults.FlagScoreLimit);
    }

    [Test]
    public async Task An_authored_limit_wins()
    {
        var doc = Generate(new MapIntent { ControlPoints = [Point], ScoreLimit = 2500 });
        await Assert.That(((Dict)doc["score"]!)["limit"]).IsEqualTo(2500L);
    }

    // Zero is an author asking for no limit — a board played to a time limit instead.
    [Test]
    public async Task A_limit_of_zero_writes_no_score_element()
    {
        var doc = Generate(new MapIntent { ControlPoints = [Point], ScoreLimit = 0 });
        await Assert.That(doc.ContainsKey("score")).IsFalse();
    }

    [Test]
    public async Task A_board_with_nothing_that_pays_gets_no_score_element()
        => await Assert.That(Generate(new MapIntent()).ContainsKey("score")).IsFalse();

    // ── score boxes ─────────────────────────────────────────────────────────────────

    private static readonly TeamDef[] Teams =
    [
        new() { Id = "red-team", Name = "Red", Color = "red" },
        new() { Id = "blue-team", Name = "Blue", Color = "blue" },
    ];

    private static MapIntent BoxBoard(bool home = true) => new()
    {
        Meta = new MetaIntent { Name = "Box Board" },
        Symmetry = new SymmetryIntent { Mode = "rot_180" },
        Teams = [.. Teams],
        Spawns = [new SpawnIntent { Team = "red-team", Point = new Pt(-30.5, 10, 0.5), Yaw = -90 }],
        ScoreBoxes = [new ScoreBoxIntent { Team = "red-team", Box = new BlockBox(28, 10, -2, 32, 13, 2), Home = home }],
    };

    private static MapXml Written(MapIntent intent)
    {
        var doc = new Dict();
        IntentGenerator.Apply(doc, intent);
        return MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));
    }

    [Test]
    public async Task A_box_pays_its_team_and_its_portal_lands_the_scorer_on_their_spawn()
    {
        var map = Written(BoxBoard());

        var red = map.Score!.Boxes.Single(box => box.FilterId == "red-team");
        await Assert.That(red.Points).IsEqualTo(ObjectiveDefaults.ScoreBoxPoints);
        var box = map.Regions[red.RegionId];
        await Assert.That((box.MinX, box.MinY, box.MaxX, box.MaxY)).IsEqualTo(((double?)28, (double?)10, (double?)33, (double?)14))
            .Because("a cuboid's max is exclusive");

        var portal = map.Portals.Single(p => p.RegionId == red.RegionId);
        await Assert.That(portal.FilterId).IsEqualTo("red-team");
        await Assert.That(portal.DestinationId).IsEqualTo("red-spawn-point");
        await Assert.That(portal.Yaw).IsEqualTo("@-90");
    }

    [Test]
    public async Task A_box_fans_to_the_other_team_with_its_own_way_home()
    {
        var map = Written(BoxBoard());

        var blue = map.Score!.Boxes.Single(box => box.FilterId == "blue-team");
        var box = map.Regions[blue.RegionId];
        await Assert.That((box.MinX, box.MaxX)).IsEqualTo(((double?)-32, (double?)-27));
        await Assert.That(map.Portals.Single(p => p.RegionId == blue.RegionId).DestinationId).IsEqualTo("blue-spawn-point");
    }

    [Test]
    public async Task A_box_board_is_a_scorebox_board_with_no_limit_it_did_not_state()
    {
        var map = Written(BoxBoard());

        await Assert.That(map.DeclaredGamemode).Contains(Gamemodes.Scorebox);
        await Assert.That(map.Score!.Limit).IsNull();
        await Assert.That(map.Objective).IsEqualTo("Reach your score box!");
        await Assert.That(Written(BoxBoard() with { ScoreLimit = 5 }).Score!.Limit).IsEqualTo(5);
    }

    [Test]
    public async Task A_box_that_keeps_its_scorer_writes_no_portal()
    {
        await Assert.That(Written(BoxBoard(home: false)).Portals).IsEmpty();
    }

    [Test]
    public async Task Applying_twice_writes_each_box_and_portal_once()
    {
        var doc = new Dict();
        IntentGenerator.Apply(doc, BoxBoard());
        IntentGenerator.Apply(doc, BoxBoard());

        await Assert.That(((List<object?>)((Dict)doc["score"]!)["boxes"]!).Count).IsEqualTo(2);
        await Assert.That(((List<object?>)doc["portals"]!).Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_board_whose_kills_pay_scores_them_and_states_no_limit_of_its_own()
    {
        var score = (Dict)Generate(new MapIntent { KillPoints = ObjectiveDefaults.KillPoints })["score"]!;

        await Assert.That(score["kills"]).IsEqualTo((long)ObjectiveDefaults.KillPoints);
        await Assert.That(score.ContainsKey("limit")).IsFalse();
    }
}
