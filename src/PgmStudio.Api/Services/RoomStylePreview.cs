using PgmStudio.Contracts;
using PgmStudio.Domain;
using PgmStudio.Minecraft;
using PgmStudio.Minecraft.Views;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Houses;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Api.Services;

/// <summary>
/// What a room style stamps. Every picture here is built by running the real <see cref="HouseStamper"/> over a
/// sample <see cref="RoomFrame"/> and reading the blocks back, so a card cannot promise a shell the export
/// would not build — the discipline the dressing pickers and the theme cards already hold.
///
/// <para>Four views, because a building varies along more axes than one picture holds. The <b>isometric</b> is
/// what it looks like. From <b>above</b> the roof reads: its hole, and whether its eave overhangs the walls.
/// The <b>section</b> is the course stack and the doorway through it — a <see cref="BlockSideView"/>
/// projection rather than a cut, so a doorway on the near wall does not hide the wall behind it. The
/// <b>cutaway</b> is one plane at the scale of the pieces in it, which is the only view that draws a stair
/// lattice as the opening it is and the only one that shows a storey's slab, the clear under it and the
/// ladder through it at once.</para>
///
/// <para>A card takes the section alone. The full set is drawn for the <em>open editor</em> only: an isometric
/// runs tens of kilobytes, which is nothing for one picture and megabytes for a grid of them.</para>
/// </summary>
public static class RoomStylePreview
{
    /// <summary>The room every preview is stamped into: the shipped 10×10 piece, whose frame resolves to the
    /// 8×8 shell (WX1). One door on the −z wall, so a section taken from the front shows a doorway and the
    /// courses around it; the marker at the piece centre gives the 2×2 pad.</summary>
    private static readonly RoomFrame Sample =
        RoomFrames.Resolve(0, 0, 10, 10, 5, 5, [(0, 0, 10, 0)], null, out _)!;

    /// <summary>Red, on the 0–15 damage scale a room's tint reads — the same sample colour the theme cards
    /// use.</summary>
    private const int SampleColor = 14;

    private const int FloorY = 16;

    /// <summary>How far outside the shell a picture reaches: enough to show an eave overhanging it, and the
    /// ground it stands on either side.</summary>
    private const int Margin = 2;

    /// <summary>The one picture a library card carries. Kept apart from <see cref="Views"/> so listing the
    /// library does not draw an isometric per row.</summary>
    public static string Card(HouseStyle style, int cell = 6)
        => WorldViews.Section(Stamped(style), Outer(style), cell);

    /// <summary>Every view of the sample room, for the one style an editor has open.</summary>
    public static RoomStylePreviewDto Views(HouseStyle style, int cell = 6)
    {
        var world = Stamped(style);
        var box = Outer(style);
        return new RoomStylePreviewDto(
            WorldViews.Plan(world, box, cell),
            WorldViews.Section(world, box, cell),
            WorldViews.Isometric(world, box, Math.Max(4, cell)),
            WorldViews.Elevation(world, Inner(style), CutawayPlane(world, Inner(style)), Math.Max(8, cell * 2)));
    }

    /// <summary>One view of the sample room as PNG bytes, or null for a view name this endpoint does not
    /// answer in that form. <c>plan</c> and <c>section</c> are cell rasters and encode either way; the
    /// isometric and the cutaway draw a block as its own shape rather than as a filled cell — a stair
    /// lattice's whole trick is the quarter each stair is missing — so they have no raster to encode and stay
    /// SVG. Every other preview in the studio answers <c>?format=png</c>, and a building is the one picture
    /// the reviewer's checklist asks to be looked at, so the two that can are offered.</summary>
    public static byte[]? Png(HouseStyle style, string view, int cell = 6)
    {
        var world = Stamped(style);
        var box = Outer(style);
        return view switch
        {
            "plan" => WorldViews.PlanRaster(world, box, cell).Png(),
            "section" => WorldViews.SectionRaster(world, box, cell).Png(),
            _ => null,
        };
    }

    /// <summary>The view names <see cref="Png"/> answers, for the refusal that names them.</summary>
    public const string PngViews = "it draws plan and section (the isometric and the cutaway are SVG only)";

    /// <summary>The sample room stamped with <paramref name="style"/>, over ground that reaches the shell's
    /// footprint — so the floor has something to sit on and a deep one has something to sink into.</summary>
    private static VoxelWorld Stamped(HouseStyle style)
    {
        var world = new VoxelWorld();
        for (var x = Sample.MinX - Margin; x < Sample.MaxX + Margin; x++)
        for (var z = Sample.MinZ - Margin; z < Sample.MaxZ + Margin; z++)
        for (var y = 1; y < FloorY; y++)
            world.SetBlock(x, y, z, Blocks.Stone);

        HouseStamper.Stamp(world, Sample, FloorY, style, SampleColor);
        // The pad belongs to the structure stampers rather than the shell, but a preview is of the room and
        // not of the shell alone — and it is what a plan view sees through the roof hole, so without it a
        // holed roof and a sealed one draw the same picture.
        PadStamp.Lay(world, Sample.Pad, FloorY, SampleColor);
        return world;
    }

    /// <summary>The box the outward views are taken over: the shell plus its margin of ground, from under the
    /// deepest floor to over the highest course of roof.</summary>
    private static BlockBox Outer(HouseStyle style) => new(
        Sample.MinX - Margin, FloorY - style.Foundation.Plate.Extent, Sample.MinZ - Margin,
        Sample.MaxX + Margin - 1, FloorY + style.TopLayerOver(Sample.Width, Sample.Depth, Sample.Doors[0].Edge), Sample.MaxZ + Margin - 1);

    /// <summary>The box the cutaway is drawn over — the shell itself, since a slice through the ground beside
    /// it is a slice through stone.</summary>
    private static BlockBox Inner(HouseStyle style) => new(
        Sample.MinX, FloorY - 1, Sample.MinZ,
        Sample.MaxX, FloorY + style.TopLayerOver(Sample.Width, Sample.Depth, Sample.Doors[0].Edge), Sample.MaxZ);

    /// <summary>The plane the cutaway is taken on: the one the ladder stands in where the building has
    /// storeys, since that is where the slab, the clear under it and the way through it are all visible at
    /// once — else one block inside the front wall, the busiest plane a single-storey shell has. Found by
    /// looking for the ladder rather than by working out where it ought to be.</summary>
    private static int CutawayPlane(VoxelWorld world, BlockBox box)
    {
        for (var z = box.MinZ; z <= box.MaxZ; z++)
            for (var x = box.MinX; x <= box.MaxX; x++)
                for (var y = box.MinY; y <= box.MaxY; y++)
                    if (world.GetBlock(x, y, z).Id == Blocks.Ladder) return z;
        return Sample.MinZ + 1;
    }
}
