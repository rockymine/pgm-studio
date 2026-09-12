using PgmStudio.Domain;
using PgmStudio.Minecraft.Anvil;
using PgmStudio.Minecraft.Palette;
using PgmStudio.Minecraft.Stamping;

namespace PgmStudio.Minecraft.Tests;

/// <summary>
/// The CP/KotH pad. The invariant every case here turns on is that a hill is <b>ground</b>: a destroyable
/// and a core float, and a pad that did would be a hill nobody could stand on — so the pad replaces the top
/// course of the terrain and the capture volume is the air over it.
/// </summary>
public sealed class ControlPointStamperTests
{
    // surfaceTop is the topmost AIR cell, so solid ground ends one block below it.
    private static Dictionary<(int X, int Z), int> FlatSurface(int y = 64)
    {
        var top = new Dictionary<(int X, int Z), int>();
        for (var x = -16; x <= 16; x++)
        for (var z = -16; z <= 16; z++)
            top[(x, z)] = y;
        return top;
    }

    // ── the pad is the ground, not a thing standing on it ───────────────────────────
    [Test]
    public async Task The_pad_replaces_the_top_course_of_the_terrain()
    {
        var pad = ControlPointStamper.PadBox(FlatSurface(64), 0, 0, 7);
        // Ground ends at 63; the pad is that course, not the air at 64.
        await Assert.That(pad.MinY).IsEqualTo(63);
        await Assert.That(pad.Height).IsEqualTo(1);
    }

    [Test]
    [Arguments(3)]
    [Arguments(5)]
    [Arguments(7)]
    [Arguments(11)]
    public async Task The_pad_is_square_and_the_size_it_was_asked_for(int size)
    {
        var pad = ControlPointStamper.PadBox(FlatSurface(), 0, 0, size);
        await Assert.That(pad.Width).IsEqualTo(size);
        await Assert.That(pad.Depth).IsEqualTo(size);
    }

    // The whole point of the volume: a player standing on the pad occupies the block above it, so a capture
    // region that does not reach there is a hill nobody can take.
    [Test]
    public async Task The_capture_volume_holds_the_block_a_standing_player_occupies()
    {
        var pad = ControlPointStamper.PadBox(FlatSurface(64), 0, 0, 7);
        var capture = ControlPointStamper.CaptureBox(pad);

        await Assert.That(capture.MinY).IsEqualTo(pad.MinY);
        await Assert.That(capture.MaxY).IsGreaterThanOrEqualTo(pad.MinY + 1);
        await Assert.That(capture.Height).IsEqualTo(ObjectiveDefaults.ControlPointCaptureHeight);
    }

    [Test]
    public async Task The_capture_volume_covers_exactly_the_pads_footprint()
    {
        var pad = ControlPointStamper.PadBox(FlatSurface(), 0, 0, 9);
        var capture = ControlPointStamper.CaptureBox(pad);
        await Assert.That(capture.MinX).IsEqualTo(pad.MinX);
        await Assert.That(capture.MaxX).IsEqualTo(pad.MaxX);
        await Assert.That(capture.MinZ).IsEqualTo(pad.MinZ);
        await Assert.That(capture.MaxZ).IsEqualTo(pad.MaxZ);
    }

    // ── what it lays ────────────────────────────────────────────────────────────────
    [Test]
    public async Task Every_block_of_the_pad_is_laid_in_a_colour_affected_material()
    {
        var world = new VoxelWorld();
        var surface = FlatSurface(64);
        var pad = ControlPointStamper.PadBox(surface, 0, 0, 7);
        ControlPointStamper.StampPad(world, pad, surface, Blocks.StainedClay, 0);

        for (var x = pad.MinX; x <= pad.MaxX; x++)
        for (var z = pad.MinZ; z <= pad.MaxZ; z++)
        {
            await Assert.That(world.GetBlock(x, pad.MinY, z).Id).IsEqualTo(Blocks.StainedClay);
            await Assert.That(world.GetBlock(x, pad.MinY, z).Data).IsEqualTo(0);   // white = the neutral PGM restores
        }
    }

    // ── uneven ground ───────────────────────────────────────────────────────────────
    // A pad that followed a slope would draw its progress pie across several courses, so it is laid level —
    // at the highest ground it spans, which is also what keeps it from being buried.
    [Test]
    public async Task The_pad_is_level_at_the_highest_ground_it_spans()
    {
        var surface = FlatSurface(64);
        foreach (var z in new[] { -1, 0, 1 }) surface[(2, z)] = 68;   // a lip under one side

        var pad = ControlPointStamper.PadBox(surface, 0, 0, 7);
        await Assert.That(pad.MinY).IsEqualTo(67);
    }

    [Test]
    public async Task Where_the_ground_falls_away_the_pad_skirts_down_to_meet_it()
    {
        var world = new VoxelWorld();
        var surface = FlatSurface(64);
        foreach (var z in new[] { -1, 0, 1 }) surface[(2, z)] = 68;

        var pad = ControlPointStamper.PadBox(surface, 0, 0, 7);
        ControlPointStamper.StampPad(world, pad, surface, Blocks.StainedClay, 0);

        // The low columns are filled from the pad course down to their own ground: no air under the pad.
        for (var y = pad.MinY; y > 63; y--)
            await Assert.That(world.GetBlock(0, y, 0).Id).IsEqualTo(Blocks.StainedClay);
        // And the ground the column already had is left where it was.
        await Assert.That(world.GetBlock(0, 63, 0).Id).IsNotEqualTo(Blocks.StainedClay);
    }

    // A pad over a drop must not build a tower down to the void.
    [Test]
    public async Task The_skirt_stops_after_a_bounded_fall()
    {
        var world = new VoxelWorld();
        var surface = FlatSurface(64);
        surface[(0, 0)] = 4;   // a column that falls away to nothing

        var pad = ControlPointStamper.PadBox(surface, 0, 0, 7);
        ControlPointStamper.StampPad(world, pad, surface, Blocks.StainedClay, 0);

        await Assert.That(world.GetBlock(0, pad.MinY - ControlPointStamper.MaxSkirt, 0).Id).IsEqualTo(Blocks.StainedClay);
        await Assert.That(world.GetBlock(0, pad.MinY - ControlPointStamper.MaxSkirt - 1, 0).Id).IsEqualTo(Blocks.Air);
    }

    // ── the volume is cleared ───────────────────────────────────────────────────────
    [Test]
    public async Task Clearing_the_volume_leaves_the_pad_and_empties_the_air_over_it()
    {
        var world = new VoxelWorld();
        var surface = FlatSurface(64);
        var pad = ControlPointStamper.PadBox(surface, 0, 0, 7);
        ControlPointStamper.StampPad(world, pad, surface, Blocks.StainedClay, 0);

        var capture = ControlPointStamper.CaptureBox(pad);
        for (var y = capture.MinY + 1; y <= capture.MaxY; y++) world.SetBlock(0, y, 0, Blocks.Stone);
        ControlPointStamper.ClearCaptureVolume(world, capture);

        await Assert.That(world.GetBlock(0, pad.MinY, 0).Id).IsEqualTo(Blocks.StainedClay);   // the floor stays
        for (var y = capture.MinY + 1; y <= capture.MaxY; y++)
            await Assert.That(world.GetBlock(0, y, 0).Id).IsEqualTo(Blocks.Air);
    }
}
