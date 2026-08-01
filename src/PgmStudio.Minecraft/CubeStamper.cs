using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>Which structure variant a room shell is — differs only in door material/height, the colour-strip
/// material, and its contents (chests vs monuments, stamped separately).</summary>
public enum CubeKind { WoolCage, SpawnCube }

/// <summary>
/// Stamps the hollow-bedrock room shell used by wool cages and spawn cubes, sized by its
/// <see cref="RoomFrame"/> (docs/world-export/structures.md WX1–WX7; the shipped 10×10 piece yields the
/// original 8×8 shell). Layers are floor-indexed (floor = 0, roof = 8): the floor is bedrock with the
/// frame's wool <see cref="RoomPad"/>, walls are bedrock except a coloured strip at layer 4 (wool for
/// cages, stained clay for spawns) and a missing course at layer 6 (light slit), and the roof is bedrock
/// with a centred hole that follows the shell (capped at 4×4). Doors start at layer 1, cut where the
/// frame's entry interfaces are: stained-glass panes 3 tall for a wool cage, open air 4 tall for a spawn.
/// </summary>
public static class CubeStamper
{
    public const int RoofLayer = 8;        // top course (bedrock + centre hole) — also the highest layer written
    public const int SlitLayer = 6;        // missing course — the light slit
    public const int StripLayer = 4;       // coloured strip (wool / stained clay)
    public const int WoolDoorHeight = 3;   // stained-glass pane door, layers 1–3
    public const int SpawnDoorHeight = 4;  // open-air door, layers 1–4

    public static void Stamp(VoxelWorld world, RoomFrame frame, int floorY, int color, CubeKind kind)
    {
        for (var x = frame.MinX; x < frame.MaxX; x++)
        for (var z = frame.MinZ; z < frame.MaxZ; z++)
        {
            // Floor (layer 0): bedrock, with the frame's pad in wool.
            var onPad = x >= frame.Pad.MinX && x < frame.Pad.MinX + frame.Pad.Size
                && z >= frame.Pad.MinZ && z < frame.Pad.MinZ + frame.Pad.Size;
            world.SetBlock(x, floorY, z, onPad ? Blocks.Wool : Blocks.Bedrock, onPad ? color : 0);

            // Roof (top layer): bedrock with the centred hole left as air.
            if (!InRoofHole(frame, x, z)) world.SetBlock(x, floorY + RoofLayer, z, Blocks.Bedrock);

            // Walls: perimeter cells only, layers 1..RoofLayer-1.
            var onRing = x == frame.MinX || x == frame.MaxX - 1 || z == frame.MinZ || z == frame.MaxZ - 1;
            if (!onRing) continue;
            for (var layer = 1; layer < RoofLayer; layer++)
            {
                if (layer == SlitLayer) continue;   // light slit — no block
                if (layer == StripLayer)
                    world.SetBlock(x, floorY + layer, z, kind == CubeKind.WoolCage ? Blocks.Wool : Blocks.StainedClay, color);
                else
                    world.SetBlock(x, floorY + layer, z, Blocks.Bedrock);
            }
        }

        foreach (var door in frame.Doors)
            StampDoor(world, frame, floorY, door,
                kind == CubeKind.WoolCage ? Blocks.StainedGlassPane : Blocks.Air,
                kind == CubeKind.WoolCage ? color : 0,
                kind == CubeKind.WoolCage ? WoolDoorHeight : SpawnDoorHeight);
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

    private static bool InRoofHole(RoomFrame frame, int x, int z)
    {
        var holeW = RoofHoleSpan(frame.Width);
        var holeD = RoofHoleSpan(frame.Depth);
        var holeMinX = frame.MinX + (frame.Width - holeW) / 2;
        var holeMinZ = frame.MinZ + (frame.Depth - holeD) / 2;
        return x >= holeMinX && x < holeMinX + holeW && z >= holeMinZ && z < holeMinZ + holeD;
    }

    private static void StampDoor(VoxelWorld world, RoomFrame frame, int floorY, RoomDoor door, int id, int data, int height)
    {
        for (var layer = 1; layer <= height; layer++)
        for (var along = door.Lo; along < door.Lo + door.Width; along++)
        {
            var (x, z) = door.Edge switch
            {
                RoomEdge.NegZ => (along, frame.MinZ),
                RoomEdge.PosZ => (along, frame.MaxZ - 1),
                RoomEdge.NegX => (frame.MinX, along),
                _ => (frame.MaxX - 1, along),
            };
            world.SetBlock(x, floorY + layer, z, id, data);
        }
    }
}
