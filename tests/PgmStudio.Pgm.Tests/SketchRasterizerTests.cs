using PgmStudio.Pgm.Editing;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// S2e: SketchRasterizer turns a stored sketch layout into the finished world's solid (x,z) cells —
/// the 4-step add/subtract/override set algebra, ring rasterization (rect/circle/polygon), and
/// per-island mirror copies. Cell output is asserted directly (no DB / island detection).
/// </summary>
public sealed class SketchRasterizerTests
{
    private static HashSet<(int, int)> Raster(string json) => SketchRasterizer.Rasterize(json).ToHashSet();

    [Test]
    public async Task Add_rectangle_rasterizes_to_block_cells()
    {
        // mirror centre far away + mirrors:false → no mirror copy, just the primary rect.
        var cells = Raster("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","override":false,"min_x":0,"max_x":4,"min_z":0,"max_z":4}],
                   "islands":[{"id":"i1","name":"A","mirrors":false,"shapeIds":["a"]}]}}
        """);
        await Assert.That(cells.Count).IsEqualTo(16);            // 4×4
        await Assert.That(cells.Contains((0, 0))).IsTrue();
        await Assert.That(cells.Contains((3, 3))).IsTrue();
        await Assert.That(cells.Contains((4, 4))).IsFalse();
    }

    [Test]
    public async Task Subtract_carves_an_interior_hole()
    {
        var cells = Raster("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layout":{"shapes":[
            {"id":"a","type":"rectangle","operation":"add","override":false,"min_x":0,"max_x":10,"min_z":0,"max_z":10},
            {"id":"b","type":"rectangle","operation":"subtract","override":false,"min_x":3,"max_x":7,"min_z":3,"max_z":7}],
          "islands":[{"id":"i1","name":"A","mirrors":false,"shapeIds":["a","b"]}]}}
        """);
        await Assert.That(cells.Count).IsEqualTo(84);            // 100 − 16
        await Assert.That(cells.Contains((0, 0))).IsTrue();
        await Assert.That(cells.Contains((9, 9))).IsTrue();
        await Assert.That(cells.Contains((4, 4))).IsFalse();     // inside the subtract
    }

    [Test]
    public async Task Mirror_x_adds_a_reflected_copy()
    {
        var cells = Raster("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":0,"cz":0}},
         "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","override":false,"min_x":20,"max_x":24,"min_z":0,"max_z":4}],
                   "islands":[{"id":"i1","name":"A","mirrors":true,"shapeIds":["a"]}]}}
        """);
        await Assert.That(cells.Count).IsEqualTo(32);            // 16 primary + 16 mirrored
        await Assert.That(cells.Contains((20, 0))).IsTrue();     // primary
        await Assert.That(cells.Contains((-24, 0))).IsTrue();    // mirror across x=0
    }

    [Test]
    public async Task Mirror_d1_adds_a_diagonally_reflected_copy()
    {
        // mirror_d1 across (0,0) maps (x,z) → (z,x): the rect at x∈[20,24],z∈[0,4] reflects to x∈[0,4],z∈[20,24].
        // Guards against the diagonal axes silently falling through to an identity transform.
        var cells = Raster("""
        {"setup":{"mirror_mode":"mirror_d1","center":{"cx":0,"cz":0}},
         "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","override":false,"min_x":20,"max_x":24,"min_z":0,"max_z":4}],
                   "islands":[{"id":"i1","name":"A","mirrors":true,"shapeIds":["a"]}]}}
        """);
        await Assert.That(cells.Count).IsEqualTo(32);            // 16 primary + 16 reflected (disjoint)
        await Assert.That(cells.Contains((20, 0))).IsTrue();     // primary
        await Assert.That(cells.Contains((0, 20))).IsTrue();     // mirror_d1 of (20,0)
    }

    [Test]
    public async Task Circle_rasterizes_a_disc()
    {
        var cells = Raster("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layout":{"shapes":[{"id":"a","type":"circle","operation":"add","override":false,"center_x":0,"center_z":0,"radius":5}],
                   "islands":[{"id":"i1","name":"A","mirrors":false,"shapeIds":["a"]}]}}
        """);
        await Assert.That(cells.Contains((0, 0))).IsTrue();      // centre
        await Assert.That(cells.Contains((4, 0))).IsTrue();      // dist 4.5 < 5
        await Assert.That(cells.Contains((5, 5))).IsFalse();     // dist ≈ 7.8 > 5
        await Assert.That(cells.Count is > 60 and < 90).IsTrue(); // ≈ π·25
    }

    [Test]
    public async Task Polygon_rasterizes_its_interior()
    {
        var cells = Raster("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layout":{"shapes":[{"id":"a","type":"polygon","operation":"add","override":false,"vertices":[[0,0],[10,0],[0,10]]}],
                   "islands":[{"id":"i1","name":"A","mirrors":false,"shapeIds":["a"]}]}}
        """);
        await Assert.That(cells.Contains((1, 1))).IsTrue();      // inside (x+z < 10)
        await Assert.That(cells.Contains((8, 8))).IsFalse();     // outside the hypotenuse
    }

    [Test]
    public async Task Empty_layout_yields_no_cells()
    {
        await Assert.That(SketchRasterizer.Rasterize("{}").Count).IsEqualTo(0);
        await Assert.That(SketchRasterizer.Rasterize("""{"layout":{"shapes":[]}}""").Count).IsEqualTo(0);
    }
}
