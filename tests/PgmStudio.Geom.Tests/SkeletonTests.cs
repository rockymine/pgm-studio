using PgmStudio.Geom;
using PgmStudio.Geom.Algorithms;

namespace PgmStudio.Geom.Tests;

/// <summary>
/// Graph tracing over a Zhang–Suen skeleton. A thick bar thins to a line (two endpoints, no junction); a
/// T-shape thins to three lanes meeting at one junction (three endpoints, ≥1 junction).
/// </summary>
public sealed class SkeletonTests
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
    public async Task A_thick_bar_thins_to_a_line_with_two_endpoints()
    {
        var skel = ZhangSuen.Thin(Rect(0, 0, 40, 7));
        var g = Skeleton.Trace(skel);
        await Assert.That(g.Junctions.Count).IsEqualTo(0);
        await Assert.That(g.Endpoints.Count).IsEqualTo(2);
    }

    [Test]
    public async Task A_T_shape_has_three_lane_tips_and_one_junction()
    {
        var t = Rect(0, 0, 40, 7);           // horizontal bar
        t.UnionWith(Rect(17, 7, 7, 30));     // stem going down from the middle
        var g = Skeleton.Trace(ZhangSuen.Thin(t));
        await Assert.That(g.Endpoints.Count).IsEqualTo(3);
        await Assert.That(g.Junctions.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Empty_input_is_handled()
    {
        var g = Skeleton.Trace(ZhangSuen.Thin([]));
        await Assert.That(g.Endpoints.Count).IsEqualTo(0);
    }
}
