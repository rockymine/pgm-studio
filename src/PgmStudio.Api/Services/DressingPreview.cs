using System.Text.Json;
using PgmStudio.Contracts;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Api.Services;

/// <summary>
/// What a prop looks like, drawn by placing it. A sample patch of ground is built, painted with a theme and
/// then run through the <see cref="Decorator"/> itself — so a picture is not a rendering of the knobs but the
/// pass's own output, and a knob that does nothing in the export does nothing here either.
///
/// <para>Two views, because a prop varies along two axes and no one view shows both. From <b>above</b> a
/// path's paving and an area's density read — where the gravel thins, where a flower patch gathers. From the
/// <b>side</b> the props' own shapes read — a tree's silhouette, a boulder's half-buried profile — neither of
/// which is visible in a top-down cell.</para>
/// </summary>
public static class DressingPreview
{
    private const int Span = 56;          // blocks across the sample
    private const int GroundTop = 8;      // the sample's first air course
    private const int SkyCourses = 40;    // how far above the ground anything could reach
    private const int GroundShown = 3;    // courses of ground under the props, so they read as seated on it
    private const int SkyMargin = 2;      // courses of clear air above the tallest thing, so it is not cropped
    private const int CardSpan = 40;      // blocks across a picker's card

    /// <summary>Both views of <paramref name="prop"/> over a sample patch finished with <paramref name="theme"/>.
    /// The prop is re-centred on the sample first, so a card shows the prop rather than wherever on the map it
    /// happens to stand.</summary>
    public static DressingPreviewDto Views(PlacedProp prop, TerrainTheme theme, int cell = 5)
    {
        // A marker is a few blocks across and an area is dozens, so one sample size would draw either a rock
        // as four pixels or a meadow cropped to a corner. The patch is sized to the prop.
        var span = SpanFor(prop);
        var (world, tally) = Dressed([Centred(prop, span / 2, span / 2)], theme, span);
        var row = span / 2;

        // From above: each cell's highest block, so paving, plants, rock and canopy all show where they fall.
        var plan = SvgRaster.Raster(span, span, cell, (x, z) => Highest(world, x, z));

        // Cut open along its middle row. Cropped to what is actually there — a fixed sky would draw a path as
        // one grey line under forty courses of nothing, and the point of the view is the prop's proportions.
        var (from, to) = SectionRange(world, row, span);
        var section = SvgRaster.Raster(span, to - from + 1, cell, (x, screenRow) =>
        {
            var (id, data) = world.GetBlock(x, to - screenRow, row);   // the raster runs down, a course runs up
            return id == Blocks.Air ? null : BlockPalette.Hex(id, data);
        });

        return new DressingPreviewDto(plan, section,
            new DressingCountsDto(tally.Plants, tally.Boulders, tally.Trees, tally.PathCells));
    }

    /// <summary>The six path styles at card size, each drawn by paving the same stroke — a picker showing
    /// hand-drawn icons could promise a look the pass does not produce.</summary>
    public static IReadOnlyList<PropOptionDto> PathStyleCards(PathProp template, TerrainTheme theme, int cell = 3)
        => [.. Enum.GetValues<PathStyle>().Select(style => new PropOptionDto(
            Key: style.ToString().ToLowerInvariant(),
            Label: PathStyleLabels[style],
            Svg: PlanCard(template with { Style = style, Points = CardStroke }, theme, cell)))];

    /// <summary>The four boulder forms at card size, each an actual rock.</summary>
    public static IReadOnlyList<PropOptionDto> BoulderFormCards(BoulderProp template, TerrainTheme theme, int cell = 3)
        => [.. Enum.GetValues<BoulderForm>().Select(form => new PropOptionDto(
            Key: form.ToString().ToLowerInvariant(),
            Label: form.ToString(),
            Svg: SectionCard(template with { Form = form }, theme, cell)))];

    /// <summary>Every species the grower knows, each grown at card size — so a picker cannot name a tree the
    /// pass could not build.</summary>
    public static IReadOnlyList<PropOptionDto> SpeciesCards(TerrainTheme theme, int cell = 2)
        => [.. DressingPalette.Species.Select(species =>
        {
            var tree = new TreeProp
            {
                Species = species.Name, Height = species.Height, Leader = species.Leader,
                BranchAngle = species.BranchAngle, Levels = species.Levels, LeafSize = species.LeafSize,
                Seed = 5,
            };
            // The card carries the shape it drew, so picking a species takes its proportions and the client
            // never has to keep a second copy of the species table.
            var defaults = JsonSerializer.Serialize(new
            {
                species = species.Name, height = species.Height, leader = species.Leader,
                branchAngle = species.BranchAngle, levels = species.Levels, leafSize = species.LeafSize,
            }, DressingJson.Options);
            return new PropOptionDto(species.Name, species.Name, SectionCard(tree, theme, cell), defaults);
        })];

    private static readonly IReadOnlyDictionary<PathStyle, string> PathStyleLabels = new Dictionary<PathStyle, string>
    {
        [PathStyle.Solid] = "Solid", [PathStyle.Worn] = "Worn", [PathStyle.Rough] = "Rough edge",
        [PathStyle.Cobble] = "Cobbled", [PathStyle.Stones] = "Stepping stones", [PathStyle.Tapered] = "Tapered",
    };

    // A bent stroke, so a card shows what a style does through a turn as well as along a straight.
    private static readonly double[][] CardStroke = [[4, 14], [14, 8], [26, 16], [36, 10]];

    /// <summary>How wide a sample a prop needs to read: enough ground around a marker to see its shape and its
    /// footing, and enough of an area to see its density field do anything.</summary>
    private static int SpanFor(PlacedProp prop) => prop switch
    {
        TreeProp tree => Math.Max(24, (int)tree.Height + 8),
        BoulderProp boulder => Math.Max(16, (int)(boulder.Size * 5)),
        _ => Span,
    };

    private static string? Highest(VoxelWorld world, int x, int z)
    {
        for (var y = GroundTop + SkyCourses; y >= 1; y--)
        {
            var (id, data) = world.GetBlock(x, y, z);
            if (id != Blocks.Air) return BlockPalette.Hex(id, data);
        }
        return null;
    }

    /// <summary>One option drawn from above at card size — half as deep as it is wide, since a stroke runs
    /// across a card rather than filling it.</summary>
    private static string PlanCard(PlacedProp prop, TerrainTheme theme, int cell)
    {
        var (world, _) = Dressed([prop], theme, CardSpan);
        var band = CardSpan / 2;
        return SvgRaster.Raster(CardSpan, band, cell, (x, z) => Highest(world, x, z + CardSpan / 4));
    }

    /// <summary>One option drawn from the side at card size — the view a rock's burial and a tree's silhouette
    /// need, and the reason those two pickers do not use <see cref="PlanCard"/>.</summary>
    private static string SectionCard(PlacedProp prop, TerrainTheme theme, int cell)
    {
        var (world, _) = Dressed([Centred(prop, CardSpan / 2, CardSpan / 2)], theme, CardSpan);
        var row = CardSpan / 2;
        var (from, to) = SectionRange(world, row, CardSpan);
        return SvgRaster.Raster(CardSpan, to - from + 1, cell, (x, screenRow) =>
        {
            var (id, data) = world.GetBlock(x, to - screenRow, row);
            return id == Blocks.Air ? null : BlockPalette.Hex(id, data);
        });
    }

    /// <summary>The prop moved so its own middle sits at (<paramref name="x"/>, <paramref name="z"/>) — a card
    /// shows the prop, not the corner of the map it was placed in.</summary>
    private static PlacedProp Centred(PlacedProp prop, int x, int z) => prop switch
    {
        TreeProp tree => tree with { X = x, Z = z },
        BoulderProp boulder => boulder with { X = x, Z = z },
        PathProp path => path with { Points = Recentre(path.Points, x, z) },
        FloraProp area => area with { Points = Recentre(area.Points, x, z) },
        _ => prop,
    };

    private static List<double[]> Recentre(IReadOnlyList<double[]> points, int x, int z)
    {
        if (points.Count == 0) return [];
        double minX = points.Min(point => point[0]), maxX = points.Max(point => point[0]);
        double minZ = points.Min(point => point[1]), maxZ = points.Max(point => point[1]);
        double dx = x - (minX + maxX) / 2, dz = z - (minZ + maxZ) / 2;
        return [.. points.Select(point => new[] { point[0] + dx, point[1] + dz })];
    }

    /// <summary>The courses the section draws: a few of ground for the props to sit on, up to a little clear
    /// air above the tallest thing the row carries. A path gets a shallow band and a tree a tall one, which is
    /// the only way both read at the same scale.</summary>
    private static (int From, int To) SectionRange(VoxelWorld world, int row, int span)
    {
        var highest = GroundTop;
        for (var y = GroundTop + SkyCourses; y >= GroundTop; y--)
        {
            var any = false;
            for (var x = 0; x < span && !any; x++) any = world.GetBlock(x, y, row).Id != Blocks.Air;
            if (any) { highest = y; break; }
        }
        return (Math.Max(0, GroundTop - GroundShown), highest + SkyMargin);
    }

    /// <summary>A flat sample patch, painted and then dressed by the real passes. Flat on purpose: a preview is
    /// asking what a prop looks like, and terrain of its own would only confuse that with what the terrain
    /// does.</summary>
    private static (VoxelWorld World, DressingTally Tally) Dressed(
        IReadOnlyList<PlacedProp> props, TerrainTheme theme, int span = Span)
    {
        var world = new VoxelWorld();
        var surface = new Dictionary<(int X, int Z), int>(span * span);
        for (var z = 0; z < span; z++)
        for (var x = 0; x < span; x++)
        {
            for (var y = 0; y < GroundTop; y++) world.SetBlock(x, y, z, Blocks.Stone);
            surface[(x, z)] = GroundTop;
        }

        TerrainPainter.Paint(world, surface, theme);
        return (world, Decorator.Decorate(world, new DressingContext(surface, props)));
    }
}
