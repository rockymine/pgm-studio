using PgmStudio.Geom;

namespace PgmStudio.Geom.Tests;

/// <summary>Minecraft yaw convention: 0 → +Z (south), 90 → -X (west), 180 → -Z (north), -90 → +X (east).</summary>
public sealed class HeadingTests
{
    [Test]
    [Arguments(0, 0, 0, 10, 0.0)]      // look +Z (south)
    [Arguments(0, 0, 0, -10, 180.0)]   // look -Z (north)
    [Arguments(0, 0, 10, 0, -90.0)]    // look +X (east)
    [Arguments(0, 0, -10, 0, 90.0)]    // look -X (west)
    public async Task CardinalDirections(double x, double z, double tx, double tz, double expected)
    {
        await Assert.That(Heading.YawTo(x, z, tx, tz)).IsEqualTo(expected).Within(1e-9);
    }

    [Test]
    public async Task SoutheastIsHalfwayBetweenSouthAndEast()
    {
        // +X and +Z equally → between south (0) and east (-90) → -45.
        await Assert.That(Heading.YawTo(0, 0, 10, 10)).IsEqualTo(-45.0).Within(1e-9);
    }

    [Test]
    public async Task CoincidentTarget_isZero()
    {
        await Assert.That(Heading.YawTo(5, 5, 5, 5)).IsEqualTo(0.0);
    }

    [Test]
    public async Task TranslationInvariant_dependsOnlyOnDelta()
    {
        await Assert.That(Heading.YawTo(100, 200, 110, 200)).IsEqualTo(Heading.YawTo(0, 0, 10, 0)).Within(1e-9);
    }

    [Test]
    [Arguments(270.0, -90.0)]
    [Arguments(-270.0, 90.0)]
    [Arguments(180.0, 180.0)]
    [Arguments(-180.0, 180.0)]
    [Arguments(540.0, 180.0)]
    [Arguments(-45.0, -45.0)]
    public async Task Wrap_bringsAnyYawIntoTheGamesRange(double yaw, double expected)
    {
        await Assert.That(Heading.Wrap(yaw)).IsEqualTo(expected).Within(1e-9);
    }

    [Test]
    [Arguments(0, 10, 0, 0, 0, 0, 90.0)]     // straight down
    [Arguments(0, 0, 0, 0, 10, 0, -90.0)]    // straight up
    [Arguments(0, 10, 0, 10, 0, 0, 45.0)]    // down a 45° slope
    [Arguments(0, 5, 0, 0, 5, 10, 0.0)]      // level
    public async Task Pitch_isDegreesBelowTheHorizon(double x, double y, double z, double tx, double ty, double tz, double expected)
    {
        await Assert.That(Heading.PitchTo(x, y, z, tx, ty, tz)).IsEqualTo(expected).Within(1e-9);
    }

    [Test]
    public async Task YawTo_isAlwaysInTheGamesRange()
    {
        for (var degrees = 0; degrees < 360; degrees += 15)
        {
            var radians = degrees * Math.PI / 180;
            var yaw = Heading.YawTo(0, 0, Math.Cos(radians), Math.Sin(radians));
            await Assert.That(yaw > -180 && yaw <= 180).IsTrue();
        }
    }
}
