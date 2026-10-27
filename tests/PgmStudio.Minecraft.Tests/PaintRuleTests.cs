using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// What a board's paint rules guarantee: the first rule a column meets lays its material over that many top
/// courses, every condition a rule states must hold, a column no rule meets keeps its theme, and a covered column
/// or bedrock is never laid on.
/// </summary>
public sealed class PaintRuleTests
{
    private const int Rock = 4, Sand = 12, Gravel = 13;

    private static ColumnProfile Column(int top, int slope, bool covered = false) =>
        new(top, VoidEdge: false, OpenEdge: false, ClosedEdge: false, VoidDrop: -1, TerrainDrop: -1, Slope: slope,
            Covered: covered);

    private static readonly TerrainTheme Theme = TerrainTheme.Default with
    {
        Surface = new TopBand(new SolidMaterial(Blocks.Grass), Depth: 1),
        Fill = new SolidMaterial(Blocks.Dirt),
    };

    private static int[] Laid(ColumnProfile column, PaintRule? rule) =>
        [.. TerrainPainter.ColumnBlocks(0, 0, column, Theme, rule: rule).Select(block => block.Id)];

    private static PaintRule? First(ColumnProfile column, params PaintRule[] rules) =>
        PaintRule.First(rules, (0, 0), column, (0, 0));

    [Test]
    public async Task The_first_rule_a_column_meets_lays_and_a_later_one_never_does()
    {
        PaintRule[] rules = [new(new SolidMaterial(Rock), SlopeAbove: 40), new(new SolidMaterial(Sand))];

        await Assert.That(First(Column(20, 50), rules)!.Material).IsEqualTo(new SolidMaterial(Rock));
        await Assert.That(First(Column(20, 10), rules)!.Material).IsEqualTo(new SolidMaterial(Sand));
        await Assert.That(First(Column(20, 40), rules[0])).IsNull().Because("steeper than 40, not 40 itself");
    }

    [Test]
    public async Task A_rule_lays_its_depth_of_top_courses_and_the_theme_paints_under_it()
    {
        var blocks = Laid(Column(20, 0), new PaintRule(new SolidMaterial(Sand), Depth: 2));

        await Assert.That(blocks[0]).IsEqualTo(Sand);
        await Assert.That(blocks[1]).IsEqualTo(Sand);
        await Assert.That(blocks[2]).IsEqualTo(Blocks.Dirt);
        await Assert.That(Laid(Column(20, 0), null)[0]).IsEqualTo(Blocks.Grass).Because("no rule keeps the theme");
    }

    [Test]
    public async Task Every_condition_a_rule_states_must_hold()
    {
        var banded = new PaintRule(new SolidMaterial(Gravel), HeightFrom: 15, HeightTo: 25, SlopeUpTo: 30,
                                   Cells: new HashSet<(int X, int Z)> { (0, 0) });

        await Assert.That(First(Column(20, 10), banded)).IsNotNull();
        await Assert.That(First(Column(30, 10), banded)).IsNull().Because("its top course stands above 25");
        await Assert.That(First(Column(12, 10), banded)).IsNull().Because("its top course stands under 15");
        await Assert.That(First(Column(20, 45), banded)).IsNull().Because("it is steeper than 30");
        await Assert.That(PaintRule.First([banded], (1, 0), Column(20, 10), (1, 0))).IsNull()
            .Because("the cell is not among the ones the rule may lay on");
    }

    [Test]
    public async Task A_covered_column_takes_no_rule_and_bedrock_is_never_laid_on()
    {
        PaintRule[] everywhere = [new(new SolidMaterial(Sand), Depth: 64)];

        await Assert.That(First(Column(20, 0, covered: true), everywhere)).IsNull();
        var blocks = Laid(Column(20, 0), everywhere[0]);
        await Assert.That(blocks[^1]).IsEqualTo(Blocks.Bedrock);
    }

    [Test]
    public async Task Noise_lays_patches_and_reads_the_same_at_the_same_sample()
    {
        var patchy = new PaintRule(new SolidMaterial(Sand), Noise: new PaintNoise(6, 0.5, 3));
        var laid = 0;
        for (var x = 0; x < 40; x++)
            for (var z = 0; z < 40; z++)
                if (PaintRule.First([patchy], (x, z), Column(20, 0), (x, z)) is not null) laid++;

        await Assert.That(laid).IsGreaterThan(200).And.IsLessThan(1400).Because("about half the ground, in patches");
        await Assert.That(PaintRule.First([patchy], (5, 5), Column(20, 0), (30, 30)) is null)
            .IsEqualTo(PaintRule.First([patchy], (30, 30), Column(20, 0), (30, 30)) is null)
            .Because("the noise is read at the sample, so a mirrored cell meets the rule its image does");
    }
}
