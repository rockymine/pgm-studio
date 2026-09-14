using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// Where a board's capture points go (<see cref="ControlPointLayout"/>) — the author's rule in
/// <c>docs/gameplay/approaches.md</c>, read back as geometry.
///
/// <para>A point belongs to nobody, so every anchor is the same walk for every team: the centre of symmetry,
/// or a ring the orbit fans from one side point sitting on the bisector between two neighbouring spawns. What
/// is asserted here is that property rather than the coordinates — a side point equidistant from the spawns
/// it lies between, at the stated share of the way out.</para>
/// </summary>
public sealed class ControlPointLayoutTests
{
    private const double SpawnX = 0, SpawnZ = -90;   // a two-team board's red spawn, 90 blocks up the axis

    private static double Reach(double x, double z) => Math.Sqrt(x * x + z * z);

    /// <summary>The counts the author stated and the corpus builds, and no others: a centre, a ring, or a
    /// ring and a centre.</summary>
    [Test]
    [Arguments(1, 2, true)]
    [Arguments(2, 2, true)]
    [Arguments(3, 2, true)]
    [Arguments(4, 2, false)]
    [Arguments(1, 4, true)]
    [Arguments(4, 4, true)]
    [Arguments(5, 4, true)]
    [Arguments(2, 4, false)]
    [Arguments(3, 4, false)]
    [Arguments(0, 2, false)]
    public async Task An_orbit_lays_out_a_centre_a_ring_or_both(int count, int order, bool fans)
    {
        await Assert.That(ControlPointLayout.Fans(count, order)).IsEqualTo(fans);
    }

    /// <summary>One point is the centre of symmetry and nothing else — the one position on a two-team board
    /// that is the same walk for both.</summary>
    [Test]
    public async Task One_point_is_the_centre()
    {
        await Assert.That(ControlPointLayout.Primaries(1, 2, SpawnX, SpawnZ))
            .IsEquivalentTo(new[] { (0d, 0d) });
    }

    /// <summary>Three points on two teams are a centre and one side primary; the orbit makes the pair.</summary>
    [Test]
    public async Task Three_points_are_a_centre_and_one_side()
    {
        var primaries = ControlPointLayout.Primaries(3, 2, SpawnX, SpawnZ);

        await Assert.That(primaries.Count).IsEqualTo(2);
        await Assert.That(primaries[0]).IsEqualTo((0d, 0d));
    }

    /// <summary>Two points are the pair alone — a board with no middle.</summary>
    [Test]
    public async Task Two_points_are_a_ring_with_no_centre()
    {
        var primaries = ControlPointLayout.Primaries(2, 2, SpawnX, SpawnZ);

        await Assert.That(primaries.Count).IsEqualTo(1);
        await Assert.That(primaries[0]).IsNotEqualTo((0d, 0d));
    }

    /// <summary>The side point lies **across** the line between the spawns, not along it: on a two-team
    /// board that is the perpendicular bisector, so it is exactly as far from one spawn as from the other.
    /// That equidistance is the rule — "the same walk for everyone" — and the coordinates are its
    /// consequence.</summary>
    [Test]
    public async Task A_side_point_is_the_same_walk_from_both_spawns()
    {
        var (x, z) = ControlPointLayout.Primaries(3, 2, SpawnX, SpawnZ)[1];

        var toRed = Reach(x - SpawnX, z - SpawnZ);
        var toBlue = Reach(x + SpawnX, z + SpawnZ);   // the 180° image of the spawn
        await Assert.That(Math.Abs(toRed - toBlue)).IsLessThan(1.0);
    }

    /// <summary>And it sits at the author's share of the way out — two-thirds on two teams, measured from the
    /// centre rather than in blocks, so a board twice the size puts its points twice as far out.</summary>
    [Test]
    [Arguments(2, ControlPointLayout.SideShare)]
    [Arguments(4, ControlPointLayout.WideSideShare)]
    public async Task A_side_point_sits_at_the_stated_share_of_the_way_to_a_spawn(int order, double share)
    {
        var (x, z) = ControlPointLayout.Primaries(order + 1, order, SpawnX, SpawnZ)[1];

        await Assert.That(Reach(x, z) / Reach(SpawnX, SpawnZ)).IsBetween(share - 0.02, share + 0.02);
    }

    /// <summary>A four-team ring sits at 45° to the spawns — on the diagonal *between* two of them, so each
    /// ring point is the same walk for the two teams it lies between rather than the doorstep of one.</summary>
    [Test]
    public async Task A_four_team_ring_sits_between_two_spawns_rather_than_in_front_of_one()
    {
        var (x, z) = ControlPointLayout.Primaries(5, 4, SpawnX, SpawnZ)[1];

        var off = Math.Atan2(z, x) - Math.Atan2(SpawnZ, SpawnX);
        await Assert.That(Math.Abs(off * 180 / Math.PI)).IsBetween(44.0, 46.0);
    }

    /// <summary>A board whose spawn sits on the centre has no frame to measure against, so the centre is the
    /// whole answer rather than a side point at nought blocks out.</summary>
    [Test]
    public async Task A_spawn_on_the_centre_gives_no_side_point()
    {
        await Assert.That(ControlPointLayout.Primaries(3, 2, 0, 0)).IsEquivalentTo(new[] { (0d, 0d) });
    }

    /// <summary>A count the orbit cannot build is answered with nothing rather than with a number it can —
    /// the plan gate names it (<c>PL16</c>) and the compiler places none.</summary>
    [Test]
    public async Task A_count_the_orbit_cannot_build_lays_out_nothing()
    {
        await Assert.That(ControlPointLayout.Primaries(4, 2, SpawnX, SpawnZ)).IsEmpty();
    }
}
