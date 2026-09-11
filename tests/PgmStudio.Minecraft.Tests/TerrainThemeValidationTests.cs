using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Vocabulary;

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
            [new VoronoiBand(new SolidMaterial(Blocks.Stone), 1), default], Rise: 3);

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
            new VoronoiMaterial(1, 7, [new VoronoiBand(new SolidMaterial(Blocks.Stone), 1), default], Rise: 3));
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
             new VoronoiBand(new SolidMaterial(Blocks.Cobblestone), 2)], Rise: 3);
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

    // ── PT3: a sampled pattern's brush against the blocks it paints ───────────────
    // A cell size or a field scale is the period a pattern varies over, in blocks. Below two it changes faster
    // than the ground can show it and no palette rescues it. The floor is the author's and it is a guard
    // against a pathological number: the committed themes sit at a median cellSize of 6 and scale of 8, so
    // every board on the shelf is silent here.

    [Test]
    public async Task A_pattern_at_the_floor_and_above_it_is_silent()
    {
        (int Id, int Data)[] pair = [(Blocks.Stone, 0), (Blocks.Cobblestone, 0)];
        TerrainMaterial[] stops = [new SolidMaterial(pair[0].Id), new SolidMaterial(pair[1].Id)];

        foreach (var period in (int[])[TerrainThemeRules.BrushFloor, 6, 19])
        {
            await Assert.That(Brushed(new CellMaterial(1, period, 40, 2, stops, Rise: 3))).IsEmpty();
            await Assert.That(Brushed(new NoiseMaterial(1, period, 3, stops, Rise: 3))).IsEmpty();
        }
    }

    /// <summary>Each of the five sampled patterns, at a period of one — every block its own feature. The
    /// field a finding names is the one the author edits, so a `cell` is pointed at `cellSize` and a `noise`
    /// at `scale`.</summary>
    [Test]
    public async Task Every_sampled_pattern_is_named_under_the_floor()
    {
        TerrainMaterial[] stops = [new SolidMaterial(Blocks.Stone), new SolidMaterial(Blocks.Cobblestone)];
        (TerrainMaterial Material, string Field)[] cases =
        [
            (new CellMaterial(1, 1, 40, 2, stops, Rise: 3), "cellSize"),
            (new VoronoiMaterial(1, 1, [new VoronoiBand(stops[0], 1), new VoronoiBand(stops[1], 2)], Rise: 3), "cellSize"),
            (new NoiseMaterial(1, 1, 3, stops, Rise: 3), "scale"),
            (new TurbulenceMaterial(1, 1, 3, stops, Rise: 3), "scale"),
            (new ElectricMaterial(1, 1, 3, stops, Rise: 3), "scale"),
        ];

        foreach (var (material, field) in cases)
        {
            var finding = Brushed(material).Single();
            await Assert.That(finding.Rule).IsEqualTo(TerrainThemeRules.BrushTooFine);
            await Assert.That(finding.Field).IsEqualTo($"fill.{field}");
        }
    }

    /// <summary>A field nested inside another pattern paints at its own scale, so the walk reaches it. The
    /// coarse voronoi around it is silent and the path names where the fine one actually sits.</summary>
    [Test]
    public async Task A_fine_field_nested_under_a_coarse_pattern_is_still_named()
    {
        TerrainMaterial[] stops = [new SolidMaterial(Blocks.Stone), new SolidMaterial(Blocks.Cobblestone)];
        var nested = new VoronoiMaterial(1, 8,
            [new VoronoiBand(new SolidMaterial(Blocks.Stone), 1),
             new VoronoiBand(new NoiseMaterial(2, 1, 3, stops, Rise: 3), 2)], Rise: 3);

        var finding = Brushed(nested).Single();
        await Assert.That(finding.Rule).IsEqualTo(TerrainThemeRules.BrushTooFine);
        await Assert.That(finding.Field).IsEqualTo("fill.bands[1].scale");
    }

    /// <summary>A checker and a wall run are drawn rather than sampled — an author states the square and the
    /// stripe at the width they meant — so neither is asked, and a one-block checker stands.</summary>
    [Test]
    public async Task A_drawn_pattern_is_not_asked_for_a_period()
    {
        var checker = new CheckerMaterial(1, new SolidMaterial(Blocks.Stone), new SolidMaterial(Blocks.Cobblestone));
        await Assert.That(Brushed(checker)).IsEmpty();
    }

    /// <summary>Every theme the studio ships is silent under every rule here. `PT3`'s floor is a guard against
    /// a pathological number and not a style rule, so the shelf is what proves it: a floor that complains
    /// about a preset is a floor set too high.</summary>
    [Test]
    public async Task Every_shipped_preset_is_silent()
    {
        foreach (var (name, theme) in ThemePresets.All)
        {
            var findings = TerrainThemeValidation.Check(theme);
            await Assert.That(findings.Select(f => $"{name}: {f.Rule} {f.Field}")).IsEmpty();
        }
    }

    // ── PT4: a sampled field on a face a player reads edge-on ────────────────────
    // A field of the plane gives every block in a column one answer, which is a fabric seen from above and a
    // run of vertical stripes seen from the side. Both tall buckets are asked: the wall is a face by
    // definition, and the fill is the face every cut through a board leaves — a cliff, a tunnel, a void edge.
    // A rise is never nought on either, whatever the band's thickness (author).

    /// <summary>A noise in an enabled wall with no vertical period is named at the bucket it landed in.</summary>
    [Test]
    public async Task A_flat_field_in_the_wall_is_named()
    {
        var flat = new NoiseMaterial(1, 8, 2,
            [new SolidMaterial(Blocks.Stone), new SolidMaterial(Blocks.Cobblestone)]);
        var finding = TerrainThemeValidation.Check(TerrainTheme.Default with { Wall = flat }).Single();

        await Assert.That(finding.Rule).IsEqualTo(TerrainThemeRules.FlatFieldOnAFace);
        await Assert.That(finding.Field).IsEqualTo("wall.rise");
    }

    /// <summary>The same field with a rise is the answer the finding asks for, so it says nothing.</summary>
    [Test]
    public async Task A_field_carrying_a_rise_is_silent()
    {
        var risen = new NoiseMaterial(1, 8, 2,
            [new SolidMaterial(Blocks.Stone), new SolidMaterial(Blocks.Cobblestone)], Rise: 8);
        await Assert.That(TerrainThemeValidation.Check(TerrainTheme.Default with { Wall = risen })).IsEmpty();
    }

    /// <summary>A wall that paints nothing is not asked. A theme built from one material binds it to every
    /// bucket and disables the ones it does not want, so a disabled wall would answer a second time for the
    /// fill's own fault.</summary>
    [Test]
    public async Task A_disabled_wall_is_not_asked()
    {
        var flat = new NoiseMaterial(1, 8, 2,
            [new SolidMaterial(Blocks.Stone), new SolidMaterial(Blocks.Cobblestone)]);
        await Assert.That(TerrainThemeValidation
            .Check(TerrainTheme.Default with { Wall = flat, WallEnabled = false })).IsEmpty();
    }

    /// <summary>A fill is asked exactly as a wall is. Ground is cut wherever a board has a cliff, a tunnel or
    /// a void edge, and what that cut leaves is the fill — the face a player stands closest to.</summary>
    [Test]
    public async Task A_flat_field_in_the_fill_is_named()
    {
        var flat = new VoronoiMaterial(1, 9,
            [new VoronoiBand(new SolidMaterial(Blocks.Stone), 1),
             new VoronoiBand(new SolidMaterial(Blocks.Cobblestone), 4)]);
        var finding = TerrainThemeValidation.Check(TerrainTheme.Default with { Fill = flat }).Single();

        await Assert.That(finding.Rule).IsEqualTo(TerrainThemeRules.FlatFieldOnAFace);
        await Assert.That(finding.Field).IsEqualTo("fill.rise");
    }

    /// <summary>A shape stating a <c>material</c> reaches the gate as a theme carrying that material in its
    /// fill, so a tunnel wall and a kerb are asked through the same bucket a board's body is.</summary>
    [Test]
    public async Task A_flat_field_on_a_made_thing_is_named()
    {
        var flat = new VoronoiMaterial(1, 9,
            [new VoronoiBand(new SolidMaterial(Blocks.Stone), 1),
             new VoronoiBand(new SolidMaterial(Blocks.Cobblestone), 4)]);
        var made = TerrainTheme.OfMaterial(flat, TerrainTheme.Default);
        var finding = TerrainThemeValidation.Check(made).Single();

        await Assert.That(finding.Rule).IsEqualTo(TerrainThemeRules.FlatFieldOnAFace);
        await Assert.That(finding.Field).IsEqualTo("fill.rise");
    }

    /// <summary>Every band of a stack is asked whatever its thickness. A course sits against the courses
    /// around it, so a field flat through one still meets its neighbours in a straight seam — which is why the
    /// clay bodies on the shelf give a rise to a band a single course deep.</summary>
    [Test]
    public async Task Every_band_of_a_stack_is_asked_whatever_its_thickness()
    {
        var turf = new CellMaterial(1, 4, 50, 4,
            [new SolidMaterial(Blocks.Grass), new SolidMaterial(Blocks.Dirt)]);
        var body = new NoiseMaterial(2, 8, 2,
            [new SolidMaterial(Blocks.Stone), new SolidMaterial(Blocks.Cobblestone)]);

        var stack = new LayeredMaterial(new BandStack([new Band(turf, 1), new Band(body, 6)]));
        var findings = TerrainThemeValidation.Check(TerrainTheme.Default with { Fill = stack });

        await Assert.That(findings.Select(f => f.Field))
            .IsEquivalentTo(new[] { "fill.stack[0].rise", "fill.stack[1].rise" });
    }

    /// <summary>Only a <b>sampled</b> field is asked. A pattern that is drawn — a checkerboard's square, a wall
    /// run's stripe, a frame's edge — places its geometry where the author put it and already says what it
    /// does with height, so it carries no <c>rise</c> to state and none is wanted of it.
    ///
    /// <para>Held by reflection rather than by a list, so a material added with a <c>Rise</c> of its own joins
    /// the rule and one without it can never be asked for a field it does not have.</para></summary>
    [Test]
    public async Task Only_the_materials_carrying_a_rise_are_asked_for_one()
    {
        var drawn = new TerrainMaterial[]
        {
            new SolidMaterial(Blocks.Stone),
            new CheckerMaterial(2, new SolidMaterial(Blocks.Stone), new SolidMaterial(Blocks.Cobblestone)),
            new LogCheckerMaterial(1, Blocks.Log),
            new LaidLogMaterial(Blocks.Log),
            new WallRunMaterial([new WallStripe(new SolidMaterial(Blocks.Stone), 2)]),
            new WallDiagonalMaterial([new WallStripe(new SolidMaterial(Blocks.Stone), 2)]),
            new WallFrameMaterial(new SolidMaterial(Blocks.Stone), new SolidMaterial(Blocks.Cobblestone)),
            new TeamTintedMaterial(Blocks.StainedClay, new SolidMaterial(Blocks.StainedClay)),
        };

        foreach (var material in drawn)
        {
            await Assert.That(material.GetType().GetProperty("Rise")).IsNull()
                .Because($"{material.GetType().Name} is drawn rather than sampled and states no vertical period");
            await Assert.That(TerrainThemeValidation.Check(TerrainTheme.Default with { Wall = material })
                .Where(f => f.Rule == TerrainThemeRules.FlatFieldOnAFace)).IsEmpty();
        }
    }

    /// <summary>A stack read any way but by depth gives every band the bucket's whole span, so each is asked
    /// exactly as the bucket was.</summary>
    [Test]
    public async Task A_band_read_across_the_shape_is_asked_whatever_its_thickness()
    {
        var flat = new NoiseMaterial(1, 8, 2,
            [new SolidMaterial(Blocks.Stone), new SolidMaterial(Blocks.Cobblestone)]);
        var across = new LayeredMaterial(new BandStack([new Band(flat, 1)]), BandAxis.Slope);

        await Assert.That(TerrainThemeValidation.Check(TerrainTheme.Default with { Wall = across })
            .Single().Field).IsEqualTo("wall.stack[0].rise");
    }

    /// <summary>The fill bucket, which claims every course under the surface — the one place a pattern is
    /// read with no depth rule firing beside it, so a brush finding stands alone.</summary>
    private static Findings Brushed(TerrainMaterial material) =>
        TerrainThemeValidation.Check(TerrainTheme.Default with { Fill = material });
}
