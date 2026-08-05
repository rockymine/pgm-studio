using PgmStudio.Contracts;
using PgmStudio.Domain;
using PgmStudio.Minecraft;

namespace PgmStudio.Api.Services;

/// <summary>
/// What a room style stamps. Every picture here is built by running the real <see cref="CubeStamper"/> over a
/// sample <see cref="RoomFrame"/> and reading the blocks back, so a card cannot promise a shell the export
/// would not build — the discipline the dressing pickers and the theme cards already hold.
///
/// <para>Two views, because a shell varies along two axes. From <b>above</b> the roof reads: its hole, and
/// whether its eave overhangs the walls. From the <b>side</b> everything else does — the course stack that is
/// the whole point of a style, the doorway cut through it, the floor's depth. The section is a
/// <see cref="BlockSideView"/> projection rather than a cut, so a doorway on the near wall does not hide the
/// wall behind it.</para>
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

    public static RoomStylePreviewDto Views(RoomStyle style, int cell = 6)
    {
        var world = Stamped(style);
        return new RoomStylePreviewDto(PlanSvg(world, style, cell), SectionSvg(world, style, cell));
    }

    /// <summary>The sample room stamped with <paramref name="style"/>, over ground that reaches the shell's
    /// footprint — so the floor has something to sit on and a deep one has something to sink into.</summary>
    private static VoxelWorld Stamped(RoomStyle style)
    {
        var world = new VoxelWorld();
        for (var x = Sample.MinX - Margin; x < Sample.MaxX + Margin; x++)
        for (var z = Sample.MinZ - Margin; z < Sample.MaxZ + Margin; z++)
        for (var y = 1; y < FloorY; y++)
            world.SetBlock(x, y, z, Blocks.Stone);

        CubeStamper.Stamp(world, Sample, FloorY, SampleColor, style);
        return world;
    }

    /// <summary>How far outside the shell a picture reaches: enough to show an eave overhanging it, and the
    /// ground it stands on either side.</summary>
    private const int Margin = 2;

    /// <summary>From above: the highest block of each column. What the roof does — its hole, and whether it
    /// oversails the walls — reads here and nowhere else.</summary>
    private static string PlanSvg(VoxelWorld world, RoomStyle style, int cell)
    {
        var top = FloorY + style.TopLayer;
        return SvgRaster.Raster(Sample.Width + Margin * 2, Sample.Depth + Margin * 2, cell, (x, z) =>
        {
            for (var y = top; y >= 1; y--)
            {
                var (id, data) = world.GetBlock(Sample.MinX - Margin + x, y, Sample.MinZ - Margin + z);
                if (id != Blocks.Air) return BlockPalette.Hex(id, data);
            }
            return null;
        });
    }

    /// <summary>From the side, looking at the door wall: the course stack the style is, depth-shaded so a
    /// doorway reads as an opening rather than as a hole in the picture.</summary>
    private static string SectionSvg(VoxelWorld world, RoomStyle style, int cell)
    {
        var (fromX, toX) = (Sample.MinX - Margin, Sample.MaxX + Margin - 1);
        var (fromY, toY) = (FloorY - style.Floor.Extent, FloorY + style.TopLayer);
        var view = BlockSideView.Project(
            world, fromX, toX, Sample.MinZ - Margin, Sample.MaxZ + Margin - 1, fromY, toY);

        return SvgRaster.Raster(view.Columns, toY - fromY + 1, cell, (column, screenRow) =>
            view.At(view.FromX + column, toY - screenRow) is { } block
                ? BlockPalette.Hex(block.Id, block.Data, block.Depth)
                : null);
    }
}
