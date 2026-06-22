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
}
