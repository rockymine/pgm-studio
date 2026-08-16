using PgmStudio.Contracts;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Views;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Api.Services;

/// <summary>
/// The library's pictures: what one style paints, and what a whole theme finishes terrain into. Everything here
/// resolves through the real <see cref="TerrainMaterial"/> / <see cref="TerrainPainter"/> and the export's own
/// <see cref="BlockPalette"/>, so a card cannot promise a block the export would not place.
///
/// <para>Two views, because a material varies along two different axes and neither view shows both. The
/// <b>plan</b> view samples one course from above — the axes a voronoi, a noise field and a wall run vary along,
/// which is why those read at a glance and a layer stack renders as one flat colour there. The <b>section</b>
/// view samples one row of columns downward, which is the axis a stack varies along, so grass-over-two-dirt is
/// three bands rather than a green square. <see cref="CardSvg"/> picks the view that shows a given kind
/// something.</para>
///
/// <para>A theme is neither: it is the geometry decision (which bucket claims which course) as much as the
/// materials, so <see cref="ThemeSectionSvg"/> paints a sample plateau through the real painter and cuts it
/// open. The sample is the smallest terrain that exercises every bucket at once — see
/// <see cref="SampleTerrain"/>.</para>
/// </summary>
public static class StylePreview
{
    /// <summary>The team a preview samples: red, on the same 0–15 damage scale a wool/clay tint reads.</summary>
    private const int SampleTeam = 14;

    /// <summary>
    /// A material seen from above over a square of ground, one course deep. The left half samples neutral land
    /// and the right half a team cell, so a tint shows both its colour and its neutral fallback.
    ///
    /// <para><b>The geometry is measured, not synthesised.</b> The square is traced and walked exactly as a
    /// real island is — <see cref="Geometry"/> — so the arc, the bend, the run direction and the inset a cell
    /// reports are the ones a footprint of that shape actually produces. It used to hand every cell a fake arc
    /// (its x column) and nothing else, which drew a wall run as stripes marching across open ground it would
    /// never stripe, and drew a frame and a laid log as nothing at all: both read the bend, which was always
    /// zero. An honest swatch shows a wall material hugging the border and flat in the middle, which is the
    /// fact about it worth knowing before it is built.</para></summary>
    public static string PlanSvg(TerrainMaterial material, TerrainBucket bucket = TerrainBucket.Surface,
        int columns = 32, int cell = 4)
        => PlanRaster(material, bucket, columns, cell).Svg();

    /// <summary>The plan view as the picture it is, before an encoding is chosen — <see cref="CellRaster"/>
    /// is what lets the SVG a card shows and the PNG an agent asks for be one derivation.</summary>
    public static CellRaster PlanRaster(TerrainMaterial material, TerrainBucket bucket = TerrainBucket.Surface,
        int columns = 32, int cell = 4)
    {
        var (arc, turn, run, inset) = Geometry(columns);
        return new CellRaster(columns, columns, cell, (x, z) =>
        {
            var (id, data) = material.Resolve(new BucketContext(
                x, 0, z, bucket, DepthFromTop: 0, TeamOf(x, columns),
                arc.GetValueOrDefault((x, z), -1), HeightFromBottom: 0,
                turn.GetValueOrDefault((x, z), 0), run.GetValueOrDefault((x, z), 0),
                inset.GetValueOrDefault((x, z), -1)));
            return BlockPalette.Hex(id, data);
        });
    }

    /// <summary>The swatch square's own outline and inward walk, measured once per picture through the same
    /// <see cref="Geom.Algorithms.GridBoundary"/> the painter's classifier reads — one derivation, so a swatch
    /// cannot answer a cell differently from the world.</summary>
    private static (Dictionary<(int X, int Z), int> Arc, Dictionary<(int X, int Z), int> Turn,
                    Dictionary<(int X, int Z), int> Run, Dictionary<(int X, int Z), int> Inset) Geometry(int columns)
    {
        var cells = new HashSet<(int X, int Z)>();
        for (var x = 0; x < columns; x++)
            for (var z = 0; z < columns; z++) cells.Add((x, z));

        var arc = Geom.Algorithms.GridBoundary.TracePerimeter(cells);
        var window = Geom.Algorithms.GridBoundary.CornerWindow;
        var turn = Geom.Algorithms.GridBoundary.Turns(arc, window)
            .ToDictionary(entry => entry.Key, entry => (int)Math.Round(entry.Value));
        var run = Geom.Algorithms.GridBoundary.Runs(arc, window);
        return (arc, turn, run, Geom.Algorithms.GridBoundary.StepsInward(cells));
    }

    /// <summary>A material cut open downward: each row is one course deeper into the band, so a layer stack's
    /// thicknesses are the bands they are. Same neutral | team split across the columns as the plan view.</summary>
    public static string SectionSvg(TerrainMaterial material, TerrainBucket bucket = TerrainBucket.Surface,
        int columns = 32, int courses = 10, int cell = 4)
        => SectionRaster(material, bucket, columns, courses, cell).Svg();

    /// <summary>The cut-open view as its raster, the <see cref="PlanRaster"/> sibling.</summary>
    public static CellRaster SectionRaster(TerrainMaterial material, TerrainBucket bucket = TerrainBucket.Surface,
        int columns = 32, int courses = 10, int cell = 4)
        => new(columns, courses, cell, (x, depth) =>
        {
            // A section is a straight wall seen face on, so its geometry is honest by construction: walking
            // along it the arc advances with x, it never bends, and it runs along x the whole way — which is
            // what a laid log needs to lie down at all. The inset across a cut is the distance to its nearer
            // end, the bands a ringed shape shows when it is sliced through the middle. The bottom row is
            // height zero rather than one, so a material that inks its bottom course inks the picture's.
            var (id, data) = material.Resolve(new BucketContext(
                x, courses - 1 - depth, 0, bucket, depth, TeamOf(x, columns),
                PerimeterArc: x, HeightFromBottom: courses - 1 - depth,
                PerimeterTurn: 0, PerimeterRun: Geom.Algorithms.GridBoundary.RunAlongX,
                Inset: Math.Min(x, columns - 1 - x)));
            return BlockPalette.Hex(id, data);
        });

    /// <summary>An area pattern is a <em>field</em>, so a swatch has to be wide enough to hold several of
    /// whatever it repeats — a voronoi with ten-block cells shown over twenty blocks is two cells and reads as a
    /// blotch. These sample four times the ground at half the pixel size, which keeps the picture the same size
    /// on the card while showing enough of the pattern to recognise it.</summary>
    private static bool IsAreaPattern(string kind) =>
        kind is MaterialKind.Voronoi or MaterialKind.Cell
             or MaterialKind.Noise or MaterialKind.Turbulence or MaterialKind.Electric;

    /// <summary>The kinds that vary along the perimeter <em>rather than</em> across the ground — a wall run's
    /// stripes, a frame's inked courses, a log lying along the face. Read off the vocabulary rather than listed
    /// again, so a kind added to the painter lands in the right view without a second table agreeing to it.
    /// Reading position as well disqualifies it: a checkerboard tiles the face on a wall and the ground
    /// everywhere else, and the ground is the case worth showing.</summary>
    private static bool IsWallPattern(string kind) =>
        MaterialVocabulary.Of(kind) is { } info
        && info.Reads.Any(fact => fact is CellFact.Arc or CellFact.Bend)
        && !info.Reads.Contains(CellFact.Position);

    /// <summary>The one view that shows a given kind what it does. A layer stack varies with depth, so it gets
    /// the section. A wall pattern varies along a perimeter, so it gets the section too — <b>an elevation is
    /// what a wall material is seen as</b>, and now that the plan swatch traces its square honestly, a wall run
    /// drawn in plan is a striped border round a flat middle: true, and useless for choosing between two of
    /// them. Everything else varies across the ground and gets the plan view.</summary>
    public static string CardSvg(string kind, TerrainMaterial material)
        => kind == MaterialKind.Layered || IsWallPattern(kind)
            ? SectionSvg(material, columns: 24, courses: 12, cell: 5)
         : IsAreaPattern(kind) ? PlanSvg(material, columns: 60, cell: 2)
         : PlanSvg(material, columns: 24, cell: 5);

    /// <summary>Both views of one material, for an editor previewing an edit as it is made.</summary>
    public static MaterialPreviewDto Views(TerrainMaterial material)
        => IsAreaPattern(TerrainThemeComposer.KindOf(material))
            ? new(PlanSvg(material, columns: 72, cell: 2), SectionSvg(material, columns: 72, courses: 24, cell: 2))
            : new(PlanSvg(material), SectionSvg(material));

    /// <summary>Both views of a serialized material.</summary>
    public static MaterialPreviewDto Views(string materialJson)
        => Views(TerrainThemeJson.DeserializeMaterial(materialJson));

    /// <summary>A whole theme: the sample plateau cut open, plus one plan swatch per themeable bucket.</summary>
    public static ThemePreviewDto ThemeViews(TerrainTheme theme) => new(
        ThemeSectionSvg(theme),
        new Dictionary<string, string>
        {
            [ThemeBuckets.Rim] = PlanSvg(theme.MaterialFor(TerrainBucket.Rim), TerrainBucket.Rim),
            [ThemeBuckets.Surface] = PlanSvg(theme.MaterialFor(TerrainBucket.Surface), TerrainBucket.Surface),
            [ThemeBuckets.Wall] = PlanSvg(theme.MaterialFor(TerrainBucket.Wall), TerrainBucket.Wall),
            [ThemeBuckets.Fill] = PlanSvg(theme.MaterialFor(TerrainBucket.Fill), TerrainBucket.Fill),
        });

    /// <summary>A serialized theme's views.</summary>
    public static ThemePreviewDto ThemeViews(string themeJson) => ThemeViews(TerrainThemeJson.Deserialize(themeJson));

    /// <summary>The sample plateau painted with <paramref name="theme"/> and cut open along its middle row: what
    /// the theme actually finishes terrain into, geometry decisions included. Each column is resolved by the same
    /// <see cref="TerrainPainter.ColumnBlocks"/> the export writes from, so a rim depth, a disabled wall or a
    /// bedrock floor moves the picture exactly as it moves the world.</summary>
    public static string ThemeSectionSvg(TerrainTheme theme, int cell = 8)
        => ThemeSectionRaster(theme, cell).Svg();

    /// <summary>The theme's cut plateau as its raster, the <see cref="PlanRaster"/> sibling.</summary>
    public static CellRaster ThemeSectionRaster(TerrainTheme theme, int cell = 8)
    {
        var courses = new string?[SampleTerrain.Width, SampleTerrain.Height];
        for (var x = 0; x < SampleTerrain.Width; x++)
        {
            if (!SampleTerrain.Profile.TryGetColumn((x, SampleTerrain.SectionRow), out var column)) continue;
            foreach (var (y, id, data) in TerrainPainter.ColumnBlocks(
                         x, SampleTerrain.SectionRow, column, theme, TeamOf(x, SampleTerrain.Width)))
                if (y >= 0 && y < SampleTerrain.Height) courses[x, y] = BlockPalette.Hex(id, data);
        }
        // The raster's rows run downward and a course index runs upward, so the top row is the tallest course.
        return new CellRaster(SampleTerrain.Width, SampleTerrain.Height, cell,
            (x, row) => courses[x, SampleTerrain.Height - 1 - row]);
    }

    /// <summary>One view of a material as PNG bytes, or null for a view name that is not one — the same two
    /// views <see cref="Views(TerrainMaterial)"/> answers as SVG, at the same sizes.</summary>
    public static byte[]? MaterialPng(TerrainMaterial material, string view)
    {
        var wide = IsAreaPattern(TerrainThemeComposer.KindOf(material));
        return view switch
        {
            "plan" => (wide ? PlanRaster(material, columns: 72, cell: 2) : PlanRaster(material)).Png(),
            "section" => (wide
                ? SectionRaster(material, columns: 72, courses: 24, cell: 2)
                : SectionRaster(material)).Png(),
            _ => null,
        };
    }

    /// <summary>One view of a theme as PNG bytes — <c>section</c> for the cut plateau, or a bucket name for
    /// its swatch — or null for a view name that is neither.</summary>
    public static byte[]? ThemePng(TerrainTheme theme, string view) => view switch
    {
        "section" => ThemeSectionRaster(theme).Png(),
        ThemeBuckets.Rim => PlanRaster(theme.MaterialFor(TerrainBucket.Rim), TerrainBucket.Rim).Png(),
        ThemeBuckets.Surface => PlanRaster(theme.MaterialFor(TerrainBucket.Surface), TerrainBucket.Surface).Png(),
        ThemeBuckets.Wall => PlanRaster(theme.MaterialFor(TerrainBucket.Wall), TerrainBucket.Wall).Png(),
        ThemeBuckets.Fill => PlanRaster(theme.MaterialFor(TerrainBucket.Fill), TerrainBucket.Fill).Png(),
        _ => null,
    };

    // The right half of any preview is team-owned land, the left neutral.
    private static int TeamOf(int x, int columns) => x < columns / 2 ? -1 : SampleTeam;

    /// <summary>
    /// The terrain every theme preview is cut from: two plateaus at different heights, void on both sides, five
    /// rows deep so the middle row's neighbours are real. That is the smallest shape in which all five buckets
    /// appear at once — the outer columns are void-facing edges (rim capping a full-height wall), the step
    /// between the plateaus is a terrain-facing edge (the <c>wallOnTerrainFaces</c> and <c>rimEdges</c> knobs
    /// are visible there and nowhere else: a void-only rim caps the outer columns and leaves the step bare),
    /// the plateau interiors are surface over fill, and the bottom course is bedrock. The profile is classified once by the real <see cref="TerrainProfile"/> rather than asserted, so
    /// the preview's geometry is the export's; it is theme-agnostic, so one instance serves every theme.
    /// </summary>
    private static class SampleTerrain
    {
        public const int Width = 16;
        public const int Depth = 5;
        /// <summary>The tallest course drawn — the high plateau's surface top.</summary>
        public const int Height = 13;
        private const int LowTop = 8;
        private const int StepAt = Width / 2;
        /// <summary>The row the section cuts along: the middle, so every neighbour it reads exists.</summary>
        public const int SectionRow = Depth / 2;

        public static TerrainProfile Profile { get; } = Build();

        private static TerrainProfile Build()
        {
            var world = new VoxelWorld();
            var surfaceTop = new Dictionary<(int X, int Z), int>();
            for (var x = 0; x < Width; x++)
            {
                var top = x < StepAt ? Height : LowTop;
                for (var z = 0; z < Depth; z++)
                {
                    for (var y = 0; y < top; y++) world.SetBlock(x, y, z, Blocks.Stone);
                    surfaceTop[(x, z)] = top;
                }
            }
            return new TerrainProfile(world, surfaceTop);
        }
    }
}
