using PgmStudio.Domain;

namespace PgmStudio.Minecraft;

/// <summary>
/// Stamps the DTM and DTC objective structures. Both float above the terrain — the gap is what lets a
/// core's lava fall and what players dig through to extend it — so neither needs a carve, a void, or any
/// other negative-space primitive.
/// <para>Coordinates are absolute world block coordinates; the caller has already resolved the anchor and
/// fanned it across the symmetry orbit.</para>
/// </summary>
public static class ObjectiveStamper
{
    /// <summary>Blocks of air between the terrain surface and a destroyable's base.</summary>
    public const int DefaultDestroyableFloat = 4;

    /// <summary>Blocks of air between the terrain surface and a core's floor.</summary>
    public const int DefaultCoreFloat = 6;

    /// <summary>Depth below the core's floor a lava block must reach to leak.</summary>
    public const int DefaultCoreLeak = 5;

    public const int DefaultCoreSize = 5;
    public const int DefaultCoreHeight = 5;
    public const int DefaultCoreShell = 1;

    // ── destroyables ────────────────────────────────────────────────────────────────

    /// <summary>The footprint and height of a style, as (width, height, depth) in blocks.</summary>
    public static (int Width, int Height, int Depth) Dimensions(DestroyableStyle style, int columnHeight = 3) => style switch
    {
        DestroyableStyle.Pillar1 => (1, 1, 1),
        DestroyableStyle.Pillar2 => (1, 2, 1),
        DestroyableStyle.Pillar3 => (1, 3, 1),
        DestroyableStyle.Cube3 => (3, 3, 3),
        DestroyableStyle.Cube4 => (4, 4, 4),
        DestroyableStyle.ColumnPlus => (3, columnHeight, 3),
        _ => (1, 1, 1),
    };

    /// <summary>
    /// The box a destroyable occupies: its footprint centred on the anchor, floating
    /// <paramref name="floatBlocks"/> above the highest surface the footprint spans. Sampling the whole
    /// footprint rather than the anchor column keeps the structure level and survives the symmetry orbit,
    /// which sampling one side of a grid line does not.
    /// </summary>
    public static BlockBox DestroyableBox(
        IReadOnlyDictionary<(int X, int Z), int> surfaceTop, int anchorX, int anchorZ,
        DestroyableStyle style, int floatBlocks = DefaultDestroyableFloat, int columnHeight = 3)
    {
        var (w, h, d) = Dimensions(style, columnHeight);
        var (minX, minZ) = (anchorX - (w - 1) / 2, anchorZ - (d - 1) / 2);
        var (maxX, maxZ) = (minX + w - 1, minZ + d - 1);
        var baseY = PositionSnap.SurfaceYOver(surfaceTop, minX, minZ, maxX, maxZ, 1) + floatBlocks;
        return new BlockBox(minX, baseY, minZ, maxX, baseY + h - 1, maxZ);
    }

    /// <summary>
    /// Place a destroyable's blocks inside <paramref name="box"/>. The cubes take an optional concentric
    /// bedrock centre so players cannot hollow one out and hide inside; it costs nothing to model because
    /// <c>materials</c> names only the outer block, leaving the bedrock invisible to the goal — neither
    /// counted in its health nor breakable.
    /// </summary>
    public static void StampDestroyable(VoxelWorld world, BlockBox box, DestroyableStyle style, int material, bool bedrockCentre = false)
    {
        for (var x = box.MinX; x <= box.MaxX; x++)
        for (var z = box.MinZ; z <= box.MaxZ; z++)
        for (var y = box.MinY; y <= box.MaxY; y++)
        {
            if (style == DestroyableStyle.ColumnPlus && !InPlusSection(box, x, z)) continue;
            world.SetBlock(x, y, z, material);
        }

        if (bedrockCentre && style is DestroyableStyle.Cube3 or DestroyableStyle.Cube4)
            FillBox(world, Inset(box, 1), Blocks.Bedrock);
    }

    // The plus/cross section: the corners of the 3×3 are left open, which is the family's signature and
    // what separates it from a cube.
    private static bool InPlusSection(BlockBox box, int x, int z)
    {
        var onCentreX = x == box.MinX + box.Width / 2;
        var onCentreZ = z == box.MinZ + box.Depth / 2;
        return onCentreX || onCentreZ;
    }

    // ── cores ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The box a core's casing occupies: a <paramref name="size"/>-square footprint centred on the anchor,
    /// <paramref name="height"/> tall, floating <paramref name="floatBlocks"/> above the terrain. The floor
    /// is <c>MinY</c>, which is the <c>region.min.y</c> a leak level measures down from.
    /// </summary>
    public static BlockBox CoreBox(
        IReadOnlyDictionary<(int X, int Z), int> surfaceTop, int anchorX, int anchorZ,
        int size = DefaultCoreSize, int height = DefaultCoreHeight, int floatBlocks = DefaultCoreFloat)
    {
        var (minX, minZ) = (anchorX - (size - 1) / 2, anchorZ - (size - 1) / 2);
        var (maxX, maxZ) = (minX + size - 1, minZ + size - 1);
        var baseY = PositionSnap.SurfaceYOver(surfaceTop, minX, minZ, maxX, maxZ, 1) + floatBlocks;
        return new BlockBox(minX, baseY, minZ, maxX, baseY + height - 1, maxZ);
    }

    /// <summary>
    /// Place a core: a solid casing of <paramref name="material"/> with a lava interior hollowed out of it,
    /// the shell <paramref name="shell"/> blocks thick. The top is capped by default — most real cores
    /// enclose their lava fully, and the open-top variant is a minority style rather than the norm. With
    /// <paramref name="openTop"/> the cap is omitted and the lava rises flush with the rim.
    /// </summary>
    public static void StampCore(VoxelWorld world, BlockBox box, int material, int shell = DefaultCoreShell, bool openTop = false)
    {
        FillBox(world, box, material);

        var lava = Inset(box, shell);
        if (openTop) lava = lava with { MaxY = box.MaxY };
        if (lava.Width > 0 && lava.Height > 0 && lava.Depth > 0)
            FillBox(world, lava, Blocks.StationaryLava);
    }

    /// <summary>
    /// How far players must dig into the terrain below a core to make it leak. Escaping lava free-falls to
    /// the terrain at <c>floor − float</c>, and the core leaks at <c>floor − leak</c>, so the two are one
    /// knob: at <c>leak ≤ float</c> a breached casing leaks on its own, and above it digging is part of the
    /// capture. Neither value means anything without the other.
    /// </summary>
    public static int DigDepth(int leak, int floatBlocks) => Math.Max(0, leak - floatBlocks);

    // ── shared ──────────────────────────────────────────────────────────────────────
    private static BlockBox Inset(BlockBox b, int by) => new(
        b.MinX + by, b.MinY + by, b.MinZ + by, b.MaxX - by, b.MaxY - by, b.MaxZ - by);

    private static void FillBox(VoxelWorld world, BlockBox b, int block)
    {
        for (var x = b.MinX; x <= b.MaxX; x++)
        for (var z = b.MinZ; z <= b.MaxZ; z++)
        for (var y = b.MinY; y <= b.MaxY; y++)
            world.SetBlock(x, y, z, block);
    }
}
