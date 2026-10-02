using PgmStudio.Analysis.Scan;

namespace PgmStudio.Analysis.Tests;

/// <summary>
/// Side-view depth-map tests over synthetic segments: a controlled case of the primary/depth axis swap,
/// nearest-depth normalisation and empty=-1.
/// </summary>
public sealed class SideViewTests
{
    // (x, z, y_start, y_end)
    private static readonly (int, int, int, int)[] Segs =
    [
        (0, 0, 0, 2),   // x0 z0, y0–2
        (0, 1, 1, 3),   // x0 z1, y1–3
        (1, 0, 0, 0),   // x1 z0, y0
        (2, 2, 5, 5),   // x2 z2, y5
    ];

    private static string Flat(short[] d) => string.Join(",", d);

    [Test]
    public async Task Projects_along_z_matching_reference()
    {
        var m = SideView.Build(Segs, "nz")!;
        await Assert.That(m.PrimaryMin).IsEqualTo(0);
        await Assert.That(m.PrimaryCount).IsEqualTo(3);
        await Assert.That(m.YMin).IsEqualTo(0);
        await Assert.That(m.YCount).IsEqualTo(6);
        await Assert.That(Flat(m.Depth)).IsEqualTo("0,0,0,128,-1,-1,0,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,255");
    }

    [Test]
    public async Task Projects_along_x_matching_reference()
    {
        var m = SideView.Build(Segs, "nx")!;
        await Assert.That(Flat(m.Depth)).IsEqualTo("0,0,0,-1,-1,-1,-1,0,0,0,-1,-1,-1,-1,-1,-1,-1,255");
    }

    [Test]
    public async Task Empty_segments_return_null()
        => await Assert.That(SideView.Build(Array.Empty<(int, int, int, int)>(), "nz")).IsNull();
}
