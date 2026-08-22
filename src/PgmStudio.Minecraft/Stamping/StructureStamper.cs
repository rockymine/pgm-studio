using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
namespace PgmStudio.Minecraft.Stamping;

/// <summary>
/// Stamps the plan-derived layout structures onto a synthesised world (docs/generator/rules.md
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

    /// <summary>Fill a footprint with solid bedrock from y=0 up to (and including) the terrain surface
    /// block, so what stands on it cannot be tunnelled into from below (ST1). This is the ground the building
    /// sits on, not the course a player walks on — that one is the shell's own floor part. Footprint is
    /// min-inclusive, max-exclusive (<c>[minX, maxX) × [minZ, maxZ)</c>).</summary>
    public static void StampFoundation(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int minX, int minZ, int maxX, int maxZ)
    {
        foreach (var (x, z) in FoundationCells(minX, minZ, maxX, maxZ))
        {
            var top = surfaceTop.GetValueOrDefault((x, z), 1);   // topmost air cell; solid ends at top-1
            for (var y = 0; y < top; y++) world.SetBlock(x, y, z, Blocks.Bedrock);
        }
    }

    /// <summary>The columns <see cref="StampFoundation"/> fills, for a caller recording what it covered. Its
    /// footprint is <b>max-exclusive</b> and a provenance rect is max-inclusive, so the two disagree by a
    /// column on each axis wherever the bounds are carried across by hand rather than walked — which is what
    /// this exists to stop.</summary>
    public static IEnumerable<(int X, int Z)> FoundationCells(int minX, int minZ, int maxX, int maxZ)
    {
        for (var x = minX; x < maxX; x++)
        for (var z = minZ; z < maxZ; z++)
            yield return (x, z);
    }

    /// <summary>Lay a redstone-wire row on top of the surface between the two given block ends (inclusive),
    /// with a redstone torch replacing the wire at each end (ST1 — the conventional entrance-protection
    /// marker). The wire is stamped at full strength (its data value is the signal level, so 15 = a fully
    /// lit line). The two ends must share an axis (a straight row); the row is one block wide.</summary>
    public static void StampRedstoneLine(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int x1, int z1, int x2, int z2)
    {
        var cells = RedstoneLineCells(x1, z1, x2, z2).ToList();
        for (var i = 0; i < cells.Count; i++)
        {
            var (x, z) = cells[i];
            var y = surfaceTop.GetValueOrDefault((x, z), 1);
            if (i == 0 || i == cells.Count - 1)
                world.SetBlock(x, y, z, Blocks.RedstoneTorch);
            else
                world.SetBlock(x, y, z, Blocks.RedstoneWire, 15);
        }
    }

    /// <summary>The columns <see cref="StampRedstoneLine"/> writes: the run itself, end to end. A caller
    /// recording what it covered walks this rather than the bounding rectangle of the two ends — the two are
    /// the same set only while the run is axis-aligned, which is what <c>EntranceRow</c> happens to produce
    /// and nothing states as a rule.</summary>
    public static IEnumerable<(int X, int Z)> RedstoneLineCells(int x1, int z1, int x2, int z2)
    {
        var dx = Math.Sign(x2 - x1);
        var dz = Math.Sign(z2 - z1);
        var steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(z2 - z1));
        for (var i = 0; i <= steps; i++) yield return (x1 + dx * i, z1 + dz * i);
    }

    /// <summary>Place a 4×4×4 iron-block cube whose base rests on the surface its footprint spans and whose
    /// top sits three blocks above it (ST2/ST3). The 4-wide footprint is centred on the anchor
    /// (<c>[anchor-2, anchor+2)</c>), so a marker snapped to a whole block splits two-and-two cleanly — which
    /// is also why the base comes from the footprint (<see cref="PositionSnap.SurfaceYOver"/>) and not the
    /// anchor column: the anchor is a grid line, and sampling one side of it does not survive the symmetry
    /// orbit.</summary>
    public static void StampIronCube(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop, int anchorX, int anchorZ)
    {
        var (minX, minZ, maxX, maxZ) = IronCubeFootprint(anchorX, anchorZ);
        var baseY = PositionSnap.SurfaceYOver(surfaceTop, minX, minZ, maxX, maxZ, 1);
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

    /// <summary>Place a <paramref name="size"/>³ iron-block cube over the given footprint min corner,
    /// resting on the surface its footprint spans — the spawn-side renewable, sized by its marker's parity
    /// (WX8; the legacy marker-anchored 4×4 is <see cref="StampIronCube"/>).</summary>
    public static void StampIronCubeAt(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop, int minX, int minZ, int size)
    {
        var baseY = PositionSnap.SurfaceYOver(surfaceTop, minX, minZ, minX + size - 1, minZ + size - 1, 1);
        for (var lx = 0; lx < size; lx++)
        for (var lz = 0; lz < size; lz++)
        for (var ly = 0; ly < size; ly++)
            world.SetBlock(minX + lx, baseY + ly, minZ + lz, Blocks.IronBlock);
    }

    /// <summary>The platform's XZ span in blocks — a fixed 5×5, independent of the destroyable's own
    /// footprint, because its job is stopping a shaft dug up from below rather than matching what stands
    /// on it.</summary>
    public const int PlatformSize = 5;

    /// <summary>How many courses of ordinary terrain a goal's plate is buried under, counted from the
    /// ground's own top block (the author's number). One is enough to stop an undermine and leaves nothing
    /// between the surface and the rock; three opens a two-course space under the goal, which is where its
    /// defence chest stands.</summary>
    public const int PlatformDepth = 3;

    /// <summary>Bury a one-block-thick bedrock plate under a footprint, <see cref="PlatformDepth"/> courses
    /// beneath the ground's own surface block, and set a defence chest into the space that opens over it: the
    /// goal's foundation stands on ordinary terrain, and this sits below it, so a tunnel dug up from below
    /// meets unbreakable rock before it reaches daylight and the ground the goal stands on cannot be mined
    /// out from under it. One course is deliberately the whole of the plate — a thicker slab reads as a wall
    /// grown out of the floor rather than a plate under it. Footprint is inclusive on every side; sampling
    /// the whole footprint (rather than a single anchor column) is what keeps the plate level and survives
    /// the symmetry orbit the same way every other structure here does. No-ops where the terrain is too
    /// shallow to bury the plate under.
    ///
    /// <para><b>The chest is a cache under the monument</b>, reached by digging straight down rather than
    /// through a face, which is why it takes <see cref="DefenseChest.Embed"/> directly instead of the wall's
    /// own placement: it stands on the plate at the centre of the footprint, the course over it is carved to
    /// air so the lid opens, and the surface course above that is left whole — so a defender breaks one
    /// block of ground and drops onto the supply.</para></summary>
    public static void StampPlatform(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int minX, int minZ, int maxX, int maxZ)
    {
        var groundTop = PositionSnap.SurfaceYOver(surfaceTop, minX, minZ, maxX, maxZ, 1);
        var plateY = groundTop - 1 - PlatformDepth;   // PlatformDepth courses below the ground's top block
        if (plateY < 0) return;
        for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
            world.SetBlock(x, plateY, z, Blocks.Bedrock);

        // The centre of the plate, which is the goal's own anchor column on every footprint this is called
        // with. Facing is the default: there is no approach to front, since the way in is from above.
        DefenseChest.Embed(world, (minX + maxX) / 2, plateY + 1, (minZ + maxZ) / 2, DefenseChest.Facing(0, 1));
    }

    /// <summary>Raise a solid bedrock wall over a seam footprint from y=0 up to <paramref name="topY"/>
    /// inclusive, and lay one course of cobweb over it (ST4). Footprint is min-inclusive, max-exclusive; it is
    /// two blocks thick across the seam and spans the full shared-interface width along it.
    /// <para>The web is the wall's last course, not decoration on it: a bedrock line a team cannot break is
    /// also a line they cannot see over, so an attacker who bridges to the top meets a course that costs time
    /// to cross and can be cut away with the shears every kit carries. It is why the bedrock itself is short —
    /// the barrier is three courses of stone and one of web, not five of stone.</para></summary>
    public static void StampWall(VoxelWorld world, int minX, int minZ, int maxX, int maxZ, int topY)
    {
        var top = Math.Clamp(topY, 0, VoxelWorld.MaxHeight - 1);
        foreach (var (x, z) in WallCells(minX, minZ, maxX, maxZ))
        {
            for (var y = 0; y <= top; y++) world.SetBlock(x, y, z, Blocks.Bedrock);
            if (top + 1 < VoxelWorld.MaxHeight) world.SetBlock(x, top + 1, z, Blocks.Cobweb);
        }
    }

    /// <summary>The columns <see cref="StampWall"/> fills, for a caller recording what it covered — the same
    /// reason <see cref="FoundationCells"/> and <see cref="RedstoneLineCells"/> exist. The footprint is
    /// <b>max-exclusive</b> and a provenance rect is max-inclusive, so a wall whose bounds are carried across
    /// by hand is recorded a column wider than it is built on each axis, and every read that trusts the
    /// sidecar draws a bedrock line thicker than it plays — which is exactly what decides whether it can be
    /// built over.</summary>
    public static IEnumerable<(int X, int Z)> WallCells(int minX, int minZ, int maxX, int maxZ)
    {
        for (var x = minX; x < maxX; x++)
        for (var z = minZ; z < maxZ; z++)
            yield return (x, z);
    }
}
