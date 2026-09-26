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

        bool BridgeableAt(Editability.Result res, int x, int z) => res.Bridgeable((z - res.MinZ) * res.Width + (x - res.MinX));
        await Assert.That(BridgeableAt(with, 8, 8)).IsFalse()
            .Because("a bridge is placed, and only breaking is permitted out there");
        await Assert.That(BridgeableAt(with, 2, 2)).IsTrue().Because("inside the drawn rectangle");
        await Assert.That(with.BridgeableCells().All(cell => cell is { X: >= 0 and < 4, Z: >= 0 and < 4 })).IsTrue();
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

    private static bool BridgesAt(Editability.Result res, int x, int z) => res.Bridgeable((z - res.MinZ) * res.Width + (x - res.MinX));
    private static string ZoneOf(Editability.Result res, int x, int z) => res.ZoneAt((z - res.MinZ) * res.Width + (x - res.MinX));

    /// <summary><b>A complement starting from everywhere states the build area as its hole.</b> The void rule
    /// covers everywhere but the rectangle, so the rectangle is the grant — the idiom 134 corpus maps write.</summary>
    [Test]
    public async Task A_complement_of_everywhere_grants_the_area_it_leaves_out()
    {
        var doc = Doc("""
            <regions>
              <complement id="void-area"><everywhere/><rectangle min="0,0" max="4,4"/></complement>
              <apply block="deny(void)" region="void-area"/>
            </regions>
            """);
        var res = Editability.Compute(doc, [], (-8, -8, 12, 12));
        await Assert.That(ZoneOf(res, 2, 2)).IsEqualTo(EditZone.BuildZone).Because("the rectangle the complement leaves out");
        await Assert.That(ZoneOf(res, 8, 8)).IsEqualTo(EditZone.Sealed).Because("void the rule covers");
        await Assert.That(BridgesAt(res, 2, 2)).IsTrue();
    }

    /// <summary><b>A map-wide void rule lets a player build across a marked column and nowhere else.</b> PGM's
    /// void filter reads the block at y=0, a block-36 marker included, so a column carrying one is not void
    /// and the rule passes it; an empty column is void and refused. The marked column stays ground on the
    /// zone map — the author drew no zone — while the walk may bridge it; a column of ground, not void
    /// either, is stood on and not bridged.</summary>
    [Test]
    public async Task A_map_wide_void_rule_bridges_a_marked_column_and_refuses_an_empty_one()
    {
        var doc = Doc("""
            <regions>
              <rectangle id="board" min="0,0" max="8,8"/>
              <apply block="deny(void)"/>
            </regions>
            """);
        var marked = new HashSet<(int, int)> { (2, 2) };
        var y0 = new HashSet<(int, int)> { (2, 2), (3, 3) };
        var res = Editability.Compute(doc, y0, (-4, -4, 12, 12), floorMarks: marked);
        await Assert.That(BridgesAt(res, 2, 2)).IsTrue().Because("a y=0 marker makes the column not void");
        await Assert.That(BridgesAt(res, 3, 3)).IsFalse().Because("ground is stood on, not bridged");
        await Assert.That(BridgesAt(res, 5, 5)).IsFalse().Because("an empty column is void");
        await Assert.That(ZoneOf(res, 2, 2)).IsEqualTo(EditZone.Ground);
    }

    /// <summary><b>A rule over a height bounds no column.</b> <c>&lt;below y="7"/&gt;</c> is a half-space in
    /// y: it stops building at the bottom of the world and leaves every column open at the height a bridge is
    /// laid, so it may not seal the board the way a region read as everywhere would.</summary>
    [Test]
    public async Task A_rule_over_a_height_alone_seals_no_column()
    {
        var doc = Doc("""
            <regions>
              <rectangle id="build-area" min="0,0" max="4,4"/>
              <negative id="not-build-area"><region id="build-area"/></negative>
              <apply block="deny(void)" region="not-build-area"/>
              <apply block="never"><region><below y="7"/></region></apply>
            </regions>
            """);
        var res = Editability.Compute(doc, [], (-8, -8, 12, 12));
        await Assert.That(ZoneOf(res, 2, 2)).IsEqualTo(EditZone.BuildZone);
        await Assert.That(BridgesAt(res, 2, 2)).IsTrue();
    }

    /// <summary><b>An x bound cuts the footprint.</b> <c>&lt;above x="0"/&gt;</c> is the half of the board at
    /// or past x=0, not everything over a height.</summary>
    [Test]
    public async Task An_above_on_x_covers_only_its_half()
    {
        var doc = Doc("""
            <regions>
              <above id="east" x="0"/>
              <apply block="never" region="east"/>
            </regions>
            """);
        var res = Editability.Compute(doc, AllSolid(Doc("""<regions><rectangle id="r" min="-8,-8" max="8,8"/></regions>""")), (-8, -8, 8, 8));
        await Assert.That(ZoneOf(res, 4, 0)).IsEqualTo(EditZone.Sealed).Because("east of x=0");
        await Assert.That(ZoneOf(res, -4, 0)).IsNotEqualTo(EditZone.Sealed).Because("west of x=0");
    }

    /// <summary><b>A deny that cannot match a placement lets it through.</b> <c>&lt;deny&gt;</c> answers only
    /// where its filter matches, and a player placing a block is never the world forming ice, so a map-wide
    /// rule over that filter leaves every column to the rules after it — here the void rule, which refuses the
    /// void it covers and grants the lanes it leaves out.</summary>
    [Test]
    public async Task A_deny_no_placement_matches_abstains_and_the_next_rule_decides()
    {
        var doc = Doc("""
            <filters>
              <deny id="deny-freezing"><all><cause>world</cause><material>ice</material></all></deny>
            </filters>
            <regions>
              <rectangle id="lane" min="0,0" max="4,4"/>
              <negative id="void-area"><region id="lane"/></negative>
              <apply block-place="deny-freezing"/>
              <apply block-place="deny(void)" region="void-area"/>
            </regions>
            """);
        var res = Editability.Compute(doc, [], (-8, -8, 12, 12));
        await Assert.That(BridgesAt(res, 8, 8)).IsFalse().Because("the void rule refuses the void outside the lane");
        await Assert.That(BridgesAt(res, 2, 2)).IsTrue().Because("the lane the void rule leaves out");
    }

    /// <summary><b>A deny that matches every placement refuses it.</b> A player's placement is the
    /// <c>player</c> cause, so denying that cause is a denial, not a condition.</summary>
    [Test]
    public async Task A_deny_of_the_player_cause_refuses_editing()
    {
        var doc = Doc("""
            <filters>
              <deny id="no-players"><cause>player</cause></deny>
            </filters>
            <regions>
              <apply block="no-players"/>
            </regions>
            """);
        var res = Editability.Compute(doc, [], (-8, -8, 12, 12));
        await Assert.That(BridgesAt(res, 2, 2)).IsFalse();
        await Assert.That(ZoneOf(res, 2, 2)).IsEqualTo(EditZone.Sealed);
    }

    /// <summary><b>A never rule over everything but an area grants that area.</b> Forbidding building
    /// everywhere outside the build area is how most DTC/M maps state it — <c>block="never"</c> over the
    /// <c>negative</c> of the area — and in PGM the area it leaves out is open, over the void too.</summary>
    [Test]
    public async Task A_never_rule_over_a_negative_grants_the_area_it_leaves_out()
    {
        var doc = Doc("""
            <regions>
              <rectangle id="build-area" min="0,0" max="4,4"/>
              <negative id="not-build-area"><region id="build-area"/></negative>
              <apply block="never" region="not-build-area"/>
            </regions>
            """);
        var res = Editability.Compute(doc, [], (-8, -8, 12, 12));
        await Assert.That(ZoneOf(res, 2, 2)).IsEqualTo(EditZone.BuildZone);
        await Assert.That(BridgesAt(res, 2, 2)).IsTrue().Because("the build area, void or not");
        await Assert.That(BridgesAt(res, 8, 8)).IsFalse().Because("the rule refuses everything else");
    }

    /// <summary><b>A rule over <c>everywhere</c> covers the map although no region of that id is defined.</b>
    /// PGM registers <c>everywhere</c> and <c>nowhere</c> as built-in regions, and a map refers to them by id;
    /// agrostid's void rule is written that way, over the water it lays at y=0 to mark its build area.</summary>
    [Test]
    public async Task A_rule_over_the_builtin_everywhere_covers_the_map()
    {
        var doc = Doc("""
            <filters>
              <not id="not-void"><void/></not>
            </filters>
            <regions>
              <apply block-place="not-void" region="everywhere"/>
            </regions>
            """);
        var marked = new HashSet<(int, int)> { (2, 2) };
        var res = Editability.Compute(doc, marked, (-4, -4, 12, 12), floorMarks: marked);
        await Assert.That(BridgesAt(res, 2, 2)).IsTrue().Because("the void rule passes a floor mark and it may be built across");
        await Assert.That(BridgesAt(res, 6, 6)).IsFalse().Because("the void rule refuses an empty column");
    }
}
