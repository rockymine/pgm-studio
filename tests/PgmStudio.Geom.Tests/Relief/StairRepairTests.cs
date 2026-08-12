using PgmStudio.Geom.Algorithms;
using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// Cutting a way up out of ground the block step stranded. What these hold is that the repair is the smallest
/// intervention that reconnects — it must not quietly re-flatten the terracing an author asked for — and that
/// it refuses rather than leaving a scar when it cannot finish.
/// </summary>
public sealed class StairRepairTests
{
    private static HeightField Field(int width, int depth, Func<int, int, int> height)
    {
        var footprint = new Footprint(0, 0, width, depth);
        footprint.Add(Enumerable.Range(0, width).SelectMany(x => Enumerable.Range(0, depth).Select(z => (x, z))));
        var blocks = new int[footprint.Cells];
        foreach (var (x, z) in footprint.Land()) blocks[footprint.Index(x, z)] = height(x, z);
        return new HeightField(footprint, [.. blocks.Select(b => (double)b)], blocks);
    }

    /// <summary>How many places the ground holds at the walking step — the count a stair exists to reduce.</summary>
    private static int Places(HeightField field)
        => GridComponents.Label(field.Footprint.Land().ToList(), 4,
            (from, to) => Math.Abs(field.At(from.X, from.Z) - field.At(to.X, to.Z)) <= 1).Count;

    private static double WalkShare(HeightField field)
    {
        int within = 0, all = 0;
        foreach (var (x, z) in field.Footprint.Land())
            foreach (var (dx, dz) in GridComponents.N4)
            {
                if (dx + dz < 0 || !field.Footprint.Inside(x + dx, z + dz)) continue;
                all++;
                if (Math.Abs(field.At(x, z) - field.At(x + dx, z + dz)) <= 1) within++;
            }
        return all == 0 ? 1 : within / (double)all;
    }

    [Test]
    public async Task Terracing_strands_the_ground_and_a_stair_gives_it_back()
    {
        // The case the repair exists for: a hillside snapped to a two-block step is a flight of separate
        // terraces, and nothing about the surface says so.
        var terraced = Field(60, 40, (x, _) => 6 + x / 10 * 2);
        await Assert.That(Places(terraced)).IsEqualTo(6);

        var repaired = StairRepair.Repair(terraced);
        await Assert.That(Places(repaired)).IsEqualTo(1);
    }

    [Test]
    public async Task The_terracing_survives_the_repair_almost_untouched()
    {
        // A repair that reconnected by flattening would be the wrong fix — the terracing is what the author
        // asked for. The stair is one cut per stranded place and nothing else moves.
        var terraced = Field(60, 40, (x, _) => 6 + x / 10 * 2);
        var repaired = StairRepair.Repair(terraced);

        var moved = terraced.Footprint.Land()
            .Count(cell => terraced.At(cell.X, cell.Z) != repaired.At(cell.X, cell.Z));

        await Assert.That(moved).IsLessThanOrEqualTo(8);                    // five risers, one tread each
        await Assert.That(WalkShare(repaired) - WalkShare(terraced)).IsLessThan(0.02);
    }

    [Test]
    public async Task Ground_already_in_one_piece_is_left_exactly_alone()
    {
        var gentle = Field(40, 40, (x, _) => 6 + x / 10);
        var repaired = StairRepair.Repair(gentle);

        foreach (var (x, z) in gentle.Footprint.Land())
            await Assert.That(repaired.At(x, z)).IsEqualTo(gentle.At(x, z));
    }

    [Test]
    public async Task A_stair_is_cut_through_the_cheapest_riser()
    {
        // Two ways up out of a pit, one a three-block climb and one a two. The cut belongs at the cheaper
        // one, because a stair is a hole in the terracing and the one to make is the one that removes least.
        var field = Field(40, 20, (x, z) =>
        {
            if (x is >= 10 and < 20 && z is >= 5 and < 15) return 4;        // the pit floor
            return z < 10 ? 7 : 6;                                          // a 3-block wall north, 2 south
        });
        await Assert.That(Places(field)).IsGreaterThan(1);

        var repaired = StairRepair.Repair(field);
        await Assert.That(Places(repaired)).IsEqualTo(1);

        // The cheap side is where the ground moved.
        var movedSouth = field.Footprint.Land()
            .Count(cell => cell.Z >= 10 && field.At(cell.X, cell.Z) != repaired.At(cell.X, cell.Z));
        var movedNorth = field.Footprint.Land()
            .Count(cell => cell.Z < 10 && field.At(cell.X, cell.Z) != repaired.At(cell.X, cell.Z));
        await Assert.That(movedSouth).IsGreaterThan(0);
        await Assert.That(movedNorth).IsEqualTo(0);
    }

    [Test]
    public async Task A_shelf_with_no_room_for_a_stair_is_left_rather_than_scarred()
    {
        // A stair that runs out of footprint part-way down leaves a riser AND a cut, which is the same map
        // with a scar in it. Refusing is the honest answer: the ledge stays a ledge.
        var field = Field(30, 30, (x, z) => x >= 29 && z >= 29 ? 40 : 6);
        var repaired = StairRepair.Repair(field);

        foreach (var (x, z) in field.Footprint.Land())
            await Assert.That(repaired.At(x, z)).IsEqualTo(field.At(x, z));
    }

    [Test]
    public async Task A_stair_cut_on_a_mirrored_map_is_folded_again()
    {
        // The repair decides things by WALKING the map — which place is largest, which riser is cheapest,
        // which way a stair runs — and a walk has a direction a half-turn does not preserve. Left unfolded it
        // hands one team a stair the other does not have: the exact unfairness the fold on the solved surface
        // prevents, arriving one pass later.
        const double CentreX = 30, CentreZ = 20;
        var footprint = Footprint.Of([([[0, 0], [60, 0], [60, 40], [0, 40]], false)]);
        var spec = new ReliefSpec
        {
            Base = 6, Step = 2, Stairs = true,
            Marks =
            [
                new RimMark(6),
                new PointMark(10, 10, 20, 4), new PointMark(2 * CentreX - 10, 2 * CentreZ - 10, 20, 4),
            ],
            FoldMode = "rot_180", FoldCentreX = CentreX, FoldCentreZ = CentreZ,
        };

        var field = ReliefSolver.Solve(footprint, spec);

        var worst = 0;
        foreach (var (x, z) in footprint.Land())
        {
            int mx = (int)Math.Floor(2 * CentreX - (x + 0.5)), mz = (int)Math.Floor(2 * CentreZ - (z + 0.5));
            if (!field.Has(mx, mz)) continue;
            worst = Math.Max(worst, Math.Abs(field.At(x, z) - field.At(mx, mz)));
        }
        await Assert.That(worst).IsEqualTo(0);
    }

    [Test]
    public async Task The_cut_budget_is_a_backstop_rather_than_a_quota()
    {
        // Given no budget at all nothing is cut, and the field comes back untouched rather than half-done.
        var terraced = Field(60, 40, (x, _) => 6 + x / 10 * 2);
        var untouched = StairRepair.Repair(terraced, maxStairs: 0);

        foreach (var (x, z) in terraced.Footprint.Land())
            await Assert.That(untouched.At(x, z)).IsEqualTo(terraced.At(x, z));
    }
}
