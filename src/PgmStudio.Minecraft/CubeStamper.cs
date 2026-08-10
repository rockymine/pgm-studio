using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>
/// Stamps the room shell wool cages and spawn cubes share, sized by its <see cref="RoomFrame"/>
/// (docs/world-export/structures.md WX1–WX7) and finished by its <see cref="RoomStyle"/>.
///
/// <para>The shell is three parts and two overrides. Floor, walls and roof are each a course stack the style
/// supplies — which is what lets a band and a light slit be courses rather than hard-coded layer indices, and
/// what makes a wool cage and a spawn cube two bound styles rather than a branch in here. Over them go the
/// two things a style may not decide: the <b>pad</b>, because it is the exported wool/spawn point (WX5), and
/// the <b>doorway</b>, because it is the entry contract (WX6/WX7).</para>
///
/// <para>A part's air course is a <em>gap left open</em> and is skipped rather than written, so a stack can
/// never erase what a stamp already placed. A doorway's air is an <em>opening cut</em> and is written, which
/// is what carves the door out of the wall this pass just built.</para>
/// </summary>
public static class CubeStamper
{
    public static void Stamp(VoxelWorld world, RoomFrame frame, int floorY, int color, RoomStyle style)
    {
        var roofFrom = floorY + style.Wall.Extent + 1;
        var eave = style.Eave == RoofEdge.Overlap ? 1 : 0;

        for (var x = frame.MinX; x < frame.MaxX; x++)
        for (var z = frame.MinZ; z < frame.MaxZ; z++)
        {
            // Floor: downward from the course players stand on.
            for (var step = 0; step < style.Floor.Extent; step++)
                Write(world, style.Floor, step, x, floorY - step, z, color, frame);

            // Walls: the perimeter ring only, upward from the floor.
            if (x != frame.MinX && x != frame.MaxX - 1 && z != frame.MinZ && z != frame.MaxZ - 1) continue;
            for (var step = 0; step < style.Wall.Extent; step++)
                Write(world, style.Wall, step, x, floorY + 1 + step, z, color, frame);
        }

        // Roof: the plane over the walls, reaching a block further out when the style overlaps.
        for (var x = frame.MinX - eave; x < frame.MaxX + eave; x++)
        for (var z = frame.MinZ - eave; z < frame.MaxZ + eave; z++)
        {
            if (style.RoofHole && InRoofHole(frame, x, z)) continue;
            for (var step = 0; step < style.Roof.Extent; step++)
                Write(world, style.Roof, step, x, roofFrom + step, z, color, frame);
        }

        PadStamp.Lay(world, frame.Pad, floorY, color);
        foreach (var door in frame.Doors) StampDoor(world, frame, floorY, door, style, color);
    }

    /// <summary>One cell of a part, resolved through its stack. Air is a gap: nothing is written, so the cell
    /// keeps whatever was there — which above a shell's own footprint is air anyway, and below it is the
    /// platform the room stands on.</summary>
    private static void Write(
        VoxelWorld world, RoomPart part, int step, int x, int y, int z, int color, RoomFrame frame)
    {
        if (y is < 1 or >= VoxelWorld.MaxHeight) return;
        var (material, depth) = part.At(step);
        // Fill is the neutral bucket: no material reads one, and a room part is not one of terrain's five.
        var arc = PerimeterArc(frame, x, z);
        var (id, data) = material.Resolve(
            new BucketContext(x, y, z, TerrainBucket.Fill, depth, color, arc, 0, PerimeterTurn(frame, arc)));
        if (id == Blocks.Air) return;
        world.SetBlock(x, y, z, id, data);
    }

    /// <summary>How sharply the shell's ring bends where a cell sits, to the same scale a painted wall reads
    /// (<see cref="PgmStudio.Geom.Algorithms.GridBoundary.TurnAt"/>). A shell is a rectangle, so the answer is
    /// closed-form rather than traced: a cell <c>d</c> cells from a corner has a window that straddles it by
    /// <c>window − d</c>, and the chords make <c>atan2(window − d, d)</c> — 90° on the corner itself, then 76,
    /// 56, 34, 14 and straight, which is the ramp a traced right angle measures.</summary>
    private static int PerimeterTurn(RoomFrame frame, int arc)
    {
        if (arc < 0) return 0;
        int width = frame.Width, depth = frame.Depth;
        var loop = 2 * (width + depth) - 4;
        if (loop <= 0) return 0;

        // Where the ring turns: the four rectangle corners, in the order PerimeterArc numbers them.
        int[] corners = [0, width - 1, width + depth - 2, 2 * width + depth - 3];
        var nearest = int.MaxValue;
        foreach (var corner in corners)
        {
            var apart = Math.Abs(arc - corner);
            nearest = Math.Min(nearest, Math.Min(apart, loop - apart));
        }

        const int window = 5;
        if (nearest >= window) return 0;
        return (int)Math.Round(Math.Atan2(window - nearest, nearest) * 180.0 / Math.PI);
    }

    /// <summary>How far round the shell's perimeter a cell sits, clockwise from its −x/−z corner, or −1 off
    /// the ring. A room wall is a closed ring, so a wall-run pattern reads it exactly as it reads a plateau's
    /// outer edge — one material stripes both.</summary>
    private static int PerimeterArc(RoomFrame frame, int x, int z)
    {
        int width = frame.Width, depth = frame.Depth;
        if (z == frame.MinZ) return x - frame.MinX;                                  // −z edge, corners included
        if (x == frame.MaxX - 1) return width - 1 + (z - frame.MinZ);                // +x edge
        if (z == frame.MaxZ - 1) return width + depth - 2 + (frame.MaxX - 1 - x);    // +z edge, back along x
        if (x == frame.MinX) return 2 * width + depth - 3 + (frame.MaxZ - 1 - z);    // −x edge, back along z
        return -1;
    }

    /// <summary>The roof hole's span over a shell <paramref name="shellSpan"/> wide: proportional (span − 4,
    /// so the hole rim keeps two courses of roof) but capped at 4 — 3 on an odd shell, so it stays centred.
    /// The shipped 8-wide shell keeps its 4×4 hole.</summary>
    public static int RoofHoleSpan(int shellSpan)
    {
        var cap = shellSpan % 2 == 0 ? 4 : 3;
        var floor = shellSpan % 2 == 0 ? 2 : 1;
        return Math.Min(cap, Math.Max(shellSpan - 4, floor));
    }

    /// <summary>The hole is centred and measured on the <b>shell</b>, never on the roof plane: it lights the
    /// interior, and an overlapping roof grows symmetrically, so the centre is the same either way.</summary>
    private static bool InRoofHole(RoomFrame frame, int x, int z)
    {
        var holeW = RoofHoleSpan(frame.Width);
        var holeD = RoofHoleSpan(frame.Depth);
        var holeMinX = frame.MinX + (frame.Width - holeW) / 2;
        var holeMinZ = frame.MinZ + (frame.Depth - holeD) / 2;
        return x >= holeMinX && x < holeMinX + holeW && z >= holeMinZ && z < holeMinZ + holeD;
    }

    private static void StampDoor(
        VoxelWorld world, RoomFrame frame, int floorY, RoomDoor door, RoomStyle style, int color)
    {
        var choice = DoorMaterials.Of(style.Door);
        for (var layer = 1; layer <= style.DoorHeight; layer++)
        for (var along = door.Lo; along < door.Lo + door.Width; along++)
        {
            var (x, z) = door.Edge switch
            {
                RoomEdge.NegZ => (along, frame.MinZ),
                RoomEdge.PosZ => (along, frame.MaxZ - 1),
                RoomEdge.NegX => (frame.MinX, along),
                _ => (frame.MaxX - 1, along),
            };
            world.SetBlock(x, floorY + layer, z, choice.BlockId, choice.Coloured ? color : 0);
        }
    }
}
