using PgmStudio.Analysis.Playability;
using PgmStudio.Pgm;
using PgmStudio.Vocabulary;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// Whether a goal can be completed where it stands, under the void rule a studio map writes: the place side
/// refuses the void, the break side lets leaves and logs through over it. A goal over a column with nothing at
/// y=0 is refused its own change; the same goal over a marked column is not.
/// </summary>
public sealed class GoalEditsTests
{
    private static Dictionary<string, object?> Doc(string goals) => Serializer.ToDict(MapParser.ParseXmlString($"""
        <?xml version="1.0"?>
        <map proto="1.4.0">
          <name>b</name><version>1</version><objective>o</objective>
          <teams><team id="red" color="red" max="8">Red</team><team id="blue" color="blue" max="8">Blue</team></teams>
          <filters>
            <not id="no-void"><void/></not>
            <any id="over-void-breakable">
              <all><any><material>leaves</material><material>log</material></any><void/></all>
              <filter id="no-void"/>
            </any>
          </filters>
          <regions>
            <cuboid id="red-core-region" min="0,40,0" max="3,43,3"/>
            <cuboid id="blue-monument-region" min="10,40,10" max="11,41,11"/>
            <apply block-place="no-void" block-break="over-void-breakable"/>
          </regions>
          {goals}
        </map>
        """));

    private const string CoreAndMonument = """
        <cores><core id="red-core" team="red" region="red-core-region"/></cores>
        <destroyables><destroyable id="blue-monument" name="M" owner="blue" materials="obsidian" region="blue-monument-region"/></destroyables>
        """;

    private static Editability.Result Zones(Dictionary<string, object?> doc, IEnumerable<(int, int)> y0) =>
        Editability.Compute(doc, [.. y0], (-8, -8, 20, 20));

    private static IEnumerable<(int, int)> Square(int x0, int z0, int x1, int z1)
    {
        for (var x = x0; x < x1; x++)
            for (var z = z0; z < z1; z++)
                yield return (x, z);
    }

    [Test]
    public async Task A_goal_over_the_void_is_refused_its_own_block_and_its_lava()
    {
        var doc = Doc(CoreAndMonument);
        var findings = GoalEdits.Check(doc, Zones(doc, []));

        await Assert.That(findings.Count(f => f.Rule == EditZoneRules.SealedGoal)).IsEqualTo(2)
            .Because("obsidian is not a material the break side lets through over the void, for the core or the monument");
        await Assert.That(findings.Count(f => f.Rule == EditZoneRules.HeldLava)).IsEqualTo(1)
            .Because("lava flowing is a block placed, and the place side refuses every void column round the core");
    }

    [Test]
    public async Task A_goal_over_marked_columns_can_be_completed()
    {
        var doc = Doc(CoreAndMonument);
        var findings = GoalEdits.Check(doc, Zones(doc, Square(-2, -2, 14, 14)));

        await Assert.That(findings).IsEmpty();
    }

    [Test]
    public async Task One_marked_column_beside_a_core_lets_its_lava_out()
    {
        var doc = Doc(CoreAndMonument);
        var findings = GoalEdits.Check(doc, Zones(doc, [.. Square(0, 0, 3, 3), (3, 1), .. Square(10, 10, 11, 11)]));

        await Assert.That(findings.Any(f => f.Rule == EditZoneRules.HeldLava)).IsFalse();
        await Assert.That(findings).IsEmpty();
    }

    [Test]
    public async Task A_wool_monument_over_the_void_is_not_asked()
    {
        var doc = Doc("""
            <wools><wool team="red" color="lime" location="5.5,40,5.5"><monument><block>-5,40,-5</block></monument></wool></wools>
            """);
        await Assert.That(GoalEdits.Check(doc, Zones(doc, []))).IsEmpty()
            .Because("PGM's wool module lets the right team's wool onto its monument whatever the block rules said");
    }

    [Test]
    public async Task Without_a_y0_layer_nothing_is_said()
    {
        var doc = Doc(CoreAndMonument);
        await Assert.That(GoalEdits.Check(doc, Editability.Compute(doc, null, (-8, -8, 20, 20)))).IsEmpty();
    }
}
