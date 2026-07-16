using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The partition-first allocator (G63-B): <see cref="BoxPartitioner"/> is the <c>budget → <see cref="BoxPartition"/></c>
/// seam. Shipping parallel to <see cref="TeamUnitGrower"/> (not the default), it allocates the partition a compose
/// produces and carries the two-currency budget accounting. The two contract checks: <b>round-trip</b> — the
/// emitted partition equals <see cref="BoxPartition.Of"/> of the unit that fills it — and <b>budget</b> — the
/// partition balances inside the team land budget (<see cref="BoxPartitioner.WithinBudget"/>).
/// </summary>
public sealed class BoxPartitionerTests
{
    // env derivation consumes rng draws before the grow does, so a round-trip must replay both from a fresh rng
    private static (ComposeEnvelope Env, ComposeRng Rng) Fresh(int players, int teams, string? symmetry, ulong seed)
    {
        var request = new ComposeRequest(players, teams, symmetry, seed);
        var rng = new ComposeRng(seed);
        return (Envelope.Derive(request, rng), rng);
    }

    /// <summary>Structural partition equality: <see cref="Box"/> carries an <c>int[] Rect</c> and
    /// <see cref="BoxPartition"/> holds lists, so record equality is reference-based — compare field by field.</summary>
    private static bool Same(BoxPartition a, BoxPartition b)
    {
        if (a.Boxes.Count != b.Boxes.Count || a.Joints.Count != b.Joints.Count) return false;
        for (var i = 0; i < a.Boxes.Count; i++)
        {
            var (x, y) = (a.Boxes[i], b.Boxes[i]);
            if (x.Id != y.Id || x.Kind != y.Kind || x.LandTargetCells != y.LandTargetCells
                || !x.Rect.SequenceEqual(y.Rect)) return false;
        }
        return a.Joints.SequenceEqual(b.Joints);   // BoxJoint has no array member — record equality is structural
    }

    [Test]
    public async Task Budget_cells_is_the_team_land_over_the_cell_area()
    {
        var (env, _) = Fresh(24, 2, null, 1);
        await Assert.That(BoxPartitioner.BudgetCells(env))
            .IsEqualTo(env.LandPerTeam / (env.Cell * (double)env.Cell)).Within(1e-9);
    }

    [Test]
    public async Task The_allocated_partition_round_trips_through_the_mirror()
    {
        // the emitted partition equals BoxPartition.Of of the unit that fills it — the grower being the parallel
        // filler, replayed byte-for-byte from a fresh rng of the same seed
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var (envA, rngA) = Fresh(24, 2, null, seed);
            var unit = TeamUnitGrower.Grow(envA, rngA);

            var (envB, rngB) = Fresh(24, 2, null, seed);
            var partition = BoxPartitioner.Partition(envB, rngB);

            await Assert.That(Same(partition, BoxPartition.Of(unit))).IsTrue();
        }
    }

    [Test]
    public async Task The_allocated_partition_is_valid_and_balances_the_budget()
    {
        // verified against the budget: Valid (each box's land within its footprint) and the total land inside the
        // team budget envelope — across seeds, player counts, and every symmetry mode
        foreach (var (players, teams, symmetry) in new[]
                 {
                     (16, 2, (string?)null), (24, 2, null), (32, 2, "mirror_x"),
                     (24, 2, "mirror_z"), (24, 4, "rot_90"), (20, 2, "rot_180"),
                 })
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var (env, rng) = Fresh(players, teams, symmetry, seed);
                var partition = BoxPartitioner.Partition(env, rng);

                await Assert.That(partition.Valid()).IsTrue();
                await Assert.That(BoxPartitioner.WithinBudget(partition, env)).IsTrue();
                // the two currencies: every box's land never exceeds its footprint (the footprint bounds the land)
                foreach (var box in partition.Boxes)
                    await Assert.That(box.LandTargetCells).IsLessThanOrEqualTo(box.Rect[2] * box.Rect[3]);
            }
    }

    [Test]
    public async Task The_partition_carries_the_typed_spine_boxes()
    {
        for (ulong seed = 1; seed <= 8; seed++)
        {
            var (env, rng) = Fresh(24, 2, null, seed);
            var partition = BoxPartitioner.Partition(env, rng);
            await Assert.That(partition.Boxes.Any(b => b.Kind == BoxKind.Hub)).IsTrue();
            await Assert.That(partition.Boxes.Any(b => b.Kind == BoxKind.Spawn)).IsTrue();
            await Assert.That(partition.Boxes.Any(b => b.Kind == BoxKind.Wool)).IsTrue();
        }
    }

    [Test]
    public async Task Within_budget_rejects_an_over_budget_partition()
    {
        var (env, _) = Fresh(24, 2, null, 1);
        // a lone tiny box holds far less land than the team budget — the land currency is starved, so the balance
        // fails even though the box itself is Valid
        var starved = new BoxPartition([new Box("hub", BoxKind.Hub, [0, 0, 2, 2], 4)], []);
        await Assert.That(starved.Valid()).IsTrue();
        await Assert.That(BoxPartitioner.WithinBudget(starved, env)).IsFalse();
    }
}
