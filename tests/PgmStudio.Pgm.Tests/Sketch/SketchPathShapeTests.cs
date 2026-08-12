using PgmStudio.Pgm.Sketch;

namespace PgmStudio.Pgm.Tests.Sketch;

/// <summary>
/// The draw-phase path primitive. A path is the one shape stored as something other than its own outline —
/// an open centerline plus a half-width — so the thing under test is that the band those imply reaches every
/// consumer that expects a ring: the footprint, the column, and the mirror image.
/// </summary>
public sealed class SketchPathShapeTests
{
    // A straight east-west path across a plate, so the band's width is a column count and not an estimate.
    private static string Layout(double radius, string? edge = null, double baseHeight = 3, bool mirrors = false) => $$"""
    {
      "setup": { "mirror_mode": "mirror_x", "center": { "cx": 40, "cz": 40 } },
      "layout": {
        "shapes": [
          { "id": "plate", "type": "rectangle", "operation": "add",
            "min_x": 0, "min_z": 0, "max_x": 80, "max_z": 80, "base_height": 2, "floor": 0 },
          { "id": "road", "type": "path", "operation": "add",
            "vertices": [[10, 40], [40, 40], [70, 40]],
            "radius": {{radius}}, "base_height": {{baseHeight}}, "floor": 0
            {{(edge is null ? "" : $", \"path_edge\": \"{edge}\", \"path_seed\": 7")}} }
        ],
        "islands": [ { "id": "i1", "mirrors": {{(mirrors ? "true" : "false")}}, "shapeIds": ["plate", "road"] } ]
      }
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
    public async Task A_path_covers_the_band_around_its_centerline()
    {
        // The centerline is what was clicked; the footprint is the band. A path that rasterized as its line
        // would be one cell wide, and one that rasterized as nothing would leave the plate showing.
        var tops = Tops(Layout(radius: 3));

        await Assert.That(tops[(40, 40)]).IsEqualTo(3);   // on the line
        await Assert.That(tops[(40, 38)]).IsEqualTo(3);   // inside the band
        await Assert.That(tops[(40, 42)]).IsEqualTo(3);
        await Assert.That(tops[(40, 34)]).IsEqualTo(2);   // past it, the plate
        await Assert.That(tops[(40, 46)]).IsEqualTo(2);
    }

    [Test]
    public async Task The_band_is_as_wide_as_the_radius_says()
    {
        // Twice the radius, to the block: the width is the authored number rather than whatever the smoothing
        // happens to leave.
        var narrow = Tops(Layout(radius: 2));
        var wide   = Tops(Layout(radius: 6));

        var narrowWidth = Enumerable.Range(0, 80).Count(z => narrow.GetValueOrDefault((40, z)) == 3);
        var wideWidth   = Enumerable.Range(0, 80).Count(z => wide.GetValueOrDefault((40, z)) == 3);

        await Assert.That(narrowWidth).IsEqualTo(4);
        await Assert.That(wideWidth).IsEqualTo(12);
    }

    [Test]
    public async Task A_path_carries_its_own_thickness_like_any_other_shape()
    {
        // The band is a footprint like a rectangle's, so the height fields mean on a path what they mean
        // everywhere else — a raised causeway is a path with a thickness, not a new kind of shape.
        var tops = Tops(Layout(radius: 3, baseHeight: 9));

        await Assert.That(tops[(40, 40)]).IsEqualTo(9);
        await Assert.That(tops[(40, 34)]).IsEqualTo(2);
    }

    [Test]
    public async Task A_rough_edge_wanders_the_band_without_losing_it()
    {
        // The two edges that vary a width must still produce a band: a rough path reads a noise field, and a
        // solid one of the same radius is the baseline it wanders around.
        var solid = Tops(Layout(radius: 4));
        var rough = Tops(Layout(radius: 4, edge: "rough"));

        var solidCells = solid.Count(c => c.Value == 3);
        var roughCells = rough.Count(c => c.Value == 3);

        await Assert.That(roughCells).IsGreaterThan(0);
        await Assert.That(roughCells).IsNotEqualTo(solidCells);
    }

    [Test]
    public async Task A_mirrored_path_lands_on_its_own_image()
    {
        // A reflection reverses handedness, so a band re-derived on the far side would swap which edge a
        // varying width was drawn on. Mirroring the band itself is what keeps the two halves the same map.
        var tops = Tops(Layout(radius: 4, edge: "rough", mirrors: true));

        var differing = 0;
        for (var x = 0; x < 80; x++)
            for (var z = 0; z < 80; z++)
            {
                var image = 79 - x;
                if (tops.GetValueOrDefault((x, z)) != tops.GetValueOrDefault((image, z))) differing++;
            }

        await Assert.That(differing).IsEqualTo(0);
    }

    [Test]
    public async Task A_path_of_one_point_is_no_path()
    {
        // A route needs somewhere to go. One click leaves nothing to build a band around, and the plate must
        // come through untouched rather than the rasterizer throwing on it.
        const string Stub = """
        {
          "layout": {
            "shapes": [
              { "id": "plate", "type": "rectangle", "operation": "add",
                "min_x": 0, "min_z": 0, "max_x": 20, "max_z": 20, "base_height": 2, "floor": 0 },
              { "id": "road", "type": "path", "operation": "add",
                "vertices": [[10, 10]], "radius": 3, "base_height": 9, "floor": 0 }
            ],
            "islands": [ { "id": "i1", "mirrors": false, "shapeIds": ["plate", "road"] } ]
          }
        }
        """;

        var tops = Tops(Stub);

        await Assert.That(tops.Count).IsEqualTo(400);
        await Assert.That(tops.Values.Distinct().ToList()).IsEquivalentTo(new List<int> { 2 });
    }
}
