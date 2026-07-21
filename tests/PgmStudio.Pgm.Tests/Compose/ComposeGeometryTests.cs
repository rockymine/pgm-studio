using PgmStudio.Pgm.Compose;

namespace PgmStudio.Pgm.Tests.Compose;

/// <summary>The collinear-chain measurement (LN2's unit of account) on synthetic rects.</summary>
public sealed class ComposeGeometryTests
{
    [Test]
    public async Task Abutting_collinear_rects_merge_into_one_chain()
    {
        // two 6-cell runs of the same cross interval, abutting end to end → one 12-cell (60-block) chain
        var rects = new[] { new[] { 0, 0, 6, 2 }, [6, 0, 6, 2] };
        await Assert.That(ComposeGeometry.MaxChainBlocks(5, rects)).IsEqualTo(60);
    }

    [Test]
    public async Task A_jogged_pair_does_not_merge()
    {
        // same width and axis but offset cross intervals — a jog is a turn, not a longer lane
        var rects = new[] { new[] { 0, 0, 6, 2 }, [6, 1, 6, 2] };
        await Assert.That(ComposeGeometry.MaxChainBlocks(5, rects)).IsEqualTo(30);
    }

    [Test]
    public async Task A_width_change_does_not_merge()
    {
        var rects = new[] { new[] { 0, 0, 6, 2 }, [6, 0, 6, 3] };
        await Assert.That(ComposeGeometry.MaxChainBlocks(5, rects)).IsEqualTo(30);
    }

    [Test]
    public async Task Separated_collinear_rects_do_not_merge()
    {
        var rects = new[] { new[] { 0, 0, 6, 2 }, [8, 0, 6, 2] };
        await Assert.That(ComposeGeometry.MaxChainBlocks(5, rects)).IsEqualTo(30);
    }

    [Test]
    public async Task A_single_long_rect_is_its_own_chain()
    {
        var rects = new[] { new[] { 0, 0, 11, 2 } };
        await Assert.That(ComposeGeometry.MaxChainBlocks(5, rects)).IsEqualTo(55);
    }

    [Test]
    public async Task Chains_measure_both_axes()
    {
        // a 4×9 rect: its z-run (9 cells = 45 blocks) dominates its x-run
        var rects = new[] { new[] { 0, 0, 4, 9 } };
        await Assert.That(ComposeGeometry.MaxChainBlocks(5, rects)).IsEqualTo(45);
    }
}
