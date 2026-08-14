using PgmStudio.Pgm.Render;

namespace PgmStudio.Pgm.Tests.Render;

/// <summary>The role/zone swatches every plan render draws from — <c>B95</c>'s fix lives here: a build zone
/// and a water lane must separate by <b>hue</b>, not merely by the shade/opacity/dash a still image can lose.
/// The bug this replaces was specifically a hue confusion (both colours read as "blue" to a viewer), which a
/// plain RGB distance does not reliably capture — #38bdf8 and #2563eb differ enough in brightness to score as
/// far apart by raw distance while still sitting in the same 23-degree hue wedge, so these tests measure hue
/// angle directly.</summary>
public sealed class PlanBoardPaletteTests
{
    /// <summary>Hue in degrees (0-360) off the packed RGB — the wheel position a viewer actually reads as
    /// "blue" or "pink", independent of how light or saturated the colour is.</summary>
    private static double Hue(int rgb)
    {
        double r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        if (delta < 1e-9) return 0;   // grey — hue is undefined; never hit by any colour tested here

        double hue = max == r ? 60 * (((g - b) / delta) % 6)
            : max == g ? 60 * ((b - r) / delta + 2)
            : 60 * ((r - g) / delta + 4);
        return hue < 0 ? hue + 360 : hue;
    }

    /// <summary>The smaller of the two arcs between two hues — how far apart they read on the wheel.</summary>
    private static double HueDistance(int a, int b)
    {
        var diff = Math.Abs(Hue(a) - Hue(b)) % 360;
        return Math.Min(diff, 360 - diff);
    }

    // Two named hue "families" (red vs orange, blue vs cyan) sit roughly 30-60 degrees apart; a bar of 45
    // requires two colours to read as genuinely different named hues rather than two shades of one, while
    // still passing pairs of already-shipped role colours that were never part of this bug (wool amber and
    // frontline orange sit about 16 degrees apart and are not tested against each other here).
    private const double MinHueDegrees = 45;

    [Test]
    public async Task Build_zone_and_water_lane_separate_by_hue_not_only_shade()
    {
        await Assert.That(HueDistance(PlanBoardPalette.BuildZoneRgb, PlanBoardPalette.WaterLaneRgb)).IsGreaterThanOrEqualTo(MinHueDegrees);
    }

    [Test]
    public async Task The_old_bug_colours_would_have_failed_the_same_hue_bar()
    {
        // The exact pair the entry names: #38bdf8 (old build zone) and #2563eb (water lane, unchanged) sit
        // about 23 degrees apart on the wheel — both readable as "blue" — which is under the bar the real
        // build zone colour now clears against the same water-lane colour.
        await Assert.That(HueDistance(0x38bdf8, PlanBoardPalette.WaterLaneRgb)).IsLessThan(MinHueDegrees);
    }

    [Test]
    public async Task Water_lane_keeps_a_blue_hue_and_build_zone_does_not()
    {
        // Blue is the one colour a reader brings a fixed meaning for; only the zone that is actually water
        // (eventually) may keep it.
        var (waterR, waterG, waterB) = Channels(PlanBoardPalette.WaterLaneRgb);
        await Assert.That(waterB).IsGreaterThan(waterR);
        await Assert.That(waterB).IsGreaterThan(waterG);

        var (buildR, buildG, buildB) = Channels(PlanBoardPalette.BuildZoneRgb);
        await Assert.That(buildB > buildR && buildB > buildG).IsFalse();
    }

    private static (int R, int G, int B) Channels(int rgb) => ((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    [Test]
    [Arguments("hub", 0xa78bfa)]
    [Arguments("spawn", 0x34d399)]
    [Arguments("wool", 0xfbbf24)]
    [Arguments("frontline", 0xfb923c)]
    public async Task Build_zone_separates_by_hue_from_every_existing_role_colour(string role, int roleRgb)
    {
        await Assert.That(HueDistance(PlanBoardPalette.BuildZoneRgb, roleRgb)).IsGreaterThanOrEqualTo(MinHueDegrees);
    }
}
