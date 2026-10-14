using System.Xml.Linq;
using PgmStudio.Domain;
using PgmStudio.Pgm;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// A score box survives both codecs unchanged: its points under either spelling, its filter, whether it is
/// silent, and its region named or written inside it — beside the portal that sends its scorer home.
/// </summary>
public sealed class ScoreBoxRoundTripTests
{
    internal const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.5.0">
          <name>Box Map</name>
          <version>1.0.0</version>
          <objective>Score</objective>
          <teams>
            <team id="red" color="red" max="8">Red</team>
            <team id="blue" color="blue" max="8">Blue</team>
          </teams>
          <score>
            <limit>5</limit>
            <box region="red-box" points="1" filter="only-red" silent="true"/>
            <box value="3" filter="only-blue">
              <region><cuboid min="-42,10,-2" max="-38,14,2"/></region>
            </box>
          </score>
          <portals>
            <portal region="red-box" destination="red-spawn" yaw="@90" filter="only-red"/>
          </portals>
          <filters>
            <team id="only-red">red</team>
            <team id="only-blue">blue</team>
          </filters>
          <regions>
            <cuboid id="red-box" min="38,10,-2" max="42,14,2"/>
            <point id="red-spawn">-30.5,10,0.5</point>
          </regions>
        </map>
        """;

    private static string ScoreOf(MapXml map) => XDocument.Parse(XmlWriter.ToXml(map)).Root!.Element("score")!.ToString();

    [Test]
    public async Task Every_box_is_read_with_its_points_filter_and_region()
    {
        var boxes = MapParser.ParseXmlString(Xml).Score!.Boxes;

        await Assert.That(boxes.Count).IsEqualTo(2);
        await Assert.That((boxes[0].RegionId, boxes[0].Points, boxes[0].FilterId, boxes[0].Silent))
            .IsEqualTo(("red-box", (int?)1, "only-red", (bool?)true));
        await Assert.That(boxes[1].Points).IsEqualTo(3).Because("value is points under its other name");
        await Assert.That(boxes[1].RegionId).IsNotEmpty();
    }

    [Test]
    public async Task A_map_writes_the_same_score_after_the_xml_codec()
    {
        var first = MapParser.ParseXmlString(Xml);
        var again = MapParser.ParseXmlString(XmlWriter.ToXml(first));

        await Assert.That(ScoreOf(again)).IsEqualTo(ScoreOf(first));
        await Assert.That(again.Portals.Single().RegionId).IsEqualTo("red-box");
    }

    [Test]
    public async Task A_map_writes_the_same_score_after_the_json_codec()
    {
        var first = MapParser.ParseXmlString(Xml);

        await Assert.That(ScoreOf(Deserializer.FromDict(Serializer.ToDict(first)))).IsEqualTo(ScoreOf(first));
    }
}
