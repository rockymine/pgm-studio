using PgmStudio.Geom;
using PgmStudio.Pgm.Compose;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The band-only crossing arithmetic and the symmetry-frame flip classification. The full carved-band
/// behaviour on real plans (flush dock, hull-exact span, connectivity) is asserted by the
/// <see cref="ComposerTests"/> sweep; these tests pin the pure pieces the carver is built from.
/// </summary>
public sealed class MidCarverTests
{
    [Test]
    [Arguments(2, 8)]    // 15 blocks a side rounds to 8 cells — 16
    [Arguments(3, 5)]    // 5 cells — 15 blocks, exactly
    [Arguments(4, 4)]    // 3.75 cells rounds away from zero — 16 blocks
    [Arguments(5, 3)]    // 3 cells — 15 blocks, exactly
    [Arguments(12, 2)]   // 1.25 cells would be under the axis margin, which floors it
    public async Task A_stoneless_band_lays_its_stated_gap_on_this_grid(int cell, int expectedHalfGap)
    {
        await Assert.That(MidCarver.EmptyHalfGapCells(cell)).IsEqualTo(expectedHalfGap);
        await Assert.That(MidCarver.EmptyHalfGapCells(cell) >= Envelope.AxisMarginCells).IsTrue()
            .Because("the band never brings a front closer to the axis than the grower's own margin");
    }

    [Test]
    public async Task The_stated_gap_is_within_one_cell_of_what_the_grid_can_carry()
    {
        // the gap is a number of blocks, and a symmetric band can only spend it in whole cells either side:
        // the realised gap tracks the stated one to within one cell wherever the margin is not the binding floor
        foreach (var cell in new[] { 2, 3, 4, 5 })
        {
            var realised = 2 * MidCarver.EmptyHalfGapCells(cell) * cell;
            await Assert.That(Math.Abs(realised - MidCarver.BandGapBlocks) <= cell).IsTrue()
                .Because($"cell {cell} realised {realised} against a stated {MidCarver.BandGapBlocks}");
        }
    }

    [Test]
    public async Task A_band_carrying_a_stone_opens_one_hop_either_side_of_it()
    {
        foreach (var players in new[] { 8, 16, 24, 32, 52 })
        {
            var env = Envelope.Derive(new ComposeRequest(players), new ComposeRng(1));
            var crossing = MidCarver.Crossing(env, splitBand: false);
            var deep = MidCarver.StoneDeepCells(env);
            await Assert.That(deep % 2).IsEqualTo(0)
                .Because("a stone astride the axis spends half its depth each side");
            await Assert.That(crossing.HalfGapCells - deep / 2).IsEqualTo(MidCarver.HopCells(env.Cell))
                .Because("what is left of the half gap once the stone has its half is exactly one hop");
        }
    }

    [Test]
    public async Task A_split_crossing_takes_the_stoneless_gap_because_its_bay_is_the_island()
    {
        var env = Envelope.Derive(new ComposeRequest(24), new ComposeRng(1));
        await Assert.That(MidCarver.Crossing(env, splitBand: true).HalfGapCells)
            .IsEqualTo(MidCarver.EmptyHalfGapCells(env.Cell));
        await Assert.That(MidCarver.Crossing(env, splitBand: true).HalfGapCells)
            .IsNotEqualTo(MidCarver.Crossing(env, splitBand: false).HalfGapCells);
    }

    [Test]
    public async Task The_mid_and_the_unit_share_out_the_whole_band_budget()
    {
        foreach (var players in new[] { 8, 16, 24, 32, 52 })
        {
            var env = Envelope.Derive(new ComposeRequest(players), new ComposeRng(1));
            // the mid is shared, so a team's own half of it plus that team's unit is the band's whole budget
            var accounted = env.UnitBudgetCells + env.MidLandCells / 2;
            await Assert.That(Math.Abs(accounted - env.BudgetCells) < 1e-9).IsTrue()
                .Because($"{env.Band}: unit {env.UnitBudgetCells:F2} + half the mid {env.MidLandCells / 2:F2} "
                       + $"against a budget of {env.BudgetCells:F2}");
            await Assert.That(env.MidLandCells).IsGreaterThan(0);
        }
    }

    /// <summary>What a stone row must hold whatever board it lands on: a stone stands astride the axis and
    /// symmetric about it, so its own fanned image abuts it into one shared island rather than landing as a
    /// second one across the gap; it is wider than it is deep, so it reads as an island and not a line; it
    /// clears its neighbours by at least a hop; and the row sits inside the band that carries it.</summary>
    [Test]
    public async Task Every_stone_a_board_carries_stands_astride_the_axis_clear_of_its_neighbours()
    {
        var carried = 0;
        foreach (var players in new[] { 8, 16, 24, 32, 52 })
            foreach (var symmetry in new[] { "rot_180", "mirror_z", "mirror_x" })
                for (ulong seed = 0; seed < 8; seed++)
                {
                    var stages = Composer.ComposeStages(new ComposeRequest(players, 2, symmetry, seed));
                    var env = stages.Envelope;
                    var deep = MidCarver.StoneDeepCells(env);
                    var stones = stages.Mid.Stones;
                    await Assert.That(stones.Count).IsLessThanOrEqualTo(MidCarver.StoneMaxCount);

                    // for mirror_x the doubled axis is x, so the stone's depth runs along x and its width along z
                    var crossX = symmetry != "mirror_x";
                    var spans = stones
                        .Select(stone => (
                            Near: crossX ? stone.Rect.Z : stone.Rect.X,
                            Far: crossX ? stone.Rect.Z + stone.Rect.Height : stone.Rect.X + stone.Rect.Width,
                            Lo: crossX ? stone.Rect.X : stone.Rect.Z,
                            Hi: crossX ? stone.Rect.X + stone.Rect.Width : stone.Rect.Z + stone.Rect.Height))
                        .OrderBy(span => span.Lo).ToList();

                    foreach (var span in spans)
                    {
                        carried++;
                        await Assert.That(span.Near).IsEqualTo(-span.Far)
                            .Because($"p{players} {symmetry} s{seed}: a stone sits symmetric about the axis");
                        await Assert.That(span.Far - span.Near).IsEqualTo(deep);
                        await Assert.That(span.Hi - span.Lo).IsGreaterThanOrEqualTo(deep)
                            .Because($"p{players} {symmetry} s{seed}: wider than deep, or it is a line");
                    }
                    for (var index = 1; index < spans.Count; index++)
                        await Assert.That(spans[index].Lo - spans[index - 1].Hi)
                            .IsGreaterThanOrEqualTo(MidCarver.HopCells(env.Cell))
                            .Because($"p{players} {symmetry} s{seed}: neighbours clear each other by a hop");

                    var band = stages.Mid.BandRect;
                    foreach (var stone in stones)
                        await Assert.That(
                            stone.Rect.X >= band.X && stone.Rect.X + stone.Rect.Width <= band.X + band.Width &&
                            stone.Rect.Z >= band.Z && stone.Rect.Z + stone.Rect.Height <= band.Z + band.Height)
                            .IsTrue().Because($"p{players} {symmetry} s{seed}: {stone.Id} sits inside its band (MD4)");
                }
        await Assert.That(carried).IsGreaterThan(50)
            .Because("a sweep that placed almost no stones would assert nothing");
    }

    /// <summary>A stone stands one hop off the ground it is reached from: the band opens the hop either side of
    /// it, so the void between a stone and the frontline face the band docks is exactly that hop.</summary>
    [Test]
    public async Task A_stone_stands_one_hop_off_the_front_the_band_docks()
    {
        foreach (var players in new[] { 8, 16, 24, 32, 52 })
            for (ulong seed = 0; seed < 8; seed++)
            {
                var stages = Composer.ComposeStages(new ComposeRequest(players, 2, "rot_180", seed));
                if (stages.Mid.Stones.Count == 0) continue;
                var env = stages.Envelope;
                var front = stages.Unit.Pieces.Min(piece => piece.Rect.Z);
                var stoneFar = stages.Mid.Stones.Max(stone => stone.Rect.Z + stone.Rect.Height);
                await Assert.That(front - stoneFar).IsEqualTo(MidCarver.HopCells(env.Cell))
                    .Because($"p{players} s{seed}: one hop from the stone's far edge to the unit's near edge");
            }
    }

    [Test]
    public async Task Mirror_symmetries_do_not_flip_and_rotations_do()
    {
        await Assert.That(MidCarver.LateralFlip("rot_180")).IsTrue();
        await Assert.That(MidCarver.LateralFlip("rot_90")).IsTrue();
        await Assert.That(MidCarver.LateralFlip("mirror_x")).IsFalse();
        await Assert.That(MidCarver.LateralFlip("mirror_z")).IsFalse();
    }

    // ── the split band ────────────────────────────────────────────────────────────────────────────────────
    private static ComposeEnvelope Env(string symmetry) => new(
        symmetry, Teams: 2, PlayersPerTeam: 12, Band: SizeBands.Nano, Cell: 5,
        Surface: 9, CorridorCells: 2, WoolCorridorCells: 2, BoardWidthBlocks: 300, BoardLengthBlocks: 300,
        LandPerTeam: 2800, UnitMinX: -40, UnitMinZ: -40, UnitMaxX: 40, UnitMaxZ: 40);

    // A unit reduced to what the carve reads: a face at the near edge plus a body behind it to keep the front
    // row unambiguous. `legs` are (x, width) pairs on the face row. The face is seated at the half gap the
    // crossing under test opens, because a face anywhere else is one the band does not dock.
    private static CellRect? Band(string symmetry, bool split, params (int X, int W)[] legs)
    {
        var env = Env(symmetry);
        var design = MidCarver.Crossing(env, split);
        var halfGap = design.HalfGapCells;
        var pieces = legs
            .Select((leg, index) => new GrownPiece($"front-{index}", new(leg.X, halfGap, leg.W, 2)))
            .Append(new GrownPiece("hub", new(legs.Min(l => l.X), halfGap + 2, 4, 3)))
            .ToList();
        var unit = new GrownUnit(pieces, new GrownSpawn("hub", [0, 0], "front"), []);
        return MidCarver.TryCarve(env, design, unit)?.BandRect;
    }

    /// <summary>A face of two equal legs with the axis in the gap between them can be crossed by a band spanning
    /// one leg — its own orbit image supplies the other, so the pair reads as two parallel crossings with the bay
    /// left as an island. The band is narrower than the face hull, which is what "split" means here.</summary>
    [Test]
    public async Task A_leg_symmetric_face_can_be_crossed_by_two_parallel_bands()
    {
        // legs at x -5..-2 and 2..5 — equal width, mirror-paired, a 4-cell bay straddling the axis
        var legs = new (int X, int W)[] { (-5, 3), (2, 3) };

        var single = Band("rot_180", split: false, legs)!.Value;
        var pair = Band("rot_180", split: true, legs)!.Value;

        await Assert.That(single.Width).IsEqualTo(10).Because("one band spans the whole face hull, bay included");
        await Assert.That(pair.Width).IsEqualTo(3).Because("a split band spans one leg and lets the image supply the other");
        // the island: the band and its own reflection leave the bay uncovered between them
        await Assert.That(-pair.X - pair.Width).IsGreaterThan(pair.X + pair.Width)
            .Because("the reflected band lands beyond this one, not on top of it");
    }

    /// <summary>Both outcomes are legal for the same face — the split is asked for, not implied by the geometry.
    /// A face that can host two bands is equally valid crossed by one.</summary>
    [Test]
    public async Task The_split_is_a_request_the_face_can_grant_not_a_consequence_of_it()
    {
        var legs = new (int X, int W)[] { (-5, 3), (2, 3) };
        await Assert.That(Band("rot_180", split: false, legs)!.Value.Width)
            .IsNotEqualTo(Band("rot_180", split: true, legs)!.Value.Width);
    }

    /// <summary>Legs of unequal width do not coincide with their own reflection, so a band over one would leave
    /// the other's image partly uncrossed. Such a face keeps the single band even when the split is asked for —
    /// which is why the leg widths stay free: the mid reads the face, it does not shape it.</summary>
    [Test]
    public async Task An_unequal_legged_face_is_refused_the_split_and_keeps_one_band()
    {
        var legs = new (int X, int W)[] { (-6, 4), (2, 3) };   // 4 wide against 3 — not its own mirror
        await Assert.That(Band("rot_180", split: true, legs)!.Value.Width)
            .IsEqualTo(Band("rot_180", split: false, legs)!.Value.Width);
    }

    /// <summary>Under a mirror the image lands straight across rather than reflected, so a band over one leg
    /// would fan back onto itself and leave the far leg with no crossing at all. The split is refused there
    /// however symmetric the face is.</summary>
    [Test]
    public async Task A_mirror_symmetry_never_splits_however_symmetric_the_face()
    {
        var legs = new (int X, int W)[] { (-5, 3), (2, 3) };
        await Assert.That(Band("mirror_z", split: true, legs)!.Value.Width)
            .IsEqualTo(Band("mirror_z", split: false, legs)!.Value.Width);
    }
}
