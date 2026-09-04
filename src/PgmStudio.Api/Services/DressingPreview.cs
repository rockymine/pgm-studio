using System.Text.Json.Nodes;
using PgmStudio.Contracts;
using PgmStudio.Geom.Algorithms;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;
using PgmStudio.Minecraft.Views;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Painting;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Api.Services;

/// <summary>
/// What a prop looks like, drawn by placing it. A sample patch of ground is built, painted with a theme and
/// then run through the <see cref="Decorator"/> itself — so a picture is not a rendering of the knobs but the
/// pass's own output, and a knob that does nothing in the export does nothing here either.
///
/// <para>Two views, because a prop varies along two axes and no one view shows both. From <b>above</b> a
/// path's paving and an area's density read — where the gravel thins, where a flower patch gathers. From the
/// <b>side</b> the props' own shapes read — a tree's silhouette, the way a boulder stands on the ground —
/// neither of which is visible in a top-down cell.</para>
///
/// <para>The side view is a <b>projection</b>, not a cut (<see cref="BlockSideView"/>): every row is looked
/// through and the nearest block wins, shaded by how far back it stands. A single row through a tree meets its
/// crown wherever that row happens to fall — as often through the air between leaf clusters as through them —
/// so a cut comes out speckled and missing pieces that are plainly there. It is the same model the
/// build-height side view draws a map with, keeping the block's own colour instead of a stone ramp.</para>
/// </summary>
public static class DressingPreview
{
    private const int Span = 56;          // blocks across the sample
    private const int GroundTop = 8;      // the sample's first air course
    private const int SkyCourses = 40;    // how far above the ground anything could reach
    private const int GroundShown = 3;    // courses of ground under the props, so they read as seated on it
    private const int SkyMargin = 2;      // courses of clear air above the tallest thing, so it is not cropped
    private const int CardSpan = 40;      // blocks across a picker's card of a stroke seen from above

    /// <summary>The lowest course a side view draws — a little ground under the props, so they read as seated
    /// on it rather than floating. Shared by every view, which is what puts every card's floor on one line.</summary>
    private const int GroundFrom = GroundTop - GroundShown;

    /// <summary>How far in from the sample's border the ground begins. The painter reads a footprint's
    /// perimeter as its edge and finishes it as one — a rim course over a wall — so the outermost ring of the
    /// patch is the sample's own boundary rather than ground a prop could stand on. Both views look inside it:
    /// from above it framed every picture in rim, and from the side it was the entire front face, walling off
    /// the grass the prop is actually planted in.</summary>
    private const int Edge = 1;

    /// <summary>The first and last column of ground in a sample <paramref name="span"/> blocks across.</summary>
    private static (int From, int To) Inside(int span) => (Edge, span - 1 - Edge);

    /// <summary>Both views of <paramref name="prop"/> over a sample patch finished with <paramref name="theme"/>.
    /// The prop is re-centred on the sample first, so a card shows the prop rather than wherever on the map it
    /// happens to stand.</summary>
    public static DressingPreviewDto Views(PlacedProp prop, TerrainTheme theme, int cell = 5)
    {
        var (plan, section, counts) = Rasters(prop, theme, cell);
        return new DressingPreviewDto(plan.Svg(), section.Svg(), counts);
    }

    /// <summary>One view of a placed prop as PNG bytes — <c>plan</c> from above, <c>section</c> from the
    /// side — or null for a view name that is neither. The same dressed patch <see cref="Views"/> draws.</summary>
    public static byte[]? Png(PlacedProp prop, TerrainTheme theme, string view, int scale = 1)
    {
        var (plan, section, _) = Rasters(prop, theme, Cell);
        return view switch
        {
            "plan" => plan.Scaled(scale).Png(),
            "section" => section.Scaled(scale).Png(),
            _ => null,
        };
    }

    /// <summary>Pixels a block takes in a rastered view before the caller's scale.</summary>
    private const int Cell = 5;

    /// <summary>The views <see cref="Png"/> answers, first being the one it draws unasked.</summary>
    public static readonly string[] PngViews = ["plan", "section"];

    /// <summary>Both views as the pictures they are, encoding unchosen — plus what the pass placed. One
    /// dressed patch feeds both, so the two encodings can never disagree about what stood on it.</summary>
    private static (CellRaster Plan, CellRaster Section, DressingCountsDto Counts) Rasters(
        PlacedProp prop, TerrainTheme theme, int cell)
    {
        // A marker is a few blocks across and an area is dozens, so one sample size would draw either a rock
        // as four pixels or a meadow cropped to a corner. The patch is sized to the prop.
        var span = SpanFor(prop);
        var (world, placed) = Dressed([Centred(prop, span / 2, span / 2)], theme, span);
        var (from, to) = Inside(span);

        // From above: each cell's highest block, so paving, plants, rock and canopy all show where they fall.
        var plan = new CellRaster(to - from + 1, to - from + 1, cell, (x, z) => Highest(world, from + x, from + z));

        // Seen from the side, cropped to what is actually there — a fixed sky would draw a path as one grey
        // line under forty courses of nothing, and the point of the view is the prop's proportions.
        var view = BlockSideView.Project(world, from, to, from, to, GroundFrom, GroundTop + SkyCourses);
        var section = SectionRaster(view, TopCourse(view), cell);

        return (plan, section,
            new DressingCountsDto(placed.Plants, placed.Boulders, placed.Trees, placed.PathCells, placed.WaterCells));
    }

    /// <summary>The six path styles at card size, each drawn by paving the same stroke — a picker showing
    /// hand-drawn icons could promise a look the pass does not produce.</summary>
    public static IReadOnlyList<PropOptionDto> StrokeStyleCards(StrokeProp template, TerrainTheme theme, int cell = 3)
        => PlanCards(theme, cell, [.. Enum.GetValues<StrokeStyle>().Select(style => (
            Key: style.ToString().ToLowerInvariant(),
            Label: StrokeStyleLabels[style],
            Prop: (PlacedProp)(template with { Style = style, Points = CardStroke }),
            Defaults: (string?)null))]);

    /// <summary>The three channel forms at card size, each an actual dug channel seen from above — where its
    /// banks run clean, wander or taper reads in the outline, which is what the picker is choosing between.</summary>
    public static IReadOnlyList<PropOptionDto> WaterFormCards(WaterProp template, TerrainTheme theme, int cell = 3)
        => PlanCards(theme, cell, [.. Enum.GetValues<ChannelForm>().Select(form => (
            Key: form.ToString().ToLowerInvariant(),
            Label: form.ToString(),
            Prop: (PlacedProp)(template with { Form = form, Points = CardStroke }),
            Defaults: (string?)null))]);

    /// <summary>The four boulder forms at card size, each an actual rock.</summary>
    public static IReadOnlyList<PropOptionDto> BoulderFormCards(BoulderProp template, TerrainTheme theme, int cell = 3)
        => SectionCards(theme, cell, [.. Enum.GetValues<BoulderForm>().Select(form => (
            Key: form.ToString().ToLowerInvariant(),
            Label: form.ToString(),
            Prop: (PlacedProp)(template with { Style = template.Style with { Form = form } }),
            Defaults: (string?)null))]);

    /// <summary>Every vanilla species, built at card size — so a picker cannot name a tree the pass could not
    /// build. The card carries the proportions it drew, so picking a species takes its natural height and the
    /// client never has to keep a second copy of the species table.</summary>
    public static IReadOnlyList<PropOptionDto> SpeciesCards(TerrainTheme theme, int cell = 2)
        => SectionCards(theme, cell, [.. DressingPalette.Species.Select(species => (
            Key: species.Name,
            Label: species.Name,
            Prop: (PlacedProp)new TreeProp { Seed = 5, Style = new TreeStyle { Form = TreeForm.Template, Species = species.Name, Height = species.Height } },
            Defaults: (string?)new JsonObject
            {
                ["species"] = species.Name, ["height"] = species.Height,
            }.ToJsonString()))]);

    /// <summary>The six woods, each shown as the <em>same</em> grown tree. A wood is a material and nothing
    /// else, so the cards differ only in colour — which is precisely the claim being made, and the reason
    /// these are not the species picker: a grown tree has no species.</summary>
    public static IReadOnlyList<PropOptionDto> WoodCards(TreeProp template, TerrainTheme theme, int cell = 2)
        => SectionCards(theme, cell, [.. DressingPalette.Woods.Select(wood => (
            Key: wood.Name,
            Label: wood.Name,
            Prop: (PlacedProp)(template with { Style = template.Style with { Form = TreeForm.Grown, Wood = wood.Name } }),
            Defaults: (string?)null))]);

    private static readonly IReadOnlyDictionary<StrokeStyle, string> StrokeStyleLabels = new Dictionary<StrokeStyle, string>
    {
        [StrokeStyle.Solid] = "Solid", [StrokeStyle.Worn] = "Worn", [StrokeStyle.Rough] = "Rough edge",
        [StrokeStyle.Stones] = "Stepping stones", [StrokeStyle.Tapered] = "Tapered",
    };

    // A bent stroke, so a card shows what a style does through a turn as well as along a straight. Centred on
    // the patch's own middle (z ≈ CardSpan/2), because the card samples the middle band of the patch — a stroke
    // drawn near the top edge is cropped rather than shown.
    private static readonly double[][] CardStroke = [[4, 22], [14, 16], [26, 24], [36, 18]];

    /// <summary>How wide a sample a prop needs to read: enough ground around a marker to see its shape and its
    /// footing, and enough of an area to see its density field do anything.
    ///
    /// <para>Sized from the prop's <em>bounded</em> reach rather than its raw field. The patch is built column
    /// by column and then painted, so its cost is quadratic in this number: a height that arrived out of range
    /// would cut a sample a thousand blocks across, which is a preview that never returns rather than an odd
    /// picture. Every other reader of these knobs bounds them, and the sample has to agree or the bound is
    /// only half applied.</para></summary>
    private static int SpanFor(PlacedProp prop) => prop switch
    {
        TreeProp tree => Math.Max(24, (int)tree.Style.Reach + 8),
        BoulderProp boulder => Math.Max(16, (int)(boulder.Style.Reach * 5)),
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

    /// <summary>A set of options drawn from above — half as deep as they are wide, since a stroke runs across
    /// a card rather than filling it.</summary>
    private static IReadOnlyList<PropOptionDto> PlanCards(
        TerrainTheme theme, int cell, IReadOnlyList<(string Key, string Label, PlacedProp Prop, string? Defaults)> options)
        => [.. options.Select(option =>
        {
            var (world, _) = Dressed([option.Prop], theme, CardSpan);
            var (from, to) = Inside(CardSpan);
            var svg = SvgRaster.Raster(to - from + 1, CardSpan / 2, cell,
                (x, z) => Highest(world, from + x, from + z + CardSpan / 4));
            return new PropOptionDto(option.Key, option.Label, svg, option.Defaults);
        })];

    /// <summary>A set of options drawn from the side — the view a rock's burial and a tree's silhouette need.
    ///
    /// <para>Every card in a set is drawn on <b>one</b> patch, cropped to the <b>same</b> courses: the widest
    /// option sizes the sample, the tallest decides the top and the ground decides the bottom, so the cards sit
    /// in one grid with their floors on one line and their heights honestly compared. Cropping each to its own
    /// content is what made a spruce and an acacia look the same size on two cards whose floors were nowhere
    /// near each other. Sizing the patch to the set is what keeps a two-block rock from being four pixels in a
    /// sample cut for a tree.</para></summary>
    private static IReadOnlyList<PropOptionDto> SectionCards(
        TerrainTheme theme, int cell, IReadOnlyList<(string Key, string Label, PlacedProp Prop, string? Defaults)> options)
    {
        var span = options.Max(option => SpanFor(option.Prop));
        var (from, to) = Inside(span);
        var views = options
            .Select(option => Dressed([Centred(option.Prop, span / 2, span / 2)], theme, span).World)
            .Select(world => BlockSideView.Project(world, from, to, from, to, GroundFrom, GroundTop + SkyCourses))
            .ToList();

        var top = views.Max(TopCourse);
        return [.. options.Select((option, at) =>
            new PropOptionDto(option.Key, option.Label, SectionSvg(views[at], top, cell), option.Defaults))];
    }

    /// <summary>One side view as an SVG, drawn top course down. The raster's rows run down and a course runs
    /// up, which is the one flip in the whole view.</summary>
    private static string SectionSvg(SideViewProjection view, int top, int cell)
        => SectionRaster(view, top, cell).Svg();

    private static CellRaster SectionRaster(SideViewProjection view, int top, int cell)
        => new(view.Columns, top - GroundFrom + 1, cell, (column, screenRow) =>
            view.At(view.FromX + column, top - screenRow) is { } block
                ? BlockPalette.Hex(block.Id, block.Data, block.Depth)
                : null);

    /// <summary>The course a view is cropped at: a little clear air above the tallest thing it carries.</summary>
    private static int TopCourse(SideViewProjection view) => (view.Highest() ?? GroundTop) + SkyMargin;

    /// <summary>The prop moved so its own middle sits at (<paramref name="x"/>, <paramref name="z"/>) — a card
    /// shows the prop, not the corner of the map it was placed in.</summary>
    private static PlacedProp Centred(PlacedProp prop, int x, int z) => prop switch
    {
        TreeProp tree => tree with { X = x, Z = z },
        BoulderProp boulder => boulder with { X = x, Z = z },
        StrokeProp path => path with { Points = Recentre(path.Points, x, z) },
        WaterProp water => water with { Points = Recentre(water.Points, x, z) },
        FloraProp area => area with { Points = Recentre(area.Points, x, z) },
        HouseProp house => house with { Wings = RecentreWings(house.Wings, x, z) },
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

    /// <summary>The same recentring, over every wing at once: the delta is measured across all of them so a
    /// multi-wing building keeps its own shape, and then every wing's own two corners are carried by it. What
    /// each wing <em>states</em> is untouched — moving a building does not reroof it.</summary>
    private static List<AuthoredWing> RecentreWings(IReadOnlyList<AuthoredWing> wings, int x, int z)
    {
        if (wings.Count == 0) return [];
        var corners = wings.SelectMany(wing => wing.Corners).ToList();
        double minX = corners.Min(point => point[0]), maxX = corners.Max(point => point[0]);
        double minZ = corners.Min(point => point[1]), maxZ = corners.Max(point => point[1]);
        double dx = x - (minX + maxX) / 2, dz = z - (minZ + maxZ) / 2;
        return [.. wings.Select(wing => wing with
        {
            Corners = [.. wing.Corners.Select(point => new[] { point[0] + dx, point[1] + dz })],
        })];
    }

    /// <summary>A flat sample patch, painted and then dressed by the real passes. Flat on purpose: a preview is
    /// asking what a prop looks like, and terrain of its own would only confuse that with what the terrain
    /// does.</summary>
    private static (VoxelWorld World, DressingPlacement Placed) Dressed(
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
