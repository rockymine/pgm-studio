using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// How a mask outline measures a point: 0 at its middle, 1 on its rim, over 1 outside, and the same distances
/// from an image of the outline as from the outline itself.
/// </summary>
public sealed class MaskAreaTests
{
    private static readonly (double X, double Z)[] Square = [(0, 0), (20, 0), (20, 20), (0, 20)];

    [Test]
    public async Task An_ellipse_measures_in_its_own_radii()
    {
        var ellipse = MaskArea.Ellipse(50, 50, 10, 4);

        await Assert.That(ellipse.E(50, 50)).IsEqualTo(0).Within(1e-12);
        await Assert.That(ellipse.E(60, 50)).IsEqualTo(1).Within(1e-12);
        await Assert.That(ellipse.E(50, 54)).IsEqualTo(1).Within(1e-12);
        await Assert.That(ellipse.E(70, 50)).IsEqualTo(2).Within(1e-12);
        await Assert.That(ellipse.Scale).IsEqualTo(10);
    }

    [Test]
    public async Task A_turned_ellipse_swaps_its_radii_at_a_quarter_turn()
    {
        var turned = MaskArea.Ellipse(0, 0, 10, 4, angleDegrees: 90);

        await Assert.That(turned.E(0, 10)).IsEqualTo(1).Within(1e-9);
        await Assert.That(turned.E(4, 0)).IsEqualTo(1).Within(1e-9);
    }

    [Test]
    public async Task A_polygon_is_zero_at_its_deepest_point_and_one_on_its_outline()
    {
        var square = MaskArea.Polygon(Square);

        await Assert.That(square.E(10, 10)).IsEqualTo(0).Within(1e-9);
        await Assert.That(square.E(0, 10)).IsEqualTo(1).Within(1e-9);
        await Assert.That(square.E(5, 10)).IsEqualTo(1 - 5 / 9.5).Within(1e-9);
        await Assert.That(square.E(-9.5, 10)).IsEqualTo(2).Within(1e-9);
        await Assert.That(square.Scale).IsEqualTo(9.5).Within(1e-9);   // the deepest cell centre is 9.5 from the outline
    }

    [Test]
    public async Task A_polygon_never_measures_under_zero_inside()
    {
        var square = MaskArea.Polygon(Square);
        var lowest = double.PositiveInfinity;
        for (var x = 0; x < 20; x++)
            for (var z = 0; z < 20; z++)
                lowest = Math.Min(lowest, square.E(x + 0.5, z + 0.5));

        await Assert.That(lowest).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task A_board_is_zero_everywhere()
    {
        await Assert.That(MaskArea.Board.E(-900, 1234)).IsEqualTo(0);
    }

    [Test]
    [Arguments("rot_180")]
    [Arguments("mirror_x")]
    [Arguments("mirror_z")]
    [Arguments("mirror_d1")]
    [Arguments("rot_90")]
    public async Task An_image_measures_a_point_as_the_original_measures_the_point_it_came_from(string axis)
    {
        var ellipse = MaskArea.Ellipse(14, 6, 9, 3, angleDegrees: 30);
        var polygon = MaskArea.Polygon([(2, 3), (18, 5), (12, 17), (4, 11)]);

        foreach (var area in new[] { ellipse, polygon })
        {
            var image = area.Image(axis, 10, 10);
            foreach (var (x, z) in new[] { (1.5, 2.5), (14.5, 6.5), (9.5, 8.5), (30.5, 12.5) })
            {
                var (imageX, imageZ) = Symmetry.Apply(x, z, axis, 10, 10);
                await Assert.That(image.E(imageX, imageZ)).IsEqualTo(area.E(x, z)).Within(1e-9);
            }
        }
    }

    [Test]
    public async Task Fanning_adds_each_image_once()
    {
        var fanned = MaskArea.Fanned([MaskArea.Ellipse(4, 6, 3, 3)], ["rot_180"], 10, 10);

        await Assert.That(fanned.Count).IsEqualTo(2);
        await Assert.That(fanned[1].E(16, 14)).IsEqualTo(0).Within(1e-12);
    }

    [Test]
    public async Task An_area_standing_on_the_symmetry_is_not_doubled()
    {
        var circle = MaskArea.Ellipse(10, 10, 5, 5);
        var square = MaskArea.Polygon([(5, 5), (15, 5), (15, 15), (5, 15)]);
        var tilted = MaskArea.Ellipse(10, 10, 8, 3, angleDegrees: 25);

        await Assert.That(MaskArea.Fanned([circle], ["rot_180"], 10, 10).Count).IsEqualTo(1);
        await Assert.That(MaskArea.Fanned([square], ["rot_180"], 10, 10).Count).IsEqualTo(1);
        await Assert.That(MaskArea.Fanned([tilted], ["rot_180"], 10, 10).Count).IsEqualTo(1);
        await Assert.That(MaskArea.Fanned([circle], ["rot_90", "rot_180", "rot_270"], 10, 10).Count).IsEqualTo(1);
    }

    [Test]
    public async Task An_image_the_author_also_drew_is_not_doubled()
    {
        var drawn = new[] { MaskArea.Ellipse(4, 6, 3, 3), MaskArea.Ellipse(16, 14, 3, 3) };

        await Assert.That(MaskArea.Fanned(drawn, ["rot_180"], 10, 10).Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_board_has_no_image()
    {
        await Assert.That(MaskArea.Fanned([MaskArea.Board], ["rot_180"], 10, 10).Count).IsEqualTo(1);
    }

    private static readonly (double X, double Z)[] Track = [(0, 0), (10, 0), (10, 10)];

    [Test]
    public async Task A_line_is_zero_on_the_path_and_one_at_half_its_width()
    {
        var line = MaskArea.Line(Track, 6);

        await Assert.That(line.E(5, 0)).IsEqualTo(0).Within(1e-12);
        await Assert.That(line.E(5, 3)).IsEqualTo(1).Within(1e-12);
        await Assert.That(line.E(5, -3)).IsEqualTo(1).Within(1e-12);
        await Assert.That(line.E(5, -6)).IsEqualTo(2).Within(1e-12);
        await Assert.That(line.E(13, 5)).IsEqualTo(1).Within(1e-12);
        await Assert.That(line.Scale).IsEqualTo(3);
        await Assert.That(line.Length).IsEqualTo(20).Within(1e-12);
    }

    [Test]
    public async Task A_line_says_how_far_along_it_a_point_stands_and_which_hand_it_lies_on()
    {
        var line = MaskArea.Line(Track, 6);

        var south = line.Fix(4, 2);
        var north = line.Fix(4, -2);
        await Assert.That(south.Along).IsEqualTo(4).Within(1e-12);
        await Assert.That(south.Distance).IsEqualTo(2).Within(1e-12);
        await Assert.That(south.Side).IsEqualTo(1).Because("walking east, south is the right hand");
        await Assert.That(north.Side).IsEqualTo(-1);
        await Assert.That(line.Fix(11, 7).Along).IsEqualTo(17).Within(1e-12);
    }

    [Test]
    public async Task A_line_needs_two_points_and_a_width()
    {
        await Assert.That(() => MaskArea.Line([(0, 0)], 4)).Throws<ArgumentException>();
        await Assert.That(() => MaskArea.Line(Track, 0)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task A_lines_middle_is_halfway_along_its_length()
    {
        var (x, z) = MaskArea.Line(Track, 6).Centre;

        await Assert.That(x).IsEqualTo(10).Within(1e-12);
        await Assert.That(z).IsEqualTo(0).Within(1e-12);
    }

    [Test]
    [Arguments("rot_180")]
    [Arguments("mirror_x")]
    [Arguments("mirror_z")]
    [Arguments("rot_90")]
    public async Task A_lines_image_measures_a_point_as_the_line_measures_the_point_it_came_from(string axis)
    {
        var line = MaskArea.Line(Track, 6);
        var image = line.Image(axis, 5, 5);
        foreach (var (x, z) in new[] { (4.5, 2.5), (3.5, -1.5), (12.5, 6.5) })
        {
            var (imageX, imageZ) = Symmetry.Apply(x, z, axis, 5, 5);
            await Assert.That(image.E(imageX, imageZ)).IsEqualTo(line.E(x, z)).Within(1e-9);
            await Assert.That(image.Fix(imageX, imageZ).Along).IsEqualTo(line.Fix(x, z).Along).Within(1e-9);
        }
    }

    [Test]
    public async Task A_reflected_line_keeps_the_hand_it_raised_on_the_image_of_that_side()
    {
        var line = MaskArea.Line([(0, 0), (10, 0)], 6);
        var mirrored = line.Image("mirror_z", 0, 5);
        var turned = line.Image("rot_180", 5, 0);

        // (5, 2) is on the right of the original; its reflection across z = 5 is (5, 8) and its half turn about (5, 0) is (5, -2).
        await Assert.That(line.Fix(5, 2).Side).IsEqualTo(1);
        await Assert.That(mirrored.Fix(5, 8).Side).IsEqualTo(1);
        await Assert.That(turned.Fix(5, -2).Side).IsEqualTo(1);
    }

    [Test]
    public async Task A_line_is_doubled_by_an_image_that_raises_the_other_hand_and_not_by_one_that_raises_the_same()
    {
        var through = MaskArea.Line([(0, 10), (20, 10)], 4);

        await Assert.That(MaskArea.Fanned([through], ["mirror_x"], 10, 10).Count).IsEqualTo(1)
            .Because("the reflection across x = 10 swaps the line's ends and keeps its south hand south");
        await Assert.That(MaskArea.Fanned([through], ["mirror_z"], 10, 10).Count).IsEqualTo(2)
            .Because("the reflection across z = 10 lies on the strip but puts its right hand on the north");
        await Assert.That(MaskArea.Fanned([through], ["rot_180"], 10, 10).Count).IsEqualTo(2)
            .Because("the half turn lies on the strip and puts its right hand on the north");
    }

    [Test]
    public async Task A_smoothed_line_passes_through_its_points_and_keeps_its_width()
    {
        var line = MaskArea.Line([(0, 0), (10, 0), (10, 10), (20, 10)], 4);
        var smooth = line.Smoothed(0.5);

        await Assert.That(smooth.Scale).IsEqualTo(2);
        await Assert.That(smooth.Points.Count).IsGreaterThan(line.Points.Count);
        foreach (var (x, z) in line.Points)
            await Assert.That(smooth.E(x, z)).IsEqualTo(0).Within(1e-9);
        await Assert.That(smooth.E(10, 0)).IsEqualTo(0).Within(1e-9);
    }
}
