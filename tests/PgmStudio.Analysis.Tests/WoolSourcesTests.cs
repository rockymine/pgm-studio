using PgmStudio.Analysis;
using PgmStudio.Pgm;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// Synthetic wool source/availability tests (exact parity vs Python over the feature maps lives in
/// tools/PgmStudio.RoundTrip --wool).
/// </summary>
public sealed class WoolSourcesTests
{
    private const string Xml = """
        <?xml version="1.0"?>
        <map proto="1.4.0">
          <name>w</name><version>1</version><objective>o</objective>
          <teams><team id="red-team" color="red">Red</team></teams>
          <wools team="red-team">
            <wool color="red" location="0,0,0"><monument><block>1,1,1</block></monument></wool>
          </wools>
          <regions><cuboid id="red-room" min="0,0,0" max="10,10,10"/></regions>
        </map>
        """;

    [Test]
    public async Task Declared_wool_with_no_source_is_an_error()
    {
        var doc = Serializer.ToDict(MapParser.ParseXmlString(Xml));
        var avail = WoolSources.CheckAvailability(doc, []);
        await Assert.That(avail.Count).IsEqualTo(1);
        await Assert.That(avail[0].Color).IsEqualTo("red");
        await Assert.That(avail[0].Obtainable).IsFalse();
        await Assert.That(avail[0].Severity).IsEqualTo("error");
    }

    [Test]
    public async Task A_wool_block_in_the_room_makes_it_obtainable()
    {
        // declare the room on the wool so availability clips physical sources to it
        var xmlWithRoom = Xml.Replace("""<wool color="red" location="0,0,0">""",
                                      """<wool color="red" location="0,0,0" monument="">""");
        var doc = Serializer.ToDict(MapParser.ParseXmlString(Xml));
        // set wool_room_region via the doc directly (parser doesn't read a room attr here)
        var wools = (List<object?>)doc["wools"]!;
        ((Dictionary<string, object?>)wools[0]!)["wool_room_region"] = "red-room";

        var sources = new List<WoolSources.Source> { new("block", "red", 5, 4, 5, 1) };  // inside red-room
        var avail = WoolSources.CheckAvailability(doc, sources);

        await Assert.That(avail[0].Obtainable).IsTrue();
        await Assert.That(avail[0].OneTime).IsTrue();           // a bare block is one-time
        await Assert.That(avail[0].SourceTypes).Contains("block");
    }

    [Test]
    public async Task Monument_is_clear_without_a_terrain_layer_and_obstructed_when_the_block_is_solid()
    {
        var doc = Serializer.ToDict(MapParser.ParseXmlString(Xml));   // monument block at 1,1,1

        // No terrain layer (segments null) — nothing to test against, reported clear.
        var clear = WoolSources.CheckMonumentObstruction(doc, null);
        await Assert.That(clear.Count).IsEqualTo(1);
        await Assert.That(clear[0].WoolColor).IsEqualTo("red");
        await Assert.That((clear[0].X, clear[0].Y, clear[0].Z)).IsEqualTo((1, 1, 1));
        await Assert.That(clear[0].Obstructed).IsFalse();
        await Assert.That(clear[0].Severity).IsEqualTo("ok");

        // A solid block at the monument cell — wool can't be placed → obstructed (error).
        var segs = new SegmentIndex([(1, 1, 0, 2)]);   // column (x=1,z=1) solid y0..2, so (1,1,1) is solid
        var blocked = WoolSources.CheckMonumentObstruction(doc, segs);
        await Assert.That(blocked[0].Obstructed).IsTrue();
        await Assert.That(blocked[0].Severity).IsEqualTo("error");
        await Assert.That(blocked[0].Message).Contains("obstructed");
    }

    [Test]
    public async Task SourcesInRegion_summarises_only_the_wool_inside_the_drawn_rectangle()
    {
        var doc = Serializer.ToDict(MapParser.ParseXmlString(Xml));
        var sources = new List<WoolSources.Source>
        {
            new("block", "red", 5, 4, 5, 1),       // inside (0,0)-(10,10)
            new("block", "blue", 50, 4, 50, 1),    // outside
        };
        var colors = WoolSources.SourcesInRegion(doc, sources, 0, 0, 10, 10);
        await Assert.That(colors.Count).IsEqualTo(1);
        await Assert.That(colors[0].Color).IsEqualTo("red");
        await Assert.That(colors[0].Total).IsEqualTo(1);
        await Assert.That(colors[0].OneTime).IsTrue();   // a bare block is one-time
    }

    [Test]
    public async Task SuggestWools_proposes_undeclared_colours_only()
    {
        var doc = Serializer.ToDict(MapParser.ParseXmlString(Xml));   // declares red
        var sources = new List<WoolSources.Source>
        {
            new("block", "blue", 1, 1, 1, 2),   // not declared → suggested
            new("block", "red", 2, 2, 2, 1),    // declared → not suggested
        };
        var sugg = WoolSources.SuggestWools(doc, sources);
        await Assert.That(sugg.Select(s => s.Color)).Contains("blue");
        await Assert.That(sugg.Any(s => s.Color == "red")).IsFalse();
    }
}
