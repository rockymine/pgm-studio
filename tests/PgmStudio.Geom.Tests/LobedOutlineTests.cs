using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// <see cref="LobedOutline"/> — an ellipse pulled in and out by lobes and turned. What is asserted is the shape
/// the numbers state: no wobble is the ellipse itself, the radius swells once per lobe and by the wobble, and a
/// turn turns the whole outline about its centre.
/// </summary>
public sealed class LobedOutlineTests
{
    [Test]
    public async Task A_wobble_of_nought_is_the_ellipse_itself()
    {
        var ring = LobedOutline.Of(10, -5, 12, 6, points: 24, lobes: 3, wobble: 0, phase: 0, turnDegrees: 0);

        await Assert.That(ring.Length).IsEqualTo(24);
        foreach (var point in ring)
        {
            var onEllipse = Math.Pow((point[0] - 10) / 12, 2) + Math.Pow((point[1] + 5) / 6, 2);
            await Assert.That(onEllipse).IsEqualTo(1).Within(0.03).Because($"({point[0]}, {point[1]}) is on the ellipse");
        }
    }

    [Test]
    public async Task The_radius_swells_once_for_every_lobe_and_by_the_wobble()
    {
        var ring = LobedOutline.Of(0, 0, 10, 10, points: 60, lobes: 3, wobble: 0.2, phase: 0, turnDegrees: 0);
        var reach = ring.Select(point => Math.Sqrt(point[0] * point[0] + point[1] * point[1])).ToArray();

        var peaks = Enumerable.Range(0, reach.Length)
            .Count(index => reach[index] > reach[(index + reach.Length - 1) % reach.Length]
                            && reach[index] >= reach[(index + 1) % reach.Length]);
        await Assert.That(peaks).IsEqualTo(3);
        await Assert.That(reach.Max()).IsEqualTo(12).Within(0.1);
        await Assert.That(reach.Min()).IsEqualTo(8).Within(0.1);
    }

    [Test]
    public async Task A_turn_turns_the_whole_outline_about_its_centre()
    {
        var plain = LobedOutline.Of(4, 4, 10, 3, points: 16, lobes: 2, wobble: 0.1, phase: 0.4, turnDegrees: 0);
        var turned = LobedOutline.Of(4, 4, 10, 3, points: 16, lobes: 2, wobble: 0.1, phase: 0.4, turnDegrees: 90);

        for (var index = 0; index < plain.Length; index++)
        {
            // A quarter turn takes (x, z) about the centre to (−z, x).
            await Assert.That(turned[index][0]).IsEqualTo(4 - (plain[index][1] - 4)).Within(0.11);
            await Assert.That(turned[index][1]).IsEqualTo(4 + (plain[index][0] - 4)).Within(0.11);
        }
    }
}
