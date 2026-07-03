namespace PgmStudio.Minecraft;

/// <summary>
/// Stamps the plan-derived layout structures onto a synthesised world (docs/contracts/layout-rules.md
/// ST1–ST4): a wool-room bedrock floor column, a wool-room entrance redstone line with end torches, a
/// 4×4×4 iron cube resting on the surface, and a pre-built bedrock approach wall. Every method takes the
/// per-column surface top (the first air Y above the solid terrain, where structures rest) so the stamps
/// sit on the real terrain rather than a fixed level. Coordinates are absolute world block coordinates —
/// the caller has already resolved and fanned them across the symmetry orbit.
/// </summary>
public static class StructureStamper
{
    /// <summary>Cube edge (blocks): a 4×4×4 iron structure resting on the surface.</summary>
    public const int IronCubeSize = 4;

    /// <summary>Fill a wool-room footprint with solid bedrock from y=0 up to (and including) the terrain
    /// surface block, so the room cannot be tunnelled into from below (ST1). Footprint is min-inclusive,
    /// max-exclusive (<c>[minX, maxX) × [minZ, maxZ)</c>).</summary>
    public static void StampRoomFloor(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int minX, int minZ, int maxX, int maxZ)
    {
        for (var x = minX; x < maxX; x++)
        for (var z = minZ; z < maxZ; z++)
        {
            var top = surfaceTop.GetValueOrDefault((x, z), 1);   // topmost air cell; solid ends at top-1
            for (var y = 0; y < top; y++) world.SetBlock(x, y, z, Blocks.Bedrock);
        }
    }

    /// <summary>Lay a redstone-wire row on top of the surface between the two given block ends (inclusive),
    /// with a redstone torch replacing the wire at each end (ST1 — the conventional entrance-protection
    /// marker). The two ends must share an axis (a straight row); the row is one block wide.</summary>
    public static void StampRedstoneLine(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int x1, int z1, int x2, int z2)
    {
        var dx = Math.Sign(x2 - x1);
        var dz = Math.Sign(z2 - z1);
        var steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(z2 - z1));
        for (var i = 0; i <= steps; i++)
        {
            var x = x1 + dx * i;
            var z = z1 + dz * i;
            var y = surfaceTop.GetValueOrDefault((x, z), 1);
            var id = i == 0 || i == steps ? Blocks.RedstoneTorch : Blocks.RedstoneWire;
            world.SetBlock(x, y, z, id);
        }
    }

    /// <summary>Place a 4×4×4 iron-block cube whose base rests on the surface at the anchor column and whose
    /// top sits three blocks above it (ST2/ST3). The 4-wide footprint is centred on the anchor
    /// (<c>[anchor-2, anchor+2)</c>), so a marker snapped to a whole block splits two-and-two cleanly.</summary>
    public static void StampIronCube(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop, int anchorX, int anchorZ)
    {
        var (minX, minZ, _, _) = IronCubeFootprint(anchorX, anchorZ);
        var baseY = surfaceTop.GetValueOrDefault((anchorX, anchorZ), 1);
        for (var lx = 0; lx < IronCubeSize; lx++)
        for (var lz = 0; lz < IronCubeSize; lz++)
        for (var ly = 0; ly < IronCubeSize; ly++)
            world.SetBlock(minX + lx, baseY + ly, minZ + lz, Blocks.IronBlock);
    }

    /// <summary>The 4×4 XZ footprint (min inclusive, max inclusive) of an iron cube anchored on
    /// <paramref name="anchorX"/>/<paramref name="anchorZ"/> — the region a renewable covers.</summary>
    public static (int MinX, int MinZ, int MaxX, int MaxZ) IronCubeFootprint(int anchorX, int anchorZ)
    {
        const int half = IronCubeSize / 2;
        return (anchorX - half, anchorZ - half, anchorX + half - 1, anchorZ + half - 1);
    }

    /// <summary>Raise a solid bedrock wall over a seam footprint from y=0 up to <paramref name="topY"/>
    /// inclusive (ST4). Footprint is min-inclusive, max-exclusive; it is two blocks thick across the seam and
    /// spans the full shared-interface width along it.</summary>
    public static void StampWall(VoxelWorld world, int minX, int minZ, int maxX, int maxZ, int topY)
    {
        var top = Math.Clamp(topY, 0, VoxelWorld.MaxHeight - 1);
        for (var x = minX; x < maxX; x++)
        for (var z = minZ; z < maxZ; z++)
        for (var y = 0; y <= top; y++)
            world.SetBlock(x, y, z, Blocks.Bedrock);
    }
}
