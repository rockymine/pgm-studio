using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>G63-C.2 — the box-model placement plan: the spawn may sit on the back or a lateral side, and the
/// wools are assigned around it (the free sides first, back preferred, a third doubling on the spawn's side).</summary>
public class TeamUnitAllocatorTests
{
    private static ComposeEnvelope Env(int players = 8, double land = 1600) =>
        new("mirror_z", Teams: 2, players, Cell: 5, Surface: 9, Headroom: 11,
            BoardWidthBlocks: 200, BoardLengthBlocks: 200, land, UnitMinX: 0, UnitMinZ: 0, UnitMaxX: 40, UnitMaxZ: 40);

    [Test]
    public async Task Spawn_on_the_back_puts_wools_on_the_sides_then_a_back_wool_c()
    {
        // reduces to the grower's model: two side wools, a third back beside the spawn
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Back, 1)).IsEquivalentTo(new[] { UnitSide.Left });
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Back, 2)).IsEquivalentTo(new[] { UnitSide.Left, UnitSide.Right });
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Back, 3)).IsEquivalentTo(new[] { UnitSide.Left, UnitSide.Right, UnitSide.Back });
    }

    [Test]
    public async Task Spawn_on_a_side_prefers_the_back_then_the_other_side()
    {
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Left, 1)).IsEquivalentTo(new[] { UnitSide.Back });
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Left, 2)).IsEquivalentTo(new[] { UnitSide.Back, UnitSide.Right });
        // the third doubles up on the spawn's own side
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Left, 3)).IsEquivalentTo(new[] { UnitSide.Back, UnitSide.Right, UnitSide.Left });
        // symmetric for the other lateral side
        await Assert.That(TeamUnitAllocator.AssignWools(UnitSide.Right, 2)).IsEquivalentTo(new[] { UnitSide.Back, UnitSide.Left });
    }

    [Test]
    public async Task Wools_never_take_the_front_and_never_collide_with_the_spawn_when_free_sides_exist()
    {
        foreach (var spawn in new[] { UnitSide.Back, UnitSide.Left, UnitSide.Right })
        {
            var wools = TeamUnitAllocator.AssignWools(spawn, 2);
            await Assert.That(wools.Contains(UnitSide.Front)).IsFalse();
            await Assert.That(wools.Contains(spawn)).IsFalse();     // two wools fit the two free sides — no doubling yet
        }
    }

    [Test]
    public async Task Sample_plan_reserves_the_front_for_the_frontline_and_seats_the_spawn_off_front()
    {
        var env = Env();
        var plan = TeamUnitAllocator.SamplePlan(env, new ComposeRng(7), hasFrontline: true);

        await Assert.That(plan.Frontline).IsEqualTo(UnitSide.Front);
        await Assert.That(plan.Spawn).IsNotEqualTo(UnitSide.Front);
        await Assert.That(plan.Wools.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(plan.Wools.All(s => s != UnitSide.Front)).IsTrue();
    }

    [Test]
    public async Task No_frontline_leaves_the_front_unassigned()
    {
        var env = Env();
        var plan = TeamUnitAllocator.SamplePlan(env, new ComposeRng(7), hasFrontline: false);
        await Assert.That(plan.Frontline).IsNull();
    }
}
