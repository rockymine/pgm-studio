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
}
