using PgmStudio.Analysis.Playability;
using PgmStudio.Analysis.Region;
using PgmStudio.Pgm;
using PgmStudio.Vocabulary;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// What the editability pass says about a column, on synthetic maps. The corpus-wide net — the zone grid of
/// every real map digested and compared — is <c>tools/PgmStudio.RoundTrip --goldens</c>.
///
/// <para>Every case here is a shape the corpus actually writes, because the pass exists to read PGM's own
/// resolution rather than an approximation of it: the first application that does not abstain settles the
/// edit, place and break are separate scopes, and a void filter is a question about the column.</para>
/// </summary>
public sealed class EditabilityTests
{
    private static Dictionary<string, object?> Doc(string body) => Serializer.ToDict(MapParser.ParseXmlString($"""
        <?xml version="1.0"?>
        <map proto="1.4.0">
          <name>b</name><version>1</version><objective>o</objective>
          {body}
        </map>
        """));

    /// <summary>Every column solid, so no void filter can bite and only the stated rules act.</summary>
    private static HashSet<(int, int)> AllSolid(Dictionary<string, object?> doc)
    {
        var bbox = Editability.RegionBbox(doc, 16);
        var y0 = new HashSet<(int, int)>();
        for (var x = bbox.minX; x < bbox.maxX; x++)
            for (var z = bbox.minZ; z < bbox.maxZ; z++)
                y0.Add((x, z));
        return y0;
    }

    private const string NeverArena = """
        <regions>
          <cuboid id="arena" min="0,0,0" max="10,10,10"/>
          <apply region="arena" block="never"/>
        </regions>
        """;

    [Test]
    public async Task A_never_rule_seals_only_the_cells_inside_its_region()
    {
        var doc = Doc(NeverArena);
        var res = Editability.Compute(doc, AllSolid(doc));

        // box(0,0,10,10) strictly contains the 10×10 block of cell centres 0.5..9.5
        await Assert.That(res.Counts[EditZone.Sealed]).IsEqualTo(100);
        await Assert.That(res.Counts[EditZone.Ground]).IsEqualTo(res.Width * res.Height - 100);
        await Assert.That(res.Counts[EditZone.BuildZone]).IsEqualTo(0)
            .Because("nothing here grants a zone — the rest is editable because nothing forbids it");
        await Assert.That(res.HasY0).IsTrue();
    }

    /// <summary><b>A build zone is stated as its own complement, and reads back as the grant it is.</b> The
    /// void rule covers everywhere the zone is not, so the ground it does not cover is what the author drew.
    /// Outside it, a column with a block at y=0 is still editable — that is PGM's own default and the whole
    /// of what <c>&lt;void/&gt;</c> tests — and a column without one is sealed.</summary>
    [Test]
    public async Task A_void_rule_tells_the_zone_the_author_drew_from_the_ground_that_qualifies_itself()
    {
        var doc = Doc("""
            <filters><not id="no-void"><void/></not></filters>
            <regions>
              <rectangle id="build-area" min="0,0" max="4,4"/>
              <negative id="not-build-area"><region id="build-area"/></negative>
              <apply region="not-build-area" block="no-void"/>
            </regions>
            """);
        // Solid ground over x 0..7 only; everything else is open void.
        var y0 = new HashSet<(int, int)>();
        for (var x = 0; x < 8; x++) for (var z = 0; z < 8; z++) y0.Add((x, z));

        var res = Editability.Compute(doc, y0, (-4, -4, 12, 12));
        string ZoneAt(int x, int z) => res.ZoneAt((z - res.MinZ) * res.Width + (x - res.MinX));

        await Assert.That(ZoneAt(2, 2)).IsEqualTo(EditZone.BuildZone).Because("inside the drawn rectangle");
        await Assert.That(ZoneAt(6, 6)).IsEqualTo(EditZone.Ground).Because("outside it, and a block at y=0");
        await Assert.That(ZoneAt(10, 10)).IsEqualTo(EditZone.Sealed).Because("outside it, and open void");
    }

    /// <summary><b>PGM stops at the first application that answers, so the studio does too.</b> Two rules
    /// over one column is the ordinary case — a wool room's material filter inside a board-wide deny — and
    /// the room's rule comes first in document order, so PGM never reaches the deny for the columns the room
    /// covers. Resolving by the last rule instead reports the blanket rule's verdict everywhere and erases
    /// every permission an author carved inside it.</summary>
    [Test]
    public async Task The_first_rule_that_answers_settles_a_column_two_rules_cover()
    {
        var doc = Doc("""
            <filters><material id="only-web">web</material></filters>
            <regions>
              <rectangle id="room" min="0,0" max="4,4"/>
              <apply region="room" block="only-web"/>
              <apply block="never"/>
            </regions>
            """);
        var res = Editability.Compute(doc, AllSolid(Doc("<regions><rectangle id=\"r\" min=\"0,0\" max=\"4,4\"/></regions>")), (-4, -4, 12, 12));
        string ZoneAt(int x, int z) => res.ZoneAt((z - res.MinZ) * res.Width + (x - res.MinX));

        await Assert.That(ZoneAt(2, 2)).IsEqualTo(EditZone.Filtered)
            .Because("the room's material filter answers first, and it permits somebody");
        await Assert.That(ZoneAt(8, 8)).IsEqualTo(EditZone.Sealed)
            .Because("out there the blanket deny is the first rule to answer");
    }

    /// <summary><b>Place and break are two scopes, and the corpus break filter carves an exception in one of
    /// them.</b> A canopy hanging over the void outside every build zone cannot be built on and — where the
    /// map states the exception — can still be cut down, so the column is a permission rather than a
    /// refusal. Reading the pair as one filter loses the exception entirely, which is what leaves a tree
    /// nobody can remove.</summary>
    [Test]
    public async Task A_break_exception_over_the_void_leaves_the_column_editable()
    {
        const string filters = """
            <filters>
              <not id="no-void"><void/></not>
              <any id="over-void-breakable">
                <all><any><material>leaves</material><material>log</material></any><void/></all>
                <filter id="no-void"/>
              </any>
            </filters>
            """;
        const string regions = """
            <regions>
              <rectangle id="build-area" min="0,0" max="4,4"/>
              <negative id="not-build-area"><region id="build-area"/></negative>
            """;

        var without = Editability.Compute(
            Doc($"{filters}{regions}<apply region=\"not-build-area\" block=\"no-void\"/></regions>"),
            [], (-4, -4, 12, 12));
        var with = Editability.Compute(
            Doc($"{filters}{regions}<apply region=\"not-build-area\" block-place=\"no-void\" block-break=\"over-void-breakable\"/></regions>"),
            [], (-4, -4, 12, 12));

        string ZoneAt(Editability.Result res, int x, int z) => res.ZoneAt((z - res.MinZ) * res.Width + (x - res.MinX));

        await Assert.That(ZoneAt(without, 8, 8)).IsEqualTo(EditZone.Sealed)
            .Because("one filter over both scopes denies the whole column");
        await Assert.That(ZoneAt(with, 8, 8)).IsEqualTo(EditZone.Filtered)
            .Because("placing is denied and breaking a canopy is not, so somebody can edit it");
    }

    /// <summary>Without a scan there is no y=0 layer, so a void filter has nothing to read. The pass says so
    /// rather than answering as if the whole board were solid.</summary>
    [Test]
    public async Task No_scanned_layer_is_reported_rather_than_assumed()
    {
        var res = Editability.Compute(Doc(NeverArena), null);

        await Assert.That(res.HasY0).IsFalse();
        await Assert.That(res.Counts[EditZone.Sealed]).IsEqualTo(100).Because("a never rule needs no terrain");
    }

    [Test]
    public async Task Region_geometry_contains_matches_footprint()
    {
        var doc = Doc(NeverArena);
        var regions = (Dictionary<string, object?>)doc["regions"]!;
        var arena = (Dictionary<string, object?>)regions["arena"]!;
        var geom = RegionGeometry2d.ToGeometry(arena, (-100, -100, 100, 100), regions);

        await Assert.That(geom).IsNotNull();
        await Assert.That(geom!.Contains(new NetTopologySuite.Geometries.Point(5, 5))).IsTrue();
        await Assert.That(geom.Contains(new NetTopologySuite.Geometries.Point(50, 50))).IsFalse();
    }
}
