using PgmStudio.Analysis.Scan;
using PgmStudio.Analysis.Playability;
using PgmStudio.Pgm;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// Synthetic wool source and availability tests. Every real map's answer is digested by
/// <c>tools/PgmStudio.RoundTrip --goldens</c>.
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
    public async Task Monument_seat_is_the_block_being_clear_and_the_block_under_it_being_solid()
    {
        var doc = Serializer.ToDict(MapParser.ParseXmlString(Xml));   // monument block at 1,1,1

        // No terrain layer (segments null) — nothing can be said, so it is not reported as a fault.
        var unscanned = WoolSources.CheckMonumentSeats(doc, null);
        await Assert.That(unscanned.Count).IsEqualTo(1);
        await Assert.That(unscanned[0].WoolColor).IsEqualTo("red");
        await Assert.That((unscanned[0].X, unscanned[0].Y, unscanned[0].Z)).IsEqualTo((1, 1, 1));
        await Assert.That(unscanned[0].Severity).IsEqualTo("ok");

        // Solid through the monument cell — the wool cannot go in at all.
        var blocked = WoolSources.CheckMonumentSeats(doc, new SegmentIndex([(1, 1, 0, 2)]));
        await Assert.That(blocked[0].Clear).IsFalse();
        await Assert.That(blocked[0].Severity).IsEqualTo("error");
        await Assert.That(blocked[0].Message).Contains("obstructed");

        // Solid up to y=0, so (1,1,1) is air standing on a block: the shape a monument is built in.
        var seated = WoolSources.CheckMonumentSeats(doc, new SegmentIndex([(1, 1, -1, 0)]));
        await Assert.That((seated[0].Clear, seated[0].Pedestal)).IsEqualTo((true, true));
        await Assert.That(seated[0].Severity).IsEqualTo("ok");

        // Held from above rather than below — a monument hung from a ceiling, which plays the same, so it
        // is seated without being on a pedestal.
        var hung = WoolSources.CheckMonumentSeats(doc, new SegmentIndex([(1, 1, 2, 4)]));
        await Assert.That((hung[0].Clear, hung[0].Support, hung[0].Pedestal)).IsEqualTo((true, true, false));
        await Assert.That(hung[0].Severity).IsEqualTo("ok");

        // Held from the side — the wall a monument is set into. The monument's own column carries a run of
        // its own (well above it), because a column with no run at all is one the scan never reached and is
        // reported as saying nothing rather than as empty.
        var walled = WoolSources.CheckMonumentSeats(doc, new SegmentIndex([(1, 1, 5, 6), (2, 1, 0, 4)]));
        await Assert.That((walled[0].Support, walled[0].Pedestal)).IsEqualTo((true, false));
        await Assert.That(walled[0].Severity).IsEqualTo("ok");

        // A column the scan never reached says nothing about the block, so it is not a fault: the map is
        // simply not read there. Without this an unscanned column reads as air on all six faces.
        var unread = WoolSources.CheckMonumentSeats(doc, new SegmentIndex([(40, 40, 0, 4)]));
        await Assert.That(unread[0].Severity).IsEqualTo("ok");

        // Nothing on any of the six faces — there is nothing to place the wool against at all.
        var floating = WoolSources.CheckMonumentSeats(doc, new SegmentIndex([(1, 1, -9, -5)]));
        await Assert.That((floating[0].Clear, floating[0].Support)).IsEqualTo((true, false));
        await Assert.That(floating[0].Severity).IsEqualTo("error");
        await Assert.That(floating[0].Message).Contains("six faces");

        // The block is the FLOORED coordinate, which is PGM's own reading (`BlockRegion` → getBlockX/Y/Z).
        // A monument written at the block centre is the corpus idiom, and a cast would truncate toward zero
        // — so at a negative coordinate the column read would be the neighbour's.
        var centred = Serializer.ToDict(MapParser.ParseXmlString(
            Xml.Replace("<block>1,1,1</block>", "<block>1.5,1,-1.5</block>")));
        var floored = WoolSources.CheckMonumentSeats(centred, new SegmentIndex([(1, -2, -4, 0)]));
        await Assert.That((floored[0].X, floored[0].Y, floored[0].Z)).IsEqualTo((1, 1, -2));
        await Assert.That((floored[0].Clear, floored[0].Pedestal)).IsEqualTo((true, true));
        await Assert.That(floored[0].Severity).IsEqualTo("ok");
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
