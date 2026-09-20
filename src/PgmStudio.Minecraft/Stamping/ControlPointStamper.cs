using PgmStudio.Domain;
using PgmStudio.Geom;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;

namespace PgmStudio.Minecraft.Stamping;

/// <summary>
/// Stamps the CP/KotH objective: a flat square pad of coloured clay, level, set into the terrain rather
/// than standing over it.
///
/// <para><b>The one rule that separates it from every other objective stamper.</b> A destroyable and a core
/// <em>float</em> — the gap is what a raid has to climb and what a core's lava falls through. A hill is the
/// opposite: it is ground, and a player has to be able to stand on it, so the pad replaces the top course of
/// the terrain and the capture volume is the air immediately above. A hill stamped with a float is a hill
/// nobody can capture.</para>
///
/// <para>The pad is laid at one height across its whole footprint, because the progress display is drawn as
/// a pie about the centre of the blocks PGM finds, and a pad that follows a slope draws that pie across
/// several courses. Where the ground falls away under it the pad skirts down to meet it, so it reads as a
/// plinth cut into the hillside rather than a sheet hanging off one.</para>
/// </summary>
public static class ControlPointStamper
{
    /// <summary>How far the skirt will chase falling ground before it stops. A pad on a lip has a side or
    /// two of skirt, which is what the corpus's own plinths look like; a pad drawn over a cliff would
    /// otherwise build a tower down to the void, so the fall is bounded and the rest is left as it is.</summary>
    public const int MaxSkirt = 8;

    /// <summary>
    /// The block the pad's top course occupies: its footprint centred on the anchor, at the highest ground
    /// the footprint spans. The whole footprint is probed rather than the anchor column — the same reason
    /// every other stamper does, so the height survives the symmetry orbit.
    /// </summary>
    public static BlockBox PadBox(
        IReadOnlyDictionary<(int X, int Z), int> surfaceTop, int anchorX, int anchorZ, int size)
    {
        var (width, depth) = ObjectiveFootprint.ControlPoint(size);
        var (minX, minZ, maxX, maxZ) = ObjectiveFootprint.Centred(anchorX, anchorZ, width, depth);
        // SurfaceYOver answers the first AIR over the ground; the pad is the course below it, because a pad
        // is walked on rather than stood over.
        var padY = PositionSnap.SurfaceYOver(surfaceTop, minX, minZ, maxX, maxZ, 1) - 1;
        return new BlockBox(minX, padY, minZ, maxX, padY, maxZ);
    }

    /// <summary>
    /// The volume a player has to be inside for their team to hold the point: the pad's own footprint,
    /// starting at the pad course and <paramref name="height"/> blocks tall.
    /// <para>It starts at the pad rather than above it on purpose. PGM tests the block a player's feet are
    /// in, which on a pad at <c>y</c> is <c>y + 1</c>; beginning at <c>y</c> puts that block in the middle of
    /// the volume with room over it for a jump, and matches what 221 of 344 corpus points do.</para>
    /// </summary>
    public static BlockBox CaptureBox(BlockBox pad, int height = ObjectiveDefaults.ControlPointCaptureHeight)
        => pad with { MaxY = pad.MinY + Math.Max(1, height) - 1 };

    /// <summary>
    /// Lay the pad: one course of <paramref name="material"/>:<paramref name="color"/> filling
    /// <paramref name="pad"/>, and under each column a skirt down to the ground that column actually has, so
    /// the pad has no air beneath it.
    /// <para>The material must be one PGM recolours — stained clay, wool, stained glass and the rest of
    /// <c>ColorUtils</c>'s set. A pad in anything else is a hill that never changes colour, which is the map's
    /// only signal that it was captured.</para>
    /// </summary>
    public static void StampPad(
        VoxelWorld world, BlockBox pad, IReadOnlyDictionary<(int X, int Z), int> surfaceTop,
        int material = Blocks.StainedClay, int color = 0)
    {
        for (var x = pad.MinX; x <= pad.MaxX; x++)
        for (var z = pad.MinZ; z <= pad.MaxZ; z++)
        {
            world.SetBlock(x, pad.MinY, z, material, color);

            // The column's own ground, as the first air over it — everything from there up to the pad is the
            // hole the pad would otherwise hang over.
            var groundTop = surfaceTop.TryGetValue((x, z), out var top) ? top - 1 : pad.MinY;
            for (var y = pad.MinY - 1; y > groundTop && y >= pad.MinY - MaxSkirt; y--)
                world.SetBlock(x, y, z, material, color);
        }
    }

    /// <summary>
    /// Clear the capture volume of anything standing in it, so a pad laid under a tree or through a wall is
    /// still a place a player can stand. The pad course itself is left alone — it is the floor, not the
    /// volume.
    /// </summary>
    public static void ClearCaptureVolume(VoxelWorld world, BlockBox capture)
    {
        for (var x = capture.MinX; x <= capture.MaxX; x++)
        for (var z = capture.MinZ; z <= capture.MaxZ; z++)
        for (var y = capture.MinY + 1; y <= capture.MaxY; y++)
            world.SetBlock(x, y, z, Blocks.Air);
    }
}
