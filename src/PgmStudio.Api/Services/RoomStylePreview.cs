using PgmStudio.Vocabulary;
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
/// <para>Four views, because a building varies along more axes than one picture holds. What it <b>looks
/// like</b> is answered as the world's own columns, drawn in 3-D by the renderer a map is drawn by. From
/// <b>above</b> the roof reads: its hole, and whether its eave overhangs the walls.
/// The <b>section</b> is the course stack and the doorway through it — a <see cref="BlockSideView"/>
/// projection rather than a cut, so a doorway on the near wall does not hide the wall behind it. The
/// <b>cutaway</b> is one plane at the scale of the pieces in it, which is the only view that draws a stair
/// lattice as the opening it is and the only one that shows a storey's slab, the clear under it and the
/// ladder through it at once.</para>
///
/// <para>A card takes the section alone. The full set is drawn for the <em>open editor</em> only: a world runs
/// kilobytes, which is nothing for one building and megabytes for a grid of them.</para>
/// </summary>
public static class RoomStylePreview
{
    /// <summary>The room a preview is stamped into, at the piece one of the four
    /// <see cref="HouseFootprints"/> names — whose frame resolves to the shell inside it (WX1). One door on
    /// the −z wall, so a section taken from the front shows a doorway and the courses around it; the marker
    /// at the piece centre gives the 2×2 pad.</summary>
    private static RoomFrame SampleOf(string? footprint)
    {
        var (width, depth) = HouseFootprints.PieceOf(footprint);
        // A whole-number marker in both axes: the pad is square, so a piece whose axes differ in parity is
        // refused outright where the marker is taken as the piece centre (WX3).
        return RoomFrames.Resolve(new BlockRect(0, 0, width, depth), footprint: null, shellBound: true,
                width / 2, depth / 2, [(0, 0, width, 0)], null, out _)
            ?? throw new InvalidOperationException(
                $"the {footprint} sample piece ({width}×{depth}) does not resolve to a shell");
    }

    /// <summary>Red, on the 0–15 damage scale a room's tint reads — the same sample colour the theme cards
    /// use.</summary>
    private const int SampleColor = 14;

    private const int FloorY = 16;

    /// <summary>How far outside the shell a picture reaches: enough to show an eave overhanging it, and the
    /// ground it stands on either side.</summary>
    private const int Margin = 2;

    /// <summary>The one picture a library card carries, cut to <paramref name="part"/> the way
    /// <see cref="Views"/> cuts, so a row and the draft that becomes it draw the same thing. Kept apart from
    /// <see cref="Views"/> so listing the library does not draw an isometric per row.</summary>
    public static string Card(HouseStyle style, int cell = 6, string? footprint = null, string? part = null)
    {
        var sample = SampleOf(footprint);
        return WorldViews.Section(
            Stamped(style, sample), CutTo(style, Outer(style, sample), part), cell);
    }

    /// <summary>
    /// The box the views are taken over for the part an editor has open — the whole shell where none is.
    ///
    /// <para><b>The part is cut out of the building rather than stamped on its own.</b> A roof's eave sits on
    /// the summed storey stack and a porch decides the front the body is split on, so a part built in
    /// isolation has to synthesise the very context that decides its geometry. What an author wants is the
    /// building as it will stand, looked at where they are working.</para>
    ///
    /// <para>The bands are the style's own: a floor is its plate, claimed downward from the course players
    /// walk on; a wall is that course up to the eave, which is every storey's headroom plus a slab between
    /// each pair; a roof is everything over the eave, <b>reaching under it by
    /// <see cref="RoofField.MaxEaveDrop"/></b> — the overhang keeps falling past the course it rests on, so a
    /// roof cut at the eave loses the very edge an author is tuning. A porch is an XZ restriction instead —
    /// it is a strip of the footprint rather than a band of height — and is left to the whole box, since the
    /// strip is only legible against the wall it stands on.</para>
    ///
    /// <para>Two neighbouring bands therefore share courses. That is the honest picture: the eave is both the
    /// top of the wall and what the roof lands on, and a band drawn to exclude its neighbour would draw a
    /// join that is not there.</para>
    /// </summary>
    private static BlockBox CutTo(HouseStyle style, BlockBox whole, string? part)
    {
        var eave = FloorY + style.WallCourses;
        return RoomParts.Canonical(part) switch
        {
            RoomParts.Floor => whole with { MaxY = FloorY },
            RoomParts.Wall => whole with { MinY = FloorY, MaxY = eave },
            RoomParts.Roof => whole with { MinY = eave - RoofField.MaxEaveDrop },
            _ => whole,
        };
    }

    /// <summary>Every view of the sample room, for the one style an editor has open.</summary>
    public static RoomStylePreviewDto Views(
        HouseStyle style, int cell = 6, string? footprint = null, string? part = null)
    {
        var sample = SampleOf(footprint);
        var world = Stamped(style, sample);
        var box = CutTo(style, Outer(style, sample), part);
        var inner = CutTo(style, Inner(style, sample), part);
        return new RoomStylePreviewDto(
            WorldViews.Plan(world, box, cell),
            WorldViews.Section(world, box, cell),
            WorldColumnPayload.Of(world, within: box),
            WorldViews.Elevation(world, inner, CutawayPlane(world, inner, sample), Math.Max(8, cell * 2)));
    }

    /// <summary>One view of the sample room as PNG bytes, or null for a view name this endpoint does not
    /// answer in that form. <c>plan</c> and <c>section</c> are cell rasters and encode either way; the
    /// isometric and the cutaway draw a block as its own shape rather than as a filled cell — a stair
    /// lattice's whole trick is the quarter each stair is missing — so they have no raster to encode and stay
    /// SVG. Every other preview in the studio answers <c>?format=png</c>, and a building is the one picture
    /// the reviewer's checklist asks to be looked at, so the two that can are offered.</summary>
    public static byte[]? Png(
        HouseStyle style, string view, int scale = 1, string? footprint = null, string? part = null)
    {
        var sample = SampleOf(footprint);
        var world = Stamped(style, sample);
        var box = CutTo(style, Outer(style, sample), part);
        return view switch
        {
            "plan" => WorldViews.PlanRaster(world, box, Cell).Scaled(scale).Png(),
            "section" => WorldViews.SectionRaster(world, box, Cell).Scaled(scale).Png(),
            _ => null,
        };
    }

    /// <summary>Pixels a block takes in a rastered view before the caller's scale.</summary>
    private const int Cell = 6;

    /// <summary>The views <see cref="Png"/> answers, first being the one it draws unasked. The isometric and
    /// the cutaway are not here: they draw a block as its own shape rather than as a filled cell, so they
    /// have no raster to encode.</summary>
    public static readonly string[] PngViews = ["section", "plan"];

    /// <summary>The sample room stamped with <paramref name="style"/>, over ground that reaches the shell's
    /// footprint — so the floor has something to sit on and a deep one has something to sink into.</summary>
    private static VoxelWorld Stamped(HouseStyle style, RoomFrame sample)
    {
        var world = new VoxelWorld();
        for (var x = sample.MinX - Margin; x < sample.MaxX + Margin; x++)
        for (var z = sample.MinZ - Margin; z < sample.MaxZ + Margin; z++)
        for (var y = 1; y < FloorY; y++)
            world.SetBlock(x, y, z, Blocks.Stone);

        HouseStamper.Stamp(world, sample, FloorY, style, SampleColor);
        // The pad belongs to the structure stampers rather than the shell, but a preview is of the room and
        // not of the shell alone — and it is what a plan view sees through the roof hole, so without it a
        // holed roof and a sealed one draw the same picture.
        PadStamp.Lay(world, sample.Pad, FloorY, SampleColor);
        return world;
    }

    /// <summary>The box the outward views are taken over: the shell plus its margin of ground, from under the
    /// deepest floor to over the highest course of roof.</summary>
    private static BlockBox Outer(HouseStyle style, RoomFrame sample) => new(
        sample.MinX - Margin, FloorY - style.Foundation.Plate.Extent, sample.MinZ - Margin,
        sample.MaxX + Margin - 1, FloorY + style.TopLayerOver(sample.Width, sample.Depth, sample.Doors[0].Edge), sample.MaxZ + Margin - 1);

    /// <summary>The box the cutaway is drawn over — the shell itself, since a slice through the ground beside
    /// it is a slice through stone.</summary>
    private static BlockBox Inner(HouseStyle style, RoomFrame sample) => new(
        sample.MinX, FloorY - 1, sample.MinZ,
        sample.MaxX, FloorY + style.TopLayerOver(sample.Width, sample.Depth, sample.Doors[0].Edge), sample.MaxZ);

    /// <summary>The plane the cutaway is taken on: the one the ladder stands in where the building has
    /// storeys, since that is where the slab, the clear under it and the way through it are all visible at
    /// once — else one block inside the front wall, the busiest plane a single-storey shell has. Found by
    /// looking for the ladder rather than by working out where it ought to be.</summary>
    private static int CutawayPlane(VoxelWorld world, BlockBox box, RoomFrame sample)
    {
        for (var z = box.MinZ; z <= box.MaxZ; z++)
            for (var x = box.MinX; x <= box.MaxX; x++)
                for (var y = box.MinY; y <= box.MaxY; y++)
                    if (world.GetBlock(x, y, z).Id == Blocks.Ladder) return z;
        return sample.MinZ + 1;
    }
}
