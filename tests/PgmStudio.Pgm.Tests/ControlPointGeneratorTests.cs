using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Pgm;
using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests;

using Dict = Dictionary<string, object?>;

/// <summary>
/// The capture-point slice of the declarative generator: one <c>&lt;hill&gt;</c> per point, the capture
/// volume and the pad it is drawn on as regions.
/// </summary>
public sealed class ControlPointGeneratorTests
{
    private static readonly BlockBox Pad = new(-3, 63, -3, 3, 63, 3);
    private static readonly BlockBox Capture = new(-3, 63, -3, 3, 65, 3);

    private static ControlPointIntent Sample(string name = "Middle") => new()
    {
        Name = name, Anchor = new Pt(0, 0, 0), Size = 7, PadBox = Pad, CaptureBox = Capture,
    };

    private static Dict Generate(MapIntent intent)
    {
        var doc = new Dict();
        ControlPointGenerator.Apply(doc, intent);
        return doc;
    }

    private static Dict Generate(params ControlPointIntent[] points)
        => Generate(new MapIntent { ControlPoints = [.. points] });

    private static List<object?> Points(Dict doc) => (List<object?>)doc["control_points"]!;
    private static Dict First(Dict doc) => (Dict)Points(doc)[0]!;
    private static Dict Regions(Dict doc) => (Dict)doc["regions"]!;

    // ── the convention the studio writes out ────────────────────────────────────────
    // PGM defaults `required` to true at every proto the studio writes, so a point that leaves it unstated
    // ends the match for whoever captures it first. This is the assertion the whole board depends on.
    [Test]
    public async Task Every_point_is_written_not_required()
        => await Assert.That((bool)First(Generate(Sample()))["required"]!).IsFalse();

    [Test]
    public async Task A_point_is_written_as_a_hill_rather_than_a_control_point()
        => await Assert.That(First(Generate(Sample()))["element"]).IsEqualTo("king");

    // Each of these differs between PGM's two spellings, so the studio states its own rather than relying on
    // whichever default the element happens to carry.
    [Test]
    public async Task The_studios_capture_convention_is_stated_in_full()
    {
        var point = First(Generate(Sample()));
        await Assert.That((bool)point["neutral_state"]!).IsTrue();
        await Assert.That((bool)point["incremental"]!).IsTrue();
        await Assert.That((bool)point["show_progress"]!).IsTrue();
        await Assert.That(point["time_multiplier"]).IsEqualTo(0d);
        await Assert.That(point["capture_time"]).IsEqualTo(ObjectiveDefaults.ControlPointCaptureTime);
        await Assert.That(point["points"]).IsEqualTo(ObjectiveDefaults.ControlPointPoints);
    }

    // ── the two regions are the stamper's own boxes (OB8) ───────────────────────────
    [Test]
    public async Task The_capture_region_is_the_stamped_volume_with_a_max_one_past_the_last_block()
    {
        var doc = Generate(Sample());
        var region = (Dict)Regions(doc)[(string)First(doc)["capture_region"]!]!;
        var min = (Dict)region["min"]!;
        var max = (Dict)region["max"]!;
        await Assert.That(((double)min["x"]!, (double)min["y"]!, (double)min["z"]!)).IsEqualTo((-3d, 63d, -3d));
        await Assert.That(((double)max["x"]!, (double)max["y"]!, (double)max["z"]!)).IsEqualTo((4d, 66d, 4d));
    }

    // A round pad leaves the square's corners as ground, so a player standing on one is not on the point.
    [Test]
    public async Task A_disc_is_captured_in_the_cylinder_standing_on_its_pad()
    {
        var doc = Generate(Sample() with { Shape = ControlPointShapes.Disc });
        var region = (Dict)Regions(doc)[(string)First(doc)["capture_region"]!]!;
        var foot = (Dict)region["base"]!;
        await Assert.That(region["type"]).IsEqualTo("cylinder");
        await Assert.That(((double)foot["x"]!, (double)foot["y"]!, (double)foot["z"]!)).IsEqualTo((0.5d, 63d, 0.5d));
        await Assert.That(((double)region["radius"]!, (double)region["height"]!)).IsEqualTo((3.5d, 3d));
    }

    // The pie is drawn on the pad, so the display region is the one course the stamper laid — never the
    // capture volume, whose upper blocks are air and hold nothing to recolour.
    [Test]
    public async Task The_progress_region_is_the_pad_course_alone()
    {
        var doc = Generate(Sample());
        var region = (Dict)Regions(doc)[(string)First(doc)["progress_region"]!]!;
        var min = (Dict)region["min"]!;
        var max = (Dict)region["max"]!;
        await Assert.That((double)min["y"]!).IsEqualTo(63d);
        await Assert.That((double)max["y"]!).IsEqualTo(64d);   // one past the single pad course
    }

    [Test]
    public async Task An_unresolved_point_emits_nothing()
        => await Assert.That(Points(Generate(Sample() with { CaptureBox = null }))).IsEmpty();

    // ── names and ids ───────────────────────────────────────────────────────────────
    [Test]
    public async Task A_named_point_keeps_its_name_and_takes_an_id_from_it()
    {
        var point = First(Generate(Sample("North Hill")));
        await Assert.That(point["name"]).IsEqualTo("North Hill");
        await Assert.That(point["id"]).IsEqualTo("north-hill");
    }

    // PGM names an unnamed point "Hill", "Hill 2", … better than anything invented here, so none is written.
    [Test]
    public async Task An_unnamed_point_is_left_for_PGM_to_name()
    {
        var point = First(Generate(Sample("")));
        await Assert.That(point.ContainsKey("name")).IsFalse();
        await Assert.That(point["id"]).IsEqualTo("hill");
    }

    [Test]
    public async Task Two_points_of_one_name_still_get_distinct_ids()
    {
        var doc = Generate(Sample("Side"), Sample("Side"));
        await Assert.That(Points(doc).OfType<Dict>().Select(d => d["id"]).Distinct().Count()).IsEqualTo(2);
    }

    // ── regenerating is a replace ───────────────────────────────────────────────────
    [Test]
    public async Task Regenerating_replaces_the_points_and_both_of_their_regions()
    {
        var doc = new Dict();
        var intent = new MapIntent { ControlPoints = [Sample()] };
        ControlPointGenerator.Apply(doc, intent);
        ControlPointGenerator.Apply(doc, intent);

        await Assert.That(Points(doc).Count).IsEqualTo(1);
        // Both regions replaced rather than orphaned beside a second copy.
        await Assert.That(Regions(doc).Count).IsEqualTo(2);
    }

    // ── end to end ──────────────────────────────────────────────────────────────────
    // The board an agent posts has to come back out of the codec as a map PGM would load, and read as KotH.
    [Test]
    public async Task A_generated_board_round_trips_as_a_koth_map()
    {
        var doc = new Dict();
        var intent = new MapIntent
        {
            Meta = new MetaIntent { Name = "Hill Board" },
            Teams = [new TeamDef { Id = "red", Name = "Red" }, new TeamDef { Id = "blue", Name = "Blue" }],
            ControlPoints = [Sample("Middle")],
        };
        IntentGenerator.Apply(doc, intent);

        var map = MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(doc)));

        await Assert.That(map.Gamemodes).IsEquivalentTo(new[] { "koth" });
        await Assert.That(map.Objective).IsEqualTo("Hold the hill!");

        var point = map.ControlPoints.Single();
        await Assert.That(point.Element).IsEqualTo(ControlPointElement.King);
        await Assert.That(point.Required).IsFalse();
        await Assert.That(point.Name).IsEqualTo("Middle");
        await Assert.That(map.Regions.ContainsKey(point.CaptureRegionId)).IsTrue();
        await Assert.That(map.Regions.ContainsKey(point.ProgressRegionId)).IsTrue();
        await Assert.That(map.Score!.Limit).IsEqualTo(ObjectiveDefaults.ControlPointScoreLimit);
    }

    // ── a touch point ───────────────────────────────────────────────────────────────

    private static MapXml Written(params ControlPointIntent[] points)
        => MapParser.ParseXmlString(XmlWriter.ToXml(Deserializer.FromDict(Generate(points))));

    [Test]
    public async Task A_touch_point_changes_hands_on_a_step_with_no_neutral_state()
    {
        var point = Written(Sample() with { CaptureMode = CaptureModes.Touch }).ControlPoints.Single();

        await Assert.That(point.CaptureTime).IsEqualTo(ObjectiveDefaults.TouchCaptureTime);
        await Assert.That(point.CaptureRule).IsEqualTo("lead");
        await Assert.That(point.NeutralState).IsFalse();
        await Assert.That(point.Incremental).IsFalse();
    }

    // The holders are filtered off the point, so a step by anyone else takes it however many holders stand there.
    [Test]
    public async Task A_touch_point_is_played_only_by_the_teams_that_do_not_hold_it()
    {
        var map = Written(Sample() with { CaptureMode = CaptureModes.Touch });
        var point = map.ControlPoints.Single();

        var touchers = map.Filters[point.PlayerFilterId];
        await Assert.That(touchers.Type).IsEqualTo("not");
        var held = map.Filters[touchers.Child!];
        await Assert.That((held.Type, held.Objective)).IsEqualTo(("objective", (string?)point.Id));
    }

    [Test]
    public async Task A_held_point_keeps_its_neutral_state_and_names_no_player_filter()
    {
        var point = Written(Sample()).ControlPoints.Single();

        await Assert.That(point.NeutralState).IsTrue();
        await Assert.That(point.PlayerFilterId).IsEmpty();
    }

    [Test]
    public async Task Applying_a_touch_point_twice_writes_its_filters_once()
    {
        var doc = new Dict();
        var intent = new MapIntent { ControlPoints = [Sample() with { CaptureMode = CaptureModes.Touch }] };
        ControlPointGenerator.Apply(doc, intent);
        ControlPointGenerator.Apply(doc, intent);

        await Assert.That(((Dict)doc["filters"]!).Count).IsEqualTo(2);
    }
}
