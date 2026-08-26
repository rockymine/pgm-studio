using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// <c>PT1</c> — a block that surfaces ground, painted below the course it surfaces.
///
/// <para>A bucket carries a depth and a material carries none, so a material that is a <b>pick</b> — a cell, a
/// voronoi, a noise field — writes whichever block it picked into every course the bucket claims. A block
/// whose whole meaning is "this is the top" repeated three courses down is ground made of its own skin, and
/// it is the single most repeated authoring mistake in this repository (author, 2026-08-14).</para>
/// </summary>
public sealed class TerrainThemeValidationTests
{
    private static TerrainTheme Surfaced(TerrainMaterial material, int depth = 3) =>
        TerrainTheme.Default with { Surface = new TopBand(material, depth) };

    /// <summary>The standard stack: grass at one course over two of dirt. The shape every other case is
    /// measured against, and the answer the finding points an author at.</summary>
    [Test]
    public async Task Grass_at_one_course_over_dirt_is_the_clean_reference()
    {
        var layered = new LayeredMaterial(new BandStack(
            [new Band(new SolidMaterial(Blocks.Grass), 1), new Band(new SolidMaterial(Blocks.Dirt), 2)]));
        await Assert.That(TerrainThemeValidation.Check(Surfaced(layered))).IsEmpty();
        await Assert.That(TerrainThemeValidation.Check(TerrainTheme.Default)).IsEmpty();
    }

    /// <summary>`corvid-hollow`'s `rookwood`, `sable-marsh`'s `sable-reeds`, `sonnet-briarlock`'s `briarlock`
    /// and `tallow-weirgate`'s `weir-silt` — probed at `(−55, −5)` on Corvid and `(30, −35)` on Weirgate, grass
    /// at all three courses in each.</summary>
    [Test]
    public async Task A_cell_holding_grass_over_a_three_course_surface_is_refused()
    {
        var cell = new CellMaterial(1, 6, 40, 2,
            [new SolidMaterial(Blocks.Grass), new SolidMaterial(Blocks.Dirt)]);
        var findings = TerrainThemeValidation.Check(Surfaced(cell));
        await Assert.That(findings.Single().Rule).IsEqualTo(TerrainThemeRules.SurfaceBlockBuried);
        await Assert.That(findings.Single().Field).IsEqualTo("surface");
        await Assert.That(findings.Single().Refuses).IsTrue();
    }

    /// <summary>Podzol is dirt at data 2, so nothing that reads ids alone can tell it from the dirt under it.
    /// `opus5-ravensmere` and `opus5-rimegarth` carry it.</summary>
    [Test]
    public async Task Podzol_is_a_surfacing_block_though_it_shares_an_id_with_dirt()
    {
        var podzol = new CellMaterial(1, 6, 40, 2,
            [new SolidMaterial(Blocks.Dirt, 2), new SolidMaterial(Blocks.Dirt)]);
        await Assert.That(TerrainThemeValidation.Check(Surfaced(podzol))).IsNotEmpty();

        // plain dirt in the same pattern is ground rather than a skin over it, and says nothing
        var dirt = new CellMaterial(1, 6, 40, 2,
            [new SolidMaterial(Blocks.Dirt), new SolidMaterial(Blocks.Dirt, 1)]);
        await Assert.That(TerrainThemeValidation.Check(Surfaced(dirt))).IsEmpty();
    }

    /// <summary>A pick over a single course is what a one-block bucket is, so the same material is fine at a
    /// depth of one: the rim is exactly that.</summary>
    [Test]
    public async Task The_same_pick_over_one_course_says_nothing()
    {
        var cell = new CellMaterial(1, 6, 40, 2,
            [new SolidMaterial(Blocks.Grass), new SolidMaterial(Blocks.Dirt)]);
        await Assert.That(TerrainThemeValidation.Check(Surfaced(cell, depth: 1))).IsEmpty();
    }

    /// <summary>A stack is read band by band: a surfacing block is the top course and nothing else. Two courses
    /// of grass at the top is the same fault as grass underneath it.</summary>
    [Test]
    public async Task A_stack_may_carry_a_surfacing_block_only_as_its_top_course()
    {
        var thick = new LayeredMaterial(new BandStack(
            [new Band(new SolidMaterial(Blocks.Grass), 2), new Band(new SolidMaterial(Blocks.Dirt), 1)]));
        await Assert.That(TerrainThemeValidation.Check(Surfaced(thick))).IsNotEmpty();

        var buried = new LayeredMaterial(new BandStack(
            [new Band(new SolidMaterial(Blocks.Dirt), 1), new Band(new SolidMaterial(Blocks.Grass), 1)]));
        await Assert.That(TerrainThemeValidation.Check(Surfaced(buried))).IsNotEmpty();
    }

    /// <summary>The fill claims everything under the buckets above it, so a surfacing block there is buried by
    /// definition.</summary>
    [Test]
    public async Task The_fill_is_never_a_surfacing_block()
    {
        var theme = TerrainTheme.Default with { Fill = new SolidMaterial(Blocks.Grass) };
        var findings = TerrainThemeValidation.Check(theme);
        await Assert.That(findings.Single().Field).IsEqualTo("fill");
    }
}
