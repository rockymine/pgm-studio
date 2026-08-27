using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// Shapes that stand OUT of the relief rather than being part of it. A shape without the word is ordinary
/// ground and the relief is what the ground does; the three words are for the other thing a shape can be.
/// </summary>
public sealed class SketchErectTests
{
    // One 40×40 island whose relief tilts from 6 up to 26, and one 10×10 shape on it that may declare a mode.
    private static string Layout(string? mode, double amount = 4, double floor = 0) => $$"""
    {
      "setup": { "mirror_mode": "rot_180", "center": { "cx": 100, "cz": 100 } },
      "layers": [{ "id": "ground", "base_y": 0, "layout": {
        "shapes": [
          { "id": "s1", "type": "rectangle", "operation": "add",
            "min_x": 0, "min_z": 0, "max_x": 40, "max_z": 40, "base_height": 5, "floor": 0 },
          { "id": "s2", "type": "rectangle", "operation": "add",
            "min_x": 14, "min_z": 14, "max_x": 24, "max_z": 24,
            "base_height": {{amount}}, "floor": {{floor}}
            {{(mode is null ? "" : $", \"height_mode\": \"{mode}\"")}} }
        ],
        "islands": [ { "id": "i1", "mirrors": false, "shapeIds": ["s1", "s2"] } ]
      } }],
      "relief": {
        "i1": {
          "base": 6,
          "marks": [
            { "kind": "point", "at": [2, 2], "h": 6, "r": 3 },
            { "kind": "point", "at": [38, 38], "h": 26, "r": 3 }
          ]
        }
      }
    }
    """;

    private static Dictionary<(int X, int Z), int> Tops(string layoutJson)
    {
        var tops = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top, _) in SketchRasterizer.RasterizeColumns(layoutJson))
            if (!tops.TryGetValue((x, z), out var current) || top > current) tops[(x, z)] = top;
        return tops;
    }

    [Test]
    public async Task Without_the_word_a_shape_is_ground_and_the_relief_decides_it()
    {
        // The default has to stay the default: a shape drawn to make a landmass is part of the landmass, and
        // the relief inside it is what the ground does.
        var tops = Tops(Layout(null));
        var plain = Tops(Layout(null, amount: 30));   // a much taller plate, still ordinary ground

        await Assert.That(tops[(19, 19)]).IsEqualTo(plain[(19, 19)]);
    }

    [Test]
    public async Task A_level_shape_cuts_a_flat_top_through_the_field()
    {
        // A mesa. The whole footprint stands at one height whatever the ground under it was doing, which is
        // what makes its faces cliffs.
        var tops = Tops(Layout("level", amount: 30));

        await Assert.That(tops[(15, 15)]).IsEqualTo(30);
        await Assert.That(tops[(23, 23)]).IsEqualTo(30);
        await Assert.That(tops[(19, 19)]).IsEqualTo(30);
    }

    [Test]
    public async Task A_raised_shape_stands_proud_of_whatever_it_is_over()
    {
        // A monolith: a fixed amount above the ground under it, read at the median so it is one flat-topped
        // thing standing proud rather than a blanket following the hillside.
        var ground = Tops(Layout(null));
        var raised = Tops(Layout("raise", amount: 7));

        var under = new[] { (15, 15), (19, 19), (23, 23) }.Select(cell => ground[cell]).OrderBy(h => h).ToList();
        var median = under[under.Count / 2];

        // One height across the whole footprint...
        await Assert.That(raised[(15, 15)]).IsEqualTo(raised[(23, 23)]);
        // ...and it is above the ground it covers.
        await Assert.That(raised[(19, 19)]).IsGreaterThan(median);
    }

    [Test]
    public async Task A_raised_shape_keeps_its_prominence_wherever_the_ground_is_higher()
    {
        // The property the word exists for: dragged onto higher ground it rises with it, rather than being
        // swallowed the way an absolute height would be.
        static string At(int minX, int minZ) => $$"""
        {
          "layers": [{ "id": "ground", "base_y": 0, "layout": {
            "shapes": [
              { "id": "s1", "type": "rectangle", "operation": "add",
                "min_x": 0, "min_z": 0, "max_x": 40, "max_z": 40, "base_height": 5, "floor": 0 },
              { "id": "s2", "type": "rectangle", "operation": "add", "height_mode": "raise",
                "min_x": {{minX}}, "min_z": {{minZ}}, "max_x": {{minX + 8}}, "max_z": {{minZ + 8}},
                "base_height": 6, "floor": 0 }
            ],
            "islands": [ { "id": "i1", "mirrors": false, "shapeIds": ["s1", "s2"] } ]
          } }],
          "relief": {
            "i1": { "base": 6, "marks": [ { "kind": "point", "at": [2, 2], "h": 6, "r": 3 },
                                          { "kind": "point", "at": [38, 38], "h": 26, "r": 3 } ] }
          }
        }
        """;

        var low = Tops(At(4, 4));
        var high = Tops(At(28, 28));
        await Assert.That(high[(32, 32)]).IsGreaterThan(low[(8, 8)]);
    }

    [Test]
    public async Task A_sunken_shape_cuts_down_into_the_ground_it_covers()
    {
        var ground = Tops(Layout(null));
        var sunk = Tops(Layout("sink", amount: 5));

        await Assert.That(sunk[(19, 19)]).IsLessThan(ground[(19, 19)]);
        await Assert.That(sunk[(15, 15)]).IsEqualTo(sunk[(23, 23)]);   // a floor, not a slope
    }

    [Test]
    public async Task An_erected_shape_never_sinks_below_its_own_floor()
    {
        // A column of no height at all is not a quarry, it is a hole in the world.
        var tops = Tops(Layout("sink", amount: 400));
        await Assert.That(tops[(19, 19)]).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task An_erected_shape_keeps_its_own_surface_rather_than_being_levelled()
    {
        // The word says what the surface is measured FROM. It does not say the surface is flat — a polygon
        // tilted through its anchors is still tilted when it is erected, and reading one number here silently
        // levelled it.
        static string Tilted(string? mode) => $$"""
        {
          "layers": [{ "id": "ground", "base_y": 0, "layout": {
            "shapes": [
              { "id": "s1", "type": "rectangle", "operation": "add",
                "min_x": 0, "min_z": 0, "max_x": 60, "max_z": 40, "base_height": 6, "floor": 0 },
              { "id": "s2", "type": "polygon", "operation": "add",
                {{(mode is null ? "" : $"\"height_mode\": \"{mode}\",")}}
                "vertices": [[10,10],[50,10],[50,30],[10,30]],
                "anchor_heights": [8, 20, 20, 8], "base_height": 14, "floor": 0 }
            ],
            "islands": [ { "id": "i1", "mirrors": false, "shapeIds": ["s1", "s2"] } ]
          } }]
        }
        """;

        var plain = Tops(Tilted(null));
        var level = Tops(Tilted("level"));
        var raised = Tops(Tilted("raise"));

        // The plain shape tilts from its west anchors to its east ones.
        await Assert.That(plain[(48, 20)] - plain[(12, 20)]).IsGreaterThan(8);
        // Erected, it still does — and a raise carries the same tilt up above the ground.
        await Assert.That(level[(48, 20)] - level[(12, 20)]).IsEqualTo(plain[(48, 20)] - plain[(12, 20)]);
        await Assert.That(raised[(48, 20)] - raised[(12, 20)]).IsEqualTo(plain[(48, 20)] - plain[(12, 20)]);
        await Assert.That(raised[(12, 20)]).IsGreaterThan(level[(12, 20)]);
    }

    [Test]
    public async Task A_skirt_sits_the_shape_in_the_terrain_instead_of_on_it()
    {
        // Unskirted, an erected shape drops its whole height in one cell — right for a plinth, wrong for a
        // landform. The skirt eases the face back into the ground it meets over a stated distance.
        static string Mesa(int skirt) => $$"""
        {
          "layers": [{ "id": "ground", "base_y": 0, "layout": {
            "shapes": [
              { "id": "s1", "type": "rectangle", "operation": "add",
                "min_x": 0, "min_z": 0, "max_x": 60, "max_z": 40, "base_height": 6, "floor": 0 },
              { "id": "s2", "type": "rectangle", "operation": "add", "height_mode": "level",
                "min_x": 20, "min_z": 12, "max_x": 44, "max_z": 30,
                "base_height": 24, "floor": 0, "skirt": {{skirt}} }
            ],
            "islands": [ { "id": "i1", "mirrors": false, "shapeIds": ["s1", "s2"] } ]
          } }]
        }
        """;

        static int WorstEdgeStep(Dictionary<(int X, int Z), int> tops)
        {
            var worst = 0;
            foreach (var ((x, z), top) in tops)
                foreach (var (dx, dz) in new[] { (1, 0), (0, 1) })
                    if (tops.TryGetValue((x + dx, z + dz), out var other))
                        worst = Math.Max(worst, Math.Abs(top - other));
            return worst;
        }

        var sheer = Tops(Mesa(0));
        var skirted = Tops(Mesa(5));

        await Assert.That(WorstEdgeStep(sheer)).IsGreaterThanOrEqualTo(17);
        await Assert.That(WorstEdgeStep(skirted)).IsLessThan(WorstEdgeStep(sheer));
        // The middle still stands at its stated height — a skirt is an edge, not a flattening.
        await Assert.That(skirted[(32, 21)]).IsEqualTo(sheer[(32, 21)]);
    }

    [Test]
    public async Task A_skirt_follows_the_ground_it_meets_rather_than_one_average()
    {
        // A shape half on a hill should embed deeper on the low side, which is what makes it read as sitting
        // in the terrain rather than as a bevelled plate.
        const string layout = """
        {
          "layers": [{ "id": "ground", "base_y": 0, "layout": {
            "shapes": [
              { "id": "s1", "type": "rectangle", "operation": "add",
                "min_x": 0, "min_z": 0, "max_x": 60, "max_z": 40, "base_height": 6, "floor": 0 },
              { "id": "s2", "type": "rectangle", "operation": "add", "height_mode": "level",
                "min_x": 20, "min_z": 12, "max_x": 44, "max_z": 30, "base_height": 26, "floor": 0, "skirt": 6 }
            ],
            "islands": [ { "id": "i1", "mirrors": false, "shapeIds": ["s1", "s2"] } ]
          } }],
          "relief": {
            "i1": { "base": 8, "marks": [ { "kind": "point", "at": [4, 20], "h": 8, "r": 3 },
                                          { "kind": "point", "at": [56, 20], "h": 22, "r": 3 } ] }
          }
        }
        """;
        var tops = Tops(layout);

        // The west skirt eases from low ground and the east from high, so the west cell sits lower.
        await Assert.That(tops[(21, 21)]).IsLessThan(tops[(43, 21)]);
    }

    [Test]
    public async Task An_unknown_word_is_ignored_rather_than_guessed_at()
    {
        // A layout written by a generator that knows a word this build does not should place ordinary ground,
        // not terrain nobody asked for.
        var plain = Tops(Layout(null));
        var odd = Tops(Layout("levitate"));
        await Assert.That(odd[(19, 19)]).IsEqualTo(plain[(19, 19)]);
    }

    [Test]
    public async Task Erecting_works_without_a_relief_at_all()
    {
        // The word is about how a top is decided, not about the relief — a monolith on a flat plate is still
        // a monolith.
        const string flat = """
        {
          "layers": [{ "id": "ground", "base_y": 0, "layout": {
            "shapes": [
              { "id": "s1", "type": "rectangle", "operation": "add",
                "min_x": 0, "min_z": 0, "max_x": 30, "max_z": 30, "base_height": 5, "floor": 0 },
              { "id": "s2", "type": "rectangle", "operation": "add", "height_mode": "raise",
                "min_x": 10, "min_z": 10, "max_x": 18, "max_z": 18, "base_height": 9, "floor": 0 }
            ],
            "islands": [ { "id": "i1", "mirrors": false, "shapeIds": ["s1", "s2"] } ]
          } }]
        }
        """;
        var tops = Tops(flat);
        await Assert.That(tops[(2, 2)]).IsEqualTo(5);
        await Assert.That(tops[(14, 14)]).IsEqualTo(14);   // nine above the five under it
    }

    // ── a flight is a tilted quad, and the gradient is the whole of whether it walks ──────────────────────
    /// <summary>A ramp at one course a cell: the plainest thing an author writes, and the one a surface
    /// sampled at the cell's centre and rounded to nearest cannot hold — every sample lands on the rounding
    /// boundary and the courses come out as a beat of noughts and twos. Flooring the sample is stable there,
    /// so what a one-to-one quad builds is a stair of single courses.</summary>
    [Test]
    public async Task A_quad_tilted_one_course_a_cell_builds_single_course_steps()
    {
        var cells = SketchRasterizer.RasterizeColumns("""
        { "setup": { "mirror_mode": "rot_180", "center": { "cx": 1000, "cz": 0 } },
          "layers": [{ "id": "ground", "base_y": 0, "layout": {
            "shapes": [
              { "id": "g", "type": "rectangle", "operation": "add",
                "min_x": -4, "min_z": -4, "max_x": 28, "max_z": 12, "floor": 0, "base_height": 4 },
              { "id": "ramp", "type": "polygon", "operation": "add", "override": true,
                "height_mode": "level", "skirt": 0,
                "vertices": [[0,0],[24,0],[24,8],[0,8]],
                "anchor_heights": [4, 28, 28, 4], "floor": 0, "base_height": 29 }],
            "islands": [{ "id": "i", "mirrors": false, "shapeIds": ["g", "ramp"] }] } }] }
        """).Where(cell => cell.Z == 4 && cell.X >= 0 && cell.X < 24)
            .OrderBy(cell => cell.X).Select(cell => cell.YTop).ToList();

        await Assert.That(cells.Count).IsEqualTo(24);
        var steps = cells.Zip(cells.Skip(1), (low, high) => high - low).ToList();
        await Assert.That(steps.All(step => step == 1)).IsTrue();
        await Assert.That(cells[^1] - cells[0]).IsEqualTo(23);
    }

    /// <summary>And a gentler one still climbs a course at a time — the shallower gradient was never the
    /// problem, and must not become one.</summary>
    [Test]
    public async Task A_gentler_quad_climbs_a_course_at_a_time_too()
    {
        var cells = SketchRasterizer.RasterizeColumns("""
        { "setup": { "mirror_mode": "rot_180", "center": { "cx": 1000, "cz": 0 } },
          "layers": [{ "id": "ground", "base_y": 0, "layout": {
            "shapes": [
              { "id": "g", "type": "rectangle", "operation": "add",
                "min_x": -4, "min_z": -4, "max_x": 28, "max_z": 12, "floor": 0, "base_height": 4 },
              { "id": "ramp", "type": "polygon", "operation": "add", "override": true,
                "height_mode": "level", "skirt": 0,
                "vertices": [[0,0],[24,0],[24,8],[0,8]],
                "anchor_heights": [4, 16, 16, 4], "floor": 0, "base_height": 17 }],
            "islands": [{ "id": "i", "mirrors": false, "shapeIds": ["g", "ramp"] }] } }] }
        """).Where(cell => cell.Z == 4 && cell.X >= 0 && cell.X < 24)
            .OrderBy(cell => cell.X).Select(cell => cell.YTop).ToList();

        var steps = cells.Zip(cells.Skip(1), (low, high) => high - low).ToList();
        await Assert.That(steps.All(step => step is 0 or 1)).IsTrue();
    }
}
