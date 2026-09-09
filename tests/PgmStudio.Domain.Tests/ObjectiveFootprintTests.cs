using PgmStudio.Domain;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// A goal's orbit image, against the footprint it actually occupies.
///
/// <para>The image of a structure is the image of the ground it covers. Fanning the anchor on its own is
/// right only while the structure sits symmetrically around it, and <see cref="ObjectiveFootprint.Centred"/>
/// deliberately leans an even-sided one a block further along +X/+Z to match the stamper. An image that
/// reverses an axis has to reverse that lean; found on a hand-built board where a <c>cube-4</c> sat snug in
/// its cage on one half and a block into the wall on the other, on both axes at once.</para>
/// </summary>
public sealed class ObjectiveFootprintTests
{
    /// <summary>The mirror of a box, cell by cell — what the image's own footprint has to be.</summary>
    private static (int MinX, int MinZ, int MaxX, int MaxZ) Mirrored(
        (int MinX, int MinZ, int MaxX, int MaxZ) box, string mode, int k)
    {
        var a = PgmStudio.Geom.Symmetry.Cell(box.MinX, box.MinZ, mode, 0, 0, k);
        var b = PgmStudio.Geom.Symmetry.Cell(box.MaxX, box.MaxZ, mode, 0, 0, k);
        return (Math.Min(a.X, b.X), Math.Min(a.Z, b.Z), Math.Max(a.X, b.X), Math.Max(a.Z, b.Z));
    }

    /// <summary>Every destroyable style, on and off the axis, under both order-2 modes: the image's stamped
    /// footprint is the mirror of the source's. The even sizes are what this is for — an odd one passes
    /// under either rule, since (n−1)/2 is symmetric whenever n is odd.</summary>
    [Test]
    [Arguments("rot_180")]
    [Arguments("mirror_x")]
    [Arguments("mirror_z")]
    public async Task A_goals_image_covers_the_mirror_of_the_ground_it_covers(string mode)
    {
        foreach (var style in Enum.GetValues<DestroyableStyle>())
        {
            var (width, _, depth) = ObjectiveFootprint.Destroyable(style);
            foreach (var (ax, az) in new[] { (0, -43), (7, -21), (-4, 12), (0, 0), (13, 5) })
            {
                var source = ObjectiveFootprint.Centred(ax, az, width, depth);
                var (ix, iz) = ObjectiveFootprint.ImageAnchor(ax, az, mode, 0, 0, 1, width, depth);
                var image = ObjectiveFootprint.Centred(ix, iz, width, depth);

                await Assert.That(image).IsEqualTo(Mirrored(source, mode, 1))
                    .Because($"{style} at ({ax},{az}) under {mode}");
            }
        }
    }

    /// <summary>The board this was found on, in its own numbers: a `cube-4` anchored at (0, −43) on a
    /// rot_180 board. The image spans x −3..0 and z 40..43, so its anchor is (−2, 41). Fanning the anchor
    /// alone answered (−1, 42) and stamped the emerald a block into the cage wall on both axes.</summary>
    [Test]
    public async Task The_cube_four_that_found_this_lands_on_its_own_reflection()
    {
        var (width, _, depth) = ObjectiveFootprint.Destroyable(DestroyableStyle.Cube4);
        await Assert.That(ObjectiveFootprint.Centred(0, -43, width, depth)).IsEqualTo((-1, -44, 2, -41));

        var image = ObjectiveFootprint.ImageAnchor(0, -43, "rot_180", 0, 0, 1, width, depth);
        await Assert.That(image).IsEqualTo((-2, 41));
        await Assert.That(ObjectiveFootprint.Centred(image.X, image.Z, width, depth))
                    .IsEqualTo((-3, 40, 0, 43));
    }

    /// <summary>An odd-sided goal is unmoved by the fix, which is why a corpus of them never showed it.</summary>
    [Test]
    public async Task An_odd_sided_goal_answers_as_it_always_did()
    {
        var (width, _, depth) = ObjectiveFootprint.Destroyable(DestroyableStyle.Cube3);
        await Assert.That(ObjectiveFootprint.ImageAnchor(0, -43, "rot_180", 0, 0, 1, width, depth))
                    .IsEqualTo((-1, 42));
    }

    /// <summary>The identity image is the anchor's own cell, whatever the size.</summary>
    [Test]
    public async Task The_zeroth_image_is_the_anchor_itself()
    {
        foreach (var n in new[] { 1, 3, 4, 5 })
            await Assert.That(ObjectiveFootprint.ImageAnchor(6.5, -2.5, "rot_180", 0, 0, 0, n, n))
                        .IsEqualTo(ObjectiveFootprint.AnchorCell(6.5, -2.5));
    }
}
