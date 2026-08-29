using PgmStudio.Geom;
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
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","override":false,"min_x":0,"max_x":4,"min_z":0,"max_z":4}],
                   "groups":[{"id":"i1","name":"A","mirrors":false,"shapeIds":["a"]}]} }]}
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
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
            {"id":"a","type":"rectangle","operation":"add","override":false,"min_x":0,"max_x":10,"min_z":0,"max_z":10},
            {"id":"b","type":"rectangle","operation":"subtract","override":false,"min_x":3,"max_x":7,"min_z":3,"max_z":7}],
          "groups":[{"id":"i1","name":"A","mirrors":false,"shapeIds":["a","b"]}]} }]}
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
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","override":false,"min_x":20,"max_x":24,"min_z":0,"max_z":4}],
                   "groups":[{"id":"i1","name":"A","mirrors":true,"shapeIds":["a"]}]} }]}
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
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","override":false,"min_x":20,"max_x":24,"min_z":0,"max_z":4}],
                   "groups":[{"id":"i1","name":"A","mirrors":true,"shapeIds":["a"]}]} }]}
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
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"circle","operation":"add","override":false,"center_x":0,"center_z":0,"radius":5}],
                   "groups":[{"id":"i1","name":"A","mirrors":false,"shapeIds":["a"]}]} }]}
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
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"polygon","operation":"add","override":false,"vertices":[[0,0],[10,0],[0,10]]}],
                   "groups":[{"id":"i1","name":"A","mirrors":false,"shapeIds":["a"]}]} }]}
        """);
        await Assert.That(cells.Contains((1, 1))).IsTrue();      // inside (x+z < 10)
        await Assert.That(cells.Contains((8, 8))).IsFalse();     // outside the hypotenuse
    }

    [Test]
    public async Task Empty_layout_yields_no_cells()
    {
        await Assert.That(SketchRasterizer.Rasterize("{}").Count).IsEqualTo(0);
        await Assert.That(SketchRasterizer.Rasterize("""{"layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[]} }]}""").Count).IsEqualTo(0);
    }

    // ── height (S5) ────────────────────────────────────────────────────────────

    [Test]
    public async Task Base_height_and_floor_give_a_uniform_column()
    {
        // Floor = elevation, Height = thickness: floor 3 + height 12 → the column spans [3, 15].
        var cells = SketchRasterizer.RasterizeColumns("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4,"base_height":12,"floor":3}],
                   "groups":[{"id":"i1","mirrors":false,"shapeIds":["a"]}]} }]}
        """);
        await Assert.That(cells.Count).IsEqualTo(16);
        await Assert.That(cells.All(c => c.YTop == 15 && c.YFloor == 3)).IsTrue();
    }

    [Test]
    public async Task Unset_height_defaults_to_one()
    {
        // A shape with no base_height is one block tall (top 1, floor 0) — never zero-height.
        var cells = SketchRasterizer.RasterizeColumns("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4}],
                   "groups":[{"id":"i1","mirrors":false,"shapeIds":["a"]}]} }]}
        """);
        await Assert.That(cells.All(c => c.YTop == 1 && c.YFloor == 0)).IsTrue();
    }

    [Test]
    public async Task Negative_floor_and_height_clamp_to_the_minimums()
    {
        // Legacy/out-of-range stored values are clamped on finish: floor >= 0, top >= 1.
        var cells = SketchRasterizer.RasterizeColumns("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4,"base_height":-5,"floor":-2}],
                   "groups":[{"id":"i1","mirrors":false,"shapeIds":["a"]}]} }]}
        """);
        await Assert.That(cells.All(c => c.YTop == 1 && c.YFloor == 0)).IsTrue();
    }

    [Test]
    public async Task Anchor_heights_ramp_north_to_south()
    {
        // A 10×10 polygon: north edge (z=0) at 0, south edge (z=10) at 20 → YTop rises with z.
        var cells = SketchRasterizer.RasterizeColumns("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"polygon","operation":"add",
            "vertices":[[0,0],[10,0],[10,10],[0,10]],"anchor_heights":[0,0,20,20]}],
                   "groups":[{"id":"i1","mirrors":false,"shapeIds":["a"]}]} }]}
        """);
        int Top(int x, int z) => cells.First(c => c.X == x && c.Z == z).YTop;
        await Assert.That(Top(5, 0)).IsLessThan(Top(5, 9));      // rises toward the south edge
        await Assert.That(Top(5, 0)).IsEqualTo(1);               // z=0 row centre (z+0.5=0.5 → ~1)
    }

    [Test]
    public async Task Anchor_heights_are_thickness_lifted_by_the_floor()
    {
        // Same ramp but elevated: floor 10 lifts every column, so YFloor is 10 and each top is
        // 10 + the per-vertex thickness (thickness = anchor_heights, not an absolute top).
        var cells = SketchRasterizer.RasterizeColumns("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"polygon","operation":"add",
            "vertices":[[0,0],[10,0],[10,10],[0,10]],"anchor_heights":[4,4,20,20],"floor":10}],
                   "groups":[{"id":"i1","mirrors":false,"shapeIds":["a"]}]} }]}
        """);
        var south = cells.First(c => c.X == 5 && c.Z == 9);
        await Assert.That(south.YFloor).IsEqualTo(10);            // floor lifts the base
        await Assert.That(south.YTop).IsEqualTo(10 + 19);         // 10 + interpolated thickness at z=9.5
    }

    [Test]
    public async Task Stacked_layers_keep_separate_columns_offset_by_base_y()
    {
        // Two layers over the same footprint: ground (base 0, height 5) + a sky bridge (base 20, height 4).
        // The shared column carries both segments — [0,5] and [20,24] — not one merged span.
        var cells = SketchRasterizer.RasterizeColumns("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layers":[
           {"base_y":0,"layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4,"base_height":5}],"groups":[{"id":"i1","mirrors":false,"shapeIds":["a"]}]}},
           {"base_y":20,"layout":{"shapes":[{"id":"b","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4,"base_height":4}],"groups":[{"id":"i2","mirrors":false,"shapeIds":["b"]}]}}
         ]}
        """);
        var col = cells.Where(c => c.X == 1 && c.Z == 1).OrderBy(c => c.YFloor).ToList();
        await Assert.That(col.Count).IsEqualTo(2);
        await Assert.That(col[0]).IsEqualTo(new ColumnSegment(1, 1, 0, 5, "layer0"));
        await Assert.That(col[1]).IsEqualTo(new ColumnSegment(1, 1, 20, 24, "layer1"));
    }

    [Test]
    public async Task A_flat_board_rasterizes_from_its_one_ground_layer()
    {
        // A board with nothing stacked on it is a stack of one, and the mirror fans it the same way.
        var cells = SketchRasterizer.Rasterize("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":4,"min_z":0,"max_z":4}],"groups":[{"id":"i1","mirrors":false,"shapeIds":["a"]}]} }]}
        """).ToHashSet();
        await Assert.That(cells.Count).IsEqualTo(16);
    }

    [Test]
    public async Task Mirror_copy_keeps_the_column_height()
    {
        // rot_180 of a height-12 rect about the origin: both the primary and its mirror are at YTop 12.
        var cells = SketchRasterizer.RasterizeColumns("""
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[{"id":"a","type":"rectangle","operation":"add","min_x":4,"max_x":8,"min_z":4,"max_z":8,"base_height":12}],
                   "groups":[{"id":"i1","mirrors":true,"shapeIds":["a"]}]} }]}
        """);
        await Assert.That(cells.Any(c => c.X >= 4 && c.YTop == 12)).IsTrue();   // primary
        await Assert.That(cells.Any(c => c.X < 0  && c.YTop == 12)).IsTrue();   // mirror, same height
    }

    [Test]
    public async Task A_push_reaching_a_structural_room_leaves_its_ground_flat()
    {
        // A room's floor is one course over its whole frame and the bedrock under it is filled column by
        // column, so ground that slopes under a room is a floor spanning air. The room is held, and a push
        // whose skirt reaches it must therefore leave it alone — while the ground either side of it still
        // takes the lift, which is what makes the push worth having.
        const string board = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers":[{"id":"ground","base_y":0,"layout":{
           "shapes":[
             {"id":"s0","type":"polygon","operation":"add","base_height":14,
              "vertices":[[-20,0],[20,0],[20,60],[-20,60]]},
             {"id":"room","type":"rectangle","operation":"add","role":"woolRoom","intentRef":"red:blue",
              "base_height":18,"relief_scope":"hold","min_x":-5,"min_z":40,"max_x":5,"max_z":50}],
           "groups":[{"id":"team","mirrors":false,"shapeIds":["s0"]}]}}],
         "relief":{"team":{"base":14,"reach":0,"step":1,
           "marks":[{"id":"m","kind":"area","h":14,"ring":[[-20,0],[20,0],[20,10],[-20,10]]}],
           "pushes":[{"id":"rise","ring":[[-18,20],[18,20],[18,38],[-18,38]],
                      "amount":6,"falloff":14,"crown":2,"seed":3}]}}}
        """;

        var top = SketchRasterizer.RasterizeColumns(board).ToDictionary(c => (c.X, c.Z), c => c.YTop);
        var room = Enumerable.Range(-5, 10).SelectMany(x => Enumerable.Range(40, 10).Select(z => (x, z)))
                             .Select(cell => top.GetValueOrDefault(cell, int.MinValue)).ToList();

        await Assert.That(room.Distinct().Count()).IsEqualTo(1);        // the room is one height, everywhere
        await Assert.That(room[0]).IsGreaterThan(int.MinValue);
        // the push still did its job on the ground it was drawn over
        await Assert.That(top[(0, 30)]).IsGreaterThan(14);
    }

    [Test]
    public async Task A_room_stating_no_door_seats_on_the_ground_outside_it()
    {
        // Which side a room is entered from is what decides the height it should be flat at, and a room
        // stating no door is entered from wherever the ground reaches it. Reading the median under its own
        // footprint instead splits the difference and leaves a step at the way in as well as at the back.
        // Here the ground climbs along +z, so the two answers differ by more than a step.
        const string board = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers":[{"id":"ground","base_y":0,"layout":{
           "shapes":[
             {"id":"s0","type":"polygon","operation":"add","base_height":10,
              "vertices":[[-20,0],[20,0],[20,60],[-20,60]]},
             {"id":"room","type":"rectangle","operation":"add","role":"woolRoom","intentRef":"red:blue",
              "base_height":10,"relief_scope":"hold","min_x":-5,"min_z":40,"max_x":5,"max_z":50}],
           "groups":[{"id":"team","mirrors":false,"shapeIds":["s0"]}]}}],
         "relief":{"team":{"base":10,"reach":0,"step":1,"marks":[
           {"id":"low","kind":"area","h":10,"ring":[[-20,0],[20,0],[20,6],[-20,6]]},
           {"id":"high","kind":"area","h":24,"ring":[[-20,54],[20,54],[20,60],[-20,60]]}]}}}
        """;

        var top = SketchRasterizer.RasterizeColumns(board).ToDictionary(c => (c.X, c.Z), c => c.YTop);
        var room = Enumerable.Range(-5, 10).SelectMany(x => Enumerable.Range(40, 10).Select(z => (x, z)))
                             .Select(cell => top[cell]).ToList();

        await Assert.That(room.Distinct().Count()).IsEqualTo(1);        // flat, as a room always is
        // Flush with the ground either side of it, within the step a player takes for nothing.
        await Assert.That(Math.Abs(room[0] - top[(0, 39)])).IsLessThanOrEqualTo(1);
        await Assert.That(Math.Abs(room[0] - top[(0, 50)])).IsLessThanOrEqualTo(1);
    }

    [Test]
    public async Task A_shape_that_marks_itself_kept_clear_reports_its_own_columns()
    {
        // A wall drawn as terrain is terrain: nothing about its material or its layer separates it from the
        // ground beside it, so the shape has to say so itself. What comes back is the marked shape's own
        // footprint, and only that — the ground round it is still ground to dress.
        var kept = SketchRasterizer.KeepClearCells(SketchLayout.Parse("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
            {"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":10,"min_z":0,"max_z":10},
            {"id":"w","type":"rectangle","operation":"add","override":true,"keepClear":true,
             "min_x":2,"max_x":4,"min_z":0,"max_z":10}],
          "groups":[{"id":"i1","name":"A","mirrors":false,"shapeIds":["a","w"]}]} }]}
        """));

        await Assert.That(kept.Count).IsEqualTo(20);             // 2 wide x 10 long
        await Assert.That(kept.Contains((2, 5))).IsTrue();
        await Assert.That(kept.Contains((3, 0))).IsTrue();
        await Assert.That(kept.Contains((1, 5))).IsFalse();      // the ground beside it is not kept
        await Assert.That(kept.Contains((4, 5))).IsFalse();      // max is exclusive, as everywhere else
    }

    [Test]
    public async Task A_marked_shape_keeps_its_mirror_images_clear_too()
    {
        // A prop is fanned through the symmetry frame, so a keep-out that held on one half only would decline
        // one image of a pair and place the other.
        var kept = SketchRasterizer.KeepClearCells(SketchLayout.Parse("""
        {"setup":{"mirror_mode":"rot_180","center":{"cx":0,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
            {"id":"w","type":"rectangle","operation":"add","keepClear":true,
             "min_x":4,"max_x":6,"min_z":4,"max_z":6}],
          "groups":[{"id":"i1","name":"A","mirrors":true,"shapeIds":["w"]}]} }]}
        """));

        await Assert.That(kept.Contains((4, 4))).IsTrue();
        await Assert.That(kept.Contains((-5, -5))).IsTrue();     // its rot_180 image
    }

    [Test]
    public async Task An_unmarked_layout_keeps_nothing_clear()
    {
        var kept = SketchRasterizer.KeepClearCells(SketchLayout.Parse("""
        {"setup":{"mirror_mode":"mirror_x","center":{"cx":1000,"cz":0}},
         "layers": [{ "id": "ground", "base_y": 0, "layout":{"shapes":[
            {"id":"a","type":"rectangle","operation":"add","min_x":0,"max_x":10,"min_z":0,"max_z":10}],
          "groups":[{"id":"i1","name":"A","mirrors":false,"shapeIds":["a"]}]} }]}
        """));
        await Assert.That(kept).IsEmpty();
    }

    // ── a made thing: a layer that is not a storey ──────────────────────────────────────────────────────

    /// <summary>A hillside under a made thing that seats on it: ground rising a course every two blocks, and
    /// a 6x6 slab drawn at an absolute floor well above it.</summary>
    private const string SeatOnGround = ",\"seat\":\"ground\"";

    private static string Hillside(string seat, string kind = "prop") =>
        """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers":[
           {"id":"ground","base_y":0,"layout":{"shapes":[
              {"id":"g0","type":"rectangle","operation":"add","min_x":0,"max_x":12,"min_z":0,"max_z":12,"floor":0,"base_height":9},
              {"id":"g1","type":"rectangle","operation":"add","min_x":6,"max_x":12,"min_z":0,"max_z":12,"floor":0,"base_height":13}],
            "groups":[{"id":"land","mirrors":false,"shapeIds":["g0","g1"]}]}},
           {"id":"thing","kind":"KIND","prop":"thing"SEAT,"base_y":0,"layout":{"shapes":[
              {"id":"t0","type":"rectangle","operation":"add","min_x":3,"max_x":9,"min_z":3,"max_z":9,"floor":40,"base_height":4}],
            "groups":[{"id":"body","mirrors":false,"shapeIds":["t0"]}]}}]}
        """.Replace("KIND", kind).Replace("SEAT", seat);

    [Test]
    public async Task A_layer_that_states_no_seat_stands_at_the_height_it_was_drawn()
    {
        var spans = SketchRasterizer.RasterizeColumns(Hillside(seat: ""));
        var thing = spans.Where(span => span.Layer == "thing").ToList();
        await Assert.That(thing.Min(span => span.YFloor)).IsEqualTo(40);
    }

    /// <summary>
    /// <b>A made thing seats on the lowest column of its own footprint, one course down.</b> The slab covers
    /// ground at 9 and ground at 13; seating on the higher of the two would leave its low side on stilts, so
    /// the low one is what it takes — and the bank standing over that seat is cleared from the footprint
    /// while the ground outside keeps its height.
    /// </summary>
    [Test]
    public async Task A_seated_made_thing_settles_on_the_low_side_and_the_bank_is_cut_out_of_its_footprint()
    {
        var spans = SketchRasterizer.RasterizeColumns(Hillside(seat: SeatOnGround));
        var thing = spans.Where(span => span.Layer == "thing").ToList();
        await Assert.That(thing.Min(span => span.YFloor)).IsEqualTo(8);      // ground tops at 9, one course down
        await Assert.That(thing.Min(span => span.YTop)).IsEqualTo(12);       // its own four courses came with it

        // Inside the footprint the high ground is cut back to the seat; outside it keeps its own top.
        var inside = spans.Single(span => span.Layer == "ground" && span.Cell == (7, 5));
        var outside = spans.Single(span => span.Layer == "ground" && span.Cell == (11, 5));
        await Assert.That(inside.YTop).IsEqualTo(8);
        await Assert.That(outside.YTop).IsEqualTo(13);
    }

    /// <summary>Every layer of one made thing moves together. Two layers naming the same <c>prop</c> seat over
    /// the union of what they cover, so a sculpture whose runs are split across layers stays one shape — the
    /// failure a per-layer seat would produce is the thing coming apart in the air.</summary>
    [Test]
    public async Task Layers_of_one_made_thing_seat_together()
    {
        const string json = """
        {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
         "layers":[
           {"id":"ground","base_y":0,"layout":{"shapes":[
              {"id":"g0","type":"rectangle","operation":"add","min_x":0,"max_x":12,"min_z":0,"max_z":12,"floor":0,"base_height":9}],
            "groups":[{"id":"land","mirrors":false,"shapeIds":["g0"]}]}},
           {"id":"a","kind":"prop","prop":"thing","seat":"ground","base_y":0,"layout":{"shapes":[
              {"id":"a0","type":"rectangle","operation":"add","min_x":3,"max_x":6,"min_z":3,"max_z":6,"floor":40,"base_height":2}],
            "groups":[{"id":"ab","mirrors":false,"shapeIds":["a0"]}]}},
           {"id":"b","kind":"prop","prop":"thing","seat":"ground","base_y":0,"layout":{"shapes":[
              {"id":"b0","type":"rectangle","operation":"add","min_x":3,"max_x":6,"min_z":3,"max_z":6,"floor":50,"base_height":2}],
            "groups":[{"id":"bb","mirrors":false,"shapeIds":["b0"]}]}}]}
        """;
        var spans = SketchRasterizer.RasterizeColumns(json);
        var lower = spans.Where(span => span.Layer == "a").Select(span => span.YFloor).Distinct().ToList();
        var upper = spans.Where(span => span.Layer == "b").Select(span => span.YFloor).Distinct().ToList();
        await Assert.That(lower).IsEquivalentTo(new[] { 8 });         // the whole thing came down 32
        await Assert.That(upper).IsEquivalentTo(new[] { 18 });        // and the ten blocks between them held
    }

    /// <summary>The same two slabs driven into one another, once as storeys and once as a made thing. A
    /// storey losing the gap under it is what <c>SK10</c> is for; a sculpture sinking into the hill it stands
    /// on has no gap to lose, and <c>SK11</c>'s reading of its overhangs — standable ground nothing reaches —
    /// is true of every dome on columns and a fault in none of them.</summary>
    [Test]
    public async Task The_storey_rules_read_a_ground_layer_and_skip_a_made_thing()
    {
        // A slab drawn INSIDE the ground's own span, and a second one hanging over it with nothing beneath.
        string Board(string kind) =>
            """
            {"setup":{"mirror_mode":"none","center":{"cx":0,"cz":0}},
             "layers":[
               {"id":"ground","base_y":0,"layout":{"shapes":[
                  {"id":"g0","type":"rectangle","operation":"add","min_x":0,"max_x":20,"min_z":0,"max_z":20,"floor":0,"base_height":9}],
                "groups":[{"id":"land","mirrors":false,"shapeIds":["g0"]}]}},
               {"id":"foot","kind":"KIND","prop":"thing","base_y":0,"layout":{"shapes":[
                  {"id":"t0","type":"rectangle","operation":"add","min_x":4,"max_x":10,"min_z":4,"max_z":10,"floor":4,"base_height":4}],
                "groups":[{"id":"a","mirrors":false,"shapeIds":["t0"]}]}},
               {"id":"canopy","kind":"KIND","prop":"thing","base_y":0,"layout":{"shapes":[
                  {"id":"t1","type":"rectangle","operation":"add","min_x":2,"max_x":12,"min_z":2,"max_z":12,"floor":30,"base_height":2}],
                "groups":[{"id":"b","mirrors":false,"shapeIds":["t1"]}]}}]}
            """.Replace("KIND", kind);

        var storeys = SketchLayout.Parse(Board("ground"));
        var made = SketchLayout.Parse(Board("prop"));

        await Assert.That(SketchRasterizer.OverlappingLayerSpans(storeys).Count).IsGreaterThan(0);
        await Assert.That(SketchRasterizer.OverlappingLayerSpans(made)).IsEmpty();

        await Assert.That(SketchRasterizer.DetachedMasses(storeys).Count).IsGreaterThan(0);
        await Assert.That(SketchRasterizer.DetachedMasses(made)).IsEmpty();
    }
}
