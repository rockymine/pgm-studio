using PgmStudio.Geom;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// Terrain painting (docs/world-export/terrain-painting.md TP1–TP12): the pure band resolver over synthetic
/// column profiles, and the whole pass over built worlds. The canonical oracle is the nine-tall void edge —
/// 1 bedrock / 7 wall / 1 rim, bottom to top.
/// </summary>
public sealed class TerrainPainterTests
{
    private static ColumnProfile VoidEdge(int top) => new(top, VoidEdge: true, OpenEdge: true, ClosedEdge: true, VoidDrop: 1, TerrainDrop: -1);
    private static ColumnProfile Interior(int top) => new(top, VoidEdge: false, OpenEdge: false, ClosedEdge: false, VoidDrop: -1, TerrainDrop: -1);
    // an edge whose only drop is onto lower terrain `neighbourTop` — a plateau standing on a plateau, never
    // touching the void, which is what the three rim modes disagree about.
    private static ColumnProfile TerrainStep(int top, int neighbourTop)
        => new(top, VoidEdge: false, OpenEdge: true, ClosedEdge: true, VoidDrop: -1, TerrainDrop: neighbourTop);

    // ── the resolver (pure) ────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Void_edge_splits_one_bedrock_seven_wall_one_rim()
    {
        var bands = TerrainPainter.Resolve(VoidEdge(9), TerrainTheme.Default);
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 1, TerrainBucket.Bedrock),
            new TerrainBand(1, 8, TerrainBucket.Wall),
            new TerrainBand(8, 9, TerrainBucket.Rim),
        });
    }

    [Test]
    public async Task Interior_is_bedrock_then_fill_then_a_three_deep_surface_stack()
    {
        // the default surface claims its configured depth (3: grass over two dirt), fill takes the middle.
        var bands = TerrainPainter.Resolve(Interior(9), TerrainTheme.Default);
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 1, TerrainBucket.Bedrock),
            new TerrainBand(1, 6, TerrainBucket.Fill),
            new TerrainBand(6, 9, TerrainBucket.Surface),
        });
    }

    [Test]
    public async Task Rim_depth_takes_more_top_blocks_and_the_wall_takes_the_rest()
    {
        var bands = TerrainPainter.Resolve(VoidEdge(9), TerrainTheme.Default with { Rim = TerrainTheme.Default.Rim with { Depth = 3 } });
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 1, TerrainBucket.Bedrock),
            new TerrainBand(1, 6, TerrainBucket.Wall),
            new TerrainBand(6, 9, TerrainBucket.Rim),
        });
    }

    [Test]
    public async Task A_one_block_terrain_step_is_pure_rim_no_wall()
    {
        // surface 9 dropping to a neighbour at 8: after the 1-block rim there is no exposed riser.
        var bands = TerrainPainter.Resolve(TerrainStep(9, 8), TerrainTheme.Default);
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 1, TerrainBucket.Bedrock),
            new TerrainBand(1, 8, TerrainBucket.Fill),
            new TerrainBand(8, 9, TerrainBucket.Rim),
        });
    }

    [Test]
    public async Task A_four_block_terrain_step_walls_its_three_exposed_courses()
    {
        // surface 9 dropping to a neighbour at 5: rim at 8, wall 5..7, fill below.
        var bands = TerrainPainter.Resolve(TerrainStep(9, 5), TerrainTheme.Default);
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 1, TerrainBucket.Bedrock),
            new TerrainBand(1, 5, TerrainBucket.Fill),
            new TerrainBand(5, 8, TerrainBucket.Wall),
            new TerrainBand(8, 9, TerrainBucket.Rim),
        });
    }

    [Test]
    public async Task Wall_on_terrain_faces_off_leaves_the_internal_riser_as_fill()
    {
        var bands = TerrainPainter.Resolve(TerrainStep(9, 5), TerrainTheme.Default with { WallOnTerrainFaces = false });
        // no void drop, so with terrain faces off there is no wall — rim on top, everything below is fill.
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 1, TerrainBucket.Bedrock),
            new TerrainBand(1, 8, TerrainBucket.Fill),
            new TerrainBand(8, 9, TerrainBucket.Rim),
        });
    }

    [Test]
    public async Task Bedrock_thickness_eats_into_the_paintable_stone()
    {
        var bands = TerrainPainter.Resolve(VoidEdge(9), TerrainTheme.Default with { Bedrock = BedrockSpec.Absolute(3) });
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 3, TerrainBucket.Bedrock),
            new TerrainBand(3, 8, TerrainBucket.Wall),
            new TerrainBand(8, 9, TerrainBucket.Rim),
        });
    }

    [Test]
    public async Task Terrain_relative_bedrock_keeps_a_constant_painted_depth()
    {
        // a fixed 4-deep painted shell: a tall column carries thick bedrock beneath it.
        var bands = TerrainPainter.Resolve(VoidEdge(12), TerrainTheme.Default with { Bedrock = BedrockSpec.TerrainRelative(4) });
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 8, TerrainBucket.Bedrock),
            new TerrainBand(8, 11, TerrainBucket.Wall),
            new TerrainBand(11, 12, TerrainBucket.Rim),
        });
    }

    [Test]
    public async Task Bedrock_as_tall_as_the_column_stops_rim_and_wall()
    {
        var bands = TerrainPainter.Resolve(VoidEdge(4), TerrainTheme.Default with { Bedrock = BedrockSpec.Absolute(4) });
        await Assert.That(bands).IsEquivalentTo(new[] { new TerrainBand(0, 4, TerrainBucket.Bedrock) });
    }

    [Test]
    public async Task Boundary_rim_lips_a_structure_facing_edge_that_drop_leaves_bare()
    {
        // a cell facing only a structure (a plateau boundary, no drop): drop ⇒ surface top, boundary ⇒ rim.
        var facingStructure = new ColumnProfile(9, VoidEdge: false, OpenEdge: false, ClosedEdge: true, VoidDrop: -1, TerrainDrop: -1);
        var drop = TerrainPainter.Resolve(facingStructure, TerrainTheme.Default);
        var boundary = TerrainPainter.Resolve(facingStructure, TerrainTheme.Default with { RimEdges = RimEdges.Boundary });
        await Assert.That(drop[^1].Bucket).IsEqualTo(TerrainBucket.Surface);
        await Assert.That(boundary[^1].Bucket).IsEqualTo(TerrainBucket.Rim);
    }

    [Test]
    public async Task A_void_only_rim_caps_the_outside_and_leaves_every_tread_of_a_staircase_bare()
    {
        // The case the mode exists for: shapes stacked into a staircase are open-edged at every tread, so the
        // default rim draws a lip on each one. Void-only asks the narrower question and finds only the outside.
        var theme = TerrainTheme.Default with { RimEdges = RimEdges.Void };
        var tread = TerrainPainter.Resolve(TerrainStep(9, 5), theme);
        var outside = TerrainPainter.Resolve(VoidEdge(9), theme);

        await Assert.That(tread[^1].Bucket).IsEqualTo(TerrainBucket.Surface);   // the tread keeps its surface
        await Assert.That(outside[^1].Bucket).IsEqualTo(TerrainBucket.Rim);     // the true outside is capped
        // The wall is the rim's own question, so the tread's riser is walled either way.
        await Assert.That(tread.Any(band => band.Bucket == TerrainBucket.Wall)).IsTrue();
    }

    [Test]
    public async Task Every_rim_mode_caps_the_void_because_the_edge_tests_nest()
    {
        foreach (var mode in new[] { RimEdges.Void, RimEdges.Drop, RimEdges.Boundary })
        {
            var bands = TerrainPainter.Resolve(VoidEdge(9), TerrainTheme.Default with { RimEdges = mode });
            await Assert.That(bands[^1].Bucket).IsEqualTo(TerrainBucket.Rim);
        }
    }

    [Test]
    public async Task A_staircase_of_stacked_plateaus_is_void_edged_only_around_its_outside()
    {
        // The profile half of the same claim, over a real world rather than a hand-built column: three treads,
        // each a block taller than the last, with void all around. Every tread edge is an open edge; only the
        // footprint's own border touches the void.
        var columns = new List<ColumnSegment>();
        for (var x = 0; x < 9; x++)
        for (var z = 0; z < 9; z++)
            columns.Add(Seg(x, z, 1, 9 + x / 3));       // three 3-wide treads at 9, 10 and 11
        var terrain = TerrainBuilder.Build(columns);
        var profile = new TerrainProfile(terrain.World, terrain.SurfaceTop);

        // The high side of the step between the first and second tread (x = 3, looking down onto x = 2) —
        // well inside the board, so its only edge is the drop.
        await Assert.That(profile.TryGetColumn((3, 4), out var tread)).IsTrue();
        await Assert.That(tread.OpenEdge).IsTrue();
        await Assert.That(tread.VoidEdge).IsFalse();

        // The board's own border.
        await Assert.That(profile.TryGetColumn((0, 4), out var border)).IsTrue();
        await Assert.That(border.VoidEdge).IsTrue();
    }

    /// <summary>
    /// <b>The inward walk crosses an elevation step</b> — the author's call, and the one thing about the inset
    /// that a flat board cannot demonstrate. The same three-tread staircase: only its outer face seeds, so the
    /// count runs across the treads and up the hill rather than restarting at each of them.
    ///
    /// <para>The middle column is what tells the two readings apart. Walked from the outer face alone it is
    /// four steps in from the border; had every tread edge seeded its own ring 0 it would be one, since a tread
    /// is three wide. Four and not one is the reading, so bands stay available on ground that is not flat.</para>
    /// </summary>
    [Test]
    public async Task The_inward_walk_runs_across_a_tread_rather_than_restarting_on_it()
    {
        var columns = new List<ColumnSegment>();
        for (var x = 0; x < 9; x++)
        for (var z = 0; z < 9; z++)
            columns.Add(Seg(x, z, 1, 9 + x / 3));       // three 3-wide treads at 9, 10 and 11
        var terrain = TerrainBuilder.Build(columns);
        var profile = new TerrainProfile(terrain.World, terrain.SurfaceTop);

        // The outer face is nought all the way round, whichever tread it falls on.
        foreach (var cell in new[] { (0, 4), (8, 4), (4, 0), (4, 8) })
        {
            await Assert.That(profile.TryGetColumn(cell, out var edge)).IsTrue();
            await Assert.That((cell, edge.Inset)).IsEqualTo((cell, 0));
        }

        // Straight in from the border, counting on past two tread boundaries (x = 2|3 and x = 5|6).
        foreach (var (cell, expected) in new[] { (((int, int))(1, 4), 1), ((2, 4), 2), ((3, 4), 3), ((4, 4), 4) })
        {
            await Assert.That(profile.TryGetColumn(cell, out var column)).IsTrue();
            await Assert.That((cell, column.Inset)).IsEqualTo((cell, expected));
        }

        // And the step itself is still an open edge — the inset is a second axis beside the edge tests, not a
        // replacement for them, so nothing a rim mode reads has moved.
        await Assert.That(profile.TryGetColumn((3, 4), out var tread)).IsTrue();
        await Assert.That(tread.OpenEdge).IsTrue();
        await Assert.That(tread.VoidEdge).IsFalse();
    }

    // ── bucket toggles (TP12): fill is the required fallback down the chain ──────────────────────────────

    [Test]
    public async Task Rim_off_lets_the_surface_stack_read_right_up_to_the_edge_lip()
    {
        // an edge with the rim disabled falls to the surface stack (depth 3), the wall still rises below it.
        var bands = TerrainPainter.Resolve(VoidEdge(9), TerrainTheme.Default with { Rim = TerrainTheme.Default.Rim with { Enabled = false } });
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 1, TerrainBucket.Bedrock),
            new TerrainBand(1, 6, TerrainBucket.Wall),
            new TerrainBand(6, 9, TerrainBucket.Surface),
        });
    }

    [Test]
    public async Task Rim_and_surface_both_off_leaves_the_edge_top_as_wall_over_fill()
    {
        // rim and surface off: the top course chain bottoms out at fill, so the wall claims the whole riser.
        var theme = TerrainTheme.Default with
        {
            Rim = TerrainTheme.Default.Rim with { Enabled = false },
            Surface = TerrainTheme.Default.Surface with { Enabled = false },
        };
        var bands = TerrainPainter.Resolve(VoidEdge(9), theme);
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 1, TerrainBucket.Bedrock),
            new TerrainBand(1, 9, TerrainBucket.Wall),
        });
    }

    [Test]
    public async Task Wall_off_turns_the_riser_to_fill_under_the_rim()
    {
        var bands = TerrainPainter.Resolve(VoidEdge(9), TerrainTheme.Default with { WallEnabled = false });
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 1, TerrainBucket.Bedrock),
            new TerrainBand(1, 8, TerrainBucket.Fill),
            new TerrainBand(8, 9, TerrainBucket.Rim),
        });
    }

    [Test]
    public async Task Surface_off_leaves_an_interior_all_fill_above_bedrock()
    {
        var bands = TerrainPainter.Resolve(Interior(9), TerrainTheme.Default with { Surface = TerrainTheme.Default.Surface with { Enabled = false } });
        await Assert.That(bands).IsEquivalentTo(new[]
        {
            new TerrainBand(0, 1, TerrainBucket.Bedrock),
            new TerrainBand(1, 9, TerrainBucket.Fill),
        });
    }

    // ── the whole pass over a built world ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Painting_a_plateau_writes_rim_wall_grass_and_leaves_bedrock()
    {
        // a solid 5×5 plateau, surface top 9, surrounded by void.
        var columns = new List<ColumnSegment>();
        for (var x = 0; x < 5; x++)
        for (var z = 0; z < 5; z++)
            columns.Add(Seg(x, z, 1, 9));
        var terrain = TerrainBuilder.Build(columns);
        TerrainPainter.Paint(terrain.World, terrain.SurfaceTop, TerrainTheme.Default);

        var w = terrain.World;
        // interior column (2,2): grass over two dirt (the default surface stack), stone body, bedrock floor.
        await Assert.That(w.GetBlock(2, 8, 2)).IsEqualTo((Blocks.Grass, 0));
        await Assert.That(w.GetBlock(2, 7, 2)).IsEqualTo((Blocks.Dirt, 0));
        await Assert.That(w.GetBlock(2, 6, 2)).IsEqualTo((Blocks.Dirt, 0));
        await Assert.That(w.GetBlock(2, 4, 2)).IsEqualTo((Blocks.Stone, 0));
        await Assert.That(w.GetBlock(2, 0, 2)).IsEqualTo((Blocks.Bedrock, 0));
        // edge column (0,2): quartz rim on top, clay wall below (neutral grey with no team map), bedrock floor.
        await Assert.That(w.GetBlock(0, 8, 2)).IsEqualTo((Blocks.QuartzBlock, 0));
        await Assert.That(w.GetBlock(0, 5, 2)).IsEqualTo((Blocks.StainedClay, 8));
        await Assert.That(w.GetBlock(0, 0, 2)).IsEqualTo((Blocks.Bedrock, 0));
    }

    // ── team tint (a material, on any bucket) ────────────────────────────────────────────────────────

    [Test]
    public async Task Team_tint_takes_the_team_damage_and_falls_back_to_neutral()
    {
        var tint = new TeamTintedMaterial(Blocks.StainedClay, new SolidMaterial(Blocks.StainedClay, 8));
        await Assert.That(tint.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Wall, 0, TeamData: 14)))
            .IsEqualTo((Blocks.StainedClay, 14));
        await Assert.That(tint.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Wall, 0, TeamData: -1)))
            .IsEqualTo((Blocks.StainedClay, 8));
    }

    [Test]
    public async Task Team_tint_nested_in_a_layer_stack_keeps_the_cell_team()
    {
        // a team-tinted layer inside a stack still reads the cell's team from the shared context.
        var layered = new LayeredMaterial(new BandStack([new Band(new SolidMaterial(Blocks.Grass), 1), new Band(new TeamTintedMaterial(Blocks.Wool, new SolidMaterial(Blocks.Dirt)), 2)]));
        await Assert.That(layered.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Surface, 0, TeamData: 3))).IsEqualTo((Blocks.Grass, 0));
        await Assert.That(layered.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Surface, 1, TeamData: 3))).IsEqualTo((Blocks.Wool, 3));
    }

    [Test]
    public async Task Default_wall_paints_the_team_colour_and_neutral_grey_off_team()
    {
        var columns = new List<ColumnSegment>();
        for (var x = 0; x < 5; x++)
        for (var z = 0; z < 5; z++)
            columns.Add(Seg(x, z, 1, 9));

        var red = Build(columns);
        TerrainPainter.Paint(red.World, red.SurfaceTop, TerrainTheme.Default, teamDamageAt: (_, _) => 14);
        await Assert.That(red.World.GetBlock(0, 5, 2)).IsEqualTo((Blocks.StainedClay, 14));   // wall = red clay

        var neutral = Build(columns);
        TerrainPainter.Paint(neutral.World, neutral.SurfaceTop, TerrainTheme.Default);           // no team map
        await Assert.That(neutral.World.GetBlock(0, 5, 2)).IsEqualTo((Blocks.StainedClay, 8));  // wall = neutral grey
    }

    [Test]
    public async Task Team_tint_applies_to_any_bucket_not_just_the_wall()
    {
        // a theme whose rim is team-tinted wool proves the tint is a general material, not wall-only.
        var theme = TerrainTheme.Default with { Rim = TerrainTheme.Default.Rim with { Material = new TeamTintedMaterial(Blocks.Wool, new SolidMaterial(Blocks.QuartzBlock)) } };
        var columns = new List<ColumnSegment>();
        for (var x = 0; x < 5; x++)
        for (var z = 0; z < 5; z++)
            columns.Add(Seg(x, z, 1, 9));
        var built = Build(columns);
        TerrainPainter.Paint(built.World, built.SurfaceTop, theme, teamDamageAt: (_, _) => 5);
        await Assert.That(built.World.GetBlock(0, 8, 2)).IsEqualTo((Blocks.Wool, 5));   // rim = team wool
    }

    private static BuiltTerrain Build(List<ColumnSegment> columns) => TerrainBuilder.Build(columns);

    [Test]
    public async Task The_painter_never_touches_a_stamped_structure_column()
    {
        // a 5×5 plateau; overwrite one column's surface block with bedrock to stand in for a stamp.
        var columns = new List<ColumnSegment>();
        for (var x = 0; x < 5; x++)
        for (var z = 0; z < 5; z++)
            columns.Add(Seg(x, z, 1, 9));
        var terrain = TerrainBuilder.Build(columns);
        // a "structure": bedrock all the way up the (2,2) column, taller than the terrain.
        for (var y = 0; y <= 12; y++) terrain.World.SetBlock(2, y, 2, Blocks.Bedrock);

        TerrainPainter.Paint(terrain.World, terrain.SurfaceTop, TerrainTheme.Default);

        var w = terrain.World;
        // the structure column is untouched — still bedrock at its surface, not grass/rim.
        await Assert.That(w.GetBlock(2, 8, 2)).IsEqualTo((Blocks.Bedrock, 0));
        // its terrain neighbour (2,1) is not walled toward it (structure seals the face) and, being interior
        // to the void otherwise, keeps a grass surface rather than a clay riser toward the structure.
        await Assert.That(w.GetBlock(2, 8, 1)).IsEqualTo((Blocks.Grass, 0));
    }

    /// <summary>
    /// The whole point of routing both callers through <see cref="TerrainPainter.ColumnBlocks"/>: a top-down
    /// preview that resolves only each column's top block must name the same block the full paint writes
    /// there. If these two ever diverge, the preview stops being a preview — so the agreement is asserted over
    /// a board with rims, walls, plateau steps and a team tint, on every cell rather than a sample.
    /// </summary>
    [Test]
    public async Task A_columns_top_block_is_what_the_full_paint_writes_on_top()
    {
        // Two plateaus side by side, so the board carries void rims, a terrain step and interiors at once.
        var columns = new List<ColumnSegment>();
        for (var x = 0; x < 12; x++)
        for (var z = 0; z < 12; z++)
            columns.Add(Seg(x, z, 1, x < 6 ? 9 : 14));
        var terrain = TerrainBuilder.Build(columns);
        int TeamAt(int x, int z) => x < 6 ? 14 : 11;   // a tint that differs across the two plateaus

        var profile = new TerrainProfile(terrain.World, terrain.SurfaceTop);
        var tops = profile.PaintableColumns().ToDictionary(
            c => c.Cell,
            c => TerrainPainter.TopBlock(c.Cell.X, c.Cell.Z, c.Profile, TerrainTheme.Default, TeamAt(c.Cell.X, c.Cell.Z)));

        TerrainPainter.Paint(terrain.World, terrain.SurfaceTop, TerrainTheme.Default, TeamAt);

        await Assert.That(tops.Count).IsGreaterThan(100);   // the whole board, not a corner of it
        foreach (var (cell, top) in tops)
        {
            await Assert.That(top).IsNotNull();
            await Assert.That(terrain.World.GetBlock(cell.X, top!.Value.Y, cell.Z))
                .IsEqualTo((top.Value.Id, top.Value.Data));
            // and it really is the column's top: nothing solid sits above it.
            await Assert.That(terrain.World.GetBlock(cell.X, top.Value.Y + 1, cell.Z).Id).IsEqualTo(0);
        }
    }

    // ── the inset is carried and not yet spent ──────────────────────────────────────────────────────
    /// <summary>The guarantee that adding the inward axis to <see cref="ColumnProfile"/> moved no paint: the
    /// resolver is handed two profiles differing in nothing but <c>Inset</c> and answers the same bands. The
    /// field is measured for the authored shape that will read it (`B199`/`B200`) and nothing reads it yet, so
    /// a theme applied to a shape resolves exactly as it did before the axis existed.</summary>
    [Test]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(7)]
    public async Task A_theme_resolves_the_same_bands_whatever_the_inset_says(int inset)
    {
        foreach (var theme in new[] { TerrainTheme.Default })
        foreach (var baseline in new[] { VoidEdge(9), Interior(9), TerrainStep(12, 9) })
        {
            var withInset = baseline with { Inset = inset };
            await Assert.That((inset, TerrainPainter.Resolve(withInset, theme)))
                .IsEquivalentTo((inset, TerrainPainter.Resolve(baseline, theme)));
        }
    }

    // ── a band stack read inward (B199/B200's axis) ─────────────────────────────────────────────────
    /// <summary>A rim, two rings of the next block, a checkered ring, five of a field, and then nothing —
    /// the author's own sequence, resolved straight off the axis. The stack is the same
    /// <see cref="LayeredMaterial"/> a depth reading uses; only <see cref="BandAxis"/> differs.</summary>
    private static LayeredMaterial RingStack() => new(
        new BandStack(
            [
                new Band(new SolidMaterial(4, 0), 1),        // ring 0
                new Band(new SolidMaterial(98, 0), 2),       // rings 1-2
                new Band(new SolidMaterial(159, 15), 1),     // ring 3
                new Band(new SolidMaterial(2, 0), 5),        // rings 4-8
            ],
            BandEnding.HandOver),
        BandAxis.Inward,
        Beyond: new SolidMaterial(3, 0));

    [Test]
    [Arguments(0, 4)]
    [Arguments(1, 98)]
    [Arguments(2, 98)]
    [Arguments(3, 159)]
    [Arguments(4, 2)]
    [Arguments(8, 2)]
    [Arguments(9, 3)]      // past the last band: handed over
    [Arguments(30, 3)]
    public async Task An_inward_stack_reads_the_ring_and_hands_over_past_its_last_band(int inset, int expected)
    {
        var ctx = new BucketContext(0, 9, 0, TerrainBucket.Surface, DepthFromTop: 0, Inset: inset);
        await Assert.That((inset, RingStack().Resolve(ctx).Id)).IsEqualTo((inset, expected));
    }

    [Test]
    public async Task An_inward_stack_ignores_the_depth_the_column_is_at()
    {
        // The axis is the point: two cells of one column, three courses apart, are in the same ring and answer
        // the same block. A depth stack would have moved between bands over that distance.
        var ring = RingStack();
        var top = ring.Resolve(new BucketContext(0, 9, 0, TerrainBucket.Surface, DepthFromTop: 0, Inset: 1));
        var lower = ring.Resolve(new BucketContext(0, 6, 0, TerrainBucket.Surface, DepthFromTop: 3, Inset: 1));
        await Assert.That(lower).IsEqualTo(top);
    }

    [Test]
    public async Task Off_the_footprint_an_inward_stack_answers_what_lies_beyond_it()
    {
        // Inset is -1 where there is no ring to be in, which is not the same as being past the last band —
        // the stack is never asked, and a stack without a Beyond falls to stone the way every unclaimed
        // block already does.
        await Assert.That(RingStack().Resolve(
            new BucketContext(0, 9, 0, TerrainBucket.Surface, 0, Inset: -1)).Id).IsEqualTo(3);
        await Assert.That(new LayeredMaterial(new BandStack([new Band(new SolidMaterial(4, 0), 1)]), BandAxis.Inward)
            .Resolve(new BucketContext(0, 9, 0, TerrainBucket.Surface, 0, Inset: -1)).Id).IsEqualTo(Blocks.Stone);
    }

    [Test]
    public async Task A_depth_stack_is_unchanged_by_the_axis_existing()
    {
        // The default is what every layered material always meant, so an unmarked stack still reads downward.
        var depth = new LayeredMaterial(new BandStack(
            [new Band(new SolidMaterial(2, 0), 1), new Band(new SolidMaterial(3, 0), 2)]));
        foreach (var (fromTop, expected) in new[] { (0, 2), (1, 3), (2, 3), (9, 3) })
            await Assert.That((fromTop, depth.Resolve(
                new BucketContext(0, 9, 0, TerrainBucket.Surface, fromTop, Inset: 0)).Id)).IsEqualTo((fromTop, expected));
    }

    [Test]
    public async Task The_two_axes_compose_so_only_the_top_course_is_ringed()
    {
        // How a ring set is restricted to the top course without a knob: a depth stack whose first band is one
        // course thick and is itself the inward stack, with dirt under it. The top course rings; the courses
        // below it are dirt, which is what keeps a grass ring one block thick.
        var surface = new LayeredMaterial(new BandStack(
            [new Band(RingStack(), 1), new Band(new SolidMaterial(3, 0), 2)]));

        await Assert.That(surface.Resolve(new BucketContext(0, 9, 0, TerrainBucket.Surface, 0, Inset: 0)).Id).IsEqualTo(4);
        await Assert.That(surface.Resolve(new BucketContext(0, 8, 0, TerrainBucket.Surface, 1, Inset: 0)).Id).IsEqualTo(3);
        await Assert.That(surface.Resolve(new BucketContext(0, 7, 0, TerrainBucket.Surface, 2, Inset: 0)).Id).IsEqualTo(3);
    }

    [Test]
    public async Task An_inward_stack_round_trips_through_the_theme_json()
    {
        // The axis and the beyond are what the schema gained, so both have to survive being stored.
        var theme = TerrainTheme.Default with { Surface = new TopBand(RingStack(), 1, Enabled: true) };
        var json = TerrainThemeJson.Serialize(theme);

        await Assert.That(json).Contains("\"axis\":\"inward\"");
        await Assert.That(json).Contains("\"ending\":\"handOver\"");
        await Assert.That(TerrainThemeJson.Deserialize(json)).IsEqualTo(theme);
    }

    [Test]
    public async Task A_stored_theme_written_before_the_axis_existed_still_reads_as_depth()
    {
        // Purely additive: a layered material with no axis is the depth reading it always was, so nothing
        // already stored has to be rewritten.
        const string json = """
            {"surface":{"material":{"kind":"layered","stack":{"bands":[{"material":{"kind":"solid","id":2,"data":0},"thickness":1}]}},"depth":1,"enabled":true}}
            """;
        var theme = TerrainThemeJson.Deserialize(json);
        var layered = (LayeredMaterial)theme.Surface.Material;

        await Assert.That(layered.Axis).IsEqualTo(BandAxis.Depth);
        await Assert.That(layered.Beyond).IsNull();
    }

    /// <summary>A ground-layer segment, for a test whose subject is the fill rather than the stack.</summary>
    private static ColumnSegment Seg(int x, int z, int floor, int top) => new(x, z, floor, top, "ground");
}
