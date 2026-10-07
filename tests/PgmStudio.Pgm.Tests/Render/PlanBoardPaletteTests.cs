using PgmStudio.Pgm.Render;
using PgmStudio.Vocabulary;
using static PgmStudio.Pgm.Render.PlanBoardPalette;

namespace PgmStudio.Pgm.Tests.Render;

/// <summary>The four inks every plan render draws from: the three accents (spawn, wool room, build zone) must
/// separate by <b>hue</b>, not merely by the shade a still image can lose, and everything that is only terrain
/// must be a neutral grey that no accent can be mistaken for. The roles that choose an ink are held here too.</summary>
public sealed class PlanBoardPaletteTests
{
    private static double Hue(int rgb)
    {
        double r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        if (delta < 1e-9) return 0;

        double hue = max == r ? 60 * (((g - b) / delta) % 6)
            : max == g ? 60 * ((b - r) / delta + 2)
            : 60 * ((r - g) / delta + 4);
        return hue < 0 ? hue + 360 : hue;
    }

    private static double HueDistance(int a, int b)
    {
        var diff = Math.Abs(Hue(a) - Hue(b)) % 360;
        return Math.Min(diff, 360 - diff);
    }

    /// <summary>How far a colour's channels spread: grey is near zero, any accent is large.</summary>
    private static int Chroma(int rgb)
    {
        int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
        return Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
    }

    // Spawn violet and wool-room green are the plan editor's role hues; the closest pair is spawn and zone.
    private const double MinHueDegrees = 35;

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task The_three_accent_inks_separate_by_hue(int pair)
    {
        int[][] pairs =
        [
            [Spawn.Edge, WoolRoom.Edge], [Spawn.Edge, Zone.Edge], [WoolRoom.Edge, Zone.Edge],
        ];
        await Assert.That(HueDistance(pairs[pair][0], pairs[pair][1])).IsGreaterThanOrEqualTo(MinHueDegrees);
    }

    [Test]
    public async Task Ground_is_a_neutral_grey_and_every_accent_is_not()
    {
        await Assert.That(Chroma(Ground.Fill)).IsLessThan(20);
        await Assert.That(Chroma(Ground.Edge)).IsLessThan(30);
        foreach (var accent in new[] { Spawn, WoolRoom, Zone })
            await Assert.That(Chroma(accent.Edge)).IsGreaterThan(80);
    }

    [Test]
    public async Task Spawn_and_wool_edges_are_the_plan_editors_role_colours_and_their_fills_the_same_hue()
    {
        await Assert.That(Spawn.Edge).IsEqualTo(0x8f7bd6);
        await Assert.That(WoolRoom.Edge).IsEqualTo(0x3fae74);
        await Assert.That(HueDistance(Spawn.Fill, Spawn.Edge)).IsLessThan(8);
        await Assert.That(HueDistance(WoolRoom.Fill, WoolRoom.Edge)).IsLessThan(8);
    }

    [Test]
    public async Task Only_rooms_leave_the_ground_ink()
    {
        await Assert.That(InkOf(BoardRoles.Hub)).IsEqualTo(Ground);
        await Assert.That(InkOf(BoardRoles.Frontline)).IsEqualTo(Ground);
        await Assert.That(InkOf(BoardRoles.Approach)).IsEqualTo(Ground);
        await Assert.That(InkOf(BoardRoles.Other)).IsEqualTo(Ground);
        await Assert.That(InkOf(BoardRoles.Spawn)).IsEqualTo(Spawn);
        await Assert.That(InkOf(BoardRoles.Wool)).IsEqualTo(WoolRoom);
    }

    [Test]
    [Arguments(PlanRoles.Spawn, "spawn-room", BoardRoles.Spawn)]
    [Arguments(PlanRoles.WoolRoom, "wool-a-room", BoardRoles.Wool)]
    [Arguments(PlanRoles.Piece, "hub", BoardRoles.Hub)]
    [Arguments(PlanRoles.Piece, "frontline", BoardRoles.Frontline)]
    [Arguments(PlanRoles.Piece, "wool-a", BoardRoles.Approach)]
    [Arguments(PlanRoles.Piece, "piece-7", BoardRoles.Other)]
    public async Task A_piece_takes_its_board_role_from_its_authored_role_then_its_id(string planRole, string id, string expected)
    {
        await Assert.That(RoleOf(planRole, id)).IsEqualTo(expected);
    }
}
