namespace PgmStudio.Minecraft;

/// <summary>Which structure variant a cube shell is — differs only in door count/size, the colour-strip
/// material, and its contents (chests vs monuments, stamped separately).</summary>
public enum CubeKind { WoolCage, SpawnCube }

/// <summary>The outward direction a wall faces (a spawn cube's single door faces one of these).</summary>
public enum Facing { NegZ, PosZ, NegX, PosX }

/// <summary>
/// Stamps the shared 8×8 hollow-bedrock cube shell used by wool cages and spawn cubes
/// (docs/contracts/sketch-world-export.md §2). Layers are floor-indexed (floor = 0, roof = 8): floor is
/// bedrock with a 2×2 wool centre (the spawn/wool marker), walls are bedrock except a coloured strip at
/// layer 4 (wool for cages, stained clay for spawns) and a missing course at layer 6 (light slit), and the
/// roof is bedrock with a 4×4 centre hole. Doors start at layer 1: a wool cage gets four 2-wide × 3-tall
/// stained-glass-pane doors (one per wall); a spawn cube gets a single 4-wide × 4-tall open-air door.
///
/// The cube is anchored on <c>(anchorX, anchorZ)</c> — the integer corner shared by the 2×2 wool centre —
/// so the 8-wide footprint spans <c>anchorX-4 .. anchorX+3</c>; layer 0 sits at world <c>floorY</c>.
/// </summary>
public static class CubeStamper
{
    public const int Size = 8;             // footprint width/depth
    public const int Half = Size / 2;      // anchor→corner offset (= low coord of the 2×2 centre)
    public const int Max = Size - 1;       // far perimeter index (near perimeter = 0)
    public const int Interior = 1;         // first interior (hollow) index
    public const int InteriorMax = Size - 2; // last interior (hollow) index
    public const int RoofLayer = 8;        // top course (bedrock + 4×4 hole) — also the highest layer written
    public const int SlitLayer = 6;        // missing course — the light slit
    public const int StripLayer = 4;       // coloured strip (wool / stained clay)

    /// <summary>The 8×8 XZ footprint (min inclusive, max inclusive) of a cube anchored on
    /// <paramref name="anchorX"/>/<paramref name="anchorZ"/> — the columns its shell stands on.</summary>
    public static (int MinX, int MinZ, int MaxX, int MaxZ) Footprint(int anchorX, int anchorZ)
        => (anchorX - Half, anchorZ - Half, anchorX + Half - 1, anchorZ + Half - 1);

    public static void Stamp(VoxelWorld world, int anchorX, int anchorZ, int floorY, int color, CubeKind kind, Facing doorFacing = Facing.NegZ)
    {
        var x0 = anchorX - Half;
        var z0 = anchorZ - Half;

        void Set(int lx, int layer, int lz, int id, int data = 0)
            => world.SetBlock(x0 + lx, floorY + layer, z0 + lz, id, data);

        for (var lx = 0; lx < Size; lx++)
        for (var lz = 0; lz < Size; lz++)
        {
            // Floor (layer 0): bedrock, 2×2 wool centre (the two coords straddling the mid-line).
            var center = lx is Half - 1 or Half && lz is Half - 1 or Half;
            Set(lx, 0, lz, center ? Blocks.Wool : Blocks.Bedrock, center ? color : 0);

            // Roof (top layer): bedrock with a centred 4×4 hole (left as air).
            var roofHole = lx is >= Half - 2 and <= Half + 1 && lz is >= Half - 2 and <= Half + 1;
            if (!roofHole) Set(lx, RoofLayer, lz, Blocks.Bedrock);

            // Walls: perimeter cells only, layers 1..RoofLayer-1.
            if (lx is not (0 or Max) && lz is not (0 or Max)) continue;
            for (var layer = 1; layer < RoofLayer; layer++)
            {
                if (layer == SlitLayer) continue;   // light slit — no block
                if (layer == StripLayer)
                    Set(lx, layer, lz, kind == CubeKind.WoolCage ? Blocks.Wool : Blocks.StainedClay, color);
                else
                    Set(lx, layer, lz, Blocks.Bedrock);
            }
        }

        if (kind == CubeKind.WoolCage)
        {
            foreach (var facing in (ReadOnlySpan<Facing>)[Facing.NegZ, Facing.PosZ, Facing.NegX, Facing.PosX])
                StampDoor(Set, facing, width: 2, height: 3, Blocks.StainedGlassPane, color);
        }
        else
        {
            StampDoor(Set, doorFacing, width: 4, height: 4, Blocks.Air, 0);
        }
    }

    private static void StampDoor(Action<int, int, int, int, int> set, Facing facing, int width, int height, int id, int data)
    {
        var lo = (Size - width) / 2;   // centred on the wall: width 2 → {3,4}, width 4 → {2..5}
        for (var layer = 1; layer <= height; layer++)
        for (var t = lo; t < lo + width; t++)
        {
            var (lx, lz) = facing switch
            {
                Facing.NegZ => (t, 0),
                Facing.PosZ => (t, Max),
                Facing.NegX => (0, t),
                Facing.PosX => (Max, t),
                _ => (t, 0),
            };
            set(lx, layer, lz, id, data);
        }
    }
}
