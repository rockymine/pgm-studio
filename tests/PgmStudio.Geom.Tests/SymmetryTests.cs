using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// The point half of the symmetry transform: what an orbit image is, as opposed to where a cell lands
/// (<see cref="SymmetryCellTests"/>).
/// </summary>
public sealed class SymmetryTests
{
    [Test]
    [Arguments("mirror_x")] [Arguments("mirror_z")] [Arguments("mirror_d1")] [Arguments("mirror_d2")]
    [Arguments("rot_180")] [Arguments("rot_90")] [Arguments("none")]
    public async Task An_image_reflects_exactly_where_the_transform_turns_a_hand_over(string mode)
    {
        // A left-handed pair of steps stays left-handed under a rotation and turns right-handed under a
        // reflection: the sign of the cross product of the two images is what says which happened.
        for (var k = 0; k < Symmetry.Order(mode); k++)
        {
            (double X, double Z) Image(double x, double z) => k == 0 ? (x, z) : Symmetry.Point(x, z, mode, 0, 0, k);
            var (ax, az) = Image(1, 0);
            var (bx, bz) = Image(0, 1);
            var turnedOver = ax * bz - az * bx < 0;
            await Assert.That(Symmetry.Reflects(mode, k)).IsEqualTo(turnedOver);
        }
    }
}
