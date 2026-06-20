using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests.Algorithms;

/// <summary>
/// Zhang–Suen thinning erodes a solid mask to a 1-cell-wide skeleton: a thick bar becomes a thin line
/// (small cross-extent, far fewer cells), and an empty mask stays empty.
/// </summary>
public sealed class ZhangSuenTests
{
    private static HashSet<(int, int)> Rect(int x0, int z0, int w, int h)
    {
        var s = new HashSet<(int, int)>();
        for (var x = x0; x < x0 + w; x++)
            for (var z = z0; z < z0 + h; z++)
                s.Add((x, z));
        return s;
    }

    [Test]
    public async Task A_thick_bar_thins_to_roughly_one_cell_wide()
    {
        var skel = ZhangSuen.Thin(Rect(0, 0, 40, 7));
        await Assert.That(skel.Count).IsLessThan(40 * 7);
        var span = skel.Max(c => c.Item2) - skel.Min(c => c.Item2);   // z-extent of a horizontal bar's skeleton
        await Assert.That(span).IsLessThanOrEqualTo(3);
    }

    [Test]
    public async Task Empty_input_stays_empty()
    {
        await Assert.That(ZhangSuen.Thin([]).Count).IsEqualTo(0);
    }
}
