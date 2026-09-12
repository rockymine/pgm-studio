using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A control point survives both codecs unchanged — XML → MapXml → XML, and MapXml → JSON → MapXml. The
/// invariant under test is that nothing is materialised on the way through: a knob the map left unset comes
/// back unset, because the value PGM would apply to it depends on which element the point was written as.
/// </summary>
public sealed class ControlPointRoundTripTests
{
    private const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>Hill Map</name>
          <version>1.0.0</version>
          <objective>Hold the hills</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <king>
            <hills required="false" capture-time="5s" points="1" time-multiplier="0"
                   neutral-state="true" incremental="true" show-progress="true" permanent="false">
              <hill name="North" id="north" capture="north-cap" progress="north-cap" captured="north-signal"/>
              <hill name="Middle" id="middle" capture="mid-cap" progress="mid-cap"/>
            </hills>
          </king>
          <score><limit>750</limit></score>
          <regions>
            <cuboid id="north-cap" min="-3,6,-2" max="4,9,5"/>
            <cuboid id="north-signal" min="-12,16,1" max="13,19,2"/>
            <cuboid id="mid-cap" min="-3,6,20" max="4,9,27"/>
          </regions>
        </map>
        """;

    private static MapXml Parsed() => MapParser.ParseXmlString(Xml);

    [Test]
    public async Task The_document_survives_the_xml_round_trip()
    {
        var again = MapParser.ParseXmlString(XmlWriter.ToXml(Parsed()));
        await Assert.That(again.ControlPoints.Count).IsEqualTo(2);
        await Assert.That(again.Score!.Limit).IsEqualTo(750);

        var north = again.ControlPoints.Single(p => p.Id == "north");
        await Assert.That(north.Element).IsEqualTo(ControlPointElement.King);
        await Assert.That(north.Name).IsEqualTo("North");
        await Assert.That(north.CaptureRegionId).IsEqualTo("north-cap");
        await Assert.That(north.ProgressRegionId).IsEqualTo("north-cap");
        await Assert.That(north.OwnerRegionId).IsEqualTo("north-signal");
        await Assert.That(north.Required).IsFalse();
        await Assert.That(north.CaptureTime).IsEqualTo("5s");
        await Assert.That(north.Points).IsEqualTo(1.0);
        await Assert.That(north.TimeMultiplier).IsEqualTo(0.0);
        await Assert.That(north.NeutralState).IsTrue();
        await Assert.That(north.Incremental).IsTrue();
        await Assert.That(north.ShowProgress).IsTrue();
        await Assert.That(north.Permanent).IsFalse();

        // Middle states no owner display and must not gain one.
        await Assert.That(again.ControlPoints.Single(p => p.Id == "middle").OwnerRegionId).IsEqualTo("");
    }

    // The spelling is what tells PGM which defaults to apply, so a map that said "hill" gets "hill" back.
    [Test]
    public async Task Each_point_is_re_emitted_under_the_element_it_was_written_as()
    {
        var m = MapParser.ParseXmlString("""
            <?xml version="1.0"?><map proto="1.5.0"><name>m</name><version>1</version><objective>o</objective>
            <control-points><control-point id="p" capture="ra"/></control-points>
            <king><hills><hill id="h" capture="rb"/></hills></king>
            <regions><cuboid id="ra" min="0,0,0" max="1,1,1"/><cuboid id="rb" min="5,0,5" max="6,1,6"/></regions>
            </map>
            """);
        var xml = XmlWriter.ToXml(m);
        await Assert.That(xml).Contains("""<control-point id="p" capture-region="ra"/>""");
        await Assert.That(xml).Contains("""<hill id="h" capture-region="rb"/>""");
        await Assert.That(xml).Contains("<king>");
    }

    // An unset knob writes no attribute: PGM then applies the same default it applied before, and writing
    // one in would pin the point to a value the author never chose.
    [Test]
    public async Task An_unset_knob_writes_no_attribute()
    {
        var m = MapParser.ParseXmlString("""
            <?xml version="1.0"?><map proto="1.5.0"><name>m</name><version>1</version><objective>o</objective>
            <king><hills><hill id="h" capture="r"/></hills></king>
            <regions><cuboid id="r" min="0,0,0" max="1,1,1"/></regions></map>
            """);
        var xml = XmlWriter.ToXml(m);
        foreach (var attribute in new[] { "required=", "neutral-state=", "incremental=", "points=", "show-progress=", "capture-time=", "permanent=" })
            await Assert.That(xml).DoesNotContain(attribute);
    }

    // A map with no <score> must not gain one: with no element PGM loads no score module, and every
    // points rate on the map pays nothing.
    [Test]
    public async Task A_map_without_a_score_element_does_not_grow_one()
    {
        var m = MapParser.ParseXmlString("""
            <?xml version="1.0"?><map proto="1.5.0"><name>m</name><version>1</version><objective>o</objective>
            <king><hills><hill id="h" capture="r"/></hills></king>
            <regions><cuboid id="r" min="0,0,0" max="1,1,1"/></regions></map>
            """);
        await Assert.That(XmlWriter.ToXml(m)).DoesNotContain("<score");
    }

    [Test]
    public async Task An_empty_score_element_survives_as_an_empty_score_element()
    {
        var m = MapParser.ParseXmlString(
            "<?xml version=\"1.0\"?><map proto=\"1.5.0\"><name>m</name><version>1</version><objective>o</objective><score/></map>");
        await Assert.That(MapParser.ParseXmlString(XmlWriter.ToXml(m)).Score).IsNotNull();
    }

    [Test]
    public async Task The_document_survives_the_json_round_trip()
    {
        var again = Deserializer.FromDict(Serializer.ToDict(Parsed()));
        await Assert.That(again.ControlPoints.Count).IsEqualTo(2);
        await Assert.That(again.Score!.Limit).IsEqualTo(750);

        var north = again.ControlPoints.Single(p => p.Id == "north");
        await Assert.That(north.Element).IsEqualTo(ControlPointElement.King);
        await Assert.That(north.Required).IsFalse();
        await Assert.That(north.CaptureTime).IsEqualTo("5s");
        await Assert.That(north.OwnerRegionId).IsEqualTo("north-signal");

        var middle = again.ControlPoints.Single(p => p.Id == "middle");
        await Assert.That(middle.OwnerRegionId).IsEqualTo("");
        await Assert.That(middle.OwnedDecay).IsNull();
    }

    // A hill whose regions are drawn inline has no id to reference, so the export has to write the
    // geometry back into the leaf rather than a dangling name.
    [Test]
    public async Task An_inline_region_is_written_back_inline()
    {
        var m = MapParser.ParseXmlString("""
            <?xml version="1.0"?><map proto="1.5.0"><name>m</name><version>1</version><objective>o</objective>
            <king><hills><hill id="top">
              <capture><cylinder base="-0.5,36,0.5" radius="6.6" height="4"/></capture>
            </hill></hills></king></map>
            """);
        var xml = XmlWriter.ToXml(m);
        await Assert.That(xml).Contains("<capture-region>");
        await Assert.That(xml).Contains("""radius="6.6""");

        var again = MapParser.ParseXmlString(xml);
        var region = again.Regions[again.ControlPoints.Single().CaptureRegionId];
        await Assert.That(region.Type).IsEqualTo("cylinder");
        await Assert.That(region.Radius).IsEqualTo(6.6);
    }
}
