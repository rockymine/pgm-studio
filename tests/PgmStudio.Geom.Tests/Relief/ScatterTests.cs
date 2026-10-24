using PgmStudio.Geom.Relief;

namespace PgmStudio.Geom.Tests.Relief;

/// <summary>
/// What the scattering operations guarantee: Lumps only lift, stand where their mask is and stay within the heights
/// they state; a Shatter moves each piece as one plane and cuts its clefts at the seams; and both lay the same pieces
/// at every image of their mask under the map's symmetry.
/// </summary>
public sealed class ScatterTests
{
    private const int Size = 40;

    private static ReliefField Flat(double height) => ReliefField.Flat(Size, Size, height);

    /// <summary>Whether every column stands as high as the one a half turn about the field's middle carries it to.</summary>
    private static bool HalfTurnSymmetric(ReliefField field)
    {
        for (var z = 0; z < Size; z++)
            for (var x = 0; x < Size; x++)
                if (Math.Abs(field.At(x, z) - field.At(Size - 1 - x, Size - 1 - z)) > 1e-9) return false;
        return true;
    }

    [Test]
    public async Task Lumps_only_lift_and_none_stands_far_outside_its_mask()
    {
        var field = Flat(50);
        var patch = MaskArea.Ellipse(20.5, 20.5, 10, 10);
        field.Run(new ModifierSpec { Op = ModifierOp.Lumps, Areas = [patch], Count = 20, Across = (4, 6), Heights = (3, 3), Jag = 0 });

        await Assert.That(field.Heights.Min()).IsEqualTo(50);
        await Assert.That(field.Heights.Max()).IsGreaterThan(52).Because("at least one lump's middle falls inside the mask");
        foreach (var index in field.Outside(patch, 1.4))
            await Assert.That(field.Heights[index]).IsEqualTo(50)
                .Because("a lump's middle is inside the mask and it reaches at most three blocks from it");
    }

    [Test]
    public async Task One_lump_peaks_at_the_height_it_states()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec
        {
            Op = ModifierOp.Lumps, Areas = [MaskArea.Ellipse(20.5, 20.5, 10, 10)], Count = 1, Seed = 3,
            Across = (12, 12), Heights = (4, 4), Stretch = (1, 1), Jag = 0,
        });

        await Assert.That(field.Heights.Max()).IsLessThanOrEqualTo(54).And.IsGreaterThan(53.5);
    }

    [Test]
    public async Task Lumps_fanned_by_a_half_turn_lay_the_same_lumps_at_the_image()
    {
        var field = Flat(50);
        var areas = MaskArea.Fanned([MaskArea.Ellipse(10.5, 12.5, 7, 5)], ["rot_180"], 20, 20);
        field.Run(new ModifierSpec { Op = ModifierOp.Lumps, Areas = areas, Count = 10, Seed = 7 });

        await Assert.That(field.Heights.Max()).IsGreaterThan(50);
        await Assert.That(HalfTurnSymmetric(field)).IsTrue();
    }

    [Test]
    public async Task A_level_shatter_moves_each_piece_by_one_amount_within_its_bound()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec
        {
            Op = ModifierOp.Shatter, Areas = [MaskArea.Board], Cell = 10, Amount = 4, Tilt = 0, Gap = 0, Wander = 0,
        });

        var levels = field.Heights.Select(height => Math.Round(height, 9)).Distinct().ToArray();
        await Assert.That(levels.Length).IsGreaterThan(4).And.IsLessThanOrEqualTo(36)
            .Because("a 40-block board broken ten apart holds about sixteen pieces, each at one height");
        await Assert.That(field.Heights.All(height => height is >= 46 and <= 54)).IsTrue();
    }

    [Test]
    public async Task A_shatter_cuts_its_clefts_along_the_seams_and_nowhere_else()
    {
        var field = Flat(50);
        field.Run(new ModifierSpec
        {
            Op = ModifierOp.Shatter, Areas = [MaskArea.Board], Cell = 10, Amount = 0, Tilt = 0, Gap = 1, Depth = 4,
        });

        await Assert.That(field.Heights.Max()).IsEqualTo(50).Within(1e-9);
        await Assert.That(field.Heights.Min()).IsLessThan(47);
        await Assert.That(field.Heights.Count(height => height > 50 - 1e-9)).IsGreaterThan(field.Heights.Length / 2)
            .Because("most of a piece lies further than its gap from any seam");
    }

    [Test]
    public async Task A_shatter_over_a_folded_board_is_the_same_at_every_image()
    {
        var field = Flat(50);
        var board = MaskArea.Fanned([MaskArea.Board], ["rot_180"], 20, 20);
        field.Run(new ModifierSpec { Op = ModifierOp.Shatter, Areas = board, Cell = 8 });

        await Assert.That(HalfTurnSymmetric(field)).IsTrue();
    }
}
