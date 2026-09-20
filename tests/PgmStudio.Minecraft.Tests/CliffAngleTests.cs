using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft.Painting;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Where a board stops calling its ground a meadow (<see cref="Materials.CliffAngle"/>), and which of a slope
/// stack's bands therefore paint ground a prop may stand on (<see cref="Materials.Resting"/>).
///
/// <para>A surface graded by slope states the boundary itself: the band that paints the steepest ground is its
/// cliff, and where that band begins is the answer. Everything here is read off band thicknesses, which on the
/// slope axis are spans of degrees.</para>
/// </summary>
public sealed class CliffAngleTests
{
    private const int Grass = Palette.Blocks.Grass;
    private const int Dirt = Palette.Blocks.Dirt;
    private const int Stone = Palette.Blocks.Stone;
    private const int Sand = Palette.Blocks.Sand;

    private static LayeredMaterial Graded(BandEnding ending, params Band[] bands) =>
        new(new BandStack(bands, ending), BandAxis.Slope);

    /// <summary>The corpus shape: meadow, a transition, then bare rock taking everything steeper. The rock
    /// band begins at 30°, which is where the board stopped calling the ground a meadow.</summary>
    [Test]
    public async Task A_repeating_stack_calls_its_last_band_the_face()
    {
        var surface = Graded(BandEnding.Repeat,
            new Band(new SolidMaterial(Grass), 16),
            new Band(new SolidMaterial(Dirt), 14),
            new Band(new SolidMaterial(Stone), 60));

        await Assert.That(Materials.CliffAngle(surface)).IsEqualTo(30);
    }

    /// <summary>A stack that hands over rather than repeating puts the face in whatever is under it, so the
    /// cliff begins where the bands run out.</summary>
    [Test]
    public async Task A_handing_over_stack_calls_its_beyond_the_face()
    {
        var surface = Graded(BandEnding.HandOver,
            new Band(new SolidMaterial(Grass), 20),
            new Band(new SolidMaterial(Dirt), 15));

        await Assert.That(Materials.CliffAngle(surface with { Beyond = new SolidMaterial(Stone) })).IsEqualTo(35);
    }

    /// <summary>A handing-over stack whose own bands already reach the steepest ground never reaches its
    /// beyond, so the band spanning that ground is the face — not the angle the bands happen to sum to.</summary>
    [Test]
    public async Task A_stack_whose_bands_cover_the_steepest_ground_is_read_at_the_band_that_does()
    {
        var surface = Graded(BandEnding.HandOver,
            new Band(new SolidMaterial(Grass), 25),
            new Band(new SolidMaterial(Stone), 65));

        await Assert.That(SurfaceGradient.Steepest).IsEqualTo(89);
        await Assert.That(Materials.CliffAngle(surface)).IsEqualTo(25);
    }

    /// <summary>Ground graded by nothing states no angle at all and is read at the corpus median.</summary>
    [Test]
    public async Task Ground_that_grades_by_nothing_takes_the_stated_angle()
    {
        await Assert.That(Materials.CliffAngle(new SolidMaterial(Grass))).IsEqualTo(Materials.DefaultCliffAngle);
        await Assert.That(Materials.CliffAngle(null)).IsEqualTo(Materials.DefaultCliffAngle);
        await Assert.That(Materials.DefaultCliffAngle).IsEqualTo(30);
    }

    /// <summary>A surface is commonly a pattern with the grading inside it, so the grading is looked for
    /// through the tree rather than only at its root.</summary>
    [Test]
    public async Task Grading_nested_inside_a_pattern_is_still_the_boards_answer()
    {
        var graded = Graded(BandEnding.Repeat,
            new Band(new SolidMaterial(Grass), 18),
            new Band(new SolidMaterial(Stone), 72));
        var scattered = new CellMaterial(1, 8, 40, 4, [graded, new SolidMaterial(Sand)]);

        await Assert.That(Materials.CliffAngle(scattered)).IsEqualTo(18);
    }

    /// <summary>The bands a prop may stand on are the ones under the cliff — the meadow and the graded band
    /// between it and the face, which is the author's ruling. The face's own blocks are not ground anything
    /// rests on and are not what a rock standing below it is read against.</summary>
    [Test]
    public async Task Resting_ground_is_every_band_under_the_face()
    {
        var surface = Graded(BandEnding.Repeat,
            new Band(new SolidMaterial(Grass), 16),
            new Band(new SolidMaterial(Dirt), 14),
            new Band(new SolidMaterial(Stone), 60));

        await Assert.That(Materials.Resting(surface).Select(block => block.Id))
            .IsEquivalentTo(new[] { Grass, Dirt });
        // And the whole-tree walk still answers all three, which is what a question about the paint asks.
        await Assert.That(Materials.BlocksOf(surface).Select(block => block.Id))
            .IsEquivalentTo(new[] { Grass, Dirt, Stone });
    }

    /// <summary>A two-band stack has one band under its face, so the meadow is the whole of the ground a prop
    /// can stand on.</summary>
    [Test]
    public async Task A_two_band_stack_rests_only_on_its_meadow()
    {
        var surface = Graded(BandEnding.Repeat,
            new Band(new SolidMaterial(Grass), 18),
            new Band(new SolidMaterial(Stone), 72));

        await Assert.That(Materials.Resting(surface).Select(block => block.Id)).IsEquivalentTo(new[] { Grass });
    }

    /// <summary>One region is no grading. A single band painting every angle alike says nothing about where a
    /// face begins — calling its own start the cliff would make every cell of the board one — so it is read at
    /// the stated angle like any ground that grades by nothing, and every cell of it is ground.</summary>
    [Test]
    [Arguments(90)]
    [Arguments(40)]
    public async Task A_single_band_grades_nothing_whatever_it_spans(int span)
    {
        var surface = Graded(BandEnding.Repeat, new Band(new SolidMaterial(Stone), span));

        await Assert.That(Materials.CliffAngle(surface)).IsEqualTo(Materials.DefaultCliffAngle);
        await Assert.That(Materials.Resting(surface).Select(block => block.Id)).IsEquivalentTo(new[] { Stone });
    }

    /// <summary>One band that hands over is two regions and does grade: the band, then whatever is under the
    /// stack from where it runs out.</summary>
    [Test]
    public async Task A_single_band_that_hands_over_is_a_grading()
    {
        var surface = Graded(BandEnding.HandOver, new Band(new SolidMaterial(Grass), 22));

        await Assert.That(Materials.CliffAngle(surface with { Beyond = new SolidMaterial(Stone) })).IsEqualTo(22);
    }
}
