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
    /// <summary>Level the ground under a footprint, so what stands on it has no holes in its floor. This is
    /// the ground the building sits on, not the course a player walks on — that one is the shell's own floor
    /// part. Footprint is min-inclusive, max-exclusive (<c>[minX, maxX) × [minZ, maxZ)</c>).
    ///
    /// <para>The plinth is <b>level, at the footprint's own highest column</b>, because what stands on it is:
    /// a room takes one floor course over its whole frame, read from that same highest column, so a plinth
    /// following the ground column by column leaves the floor spanning air wherever the ground falls away
    /// under it. Levelling costs nothing where the ground is flat, which is the common case, and is the
    /// difference between a floor and a lid everywhere else.</para>
    ///
    /// <para>It fills in <b>stone</b>, which is what the terrain painter finishes (TP6 — the painter rewrites
    /// only stone), so the ground a room stands on is painted like the ground around it rather than reading as
    /// a made thing. A cell with no ground at all is left alone: there is nothing under it to level against,
    /// and building a column there would put a stack in open sky. Nothing is filled <b>downward</b>: what
    /// keeps a room from being entered from below is its own protection region, and a plinth sunk to
    /// <c>y 0</c> is a wall nobody drew.</para></summary>
    public static void StampFoundation(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int minX, int minZ, int maxX, int maxZ)
    {
        var level = 1;
        foreach (var cell in FoundationCells(minX, minZ, maxX, maxZ))
            level = Math.Max(level, surfaceTop.GetValueOrDefault(cell, 1));   // topmost air cell

        foreach (var cell in FoundationCells(minX, minZ, maxX, maxZ))
        {
            if (!surfaceTop.TryGetValue(cell, out var top)) continue;         // no ground here to stand on
            var (x, z) = cell;
            for (var y = top; y < level; y++) world.SetBlock(x, y, z, Blocks.Stone);
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

    /// <summary>Place a <paramref name="size"/>³ iron-block cube over the given footprint min corner, resting
    /// on the surface its footprint spans (ST3). Every cube on a board comes through here, seated where
    /// <see cref="PgmStudio.Domain.RoomFrames.PlaceIron"/> resolved it (WX8). The base comes from the whole
    /// footprint (<see cref="PositionSnap.SurfaceYOver"/>) rather than one column, so an orbit image of the
    /// cube rests at the height its own image of the ground reached.</summary>
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

    /// <summary>Bury a one-block-thick bedrock plate <see cref="PlatformDepth"/> courses beneath
    /// <paramref name="groundTop"/>, the surface block of the ground the goal it protects is standing on: the
    /// goal's foundation stands on ordinary terrain and this sits below it, so a tunnel dug up from beneath
    /// meets unbreakable rock before it reaches daylight and the ground the goal stands on cannot be mined
    /// out from under it. One course is deliberately the whole of the plate — a thicker slab reads as a wall
    /// grown out of the floor rather than a plate under it. Footprint is inclusive on every side. No-ops
    /// where the terrain is too shallow to bury the plate under.
    ///
    /// <para><b>The ground is the caller's, and it is the goal's own.</b> The plate is a fixed
    /// <see cref="PlatformSize"/> square that the goal did not choose, so it routinely overhangs whatever the
    /// goal stands on — the lip of a platform, a stair beside it, the street over a sunken hall — and a plate
    /// resolving its own height over that footprint takes the highest column in it and buries itself under
    /// something else entirely. Reading the depth off the goal's own floor is what keeps the two from
    /// answering different questions about one place: there is one surface read per objective, and both the
    /// box and the plate are measured from it.</para>
    ///
    /// <para><b>A destroyable stands over one and a core does not</b> (the author's ruling). A destroyable is
    /// broken from above and nothing under it is play; a core is won by digging under it until the lava
    /// leaks, so the plate would be a floor the objective's own rules tell players to break through. Capping
    /// the dig at the plate's depth would make a core's <c>float</c>/<c>leak</c> pair asking for a deeper dig
    /// a pair the terrain silently refuses.</para></summary>
    public static void StampPlatform(
        VoxelWorld world, int groundTop, int minX, int minZ, int maxX, int maxZ)
    {
        var plateY = groundTop - 1 - PlatformDepth;   // PlatformDepth courses below the ground's top block
        if (plateY < 0) return;
        for (var x = minX; x <= maxX; x++)
        for (var z = minZ; z <= maxZ; z++)
            world.SetBlock(x, plateY, z, Blocks.Bedrock);
    }

    /// <summary>Set a goal's defence chest into the ground beside it. <b>The chest stands on the ground under
    /// the monument</b>, never on the plate: the plate is what goes into the terrain, the supply is what a
    /// defender walks up to, and one set into the plate is three courses down with whole ground over it — a
    /// cache nobody can see, reach, or guess at. So it takes <c>DefenseChest.Embed</c> at the centre column's
    /// own surface, with the course over it carved for the lid. It has no approach to front, the monument
    /// standing over it being reached from every side, so its facing is the default.
    ///
    /// <para>Separate from the plate because the two do not go together: every goal a team defends is worth
    /// supplying, and only a destroyable is worth flooring.</para></summary>
    public static void StampDefenseChest(
        VoxelWorld world, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int minX, int minZ, int maxX, int maxZ)
    {
        // The centre of the footprint, which is the goal's own anchor column on every footprint this is
        // called with. The chest rests on that column's own surface rather than on any level a plate was cut
        // at, so it stands on the ground beside the monument however the terrain runs under it.
        var groundTop = PositionSnap.SurfaceYOver(surfaceTop, minX, minZ, maxX, maxZ, 1);
        var (chestX, chestZ) = ((minX + maxX) / 2, (minZ + maxZ) / 2);
        DefenseChest.Embed(world, chestX, surfaceTop.GetValueOrDefault((chestX, chestZ), groundTop), chestZ,
                           DefenseChest.Facing(0, 1));
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
