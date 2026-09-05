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

    /// <summary>
    /// <b>A band that states its depth and no material is caught where the layout is stored.</b> The three
    /// pair-carrying members are value types, so an entry naming only its number binds with an empty material
    /// instead of refusing to bind — which is what a bare list of materials handed to a voronoi's
    /// <c>bands</c>, by analogy with a noise's <c>stops</c>, produces one of per entry.
    /// </summary>
    [Test]
    public async Task A_band_carrying_no_material_is_named_where_it_sits()
    {
        var voronoi = new VoronoiMaterial(1, 7,
            [new VoronoiBand(new SolidMaterial(Blocks.Stone), 1), default]);

        var findings = TerrainThemeValidation.Check(TerrainTheme.Default with { Fill = voronoi });

        await Assert.That(findings.Single().Rule).IsEqualTo(TerrainThemeRules.MaterialMissing);
        await Assert.That(findings.Single().Field).IsEqualTo("fill.bands[1]");
    }

    /// <summary>The same gap in every member that holds a material, named by the path to it — a stack's band,
    /// a wall run's stripe and the two sides of a checker, nested as deep as the pattern goes.</summary>
    [Test]
    public async Task Every_member_that_holds_a_material_is_walked()
    {
        var stack = new LayeredMaterial(new BandStack([new Band(new SolidMaterial(Blocks.Stone), 1), default]));
        await Assert.That(TerrainThemeValidation.Check(TerrainTheme.Default with { Fill = stack })
            .Single().Field).IsEqualTo("fill.stack[1]");

        var run = new WallRunMaterial([new WallStripe(new SolidMaterial(Blocks.Stone), 2), default]);
        await Assert.That(TerrainThemeValidation.Check(TerrainTheme.Default with { Fill = run })
            .Single().Field).IsEqualTo("fill.runs[1]");

        // Nested: the checker's odd side is a voronoi whose second band is empty.
        var nested = new CheckerMaterial(4, new SolidMaterial(Blocks.Stone),
            new VoronoiMaterial(1, 7, [new VoronoiBand(new SolidMaterial(Blocks.Stone), 1), default]));
        await Assert.That(TerrainThemeValidation.Check(TerrainTheme.Default with { Fill = nested })
            .Single().Field).IsEqualTo("fill.odd.bands[1]");
    }

    /// <summary>A theme whose every member carries its material says nothing, so the gate does not fire on the
    /// finishes the boards already ship.</summary>
    [Test]
    public async Task A_pattern_that_carries_every_material_is_silent()
    {
        var voronoi = new VoronoiMaterial(1, 7,
            [new VoronoiBand(new SolidMaterial(Blocks.Stone), 1),
             new VoronoiBand(new SolidMaterial(Blocks.Cobblestone), 2)]);
        await Assert.That(TerrainThemeValidation.Check(TerrainTheme.Default with { Fill = voronoi })).IsEmpty();
    }

    /// <summary>Only the depth axis measures courses. An angle mask's bands are spans of degrees, so a band
    /// twenty wide carrying the standard grass-over-two-dirt stack is a one-course surface on every cell it
    /// claims — reading its number as a depth would call the meadow twenty courses of grass.</summary>
    [Test]
    public async Task A_bands_number_is_only_a_depth_on_the_depth_axis()
    {
        var meadow = new LayeredMaterial(new BandStack(
            [new Band(new SolidMaterial(Blocks.Grass), 1), new Band(new SolidMaterial(Blocks.Dirt), 2)]));
        var mask = new LayeredMaterial(new BandStack(
            [new Band(meadow, 20), new Band(new SolidMaterial(Blocks.Cobblestone), 70)]), BandAxis.Slope);

        await Assert.That(TerrainThemeValidation.Check(Surfaced(mask))).IsEmpty();

        // The recursion still names a band that genuinely buries its surfacing block: bare grass on the
        // shallow side fills all three courses of the bucket, mask or no mask.
        var bare = new LayeredMaterial(new BandStack(
            [new Band(new SolidMaterial(Blocks.Grass), 20),
             new Band(new SolidMaterial(Blocks.Cobblestone), 70)]), BandAxis.Slope);
        await Assert.That(TerrainThemeValidation.Check(Surfaced(bare)).Single().Rule)
                    .IsEqualTo(TerrainThemeRules.SurfaceBlockBuried);
    }
}
