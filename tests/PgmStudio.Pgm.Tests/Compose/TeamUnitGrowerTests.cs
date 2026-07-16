using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// Stage two: growing one team's unit directly (below the full <see cref="Composer"/> pipeline). Structural
/// shape (a hub, a spawn, 1-3 wools, all anonymous pieces), determinism, and the collinear-chain measurement
/// (LN2) on synthetic rects; the fuller cross-invariant sweep (fanned separation, marker distances, area
/// budget, plan-validator agreement) lives in <see cref="ComposerTests"/>, which needs the assembled
/// <see cref="Plan.PlanModel"/> to run those checks.
/// </summary>
public sealed class TeamUnitGrowerTests
{
    private static GrownUnit Grow(int players, int teams = 2, string? symmetry = null, ulong seed = 1)
    {
        var request = new ComposeRequest(players, teams, symmetry, seed);
        var rng = new ComposeRng(seed);
        var env = Envelope.Derive(request, rng);
        return TeamUnitGrower.Grow(env, rng);
    }

    [Test]
    public async Task Grown_unit_has_a_hub_a_spawn_and_at_least_one_wool()
    {
        var unit = Grow(12, seed: 5);
        await Assert.That(unit.Pieces.Any(p => p.Id == "hub")).IsTrue();
        await Assert.That(unit.Spawn).IsNotNull();
        await Assert.That(unit.Wools.Count is 1 or 2).IsTrue();
    }

    // ── the collinear-chain measurement (LN2's unit of account) ─────────────────────────────────────────

    [Test]
    public async Task Abutting_collinear_rects_merge_into_one_chain()
    {
        // two 6-cell runs of the same cross interval, abutting end to end → one 12-cell (60-block) chain
        var rects = new[] { new[] { 0, 0, 6, 2 }, [6, 0, 6, 2] };
        await Assert.That(TeamUnitGrower.MaxChainBlocks(5, rects)).IsEqualTo(60);
    }

    [Test]
    public async Task A_jogged_pair_does_not_merge()
    {
        // same width and axis but offset cross intervals — a jog is a turn, not a longer lane
        var rects = new[] { new[] { 0, 0, 6, 2 }, [6, 1, 6, 2] };
        await Assert.That(TeamUnitGrower.MaxChainBlocks(5, rects)).IsEqualTo(30);
    }

    [Test]
    public async Task A_width_change_does_not_merge()
    {
        var rects = new[] { new[] { 0, 0, 6, 2 }, [6, 0, 6, 3] };
        await Assert.That(TeamUnitGrower.MaxChainBlocks(5, rects)).IsEqualTo(30);
    }

    [Test]
    public async Task Separated_collinear_rects_do_not_merge()
    {
        var rects = new[] { new[] { 0, 0, 6, 2 }, [8, 0, 6, 2] };
        await Assert.That(TeamUnitGrower.MaxChainBlocks(5, rects)).IsEqualTo(30);
    }

    [Test]
    public async Task A_single_long_rect_is_its_own_chain()
    {
        var rects = new[] { new[] { 0, 0, 11, 2 } };
        await Assert.That(TeamUnitGrower.MaxChainBlocks(5, rects)).IsEqualTo(55);
    }

    [Test]
    public async Task Chains_measure_both_axes()
    {
        // a 4×9 rect: its z-run (9 cells = 45 blocks) dominates its x-run
        var rects = new[] { new[] { 0, 0, 4, 9 } };
        await Assert.That(TeamUnitGrower.MaxChainBlocks(5, rects)).IsEqualTo(45);
    }

    [Test]
    public async Task Only_box_rooms_carry_a_role_at_grow_time()
    {
        // the wool and spawn boxes emit their rooms as real role-bearing terminals; everything else the
        // grower authors (hub, frontline, the third wool lane) is the anonymous piece role
        var unit = Grow(16, seed: 9);
        await Assert.That(unit.Pieces).IsNotEmpty();
        foreach (var p in unit.Pieces)
        {
            await Assert.That(p.Rect.Length).IsEqualTo(4);
            await Assert.That(p.Role is Plan.PlanRoles.Piece or Plan.PlanRoles.WoolRoom or Plan.PlanRoles.Spawn).IsTrue();
        }
        // the spawn is a box now: its room is a Spawn-role terminal carrying the marker
        var spawnRoom = unit.Pieces.Single(p => p.Id == unit.Spawn.Piece);
        await Assert.That(spawnRoom.Role).IsEqualTo(Plan.PlanRoles.Spawn);
        await Assert.That(spawnRoom.Slot).IsEqualTo(PgmStudio.Pgm.Shapes.ApproachSlots.Room);
        await Assert.That(spawnRoom.Box!.Kind).IsEqualTo(BoxKind.Spawn);
    }

    [Test]
    public async Task Growth_is_deterministic_for_the_same_seed()
    {
        var a = Grow(20, seed: 42);
        var b = Grow(20, seed: 42);
        await Assert.That(a.Pieces.Select(p => (p.Id, string.Join(',', p.Rect))))
            .IsEquivalentTo(b.Pieces.Select(p => (p.Id, string.Join(',', p.Rect))));
        await Assert.That(a.Spawn.Piece).IsEqualTo(b.Spawn.Piece);
        await Assert.That(a.Spawn.At).IsEquivalentTo(b.Spawn.At);
    }

    [Test]
    public async Task Rot_180_unit_pieces_sit_on_the_positive_z_side()
    {
        var unit = Grow(12, seed: 2);
        foreach (var p in unit.Pieces)
            await Assert.That(p.Rect[1] >= Envelope.AxisMarginCells).IsTrue();   // z (min) at/after the margin
    }

    [Test]
    public async Task Mirror_x_unit_pieces_sit_on_the_negative_x_side()
    {
        var unit = Grow(12, symmetry: "mirror_x", seed: 6);
        foreach (var p in unit.Pieces)
            await Assert.That(p.Rect[0] + p.Rect[2] <= -Envelope.AxisMarginCells).IsTrue();   // x (max) at/before -margin
    }

    [Test]
    public async Task Spawn_facing_matches_the_symmetry_frame()
    {
        await Assert.That(Grow(12, seed: 1).Spawn.Facing).IsEqualTo("front");
        await Assert.That(Grow(12, symmetry: "mirror_x", seed: 1).Spawn.Facing).IsEqualTo("right");
    }
}
