using PgmStudio.Pgm.Sketch;

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

    // ── height (S5) ────────────────────────────────────────────────────────────

    [Test]
    public async Task Base_height_and_floor_give_a_uniform_column()
    {
        var cells = SketchRasterizer.RasterizeColumns("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4,"base_height":12,"floor":-3}],
                   "islands":[{"id":"i1","mirrors":false,"shapeIds":["a"]}]}}
        """);
        await Assert.That(cells.Count).IsEqualTo(16);
        await Assert.That(cells.All(c => c.YTop == 12 && c.YFloor == -3)).IsTrue();
    }

    [Test]
    public async Task Anchor_heights_ramp_north_to_south()
    {
        // A 10×10 polygon: north edge (z=0) at 0, south edge (z=10) at 20 → YTop rises with z.
        var cells = SketchRasterizer.RasterizeColumns("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layout":{"shapes":[{"id":"a","type":"polygon","operation":"add",
            "vertices":[[0,0],[10,0],[10,10],[0,10]],"anchor_heights":[0,0,20,20]}],
                   "islands":[{"id":"i1","mirrors":false,"shapeIds":["a"]}]}}
        """);
        int Top(int x, int z) => cells.First(c => c.X == x && c.Z == z).YTop;
        await Assert.That(Top(5, 0)).IsLessThan(Top(5, 9));      // rises toward the south edge
        await Assert.That(Top(5, 0)).IsEqualTo(1);               // z=0 row centre (z+0.5=0.5 → ~1)
    }

    [Test]
    public async Task Stacked_layers_keep_separate_columns_offset_by_base_y()
    {
        // Two layers over the same footprint: ground (base 0, height 5) + a sky bridge (base 20, height 4).
        // The shared column carries both segments — [0,5] and [20,24] — not one merged span.
        var cells = SketchRasterizer.RasterizeColumns("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layers":[
           {"base_y":0,"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4,"base_height":5}],"islands":[{"id":"i1","mirrors":false,"shapeIds":["a"]}]}},
           {"base_y":20,"layout":{"shapes":[{"id":"b","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4,"base_height":4}],"islands":[{"id":"i2","mirrors":false,"shapeIds":["b"]}]}}
         ]}
        """);
        var col = cells.Where(c => c.X == 1 && c.Z == 1).OrderBy(c => c.YFloor).ToList();
        await Assert.That(col.Count).IsEqualTo(2);
        await Assert.That(col[0]).IsEqualTo((1, 1, 0, 5));
        await Assert.That(col[1]).IsEqualTo((1, 1, 20, 24));
    }

    [Test]
    public async Task Legacy_single_layout_still_rasterizes_as_one_layer()
    {
        // Back-compat: a pre-S7 {layout:{…}} with no `layers` is treated as one layer at base_y 0.
        var cells = SketchRasterizer.Rasterize("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4}],"islands":[{"id":"i1","mirrors":false,"shapeIds":["a"]}]}}
        """).ToHashSet();
        await Assert.That(cells.Count).IsEqualTo(16);
    }

    [Test]
    public async Task Mirror_copy_keeps_the_column_height()
    {
        // rot_180 of a height-12 rect about the origin: both the primary and its mirror are at YTop 12.
        var cells = SketchRasterizer.RasterizeColumns("""
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":4,"max_x":8,"min_z":4,"max_z":8,"base_height":12}],
                   "islands":[{"id":"i1","mirrors":true,"shapeIds":["a"]}]}}
        """);
        await Assert.That(cells.Any(c => c.X >= 4 && c.YTop == 12)).IsTrue();   // primary
        await Assert.That(cells.Any(c => c.X < 0  && c.YTop == 12)).IsTrue();   // mirror, same height
    }
}
