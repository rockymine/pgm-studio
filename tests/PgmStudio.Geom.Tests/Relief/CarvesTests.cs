using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// What a carve guarantees of the ground it cuts: a Tunnel's floor runs level between mouths at the ground and
/// keeps its cover overhead, a Chamber's dome stays its cover under the surface, a Shaft reaches the sky with a
/// ladder up it, and a carve fanned by the symmetry cuts the same space at every image.
/// </summary>
public sealed class CarvesTests
{
    /// <summary>Ground at 10 with a hill to 30 over x 12–28.</summary>
    private static int? Hill(int x, int z) => x is >= 0 and < 41 && z is >= 0 and < 41 ? (x is >= 12 and <= 28 ? 30 : 10) : null;

    private static CarveSpec Tunnel(params MaskArea[] areas) => new() { Op = CarveOp.Tunnel, Areas = areas, Wander = 0 };

    private static MaskArea Through => MaskArea.Line([(0.5, 20.5), (40.5, 20.5)], 5);

    [Test]
    public async Task A_tunnel_runs_through_a_hill_on_a_level_floor_from_mouths_at_the_ground()
    {
        var air = Carves.Cut(Tunnel(Through), Hill).Air.ToHashSet();

        await Assert.That(air.Contains((20, 10, 20))).IsTrue().Because("the floor stands at the ground of its mouths");
        await Assert.That(air.Contains((20, 11, 20))).IsTrue();
        await Assert.That(air.Contains((20, 9, 20))).IsFalse().Because("below the floor stays solid");
        await Assert.That(air.Where(cell => cell.X == 20).Max(cell => cell.Y)).IsLessThan(27).Because("three of cover stay over it");
        await Assert.That(air.Any(cell => cell.Y >= 30)).IsFalse();
    }

    [Test]
    public async Task Where_its_roof_would_be_thinner_than_its_cover_a_tunnel_is_cut_open_to_the_sky()
    {
        var shallow = Carves.Cut(Tunnel(Through) with { Mouth = 0, PointFloors = [[10.0, 10.0]] }, (x, z) => 13).Air.ToHashSet();

        await Assert.That(shallow.Contains((20, 10, 20)) && shallow.Contains((20, 12, 20))).IsTrue()
            .Because("ground three over the floor is too thin to roof it, so the run is a trench to the surface");
        await Assert.That(shallow.Any(cell => cell.Y >= 13)).IsFalse();
    }

    [Test]
    public async Task A_tunnel_into_a_gently_rising_hill_is_never_shut_between_its_mouth_and_its_roof()
    {
        int? Ramp(int x, int z) => x is >= 0 and < 41 ? 10 + Math.Min(Math.Min(x, 40 - x), 20) : null;
        var air = Carves.Cut(Tunnel(Through), Ramp).Air.ToHashSet();
        bool Open(int x, int y) => y >= Ramp(x, 20) || air.Contains((x, y, 20));

        for (var x = 1; x < 40; x++)
            await Assert.That((x, Open(x, 10) && Open(x, 11))).IsEqualTo((x, true))
                .Because("two blocks of headroom run the whole way, a cutting where the hill is low and a tube where it is high");
        await Assert.That(air.Where(cell => cell.X == 20).Max(cell => cell.Y)).IsLessThan(27).Because("the high middle keeps its cover");
    }

    [Test]
    public async Task A_tunnel_point_may_state_its_floor_and_the_rest_runs_straight_between()
    {
        var spec = Tunnel(Through) with { PointFloors = [[4.0, 16.0]], Mouth = 0 };
        var air = Carves.Cut(spec, (x, z) => 60).Air.ToHashSet();

        await Assert.That(air.Where(cell => cell.X == 0).Min(cell => cell.Y)).IsEqualTo(4);
        await Assert.That(air.Where(cell => cell.X == 20).Min(cell => cell.Y)).IsEqualTo(10).Because("halfway from 4 to 16");
        await Assert.That(air.Where(cell => cell.X == 40).Min(cell => cell.Y)).IsEqualTo(16);
    }

    [Test]
    public async Task A_chamber_is_a_dome_its_height_over_its_floor_and_its_cover_under_the_ground()
    {
        var chamber = new CarveSpec { Op = CarveOp.Chamber, Areas = [MaskArea.Ellipse(20.5, 20.5, 6, 6)], Jag = 0 };
        var air = Carves.Cut(chamber, (x, z) => 30).Air.ToHashSet();

        await Assert.That(air.Where(cell => (cell.X, cell.Z) == (20, 20)).Select(cell => cell.Y).Order())
            .IsEquivalentTo(new[] { 21, 22, 23, 24, 25, 26 }).Because("a floor at 30 less six and three of cover");
        await Assert.That(air.Where(cell => (cell.X, cell.Z) == (25, 20)).Count()).IsLessThan(6).Because("the dome falls toward its rim");
    }

    [Test]
    public async Task A_shaft_reaches_the_sky_and_carries_a_ladder_up_its_south_wall()
    {
        var shaft = new CarveSpec { Op = CarveOp.Shaft, Areas = [MaskArea.Ellipse(20.5, 20.5, 2, 2)], Floor = 12 };
        var cut = Carves.Cut(shaft, (x, z) => 30);

        await Assert.That(cut.Air.Where(cell => (cell.X, cell.Z) == (20, 20)).Max(cell => cell.Y)).IsEqualTo(29);
        await Assert.That(cut.Ladders.Count).IsEqualTo(18);
        await Assert.That(cut.Ladders.All(ladder => ladder.Z == cut.Ladders[0].Z && ladder.Data == 2)).IsTrue();
        await Assert.That(cut.Ladders[0].Z).IsEqualTo(cut.Air.Where(cell => cell.X == cut.Ladders[0].X).Max(cell => cell.Z));
    }

    [Test]
    public async Task A_tunnel_fanned_by_a_half_turn_cuts_the_same_space_at_its_image()
    {
        var bent = MaskArea.Line([(2.5, 8.5), (14.5, 12.5), (26.5, 9.5)], 5);
        var spec = new CarveSpec { Op = CarveOp.Tunnel, Areas = MaskArea.Fanned([bent], ["rot_180"], 20, 20), Mouth = 0,
                                   PointFloors = [[10.0, 10.0, 10.0], [10.0, 10.0, 10.0]] };
        var air = Carves.Cut(spec, (x, z) => 40).Air.ToHashSet();

        await Assert.That(air.Count).IsGreaterThan(100);
        await Assert.That(air.All(cell => air.Contains((39 - cell.X, cell.Y, 39 - cell.Z)))).IsTrue();
    }
}
