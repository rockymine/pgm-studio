using PgmStudio.Contracts;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Dressing;

namespace PgmStudio.Api.Services;

/// <summary>
/// What a dressing recipe grows, drawn by growing it. A sample patch of ground is built, painted with a theme
/// and then run through the <see cref="Decorator"/> itself — so the picture is not a rendering of the recipe
/// but the recipe's own output, and a knob that does nothing in the export does nothing here either.
///
/// <para>Two views, for the same reason the style library needs two: dressing varies along two axes and no one
/// view shows both. From <b>above</b> the density fields read — where flora thins into clearings, where flower
/// patches gather, how far apart the boulders and groves fall. From the <b>side</b> the props' own shapes read
/// — a tree's silhouette and a boulder's half-buried profile, neither of which is visible in a top-down cell.</para>
/// </summary>
public static class DressingPreview
{
    private const int PlanSpan = 56;      // blocks across the top-down sample
    private const int SectionSpan = 56;   // blocks along the cut
    private const int SectionDepth = 9;   // rows deep, so a tree's neighbours exist and its crown has room
    private const int GroundTop = 8;      // the sample's first air course
    private const int SkyCourses = 30;    // how far above the ground anything could reach
    private const int GroundShown = 3;    // courses of ground under the props, so they read as seated on it
    private const int SkyMargin = 2;      // courses of clear air above the tallest thing, so it is not cropped

    /// <summary>Both views of a recipe over a sample patch finished with <paramref name="theme"/> — the theme
    /// matters because what the paint leaves on top is exactly what decides whether flora grows at all.
    /// <para>Two samples rather than one: the plan view wants a square of ground to show a scatter's spacing,
    /// and the section wants a long thin strip, since cutting a square open would grow a hundred trees to show
    /// the nine on its middle row.</para></summary>
    public static DressingPreviewDto Views(DressingRecipe recipe, TerrainTheme theme, int cell = 5)
    {
        var plan = Dressed(recipe, theme, PlanSpan, PlanSpan);
        var section = Dressed(recipe, theme, SectionSpan, SectionDepth);
        var row = SectionDepth / 2;

        // From above: each cell's highest block, so plants, rock and canopy all show where they fall.
        var planSvg = SvgRaster.Raster(PlanSpan, PlanSpan, cell, (x, z) =>
        {
            for (var y = GroundTop + SkyCourses; y >= 1; y--)
            {
                var (id, data) = plan.World.GetBlock(x, y, z);
                if (id != Blocks.Air) return BlockPalette.Hex(id, data);
            }
            return null;
        });

        // Cut open along its middle row: the only view a tree's silhouette or a boulder's burial is visible in.
        // Cropped to what is actually there — a fixed sky would draw a meadow as one green line under thirty
        // courses of nothing, and the whole point of the view is the props' proportions.
        var (from, to) = SectionRange(section.World, row);
        var courses = to - from + 1;
        var sectionSvg = SvgRaster.Raster(SectionSpan, courses, cell, (x, screenRow) =>
        {
            var y = to - screenRow;    // the raster's rows run down; a course index runs up
            var (id, data) = section.World.GetBlock(x, y, row);
            return id == Blocks.Air ? null : BlockPalette.Hex(id, data);
        });

        var tally = plan.Tally;
        return new DressingPreviewDto(planSvg, sectionSvg, new DressingCountsDto(tally.Plants, tally.Boulders, tally.Trees));
    }

    /// <summary>The courses the section draws: a few of ground for the props to sit on, up to a little clear
    /// air above the tallest thing the row carries. A recipe of ground cover gets a shallow band and one with
    /// trees gets a tall one, which is the only way both read at the same scale.</summary>
    private static (int From, int To) SectionRange(VoxelWorld world, int row)
    {
        var highest = GroundTop;
        for (var y = GroundTop + SkyCourses; y >= GroundTop; y--)
        {
            var any = false;
            for (var x = 0; x < SectionSpan && !any; x++) any = world.GetBlock(x, y, row).Id != Blocks.Air;
            if (any) { highest = y; break; }
        }
        return (Math.Max(0, GroundTop - GroundShown), highest + SkyMargin);
    }

    /// <summary>A flat sample patch, painted and then dressed by the real passes. Flat on purpose: a preview is
    /// asking what a recipe grows, and terrain of its own would only confuse that with what the terrain does.</summary>
    private static (VoxelWorld World, DressingTally Tally) Dressed(
        DressingRecipe recipe, TerrainTheme theme, int spanX, int spanZ)
    {
        var world = new VoxelWorld();
        var surface = new Dictionary<(int X, int Z), int>(spanX * spanZ);
        for (var z = 0; z < spanZ; z++)
        for (var x = 0; x < spanX; x++)
        {
            for (var y = 0; y < GroundTop; y++) world.SetBlock(x, y, z, Blocks.Stone);
            surface[(x, z)] = GroundTop;
        }

        TerrainPainter.Paint(world, surface, theme);
        return (world, Decorator.Decorate(world, new DressingContext(surface, recipe)));
    }
}
