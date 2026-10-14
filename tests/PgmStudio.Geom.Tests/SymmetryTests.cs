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
            (double X, double Z) Image(double x, double z) => Symmetry.Point(x, z, mode, 0, 0, k);
            var (ax, az) = Image(1, 0);
            var (bx, bz) = Image(0, 1);
            var turnedOver = ax * bz - az * bx < 0;
            await Assert.That(Symmetry.Reflects(mode, k)).IsEqualTo(turnedOver);
        }
    }

    /// <summary>Image 0 is the point itself under every mode, a mirror and the half-turn included.</summary>
    [Test]
    [Arguments("mirror_x")] [Arguments("mirror_d1")] [Arguments("rot_180")] [Arguments("rot_90")] [Arguments("none")]
    public async Task Image_zero_is_the_point_itself(string mode)
    {
        await Assert.That(Symmetry.Point(7.3, -2.6, mode, 0.5, 0.5, 0)).IsEqualTo((7.3, -2.6));
        await Assert.That(Symmetry.Cell(7, -3, mode, 0.5, 0.5, 0)).IsEqualTo((7, -3));
    }

    /// <summary>A caller holding one concrete axis gets the same image as one holding the mode and the image
    /// index that axis stands for — the point, the cell and the rectangle alike.</summary>
    [Test]
    [Arguments("mirror_x")] [Arguments("mirror_z")] [Arguments("mirror_d1")] [Arguments("mirror_d2")]
    [Arguments("rot_180")] [Arguments("rot_90")]
    public async Task An_axis_answers_what_its_mode_and_image_answer(string mode)
    {
        var axes = Symmetry.OrbitAxes(mode);
        for (var k = 1; k < Symmetry.Order(mode); k++)
        {
            var axis = axes[k - 1];
            await Assert.That(Symmetry.Apply(7.3, -2.6, axis, 0.5, 1)).IsEqualTo(Symmetry.Point(7.3, -2.6, mode, 0.5, 1, k));
            await Assert.That(Symmetry.ApplyCell(7, -3, axis, 0.5, 1)).IsEqualTo(Symmetry.Cell(7, -3, mode, 0.5, 1, k));
            await Assert.That(Symmetry.ApplyRect(2, -4, 9, 3, axis, 0.5, 1)).IsEqualTo(Symmetry.Rect(2, -4, 9, 3, mode, 0.5, 1, k));
        }
    }

    /// <summary>Every image of a point answers the same canonical member, which is what lets a value read
    /// there read alike at every image.</summary>
    [Test]
    [Arguments("rot_180")]
    [Arguments("rot_90")]
    [Arguments("mirror_x")]
    [Arguments("mirror_d2")]
    public async Task Every_image_of_a_point_answers_the_same_canonical_member(string mode)
    {
        var canonical = Symmetry.Canonical(7.3, -2.6, mode, 0.5, 0.5);
        for (var k = 1; k < Symmetry.Order(mode); k++)
        {
            var (x, z) = Symmetry.Point(7.3, -2.6, mode, 0.5, 0.5, k);
            await Assert.That(Symmetry.Canonical(x, z, mode, 0.5, 0.5)).IsEqualTo(canonical);
        }
    }

    /// <summary>A rectangle centred on the board's centre is its own image, and the vertex each lands on is
    /// named; the same rectangle off to one side is another shape's image, not its own.</summary>
    [Test]
    public async Task A_ring_about_the_centre_is_its_own_image_and_one_off_it_is_not()
    {
        double[][] centred = [[-10, -6], [10, -6], [10, 6], [-10, 6]];
        double[][] aside = [[20, -6], [40, -6], [40, 6], [20, 6]];

        await Assert.That(Symmetry.SelfImage(centred, "rot_180", 0, 0)![0]).IsEquivalentTo(new[] { 2, 3, 0, 1 });
        await Assert.That(Symmetry.SelfImage(centred, "mirror_x", 0, 0)![0]).IsEquivalentTo(new[] { 1, 0, 3, 2 });
        await Assert.That(Symmetry.SelfImage(aside, "rot_180", 0, 0)).IsNull();
        await Assert.That(Symmetry.SelfImage(centred, "none", 0, 0)).IsNull();
    }

    /// <summary>A turn keeps a ring's direction, so an edge lands on the edge leaving its first vertex's image;
    /// a reflection reverses it, so the edge lands on the one leaving its second vertex's image.</summary>
    [Test]
    public async Task An_edge_lands_on_the_edge_between_the_images_of_its_two_ends()
    {
        double[][] centred = [[-10, -6], [10, -6], [10, 6], [-10, 6]];

        await Assert.That(Symmetry.ImageEdge(Symmetry.SelfImage(centred, "rot_180", 0, 0)![0], 0)).IsEqualTo(2);
        await Assert.That(Symmetry.ImageEdge(Symmetry.SelfImage(centred, "mirror_x", 0, 0)![0], 0)).IsEqualTo(0);
        await Assert.That(Symmetry.ImageEdge(Symmetry.SelfImage(centred, "mirror_x", 0, 0)![0], 1)).IsEqualTo(3);
    }
}
