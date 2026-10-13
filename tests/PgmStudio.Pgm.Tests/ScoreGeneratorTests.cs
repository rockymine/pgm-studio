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
}
