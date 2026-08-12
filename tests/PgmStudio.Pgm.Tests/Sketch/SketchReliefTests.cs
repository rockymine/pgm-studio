using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The seam where an authored relief becomes terrain: the rasterizer takes a relief-bearing island's column
/// tops from the solved field instead of from the shape's own outline heights, and a mirrored copy takes them
/// from the same field read back through the same transform.
/// </summary>
public sealed class SketchReliefTests
{
    // One island, one square shape, and whatever relief the caller wants over it.
    private static string Layout(string? relief, bool mirrors = false, string mirrorMode = "rot_180") => $$"""
    {
      "setup": { "mirror_mode": "{{mirrorMode}}", "center": { "cx": 40, "cz": 10 } },
      "layout": {
        "shapes": [
          { "id": "s1", "type": "rectangle", "operation": "add",
            "min_x": 0, "min_z": 0, "max_x": 20, "max_z": 20, "base_height": 3, "floor": 0 }
        ],
        "islands": [ { "id": "i1", "name": "board", "mirrors": {{(mirrors ? "true" : "false")}}, "shapeIds": ["s1"] } ]
      }
      {{(relief is null ? "" : $", \"relief\": {relief}")}}
    }
    """;

    private static Dictionary<(int X, int Z), int> Tops(string layoutJson)
    {
        var tops = new Dictionary<(int X, int Z), int>();
        foreach (var (x, z, _, top) in SketchRasterizer.RasterizeColumns(layoutJson))
            if (!tops.TryGetValue((x, z), out var current) || top > current) tops[(x, z)] = top;
        return tops;
    }

    [Test]
    public async Task Without_a_relief_a_shape_keeps_its_own_height()
    {
        var tops = Tops(Layout(null));
        await Assert.That(tops[(10, 10)]).IsEqualTo(3);
    }

    [Test]
    public async Task A_relief_replaces_the_islands_column_tops()
    {
        const string relief = """
        { "i1": { "base": 5, "marks": [ { "kind": "point", "at": [10, 10], "h": 12, "r": 3 } ] } }
        """;
        var tops = Tops(Layout(relief));

        // One mark and no rim decides the whole surface, so the island stands at the height that was stated —
        // not the 3 the shape's own base_height asked for.
        await Assert.That(tops[(10, 10)]).IsEqualTo(12);
        await Assert.That(tops[(2, 2)]).IsEqualTo(12);
    }

    [Test]
    public async Task A_rim_keeps_the_hill_inside_the_shape()
    {
        const string relief = """
        {
          "i1": {
            "base": 5,
            "marks": [
              { "kind": "rim",   "h": 5, "depth": 1 },
              { "kind": "point", "at": [10, 10], "h": 14, "r": 3 }
            ]
          }
        }
        """;
        var tops = Tops(Layout(relief));

        await Assert.That(tops[(10, 10)]).IsEqualTo(14);
        await Assert.That(tops[(0, 0)]).IsEqualTo(5);
        await Assert.That(tops[(19, 19)]).IsEqualTo(5);
    }

    [Test]
    public async Task A_relief_never_pushes_a_column_below_its_own_floor()
    {
        // A relief says where the ground is, not how thick the slab under it is — but a surface at or under
        // the floor would be a column of no height at all.
        const string relief = """
        { "i1": { "base": 0, "marks": [ { "kind": "point", "at": [10, 10], "h": -4, "r": 3 } ] } }
        """;
        var tops = Tops(Layout(relief));
        await Assert.That(tops[(10, 10)]).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task A_mirrored_island_carries_the_same_relief_cell_for_cell()
    {
        // The mirror copy reads the island's own solved surface back through the same transform, so the two
        // sides are identical by construction rather than to within a second solve's tolerance.
        const string relief = """
        {
          "i1": {
            "base": 5,
            "grain": { "amplitude": 1.5, "scale": 7, "seed": 4 },
            "marks": [
              { "kind": "rim",   "h": 5, "depth": 1 },
              { "kind": "point", "at": [6, 6], "h": 15, "r": 3 }
            ]
          }
        }
        """;
        var tops = Tops(Layout(relief, mirrors: true, mirrorMode: "mirror_x"));

        // The centre is cx 40, so a cell at x mirrors to 79 - x.
        var worst = 0;
        for (var x = 0; x <= 20; x++)
            for (var z = 0; z <= 20; z++)
                if (tops.TryGetValue((x, z), out var here) && tops.TryGetValue((79 - x, z), out var there))
                    worst = Math.Max(worst, Math.Abs(here - there));

        await Assert.That(worst).IsEqualTo(0);
    }

    [Test]
    public async Task A_relief_naming_no_island_places_nothing()
    {
        const string relief = """
        { "somewhere-else": { "base": 5, "marks": [ { "kind": "point", "at": [10, 10], "h": 12, "r": 3 } ] } }
        """;
        var tops = Tops(Layout(relief));
        await Assert.That(tops[(10, 10)]).IsEqualTo(3);
    }

    [Test]
    public async Task The_preview_reads_the_same_field_the_build_does()
    {
        // The property the whole preview rests on: what an author sees on the canvas is the surface that
        // gets built, not a second computation that agrees with it most of the time.
        const string relief = """
        {
          "i1": {
            "base": 5,
            "grain": { "amplitude": 1.1, "scale": 6, "seed": 3 },
            "marks": [ { "kind": "rim", "h": 5 }, { "kind": "point", "at": [10, 10], "h": 16, "r": 3 } ]
          }
        }
        """;
        var layout = Layout(relief);
        var field = SketchRasterizer.ReliefFields(layout)["i1"];
        var tops = Tops(layout);

        foreach (var (x, z) in field.Footprint.Land())
            await Assert.That(tops[(x, z)]).IsEqualTo(field.At(x, z));
    }

    [Test]
    public async Task A_layer_offset_moves_the_previewed_field_with_the_ground()
    {
        // A relief on a stacked layer is solved in layer-local Y, so a preview reading it raw would draw a
        // sky bridge's contours down at the ground's levels.
        const string layout = """
        {
          "setup": { "mirror_mode": "rot_180", "center": { "cx": 40, "cz": 10 } },
          "layers": [
            { "id": "l1", "base_y": 30, "layout": {
              "shapes": [ { "id": "s1", "type": "rectangle", "operation": "add",
                            "min_x": 0, "min_z": 0, "max_x": 20, "max_z": 20, "base_height": 3, "floor": 0 } ],
              "islands": [ { "id": "i1", "mirrors": false, "shapeIds": ["s1"] } ] } }
          ],
          "relief": { "i1": { "base": 5, "marks": [ { "kind": "point", "at": [10, 10], "h": 12, "r": 3 } ] } }
        }
        """;
        var field = SketchRasterizer.ReliefFields(layout)["i1"];
        await Assert.That(field.At(10, 10)).IsEqualTo(42);
        await Assert.That(field.Continuous[field.Footprint.Index(10, 10)]).IsBetween(41.5, 42.5);
    }

    [Test]
    public async Task A_layout_with_no_relief_previews_nothing()
    {
        await Assert.That(SketchRasterizer.ReliefFields(Layout(null)).Count).IsEqualTo(0);
    }

    [Test]
    public async Task A_half_written_mark_is_dropped_rather_than_guessed_at()
    {
        // A point with no position cannot place terrain, and inventing one would put ground where nobody
        // asked for it.
        var mark = new ReliefMarkJson { Kind = "point", Heights = [9] };
        await Assert.That(mark.ToMark()).IsNull();

        var line = new ReliefMarkJson { Kind = "line", Points = [[0, 0], [4, 4]], Heights = [7, 9] };
        await Assert.That(line.ToMark()).IsNotNull();
    }
}
