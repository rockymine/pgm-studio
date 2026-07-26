using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>
/// The band-only crossing arithmetic and the symmetry-frame flip classification. The full carved-band
/// behaviour on real plans (flush dock, hull-exact span, connectivity) is asserted by the
/// <see cref="ComposerTests"/> sweep; these tests pin the pure pieces the carver is built from.
/// </summary>
public sealed class MidCarverTests
{
    [Test]
    public async Task The_band_only_crossing_is_a_uniform_20_block_gap()
    {
        var request = new ComposeRequest(12, seed: 1);
        var env = Envelope.Derive(request, new ComposeRng(1));
        var crossing = MidCarver.BandOnly(env);
        await Assert.That(crossing.HalfGapCells * env.Cell).IsEqualTo(10);   // 10 blocks per side
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
    // A unit reduced to what the carve reads: a face at the near edge (u = the half gap) plus a body behind it
    // to keep the front row unambiguous. `legs` are (x, width) pairs on the face row.
    private static GrownUnit Unit(int halfGap, params (int X, int W)[] legs)
    {
        var pieces = legs
            .Select((l, i) => new GrownPiece($"front-{i}", [l.X, halfGap, l.W, 2]))
            .Append(new GrownPiece("hub", [legs.Min(l => l.X), halfGap + 2, 4, 3]))
            .ToList();
        return new GrownUnit(pieces, new GrownSpawn("hub", [0, 0], "front"), []);
    }

    private static int[]? Band(string symmetry, bool split, GrownUnit unit)
    {
        var env = new ComposeEnvelope(symmetry, Teams: 2, PlayersPerTeam: 12, Cell: 5, Surface: 9, Headroom: 11,
            BoardWidthBlocks: 300, BoardLengthBlocks: 300, LandPerTeam: 2800,
            UnitMinX: -40, UnitMinZ: -40, UnitMaxX: 40, UnitMaxZ: 40);
        var design = MidCarver.BandOnly(env) with { SplitBand = split };
        return MidCarver.TryCarve(env, design, unit)?.BandRect;
    }

    /// <summary>A face of two equal legs with the axis in the gap between them can be crossed by a band spanning
    /// one leg — its own orbit image supplies the other, so the pair reads as two parallel crossings with the bay
    /// left as an island. The band is narrower than the face hull, which is what "split" means here.</summary>
    [Test]
    public async Task A_leg_symmetric_face_can_be_crossed_by_two_parallel_bands()
    {
        var h = MidCarver.BandOnly(new ComposeEnvelope("rot_180", 2, 12, 5, 9, 11, 300, 300, 2800, -40, -40, 40, 40))
            .HalfGapCells;
        // legs at x -5..-2 and 2..5 — equal width, mirror-paired, a 4-cell bay straddling the axis
        var face = Unit(h, (-5, 3), (2, 3));

        var single = Band("rot_180", split: false, face)!;
        var pair = Band("rot_180", split: true, face)!;

        await Assert.That(single[2]).IsEqualTo(10).Because("one band spans the whole face hull, bay included");
        await Assert.That(pair[2]).IsEqualTo(3).Because("a split band spans one leg and lets the image supply the other");
        // the island: the band and its own reflection leave the bay uncovered between them
        await Assert.That(-pair[0] - pair[2]).IsGreaterThan(pair[0] + pair[2])
            .Because("the reflected band lands beyond this one, not on top of it");
    }

    /// <summary>Both outcomes are legal for the same face — the split is asked for, not implied by the geometry.
    /// A face that can host two bands is equally valid crossed by one.</summary>
    [Test]
    public async Task The_split_is_a_request_the_face_can_grant_not_a_consequence_of_it()
    {
        var h = MidCarver.BandOnly(new ComposeEnvelope("rot_180", 2, 12, 5, 9, 11, 300, 300, 2800, -40, -40, 40, 40))
            .HalfGapCells;
        var face = Unit(h, (-5, 3), (2, 3));
        await Assert.That(Band("rot_180", split: false, face)![2])
            .IsNotEqualTo(Band("rot_180", split: true, face)![2]);
    }

    /// <summary>Legs of unequal width do not coincide with their own reflection, so a band over one would leave
    /// the other's image partly uncrossed. Such a face keeps the single band even when the split is asked for —
    /// which is why the leg widths stay free: the mid reads the face, it does not shape it.</summary>
    [Test]
    public async Task An_unequal_legged_face_is_refused_the_split_and_keeps_one_band()
    {
        var h = MidCarver.BandOnly(new ComposeEnvelope("rot_180", 2, 12, 5, 9, 11, 300, 300, 2800, -40, -40, 40, 40))
            .HalfGapCells;
        var lopsided = Unit(h, (-6, 4), (2, 3));   // 4 wide against 3 — not its own mirror
        await Assert.That(Band("rot_180", split: true, lopsided)![2])
            .IsEqualTo(Band("rot_180", split: false, lopsided)![2]);
    }

    /// <summary>Under a mirror the image lands straight across rather than reflected, so a band over one leg
    /// would fan back onto itself and leave the far leg with no crossing at all. The split is refused there
    /// however symmetric the face is.</summary>
    [Test]
    public async Task A_mirror_symmetry_never_splits_however_symmetric_the_face()
    {
        var h = MidCarver.BandOnly(new ComposeEnvelope("mirror_z", 2, 12, 5, 9, 11, 300, 300, 2800, -40, -40, 40, 40))
            .HalfGapCells;
        var face = Unit(h, (-5, 3), (2, 3));
        await Assert.That(Band("mirror_z", split: true, face)![2])
            .IsEqualTo(Band("mirror_z", split: false, face)![2]);
    }
}
