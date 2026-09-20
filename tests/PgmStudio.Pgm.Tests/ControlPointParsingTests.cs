using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// The CP/KotH XML surface. One PGM parser builds both spellings, so one reader does too, and the element a
/// point was written as is carried rather than resolved: a hill and a control point disagree about nearly
/// every default, so materialising one here would change the map. <c>docs/pgm/control-points.md</c> holds
/// the default table and the corpus measurements these cases are drawn from.
/// </summary>
public sealed class ControlPointParsingTests
{
    private static MapXml Parse(string body) => MapParser.ParseXmlString(
        "<?xml version=\"1.0\"?><map proto=\"1.5.0\"><name>m</name><version>1</version><objective>o</objective>"
        + body + "</map>");

    private static ControlPoint One(string body) => Parse(body).ControlPoints.Single();

    // ── the two spellings ───────────────────────────────────────────────────────────
    [Test]
    public async Task A_hill_and_a_control_point_are_the_same_objective_under_different_elements()
    {
        var m = Parse("""
            <control-points><control-point id="a" capture="ra"/></control-points>
            <king><hills><hill id="b" capture="rb"/></hills></king>
            """);
        await Assert.That(m.ControlPoints.Select(p => p.Id)).IsEquivalentTo(new[] { "a", "b" });
        await Assert.That(m.ControlPoints.Single(p => p.Id == "a").Element).IsEqualTo(ControlPointElement.ControlPoints);
        await Assert.That(m.ControlPoints.Single(p => p.Id == "b").Element).IsEqualTo(ControlPointElement.King);
    }

    // PGM flattens <king> over "hills"/"hill", so the <hills> level is optional.
    [Test]
    public async Task A_hill_directly_under_king_needs_no_hills_element()
    {
        var p = One("""<king><hill id="h" capture="r"/></king>""");
        await Assert.That(p.Element).IsEqualTo(ControlPointElement.King);
        await Assert.That(p.CaptureRegionId).IsEqualTo("r");
    }

    // ── attribute inheritance ───────────────────────────────────────────────────────
    // The corpus writes the shared tuning once on <hills> and gives each hill only a name and its regions.
    [Test]
    public async Task Group_attributes_cascade_to_every_hill()
    {
        var m = Parse("""
            <king>
              <hills required="false" capture-time="5s" points="1" neutral-state="true">
                <hill name="North" capture="north"/>
                <hill name="South" capture="south"/>
              </hills>
            </king>
            """);
        await Assert.That(m.ControlPoints.Count).IsEqualTo(2);
        foreach (var p in m.ControlPoints)
        {
            await Assert.That(p.Required).IsFalse();
            await Assert.That(p.CaptureTime).IsEqualTo("5s");
            await Assert.That(p.Points).IsEqualTo(1.0);
            await Assert.That(p.NeutralState).IsTrue();
        }
    }

    // <king> is a level above <hills>, and PGM's copy-on-flatten means its attributes reach the leaf too.
    [Test]
    public async Task An_attribute_on_king_reaches_the_hill_through_hills()
    {
        var p = One("""<king points="2"><hills capture-time="8s"><hill capture="r"/></hills></king>""");
        await Assert.That(p.Points).IsEqualTo(2.0);
        await Assert.That(p.CaptureTime).IsEqualTo("8s");
    }

    [Test]
    public async Task The_nearest_declaration_wins()
    {
        var p = One("""<king points="2"><hills points="3"><hill capture="r" points="4"/></hills></king>""");
        await Assert.That(p.Points).IsEqualTo(4.0);
    }

    // ── the three regions ───────────────────────────────────────────────────────────
    [Test]
    public async Task Each_region_is_read_under_either_spelling_as_an_attribute()
    {
        var p = One("""
            <control-points><control-point id="h" capture="cap" progress="prog" captured="own"/></control-points>
            """);
        await Assert.That(p.CaptureRegionId).IsEqualTo("cap");
        await Assert.That(p.ProgressRegionId).IsEqualTo("prog");
        await Assert.That(p.OwnerRegionId).IsEqualTo("own");
    }

    [Test]
    public async Task The_long_spellings_read_the_same_regions()
    {
        var p = One("""
            <control-points>
              <control-point id="h" capture-region="cap" progress-display-region="prog" owner-display-region="own"/>
            </control-points>
            """);
        await Assert.That(p.CaptureRegionId).IsEqualTo("cap");
        await Assert.That(p.ProgressRegionId).IsEqualTo("prog");
        await Assert.That(p.OwnerRegionId).IsEqualTo("own");
    }

    // The corpus form for a round hill: the geometry inline, under the short child element.
    [Test]
    public async Task A_child_element_wrapping_geometry_registers_the_region()
    {
        var m = Parse("""
            <king><hills><hill id="top">
              <capture><cylinder base="-0.5,36,0.5" radius="6.6" height="4"/></capture>
            </hill></hills></king>
            """);
        var p = m.ControlPoints.Single();
        await Assert.That(p.CaptureRegionId).IsNotEmpty();
        var region = m.Regions[p.CaptureRegionId];
        await Assert.That(region.Type).IsEqualTo("cylinder");
        await Assert.That(region.Radius).IsEqualTo(6.6);
        await Assert.That(region.Height).IsEqualTo(4.0);
    }

    // A wrapper holding several shapes registers a union, so no shape is dropped — beach_battles draws its
    // progress display as a castle roof plus the pad under it.
    [Test]
    public async Task A_wrapper_with_several_shapes_becomes_a_union()
    {
        var m = Parse("""
            <king><hills><hill id="north" capture="c">
              <progress>
                <cuboid min="-13,20,-72" max="13,30,-46"/>
                <cuboid min="-9,8,-66" max="9,9,-47"/>
              </progress>
            </hill></hills></king>
            """);
        var region = m.Regions[m.ControlPoints.Single().ProgressRegionId];
        await Assert.That(region.Type).IsEqualTo("union");
        await Assert.That(region.Children!.Count).IsEqualTo(2);
    }

    // ── what stays unstated ─────────────────────────────────────────────────────────
    // PGM's default for each of these depends on the element, so an unwritten attribute cannot be
    // resolved here without deciding which map it is.
    [Test]
    public async Task An_unstated_knob_stays_unstated()
    {
        var p = One("""<control-points><control-point id="h" capture="r"/></control-points>""");
        await Assert.That(p.Incremental).IsNull();
        await Assert.That(p.NeutralState).IsNull();
        await Assert.That(p.ShowProgress).IsNull();
        await Assert.That(p.Required).IsNull();
        await Assert.That(p.Points).IsNull();
        await Assert.That(p.TimeMultiplier).IsNull();
        await Assert.That(p.CaptureTime).IsEqualTo("");
        await Assert.That(p.CaptureRule).IsEqualTo("");
    }

    // required="false" is a statement, and the one the whole corpus makes: unstated means PGM's true,
    // which ends the match for whoever captures.
    [Test]
    public async Task Required_false_is_not_the_same_as_unstated()
    {
        await Assert.That(One("""<king><hills required="false"><hill capture="r"/></hills></king>""").Required).IsFalse();
        await Assert.That(One("""<king><hills><hill capture="r"/></hills></king>""").Required).IsNull();
    }

    [Test]
    [Arguments("recovery", "1")]
    [Arguments("recovery-rate", "1")]
    [Arguments("decay", "0")]
    [Arguments("decay-rate", "0")]
    public async Task Either_spelling_of_a_rate_is_read(string attribute, string value)
    {
        var p = One($"""<king><hills><hill capture="r" {attribute}="{value}"/></hills></king>""");
        var read = attribute.StartsWith("recovery") ? p.Recovery : p.Decay;
        await Assert.That(read).IsEqualTo(double.Parse(value));
    }

    // ── ids and names ───────────────────────────────────────────────────────────────
    // PGM names an unnamed point "Hill", "Hill 2", …; that is PGM's to do, and writing one here would put
    // a name the author never chose into the export.
    [Test]
    public async Task An_unnamed_point_keeps_no_name_and_still_gets_an_id()
    {
        var m = Parse("""<king><hills><hill capture="a"/><hill capture="b"/></hills></king>""");
        await Assert.That(m.ControlPoints.Select(p => p.Name)).IsEquivalentTo(new[] { "", "" });
        await Assert.That(m.ControlPoints.Select(p => p.Id).Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task An_id_is_generated_from_the_name_when_the_map_states_none()
    {
        var p = One("""<king><hills><hill name="Mid Hill" capture="r"/></hills></king>""");
        await Assert.That(p.Id).IsEqualTo("mid-hill");
    }

    // ── the score module ────────────────────────────────────────────────────────────
    [Test]
    public async Task A_map_with_no_score_element_has_no_score_config()
        => await Assert.That(Parse("""<king><hills><hill capture="r"/></hills></king>""").Score).IsNull();

    [Test]
    public async Task An_empty_score_element_is_a_score_module_with_nothing_set()
    {
        var score = Parse("<score/>").Score;
        await Assert.That(score).IsNotNull();
        await Assert.That(score!.Limit).IsNull();
    }

    [Test]
    public async Task The_score_limit_is_read_as_a_child_or_an_attribute()
    {
        await Assert.That(Parse("<score><limit>750</limit></score>").Score!.Limit).IsEqualTo(750);
        await Assert.That(Parse("""<score limit="750"/>""").Score!.Limit).IsEqualTo(750);
    }

    [Test]
    public async Task Kills_deaths_and_mercy_are_read()
    {
        var score = Parse("""<score><kills>2</kills><deaths>1</deaths><mercy min="30">60</mercy></score>""").Score!;
        await Assert.That(score.Kills).IsEqualTo(2);
        await Assert.That(score.Deaths).IsEqualTo(1);
        await Assert.That(score.Mercy).IsEqualTo(60);
        await Assert.That(score.MercyMin).IsEqualTo(30);
    }

    // The legacy marker zeroed the default kill and death scores; at every proto the studio reads it does
    // nothing, and it is kept only so it round-trips.
    [Test]
    public async Task The_legacy_king_marker_inside_score_is_kept()
        => await Assert.That(Parse("<score><limit>2500</limit><king/></score>").Score!.King).IsTrue();
}
