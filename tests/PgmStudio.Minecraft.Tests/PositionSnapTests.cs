using PgmStudio.Minecraft;

namespace PgmStudio.Minecraft.Tests;

/// <summary>Snapping + door orientation for structure anchoring (spec §5).</summary>
public sealed class PositionSnapTests
{
    [Test]
    public async Task Xz_rounds_to_whole_integers()
    {
        await Assert.That(PositionSnap.SnapXZ(12.5, -8.4)).IsEqualTo((13, -8));
        await Assert.That(PositionSnap.SnapXZ(0.49, 0.5)).IsEqualTo((0, 1));
    }

    [Test]
    public async Task Surface_y_uses_the_column_top_or_the_fallback()
    {
        var surface = new Dictionary<(int, int), int> { [(3, 4)] = 70 };
        await Assert.That(PositionSnap.SurfaceY((3, 4), surface, 1)).IsEqualTo(70);
        await Assert.That(PositionSnap.SurfaceY((9, 9), surface, 1)).IsEqualTo(1);
    }

    [Test]
    public async Task Yaw_maps_to_the_door_facing()
    {
        await Assert.That(PositionSnap.FacingFromYaw(0)).IsEqualTo(Facing.PosZ);     // south
        await Assert.That(PositionSnap.FacingFromYaw(90)).IsEqualTo(Facing.NegX);    // west
        await Assert.That(PositionSnap.FacingFromYaw(180)).IsEqualTo(Facing.NegZ);   // north
        await Assert.That(PositionSnap.FacingFromYaw(-90)).IsEqualTo(Facing.PosX);   // east (wraps)
        await Assert.That(PositionSnap.FacingFromYaw(370)).IsEqualTo(Facing.PosZ);   // wraps to ~10°
    }
}
