using System.Text.Json;
using PgmStudio.Export;
using PgmStudio.Geom;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Pgm.Sketch;
using PgmStudio.Minecraft.Stamping;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Vocabulary;

namespace PgmStudio.Export.Tests;

/// <summary>
/// Scoped theme resolution for the export (docs/world-export/terrain-painting.md TP10): <see cref="TerrainThemeScope"/>
/// turns the theme data on the sketch layout into a per-cell theme — a themed shape paints its theme, everything
/// else the map default, and the smaller (most specific) shape wins an overlap. Because the scope is the shape,
/// rasterised fresh, reshaping a shape moves its paint. Pure (no DB): themes are built from real theme JSON and
/// read back through their fill material.
/// </summary>
public sealed class TerrainThemeScopeTests
{
    private static JsonElement Fill(int id) => JsonSerializer.Deserialize<JsonElement>(
        TerrainThemeJson.Serialize(TerrainTheme.Default with { Fill = new SolidMaterial(id) }));
    private static int FillId(TerrainTheme theme) => theme.Fill.Resolve(new BucketContext(0, 0, 0, TerrainBucket.Fill, 0)).Id;

    /// <summary>A theme whose top course is a distinctive solid — what a painted surface reads back as.</summary>
    private static JsonElement Surfaced(int id) => JsonSerializer.Deserialize<JsonElement>(
        TerrainThemeJson.Serialize(TerrainTheme.Default with
        {
            Surface = TerrainTheme.Default.Surface with { Material = new SolidMaterial(id), Depth = 1 },
        }));

    private static SketchShape Rect(string id, double minX, double minZ, double maxX, double maxZ, string? theme) =>
        new() { Id = id, Type = "rectangle", Operation = "add", MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ, Theme = theme };

    // A non-mirroring island so the shapes theme exactly their own footprint (no orbit fan to reason about).
    private static string Layout(Dictionary<string, JsonElement>? themes, string? mapTheme, params SketchShape[] shapes)
    {
        var layout = new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "rot_180", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Themes = themes,
            MapTheme = mapTheme,
            Layers = [SketchLayer.Ground(shapes.ToList(),
                [new SketchGroup { Id = "i", Mirrors = false, ShapeIds = shapes.Select(s => s.Id).ToList() }])],
        };
        return layout.ToJson();
    }

    [Test]
    public async Task No_theming_resolves_the_builtin_default_everywhere()
    {
        var at = TerrainThemeScope.ThemeAt(Layout(null, null, Rect("s0", 0, 0, 4, 4, null)));
        await Assert.That(at("ground", 3, 3).Theme).IsEqualTo(TerrainTheme.Default);
        await Assert.That(at("ground", -99, 99).Theme).IsEqualTo(TerrainTheme.Default);
    }

    [Test]
    public async Task A_themed_shape_paints_its_theme_and_the_rest_the_map_default()
    {
        var themes = new Dictionary<string, JsonElement> { ["map"] = Fill(100), ["red"] = Fill(200) };
        var at = TerrainThemeScope.ThemeAt(Layout(themes, "map",
            Rect("p1", 0, 0, 4, 4, "red"),
            Rect("p2", 10, 10, 14, 14, null)));   // present but unthemed
        await Assert.That(FillId(at("ground", 2, 2).Theme)).IsEqualTo(200);     // inside p1 → red theme
        await Assert.That(FillId(at("ground", 12, 12).Theme)).IsEqualTo(100);   // inside p2, unthemed → map default
        await Assert.That(FillId(at("ground", 50, 50).Theme)).IsEqualTo(100);   // no shape → map default
    }

    [Test]
    public async Task Smallest_shape_wins_an_overlapped_cell()
    {
        var themes = new Dictionary<string, JsonElement> { ["big"] = Fill(400), ["small"] = Fill(300) };
        var at = TerrainThemeScope.ThemeAt(Layout(themes, null,
            Rect("big", 0, 0, 10, 10, "big"),
            Rect("small", 1, 1, 3, 3, "small")));
        await Assert.That(FillId(at("ground", 2, 2).Theme)).IsEqualTo(300);   // in both → the smaller (more specific) shape
        await Assert.That(FillId(at("ground", 6, 6).Theme)).IsEqualTo(400);   // only the big shape
    }

    [Test]
    public async Task Reshaping_a_shape_moves_its_paint()
    {
        var themes = new Dictionary<string, JsonElement> { ["map"] = Fill(100), ["red"] = Fill(200) };
        var before = TerrainThemeScope.ThemeAt(Layout(themes, "map", Rect("s0", 0, 0, 2, 2, "red")));
        await Assert.That(FillId(before("ground", 1, 1).Theme)).IsEqualTo(200);
        await Assert.That(FillId(before("ground", 5, 5).Theme)).IsEqualTo(100);

        // Same shape id, moved: the theme follows the new footprint with no re-assignment.
        var after = TerrainThemeScope.ThemeAt(Layout(themes, "map", Rect("s0", 4, 4, 6, 6, "red")));
        await Assert.That(FillId(after("ground", 1, 1).Theme)).IsEqualTo(100);
        await Assert.That(FillId(after("ground", 5, 5).Theme)).IsEqualTo(200);
    }

    [Test]
    public async Task An_unknown_map_theme_id_falls_back_to_the_builtin_default()
    {
        var at = TerrainThemeScope.ThemeAt(Layout(null, "missing", Rect("s0", 0, 0, 4, 4, null)));
        await Assert.That(at("ground", 0, 0).Theme).IsEqualTo(TerrainTheme.Default);
    }

    // ── A stacked board wears one theme per storey ───────────────────────────────────────────────────────
    private static SketchShape Slab(string id, int floor, int height, string? theme) => new()
    {
        Id = id, Type = "rectangle", Operation = "add",
        MinX = 0, MinZ = 0, MaxX = 8, MaxZ = 8,
        Floor = floor, BaseHeight = height, Theme = theme,
    };

    private static string Stacked(Dictionary<string, JsonElement> themes, string mapTheme,
                                  SketchShape lower, SketchShape upper) =>
        new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "rot_180", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Themes = themes,
            MapTheme = mapTheme,
            Layers =
            [
                new SketchLayer { Id = "ground", BaseY = 0, Layout = new SketchShapes { Shapes = [lower] } },
                new SketchLayer { Id = "deck", BaseY = 20, Layout = new SketchShapes { Shapes = [upper] } },
            ],
        }.ToJson();

    /// <summary>One cell, two storeys, two themes. The scope has to tell them apart or the upper slab's shape
    /// owns the ground beneath it — which is what made a deck wear the paint of the gallery under it.</summary>
    [Test]
    public async Task A_cell_on_two_layers_resolves_a_theme_for_each()
    {
        var themes = new Dictionary<string, JsonElement>
            { ["map"] = Fill(100), ["floor"] = Fill(200), ["roof"] = Fill(300) };
        var at = TerrainThemeScope.ThemeAt(Stacked(themes, "map",
            Slab("gallery", 0, 4, "floor"), Slab("deck", 0, 6, "roof")));

        await Assert.That(FillId(at("ground", 4, 4).Theme)).IsEqualTo(200);
        await Assert.That(FillId(at("deck", 4, 4).Theme)).IsEqualTo(300);
    }

    /// <summary>A shape id is unique within its layer and not across the stack: two made things compiled by one
    /// tool number their shapes alike, and each layer's cell has to paint its own shape's theme rather than
    /// whichever layer stated that id last.</summary>
    [Test]
    public async Task Two_layers_carrying_a_shape_of_one_id_each_paint_their_own_theme()
    {
        var themes = new Dictionary<string, JsonElement>
            { ["map"] = Fill(100), ["red"] = Fill(200), ["blue"] = Fill(300) };
        var at = TerrainThemeScope.ThemeAt(Stacked(themes, "map",
            Slab("statue-0", 0, 4, "red"), Slab("statue-0", 0, 6, "blue")));

        await Assert.That(FillId(at("ground", 4, 4).Theme)).IsEqualTo(200);
        await Assert.That(FillId(at("deck", 4, 4).Theme)).IsEqualTo(300);
    }

    /// <summary>And the paint follows: each storey's own surface takes its own theme's material, so a themed
    /// upper slab lands on blocks rather than on nothing.</summary>
    [Test]
    public async Task Each_storey_is_painted_in_its_own_theme()
    {
        var themes = new Dictionary<string, JsonElement>
            { ["map"] = Surfaced(100), ["floor"] = Surfaced(200), ["roof"] = Surfaced(300) };
        var layoutJson = Stacked(themes, "map", Slab("gallery", 0, 4, "floor"), Slab("deck", 0, 6, "roof"));

        var terrain = TerrainBuilder.Build(SketchRasterizer.RasterizeColumns(layoutJson));
        TerrainPainter.Paint(terrain.World, terrain.SurfaceByLayer, TerrainThemeScope.ThemeAt(layoutJson));

        var lowerTop = terrain.SurfaceByLayer["ground"][(4, 4)];
        var upperTop = terrain.SurfaceByLayer["deck"][(4, 4)];
        await Assert.That(upperTop).IsGreaterThan(lowerTop);

        await Assert.That(terrain.World.GetBlock(4, lowerTop - 1, 4).Id).IsEqualTo(200);
        await Assert.That(terrain.World.GetBlock(4, upperTop - 1, 4).Id).IsEqualTo(300);
    }
    // ── a shape's own material (TP22) ──────────────────────────────────────────────────────────────

    private static JsonElement Material(int id) =>
        JsonSerializer.Deserialize<JsonElement>(TerrainThemeJson.Serialize(new SolidMaterial(id)));

    private static SketchShape Made(string id, double minX, double minZ, double maxX, double maxZ, int material)
        => new()
        {
            Id = id, Type = "rectangle", Operation = "add",
            MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ, Material = Material(material),
        };

    /// <summary>A shape stating a material is one material in every bucket, so whichever band a block lands in
    /// it resolves to the same thing — which is what "this is made of that" means and what a theme cannot say
    /// about a shape too small to have an interior.</summary>
    [Test]
    public async Task A_shape_stating_a_material_paints_that_material_in_every_bucket()
    {
        var at = TerrainThemeScope.ThemeAt(Layout(
            new Dictionary<string, JsonElement> { ["map"] = Fill(100) }, "map",
            Made("rail", 0, 0, 2, 2, 42)));
        var theme = at("ground", 1, 1).Theme;

        TerrainBucket[] buckets = [TerrainBucket.Fill, TerrainBucket.Wall, TerrainBucket.Surface, TerrainBucket.Rim];
        foreach (var bucket in buckets)
            await Assert.That(theme.MaterialFor(bucket).Resolve(new BucketContext(0, 0, 0, bucket, 0)).Id)
                .IsEqualTo(42);

        // And the three buckets that choose themselves by geometry are off, which is what leaves one band.
        await Assert.That(theme.Rim.Enabled).IsFalse();
        await Assert.That(theme.Surface.Enabled).IsFalse();
        await Assert.That(theme.WallEnabled).IsFalse();
    }

    /// <summary>The whole point: on a shape every one of whose columns is an edge, a theme paints its rim over
    /// its wall and never its surface, and a material paints itself. The column is resolved through the
    /// painter's own band split, so this is what the world holds and not what the theme record says.</summary>
    [Test]
    public async Task A_material_covers_the_span_a_theme_would_have_split_into_rim_and_wall()
    {
        var themes = new Dictionary<string, JsonElement>
        {
            ["map"] = Fill(100),
            ["kerbed"] = JsonSerializer.Deserialize<JsonElement>(TerrainThemeJson.Serialize(TerrainTheme.Default with
            {
                RimEdges = RimEdges.Void,
                Rim = new TopBand(new SolidMaterial(7), Depth: 1),
                Wall = new SolidMaterial(8),
                Surface = TerrainTheme.Default.Surface with { Material = new SolidMaterial(9) },
                Fill = new SolidMaterial(9),
            })),
        };
        var at = TerrainThemeScope.ThemeAt(Layout(themes, "map",
            Rect("stilt", 0, 0, 2, 2, "kerbed"),
            Made("post", 10, 10, 12, 12, 42)));

        // One two-by-two column standing free: every side is void, so it is a rim column under any rimEdges.
        var column = new ColumnProfile(SurfaceTop: 6, Base: 1, VoidEdge: true, OpenEdge: true, ClosedEdge: true,
                                       VoidDrop: 1, TerrainDrop: -1);
        var themed = TerrainPainter.ColumnBlocks(0, 0, column, at("ground", 1, 1).Theme).Select(b => b.Id).ToList();
        var made = TerrainPainter.ColumnBlocks(10, 10, column, at("ground", 11, 11).Theme).Select(b => b.Id).ToList();

        await Assert.That(themed[0]).IsEqualTo(7).Because("the theme's rim caps the column");
        await Assert.That(themed.Skip(1).Distinct().ToList()).IsEquivalentTo(new List<int> { 8 })
            .Because("everything under the rim is the wall, and the theme's surface is nowhere on it");
        await Assert.That(made.Distinct().ToList()).IsEquivalentTo(new List<int> { 42 })
            .Because("a material is the whole span, top to bottom");
    }
    // ── SK23: a theme scoped to a shape it cannot show on ──────────────────────────────────────────

    /// <summary>A theme with a rim that paints, which is the default and the only case SK23 judges.</summary>
    private static JsonElement Kerbed() => JsonSerializer.Deserialize<JsonElement>(
        TerrainThemeJson.Serialize(TerrainTheme.Default with { Rim = new TopBand(new SolidMaterial(7), Depth: 1) }));

    private static JsonElement Unkerbed() => JsonSerializer.Deserialize<JsonElement>(
        TerrainThemeJson.Serialize(TerrainTheme.Default with
        {
            Rim = new TopBand(new SolidMaterial(7), Depth: 1, Enabled: false),
        }));

    [Test]
    public async Task A_theme_on_a_shape_with_no_interior_column_is_SK23()
    {
        var findings = TerrainThemeScope.Check(Layout(
            new Dictionary<string, JsonElement> { ["kerb"] = Kerbed() }, null,
            Rect("stilt", 0, 0, 1, 1, "kerb")));

        await Assert.That(findings.Count).IsEqualTo(1);
        await Assert.That(findings[0].Rule).IsEqualTo(SketchRules.ThemeShowsOnlyItsEdge);
        await Assert.That(findings[0].Severity).IsEqualTo(Severity.Complaint);
        await Assert.That(findings[0].Subjects!).Contains("stilt");
    }

    /// <summary>One finding per layer and theme, not per shape: a board drawn out of small pieces has
    /// hundreds of them and the decision that answers all of them is one.</summary>
    [Test]
    public async Task Every_shape_a_theme_cannot_show_on_is_one_finding_for_the_theme()
    {
        var findings = TerrainThemeScope.Check(Layout(
            new Dictionary<string, JsonElement> { ["kerb"] = Kerbed(), ["other"] = Kerbed() }, null,
            Rect("a", 0, 0, 1, 1, "kerb"),
            Rect("b", 4, 0, 5, 1, "kerb"),
            Rect("c", 8, 0, 9, 1, "kerb"),
            Rect("d", 12, 0, 13, 1, "other")));

        await Assert.That(findings.Count).IsEqualTo(2).Because("two themes, one finding each");
        var kerb = findings.Single(f => f.Message.Contains("'kerb'"));
        await Assert.That(kerb.Message).Contains("3 shape(s)");
        await Assert.That(kerb.Subjects!.Order(StringComparer.Ordinal))
            .IsEquivalentTo(new[] { "a", "b", "c" });
    }

    [Test]
    public async Task A_theme_on_ground_with_a_middle_is_not_SK23()
    {
        var findings = TerrainThemeScope.Check(Layout(
            new Dictionary<string, JsonElement> { ["kerb"] = Kerbed() }, null,
            Rect("field", 0, 0, 8, 8, "kerb")));
        await Assert.That(findings.Count).IsEqualTo(0);
    }

    /// <summary>With the rim off the top course falls to the surface (TP12), so the theme shows exactly as it
    /// was written — which is the honest way to paint thin ground and not a fault.</summary>
    [Test]
    public async Task A_theme_that_paints_no_rim_is_not_SK23_however_thin_the_shape()
    {
        var findings = TerrainThemeScope.Check(Layout(
            new Dictionary<string, JsonElement> { ["plain"] = Unkerbed() }, null,
            Rect("stilt", 0, 0, 1, 1, "plain")));
        await Assert.That(findings.Count).IsEqualTo(0);
    }

    /// <summary>The answer SK23 exists to point at: state what the shape is made of and there is one bucket,
    /// so there is nothing the geometry can hide.</summary>
    [Test]
    public async Task A_material_on_the_same_shape_is_not_SK23()
    {
        var findings = TerrainThemeScope.Check(Layout(
            new Dictionary<string, JsonElement> { ["kerb"] = Kerbed() }, null,
            Made("stilt", 0, 0, 1, 1, 42)));
        await Assert.That(findings.Count).IsEqualTo(0);
    }

    // ── paint laid over ground rather than being it (TP23) ────────────────────────────────────────

    /// <summary>A theme with a distinctive block in every bucket, so which bucket answered is readable.</summary>
    private static JsonElement Everywhere(int rim, int wall, int surface, int fill, bool edgesFromGround = false) =>
        JsonSerializer.Deserialize<JsonElement>(TerrainThemeJson.Serialize(new TerrainTheme
        {
            Rim = new TopBand(new SolidMaterial(rim), Depth: 1),
            Wall = new SolidMaterial(wall),
            Surface = new TopBand(new SolidMaterial(surface), Depth: 1),
            Fill = new SolidMaterial(fill),
            EdgesFromGround = edgesFromGround,
        }));

    private static int BucketId(TerrainTheme theme, TerrainBucket bucket) =>
        theme.MaterialFor(bucket).Resolve(new BucketContext(0, 0, 0, bucket, 0)).Id;

    /// <summary><b>A stroke keeps its own top and leaves the landmass's edge alone.</b> A theme is a whole
    /// column, which is right for a theme that <em>is</em> the ground and wrong for one laid over it: scoped
    /// to a road marking or a worn patch it repaints the rim and runs its own material the whole height of the
    /// exposed wall, which on `opus5-quatrefoil` put five courses of a stroke's dirt down the map's own face
    /// at (44, 19) against two on the ground inside it.</summary>
    [Test]
    public async Task A_theme_whose_edges_are_the_grounds_keeps_its_surface_and_takes_the_rest()
    {
        var themes = new Dictionary<string, JsonElement>
        {
            ["moor"] = Everywhere(rim: 10, wall: 11, surface: 12, fill: 13),
            ["stroke"] = Everywhere(rim: 20, wall: 21, surface: 22, fill: 23, edgesFromGround: true),
        };
        var at = TerrainThemeScope.ThemeAt(Layout(themes, "moor",
            Rect("ground", 0, 0, 20, 20, "moor"),
            Rect("brake", 4, 4, 8, 8, "stroke")));

        var paint = at("ground", 5, 5).Theme;
        await Assert.That(BucketId(paint, TerrainBucket.Surface)).IsEqualTo(22).Because("the stroke's own top");
        await Assert.That(BucketId(paint, TerrainBucket.Fill)).IsEqualTo(23).Because("and its own fill");
        await Assert.That(BucketId(paint, TerrainBucket.Rim)).IsEqualTo(10).Because("the landmass's edge");
        await Assert.That(BucketId(paint, TerrainBucket.Wall)).IsEqualTo(11).Because("and the landmass's face");
    }

    /// <summary>Unset, a theme is the whole column it always was — the word is opt-in, and a board that states
    /// none paints exactly as it did.</summary>
    [Test]
    public async Task A_theme_that_does_not_say_so_still_owns_its_whole_column()
    {
        var themes = new Dictionary<string, JsonElement>
        {
            ["moor"] = Everywhere(rim: 10, wall: 11, surface: 12, fill: 13),
            ["stroke"] = Everywhere(rim: 20, wall: 21, surface: 22, fill: 23),
        };
        var at = TerrainThemeScope.ThemeAt(Layout(themes, "moor",
            Rect("ground", 0, 0, 20, 20, "moor"),
            Rect("brake", 4, 4, 8, 8, "stroke")));

        var paint = at("ground", 5, 5).Theme;
        foreach (var (bucket, id) in new[]
                 { (TerrainBucket.Rim, 20), (TerrainBucket.Wall, 21),
                   (TerrainBucket.Surface, 22), (TerrainBucket.Fill, 23) })
            await Assert.That(BucketId(paint, bucket)).IsEqualTo(id);
    }

    /// <summary>The word survives the round trip, since a theme is stored as the record graph.</summary>
    [Test]
    public async Task The_word_round_trips_through_the_theme_json()
    {
        var read = TerrainThemeJson.Deserialize(
            TerrainThemeJson.Serialize(TerrainTheme.Default with { EdgesFromGround = true }));
        await Assert.That(read.EdgesFromGround).IsTrue();
    }

    // ── the orbit carries what a shape says, not only where it is ─────────────────────────────────

    /// <summary>A mirroring group, so every shape stands on the board once per orbit image.</summary>
    private static string Fanned(Dictionary<string, JsonElement>? themes, string? mapTheme,
                                 params SketchShape[] shapes) =>
        new SketchLayout
        {
            Setup = new SketchSetup { MirrorMode = "rot_180", Center = new SketchCenter { Cx = 0, Cz = 0 } },
            Themes = themes,
            MapTheme = mapTheme,
            Layers = [SketchLayer.Ground(shapes.ToList(),
                [new SketchGroup { Id = "team", Mirrors = true, ShapeIds = shapes.Select(s => s.Id).ToList() }])],
        }.ToJson();

    /// <summary>An orbit image is painted by the shape that drew it, so a road drawn on one half is a road on
    /// the other. What decides that is whether the image still <em>states</em> a material: a scope resolver
    /// asks the shape, so an image that dropped the word is ground the map default finishes.</summary>
    [Test]
    public async Task A_mirrored_image_of_a_material_shape_keeps_that_material()
    {
        var at = TerrainThemeScope.ThemeAt(Fanned(
            new Dictionary<string, JsonElement> { ["map"] = Fill(100) }, "map",
            Made("road", 4, 4, 8, 8, 42)));

        await Assert.That(FillId(at("ground", 5, 5).Theme)).IsEqualTo(42).Because("the drawn half");
        await Assert.That(FillId(at("ground", -6, -6).Theme)).IsEqualTo(42).Because("its rot_180 image");
    }

    /// <summary>The same for the other grain, so the two are not allowed to drift apart again.</summary>
    [Test]
    public async Task A_mirrored_image_of_a_themed_shape_keeps_that_theme()
    {
        var at = TerrainThemeScope.ThemeAt(Fanned(
            new Dictionary<string, JsonElement> { ["map"] = Fill(100), ["road"] = Fill(42) }, "map",
            Rect("road", 4, 4, 8, 8, "road")));

        await Assert.That(FillId(at("ground", 5, 5).Theme)).IsEqualTo(42);
        await Assert.That(FillId(at("ground", -6, -6).Theme)).IsEqualTo(42);
    }

    // ── a made thing fanned onto its images turns its blocks with it ─────────────────────────────

    private const int Wall = 98, Course = 10;
    private const int LadderLookingNorth = 2, StairClimbingEast = 0, LogAlongX = 4;
    private static readonly (int X, int Z) WallCell = (6, 6), LadderCell = (6, 5), StairCell = (5, 6), LogCell = (7, 6);

    /// <summary>A one-cell shape one course thick stating the block it is made of.</summary>
    private static SketchShape Block(string id, (int X, int Z) cell, int blockId, int data) => new()
    {
        Id = id, Type = "rectangle", Operation = "add",
        MinX = cell.X, MinZ = cell.Z, MaxX = cell.X + 1, MaxZ = cell.Z + 1, BaseHeight = 1,
        Material = JsonSerializer.Deserialize<JsonElement>(TerrainThemeJson.Serialize(new SolidMaterial(blockId, data))),
    };

    /// <summary>A made layer ten courses up holding the given shapes as one group, fanned by
    /// <paramref name="mode"/> about the origin where <paramref name="mirrors"/> says so.</summary>
    private static string Thing(string mode, bool mirrors, params SketchShape[] shapes) => new SketchLayout
    {
        Setup = new SketchSetup { MirrorMode = mode, Center = new SketchCenter { Cx = 0, Cz = 0 } },
        Layers =
        [
            new SketchLayer
            {
                Id = "thing", BaseY = Course, Kind = SketchLayer.MadeKind,
                Layout = new SketchShapes
                {
                    Shapes = [.. shapes],
                    Groups = [new SketchGroup { Id = "thing", Mirrors = mirrors, ShapeIds = [.. shapes.Select(s => s.Id)] }],
                },
            },
        ],
    }.ToJson();

    /// <summary>A wall block with three blocks that each point at it: a ladder on its north face with its back
    /// to it, a stair west of it climbing into it, and a log east of it lying along the line to it.</summary>
    private static SketchShape[] Pointing() =>
    [
        Block("wall", WallCell, Wall, 0),
        Block("ladder", LadderCell, Blocks.Ladder, LadderLookingNorth),
        Block("stair", StairCell, Blocks.OakStairs, StairClimbingEast),
        Block("log", LogCell, Blocks.Log, LogAlongX),
    ];

    /// <summary>The world the export paints: the scope's resolver, the board's fold, each layer over its own
    /// span and the made layer first.</summary>
    private static VoxelWorld Painted(string layoutJson)
    {
        var terrain = TerrainBuilder.Build(SketchRasterizer.RasterizeColumns(layoutJson));
        TerrainPainter.Paint(terrain.World, terrain.SurfaceByLayer, TerrainThemeScope.ThemeAt(layoutJson),
                             foldAt: DressingScope.SymmetryOf(layoutJson).Canonical,
                             floorByLayer: terrain.FloorByLayer, madeLayers: new HashSet<string> { "thing" });
        return terrain.World;
    }

    private static (int Id, int Data) At(VoxelWorld world, (int X, int Z) cell) =>
        world.GetBlock(cell.X, Course, cell.Z);

    private static (int X, int Z) Offset((int X, int Z) from, (int X, int Z) to) => (to.X - from.X, to.Z - from.Z);

    /// <summary>The way a ladder's back points — the side opposite the one it looks toward (north 2, south 3,
    /// west 4, east 5), which is the block holding it up.</summary>
    private static (int X, int Z) LadderBack(int data) => (data & 7) switch
    {
        2 => (0, 1),
        3 => (0, -1),
        4 => (1, 0),
        _ => (-1, 0),
    };

    /// <summary>The side a stair climbs toward (east 0, west 1, south 2, north 3).</summary>
    private static (int X, int Z) StairClimb(int data) => (data & 3) switch
    {
        0 => (1, 0),
        1 => (-1, 0),
        2 => (0, 1),
        _ => (0, -1),
    };

    /// <summary>The axis a log lies along, as a unit offset (along x 4, along z 8), or none for upright.</summary>
    private static (int X, int Z) LogAxis(int data) => (data & 12) switch
    {
        4 => (1, 0),
        8 => (0, 1),
        _ => (0, 0),
    };

    /// <summary>
    /// <b>Every image of a fanned made thing points at the image of what the authored one points at.</b> A
    /// ladder keeps its back to the image of its wall, a stair keeps climbing into it and a log keeps lying
    /// along the line to it — under both reflections, the half-turn, and all four images of a quarter-turn.
    /// Each block is judged by where its data points in the painted world, not against a transcribed table,
    /// so a mirror that swaps east for west and a quarter-turn that swaps the axes are both caught.
    /// </summary>
    [Test]
    [Arguments("mirror_x")]
    [Arguments("mirror_z")]
    [Arguments("rot_180")]
    [Arguments("rot_90")]
    public async Task Every_image_of_a_fanned_made_thing_points_at_the_image_of_its_wall(string mode)
    {
        var world = Painted(Thing(mode, mirrors: true, Pointing()));

        for (var image = 0; image < Symmetry.Order(mode); image++)
        {
            (int X, int Z) Image((int X, int Z) cell) => Symmetry.Cell(cell.X, cell.Z, mode, 0, 0, image);
            var wall = Image(WallCell);
            var ladder = At(world, Image(LadderCell));
            var stair = At(world, Image(StairCell));
            var log = At(world, Image(LogCell));

            await Assert.That((mode, image, At(world, wall).Id, ladder.Id, stair.Id, log.Id))
                .IsEqualTo((mode, image, Wall, Blocks.Ladder, Blocks.OakStairs, Blocks.Log));
            await Assert.That((mode, image, "ladder", LadderBack(ladder.Data)))
                .IsEqualTo((mode, image, "ladder", Offset(Image(LadderCell), wall)));
            await Assert.That((mode, image, "stair", StairClimb(stair.Data)))
                .IsEqualTo((mode, image, "stair", Offset(Image(StairCell), wall)));
            var toWall = Offset(Image(LogCell), wall);
            await Assert.That((mode, image, "log", LogAxis(log.Data)))
                .IsEqualTo((mode, image, "log", (Math.Abs(toWall.X), Math.Abs(toWall.Z))));
        }
    }

    /// <summary>
    /// <b>The turn is the fan's, never the fold's.</b> A thing the author drew a second time on the far half,
    /// in a group that does not mirror, already points where it should, and is written as drawn: folding its
    /// cells into the primary image and turning them from there would turn it a second time, away from the
    /// wall it was drawn against.
    /// </summary>
    [Test]
    public async Task A_thing_drawn_on_the_far_half_of_an_unmirrored_group_is_painted_as_drawn()
    {
        const int LadderLookingSouth = 3;
        var wall = Symmetry.Cell(WallCell.X, WallCell.Z, "rot_180", 0, 0, 1);
        var ladderCell = (wall.X, wall.Z + 1);
        var world = Painted(Thing("rot_180", mirrors: false,
            Block("wall", wall, Wall, 0),
            Block("ladder", ladderCell, Blocks.Ladder, LadderLookingSouth)));

        await Assert.That(At(world, ladderCell)).IsEqualTo((Blocks.Ladder, LadderLookingSouth));
        await Assert.That(LadderBack(At(world, ladderCell).Data)).IsEqualTo(Offset(ladderCell, wall));
    }

    /// <summary>The footprint an interior is judged against is the layer's: the same two-block shape has a
    /// middle when it stands inside ground that shares its layer, and none when it stands alone.</summary>
    [Test]
    public async Task The_interior_is_read_against_the_layer_and_not_the_shape()
    {
        var findings = TerrainThemeScope.Check(Layout(
            new Dictionary<string, JsonElement> { ["kerb"] = Kerbed() }, null,
            Rect("plaza", -6, -6, 8, 8, null),
            Rect("inlay", 0, 0, 1, 1, "kerb")));
        await Assert.That(findings.Count).IsEqualTo(0);
    }
}
